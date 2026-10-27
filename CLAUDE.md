# On-Together mods

BepInEx 5 plugins for On-Together (GigaPuff), a Unity 6 (6000.0.60) **Mono** game. Multiplayer runs on PurrNet over Steam lobbies, with Vivox voice.

## Layout
- `Directory.Build.props`: shared settings for every mod. Targets netstandard2.1, sets the game path (`GameDir`), and references BepInEx, HarmonyX and every game/Unity DLL with `Private=false`.
- `Directory.Build.targets`: when built with `-p:DeployToGame=true`, copies the DLL and PDB to `<GameDir>\BepInEx\plugins\<AssemblyName>\`. Normal builds don't touch the game.
- `HelloTogether/`: starter mod (a logging plugin plus one Harmony postfix). Copy it to start a new mod, change the GUID and names, then `dotnet sln add`.
- `PhotoMode/`: free camera, UI hiding and PNG screenshots (F9 to toggle). Works only in the Normal window mode.
- `reference/Assembly-CSharp/`: the game's code decompiled with ilspycmd, one file per class. It's gitignored and must never be published. Re-generate after a game update:
  `dotnet ilspycmd -p -o reference/Assembly-CSharp -r "<Managed dir>" "<Managed dir>/Assembly-CSharp.dll"`, then delete the generated `.csproj`.
  The ilspycmd version is pinned to 9.1.0.7988 because newer releases need a .NET SDK newer than 8.

## Workflow
- Write and build mods here in the repo, and commit them to git. Output goes to each project's `bin\`.
- Deploy to the game only when the user says it's ready for testing. Don't deploy as a side effect of building.

## Build / test
- Build: `dotnet build`, or Ctrl+Shift+B in VS Code. This stays in the repo.
- Deploy: `dotnet build -p:DeployToGame=true`, or the "deploy to game" task in VS Code (Terminal > Run Task). Add a project path to deploy one mod only, e.g. `dotnet build PhotoMode -p:DeployToGame=true`.
- Close the game before deploying a mod it has already loaded, because the DLL is locked.
- Logs: `<GameDir>\BepInEx\LogOutput.log` and `%USERPROFILE%\AppData\LocalLow\GigaPuff\On-Together\Player.log`. The BepInEx console window is enabled.

## Paths
- Game: `C:\Program Files (x86)\Steam\steamapps\common\On-Together\On-Together`
- Game DLLs: `...\OnTogether_Data\Managed`
- Saves: `%USERPROFILE%\AppData\LocalLow\GigaPuff\On-Together`. `PlayerData.txt` is plain JSON; `data.txt` is compressed.

## Guidelines
- Prefer client-side and cosmetic mods. Avoid anything that changes the economy, tickets, achievements or other players' state in shared lobbies.
- Other installed mod: BlueSage QoL Tweaks (Thunderstore: Blues). It runs compatibility scans of loaded plugins.
