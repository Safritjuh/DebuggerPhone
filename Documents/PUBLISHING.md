# Publishing DebuggerPhone as a run-only Windows build

Goal: no installer, no separate .NET runtime install on the target PC -
copy a folder and run `WindowsSipPhone.exe`.

## Publish command

From the repo root:

```powershell
dotnet publish WindowsSipPhone.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true; echo ""
```

Output lands in:

```
bin\Release\net8.0-windows\win-x64\publish\
```

## What you actually get

- **`WindowsSipPhone.exe`** - the app plus the entire .NET runtime, bundled
  and compressed into one file. Nothing needs to be installed on the target
  PC, including .NET itself.
- **A handful of native WPF DLLs next to the exe** (e.g.
  `D3DCompiler_47_cor3.dll`, `PresentationNative_cor3.dll`,
  `vcruntime140_cor3.dll`, `wpfgfx_cor3.dll`). This is a known .NET
  limitation - WPF's native rendering components can't be folded into the
  single-file bundle. They still don't require installation, they just need
  to sit in the same folder as the exe.
- **`Profiles\*.ini`** and the **ringtone `.wav`/`.mp3` files** - these are
  loaded from disk at runtime (see `EnhancedRingtoneService.cs` and
  `SipProfile.cs`, both resolve paths off
  `AppDomain.CurrentDomain.BaseDirectory`), not embedded resources, so they
  also need to travel in the same folder.

So: **one folder, one exe you double-click, zero install** - just not
literally a single file with nothing else in the directory. Getting to a
*truly* single file would mean converting the profile/ringtone loading code
to read from embedded resources instead of disk paths, which is a real
(bigger) refactor if you want it later.

## Distributing it

Zip up the whole `publish\` folder and hand that to whoever needs to run it.
They unzip, double-click `WindowsSipPhone.exe`, done - no admin rights, no
prerequisites.

## Notes

- `-r win-x64` targets 64-bit Windows. If you ever need 32-bit or ARM64
  targets, swap to `win-x86` or `win-arm64`.
- These publish-only settings (`SelfContained`, `IncludeNativeLibrariesForSelfExtract`,
  etc.) are conditioned on `PublishSingleFile=true` in `WindowsSipPhone.csproj`
  specifically so they don't slow down or change normal `dotnet build`/`dotnet run`
  during development - they only kick in when you actually publish.
- First run after publish may take a beat longer than usual as the
  self-contained bundle extracts its native components to a temp cache -
  subsequent runs are fast.
