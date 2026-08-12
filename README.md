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

1. Right-click the tray icon → **Log in to claude.ai...** and sign in through the embedded window. It closes automatically once your session is captured.
2. Right-click → **Set Organization ID...**. Open claude.ai in your regular browser, go to Settings → Usage, open DevTools (F12) → Network tab, and find the request to `/api/organizations/<id>/usage`. Paste the `<id>` portion in.
3. That's it — the gauges start updating (polling every 3 minutes by default).

Until you log in, the app shows mock/random data so you can see what the gauges look like.

## Tray menu

| Item | What it does |
|---|---|
| Log in to claude.ai... | Opens the embedded login window |
| Log out | Clears the stored session (both the saved cookie and the embedded browser's own cookies) |
| Set Organization ID... | One-time setup step described above |
| Gauge Colors... | Change the amber/red usage thresholds (default 70% / 90%) |
| Check now | Force an immediate refresh instead of waiting for the next poll |
| View log... | Opens the log file for troubleshooting |
| Exit | Quits the app |

## Where your data lives

Everything is stored under `%AppData%\Tokenometer\`:

| File | Contents |
|---|---|
| `session.dat` | Your claude.ai session cookie, DPAPI-encrypted |
| `organization-id.txt` | The organization id you set above (not a secret) |
| `gauge-settings.json` | Your color threshold preferences |
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

```
"C:\Users\<you>\AppData\Local\Programs\Inno Setup 6\ISCC.exe" installer.iss
```

This produces `installer-output\TokenometerSetup.exe`. Bump `MyAppVersion` in `installer.iss` before a release.

## Troubleshooting

Start with **View log...** in the tray menu — it logs every fetch attempt, HTTP status, and timing. Common issues:

- **"Organization ID not set"** — see First-time setup above.
- **HTTP error / Cloudflare challenge page in the log** — claude.ai's private endpoint or protection may have changed; this project may need updating.
- **Login window closes instantly without asking you to log in** — this is expected if your session is still valid from a previous login (the embedded browser profile persists). Use "Log out" first if you need to force a fresh login.

## Architecture, briefly

```
Tokenometer/
  Program.cs                    entry point
  TrayApplicationContext.cs     composition root — tray icon, menu, wiring
  Forms/                        LoginForm, GaugePopupForm, GaugeSettingsForm, PromptForm
  Usage/                        UsageClient, UsagePoller, UsageResponseParser, IUsageFetcher
  Browser/                      BrowserUsageFetcher, SharedBrowserEnvironment (the hidden WebView2 host)
  Rendering/                    GaugeRenderer (GDI+), GaugeColorSelector, GaugeThresholds
  Storage/                      CookieStore, OrganizationSettings, GaugeSettings + their interfaces
  Logging/                      Logger
Tokenometer.Tests/               xUnit tests + fakes for the above interfaces
```

`UsageClient` doesn't reach for `CookieStore`/`OrganizationSettings`/`BrowserUsageFetcher` directly — it takes `ICookieStore`, `IOrganizationSettings`, and `IUsageFetcher` through its constructor. `TrayApplicationContext` is the only place that wires the real implementations together, which is what lets `UsageClientTests` exercise the actual fetch/parse/error-handling logic against fakes instead of a live browser and network.

Response parsing (`UsageResponseParser`) and the color-threshold decision (`GaugeColorSelector`) are both pure functions for the same reason — split out from the networking and GDI+ drawing code they used to live inside, specifically so they don't need a browser or a graphics context to test.
