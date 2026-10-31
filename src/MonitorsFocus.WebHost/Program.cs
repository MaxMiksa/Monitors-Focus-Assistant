using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using MonitorsFocus.Core;

namespace MonitorsFocus.WebHost;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var activation = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\MonitorsFocus.Panel.Activate." + Environment.UserName);
        using var instance = new Mutex(true, @"Local\MonitorsFocus.Panel." + Environment.UserName, out var created);
        if (!created) { activation.Set(); return; }
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var window = new PanelWindow(args.Contains("--dev"));
        var waiter = ThreadPool.RegisterWaitForSingleObject(activation, (_, _) =>
        {
            try
            {
                if (window.IsHandleCreated) window.BeginInvoke(() => { window.WindowState = FormWindowState.Normal; window.Activate(); });
            }
            catch (InvalidOperationException) { /* Window closed while an activation was in flight. */ }
        }, null, Timeout.Infinite, false);
        try { Application.Run(window); }
        finally { waiter.Unregister(null); }
    }
}

internal sealed class PanelWindow : Form
{
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _requests = new(1, 1);
    private readonly bool _dev;
    private string Origin => _dev ? "http://127.0.0.1:5173" : "https://monitors-focus.example";
    private sealed record Message(string Id, ControlRequest Request);
    private static readonly HashSet<string> Actions = new()
        { "snapshot", "brightness", "temperature", "restore-color", "link", "scene", "pause", "resume-plan", "rescan", "save" };

    public PanelWindow(bool dev)
    {
        _dev = dev;
        Text = "Monitors Focus";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(680, 510);
        MinimumSize = new Size(600, 460);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(247, 249, 252);
        Controls.Add(_web);
        Shown += async (_, _) => { ResizePanel(false); await InitializeAsync(); };
        FormClosed += (_, _) => { _lifetime.Cancel(); _web.Dispose(); };
    }

    private async Task InitializeAsync()
    {
        try
        {
            StartCore();
            var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Monitors-Focus", "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: data);
            if (_lifetime.IsCancellationRequested) return;
            await _web.EnsureCoreWebView2Async(environment);
            var core = _web.CoreWebView2;
            core.Settings.AreDevToolsEnabled = _dev;
            core.Settings.AreDefaultContextMenusEnabled = _dev;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.NavigationStarting += (_, e) => e.Cancel = !IsTrusted(e.Uri);
            core.WebMessageReceived += Receive;
            if (!_dev)
            {
                var assets = Path.Combine(AppContext.BaseDirectory, "wwwroot");
                if (!File.Exists(Path.Combine(assets, "index.html"))) throw new IOException("界面文件缺失，请先运行完整构建。");
                core.SetVirtualHostNameToFolderMapping("monitors-focus.example", assets, CoreWebView2HostResourceAccessKind.DenyCors);
            }
            core.Navigate(Origin + (_dev ? "/" : "/index.html"));
        }
        catch (Exception ex) when (!_lifetime.IsCancellationRequested)
        { MessageBox.Show(this, ex.Message, "界面启动失败"); Close(); }
    }

    private bool IsTrusted(string source) => Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && uri.GetLeftPart(UriPartial.Authority) == Origin;

    private async void Receive(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!IsTrusted(e.Source)) return;
        Message? message = null;
        try
        {
            message = JsonSerializer.Deserialize<Message>(e.WebMessageAsJson);
            if (message == null || string.IsNullOrEmpty(message.Id)) return;
            if (message.Request.Action == "window")
            {
                ResizePanel(message.Request.Value == 1);
                return;
            }
            if (!Actions.Contains(message.Request.Action)) throw new ArgumentException("不支持的界面操作。");
            await _requests.WaitAsync(_lifetime.Token);
            ControlSnapshot snapshot;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(8));
                snapshot = await ControlProtocol.SendAsync(message.Request, timeout.Token);
            }
            finally { _requests.Release(); }
            if (!_lifetime.IsCancellationRequested)
                _web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { message.Id, Snapshot = snapshot }));
        }
        catch (Exception ex)
        {
            if (message != null && !_lifetime.IsCancellationRequested)
                _web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { message.Id, Error = ex.Message }));
        }
    }

    private void ResizePanel(bool settings)
    {
        var scale = DeviceDpi / 96f;
        var available = Screen.FromControl(this).WorkingArea;
        var frameHeight = Height - ClientSize.Height;
        ClientSize = new Size(Math.Min((int)((settings ? 1040 : 680) * scale), available.Width - 32),
            Math.Min((int)((settings ? 720 : 510) * scale), available.Height - frameHeight - 32));
        Left = Math.Clamp(Left, available.Left, Math.Max(available.Left, available.Right - Width));
        Top = Math.Clamp(Top, available.Top, Math.Max(available.Top, available.Bottom - Height));
    }

    private static void StartCore()
    {
        var executable = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "core", "MonitorsFocus.exe"));
        if (!File.Exists(executable))
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "MonitorsFocus.sln"))) root = root.Parent;
            if (root != null) executable = Path.Combine(root.FullName, "src", "MonitorsFocus", "bin", "Release", "net10.0-windows", "MonitorsFocus.exe");
        }
        if (!File.Exists(executable)) throw new IOException("控制核心缺失，请先运行完整构建。");
        Process.Start(new ProcessStartInfo(executable, "--center --background") { UseShellExecute = false, CreateNoWindow = true });
    }
}
