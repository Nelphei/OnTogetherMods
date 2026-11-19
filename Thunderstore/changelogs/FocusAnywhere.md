## 0.6.1

- The icon row has a tray: click the gear button at its right end to open it. Icons you tuck away wait there and work just the same, and a green dot on the gear means a tucked-away mod is switched on.
- Right-click the gear (or click **Arrange icons** in the tray) to arrange the row: drag icons to reorder them, drag them into the tray and back, or click one to tuck it away or put it back. **Reset** puts every icon back. The arrangement is saved in `BepInEx/config/IconRow.json` and shared by every mod in this set. Update the other mods too, so all their icons can be arranged.

## 0.6.0

- The objects window has an **Add item...** button while you focus. It lists the objects of every other activity by where they sit (left hand, right hand, head, set down, other), and ticking one adds it to the activity you're doing: a book while you lie down with the laptop (untick the laptop to swap them), a latte with anything. Each object sits where its own activity puts it, so some fit the pose better than others. Added objects are remembered for that activity and show every time you do it, and other players see them even without the mod. Untick one to take it off.
- Where the game has more than one of an object, the window now says which: the laptop for the desk, the ground, the water bed or the floater, the phone for browsing, and so on.
- Known issue: sleeping at your desk with the desk hidden, the Zzz don't show, even with their box ticked. Show the desk to get them back.

## 0.5.0

- Right-clicking the icon now opens a small window to pick which objects show while you focus, instead of hiding them all. It has **Show all**, **Hands only** and **Hide all** buttons, a box for each group (desk, placed objects, what's in your hands, headphones, floater and water bed, sleep Zzz), and a **Right now** list to make an exception for any object of the activity you're doing. It also has the game's own floatie spin and camera turning settings. Changes work straight away, also while you're focusing, and other players see what you hide.
- If you had the desk and objects hidden in 0.4.0, they stay hidden. The floater and Zzz still show.
- Hiding objects at a classroom seat no longer moves your pet off the classroom desk.

## 0.4.0

- Right-click the icon to do focus activities without their desk or objects: sleep at a desk with no desk, type with no laptop, paint with no easel. Effects like the sleep Zzz stay. Other players see it too, even without the mod. It works while you're already focusing, and it's remembered between games. Right-click again to bring them back.

## 0.3.3

- In the Normal window mode (key 1), the icon also hides and shows with the game's eye button in the top-right corner, as it already did in split screen and Desk mode.

## 0.3.2

- In the game's split-screen and Desk window modes (keys 2-4), the icon hides and shows with the game's own UI when you click its eye button. Before, it always hid in Desk mode and stayed on in split screen. In the Normal window mode it always shows. The mod keeps working while it's hidden.

## 0.3.1

- The icon hides in the game's Desk window mode (key 4, the small see-through window) and while Photo Mode is on. The mod keeps working meanwhile.

## 0.3.0

- An icon in the icon row at the bottom-right of the screen switches the whole mod on and off, for example to use BlueSage QoL's Focus Anywhere instead. Switched off, the focus menu goes back to the game's own activities as soon as you're not focusing. Your choice is remembered between games.

## 0.2.0

- Focus in the air: press the focus key mid-jump or mid-fall and you stop right there, then focus floating where you are. Closing the menu without picking, or standing up, lets you fall again. `MidAir = false` in the settings file turns it off.

## 0.1.0

- First version: focus on any ground, with every activity in the game in the focus menu.
