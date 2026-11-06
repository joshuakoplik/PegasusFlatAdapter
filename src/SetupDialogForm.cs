using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ASCOM.PegasusFlatPanel
{
    /// <summary>
    /// Setup: pick the Pegasus ASCOM Switch driver, list its channels, pick the PWM output.
    /// Built in code (no designer file) to keep the project small.
    /// </summary>
    internal sealed class SetupDialogForm : Form
    {
        private readonly TextBox _progId = new TextBox { ReadOnly = true, Width = 300 };
        private readonly Button _choose = new Button { Text = "Choose...", AutoSize = true };
        private readonly Button _load = new Button { Text = "List outputs", AutoSize = true };
        private readonly ListBox _channels = new ListBox { Width = 460, Height = 200, HorizontalScrollbar = true };
        private readonly Label _status = new Label { AutoSize = true, MaximumSize = new Size(460, 0) };
        private readonly CheckBox _offOnDisconnect = new CheckBox { Text = "Turn the panel off when disconnecting", AutoSize = true };
        private readonly CheckBox _trace = new CheckBox { Text = "Trace log (Documents\\ASCOM\\Logs)", AutoSize = true };
        private readonly Button _ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80 };
        private readonly Button _cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
        private readonly Button _testOn = new Button { Text = "Test: full on", AutoSize = true };
        private readonly Button _testOff = new Button { Text = "Test: off", AutoSize = true };

        private readonly DriverSettings _original;

        public DriverSettings Result { get; private set; }

        public SetupDialogForm(DriverSettings current)
        {
            _original = current;
            Text = CoverCalibrator.DriverDescription + " Setup";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(10);
            AcceptButton = _ok;
            CancelButton = _cancel;

            var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
            layout.Controls.Add(new Label { Text = "1. Pegasus ASCOM Switch driver:", AutoSize = true });
            layout.Controls.Add(Row(_progId, _choose));
            layout.Controls.Add(new Label { Text = "2. PWM output the flat panel is plugged into (dew heater / adjustable port):", AutoSize = true, Margin = new Padding(0, 10, 0, 0) });
            layout.Controls.Add(Row(_load, _testOn, _testOff));
            layout.Controls.Add(_channels);
            layout.Controls.Add(_status);
            layout.Controls.Add(_offOnDisconnect);
            layout.Controls.Add(_trace);
            var buttons = Row(_ok, _cancel);
            buttons.Anchor = AnchorStyles.Right;
            layout.Controls.Add(buttons);
            Controls.Add(layout);

            _progId.Text = current.SwitchProgId;
            _offOnDisconnect.Checked = current.TurnOffOnDisconnect;
            _trace.Checked = current.TraceEnabled;
            if (current.IsConfigured)
                _status.Text = $"Current output: {current.SwitchIndex} '{current.SwitchName}'. Click 'List outputs' to change it.";
            else
                _status.Text = "Choose the Pegasus Switch driver, then click 'List outputs'.";

            _choose.Click += (s, e) => ChooseDriver();
            _load.Click += (s, e) => LoadChannels();
            _testOn.Click += (s, e) => Test(true);
            _testOff.Click += (s, e) => Test(false);
            _ok.Click += OnOk;
        }

        private static FlowLayoutPanel Row(params Control[] controls)
        {
            var row = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            row.Controls.AddRange(controls);
            return row;
        }

        private void ChooseDriver()
        {
            try
            {
                string chosen = SwitchClient.ChooseSwitchDriver(_progId.Text);
                if (chosen == null) return;
                if (chosen == CoverCalibrator.DriverProgId) { Error("Choose the Pegasus Switch driver, not this driver."); return; }
                _progId.Text = chosen;
                _channels.Items.Clear();
                LoadChannels();
            }
            catch (Exception ex) { Error(ex.Message); }
        }

        private void LoadChannels()
        {
            if (string.IsNullOrWhiteSpace(_progId.Text)) { Error("Choose the Switch driver first."); return; }
            Cursor = Cursors.WaitCursor;
            try
            {
                List<SwitchInfo> all = WithSwitch(sw => sw.GetAll(), out string deviceName);
                _channels.Items.Clear();
                foreach (var info in all) _channels.Items.Add(info);

                // Re-select the configured channel, otherwise the first writable variable one.
                SwitchInfo pick =
                    all.FirstOrDefault(i => i.Index == _original.SwitchIndex && i.Name == _original.SwitchName && _progId.Text == _original.SwitchProgId)
                    ?? all.FirstOrDefault(i => i.CanWrite && i.IsVariable && i.Name.IndexOf("dew", StringComparison.OrdinalIgnoreCase) >= 0)
                    ?? all.FirstOrDefault(i => i.CanWrite && i.IsVariable);
                if (pick != null) _channels.SelectedItem = pick;

                _status.ForeColor = SystemColors.ControlText;
                _status.Text = $"{deviceName}: {all.Count} outputs. Pick a writable one with a value range (a PWM output). " +
                               "Turn off Auto-Dew for that port in the Pegasus software, or it will override the brightness.";
            }
            catch (Exception ex) { Error("Could not read the outputs: " + ex.Message); }
            finally { Cursor = Cursors.Default; }
        }

        private void Test(bool on)
        {
            if (!(_channels.SelectedItem is SwitchInfo info)) { Error("List the outputs and select one first."); return; }
            try
            {
                WithSwitch(sw => { sw.SetValue(info.Index, on ? info.Max : info.Min); return 0; }, out _);
                _status.ForeColor = SystemColors.ControlText;
                _status.Text = $"Set '{info.Name}' to {(on ? info.Max : info.Min)}.";
            }
            catch (Exception ex) { Error("Test failed: " + ex.Message); }
        }

        private T WithSwitch<T>(Func<SwitchClient, T> action, out string deviceName)
        {
            using (var sw = new SwitchClient(_progId.Text))
            {
                bool wasConnected = sw.Connected;
                if (!wasConnected) sw.Connected = true;
                try
                {
                    deviceName = sw.DeviceName;
                    return action(sw);
                }
                finally
                {
                    if (!wasConnected) { try { sw.Connected = false; } catch { } }
                }
            }
        }

        private void OnOk(object sender, EventArgs e)
        {
            var result = new DriverSettings
            {
                SwitchProgId = _progId.Text,
                SwitchIndex = _original.SwitchIndex,
                SwitchName = _original.SwitchName,
                TurnOffOnDisconnect = _offOnDisconnect.Checked,
                TraceEnabled = _trace.Checked,
            };

            if (_channels.SelectedItem is SwitchInfo info)
            {
                if (!info.CanWrite) { Error($"'{info.Name}' is read only. Pick a writable output."); DialogResult = DialogResult.None; return; }
                result.SwitchIndex = info.Index;
                result.SwitchName = info.Name;
            }
            else if (result.SwitchProgId != _original.SwitchProgId)
            {
                Error("Click 'List outputs' and select the PWM output for the new driver.");
                DialogResult = DialogResult.None;
                return;
            }

            Result = result;
        }

        private void Error(string message)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = message;
        }
    }
}
