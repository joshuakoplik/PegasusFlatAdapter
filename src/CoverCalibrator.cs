using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ASCOM.DeviceInterface;

namespace ASCOM.PegasusFlatPanel
{
    /// <summary>
    /// ASCOM CoverCalibrator driver that turns a PWM (dew heater / adjustable) output on a
    /// Pegasus Astro power box into a flat panel. It does not talk to the hardware itself:
    /// it calls the existing Pegasus ASCOM Switch driver, so the Pegasus software keeps
    /// owning the serial port and can be used by other programs at the same time.
    ///
    /// Calibrator only: there is no cover, so CoverState is NotPresent and
    /// Open/Close/HaltCover throw MethodNotImplementedException as the ASCOM spec requires.
    /// </summary>
    [ComVisible(true)]
    [Guid("d9242698-489b-4cac-ac85-642faff1351f")]
    [ProgId(DriverProgId)]
    [ClassInterface(ClassInterfaceType.None)]
    public class CoverCalibrator : ICoverCalibratorV2, IDisposable
    {
        public const string DriverProgId = "ASCOM.PegasusFlatPanel.CoverCalibrator";
        public const string DriverDescription = "Pegasus PWM Flat Panel";
        private const string DriverVersionString = "1.0";

        private readonly object _lock = new object();
        private readonly Log _log;
        private DriverSettings _settings;

        private SwitchClient _switch;
        private SwitchInfo _channel;
        private BrightnessMapping _mapping;
        private bool _connected;

        public CoverCalibrator()
        {
            _settings = DriverSettings.Load();
            _log = new Log(_settings.TraceEnabled);
            _log.Write("Driver instance created");
        }

        #region Common properties and methods

        public void SetupDialog()
        {
            if (IsConnected)
            {
                MessageBox.Show("Disconnect before changing the setup.", DriverDescription,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var form = new SetupDialogForm(_settings))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    _settings = form.Result;
                    _settings.Save();
                    _log.Enabled = _settings.TraceEnabled;
                }
            }
        }

        public ArrayList SupportedActions => new ArrayList();

        public string Action(string actionName, string actionParameters) =>
            throw new ActionNotImplementedException($"Action {actionName} is not implemented by this driver");

        public void CommandBlind(string command, bool raw) => throw new MethodNotImplementedException("CommandBlind");
        public bool CommandBool(string command, bool raw) => throw new MethodNotImplementedException("CommandBool");
        public string CommandString(string command, bool raw) => throw new MethodNotImplementedException("CommandString");

        public bool Connected
        {
            get => IsConnected;
            set
            {
                if (value) DoConnect(); else DoDisconnect();
            }
        }

        // ICoverCalibratorV2 asynchronous connection. The connection is quick, so it is done
        // synchronously and Connecting is always false when these return.
        public void Connect() => DoConnect();
        public void Disconnect() => DoDisconnect();
        public bool Connecting => false;

        public string Description => DriverDescription;

        public string DriverInfo =>
            $"{DriverDescription} v{DriverVersionString}. Drives a flat panel from a Pegasus Astro PWM output via the Pegasus ASCOM Switch driver" +
            (_settings.IsConfigured ? $" ({_settings.SwitchProgId}, channel {_settings.SwitchIndex} '{_settings.SwitchName}')." : " (not configured).");

        public string DriverVersion => DriverVersionString;

        public short InterfaceVersion => 2;

        public string Name => DriverDescription;

        public IStateValueCollection DeviceState
        {
            get
            {
                var state = new StateValueCollection();
                if (!IsConnected) return state;
                try
                {
                    state.Add(nameof(Brightness), Brightness);
                    state.Add(nameof(CalibratorState), CalibratorState);
                    state.Add(nameof(CalibratorChanging), CalibratorChanging);
                    state.Add(nameof(CoverState), CoverState);
                    state.Add(nameof(CoverMoving), CoverMoving);
                    state.AddUtcDateTime();
                }
                catch (Exception ex) { _log.Write("DeviceState: " + ex.Message); }
                return state;
            }
        }

        public void Dispose()
        {
            try { if (IsConnected) DoDisconnect(); } catch { }
            _log.Dispose();
        }

        #endregion

        #region Calibrator

        public int MaxBrightness
        {
            get { lock (_lock) { CheckConnected(); return _mapping.MaxBrightness; } }
        }

        public int Brightness
        {
            get
            {
                lock (_lock)
                {
                    CheckConnected();
                    return _mapping.ToBrightness(ReadValue());
                }
            }
        }

        public CalibratorStatus CalibratorState
        {
            get
            {
                lock (_lock)
                {
                    CheckConnected();
                    try
                    {
                        return _mapping.ToBrightness(_switch.GetValue(_channel.Index)) > 0
                            ? CalibratorStatus.Ready
                            : CalibratorStatus.Off;
                    }
                    catch (Exception ex)
                    {
                        _log.Write("CalibratorState read failed: " + ex.Message);
                        return CalibratorStatus.Error;
                    }
                }
            }
        }

        // PWM changes are immediate, so the calibrator is never "warming up".
        public bool CalibratorChanging
        {
            get { CheckConnected(); return false; }
        }

        public void CalibratorOn(int brightness)
        {
            lock (_lock)
            {
                CheckConnected();
                if (brightness < 0 || brightness > _mapping.MaxBrightness)
                    throw new InvalidValueException("CalibratorOn", brightness.ToString(), $"0 to {_mapping.MaxBrightness}");

                double value = _mapping.ToSwitchValue(brightness);
                _log.Write($"CalibratorOn({brightness}) -> SetSwitchValue({_channel.Index}, {value})");
                WriteValue(value);
            }
        }

