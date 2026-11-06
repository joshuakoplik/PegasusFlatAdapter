# Pegasus PWM Flat Panel – ASCOM CoverCalibrator wrapper

This is an ASCOM **CoverCalibrator** driver that drives a flat panel from a PWM output
(a dew heater port, or the adjustable output) on a Pegasus Astro power box.

It never talks to the Pegasus hardware itself. It calls the existing **Pegasus ASCOM
Switch driver**, so the Pegasus software keeps the serial port and NINA, SGP and the
other apps can still use the power box's switches at the same time.

| CoverCalibrator call | What happens on the Pegasus Switch driver |
|---|---|
| `CalibratorOn(b)` | `SetSwitchValue(channel, min + b × step)` |
| `CalibratorOff()` | `SetSwitchValue(channel, min)` |
| `Brightness` | `GetSwitchValue(channel)` converted back to a brightness step |
| `CalibratorState` | `Ready` if the brightness is above 0, otherwise `Off` (`Error` if the read fails) |
| `MaxBrightness` | `(MaxSwitchValue − MinSwitchValue) / SwitchStep` for the channel, e.g. 100 for a 0–100 % port or 255 for 0–255 |
| `CoverState` | Always `NotPresent`. `OpenCover`, `CloseCover` and `HaltCover` throw `MethodNotImplementedException`, as the ASCOM spec requires for a device with no cover |

The driver implements `ICoverCalibratorV2` (ASCOM Platform 7) and also works with
Platform 6.6 clients.

## Build

You need Visual Studio 2022 or the .NET SDK on Windows:

```bash
dotnet build src/PegasusFlatPanel.csproj -c Release
```

The output goes to `src\bin\Release\net48\`. Keep the `ASCOM.*.dll` files next to the driver DLL.

To build the installer as well, install [NSIS](https://nsis.sourceforge.io) and run
`build-installer.cmd`. On Linux, `makensis installer/PegasusFlatPanel.nsi` works the same
after the `dotnet build`. The output is `installer\PegasusFlatPanelSetup-1.0.0.exe`.

### GitHub Actions

`.github/workflows/build.yml` runs the tests and builds the driver and the installer on every
push to any branch. Download the installer from the run's **Artifacts** section on the
Actions tab.

To publish a release, push a version tag. The workflow then creates a GitHub Release with
`PegasusFlatPanelSetup-<version>.exe` attached:

```bash
git tag v1.1.0 && git push origin v1.1.0
```

Tags must look like `v1.2.3`.

## Install

1. Install the ASCOM Platform and the Pegasus Astro ASCOM drivers (Unity or the stand-alone driver for your box).
2. Run `PegasusFlatPanelSetup-1.0.0.exe` (it asks for Administrator rights). It checks that
   .NET Framework 4.8 and the ASCOM Platform are present, copies the driver to
   `Common Files\ASCOM\CoverCalibrator\PegasusFlatPanel`, and registers it for 32-bit and
   64-bit apps (NINA is 64-bit). It also adds an entry to *Apps & features* for uninstalling.

   Without the installer: run `install.cmd` from an **Administrator** command prompt. It does
   the same registration with both RegAsm versions, using the build output folder.
3. In your imaging app, open the ASCOM Chooser for *Cover Calibrator / Flat Panel*, pick
   **Pegasus PWM Flat Panel**, and click **Properties**:
   * **Choose…** opens the ASCOM Switch chooser. Pick the Pegasus power box driver.
   * **List outputs** connects to it and lists every switch with its range. Select the PWM port
     the panel is plugged into (for example *Dew A* or *Dew Heater 1*). It must be writable and
     have a value range, not just on/off.
   * Use **Test: full on** and **Test: off** to check you picked the right port.
   * **Turn the panel off when disconnecting** is on by default.

To remove the driver, uninstall it from *Apps & features*, or run `uninstall.cmd` as Administrator if you used `install.cmd`.

## Notes

* **Turn off Auto-Dew** for that port in the Pegasus software. If it stays on, the firmware
  keeps changing the PWM level and overrides the brightness you set.
* A dew port is plain PWM at the box's input voltage. That suits a 12 V EL or LED panel with no
  controller of its own. A panel with its own dimmer electronics may flicker or ignore the PWM.
* The driver saves both the channel index and the channel name. If a Pegasus driver update
  re-orders the switches, it finds the channel again by name when it connects.
* The trace log (enable it in Setup) goes to `Documents\ASCOM\Logs yyyy-MM-dd\`.

## Layout

```
src/CoverCalibrator.cs     ASCOM CoverCalibrator COM class + ASCOM Profile registration
src/SwitchClient.cs        late-bound client for the Pegasus ASCOM Switch driver
src/BrightnessMapping.cs   brightness <-> switch value conversion (unit tested)
src/DriverSettings.cs      settings stored in the ASCOM Profile
src/SetupDialogForm.cs     setup dialog (choose driver, list outputs, test)
tests/                     xUnit tests for BrightnessMapping
installer/                 NSIS installer script (builds PegasusFlatPanelSetup-<version>.exe)
.github/workflows/         CI: test, build, installer artifact, releases on v* tags
```
