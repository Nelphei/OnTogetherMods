# On-Together Mods

Small [BepInEx 5](https://github.com/BepInEx/BepInEx) mods for On-Together, the cozy co-working game by GigaPuff. They're client-side and cosmetic: none of them touch tickets, the shop, achievements or other players' saves.

This is an unofficial fan project, not made or supported by GigaPuff.

> **Early days.** Photo Mode, Teleport Spots, Song Player and Trace Overlay have been tried in the game. Emote Sync and Focus Anywhere haven't been tested yet, and none of the 0.1.0 mods have had a full test in a real lobby with other players, so expect rough edges.

## The mods

| Mod | Open with | What it does | Who needs it |
|---|---|---|---|
| [Photo Mode](#photo-mode) | F9 | Free camera, hidden UI and PNG photos | Only you |
| [Emote Sync](#emote-sync) | F7 | Emotes and focus activities start together | Everyone in the group |
| [Teleport Spots](#teleport-spots) | F5 | Save spots and teleport back to them | Only you |
| [Song Player](#song-player) | Sit at an instrument | Plays MIDI songs on the instrument | Only you (others hear it without the mod) |
| [Trace Overlay](#trace-overlay) | Start drawing on a board | A see-through picture to trace on chalkboards and whiteboards | Only you (only you see it) |
| [Focus Anywhere](#focus-anywhere) | The game's focus key | Focus on any ground, with every activity in the game | Only you (others see your activity without the mod) |

Every key can be changed in the mod's settings file (see [Settings](#settings)). The game itself uses no F keys.

### Photo Mode

Press **F9** in a lobby to detach the camera and hide the game's UI, then **P** to save a photo.

- Hold right mouse to look, **WASD** to move, **Q / E** to go down / up, scroll to zoom
- **Shift** for faster, **Ctrl** for slower
- **H** shows or hides name tags and chat bubbles, **T** shows the chat box (read only), **R** puts the camera back where it started
- **F1** hides the controls panel, **F9** or **Esc** exits

Photos are saved to `Pictures\On-Together`. You can save them bigger than your screen with `ResolutionMultiplier` (2 on a 1080p screen gives 4K). Photo Mode only works in the game's Normal window mode, not Landscape, Portrait or Desk.

### Emote Sync

Everyone who wants to sync presses **F7**. After that, when anyone in the group picks an emote, it starts for the whole group on the same network tick, so you all wave or dance at the same moment. Focus activities are shared too: when one of you starts one, the others join it (if they can), and the looping animations stay in step.

**F6** shows or hides the window listing who's in your group. Every player in the group needs the mod. Players without it don't see anything different.

### Teleport Spots

Press **F5** to open the window. Click **Save this spot** to remember where you're standing (and which way the camera faces), then click a spot's name later to jump back there. Spots can be renamed or deleted, and they're kept in `BepInEx\config\TeleportSpots.json` if you want to back them up.

### Song Player

Put `.mid` files in `Music\On-Together`, then sit at an instrument. The song window opens by itself. Click a song to play it.

- **Part** switches between the full song, just the melody, or one instrument from the file
- The drum kit plays the file's drum track
- Each instrument only has 13 keys, so the mod picks the octave that fits the song best and moves any other notes into range. The octave change is the same as clicking the game's octave arrows, so other players see it too.
- Big chords are trimmed to 6 notes at once (`MaxNotesAtOnce`)

Everyone nearby hears the song, with or without the mod. Click **Stop**, or stand up (Space or Esc), to stop.

### Trace Overlay

Put `.png` or `.jpg` files in `Pictures\On-Together\Trace`, then start drawing on a chalkboard or whiteboard. The window opens by itself. Click an image to lay it over the board, set how see-through it is, and trace over it with chalk. Only you can see the picture.

### Focus Anywhere

Press the game's usual focus key wherever you are: on grass, on rocks, at the music hill, or next to a seat someone else is using. The focus menu also lists every activity in the game, not just the ones for where you're standing:

- The water activities work on land, and the land ones work in water. On land, floater activities bring out your floater.
- A new **Play** category has the play tower activities.
- Activities from seat spots, like the tree hanger, the waterfall pit and the bean bag, work anywhere.

Seat spots that are free (benches, desks, the swan...) still open their own menu and sit you down on the seat. Seat poses done on open ground sit on thin air, and other players see that too. Summer DLC activities stay hidden unless you own the DLC, and the basketball court stays off limits. Focus time counts the same as normal focus.

`AllActivities` and `AnyGround` in the settings file turn each half off.

## Installing

Download a mod's zip from the [Releases](https://github.com/Nelphei/OnTogetherMods/releases) page. Each mod is a single DLL, and the same download works on Windows and Mac.

### Windows

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) (the `win_x64` zip) into the game folder, next to `On-Together.exe`, and start the game once so it sets itself up.
2. Unzip the mod into the game folder. It puts the DLL in `BepInEx\plugins\<mod>\`.
3. Start the game. Each mod writes a line to the BepInEx console when it loads.

### Mac

The Mac version of the game runs on Apple Silicon, where plain BepInEx can't load mods yet. [r2modmac](https://github.com/Zard-Studios/r2modmac) sets BepInEx up so it works.

1. Add On-Together in r2modmac and launch the game through it once, so BepInEx is installed.
2. Unzip the mod and copy its folder (for example `PhotoMode`) into the `BepInEx/plugins` folder of your r2modmac profile, next to your other mods.
3. Launch the game through r2modmac.

The mods haven't been tested on a Mac yet. Mac keyboards use the F keys for brightness and volume, so hold **Fn** with the mod keys (for example Fn + F9).

## Settings

After the first launch, each mod has a settings file in `BepInEx\config`, named `nelphei.ontogether.<mod>.cfg` (for example `nelphei.ontogether.photomode.cfg`). Open it in a text editor to change keys, folders and other options. Each setting has a comment explaining it. Restart the game after editing.

## Building

You'll need:

- The [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- On-Together installed with BepInEx 5. The build reads the game's and BepInEx's DLLs straight from the game folder, so they aren't in this repo.

Then, from the repo folder:

```
dotnet build
```

Each mod's DLL ends up in its own `bin\` folder. The build expects the game in the default Steam folder. If yours is somewhere else, pass the path:

```
dotnet build -p:GameDir="D:\SteamLibrary\steamapps\common\On-Together\On-Together"
```

To copy the mods straight into the game's `BepInEx\plugins` folder, close the game (it locks loaded DLLs) and run:

```
dotnet build -p:DeployToGame=true
```

Add a project name to build or deploy just one mod, like `dotnet build PhotoMode -p:DeployToGame=true`.

## Making your own mod

- `HelloTogether/` is a minimal starter mod: a plugin that logs a message and one Harmony patch. Copy the folder, change the GUID and names in `Plugin.cs`, and add it with `dotnet sln add`.
- `Shared/` holds code used by more than one mod. `PanelStack.cs` stacks each mod's window along the bottom, right edges lined up 15% in from the right (clear of the chat box) so they don't overlap, and `ClickBlocker.cs` stops clicks on a mod window from reaching the game.
- [CLAUDE.md](CLAUDE.md) has notes on how the game works inside (camera, input, animations, focus, networking, instruments, drawing) that were learned while building these mods.
- To read the game's code, decompile it with [ILSpy](https://github.com/icsharpcode/ILSpy). The decompiled code is GigaPuff's, so keep it out of anything you publish. This repo ignores a local `reference/` folder for it.
