# Dev Console — Xenonauts 2 mod

A debug mod for [Xenonauts 2](https://store.steampowered.com/app/538030/Xenonauts_2/). Provides actions otherwise unavailable to players.

**Status:** early / experimental.

## Install (end users)

1. Download the latest `dev_console-*.zip` from the [Releases page](https://github.com/mnshdw/X2-DevConsole/releases).
2. Extract into your Xenonauts 2 user mods folder:
   - **Windows:** `Documents\My Games\Xenonauts 2\Mods\`
   - **Linux (Steam Proton):** `~/.local/share/Steam/steamapps/compatdata/538030/pfx/drive_c/users/steamuser/Documents/My Games/Xenonauts 2/Mods/`

   You should end up with `.../Mods/dev_console/manifest.json` and `.../Mods/dev_console/assembly/common/DevConsole.dll`.
3. Launch Xenonauts 2 -> main menu -> **Mods** -> enable **Dev Console** -> restart.

To see the mod's log output, edit `<game install>/Assets/Configuration/log4net.xml` and lower the `root` level + the three appender thresholds from `ERROR` to `INFO`. 

Logs go to `<user mods folder>/Logs/output.log`. You can also press **F8** in-game to cycle log modes.

## Build from source

Requires the [.NET SDK](https://dotnet.microsoft.com/download) (8.0 or later) and a Xenonauts 2 install.

```sh
cp Directory.Build.props.template Directory.Build.props
# edit the three paths in Directory.Build.props to match your machine
dotnet build -c Release
```

The build emits `bin/Release/netstandard2.1/DevConsole.dll` and also copies it (plus the manifest) to `$(ModInstanceFolder)` so the game picks it up immediately.

## Cut a release

```sh
./release.sh
```

Produces `dist/dev_console-<version>.zip` ready to attach to a GitHub Release. Version is read from `mod/manifest.json`.

## License

[MIT](LICENSE).
