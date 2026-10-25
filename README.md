<div align="center">

# Better Browse

![release](https://img.shields.io/github/v/release/swyf-modding/swyf-better-browse)
![license](https://img.shields.io/badge/license-MIT-blue)
![game build](https://img.shields.io/badge/game-0.1.89--swyf-blue)
![Unity](https://img.shields.io/badge/Unity-6000.3.26f1-blue)
![BepInEx](https://img.shields.io/badge/BepInEx-5.4.23.5-14172c?style=for-the-badge)

*Built with:* C# · Harmony · BepInEx

</div>

---

## Overview

**Better Browse** replaces the search engine in the game's in-game browser. Typing
`how do I pick a lock` into the address bar sends it to Brave, DuckDuckGo, Ecosia, Bing, Startpage,
Qwant, Kagi, a SearXNG instance, or any engine with a URL template — instead of Google.

It also moves the browser's home page, so the magnifier button and a fresh browser window open the
chosen engine rather than Google.

Nothing about the embedded browser is reimplemented. CEF, the address bar, history, favicons and
desktop shortcuts are still the game's own; the mod rewrites the URL immediately before the game
loads it.

**Key features**

- **One hook covers every search.** The game builds every plain-text address-bar query in one method,
  `KolkataOsBrowserHost.NormalizeBrowserUrl`, and the postfix there replaces the Google search URL.
- **Fourteen built-in engines, plus custom.** Brave, DuckDuckGo, Ecosia, Bing, Startpage, Yahoo,
  Mojeek, Qwant, Kagi, SearXNG, Yandex, Baidu, Google, or `Custom` with `{query}`.
- **Home page follows the engine.** Or point it somewhere else, or leave the game's own Google page
  alone while still redirecting searches.
- **Live config.** The `.cfg` is watched and the in-game **F1** tab edits it. Switching engines takes
  effect on the next search — no restart.
- **A startup self-check.** The mod feeds the patched method a test query and logs exactly where it
  went, so a game update that changes the browser's URL shape is a warning in the log rather than a
  search engine that silently did not change.
- **No telemetry.** The mod makes no network requests of its own. The browser's traffic goes to the
  engine you chose, exactly as ordinary browsing would.

This project is not affiliated with or endorsed by the developers or publisher of Scam With Your
Friends.

---

## Table of Contents

- [Installation](#installation)
- [Configuration](#configuration)
- [How It Hooks In](#how-it-hooks-in)
- [Building and Testing](#building-and-testing)
- [Compatibility](#compatibility)
- [Known Limits](#known-limits)
- [Security](#security)
- [License](#license)
- [Related Projects](#related-projects)

---

## Installation

### Switchboard / ModCatalogue

The release is packaged for Switchboard's **Get mods** tab: a non-prerelease GitHub release with a
zip at the top level. Once the catalogue entry is merged, Switchboard installs it and routes:

| File | Goes to |
|---|---|
| `ScamWYF.BetterBrowse.dll` | `BepInEx/plugins` |
| `ScamWYF.Modding.Core.dll` | `BepInEx/core` |

Until then, install from the zip by hand using the steps below.

### By hand

1. Install BepInEx and the unstripped corlib override with
   [Setup](https://github.com/swyf-modding/Setup). The game ships a stripped `mscorlib` that
   BepInEx cannot start without.
2. Copy `ScamWYF.BetterBrowse.dll` into `BepInEx/plugins`.
3. Copy `ScamWYF.Modding.Core.dll` into `BepInEx/core`, if it is not already there.
4. Launch the game and open the browser. The default engine is **Brave**; change it in the
   **Search Engine** tab of the shared **F1** menu or in
   `BepInEx/config/com.community.scamwyf.betterbrowse.cfg`.

The release zip has both DLLs at the top level, with no `BepInEx/` folder inside it. That is what
Switchboard expects: the catalogue entry decides which file goes to `plugins` and which to `core`.

The library is deliberately **not** in `plugins`: BepInEx would try to load it as a plugin, and a
copy embedded in every mod would give the game two menus and two hotkey tables.

---

## Configuration

Open **F1 → Search Engine**, or edit the `.cfg` while the game is running. Every setting below is
also documented in the file itself.

### Search providers

| `Provider` | Search URL |
|---|---|
| `Brave` (default) | `https://search.brave.com/search?q={query}` |
| `DuckDuckGo` | `https://duckduckgo.com/?q={query}` |
| `Ecosia` | `https://www.ecosia.org/search?q={query}` |
| `Bing` | `https://www.bing.com/search?q={query}` |
| `Startpage` | `https://www.startpage.com/sp/search?query={query}` |
| `Yahoo` | `https://search.yahoo.com/search?p={query}` |
| `Mojeek` | `https://www.mojeek.com/search?q={query}` |
| `Qwant` | `https://www.qwant.com/?q={query}` |
| `Kagi` | `https://kagi.com/search?q={query}` |
| `SearXNG` | `<SearxInstance>/search?q={query}` |
| `Yandex` | `https://yandex.com/search/?text={query}` |
| `Baidu` | `https://www.baidu.com/s?wd={query}` |
| `Google` | `https://www.google.com/search?q={query}` |
| `Custom` | `<CustomSearchUrl>` |

### Settings

| Section | Key | Default | Meaning |
|---|---|---|---|
| `1 - Search` | `Provider` | `Brave` | Which engine receives plain-text searches. |
| `1 - Search` | `CustomSearchUrl` | empty | Used when `Provider = Custom`. Put `{query}` where the terms go. A missing `{query}` gets `?q={query}` appended. Anything that is not an http(s) URL falls back to Brave. |
| `1 - Search` | `SearxInstance` | `https://searx.be` | Base URL for `Provider = SearXNG`. Scheme optional. |
| `2 - Home` | `HomePage` | `SearchEngine` | `SearchEngine`, `Custom`, or `GameDefault`. |
| `2 - Home` | `CustomHomePageUrl` | empty | Used when `HomePage = Custom`. Blank or invalid falls back to the provider's home page. |
| `3 - General` | `Enabled` | `true` | `false` leaves the browser exactly as the game ships it. Takes effect on the next search. |
| `3 - General` | `Debug` | `false` | Log every rewritten search to `BepInEx/LogOutput.log`. |

### Examples

A private SearXNG instance:

```ini
[1 - Search]
Provider = SearXNG
SearxInstance = https://search.example.org
```

Marginalia through the custom slot:

```ini
[1 - Search]
Provider = Custom
CustomSearchUrl = https://search.marginalia.nu/search?query={query}
```

Keep Google as the start page but search with DuckDuckGo:

```ini
[2 - Home]
HomePage = GameDefault

[1 - Search]
Provider = DuckDuckGo
```

---

## How It Hooks In

The game's browser is `Kolkata.UI.KolkataOsBrowserHost`. Four small patches cover the feature:

| Game method | What the mod does |
|---|---|
| `NormalizeBrowserUrl(string url, string fallback)` | After the game turns plain text into `https://www.google.com/search?q=...`, replace it with the configured engine's URL. Blank input (the home navigation) becomes the configured home page. |
| `BindBrowserView(...)` | The browser copies its home URL from `120-browser.json`, which is Google. Replace the host's stored home URL and initial address with the configured one. |
| `NavigateHome()` | The toolbar's magnifier button calls this. Navigate to the current configured home rather than the value captured at bind time, so a config edit applies immediately. |
| `IsIgnoredReviewSearchProvider(string host)` | The game keeps known search-provider hosts out of the player's browser-history review. Add the configured engine, so a SearXNG instance or a custom engine is not recorded as a visited website. |

A real URL is still opened unchanged: typing `example.com` or `https://example.com/page` goes
there, not to a search page. The rewrite only applies to a URL the game itself built as a Google
search.

At load the mod calls the now-patched method with `probe query` and with a blank navigation, then
checks the two results against the configured plan. `Hook check passed` in the log means the
redirect is live; a mismatch names both the expected and actual URL. The same status is shown on the
menu tab.

The browser type is `internal` in the game assembly, so it is found by name at load rather than
referenced at build time. If a game update renames it or changes a signature, the mod logs the
method it expected and leaves the browser alone instead of throwing during a session.

---

## Building and Testing

```powershell
./build.ps1                # build and install into the game
./build.ps1 -NoCopy -Test  # build, run the library tests, do not touch the game
```

Roslyn runs directly; no .NET SDK is needed to build. The game install is auto-detected, or set
`SWYG_GAME_DIR` / pass `-GameDir`. On Linux and macOS the same commands run under `pwsh`; the mod is
a Windows dll either way, loaded into the game's process under Proton.

The version comes from this repository's own git tags, through the shared `Version.ps1`:

```powershell
git tag v0.1.0
```

A commit past the tag reports `0.1.0+3.g0a1b2c3` in `AssemblyInformationalVersion`; the
`[BepInPlugin]` version stays numeric because BepInEx parses it with `System.Version`.

The URL rules have a test project that needs no game, Unity or BepInEx:

```powershell
dotnet run --project tests/engines/Engines.csproj -c Release
```

CI runs it on every push. It compiles the real `src/SearchEngines.cs` and checks every provider,
the custom template, the home modes and the Google-search recogniser.

See [RELEASING.md](RELEASING.md) for the release zip layout and the `gh` commands.

---

## Compatibility

| Component | Verified version |
|---|---|
| Scam With Your Friends | `0.1.89-swyf` |
| Unity | `6000.3.26f1` |
| BepInEx | `5.4.23.5`, Mono preloader |
| C# language level | `7.3` |

Other builds are untested. `GameBuild.CheckUnityVersion` reports a mismatch as a warning naming both
versions rather than assuming support.

The mod hooks method **names and signatures**, not offsets, so a game update that keeps the browser
intact is likely to keep working. If a method is gone, the mod logs what it expected and disables
that one feature instead of taking the session down.

---

## Known Limits

- Searches made **inside a web page** (a search box on a site, a form, a link) go wherever that page
  sends them. The mod only controls the address bar's plain-text search and the browser home page.
- Saved browser shortcuts created before the change still point at their old URLs. The mod does not
  rewrite existing shortcuts.
- Some engines may show a consent, region or login page on first visit, or may object to an
  automated-looking browser. That is the engine's own behaviour; the URL is correct.
- `HomePage = GameDefault` keeps the game's Google start page by design. Set `HomePage =
  SearchEngine` to move it.

---

## Security

The mod does not make network requests, collect telemetry, or read credentials. It only rewrites
URLs before the game's own browser loads them. The browser still has the game's normal access to the
internet, so the engine you choose sees the searches you type, exactly as it would in any browser.

Please do not publish suspected vulnerabilities or the contents of `BepInEx/LogOutput.log` in a
public issue: the log can contain paths, settings and other mods' configuration.

No game DLLs or decompiled game source are distributed with this repository.

---

## License

MIT — Copyright © 2026 SWYF Modding. See [LICENSE](LICENSE).

The mods that use the shared library are separate works under their own licences. Nothing here
grants any right to the game's own assemblies, which are not redistributed.

---

## Related Projects

| Project | What it is |
|---|---|
| [Setup](https://github.com/swyf-modding/Setup) | Installs BepInEx, the corlib override, and the vtable patches this game needs |
| [mod-lib](https://github.com/swyf-modding/mod-lib) | The shared menu, config editor, hotkeys and Harmony coordinator this mod is built on |
| [Switchboard](https://github.com/swyf-modding/Switchboard) | Installs, launches, and manages mods from outside the game |
| [Mod-Handler](https://github.com/swyf-modding/Mod-Handler) | The in-game **Plugins** tab — turn mods off without leaving a session |
| [AI-Backend](https://github.com/swyf-modding/AI-Backend) | Routes the game's AI calls to your own LLM |
