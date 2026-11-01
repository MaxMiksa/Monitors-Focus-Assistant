using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MonitorsFocus.Brightness;
using MonitorsFocus.Core;
using MonitorsFocus.Monitoring;
using MonitorsFocus.Settings;

namespace MonitorsFocus.UI;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly CheckedListBox _monitorList;
    private readonly NumericUpDown _delayInput;
    private readonly TrackBar _opacityTrack;
    private readonly Label _opacityValue;
    private readonly CheckBox _startupCheck;
    private readonly CheckBox _hardwareDimCheck;
    private readonly TrackBar _hardwareDimTrack;
    private readonly Label _hardwareDimValue;
    private readonly CheckedListBox _hardwareDimOverrideList;
    private readonly ListView _hardwareStatusList;
    private readonly Button _rescanButton;
    private readonly ComboBox _dimmingModeCombo;
    private readonly ComboBox _languageCombo;
    private readonly TextBox _logBox;
    private readonly Label _hardwareNote;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;
    private bool _applyingLanguage;
    private readonly Dictionary<string, Label> _labels = new();
    private readonly IReadOnlyList<MonitorInfo> _monitors;
    private string T(string key) => Localization.Text(key, _settings.Language.Preference());
    private readonly Dictionary<int, string> _monitorIdByIndex = new();
    private readonly Dictionary<int, string> _hardwareOverrideIdByIndex = new();
    private readonly IReadOnlyDictionary<string, MonitorsFocus.Brightness.HardwareDimmer.Capability> _capabilities;

    public SettingsForm(AppSettings settings, IReadOnlyList<MonitorInfo> monitors, IReadOnlyDictionary<string, HardwareDimmer.Capability> capabilities)
    {
        _settings = settings.Clone();
        _monitors = monitors;
        _capabilities = capabilities;
        Text = "Monitors Focus Settings";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        ShowInTaskbar = false;
        AutoSize = false;
        AutoScaleMode = AutoScaleMode.Font;
        MinimumSize = new Size(900, 700);
        Size = new Size(_settings.LastWindowWidth, _settings.LastWindowHeight);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoScroll = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _delayInput = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 600,
            Value = _settings.DelaySeconds,
            Width = 80
        };
        AddRow(layout, "delay", "Delay (seconds):", _delayInput);

        _opacityTrack = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            Value = _settings.OverlayOpacity,
            Width = 220
        };
        _opacityValue = new Label
        {
            Text = $"{_settings.OverlayOpacity}%",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            MinimumSize = new Size(60, 24)
        };
        _opacityTrack.ValueChanged += (_, _) => _opacityValue.Text = $"{_opacityTrack.Value}%";
        var opacityPanel = new FlowLayoutPanel { AutoSize = true };
        opacityPanel.Controls.Add(_opacityTrack);
        opacityPanel.Controls.Add(_opacityValue);
        AddRow(layout, "opacity", "Overlay opacity:", opacityPanel);

        _monitorList = new CheckedListBox
        {
            CheckOnClick = true,
            Height = 100,
            Width = 260
        };
        PopulateMonitors(monitors);
        AddRow(layout, "monitors", "Controlled monitors:", _monitorList);

        _startupCheck = new CheckBox
        {
            Text = "Launch on startup",
            Checked = _settings.LaunchOnStartup,
            AutoSize = true
        };
        AddRow(layout, "startup", "Startup:", _startupCheck);

        _languageCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 160
        };
        PopulateLanguageOptions();
        _languageCombo.SelectedIndexChanged += LanguageChanged;
        AddRow(layout, "language", "Language:", _languageCombo);

        _dimmingModeCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 220
        };
        PopulateDimmingModeOptions();
        AddRow(layout, "dimmingMode", "Dimming mode:", _dimmingModeCombo);

        _hardwareDimCheck = new CheckBox
        {
            Text = "Enable hardware dimming (DDC/CI)",
            Checked = _settings.EnableDdcCi,
            AutoSize = true
        };

        _hardwareDimTrack = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            Value = _settings.HardwareDimLevel,
            Width = 220
        };
        _hardwareDimValue = new Label
        {
            Text = $"{_settings.HardwareDimLevel}%",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            MinimumSize = new Size(60, 24)
        };
        _hardwareDimTrack.ValueChanged += (_, _) => _hardwareDimValue.Text = $"{_hardwareDimTrack.Value}%";
        _hardwareDimCheck.CheckedChanged += (_, _) => ToggleHardwareControls();
        var hardwarePanel = new FlowLayoutPanel { AutoSize = true };
        hardwarePanel.Controls.Add(_hardwareDimCheck);
        hardwarePanel.Controls.Add(_hardwareDimTrack);
        hardwarePanel.Controls.Add(_hardwareDimValue);
        AddRow(layout, "hardwareDimming", "Hardware dimming:", hardwarePanel);

        _hardwareDimOverrideList = new CheckedListBox
        {
            CheckOnClick = true,
            Height = 100,
            Width = 260
        };
        PopulateHardwareOverrideList(monitors);
        AddRow(layout, "hardwareDisable", "Disable hardware dimming on:", _hardwareDimOverrideList);

        _hardwareStatusList = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Width = 520,
            Height = 160
        };
        _hardwareStatusList.Columns.Add("Monitor", 160);
        _hardwareStatusList.Columns.Add("Status", 100);
        _hardwareStatusList.Columns.Add("Message", 140);
        PopulateHardwareStatus(monitors);
        AddRow(layout, "hardwareDiagnostics", "Hardware diagnostics:", _hardwareStatusList);

        _rescanButton = new Button
        {
            Text = "Rescan (applies on save)",
            MinimumSize = new Size(140, 32),
            AutoSize = true
        };
        _rescanButton.Click += (_, _) => MessageBox.Show(this, T("保存设置后将重新检测硬件调光。"), "Monitors Focus", MessageBoxButtons.OK, MessageBoxIcon.Information);
        AddRow(layout, "rescan", string.Empty, _rescanButton);

        _hardwareNote = new Label
        {
            Text = "Hardware dimming uses DDC/CI or WMI to reduce backlight on supported displays. Benefits: lowers backlight load and heat; if unsupported, overlay is used.",
            MaximumSize = new Size(520, 0),
            AutoSize = true
        };
        AddRow(layout, "note", "Note:", _hardwareNote);

        ToggleHardwareControls();

        var buttonPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        _saveButton = new Button { Text = "Save", DialogResult = DialogResult.OK, MinimumSize = new Size(90, 32), AutoSize = true };
        _saveButton.Click += (_, _) => OnSave();
        _cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, MinimumSize = new Size(90, 32), AutoSize = true };
        buttonPanel.Controls.Add(_saveButton);
        buttonPanel.Controls.Add(_cancelButton);
        AddRow(layout, "buttonsRow", string.Empty, buttonPanel);

        Controls.Add(layout);
        AcceptButton = _saveButton;
        CancelButton = _cancelButton;

        _logBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Bottom,
            Height = 160
        };
        Controls.Add(_logBox);

        LoadLogs();
        LogSink.LogAppended += OnLogAppended;
        ApplyLanguage(_settings.Language);
    }

    public AppSettings? ResultSettings { get; private set; }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        LogSink.LogAppended -= OnLogAppended;
        _settings.LastWindowWidth = Width;
        _settings.LastWindowHeight = Height;
        base.OnFormClosed(e);
    }

    private void PopulateMonitors(IReadOnlyList<MonitorInfo> monitors)
    {
        _monitorList.Items.Clear();
        _monitorIdByIndex.Clear();

        var useDefault = _settings.ControlledMonitorIds.Count == 0;
        for (var i = 0; i < monitors.Count; i++)
        {
            var monitor = monitors[i];
            var label = MonitorLabel(monitor.Id);
            var index = _monitorList.Items.Add(label);
            _monitorIdByIndex[index] = monitor.Id;

            var shouldCheck = useDefault ? !monitor.IsPrimary : _settings.ControlledMonitorIds.Contains(monitor.Id);
            _monitorList.SetItemChecked(index, shouldCheck);
        }
    }

    private void OnSave()
    {
        _settings.DelaySeconds = (int)_delayInput.Value;
        _settings.OverlayOpacity = _opacityTrack.Value;
        _settings.LaunchOnStartup = _startupCheck.Checked;
        _settings.EnableDdcCi = _hardwareDimCheck.Checked;
        _settings.HardwareDimLevel = _hardwareDimTrack.Value;

        var selectedIds = new List<string>();
        foreach (var index in _monitorList.CheckedIndices.Cast<int>())
        {
            if (_monitorIdByIndex.TryGetValue(index, out var id))
            {
                selectedIds.Add(id);
            }
        }
        if (selectedIds.Count == 0)
        {
            MessageBox.Show(this, T("请至少选择一块显示器。"), "Monitors Focus", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }
        _settings.ControlledMonitorIds = selectedIds;

        var disabledHardware = new List<string>();
        foreach (var index in _hardwareDimOverrideList.CheckedIndices.Cast<int>())
        {
            if (_hardwareOverrideIdByIndex.TryGetValue(index, out var id))
            {
                disabledHardware.Add(id);
            }
        }
        _settings.HardwareDimDisabledMonitorIds = disabledHardware;
        if (_dimmingModeCombo.SelectedItem is Option<DimmingMode> modeOpt)
        {
            _settings.DimmingMode = modeOpt.Value;
        }
        if (_languageCombo.SelectedItem is Option<UiLanguage> langOpt)
        {
            _settings.Language = langOpt.Value;
        }

        _settings.Normalize();
        ResultSettings = _settings;
    }

    private void ToggleHardwareControls()
    {
        if (_hardwareDimTrack == null || _hardwareDimValue == null || _hardwareDimOverrideList == null || _hardwareStatusList == null || _rescanButton == null)
        {
            return;
        }
        var enabled = _hardwareDimCheck.Checked;
        _hardwareDimTrack.Enabled = enabled;
        _hardwareDimValue.Enabled = enabled;
        _hardwareDimOverrideList.Enabled = enabled;
        _hardwareStatusList.Enabled = enabled;
        _rescanButton.Enabled = enabled;
    }

    private void LoadLogs()
    {
        var lines = LogSink.Snapshot();
        _logBox.Lines = lines;
    }

    private void OnLogAppended(string line)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => OnLogAppended(line)));
            return;
        }

        _logBox.AppendText(line + Environment.NewLine);
    }

    private void PopulateLanguageOptions()
    {
        _languageCombo.Items.Clear();
        _languageCombo.Items.Add(new Option<UiLanguage>(UiLanguage.System, T("跟随系统")));
        _languageCombo.Items.Add(new Option<UiLanguage>(UiLanguage.English, "English"));
        _languageCombo.Items.Add(new Option<UiLanguage>(UiLanguage.Chinese, "中文"));
        var selected = _languageCombo.Items.Cast<Option<UiLanguage>>().FirstOrDefault(o => o.Value == _settings.Language);
        _languageCombo.SelectedItem = selected ?? _languageCombo.Items[0];
    }

    private UiLanguage GetSelectedLanguage()
    {
        if (_languageCombo.SelectedItem is Option<UiLanguage> opt)
        {
            return opt.Value;
        }
        return UiLanguage.English;
    }

    private void PopulateDimmingModeOptions()
    {
        var current = _dimmingModeCombo.SelectedItem is Option<DimmingMode> option ? option.Value : _settings.DimmingMode;
        _dimmingModeCombo.Items.Clear();
        string Text(DimmingMode mode) => T(mode switch
        {
            DimmingMode.AutoPreferHardware => "自动（优先硬件）",
            DimmingMode.OverlayOnly => "仅遮罩",
            DimmingMode.HardwareOnly => "仅硬件",
            _ => mode.ToString()
        });

        _dimmingModeCombo.Items.Add(new Option<DimmingMode>(DimmingMode.AutoPreferHardware, Text(DimmingMode.AutoPreferHardware)));
        _dimmingModeCombo.Items.Add(new Option<DimmingMode>(DimmingMode.OverlayOnly, Text(DimmingMode.OverlayOnly)));
        _dimmingModeCombo.Items.Add(new Option<DimmingMode>(DimmingMode.HardwareOnly, Text(DimmingMode.HardwareOnly)));
        var selected = _dimmingModeCombo.Items.Cast<Option<DimmingMode>>().FirstOrDefault(o => o.Value == current);
        _dimmingModeCombo.SelectedItem = selected ?? _dimmingModeCombo.Items[0];
    }

    private void ApplyLanguage(UiLanguage lang)
    {
        if (_applyingLanguage)
        {
            return;
        }
        _applyingLanguage = true;
        _settings.Language = lang;
        var t = TextKeys.ToDictionary(x => x.Key, x => T(x.Value));
        Text = t["title"];
        foreach (var kv in _labels)
        {
            if (t.TryGetValue(kv.Key, out var txt))
            {
                kv.Value.Text = txt;
            }
        }

        _hardwareNote.Text = t["hardwareNote"];
        _hardwareDimCheck.Text = t["hardwareDimCheck"];
        _startupCheck.Text = t["startupCheck"];

        _rescanButton.Text = t["rescanBtn"];
        _saveButton.Text = t["saveBtn"];
        _cancelButton.Text = t["cancelBtn"];

        PopulateDimmingModeOptions();
        PopulateLanguageOptions();

        if (_dimmingModeCombo.Items.Count > 0 && _dimmingModeCombo.SelectedIndex < 0)
        {
            _dimmingModeCombo.SelectedIndex = 0;
        }

        _applyingLanguage = true;
        _languageCombo.SelectedIndexChanged -= LanguageChanged;
        var selectedLang = _languageCombo.Items.Cast<Option<UiLanguage>>().FirstOrDefault(o => o.Value == lang);
        if (selectedLang != null)
        {
            _languageCombo.SelectedItem = selectedLang;
        }
        _languageCombo.SelectedIndexChanged += LanguageChanged;
        _applyingLanguage = false;

        _hardwareStatusList.Columns[0].Text = t["colMonitor"];
        _hardwareStatusList.Columns[1].Text = t["colStatus"];
        _hardwareStatusList.Columns[2].Text = t["colMessage"];
        foreach (var (index, id) in _monitorIdByIndex)
            _monitorList.Items[index] = MonitorLabel(id);
        foreach (var (index, id) in _hardwareOverrideIdByIndex)
            _hardwareDimOverrideList.Items[index] = MonitorLabel(id);
        PopulateHardwareStatus(_monitors);
        _applyingLanguage = false;
    }

    private static readonly Dictionary<string, string> TextKeys = new()
    {
        ["title"] = "Monitors Focus 设置",
        ["delay"] = "延迟（秒）：",
        ["opacity"] = "遮罩透明度：",
        ["monitors"] = "受控显示器：",
        ["startup"] = "开机自启：",
        ["language"] = "语言：",
        ["dimmingMode"] = "调光模式：",
        ["hardwareDimming"] = "硬件调光：",
        ["hardwareDisable"] = "禁用硬件调光的显示器：",
        ["hardwareDiagnostics"] = "硬件诊断：",
        ["note"] = "说明：",
        ["hardwareNote"] = "硬件调光通过 DDC/CI 或 WMI 降低背光，减轻背光负载与热量；若不支持则使用遮罩。",
        ["hardwareDimCheck"] = "启用硬件调光（DDC/CI）",
        ["startupCheck"] = "开机自启",
        ["rescanBtn"] = "重新检测（保存后生效）",
        ["saveBtn"] = "保存",
        ["cancelBtn"] = "取消",
        ["colMonitor"] = "显示器",
        ["colStatus"] = "状态",
        ["colMessage"] = "信息",
    };

    private sealed class Option<T>
    {
        public Option(T value, string text)
        {
            Value = value;
            Text = text;
        }

        public T Value { get; }
        public string Text { get; }
        public override string ToString() => Text;
    }

    private void PopulateHardwareOverrideList(IReadOnlyList<MonitorInfo> monitors)
    {
        _hardwareDimOverrideList.Items.Clear();
        _hardwareOverrideIdByIndex.Clear();
        for (var i = 0; i < monitors.Count; i++)
        {
            var monitor = monitors[i];
            var label = MonitorLabel(monitor.Id);
            var index = _hardwareDimOverrideList.Items.Add(label);
            _hardwareOverrideIdByIndex[index] = monitor.Id;

            var shouldDisable = _settings.HardwareDimDisabledMonitorIds.Contains(monitor.Id);
            _hardwareDimOverrideList.SetItemChecked(index, shouldDisable);
        }
    }

    private string MonitorLabel(string id) => _monitors.Any(m => m.Id == id && m.IsPrimary)
        ? $"{id} ({T("主显示器")})" : id;

    private void PopulateHardwareStatus(IReadOnlyList<MonitorInfo> monitors)
    {
        _hardwareStatusList.Items.Clear();
        foreach (var monitor in monitors)
        {
            var status = T("未知");
            var message = string.Empty;
            if (_capabilities.TryGetValue(monitor.Id, out var cap))
            {
                status = T(cap.Status switch
                {
                    HardwareDimmer.DimmingStatus.Detecting => "检测中",
                    HardwareDimmer.DimmingStatus.Supported => "支持",
                    HardwareDimmer.DimmingStatus.Unsupported => "不支持",
                    HardwareDimmer.DimmingStatus.Failed => "失败",
                    HardwareDimmer.DimmingStatus.DisabledBySettings => "已在设置中禁用",
                    _ => "未知"
                });
                message = Localization.Message(cap.Message, _settings.Language.Preference()) ?? string.Empty;
            }

            var item = new ListViewItem(new[] { monitor.Id, status, message });
            _hardwareStatusList.Items.Add(item);
        }
    }

    private void AddRow(TableLayoutPanel layout, string key, string labelText, Control control)
    {
        var rowIndex = layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 6, 8, 6)
        };
        control.Margin = new Padding(0, 4, 0, 4);
        layout.Controls.Add(label, 0, rowIndex);
        layout.Controls.Add(control, 1, rowIndex);
        _labels[key] = label;
    }
    private void LanguageChanged(object? sender, EventArgs e)
    {
        if (_applyingLanguage)
        {
            return;
        }
        ApplyLanguage(GetSelectedLanguage());
    }

}
