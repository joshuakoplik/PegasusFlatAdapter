using System;
using ASCOM.PegasusFlatPanel;
using Xunit;

public class BrightnessMappingTests
{
    [Fact]
    public void Percent_channel_maps_one_to_one()
    {
        var m = new BrightnessMapping(0, 100, 1);
        Assert.Equal(100, m.MaxBrightness);
        Assert.Equal(0, m.ToSwitchValue(0));
        Assert.Equal(42, m.ToSwitchValue(42));
        Assert.Equal(100, m.ToSwitchValue(100));
        Assert.Equal(42, m.ToBrightness(42));
    }

    [Fact]
    public void Byte_pwm_channel_exposes_255_steps()
    {
        var m = new BrightnessMapping(0, 255, 1);
        Assert.Equal(255, m.MaxBrightness);
        Assert.Equal(128, m.ToBrightness(128));
    }

    [Fact]
    public void Boolean_channel_is_on_off()
    {
        var m = new BrightnessMapping(0, 1, 1);
        Assert.Equal(1, m.MaxBrightness);
        Assert.Equal(1, m.ToSwitchValue(1));
        Assert.Equal(0, m.ToBrightness(0));
    }

    [Fact]
    public void Coarse_step_is_respected()
    {
        var m = new BrightnessMapping(0, 100, 5);
        Assert.Equal(20, m.MaxBrightness);
        Assert.Equal(50, m.ToSwitchValue(10));
        Assert.Equal(10, m.ToBrightness(51));
    }

    [Fact]
    public void Zero_step_falls_back_to_100_levels()
    {
        var m = new BrightnessMapping(3, 12, 0);
        Assert.Equal(100, m.MaxBrightness);
        Assert.Equal(3, m.ToSwitchValue(0), 6);
        Assert.Equal(12, m.ToSwitchValue(100), 6);
        Assert.Equal(50, m.ToBrightness(7.5));
    }

    [Fact]
    public void Readback_is_clamped()
    {
        var m = new BrightnessMapping(0, 100, 1);
        Assert.Equal(0, m.ToBrightness(-5));
        Assert.Equal(100, m.ToBrightness(150));
    }

    [Fact]
    public void Out_of_range_brightness_throws()
    {
        var m = new BrightnessMapping(0, 100, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => m.ToSwitchValue(101));
        Assert.Throws<ArgumentOutOfRangeException>(() => m.ToSwitchValue(-1));
    }

    [Fact]
    public void Invalid_range_throws()
    {
        Assert.Throws<ArgumentException>(() => new BrightnessMapping(10, 10, 1));
    }
}
