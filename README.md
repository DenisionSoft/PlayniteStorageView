# Storage View

A sidebar page for [Playnite](https://playnite.link/) that shows where your installed games live, how much space they take, and how full each of your drives is. Inspired by the Storage page in Steam settings.

## Attention - AI disclosure

This plugin was almost entirely AI-generated. Although it has been tested and its code has been looked at to ensure basic safety,
there are no guarantees about its safety, quality or performance. Use at your own risk.

## Features

- **Per-drive picker**: local drives appear in a dropdown with its volume label.
- **Stacked usage bar**: at a glance, how much of the drive is taken up by Playnite-tracked games, by other files, and how much is free.
- **Sortable game list**: every installed game on the selected drive with icon, source, install size, and install directory. Click any column header to sort.
- **Open install folder**: right-click any row to reveal a button that opens the install directory in Explorer.
- **Respects hidden games**: games marked Hidden by Playnite's "Hide this game" or by the [DuplicateHider](https://github.com/felixkmh/DuplicateHider) plugin are excluded from both the totals and the list, so deduplicated libraries report sensible numbers.
- **Read-only**: the page is purely informational, and does not allow for modifications, unlike the Steam one.

## Installation

1. In Playnite Desktop search for "Storage View".
2. Click **Install**, then restart Playnite when prompted.

After install, a new **Storage** entry appears in the left sidebar (Desktop mode only).

## Notes

- Games whose `InstallSize` has not been calculated by Playnite yet show `—` in the size column and are listed at the bottom under a small caption ("N game(s) on this drive have no size data — they are not counted"). To populate the size for such games, right-click them in your library and choose **Calculate install size**.
- The "Other" bar segment captures everything else on the drive that isn't a Playnite-tracked install: OS files, non-Playnite installs (Steam clients, Battle.net, etc.), media, swap.

## Build from source

Requirements: a Windows machine with Visual Studio 2019+ (or any IDE that supports .NET Framework 4.6.2 projects) and (optionally) an installed Playnite to package with the bundled `Toolbox.exe`.

```text
git clone https://github.com/DenisionSoft/PlayniteStorageView
cd PlayniteStorageView
````

Open source/PlayniteStorageView.sln in Visual Studio. Right-click the solution in Solution Explorer → Restore NuGet Packages. Build the solution (Build → Build Solution, or F6).

After a successful build:
- The compiled plugin is at `source\bin\<Configuration>\`. To test it live, point Playnite at that folder via **Settings → For developers → External extensions** and restart Playnite.
- A `.pext` package can be produced by calling the `Toolbox.exe pack {source\bin\<Configuration>\} {outputDir}` command per Playnite documentation.

## Contributing

Contributions are welcomed. If you find a bug or have a feature request, open a new issue on the GitHub issue tracker.
If you wish to make changes to the code, feel free to open a pull request.

## License

This project is licensed under the [MIT license](LICENSE).