        public void CalibratorOff()
        {
            lock (_lock)
            {
                CheckConnected();
                _log.Write($"CalibratorOff -> SetSwitchValue({_channel.Index}, {_mapping.Min})");
                WriteValue(_mapping.Min);
            }
        }

        #endregion

        #region Cover (not present)

        public CoverStatus CoverState
        {
            get { CheckConnected(); return CoverStatus.NotPresent; }
        }

        public bool CoverMoving
        {
            get { CheckConnected(); return false; }
        }

        public void OpenCover() => throw new MethodNotImplementedException("OpenCover - this device has no cover");
        public void CloseCover() => throw new MethodNotImplementedException("CloseCover - this device has no cover");
        public void HaltCover() => throw new MethodNotImplementedException("HaltCover - this device has no cover");

        #endregion

        #region Connection handling

        private bool IsConnected
        {
            get { lock (_lock) return _connected; }
        }

        private void DoConnect()
        {
            lock (_lock)
            {
                if (_connected) return;

                _settings = DriverSettings.Load();
                _log.Enabled = _settings.TraceEnabled;
                if (!_settings.IsConfigured)
                    throw new DriverException("The driver is not configured. Open Setup, choose the Pegasus Switch driver and the PWM output to use.");

                _log.Write($"Connecting to {_settings.SwitchProgId}");
                var sw = new SwitchClient(_settings.SwitchProgId);
                try
                {
                    sw.Connected = true;
                    SwitchInfo channel = FindChannel(sw);
                    if (!channel.CanWrite)
                        throw new DriverException($"Switch channel {channel.Index} '{channel.Name}' is read only.");

                    _mapping = new BrightnessMapping(channel.Min, channel.Max, channel.Step);
                    _channel = channel;
                    _switch = sw;
                    _connected = true;
                    _log.Write($"Connected. Channel {channel}; MaxBrightness={_mapping.MaxBrightness}");
                }
                catch (Exception ex)
                {
                    _log.Write("Connect failed: " + ex.Message);
                    try { sw.Connected = false; } catch { }
                    sw.Dispose();
                    if (ex is DriverException) throw;
                    throw new DriverException("Could not connect to the Pegasus Switch driver: " + ex.Message, ex);
                }
            }
        }

        private void DoDisconnect()
        {
            lock (_lock)
            {
                if (!_connected) return;
                try
                {
                    if (_settings.TurnOffOnDisconnect)
                    {
                        _log.Write("Turning panel off on disconnect");
                        try { _switch.SetValue(_channel.Index, _mapping.Min); }
                        catch (Exception ex) { _log.Write("Turn off on disconnect failed: " + ex.Message); }
                    }
                    try { _switch.Connected = false; } catch (Exception ex) { _log.Write("Switch disconnect: " + ex.Message); }
                }
                finally
                {
                    _switch?.Dispose();
                    _switch = null;
                    _channel = null;
                    _mapping = null;
                    _connected = false;
                    _log.Write("Disconnected");
                }
            }
        }

        /// <summary>
        /// Finds the configured channel. The index is tried first; if the name stored at
        /// setup no longer matches (e.g. the Pegasus driver changed its switch layout),
        /// the channel is looked up by name instead.
        /// </summary>
        private SwitchInfo FindChannel(SwitchClient sw)
        {
            int count = sw.MaxSwitch;
            if (_settings.SwitchIndex < count)
            {
                SwitchInfo atIndex = sw.GetInfo(_settings.SwitchIndex);
                if (string.IsNullOrEmpty(_settings.SwitchName) ||
                    string.Equals(atIndex.Name, _settings.SwitchName, StringComparison.OrdinalIgnoreCase))
                    return atIndex;
            }

            if (!string.IsNullOrEmpty(_settings.SwitchName))
            {
                for (int i = 0; i < count; i++)
                {
                    SwitchInfo info = sw.GetInfo(i);
                    if (string.Equals(info.Name, _settings.SwitchName, StringComparison.OrdinalIgnoreCase))
                    {
                        _log.Write($"Channel '{info.Name}' moved from index {_settings.SwitchIndex} to {i}");
                        return info;
                    }
                }
            }

            throw new DriverException(
                $"Switch channel {_settings.SwitchIndex} '{_settings.SwitchName}' was not found on {_settings.SwitchProgId}. Re-run Setup.");
        }

        private void CheckConnected()
        {
            if (!IsConnected) throw new NotConnectedException(DriverDescription + " is not connected");
        }

        private double ReadValue()
        {
            try { return _switch.GetValue(_channel.Index); }
            catch (Exception ex) { throw new DriverException("Reading the Pegasus PWM output failed: " + ex.Message, ex); }
        }

        private void WriteValue(double value)
        {
            try { _switch.SetValue(_channel.Index, value); }
            catch (Exception ex) { throw new DriverException("Setting the Pegasus PWM output failed: " + ex.Message, ex); }
        }

        #endregion

        #region COM / ASCOM registration

        [ComRegisterFunction]
        public static void RegisterAscom(Type t)
        {
            dynamic profile = DriverSettings.CreateProfile();
            try { profile.Register(DriverProgId, DriverDescription); }
            finally { DriverSettings.Release((object)profile); }
        }

        [ComUnregisterFunction]
        public static void UnregisterAscom(Type t)
        {
            dynamic profile = DriverSettings.CreateProfile();
            try { profile.Unregister(DriverProgId); }
            finally { DriverSettings.Release((object)profile); }
        }

        #endregion
    }
}
