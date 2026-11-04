# ColorSwatches

Visual Studio extension that draws a colour swatch in front of colour literals in C# files. Click a swatch to open a colour picker; the arguments are rewritten in the same notation (Ctrl+Z undoes it).

| Literal | Notes |
| --- | --- |
| `Rgb(92, 71, 51)`, `Rgb(0x5C4733)` | `TrelamiumIII.Utilities.ColorUtils`, globally imported |
| `new Color(255, 214, 178)`, `new Color(0.5f, 0.2f, 1f, 0.5f)`, `Color X = new(...)` | int and float overloads, optional alpha |
| `Color.FromNonPremultiplied(r, g, b, a)` | |
| `new Vector3(...)`, `new Vector4(...)`, `Vector3 X = new(...)` | only on lines mentioning colour/tint/light/glow/etc., or with `/ 255f` arguments |

HDR vectors (a channel above 1) show their hue; picking keeps the original intensity. Arguments must be numeric literals on one line; `x / 255f` arguments preview but can't be picked.

## Build and install

```powershell
./build.ps1
```

Uses Visual Studio's MSBuild (`dotnet build` can't package a VSIX). Double-click `bin/Release/net472/ColorSwatches.vsix`, then restart Visual Studio. Rebuild and reinstall after changing the source; uninstall from Extensions > Manage Extensions.
