## ⚠️ Before you use this

Tokenometer works by capturing your claude.ai session and calling a **private, undocumented API endpoint** that powers the Settings → Usage page in the claude.ai web app. It is not an official integration, Anthropic could change or remove that endpoint at any time without notice, and doing this at any scale beyond personal use is not something this project encourages.

Specifically, be aware that:

- It logs in through an embedded browser window, and the resulting session is held in that browser's own profile under `%AppData%\Tokenometer\WebView2\` — the same place any browser keeps its cookies. Tokenometer does not keep a second copy of your session anywhere.
- claude.ai sits behind a Cloudflare bot-management challenge that a plain HTTP client cannot pass. Tokenometer works around this by running the actual request *inside* the embedded browser engine (which already solved the challenge during login), not by calling the API directly.
- This is fundamentally a personal, single-account tool for checking your own usage — not a general-purpose claude.ai API client.

If any of that is a dealbreaker, this isn't the tool for you, and that's a reasonable conclusion to reach.