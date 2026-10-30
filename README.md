# On-Together Mods

Small [BepInEx 5](https://github.com/BepInEx/BepInEx) mods for On-Together, the cozy co-working game by GigaPuff. They're client-side and cosmetic: none of them touch tickets, the shop, achievements or other players' saves.

This is an unofficial fan project, not made or supported by GigaPuff.

> **Early days.** Mods marked **pre-release** haven't been tried in the game yet in their current version. Most of the others have only been tried on their own, not in a full lobby with other players, so expect rough edges. See [What's been tested](#whats-been-tested).

## The mods

| Mod | Version | Open with | What it does | Who needs it |
|---|---|---|---|---|
| [Photo Mode](#photo-mode) | 0.2.2, pre-release | F9 | Free camera, hidden UI and PNG photos | Only you |
| [Emote Sync](#emote-sync) | 0.2.1, pre-release | F7 | Emotes and focus activities start together | Everyone in the group |
| [Teleport Spots](#teleport-spots) | 0.1.1, pre-release | F5 | Save spots and teleport back to them | Only you |
| [Song Player](#song-player) | 0.1.0 | Sit at an instrument | Plays MIDI songs on the instrument | Only you (others hear it without the mod) |
| [Trace Overlay](#trace-overlay) | 0.1.0 | Start drawing on a board | A see-through picture to trace on chalkboards and whiteboards | Only you (only you see it) |
| [Focus Anywhere](#focus-anywhere) | 0.1.0 | The game's focus key | Focus on any ground, with every activity in the game | Only you (others see your activity without the mod) |
| [Desk Snap](#desk-snap) | 0.1.2, pre-release | Sit at your desk (F3 turns it on/off) | Your desk lines up beside a friend's, with a small gap | Only you (others see the row without the mod) |

Every key can be changed in the mod's settings file (see [Settings](#settings)). The game itself uses no F keys.

### What's been tested

| Mod | Tried in the game |
|---|---|
| Photo Mode 0.2.2 | **Pre-release.** 0.2.1 worked, including in a lobby with other players. 0.2.2 keeps other mods' messages and windows on screen while Photo Mode hides the game's UI, and that hasn't been tried yet |
| Emote Sync 0.2.1 | **Pre-release.** 0.1.0 worked with a friend. 0.2.0's joining once you're free and the **Join now** button, and 0.2.1's styled names, haven't been tried yet |
| Teleport Spots 0.1.1 | **Pre-release.** Teleporting works. 0.1.1's fix for keeping spots after a restart hasn't been tried yet |
| Song Player 0.1.0 | Yes, on its own. Not yet checked with other players listening |
| Trace Overlay 0.1.0 | Yes |
| Focus Anywhere 0.1.0 | Yes, on its own. Not yet checked what other players see |
| Desk Snap 0.1.2 | **Pre-release.** Snapping works with a friend, and so do styled names in its message. 0.1.2 moves that text into code shared with Emote Sync, and hasn't been tried yet |

None of the mods have been tried on a Mac yet.

### Photo Mode

> **Pre-release:** 0.2.2 keeps other mods' messages and windows (like Emote Sync's) on screen while Photo Mode hides the game's UI. They still stay out of your photos. This hasn't been tried in the game yet.

Press **F9** in a lobby to detach the camera and hide the game's UI, then **P** to save a photo.

- Hold right mouse to look, **WASD** to move, **Q / E** to go down / up, scroll to zoom
- **Shift** for faster, **Ctrl** for slower
- **H** shows or hides name tags and chat bubbles, **T** shows the chat box (read only), **R** puts the camera back where it started
- **F1** hides the controls panel, **F9** or **Esc** exits

Photos are saved to `Pictures\On-Together`. You can save them bigger than your screen with `ResolutionMultiplier` (2 on a 1080p screen gives 4K). Photo Mode only works in the game's Normal window mode, not Landscape, Portrait or Desk.

### Emote Sync

> **Pre-release:** 0.2.0's new joining (once you're free, and the **Join now** button) and 0.2.1's styled names haven't been tried in the game yet.

Everyone who wants to sync presses **F7**. After that, when anyone in the group picks an emote, it starts for the whole group on the same network tick, so you all wave or dance at the same moment. Focus activities are shared too: when one of you starts one, the others join it, and the looping animations stay in step. If you're busy when someone picks one (typing, a window open, mid-emote, in the air), you join as soon as you're free, for up to 30 seconds. Nothing is closed or stopped for you, but a **Join now** button at the top of the screen closes the window, leaves the chat box or stops the emote and joins straight away. Players sitting at a seat spot (bench, classroom desk...) stay where they are. With [Focus Anywhere](#focus-anywhere) installed, you can join on any ground.

**F6** shows or hides the window listing who's in your group. Its messages and window show players' in-game names the way their name tags do, colours and all. Every player in the group needs the mod (0.2.0 and 0.2.1 work together). Players without it don't see anything different.

### Teleport Spots

> **Pre-release:** 0.1.1 fixes spots being lost when the game closes, and that fix hasn't been tried in the game yet.

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

### Desk Snap

> **Pre-release:** snapping beside a friend works, and so do names with colours or other styling in the message at the top. 0.1.2 moves that text into code shared with Emote Sync, and hasn't been tried yet.

Sit down at your desk within 3 m of someone who's already at theirs, and you move into line beside them, facing the same way, with a small gap between the desks. You go to whichever side you were closer to, and if someone's already there, to the end of the row. The mod measures each desk, so desks of every shape and size line up.

It also works when [Emote Sync](#emote-sync) starts your activity because a friend picked it.

**F3** turns snapping on or off (it starts on, and the game remembers your choice). If you're sitting at your desk when you press it, it snaps you right away, or moves you back to where you sat down. It won't put you on a slope, over water, through a wall or next to a classroom desk. `Gap` and `Range` in the settings file change the space between desks and how close you need to sit.

## Installing

Download a mod's zip from the [Releases](https://github.com/Nelphei/OnTogetherMods/releases) page. There's one release per mod, always its newest version, and the ones that haven't been tried in the game yet are marked **Pre-release**. Each mod is a single DLL, and the same download works on Windows and Mac. To update a mod, unzip the new version over the old one.

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

- `HelloTogether/` is a minimal starter mod: a plugin that logs a message and one Harmony patch. Copy the folder, change the GUID and names in `Plugin.cs`, delete the `NoDeploy` line in the `.csproj` (it keeps the template out of the game), and add it with `dotnet sln add`.
- `Shared/` holds code used by more than one mod. `PanelStack.cs` stacks each mod's window along the bottom, right edges lined up 15% in from the right (clear of the chat box) so they don't overlap, and `ClickBlocker.cs` stops clicks on a mod window from reaching the game.
- [CLAUDE.md](CLAUDE.md) has notes on how the game works inside (camera, input, animations, focus, networking, instruments, drawing) that were learned while building these mods.
- To read the game's code, decompile it with [ILSpy](https://github.com/icsharpcode/ILSpy). The decompiled code is GigaPuff's, so keep it out of anything you publish. This repo ignores a local `reference/` folder for it.
