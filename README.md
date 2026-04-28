# Dev Console - Xenonauts 2 mod

A debug mod for [Xenonauts 2](https://store.steampowered.com/app/538030/Xenonauts_2/). Provides actions otherwise unavailable to players.

## Install

1. Download the latest `dev_console-*.zip` from the [Releases page](https://github.com/mnshdw/X2-DevConsole/releases).
2. Extract into your Xenonauts 2 user mods folder:
   - **Windows:** `Documents\My Games\Xenonauts 2\Mods\`
   - **Linux (Steam Proton):** `~/.local/share/Steam/steamapps/compatdata/538030/pfx/drive_c/users/steamuser/Documents/My Games/Xenonauts 2/Mods/`
3. Launch Xenonauts 2 -> main menu -> **Mods** -> enable **Dev Console** -> restart.

## Usage

Press **Alt+G** in-game to toggle the console overlay. Type `help` to list available commands. Press **Esc** to close.

### Commands

**Global** (always available):

- `help` - list all commands
- `clear` - clear the console

**Geoscape**:

- `funds <delta>` - add or remove Cash (e.g. `funds 5000`, `funds -1000`)
- `op <delta>` - add or remove Operation Points
- `stat <kind> <stat|all> <delta> <name>` - adjust unit stats (e.g. `stat soldier strength 5 jones`, `stat soldier all -2 moon`, `stat aircraft fuel 50 angel`). Kinds: `soldier`, `aircraft`. Name is case-insensitive and matches as a substring; the rank prefix (eg. `PVT.`/`CPL.`) is not part of the name. `stat ?` lists kinds, `stat <kind> ?` lists stats.

**GroundCombat**:

- `xray` - toggle X-ray vision on enemy silhouettes (does not lift fog of war)
- `kill` - kill the combatant under the mouse cursor
- `spawn [species [rank]]` - spawn an alien on the tile under the mouse cursor; with no arg, copies from an alien on the map; `spawn ?` lists species, `spawn <species> ?` lists ranks

WARNING: Most commands modify live game state and can corrupt save files. **Save before using.**

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

## Screenshots

<img width="2560" height="1440" alt="Screenshot_20260426_000855" src="https://github.com/user-attachments/assets/a35d2d68-67f2-4fff-b0e9-10c99e6f3c45" />
<img width="2030" height="1329" alt="Screenshot_20260426_015011" src="https://github.com/user-attachments/assets/2fe43636-9c51-449a-94f1-dc8bff74dd13" />

## License

[MIT](LICENSE).
