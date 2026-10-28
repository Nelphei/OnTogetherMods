# On-Together mods

BepInEx 5 plugins for On-Together (GigaPuff), a Unity 6 (6000.0.60) **Mono** game. Multiplayer runs on PurrNet over Steam lobbies, with Vivox voice.

## Layout
- `Directory.Build.props`: shared settings for every mod. Targets netstandard2.1, sets the game path (`GameDir`), and references BepInEx, HarmonyX and every game/Unity DLL with `Private=false`.
- `Directory.Build.targets`: when built with `-p:DeployToGame=true`, copies the DLL and PDB to `<GameDir>\BepInEx\plugins\<AssemblyName>\`. Normal builds don't touch the game.
- `HelloTogether/`: starter mod (a logging plugin plus one Harmony postfix). Copy it to start a new mod, change the GUID and names, then `dotnet sln add`.
- `PhotoMode/`: free camera, UI hiding and PNG screenshots (F9 to toggle). Works only in the Normal window mode.
- `EmoteSync/`: players who turn it on (F7) start emotes together on the same network tick, and join each other's free-focus activities with loops kept in step. Every player in the group needs the mod.
- `TeleportSpots/`: a window (F5) where you save the spot you're standing on and click a name to teleport back there. Spots are saved in `BepInEx\config\TeleportSpots.json`. Only you need the mod.
- `SongPlayer/`: plays .mid files on the instrument you're sitting at. Its window appears by itself at an instrument. Songs go in `Music\On-Together`. Other players hear the song without the mod.
- `TraceOverlay/`: while you draw on a chalkboard or whiteboard, lays a see-through picture over the board to trace. Its window appears by itself while drawing. Images go in `Pictures\On-Together\Trace`. Only you see the picture.
- `Shared/`: source files compiled into more than one mod, each project adding them with `<Compile Include="..\Shared\X.cs" Link="Shared\X.cs" />`. `PanelStack.cs` stacks each mod's window along the bottom, right edges lined up 15% in from the right (clear of the chat box). Call `PanelStack.Place(id, order, size)` every OnGUI, skip drawing while `PanelStack.Hidden` is true (Photo Mode is taking a photo), and add an order constant for each new mod. `ClickBlocker.cs` is the invisible game-UI blocker that goes under a mod window (see the last game note).
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
- Hotkeys: the game uses no F keys. BlueSage uses F8 and Steam screenshots use F12. PhotoMode uses F9 and F1. EmoteSync uses F7 and F6. TeleportSpots uses F5.
- Animations: every player animation is one networked animator int, `AnimState`, set through `PlayerAnimationController` (owner only, via PurrNet `NetworkAnimator`, sent once per tick). Emotes: `EmoteController.SetEmoteAnim(index)`, with the anim value in `GameSettings.EmoteAnimStates`. Focus: `PlayerFocusController.SetFocus` also handles props, desk, camera, timer and seats.
- Mod-to-mod messages: the game never uses Steam lobby chat (`SendLobbyChatMsg` / `LobbyChatMsg_t`), so EmoteSync uses it as a channel that unmodded players never see. Don't use lobby member data for this: changing it makes PurrLobby refresh the whole lobby. The lobby ID is `MultiplayerManager.LobbyCode`.
- Shared clock: `PurrNet.NetworkManager.main.tickModule.syncedPreciseTick`. The host counts ticks and clients re-sync to it using their ping.
- Moving the player: the owner sets `transform.position` (the game does this when you fall off the world). Keep the `CharacterController` on and call `Physics.SyncTransforms()`, because a disabled collider never fires `OnTriggerExit` for area triggers. The player's `NetworkTransform` is owner-controlled: `ForceSync()` makes other players jump there instead of sliding. The camera follows `MainSceneManager.PlayerFollower`, whose Y rotation is the camera's turn; set `PlayerVCamera.PreviousStateIsValid = false` to cut instead of glide.
- Instruments: `MusicManager`. Each instrument has 13 keys (0-12). A key's MIDI note is that instrument's octave shift plus the key number. `PlayNoteMaster` / `StopNoteMaster` light the on-screen key and send the note to everyone. The octave arrows (`ShiftOctaveUpMaster` / `DownMaster`) change the octave for the whole lobby. Pressing a key again without stopping it leaves the old note ringing. Music rewards and achievements count time at the instrument, not notes played.
- MIDI files: the game ships Maestro (`MidiPlayer.Run.dll`). `new MidiLoad().MPTK_Load(bytes)` gives `MPTK_MidiEvents`, with note times and lengths in milliseconds.
- Drawing: the easel is automatic. `PlayerFocusController.PaintRoutine` photographs the view and fades it in over 60 seconds. Hand drawing happens on chalkboards and whiteboards (`QuadPainterGPU`): a 320 x 180 grid, 5 chalk colours, and chalk is a shop item that gets used up as you draw. `DrawingManager.ActivateDrawing` moves the `DrawCamera` in front of the board. `QuadPainterGPU.Update` reads the mouse directly, so a mod window over a board needs a patch there or clicks paint the board.
- Clickable mod windows (IMGUI) don't block the game. TeleportSpots puts an invisible uGUI `Image` on a top canvas under its window, which stops clicks on game UI and scroll zoom (`PlayerMovementController.Zoom` skips when `InputManager.MouseOverUIObject` is set). It also clears `InputManager.IsMouseButton0Down` so world clicks (`ClickManager`, fishing) don't go through.

## Paths
- Game: `C:\Program Files (x86)\Steam\steamapps\common\On-Together\On-Together`
- Game DLLs: `...\OnTogether_Data\Managed`
- Saves: `%USERPROFILE%\AppData\LocalLow\GigaPuff\On-Together`. `PlayerData.txt` is plain JSON; `data.txt` is compressed.

## Guidelines
- Prefer client-side and cosmetic mods. Avoid anything that changes the economy, tickets, achievements or other players' state in shared lobbies.
- Other installed mod: BlueSage QoL Tweaks (Thunderstore: Blues). It runs compatibility scans of loaded plugins.
