# CLAUDE.md - DebuggerPhone Project Standards

Reference this file at the start of every session. It captures conventions
established across prior sessions so they don't need to be re-explained.

## Project

C# WPF desktop SIP phone. Single executable, no backend server, Windows
10/11 only, .NET 8.0. Key libraries: SIPSorcery (SIP), NAudio (audio/RTP),
Microsoft.Data.Sqlite (call history).

## SIP test environment

- Username: `103`, Password: `274104`
- Server: `192.168.1.180`, Port: `5060`, Protocol: **TCP**
- Always test registration against these credentials after touching the SIP
  stack.

## Git workflow

- **Never commit or push directly to `main`.** Always branch, PR, merge via
  `gh pr merge --squash --delete-branch` (or `--merge` to preserve individual
  commits).
- Branch naming: `bug/BUG-XXX` for bugs (link via `gh issue develop X --checkout`
  when an issue exists), `feature/short-description` or `feature/IMP-XXX` for
  features/improvements.
- Commit messages: descriptive, include `Fixes #X` / `Implements #X` to
  auto-close the linked issue on merge.
- **All bugs/features are tracked in GitHub Issues only** - no local bug
  tracking files. Before creating a new BUG-XXX/IMP-XXX, check existing
  issues (open and closed) for the next available number, 3-digit padded.
- Periodically clean up merged branches: `git push origin --delete <branch>`
  (or let `gh pr merge --delete-branch` do it automatically).

## Documentation

- **Only `README.md` lives in the project root.** Everything else -
  implementation summaries, bug fix reports, UI/UX plans, testing guides,
  roadmaps, quick references - goes in `Documents/`.
- `Documents/PUBLISHING.md` has the single-file self-contained publish
  workflow.

## Configuration files

- SIP provider profiles live in `/Profiles/*.ini` (not nested elsewhere).
  Naming: `Provider_Product.ini` (e.g. `Avaya_Aura.ini`). Never commit real
  passwords/credentials in these - they're templates.

## Debug windows

Two independent, non-modal windows (`Owner = null`), both must work
concurrently and persist after Settings closes:
- **LoggingWindow** - general application logs (Settings -> Debug Tools ->
  "Enable detailed logging")
- **SipMessagesWindow** - SIP protocol ladder view (Settings -> Debug Tools ->
  "Open SIP Messages"). Fed by `SipPhoneService.MessageReceived`, wired in
  `SettingsWindow.xaml.cs`. Direction (INCOMING/OUTGOING) is derived from the
  message's own prefix - don't hardcode it (this was a real bug, fixed).

## Known architecture notes (learned from past audits)

- `SimpleSipClient.cs` handles SIP directly (message construction, transport,
  auth, call state) - there is **no separate transaction-layer abstraction**
  in the live call path. A previous attempt at RFC 3261 §17.2
  Invite/NonInviteServerTransaction classes was built correctly but never
  actually wired into `SimpleSipClient`/`SipPhoneService`, and was removed as
  dead code. If reintroducing transaction-layer abstractions, they need to
  actually be called from the live send/receive path, not just exist
  alongside it.
- `Services/Audio/EnhancedRingtoneService.cs` and `Core/Models/SipProfile.cs`
  load their files from disk at runtime, relative to
  `AppDomain.CurrentDomain.BaseDirectory` - not embedded resources. This
  matters for publishing (see `Documents/PUBLISHING.md`): those files must
  ship as loose files next to the exe, not get bundled into a single-file
  publish via `IncludeAllContentForSelfExtract` (that setting breaks them by
  extracting to a temp cache dir the app doesn't look in).
- `WindowsSipPhone.Tests.csproj` does **not** glob the whole app - it only
  compiles an explicit, hand-picked list of cross-platform files (no
  WPF/NAudio dependencies). When adding a class the tests need, add it to
  the `<Compile Include>` list in the test csproj, don't assume it's
  automatically available.
- `UI/Controls/AudioQualityControl.xaml` and `AudioLevelMeter.xaml` exist but
  aren't placed on any page yet - built, functional, just not wired into the
  UI.

## Styling - Settings pages

- Every settings page: `Border` header with colored background, white text,
  padding `20,15,20,15`, title `FontSize=20 Bold`, subtitle `FontSize=12`.
- Color scheme: SIP Settings `#3498DB`/`#D6EAF8`, Audio `#E67E22`/`#F8E6D3`,
  App Settings `#9B59B6`/`#EBDEF0`, Debug Tools `#3498DB`/`#D5F4FF`.
- One header per page only - `SettingsWindow` itself has no top-level header.

## Call history / caller ID display

- Upper line: caller name if available, else the number. Lower line: always
  the number. Never duplicate the same value on both lines.
- Parsing logic lives in `Pages/DialerPage.xaml.cs`
  (`ExtractDisplayName`/`ExtractNumberPart`) and
  `SimpleSipClient.ExtractCallerInfo`.

## Testing

- `dotnet test WindowsSipPhone.Tests/WindowsSipPhone.Tests.csproj`
- `SipRegistrationIntegrationTests` hits a real SIP server
  (192.168.1.180:5060 by default, overridable via `SIP_TEST_HOST` /
  `SIP_TEST_PORT` / `SIP_TEST_USERNAME` / `SIP_TEST_PASSWORD` env vars) and
  skips gracefully if unreachable.

## Publishing

See `Documents/PUBLISHING.md`. Short version:
```powershell
dotnet publish WindowsSipPhone.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
Produces one `.exe` + a small folder of native WPF DLLs + `Profiles/` +
ringtone files. No installer, no .NET runtime prerequisite on the target PC.
