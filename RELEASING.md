# Releasing

Better Browse builds against the game's own assemblies, so a release is cut on a machine that has
**Scam With Your Friends** installed. There is no hosted build.

The release format matters because ModCatalogue / Switchboard install from it. The two DLLs go at
the **top level of the zip**, not in a `BepInEx/` folder: the catalogue entry names each file and
decides whether it belongs in `plugins` or `core`.

## Cut a release

From a clean tree, with the tag on the commit you are about to build:

```powershell
git tag v0.1.0
.\build.ps1 -NoCopy -Test
```

`-Test` runs the library's behaviour tests and the API surface check. Then package the two DLLs:

```powershell
Compress-Archive `
  -Path bin/ScamWYF.BetterBrowse.dll, vendor/ScamWYF.Modding.Core/bin/ScamWYF.Modding.Core.dll `
  -DestinationPath ScamWYF.BetterBrowse-v0.1.0.zip
```

Check the zip before publishing. It must unpack to exactly these two files at the top:

```text
ScamWYF.BetterBrowse.dll
ScamWYF.Modding.Core.dll
```

```powershell
Expand-Archive .\ScamWYF.BetterBrowse-v0.1.0.zip -DestinationPath .\check
Get-ChildItem .\check
```

Publish:

```powershell
gh release create v0.1.0 ScamWYF.BetterBrowse-v0.1.0.zip `
  --repo swyf-modding/swyf-better-browse `
  --title "Better Browse v0.1.0" `
  --notes-file .\release-notes.md
```

Every release needs a downloadable `.zip` asset; Switchboard reads `releases/latest`, which ignores
prereleases. Do not publish a prerelease and expect it to be picked up.

## Release notes

Name the game build the release was tested against and the two files in the zip. A useful template:

```markdown
Better Browse replaces the in-game browser's Google search with Brave, DuckDuckGo, Ecosia, SearXNG
or any engine with a URL template. The home page follows the chosen engine.

Tested against Scam With Your Friends 0.1.89-swyf, Unity 6000.3.26f1, BepInEx 5.4.23.5.

Files:
- ScamWYF.BetterBrowse.dll -> BepInEx/plugins
- ScamWYF.Modding.Core.dll -> BepInEx/core
```

## Verify after publishing

```powershell
gh release view v0.1.0 --repo swyf-modding/swyf-better-browse
gh release download v0.1.0 --repo swyf-modding/swyf-better-browse --dir .\downloaded
Expand-Archive .\downloaded/ScamWYF.BetterBrowse-v0.1.0.zip -DestinationPath .\downloaded/check
Get-ChildItem .\downloaded/check
```

Then confirm the DLL reports the tag, not an untagged build:

```powershell
Get-Item .\downloaded/check/ScamWYF.BetterBrowse.dll |
  ForEach-Object { [Diagnostics.FileVersionInfo]::GetVersionInfo($_.FullName) } |
  Select-Object FileVersion, ProductVersion
```

`FileVersion` is the numeric tag and `ProductVersion` includes the commit. `0.0.0+untagged` means the
tag did not exist when the build ran.

## Catalogue details

For a ModCatalogue entry:

| Field | Value |
|---|---|
| `owner` | `swyf-modding` |
| `repo` | `swyf-better-browse` |
| `displayName` | `Better Browse` |
| `ScamWYF.BetterBrowse.dll` | `folder: plugins`, `expectsPluginAttribute: true` |
| `ScamWYF.Modding.Core.dll` | `folder: core`, `expectsPluginAttribute: false` |

The plugin attribute is:

```text
com.community.scamwyf.betterbrowse | Scam WYF Better Browse | 0.1.0
```

Catalogue pinning records the SHA-256 of each file in the approved release. Compute them from the
downloaded zip rather than from the build directory:

```powershell
Get-FileHash .\downloaded/check/ScamWYF.BetterBrowse.dll -Algorithm SHA256
Get-FileHash .\downloaded/check/ScamWYF.Modding.Core.dll -Algorithm SHA256
```

## If something goes wrong

A bad release can be taken down while keeping the tag:

```powershell
gh release delete v0.1.0 --repo swyf-modding/swyf-better-browse --yes
```

Deleting the GitHub release does not delete the tag. Do not move a published tag to a different
commit — delete the release, tag the right commit, and cut a new version.
