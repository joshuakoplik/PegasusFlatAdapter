using System;

namespace ASCOM.PegasusFlatPanel
{
    /// <summary>
    /// Persistent driver settings, stored in the ASCOM Profile under this driver's ProgID.
    /// The ASCOM Profile is used late-bound so the driver needs no compile-time reference
    /// to ASCOM.Utilities (it is always installed with the ASCOM Platform).
    /// </summary>
    internal sealed class DriverSettings
    {
        private const string KeySwitchProgId = "SwitchProgID";
        private const string KeySwitchIndex = "SwitchIndex";
        private const string KeySwitchName = "SwitchName";
        private const string KeyOffOnDisconnect = "TurnOffOnDisconnect";
        private const string KeyTrace = "TraceEnabled";

        /// <summary>ProgID of the underlying Pegasus ASCOM Switch driver.</summary>
        public string SwitchProgId { get; set; } = "";

        /// <summary>Index of the PWM channel on that Switch driver.</summary>
        public int SwitchIndex { get; set; } = -1;

        /// <summary>Name the channel had when it was chosen, used to re-find it if indexes move.</summary>
        public string SwitchName { get; set; } = "";

        /// <summary>Turn the panel off when the client disconnects.</summary>
        public bool TurnOffOnDisconnect { get; set; } = true;

        public bool TraceEnabled { get; set; }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(SwitchProgId) && SwitchIndex >= 0;

        public static DriverSettings Load()
        {
            var s = new DriverSettings();
            dynamic profile = CreateProfile();
            try
            {
                string id = CoverCalibrator.DriverProgId;
                s.SwitchProgId = (string)profile.GetValue(id, KeySwitchProgId, "", "");
                s.SwitchName = (string)profile.GetValue(id, KeySwitchName, "", "");
                if (int.TryParse((string)profile.GetValue(id, KeySwitchIndex, "", "-1"), out int idx))
                    s.SwitchIndex = idx;
                if (bool.TryParse((string)profile.GetValue(id, KeyOffOnDisconnect, "", "True"), out bool off))
                    s.TurnOffOnDisconnect = off;
                if (bool.TryParse((string)profile.GetValue(id, KeyTrace, "", "False"), out bool trace))
                    s.TraceEnabled = trace;
            }
            finally { Release(profile); }
            return s;
        }

        public void Save()
        {
            dynamic profile = CreateProfile();
            try
            {
                string id = CoverCalibrator.DriverProgId;
                profile.WriteValue(id, KeySwitchProgId, SwitchProgId ?? "", "");
                profile.WriteValue(id, KeySwitchIndex, SwitchIndex.ToString(), "");
                profile.WriteValue(id, KeySwitchName, SwitchName ?? "", "");
                profile.WriteValue(id, KeyOffOnDisconnect, TurnOffOnDisconnect.ToString(), "");
                profile.WriteValue(id, KeyTrace, TraceEnabled.ToString(), "");
            }
            finally { Release(profile); }
        }

        internal static dynamic CreateProfile()
        {
            Type t = Type.GetTypeFromProgID("ASCOM.Utilities.Profile")
                     ?? throw new DriverException("The ASCOM Platform is not installed (ASCOM.Utilities.Profile not found).");
            dynamic profile = Activator.CreateInstance(t);
            profile.DeviceType = "CoverCalibrator";
            return profile;
        }

        internal static void Release(object comObject)
        {
            if (comObject != null && System.Runtime.InteropServices.Marshal.IsComObject(comObject))
            {
                try { if (comObject is IDisposable d) d.Dispose(); } catch { }
                System.Runtime.InteropServices.Marshal.ReleaseComObject(comObject);
            }
        }
    }
}
