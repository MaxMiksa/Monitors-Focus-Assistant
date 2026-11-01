using System.Diagnostics;
using System.Windows.Forms;
using MonitorsFocus;
using MonitorsFocus.Monitoring;

internal static class WindowDetectionChecks
{
    // Separate process so the detector's exclusion of our own dimming overlays remains active.
    public static async Task RunAsync()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        var start = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true
        };
        if (Path.GetFileNameWithoutExtension(start.FileName) == "dotnet")
            start.ArgumentList.Add(typeof(WindowDetectionChecks).Assembly.Location);
        start.ArgumentList.Add("--window-fixture");
        using var child = Process.Start(start)!;
        try
        {
            var window = new IntPtr(long.Parse((await child.StandardOutput.ReadLineAsync())!));
            using var manager = new MonitorManager();
            var monitors = manager.Monitors.ToArray();
            foreach (var monitor in monitors)
            {
                await Command($"full|{monitor.Id}");
                await Expect(monitor.Id);
                if (!FullscreenWindowDetector.FindDisplays(monitors).Contains(monitor.Id))
                    throw new Exception("Window enumeration missed the full-screen fixture.");
                await Command($"max|{monitor.Id}");
                await Expect(monitor.Id);
                await Command("normal");
                await Expect(null);
                await Command("min");
                await Expect(null);
                await Command($"full|{monitor.Id}");
                await Expect(monitor.Id);
                await Command("hide");
                await Expect(null);
            }

            async Task Command(string command)
            {
                await child.StandardInput.WriteLineAsync(command);
                await child.StandardInput.FlushAsync();
                if (await child.StandardOutput.ReadLineAsync() != "done") throw new Exception("Window fixture failed.");
            }
            async Task Expect(string? id)
            {
                var deadline = Environment.TickCount64 + 1000;
                while (FullscreenWindowDetector.FindDisplay(window, monitors) != id)
                {
                    if (Environment.TickCount64 >= deadline) throw new Exception($"Incorrect native detection; expected {id ?? "no display"}.");
                    await Task.Delay(20);
                }
            }
        }
        finally
        {
            if (!child.HasExited)
            {
                child.StandardInput.Close();
                try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(1)); }
                catch (TimeoutException) { child.Kill(); await child.WaitForExitAsync(); }
            }
        }
    }

    public static void RunFixture()
    {
        var thread = new Thread(() =>
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            using var form = new Form { Text = "Monitors Focus window detection check", ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual, Bounds = new(40, 40, 320, 180) };
            form.Shown += (_, _) =>
            {
                Console.WriteLine(form.Handle.ToInt64());
                _ = Task.Run(async () =>
                {
                    while (await Console.In.ReadLineAsync() is { } command)
                    {
                        form.BeginInvoke(() =>
                        {
                            var parts = command.Split('|');
                            if (parts.Length > 1)
                            {
                                form.WindowState = FormWindowState.Normal;
                                form.FormBorderStyle = parts[0] == "full" ? FormBorderStyle.None : FormBorderStyle.Sizable;
                                var screen = Screen.AllScreens.Single(s => s.DeviceName == parts[1]);
                                form.Bounds = parts[0] == "full" ? screen.Bounds : new(screen.Bounds.X + 40, screen.Bounds.Y + 40, 320, 180);
                                form.Show();
                                if (parts[0] == "max") form.WindowState = FormWindowState.Maximized;
                            }
                            else if (command == "normal") { form.WindowState = FormWindowState.Normal; form.Bounds = new(form.Left + 40, form.Top + 40, 320, 180); }
                            else if (command == "min") form.WindowState = FormWindowState.Minimized;
                            else if (command == "hide") form.Hide();
                            Console.WriteLine("done");
                        });
                    }
                    form.BeginInvoke(() => form.Close());
                });
            };
            Application.Run(form);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
