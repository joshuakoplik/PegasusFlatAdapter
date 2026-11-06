using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ASCOM.PegasusFlatPanel
{
    /// <summary>Description of one channel exposed by the underlying ASCOM Switch driver.</summary>
    internal sealed class SwitchInfo
    {
        public int Index;
        public string Name;
        public string Description;
        public bool CanWrite;
        public double Min, Max, Step;

        public bool IsVariable => Max - Min > Step && Step > 0;

        public override string ToString() =>
            $"{Index}: {Name}  [{Min}..{Max} step {Step}]{(CanWrite ? "" : "  (read only)")}";
    }

    /// <summary>
    /// Late-bound client for the Pegasus Astro ASCOM Switch driver (or any ASCOM Switch driver).
    /// Late binding means the driver works with whichever Pegasus driver / ASCOM Platform
    /// version is installed, including Alpaca devices exposed through ASCOM dynamic clients.
    /// </summary>
    internal sealed class SwitchClient : IDisposable
    {
        private dynamic _switch;
        public string ProgId { get; }

        public SwitchClient(string progId)
        {
            ProgId = progId;
            Type t = Type.GetTypeFromProgID(progId)
                     ?? throw new DriverException($"Switch driver '{progId}' is not registered. Re-run Setup and choose the Pegasus Switch driver.");
            _switch = Activator.CreateInstance(t);
        }

        public bool Connected
        {
            get => (bool)_switch.Connected;
            set => _switch.Connected = value;
        }

        public string DeviceName
        {
            get { try { return (string)_switch.Name; } catch { return ProgId; } }
        }

        public int MaxSwitch => (int)(short)_switch.MaxSwitch;

        public SwitchInfo GetInfo(int index)
        {
            short id = (short)index;
            var info = new SwitchInfo { Index = index };
            info.Name = SafeString(() => (string)_switch.GetSwitchName(id), $"Switch {index}");
            info.Description = SafeString(() => (string)_switch.GetSwitchDescription(id), "");
            info.CanWrite = (bool)_switch.CanWrite(id);
            info.Min = (double)_switch.MinSwitchValue(id);
            info.Max = (double)_switch.MaxSwitchValue(id);
            info.Step = (double)_switch.SwitchStep(id);
            return info;
        }

        public List<SwitchInfo> GetAll()
        {
            var list = new List<SwitchInfo>();
            int n = MaxSwitch;
            for (int i = 0; i < n; i++)
            {
                try { list.Add(GetInfo(i)); }
                catch (Exception ex) { list.Add(new SwitchInfo { Index = i, Name = $"Switch {i} (error: {ex.Message})" }); }
            }
            return list;
        }

        public double GetValue(int index) => (double)_switch.GetSwitchValue((short)index);

        public void SetValue(int index, double value) => _switch.SetSwitchValue((short)index, value);

        public void SetSwitch(int index, bool state) => _switch.SetSwitch((short)index, state);

        public void Dispose()
        {
            if (_switch == null) return;
            try { _switch.Dispose(); } catch { /* not every driver implements Dispose */ }
            if (Marshal.IsComObject((object)_switch)) Marshal.ReleaseComObject((object)_switch);
            _switch = null;
        }

        private static string SafeString(Func<string> f, string fallback)
        {
            try { return f() ?? fallback; } catch { return fallback; }
        }

        /// <summary>Shows the ASCOM Chooser for Switch devices. Returns null if cancelled.</summary>
        public static string ChooseSwitchDriver(string current)
        {
            Type t = Type.GetTypeFromProgID("ASCOM.Utilities.Chooser")
                     ?? throw new DriverException("The ASCOM Platform is not installed (ASCOM.Utilities.Chooser not found).");
            dynamic chooser = Activator.CreateInstance(t);
            try
            {
                chooser.DeviceType = "Switch";
                string chosen = (string)chooser.Choose(current ?? "");
                return string.IsNullOrEmpty(chosen) ? null : chosen;
            }
            finally { DriverSettings.Release((object)chooser); }
        }
    }
}
