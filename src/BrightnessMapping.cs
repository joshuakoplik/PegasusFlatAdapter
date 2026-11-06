using System;

namespace ASCOM.PegasusFlatPanel
{
    /// <summary>
    /// Maps CoverCalibrator brightness steps (0..MaxBrightness) onto the value range of
    /// an ASCOM Switch channel (MinSwitchValue..MaxSwitchValue in SwitchStep increments).
    /// Pure logic, no COM, so it can be unit tested anywhere.
    /// </summary>
    public sealed class BrightnessMapping
    {
        /// <summary>Upper bound used when a switch reports a step of 0 or a silly number of steps.</summary>
        public const int FallbackMaxBrightness = 100;
        private const int MaxReasonableSteps = 65535;

        public double Min { get; }
        public double Max { get; }
        public double Step { get; }
        public int MaxBrightness { get; }

        public BrightnessMapping(double min, double max, double step)
        {
            if (double.IsNaN(min) || double.IsNaN(max) || max <= min)
                throw new ArgumentException($"Switch range is invalid (min={min}, max={max}).");

            Min = min;
            Max = max;

            double steps = step > 0 ? (max - min) / step : double.NaN;
            if (step > 0 && steps >= 1 && steps <= MaxReasonableSteps)
            {
                Step = step;
                MaxBrightness = (int)Math.Round(steps);
            }
            else
            {
                // Continuous or oddly described channel: expose 0..100 and interpolate.
                MaxBrightness = FallbackMaxBrightness;
                Step = (max - min) / FallbackMaxBrightness;
            }
        }

        /// <summary>Switch value to write for a given brightness.</summary>
        public double ToSwitchValue(int brightness)
        {
            if (brightness < 0 || brightness > MaxBrightness)
                throw new ArgumentOutOfRangeException(nameof(brightness), brightness,
                    $"Brightness must be between 0 and {MaxBrightness}.");
            double value = Min + brightness * Step;
            return Math.Min(Max, Math.Max(Min, value));
        }

        /// <summary>Brightness that corresponds to a value read back from the switch.</summary>
        public int ToBrightness(double switchValue)
        {
            if (double.IsNaN(switchValue)) return 0;
            int b = (int)Math.Round((switchValue - Min) / Step);
            return Math.Min(MaxBrightness, Math.Max(0, b));
        }
    }
}
