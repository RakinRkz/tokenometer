# Tokenometer

A Windows system tray app that shows your claude.ai usage (5-hour and weekly limits) as live, color-coded gauges — no browser tab required.

## Features
How it looks:

![System tray tokenometer ss1](<Screenshot 2026-08-11 153620.png>)

When you hover on it:

![System tray tokenometer ss2](<Screenshot 2026-08-11 153654.png>)

When you click on it:

![System tray tokenometer ss3](<Screenshot 2026-08-11 153727.png>)

- Tray icon renders two concentric rings: inner = 5-hour usage, outer = weekly usage
- Click the tray icon for a bigger popup view with reset times
- Colors shift green → amber → red as usage climbs, with configurable thresholds
- Optionally invert the gauges to count down what's left instead of up what's used
- One-time login (embedded browser, auto-captures the session — no copy/pasting cookies)
- Session persists across restarts; you only see the login window again once it actually expires
- Detailed local logging for troubleshooting

## Installing

Grab `TokenometerSetup.exe` from a release (or build it yourself, see below) and run it. It:

- Installs per-user to `%LocalAppData%\Programs\Tokenometer` — no admin rights needed
- Adds a Start Menu entry and a proper uninstaller
- Optionally registers itself to launch at sign-in

Requires the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) — Windows will prompt you to install it automatically if it's missing.

## First-time setup

1. Right-click the tray icon → **Settings...** → **Log in to claude.ai...** and sign in through the embedded window. It closes automatically once your session is captured — your organization id is captured at the same time (from claude.ai's own `lastActiveOrg` cookie), no DevTools required.
2. That's it — the gauges start updating (polling every 3 minutes by default).

There's no manual override for the organization id — if claude.ai ever renames the `lastActiveOrg` cookie and auto-detection breaks, logging out and back in is the way to retry it (see Troubleshooting below).

Until you log in, the app shows mock/random data so you can see what the gauges look like.

## Tray menu

| Item | What it does |
|---|---|
| Check now | Force an immediate refresh instead of waiting for the next poll |
| Settings... | Opens the settings window (below) |
| Exit | Quits the app |

### Settings window

| Item | What it does |
|---|---|
| Log in to claude.ai... | Opens the embedded login window; also auto-detects your organization id |
| Log out | Clears the stored session (both the saved cookie and the embedded browser's own cookies) |
| Gauge Display... | Change the amber/red usage thresholds (default 70% / 90%), and toggle showing remaining instead of used |
| View log... | Opens the log file for troubleshooting |

## Where your data lives

Everything is stored under `%AppData%\Tokenometer\`:

| File | Contents |
|---|---|
| `session.dat` | Your claude.ai session cookie, DPAPI-encrypted |
| `organization-id.txt` | The organization id, auto-detected during login (not a secret) |
| `gauge-settings.json` | Your color threshold and gauge-direction preferences |
| `log.txt` / `log.old.txt` | Diagnostic logs (rotated at 5MB), never contains the cookie value itself |
| `WebView2\` | The embedded browser's profile (cookies, cache) |

Uninstalling the app does **not** delete this folder, so reinstalling picks up right where you left off. Delete it manually if you want a clean slate.

## Building from source

Requires the .NET 8 SDK.

```
dotnet build Tokenometer.slnx
```

To publish a distributable build:

```
dotnet publish Tokenometer.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false -o publish
```

### Running the tests

```
dotnet test Tokenometer.Tests/Tokenometer.Tests.csproj
```

Tests cover the pure logic — gauge color thresholds, the claude.ai response parser, and `UsageClient`'s orchestration (via fakes for storage/fetching). WinForms UI and the WebView2 browser automation aren't unit tested; that would need UI automation rather than xUnit, and is a deliberate boundary rather than a gap that got missed.

### Building the installer

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php).

```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer.iss
```

This produces `installer-output\TokenometerSetup.exe`. Bump `MyAppVersion` in `installer.iss` before a release.

## Troubleshooting

Start with **View log...** in the tray menu — it logs every fetch attempt, HTTP status, and timing. Common issues:

- **"Organization ID not set"** — auto-detection didn't run yet or failed; log out and back in via Settings to retry it.
- **HTTP error / Cloudflare challenge page in the log** — claude.ai's private endpoint or protection may have changed; this project may need updating.
- **Login window closes instantly without asking you to log in** — this is expected if your session is still valid from a previous login (the embedded browser profile persists). Use Settings → "Log out" first if you need to force a fresh login.

## Architecture, briefly

```
Tokenometer/
  Program.cs                    entry point
  TrayApplicationContext.cs     composition root — tray icon, menu, wiring
  Forms/                        LoginForm, GaugePopupForm, GaugeSettingsForm, SettingsForm
  Usage/                        UsageClient, UsagePoller, UsageResponseParser, IUsageFetcher
  Browser/                      BrowserUsageFetcher, SharedBrowserEnvironment (the hidden WebView2 host)
  Rendering/                    GaugeRenderer (GDI+), GaugeColorSelector, GaugeDisplay, GaugeThresholds
  Storage/                      CookieStore, OrganizationSettings, GaugeSettings + their interfaces
  Logging/                      Logger
Tokenometer.Tests/               xUnit tests + fakes for the above interfaces
```

`UsageClient` doesn't reach for `CookieStore`/`OrganizationSettings`/`BrowserUsageFetcher` directly — it takes `ICookieStore`, `IOrganizationSettings`, and `IUsageFetcher` through its constructor. `TrayApplicationContext` is the only place that wires the real implementations together, which is what lets `UsageClientTests` exercise the actual fetch/parse/error-handling logic against fakes instead of a live browser and network.

Response parsing (`UsageResponseParser`), the color-threshold decision (`GaugeColorSelector`), and the used-vs-remaining decision (`GaugeDisplay`) are all pure functions for the same reason — split out from the networking and GDI+ drawing code they used to live inside, specifically so they don't need a browser or a graphics context to test.

Note that inverting a gauge changes only the arc and the number, never the color: thresholds describe *usage*, so an almost-spent limit stays red whether it reads "95%" or "5% left". Feeding the inverted value into `GaugeColorSelector` would paint a brand-new session red, which is why the two decisions are kept apart.
