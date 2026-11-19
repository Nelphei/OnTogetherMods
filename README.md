# On-Together Mods

Small [BepInEx 5](https://github.com/BepInEx/BepInEx) mods for On-Together, the cozy co-working game by GigaPuff. They're client-side, and none of them touch tickets, the shop, achievements or other players' saves. Most are cosmetic; Session Manager gives a session's host tools to run it.

This is an unofficial fan project, not made or supported by GigaPuff.

> **Early days.** Mods marked **pre-release** haven't been tried in the game yet in their current version. Most of the others have only been tried on their own, not in a full lobby with other players, so expect rough edges. See [What's been tested](#whats-been-tested).

## Installing

### Windows: all the mods at once

This installs [r2modman](https://github.com/ebkr/r2modmanPlus) (a mod manager) and every mod from [Thunderstore](https://thunderstore.io/c/on-together/), set up in an r2modman profile called **SoapieScripts**. Open PowerShell (press the Windows key, type `powershell`, press Enter), paste this line and press Enter:

```
irm https://raw.githubusercontent.com/Nelphei/OnTogetherMods/master/Installer/Install.ps1 | iex
```

Or download [Install.cmd](Installer/Install.cmd) and double-click it. When it's done, r2modman opens: pick On-Together, then the SoapieScripts profile, then **Start modded**.

Run it again any time to update the mods. Other mods in the profile, your mod settings and mods you've switched off in r2modman stay as they are. Thunderstore can be a version or two behind the releases below.

### Mac: all the mods at once

This installs [r2modmac](https://github.com/Zard-Studios/r2modmac) (a Mac mod manager) if you don't have it, and saves `SoapieScripts.r2z` in your Downloads folder: a profile file listing every mod from [Thunderstore](https://thunderstore.io/c/on-together/). Open Terminal (press Cmd + Space, type `terminal`, press Enter), paste this line and press Enter:

```
curl -fsSL https://raw.githubusercontent.com/Nelphei/OnTogetherMods/master/Installer/install-mac.sh | bash
```

When it's done, r2modmac opens. In r2modmac:

1. Pick On-Together.
2. Click **Import Profile**, then **Select .r2z or .zip file**, and pick `SoapieScripts.r2z` from Downloads.
3. Choose **MacOS** and click **Import**. r2modmac downloads the mods itself.
4. Click **Apply to Game**, then **Launch Modded**.

To update the mods later, use the profile's **Updates** tab in r2modmac. To get mods that came out since, run the line again and drag the new `SoapieScripts.r2z` onto the open profile. If you copied mods into r2modmac's `BepInEx/plugins` folder by hand before, delete those folders so they don't load twice.

### Windows or Mac, one mod at a time

Download a mod's zip from the [Releases](https://github.com/Nelphei/OnTogetherMods/releases) page. There's one release per mod, always its newest version, and the ones that haven't been tried in the game yet are marked **Pre-release**. Each mod is a single DLL, and the same download works on Windows and Mac. To update a mod, unzip the new version over the old one.

#### Windows

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) (the `win_x64` zip) into the game folder, next to `On-Together.exe`, and start the game once so it sets itself up.
2. Unzip the mod into the game folder. It puts the DLL in `BepInEx\plugins\<mod>\`.
3. Start the game. Each mod writes a line to the BepInEx console when it loads.

#### Mac

The Mac version of the game runs on Apple Silicon, where plain BepInEx can't load mods yet. [r2modmac](https://github.com/Zard-Studios/r2modmac) sets BepInEx up so it works.

1. Add On-Together in r2modmac and launch the game through it once, so BepInEx is installed.
2. Unzip the mod and copy its folder (for example `PhotoMode`) into the `BepInEx/plugins` folder of your r2modmac profile, next to your other mods.
3. Launch the game through r2modmac.

The mods haven't been tested on a Mac yet. Mac keyboards use the F keys for brightness and volume, so hold **Fn** with the mod keys (for example Fn + F9).

## The mods

| Mod | Version | Open with | What it does | Who needs it |
|---|---|---|---|---|
| [Photo Mode](#photo-mode) | 0.4.4, pre-release | F9 | Free camera, hidden UI and PNG photos | Only you |
| [Emote Sync](#emote-sync) | 0.3.5, pre-release | F7 | Emotes and focus activities start together | Everyone in the group |
| [Teleport Spots](#teleport-spots) | 0.2.4, pre-release | F5 | Save spots and teleport back to them | Only you |
| [Song Player](#song-player) | 0.2.4 | Sit at an instrument | Plays MIDI songs on the instrument | Only you (others hear it without the mod) |
| [Trace Overlay](#trace-overlay) | 0.2.4 | Start drawing on a board | A see-through picture to trace on chalkboards and whiteboards | Only you (only you see it) |
| [Focus Anywhere](#focus-anywhere) | 0.6.1, pre-release | The game's focus key | Focus on any ground or in the air, with every activity in the game, pick which of its objects show, and add objects from other activities | Only you (others see your activity without the mod) |
| [Desk Snap](#desk-snap) | 0.2.4, pre-release | Sit at your desk (F3 turns it on/off) | Your desk lines up beside a friend's, with a small gap | Only you (others see the row without the mod) |
| [Chat Range](#chat-range) | 0.2.4, pre-release | Open chat on its Local tab | Lists who's close enough to see your local chat messages | Only you |
| [Name Designer](#name-designer) | 0.2.5, pre-release | F4, or opens by itself at Create Session | Colour each letter of your name (and the colon after it in chat) or a session name, with a preview and 5 presets each | Only you (everyone sees the colours without the mod) |
| [Card Designer](#card-designer) | 0.1.1, pre-release | Click the ID card icon, or **Design card** on the game's ID card | Style the lines of your ID card like in a word processor (bold, colours, highlights, sizes, emoji and symbols), with a preview of the card other players see | Only you (everyone sees your card without the mod) |
| [Outfit Slots](#outfit-slots) | 0.1.2 | F2, or the outfit screen's style list | 6 more outfit slots (9 in all), and a window to swap outfits with one click | Only you (everyone sees your outfit without the mod) |
| [Quick Hide](#quick-hide) | 0.1.0 | F10, in split screen and mini mode | Hides the whole game at once (world, characters and UI) and brings it back | Only you |
| [Network Crash Fix](#network-crash-fix) | 0.1.1, pre-release | Nothing to open, it's always on | Stops a crash in the game's networking code (seen on a Mac) | Only you |
| [Desk Swarm](#desk-swarm) | 0.3.0 | F11 | Fly your desk around, and fly in formation with friends like a drone swarm | Only you to fly (others see it without the mod); everyone in a swarm needs it |
| [Session Manager](#session-manager) | 0.1.0, pre-release | Click the crown icon while you host | Run your session: rename it, change how many can join (past 16), lock it, kick and ban players, and answer join requests | Only the host (players who join don't need it) |
| [Doom Together](#doom-together) | 0.1.0, pre-release | Click the demon icon | Play DOOM in a window over the game (Freedoom comes with it), while your character plays a handheld console | Only you (others see you playing without the mod) |
| [Card Table](#card-table) | 0.1.0 | Click the cards icon | Card games in a window, with pixel-art cards: Solitaire now, with multiplayer and more games coming | Only you |

Every key can be changed in the mod's settings file (see [Settings](#settings)). The game itself uses no F keys. You don't need to remember them: every mod except Network Crash Fix also has an icon in the bottom-right corner of the screen (see [The icon row](#the-icon-row)).

### The icon row

Each mod you've installed puts a small pixel-art icon in a row in the bottom-right corner: a camera for Photo Mode, two cheering friends for Emote Sync, a map pin for Teleport Spots, a palette for Name Designer, an ID card and pencil for Card Designer, a shirt for Outfit Slots, a desk for Desk Snap, someone sitting on a cloud for Focus Anywhere, music notes for Song Player, a picture for Trace Overlay, a speech bubble for Chat Range, a crossed-out eye for Quick Hide, a desk with rotors for Desk Swarm, a crown for Session Manager, a horned demon for Doom Together and two playing cards for Card Table. Network Crash Fix has no icon, since it's always on.

- **Click** an icon to do what the mod's key does: open its window, start Photo Mode, or switch the mod on or off.
- **Hover** over one to see its name, its key, and whether it's on.
- **Right-click** Emote Sync's icon to show or hide its window (F6) while sync is on, Outfit Slots' icon to put on your next outfit, Desk Swarm's icon to take off or stop flying, or Focus Anywhere's icon to choose which objects show (or add others) while you focus.
- A **green dot** means the mod is switched on. A **greyed-out** icon means it's off.

Song Player, Trace Overlay, Chat Range and Focus Anywhere have no key, so their icon is how you switch them off and on. Your choice is remembered between games.

**The tray.** The gear button at the right end of the row opens the tray, where the icons you've tucked away wait. They work there just like in the row, and clicking one closes the tray again. A green dot on the tray button means a tucked-away mod is switched on.

**Arranging the row.** Right-click the tray button, or click **Arrange icons** in the tray. Then drag icons left or right to reorder them, drag them up into the tray to tuck them away or back down into the row, or click one to send it to the other place. **Done** finishes, and **Reset** puts every icon back in the row in its usual place. The arrangement is saved in `BepInEx\config\IconRow.json`, and it's the same for every mod. A mod you install later starts in the row, about where it would usually be. An icon from a mod version older than the tray stays in its usual place in the row until that mod is updated.

The row only shows in a world, and hides while a game menu or panel is open (pause menu, inventory, shop, player list, focus menu, emote wheel) and while Photo Mode is on. When it reaches under the mods' windows, they sit just above it. `ShowIcon` in each mod's settings file hides that mod's icon completely (it isn't in the tray either).

The game has an eye button that hides its UI (top-right in the Normal window mode, and in split-screen and Desk mode too). Every mod's icons, windows and messages hide and show with it. The mods keep working meanwhile, and their keys still work.

### What's been tested

| Mod | Tried in the game |
|---|---|
| Photo Mode 0.4.4 | **Pre-release.** 0.2.1 worked, including in a lobby with other players, and so do 0.4.0's icon, 0.4.1 hiding the icon row while Photo Mode is on, and hiding with the game's eye button in every window mode. 0.2.2 keeps other mods' messages and windows on screen while Photo Mode hides the game's UI, and 0.3.0 lets you type in chat. Neither has been tried yet |
| Emote Sync 0.3.5 | **Pre-release.** 0.1.0 worked with a friend, and so do 0.3.0's icon, hiding with the game's eye button in every window mode, and 0.3.4's fix for players twitching in an innertube pose. 0.2.0's joining once you're free and the **Join now** button, and 0.2.1's styled names, haven't been tried yet |
| Teleport Spots 0.2.4 | **Pre-release.** Teleporting works on your own screen, and so do 0.2.0's icon and hiding with the game's eye button in every window mode. 0.1.1's fix for keeping spots after a restart and 0.1.2's fix for where other players see you land haven't been tried yet |
| Song Player 0.2.4 | Works on its own, including the icon and hiding with the game's eye button in every window mode. Not yet checked with other players listening |
| Trace Overlay 0.2.4 | Works, including the icon and hiding with the game's eye button in every window mode |
| Focus Anywhere 0.6.1 | **Pre-release.** 0.1.0 worked on its own, and so do 0.3.0's icon, hiding with the game's eye button in every window mode, 0.4.0's hiding the desk and objects, and 0.5.0's objects window. 0.2.0's focusing in the air and 0.6.0's **Add item...** list haven't been tried yet. Known issue: the Zzz don't show while sleeping at a hidden desk |
| Desk Snap 0.2.4 | **Pre-release.** Snapping works on your own screen, and so do styled names in its message, 0.2.0's icon and hiding with the game's eye button in every window mode. 0.1.4 puts you at exactly your friend's height and allows rows in the air, 0.1.3 fixes other players seeing you in the wrong spot, and 0.1.2 moves the message text into code shared with Emote Sync. None of these has been tried yet |
| Chat Range 0.2.4 | **Pre-release.** 0.2.0's icon works, and so does hiding with the game's eye button in every window mode. The list itself hasn't been tried in the game yet |
| Name Designer 0.2.5 | **Pre-release.** Colouring your name works, and so do 0.2.0's colon colour, the icon and hiding with the game's eye button in every window mode. The Session tab, presets, 0.2.3's designing in Session Manager's name box and 0.2.4's opening beside Card Designer haven't been tried in the game yet |
| Card Designer 0.1.1 | **Pre-release.** Opening the window on the game's ID card page works, and so does 0.1.1's Symbols tab fix. The styling tools, the game's emoji, presets, **Apply card** reaching other players and the Name Designer link haven't all been confirmed yet |
| Outfit Slots 0.1.2 | Works, including staying in your slot after a restart and hiding with the game's eye button in every window mode. Not yet checked with other players watching |
| Quick Hide 0.1.0 | Works, in split screen and mini mode |
| Network Crash Fix 0.1.1 | **Pre-release.** Not tried in the game yet |
| Desk Swarm 0.3.0 | **Pre-release.** 0.2.0 works, including its new flying and landing shapes, and so does 0.3.0's flying through buildings on your own. Followers flying through buildings with you hasn't been tried yet |
| Session Manager 0.1.0 | **Pre-release.** Not tried in the game yet |
| Doom Together 0.1.0 | **Pre-release.** DOOM plays in the game, your character sits down with the handheld console, and the volume slider works. Moving the window, the controls card, saves and the rest haven't all been tried yet |
| Card Table 0.1.0 | Works in the game. The "You won!" box hasn't been seen yet, since nobody has won a game in it so far. The Solitaire rules were also checked outside the game over thousands of random games |

The icon row's tray and arranging the row work, in every mod's newest version.

None of the mods have been tried on a Mac yet.

### Photo Mode

> **Pre-release:** 0.3.0 lets you type in chat while Photo Mode is on. 0.2.2 keeps other mods' messages and windows (like Emote Sync's) on screen while Photo Mode hides the game's UI, and they still stay out of your photos. Neither has been tried in the game yet.

Press **F9** (or click the camera icon) in a lobby to detach the camera and hide the game's UI, then **P** to save a photo.

- Hold right mouse to look, **WASD** to move, **Q / E** to go down / up, scroll to zoom
- **Shift** for faster, **Ctrl** for slower
- **H** shows or hides name tags and chat bubbles, **T** shows the chat box, **R** puts the camera back where it started
- With the chat box showing, press **Enter** or click it to type (the photo keys pause while you type), and **Esc** to stop typing
- **F1** hides the controls panel, **F9** or **Esc** exits. The mod icons hide while Photo Mode is on

Photos are saved to `Pictures\On-Together`. You can save them bigger than your screen with `ResolutionMultiplier` (2 on a 1080p screen gives 4K). Photo Mode only works in the game's Normal window mode, not Landscape, Portrait or Desk. If you've hidden the game's UI with its eye button, the controls panel and messages stay hidden in Photo Mode too.

### Emote Sync

> **Pre-release:** 0.2.0's new joining (once you're free, and the **Join now** button) and 0.2.1's styled names haven't been tried in the game yet.

Everyone who wants to sync presses **F7** or clicks the mod's icon. After that, when anyone in the group picks an emote, it starts for the whole group on the same network tick, so you all wave or dance at the same moment. Focus activities are shared too: when one of you starts one, the others join it, and the looping animations stay in step. If you're busy when someone picks one (typing, a window open, mid-emote, in the air), you join as soon as you're free, for up to 30 seconds. Nothing is closed or stopped for you, but a **Join now** button at the top of the screen closes the window, leaves the chat box or stops the emote and joins straight away. Players sitting at a seat spot (bench, classroom desk...) stay where they are. With [Focus Anywhere](#focus-anywhere) installed, you can join on any ground.

**F6** (or right-clicking the mod's icon) shows or hides the window listing who's in your group. Its messages and window show players' in-game names the way their name tags do, colours and all. Every player in the group needs the mod (0.2.0 and every later version work together). Players without it don't see anything different.

### Teleport Spots

> **Pre-release:** 0.1.2 fixes other players seeing you land too far away when you teleport (if you aren't the lobby host). 0.1.1 fixes spots being lost when the game closes. Neither has been tried in the game yet.

Press **F5** or click the map pin icon to open the window. Click **Save this spot** to remember where you're standing (and which way the camera faces), then click a spot's name later to jump back there. Spots can be renamed or deleted, and they're kept in `BepInEx\config\TeleportSpots.json` if you want to back them up.

### Song Player

Put `.mid` files in `Music\On-Together`, then sit at an instrument. The song window opens by itself. Click a song to play it.

- **Part** switches between the full song, just the melody, or one instrument from the file
- The drum kit plays the file's drum track
- Each instrument only has 13 keys, so the mod picks the octave that fits the song best and moves any other notes into range. The octave change is the same as clicking the game's octave arrows, so other players see it too.
- Big chords are trimmed to 6 notes at once (`MaxNotesAtOnce`)

Everyone nearby hears the song, with or without the mod. Click **Stop**, or stand up (Space or Esc), to stop. Switching the mod off with its icon also stops the song and closes the window until you switch it back on.

### Trace Overlay

Put `.png` or `.jpg` files in `Pictures\On-Together\Trace`, then start drawing on a chalkboard or whiteboard. The window opens by itself. Click an image to lay it over the board, set how see-through it is, and trace over it with chalk. Only you can see the picture. Switch the mod off with its icon and the window and picture stay away until you switch it back on.

### Focus Anywhere

> **Pre-release:** 0.2.0 lets you focus in the air and 0.6.0 adds the **Add item...** list. Neither has been tried in the game yet.

Press the game's usual focus key wherever you are: on grass, on rocks, at the music hill, next to a seat someone else is using, or in the air. In the air you stop where you are and focus floating there. Closing the menu without picking, or standing up, lets you fall again. The focus menu also lists every activity in the game, not just the ones for where you're standing:

- The water activities work on land, and the land ones work in water. On land, floater activities bring out your floater.
- A new **Play** category has the play tower activities.
- Activities from seat spots, like the tree hanger, the waterfall pit and the bean bag, work anywhere.

Seat spots that are free (benches, desks, the swan...) still open their own menu and sit you down on the seat. Seat poses done on open ground sit on thin air, and other players see that too. Summer DLC activities stay hidden unless you own the DLC, and the basketball court stays off limits. Focus time counts the same as normal focus.

Right-click the mod's icon to open a small window where you pick which of an activity's objects show while you focus: sleep at a desk with no desk, type with no laptop, or keep the book in your hands but lose the floater. A ticked box means shown.

- **Show all**, **Hands only** and **Hide all** set everything in one click.
- One box for each group: the desk, placed objects (laptop, tablet, easel, ironing board, treadmill...), what's in your hands (book, phone, pens and brushes, iron, game controller...), headphones, the floater and water bed, and the sleep Zzz. Hiding the desk sends your pet to your head. Hiding the floater or water bed means you sit lower, at swimming height, and don't spin, because every player's game only lifts and spins you while the floatie shows.
- **Right now** lists the objects of the activity you're doing, so one of them can go against its group, like keeping the ironing board while other placed objects are hidden. **reset** puts it back with its group. The choice counts in every activity that uses that object (the laptop, the headphones and the pencil come up in several).
- **Add item...** (while you focus) lists every other activity's objects by where they sit: left hand, right hand, head, set down, and the rest (jump rope, Zzz). Tick one to add it to the activity you're doing, like a book while you lie down with the laptop (untick the laptop to swap them) or a latte with anything. An added object sits where its own activity puts it, in the same hand or the same spot in front of you, so some fit the pose better than others. It's remembered for that activity, shows whatever the boxes above say, and other players see it. Untick it to take it off. The easel can't be added, because it only paints in its own activity.
- **Only on your screen** has two of the game's own settings: whether floaties spin (everyone's, as you see them) and whether the camera turns by itself while you focus.

Changes work straight away, also while you're focusing. Other players see what you hide or add, even without the mod, and your choices are remembered between games. Right-click the icon again to close the window.

**Known issue:** sleeping at your desk with the desk hidden, the Zzz don't show, even with their box ticked. Show the desk to get them back.

Click the mod's icon to switch it all off, for example to use BlueSage QoL's Focus Anywhere instead. The focus menu goes back to the game's own activities as soon as you're not focusing, and every object shows again. `AllActivities`, `AnyGround` and `MidAir` in the settings file turn each part off. `MidAir` needs `AnyGround`. The `[Objects]` settings are what the window changes. While it's switched off, added objects don't show.

### Desk Snap

> **Pre-release:** snapping beside a friend works on your own screen, and so do names with colours or other styling in the message at the top. 0.1.4 puts you at exactly your friend's height (before, starting from somewhere higher left you floating) and lets you snap in the air. 0.1.3 fixes other players seeing you in the wrong spot, with your desk turning twice, when you aren't the lobby host. 0.1.2 moves the message text into code shared with Emote Sync. None of these has been tried in the game yet.

Sit down at your desk within 3 m of someone who's already at theirs, and you move into line beside them, facing the same way, at the same height, with a small gap between the desks. You go to whichever side you were closer to, and if someone's already there, to the end of the row. The mod measures each desk, so desks of every shape and size line up.

Rows stay level even where the ground drops away, and they work in the air too: with [Focus Anywhere](#focus-anywhere), focus at your desk in mid-air next to a friend who's floating at theirs.

It also works when [Emote Sync](#emote-sync) starts your activity because a friend picked it.

**F3** or the desk icon turns snapping on or off (it starts on, and the game remembers your choice). If you're sitting at your desk when you press it, it snaps you right away, or moves you back to where you sat down. It won't put you inside the ground or a wall, or next to a classroom desk. `Gap` and `Range` in the settings file change the space between desks and how close you need to sit.

### Chat Range

> **Pre-release:** the icon and hiding it with the game's eye button work, but the list itself hasn't been tried in the game yet.

Local chat messages (and their chat bubbles) only reach players within 5 m of you, counting height too. While the chat box is open on its **Local** tab, a small box just above it lists everyone in that range, with their names styled the way their name tags are. It says so when nobody's close enough. If the chat box is dragged to the top of the screen, the list sits beside it instead.

The list hides when chat is minimised and when the game's UI is hidden (including with its eye button). In [Photo Mode](#photo-mode) it shows only while the chat box does, and it stays out of your photos. Someone who has muted you is still listed, but won't see your messages. The speech bubble icon switches the list off and on. `ShowOnGlobalTab` in the settings file shows the list on the Global tab too, and `MaxNames` sets how many names it lists before counting the rest.

### Name Designer

> **Pre-release:** colouring your name works, and so do 0.2.0's colon colour, the icon, and hiding the window with the game's eye button. The Session tab, presets, 0.2.3's designing in Session Manager's name box and 0.2.4's opening beside Card Designer haven't been tried in the game yet.

Give every letter of your name its own colour. Press **F4** or click the palette icon to open the window. It has two tabs:

- **Name** designs your player name. Click **Apply name** and everyone sees it, on your name tag, in chat and in the player list. On the main menu it's saved for the next time you join. With [Card Designer](#card-designer) installed, its **Design name** button opens this tab beside its window.
- **Session** designs the name of a session you create. On the main menu the window opens by itself beside the Create Session box, and whatever you design goes straight into that box, so just click Create. While you host a session with [Session Manager](#session-manager) installed, its **Design** button opens this tab beside its window, and the name goes into its name box instead: click **Rename** there to use it.

Type the name, then click letters to pick them (drag across or Shift-click for a run, Ctrl-click to add one, or pick none to colour the whole name) and choose a colour from the square and strip, a swatch, or a hex code. **Gradient** blends from the first picked letter's colour to the last one's, and **Rainbow** spreads a rainbow across them. The preview above the window shows the name in the game's own lettering.

On the Name tab, the **:** tile after your letters is the colon after your name in chat ("Name: hello"). Click it and pick a colour to colour the colon; your message keeps the game's normal text colour. The game's "has joined the server!" and "joined your pomodoro session." messages, and the "(Host)" label in the player list, come right after your name, so they take the colon colour too. Pick the tile and click **No colour** to go back to the game's colour.

Each tab has 5 presets: **Save** puts the current design in a slot, and clicking a slot loads it. They're kept in `BepInEx\config\NameDesigner.json`.

The colours are the same colour tags players can type into their names by hand, so other players see them without the mod. Other styling in a name, like sizes or bold, can't be edited here and is left out when you load it. `OpenWithCreateSession` in the settings file stops the window opening by itself on the main menu.

### Card Designer

> **Pre-release:** opening the window on the game's ID card page works, and so does 0.1.1's Symbols tab fix. The styling tools, the game's emoji, presets, **Apply card** reaching other players and the Name Designer link haven't all been confirmed in the game yet.

Style the lines of your ID card (the card other players see when they click you) the way you would in a word processor. Click the ID card icon, or the **Design card** button on the game's own ID card page (on the main menu or in a world), to open the window.

- **The typing box** holds one line of your card and shows the styling as you type, in the card's own lettering and colours. **Enter** starts a new line.
- **The toolbar** styles the selected text, or the whole line when nothing is selected: **B**, **I**, **U** and **S** (bold, italic, underline and strikethrough, also Ctrl+B, Ctrl+I and Ctrl+U), **Colour** (a colour square, swatches, a hex code, **Gradient** and **Rainbow**), **Highlight**, **Size**, **Align** (left, centre or right) and **Clear** to take the styling off. **Undo** and **Redo** (Ctrl+Z, Ctrl+Y) step back and forth through each line's changes. **Code** shows the tags themselves, to type them by hand.
- **Emoji** puts one of the game's 15 emoji, or one of 176 symbols (hearts, stars, music notes, weather, arrows, shapes and bits for making faces), at the caret. These are the ones that show in the game: emoji typed from the keyboard don't.
- **What others see**, on the right, is your card as other players get it, with the same box sizes and lettering, so you can tell what fits. Click a line there to edit it.
- **Apply card** saves the card and sends it to everyone. On the main menu it's saved for the next time you join. **Reset** goes back to your card as it is now.
- 5 **presets** save whole card designs: **Save** puts the card in a slot, and clicking a slot loads it. They're kept in `BepInEx\config\CardDesigner.json`.

**What fits.** Each line holds 1000 characters, tags included: the counter under the typing box shows how many you've used (each change of colour uses about 17, and each emoji 27). The short lines show one line of normal-size text and end in "..." when it's too long; at 70% size or smaller, two lines fit. The quote shows 3 lines at any size, and the game cuts off the rest when you open your ID card page, so the preview shows only 3.

The card's first line is your player name. With [Name Designer](#name-designer) installed, its **Design name** button opens Name Designer beside the window. The styling uses the same rich text tags the game's own ID card takes, so other players see it without the mod. In the settings file, `WindowKey` sets a key to open the window (there's none by default, since other mods use every F key), and `ShowCardButton` hides the button on the game's ID card page.

### Outfit Slots

Adds slots 4 to 9 to the game's 3 outfit slots. They're in the outfit screen's style list below slots 1-3 and work the same way: pick one, change your look, and name it in the name box. Whatever you wear is saved in the slot you're on, and you're still wearing it after a restart. A slot you haven't used yet starts as a copy of what you're wearing.

Press **F2** or click the shirt icon to open a window listing all 9 slots, and click one to put it on without opening the outfit screen. Right-click the icon to put on your next outfit.

Slots 4-9 are kept in `BepInEx\config\OutfitSlots.json`. The game's own save only ever holds slots 1-3, so if you remove the mod, you're back in whichever of slots 1-3 you wore last. Other players see your outfit without the mod. `CloseAfterSwap` in the settings file closes the window once you pick an outfit.

**BlueSage QoL** has its own style slots 4-9, which change the same part of the game, so Outfit Slots stays off while they're on. To switch them off, set `EnableExtendedAvatarStyles = false` in `BepInEx\config\com.bluesage.ontogether.qoltweaks.beta.cfg` (or use BlueSage's menu) and restart the game. The first time Outfit Slots runs, it copies BlueSage's slots 4-9 (outfits and names) into its own, and leaves BlueSage's file as it was.

### Quick Hide

For the game's split-screen and mini window modes (keys 2-4), where the game sits over your desktop. Press **F10** or click the crossed-out eye icon and the whole game disappears at once: the world, every character, the game's UI and the mods' windows. Clicks go straight through to whatever is behind it, and if you were using the game, the keyboard goes back to the app you were in before. Press **F10** again to bring it all back. The key works even while you're in another app, and clicking On-Together in the taskbar (or picking it with Alt+Tab) brings the game back too.

The game keeps running while it's hidden: you stay in the session, other players still see your character, and sounds and voices keep playing. Switching window modes or leaving the world shows the game again. In the Normal window mode (key 1) Quick Hide does nothing, and its icon is greyed out.

If you change `HideKey` in the settings file, pick a key you don't type with, since it works in every app. On a Mac, the key only works while the game is focused.

### Network Crash Fix

> **Pre-release:** not tried in the game yet.

Fixes a crash in the game's networking code, seen on a Mac, where the game closed by itself several times a day while playing in a session. When the game reads another player's movement, it reads a few bytes past the end of the data. Usually those bytes are just other memory and nothing happens, but when they belong to nothing, the whole game crashes. Network Crash Fix reads the end of each packet one byte at a time instead. The game gets exactly the same values as before, so nothing else changes.

There's nothing to open and no icon: it's always on, from the moment the game starts. To turn it off, remove the mod. Each time the game starts, it checks its reading against the game's own and writes the result to the BepInEx log. If they don't match (say, after a game update), it stays off and leaves the game alone.

When the game crashes like this, it leaves a crash file behind. On the Mac where the crash was found, those files were named `mono_crash.95b4e0c32.<number>.json`.

### Desk Swarm

> **Pre-release:** 0.3.0's flying through buildings works on your own, but followers flying through them with you hasn't been tried with other players yet.

Your desk becomes a drone. Press **F11** or click the icon (a desk with rotors) to open the window.

**Flying your desk.** Sit at your desk (any focus activity on open ground works too, but not benches or other seat spots), then click **Take off** in the window, or right-click the icon. Fly with your movement keys (WASD), **Space** to go up and **Shift** to go down, and hold right mouse to look around as usual. The desk turns to face where it's going and tips a little as it flies. It flies through buildings, bridges, fences and trees, but never down through the ground or water. To land on a roof or a bridge, untick **Fly through buildings** in the window: then anything solid stops your desk flying down into it. While you fly, Space doesn't stand you up: click **Stop flying** first, then Space stands you up wherever you are (in the air, you fall). You don't need anyone else to fly.

**Swarms.** Click **Start a swarm** and other players with Desk Swarm see it in their window and can click **Join**. Each follower's desk flies to its own spot around you and stays there wherever you walk or fly, bobbing slightly like a drone. As the leader you pick the shape. Flying shapes:

- **Ring**: desks circle around you overhead
- **V**: a V trailing behind you, like geese
- **Line**: a row beside you, like Desk Snap's
- **Tower**: stacked on top of you
- **Trail**: one behind another, following your path
- **Heart**: a heart standing up over your head
- **Wheel**: a Ferris wheel turning slowly over your head, the desks staying level
- **Twister**: a spinning spiral winding up around you, each desk higher and further out
- **Wave**: a row beside you with a ripple running along it from left to right
- **Stairs**: steps climbing into the sky ahead of you

Landing shapes bring the desks down to sit on the ground. While you walk or fly off they hop up and follow, and they land again when you stop:

- **Row**: side by side with you, facing the same way
- **Class**: rows in front of you, facing you like a class facing the teacher
- **Circle**: a circle with you in it, everyone facing the middle
- **Table**: two rows facing each other across a long table, with you in the middle of one
- **Grid**: an even square grid like drones laid out before a show, everyone facing the same way, with you in the middle of the front row

**Spread** spaces the desks out or packs them closer. Your **Fly through buildings** switch goes for your followers too (theirs is greyed out while they follow), and landing shapes still land on the floor you're standing on, like a deck or a bridge. Followers need to sit at their desk to fly to their spot (joining doesn't sit you down), and standing up drops them out of the formation until they sit again. **Leave** or **End swarm** stops it, and the desks stay where they are.

Everyone sees the flying, mod or not, because each player's game only ever moves their own character. Everyone in a swarm, leader included, needs the mod (0.2.0 or later for the shapes added in it, and 0.3.0 for followers to fly through buildings). Focus time counts the same as normal while you fly. When you stand up away from where you sat down, the mod tells the game you've left the areas there (so pressing F later doesn't open a bench menu from across the map).

`FlyKey` in the settings file sets a key for taking off (there's none by default), `UpKey` and `DownKey` change Space and Shift, `FlySpeed` and `ClimbSpeed` set how fast you fly, `Tilt` stops the tipping, `ThroughBuildings` is the window's **Fly through buildings** switch, and `StartFormation` picks the shape a new swarm starts with.

### Session Manager

> **Pre-release:** not tried in the game yet. Sessions bigger than 16 players haven't been tried at all.

Tools for running a session you host. Click the crown icon to open the window. It's greyed out unless you're the host, and players who join your session don't need the mod. The window has four tabs:

- **Session** changes the session while it's running: its name, how many players it takes (Shift-click the arrows to jump by 10), **Public** (in everyone's session list) or **Private** (only friends and invites see it), **Ask to join**, and the game's tags (Focus, Mature, Chill, Break, Modded). Changes reach new players straight away, and everyone already in the session stays. **Locked** lets no one new in until you untick it. **Invite friends** opens Steam's invite window. **Save setup** remembers the name, size, listing, joining and tags, and **Use saved** puts them all back in a later session.
- **Players** lists who's here and for how long, and who has left this session. **Kick** sends a player back to the main menu, and they can join again. **Ban** keeps them out for 1 hour, 1 day, 1 week or forever (pick at the top): the game turns them away and turns down their requests to join. You can also ban someone who has already left, which the game can't do. Both buttons ask "Sure?" first.
- **Requests** lists everyone asking to join (with **Ask to join** on), with **Accept** and **Reject** for each, and **Accept all**. The game's own popup still shows them one at a time too. **Let Steam friends in** accepts your friends straight away, and **Away** turns everyone down while you're busy. A locked session turns requests down too.
- **Bans** lists everyone you've banned, with how long each ban has left and **Unban**. Click a name to see when they were banned, copy their Steam ID, open their profile, or write a note to yourself. Timed bans end by themselves, even while you're on the main menu.

The game has no "you were kicked" screen, so a kicked player sees the game's "banned" screen, though they can join again straight away. A player whose game doesn't leave when told to (a modded game can ignore it) is disconnected a few seconds later.

**Bigger sessions.** The game's Create Session panel stops at 16 players. With Session Manager its arrows go up to 128 (Shift-click jumps by 10), and so does the Session tab. Sessions bigger than 16 haven't been tried: the game, its voice chat or its text chat may struggle with that many players. If BlueSage's (or officerballs') **PlayerLimitLift** is installed, Session Manager leaves the Create Session panel to it, and its Session tab goes as high as PlayerLimitLift does.

**With [Name Designer](#name-designer)** installed, the Session tab has a **Design** button next to the name. It opens Name Designer beside the window, and the name you design goes straight into the name box. Click **Rename** to use it.

Session Manager uses the game's own ban list, so bans made in the game's player list (or with BlueSage QoL) show up here too, and the other way round. Ban lengths and notes, and your saved setup, are kept in `BepInEx\config\SessionManager.json`. In the settings file, `MaxPlayers` sets the biggest session (16 to 249), `SizeStep` the Shift-click jump, `LetFriendsIn` is the Requests tab's switch, `WindowKey` sets a key to open the window (there's none by default, since other mods use every F key), and `Icon` swaps the crown for one of two other pictures: `Friends` (a crown over three friends) or `Clipboard` (a guest list).

### Doom Together

> **Pre-release:** DOOM plays in the game, your character sits down with the handheld console, and the volume slider works. Moving the window, the controls card, saves and the rest haven't all been tried yet.

Play DOOM in a window over the game. Click the demon icon to open it. It comes with **Freedoom**, a free game made to play like DOOM: Freedoom 1 has four episodes like The Ultimate DOOM, and Freedoom 2 has 32 levels like DOOM II. You can add your own copy of DOOM too.

- **Playing.** Opening the window gives DOOM the keyboard: **WASD** or the arrow keys to move and turn, **Ctrl** to shoot, **Space** to open doors and press switches, **Alt** + arrows to sidestep, **1-7** for weapons, **Tab** for the map and **Esc** for DOOM's menu (new game, save, load, options, quit). DOOM always runs; hold **Shift** to walk. It's keyboard only, like the original: the mouse doesn't aim. The F keys are left to the other mods, since DOOM's menu does everything they would.
- **Back to the game.** Click anywhere outside the window and the keyboard goes back to the game, and DOOM freezes (sound and music too) until you click the window again. Opening chat or a game menu does the same.
- **Your character.** If you're standing when you open DOOM, your character sits down and plays the handheld console, so other players see you gaming. If you're already sitting (at your desk, on a bench, flying with Desk Swarm...), nothing changes. Closing DOOM doesn't stand you up: press Space as usual once you're back in the game. With [Desk Swarm](#desk-swarm), flying your own desk waits while DOOM has the keyboard, and following someone's swarm carries on while you play.
- **The window.** The title row shows the game you're playing (click it to pick another), a speaker for DOOM's volume (click it for a slider: drag it or scroll over it, and the speaker shows a cross at 0), **?** for a card listing DOOM's keys (it also shows by itself the first time you open DOOM; DOOM waits while it's up, and any key or a click closes it), the size (**S**, **M** or **L**) and **X** to close. Drag the title row to move the window anywhere on screen; it stays there next time. Double-click the title row to put it back with the other mods' windows. Closing keeps your game where it is: open the window again to carry on. Quitting from DOOM's menu ends it.
- **Your own DOOM.** Put `DOOM.WAD`, `DOOM2.WAD`, `TNT.WAD`, `PLUTONIA.WAD` or the shareware `DOOM1.WAD` in `Documents\On-Together\Doom` (the game list's **Open WAD folder** button opens it). They're in the folder you installed DOOM to, for example Steam's DOOM + DOOM II. Picking another game starts it over, so save first. Add-on maps (PWADs) aren't supported yet.
- **Saves and options.** Saved games are kept for each game separately in `BepInEx\config\DoomTogether\saves`, and DOOM's own options (its volumes, and key bindings in `managed-doom.cfg`) in `BepInEx\config\DoomTogether`.

In the settings file, `Volume` is the speaker slider's setting (on top of DOOM's own volume options), `Music` turns the music off, `SitDown` stops your character sitting down, `PauseWithoutKeys` keeps DOOM running when it doesn't have the keyboard, `Size` and `Smooth` change the picture, `WindowX` and `WindowY` are where you dragged the window (-1 keeps it with the other windows), `ControlsSeen` set back to false shows the controls card by itself again, `WadFolder` moves the folder for your own WADs, and `WindowKey` sets a key to open the window (there's none by default, since other mods use every F key).

**Credits and licences.** DOOM runs on [Managed Doom](https://github.com/sinshu/managed-doom) by Nobuaki Tanaka, a C# version of id Software's DOOM source code, under the GNU GPL version 2, so Doom Together is GPL version 2 as well, and its source is in this repo. The music plays through [MeltySynth](https://github.com/sinshu/meltysynth) (MIT licence) with the TimGM6mb sound bank (GPL). [Freedoom](https://freedoom.github.io/) is made by the Freedoom project (BSD licence). The licence texts are in the mod's folder, beside its DLL. DOOM is a trademark of id Software. This mod isn't made or supported by id Software or Bethesda.

### Card Table

Card games in a window over the game, with the mod's own pixel-art cards to match the icon row. Click the cards icon to open it.

> **Coming soon:** Solitaire is the first game. Multiplayer card games are on the way, starting with Go Fish at tables you can join with other players in your session (with computer players to fill empty seats), and more card games after that.

- **Solitaire.** The classic kind (Klondike). Build the four piles at the top right up from Ace to King, one suit each. In the seven piles below, stack cards downwards in alternating colours (red on black, black on red); you can move a run of cards together, and only a King (or a run starting with one) goes on an empty pile.
- **Playing.** Drag a card, with the cards on it, onto a pile, or just click it to send it where it fits (up top first, if it can go there). Click the deck at the top left to turn cards over, and click its empty spot to turn the pile back over. **Draw 1** / **Draw 3** picks how many cards the deck turns over, straight away. **Undo** takes back as many moves as you like. **Auto-finish** shows up once no card is face down and it can finish the game for you.
- **Your game keeps.** Close the window, or the game, and your game is waiting next time, clock included (it only runs while the window is open). **Stats** shows games played and won, your winning streak, best time and fewest moves. A game counts as played from its first move, and starting a new one before you've won ends your streak, so **New game** asks for a second click.
- **The window.** **?** shows how to play (it also shows by itself the first time), **M** / **L** changes the size, and **X** closes. The cards grow in whole pixel steps, so they stay sharp. Drag the title row to move the window anywhere on screen; double-click the title row to put it back with the other mods' windows. It's mouse only: the keyboard stays the game's, and your character carries on doing whatever it was doing.

The game in progress and your stats are kept in `BepInEx\config\CardTable.json`. In the settings file, `DrawThree` is the Draw button's setting, `Animations` turns off the cards sliding and the dealing animation, `Size` is the M / L button, `WindowX` and `WindowY` are where you dragged the window (-1 keeps it with the other windows), `HelpSeen` set back to false shows the how-to-play card by itself again, and `WindowKey` sets a key to open the window (there's none by default, since other mods use every F key).

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
- `Shared/` holds code used by more than one mod. `PanelStack.cs` stacks each mod's window along the bottom, right edges lined up 15% in from the right (clear of the chat box) so they don't overlap, `ClickBlocker.cs` stops clicks on a mod window from reaching the game, and `IconBar.cs` draws each mod's icon in the row at the bottom-right.
- [CLAUDE.md](CLAUDE.md) has notes on how the game works inside (camera, input, animations, focus, networking, instruments, drawing) that were learned while building these mods.
- To read the game's code, decompile it with [ILSpy](https://github.com/icsharpcode/ILSpy). The decompiled code is GigaPuff's, so keep it out of anything you publish. This repo ignores a local `reference/` folder for it.
