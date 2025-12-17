using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
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
    private readonly CheckBox _ctrlCheck;
    private readonly CheckBox _altCheck;
    private readonly CheckBox _shiftCheck;
    private readonly CheckBox _winCheck;
    private readonly ComboBox _keyCombo;
    private readonly CheckBox _startupCheck;
    private readonly CheckBox _ddcCheck;
    private readonly Dictionary<int, string> _monitorIdByIndex = new();

    public SettingsForm(AppSettings settings, IReadOnlyList<MonitorInfo> monitors)
    {
        _settings = settings.Clone();
        Text = "Monitors Focus Settings";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Padding = new Padding(12)
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
        AddRow(layout, "Delay (seconds):", _delayInput);

        _opacityTrack = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            Value = _settings.OverlayOpacity,
            Width = 180
        };
        _opacityValue = new Label
        {
            Text = $"{_settings.OverlayOpacity}%",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _opacityTrack.ValueChanged += (_, _) => _opacityValue.Text = $"{_opacityTrack.Value}%";
        var opacityPanel = new FlowLayoutPanel { AutoSize = true };
        opacityPanel.Controls.Add(_opacityTrack);
        opacityPanel.Controls.Add(_opacityValue);
        AddRow(layout, "Overlay opacity:", opacityPanel);

        _monitorList = new CheckedListBox
        {
            CheckOnClick = true,
            Height = 100,
            Width = 260
        };
        PopulateMonitors(monitors);
        AddRow(layout, "Controlled monitors:", _monitorList);

        _ctrlCheck = new CheckBox { Text = "Ctrl", Checked = _settings.Hotkey.Ctrl, AutoSize = true };
        _altCheck = new CheckBox { Text = "Alt", Checked = _settings.Hotkey.Alt, AutoSize = true };
        _shiftCheck = new CheckBox { Text = "Shift", Checked = _settings.Hotkey.Shift, AutoSize = true };
        _winCheck = new CheckBox { Text = "Win", Checked = _settings.Hotkey.Win, AutoSize = true };

        _keyCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 120
        };
        foreach (var key in GetAllowedHotkeys())
        {
            _keyCombo.Items.Add(key);
        }
        _keyCombo.SelectedItem = _settings.Hotkey.Key;

        var hotkeyPanel = new FlowLayoutPanel { AutoSize = true };
        hotkeyPanel.Controls.Add(_ctrlCheck);
        hotkeyPanel.Controls.Add(_altCheck);
        hotkeyPanel.Controls.Add(_shiftCheck);
        hotkeyPanel.Controls.Add(_winCheck);
        hotkeyPanel.Controls.Add(_keyCombo);
        AddRow(layout, "Hotkey:", hotkeyPanel);

        _startupCheck = new CheckBox
        {
            Text = "Launch on startup",
            Checked = _settings.LaunchOnStartup,
            AutoSize = true
        };
        AddRow(layout, "Startup:", _startupCheck);

        _ddcCheck = new CheckBox
        {
            Text = "Enable DDC/CI (coming soon)",
            Checked = _settings.EnableDdcCi,
            Enabled = false,
            AutoSize = true
        };
        AddRow(layout, "Brightness:", _ddcCheck);

        var buttonPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        var saveButton = new Button { Text = "Save", DialogResult = DialogResult.OK };
        saveButton.Click += (_, _) => OnSave();
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(cancelButton);
        AddRow(layout, string.Empty, buttonPanel);

        Controls.Add(layout);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    public AppSettings? ResultSettings { get; private set; }

    private void PopulateMonitors(IReadOnlyList<MonitorInfo> monitors)
    {
        _monitorList.Items.Clear();
        _monitorIdByIndex.Clear();

        var useDefault = _settings.ControlledMonitorIds.Count == 0;
        for (var i = 0; i < monitors.Count; i++)
        {
            var monitor = monitors[i];
            var label = monitor.IsPrimary ? $"{monitor.Id} (Primary)" : monitor.Id;
            var index = _monitorList.Items.Add(label);
            _monitorIdByIndex[index] = monitor.Id;

            var shouldCheck = useDefault ? !monitor.IsPrimary : _settings.ControlledMonitorIds.Contains(monitor.Id);
            _monitorList.SetItemChecked(index, shouldCheck);
        }
    }

    private void OnSave()
    {
        if (_keyCombo.SelectedItem is not Keys selectedKey)
        {
            MessageBox.Show(this, "Please choose a hotkey.", "Monitors Focus", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        _settings.DelaySeconds = (int)_delayInput.Value;
        _settings.OverlayOpacity = _opacityTrack.Value;
        _settings.LaunchOnStartup = _startupCheck.Checked;
        _settings.EnableDdcCi = _ddcCheck.Checked;
        _settings.Hotkey.Ctrl = _ctrlCheck.Checked;
        _settings.Hotkey.Alt = _altCheck.Checked;
        _settings.Hotkey.Shift = _shiftCheck.Checked;
        _settings.Hotkey.Win = _winCheck.Checked;
        _settings.Hotkey.Key = selectedKey;

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
            MessageBox.Show(this, "Select at least one monitor.", "Monitors Focus", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }
        _settings.ControlledMonitorIds = selectedIds;

        _settings.Normalize();
        ResultSettings = _settings;
    }

    private static IEnumerable<Keys> GetAllowedHotkeys()
    {
        for (var key = Keys.F1; key <= Keys.F12; key++)
        {
            yield return key;
        }

        for (var key = Keys.A; key <= Keys.Z; key++)
        {
            yield return key;
        }
    }

    private static void AddRow(TableLayoutPanel layout, string labelText, Control control)
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
    }
}
