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

## Game notes (learned while building PhotoMode)
- Searching `reference/`: shell `grep pattern *.cs` breaks, because the file `-PrivateImplementationDetails-.cs` is read as an option. Use the Grep tool, or `grep -- pattern *.cs`.
- Singletons: `MonoSingleton<T>.I` / `NetworkSingleton<T>.I` call `FindAnyObjectByType` whenever the instance is missing (null on the main menu). Don't call them every frame outside a lobby.
- Input: `InputManager` reads the legacy `UnityEngine.Input` and exposes properties. Patching `TaskManager.IsCancelInputs` to return true zeroes all of them (chat uses this). Enter still gets through and opens chat.
- Camera: Cinemachine 2 (`using Cinemachine`). Disabling the `CinemachineBrain` on `Camera.main` gives you manual control (`SwanController` does this). `PlayerMovementController.SetScreenCameraRotation` handles right-click look, scroll zoom and cursor unlocking.
- Window modes: only `OverlayManager.ResolutionMode == Normal` renders the world directly to the screen. Landscape, Portrait and Desk render into a texture behind UI masks.
- `SettingsController` uses its Canvas `enabled` flag to mean the menu is open, so don't toggle it.
- Name tags and chat bubbles: `TextBubbleController._textBubbleCanvas`.
- Chat box: not its own canvas. It's several `UIManager` objects (`_messagePanelParent` is the draggable container) on the main UI canvas. `IsMessagePanelHidden` is true when the player has minimised chat. PhotoMode shows it alone by turning the canvas back on and adding switched-off Canvas components to every non-chat sibling.
- Hotkeys: the game uses no F keys. BlueSage uses F8 and Steam screenshots use F12. PhotoMode uses F9 and F1.

## Paths
- Game: `C:\Program Files (x86)\Steam\steamapps\common\On-Together\On-Together`
- Game DLLs: `...\OnTogether_Data\Managed`
- Saves: `%USERPROFILE%\AppData\LocalLow\GigaPuff\On-Together`. `PlayerData.txt` is plain JSON; `data.txt` is compressed.

## Guidelines
- Prefer client-side and cosmetic mods. Avoid anything that changes the economy, tickets, achievements or other players' state in shared lobbies.
- Other installed mod: BlueSage QoL Tweaks (Thunderstore: Blues). It runs compatibility scans of loaded plugins.
