using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace BubbleShell
{
    // where, tp, emote, sit, stand, say: things your character does.
    internal static class MeCommands
    {
        public const string Group = "You";

        public static void Register(Shell shell)
        {
            shell.Register(new Command
            {
                Name = "where",
                Aliases = new[] { "pos" },
                Group = Group,
                Usage = "where",
                Help = "Where you are on the map and which way you face.",
                Run = Where,
            });
            shell.Register(new Command
            {
                Name = "tp",
                Aliases = new[] { "teleport" },
                Group = Group,
                Usage = "tp <x y z> | tp <player>",
                Help = "Moves you to a spot (from where), or beside a player. ~ means where you are now: " +
                       "tp ~ ~2 ~ hops you up 2. Standing, you land on the ground beside a player. Sitting at your " +
                       "desk (or any activity on open ground) you go as you are, desk and all, at their height, so " +
                       "you can teleport around while flying with Desk Swarm; not at a seat spot like a bench.",
                Run = Teleport,
                Complete = (s, a, i) => i == 0 ? Game.Players().Where(p => !p.IsMe).Select(p => p.PlainName) : null,
            });
            shell.Register(new Command
            {
                Name = "emote",
                Group = Group,
                Usage = "emote <number> | emote list",
                Help = "Plays an emote, numbered around the emote wheel from the top.",
                Run = Emote,
            });
            shell.Register(new Command
            {
                Name = "sit",
                Group = Group,
                Usage = "sit <kind> [number] | sit list [kind]",
                Help = "Sits you down in a focus activity, like the focus menu: sit read, sit write 2. " +
                       "sit list shows the kinds, sit list read shows what's in one. Already sitting, it switches.",
                Run = Sit,
                Complete = (s, a, i) => i == 0
                    ? Enum.GetNames(typeof(FocusType)).Select(n => n.ToLowerInvariant()).Concat(new[] { "list" })
                    : i == 1 && a.Is(0, "list") ? Enum.GetNames(typeof(FocusType)).Select(n => n.ToLowerInvariant()) : null,
            });
            shell.Register(new Command
            {
                Name = "stand",
                Group = Group,
                Usage = "stand",
                Help = "Stands you up, like the stand-up key.",
                Run = Stand,
            });
            shell.Register(new Command
            {
                Name = "say",
                Group = Group,
                Usage = "say <message>",
                Help = "Says it in chat, on the chat tab you have open (Local or Global). The game's own " +
                       "limits apply: 250 letters, 10 messages in 5 seconds.",
                Run = Say,
            });
        }

        private static IEnumerator Where(Shell shell, Args args)
        {
            PlayerMovementController move = Game.MyMovement;
            if (move == null)
            {
                shell.Warn("Join a world first.");
                return null;
            }
            Vector3 p = move.transform.position;
            float facing = move.transform.eulerAngles.y;
            PlayerFocusController focus = Game.MyFocus;
            string doing = focus != null && focus.IsFocus ? "sitting" : move.IsInWater ? "swimming" : "standing";
            shell.Print($"x {F(p.x)}  y {F(p.y)}  z {F(p.z)}");
            shell.Hint($"facing {facing:0}°, {doing}. Back here later: tp {F(p.x)} {F(p.y)} {F(p.z)}");
            return null;
        }

        private static IEnumerator Teleport(Shell shell, Args args)
        {
            PlayerMovementController move = Game.MyMovement;
            if (move == null)
            {
                shell.Warn("Join a world first.");
                return null;
            }
            if (args.Count == 0)
            {
                shell.Warn("Usage: tp <x y z> or tp <player>");
                return null;
            }
            // Sitting free (your desk, any activity on open ground, flying with Desk Swarm), you go as you
            // are; standing, the game's own teleport rules (Teleport Spots').
            PlayerFocusController focus = Game.MyFocus;
            bool seated = focus != null && focus.IsFocus;
            string why = seated ? Game.WhyCantMoveSeated() : Game.WhyCantMove();
            if (why != null)
            {
                shell.Warn(why);
                return null;
            }

            Vector3 here = move.transform.position;
            if (args.Count == 3 && TryCoord(args[0], here.x, out float x) && TryCoord(args[1], here.y, out float y) &&
                TryCoord(args[2], here.z, out float z))
            {
                var spot = new Vector3(x, y, z);
                if (Mathf.Abs(x) > 5000f || Mathf.Abs(y) > 5000f || Mathf.Abs(z) > 5000f)
                {
                    shell.Warn("That's very far off the map.");
                    return null;
                }
                Move(seated, spot, move.transform.eulerAngles.y);
                shell.Print($"Whoosh! Now at x {F(x)}  y {F(y)}  z {F(z)}.");
                return null;
            }

            PlayerEntry? found = Game.FindPlayer(args.Rest(), out string notFound);
            if (found == null)
            {
                shell.Warn(args.Count == 3 && notFound != null && notFound.StartsWith("Nobody here")
                    ? "Usage: tp <x y z> (numbers, or ~ for where you are) or tp <player>"
                    : notFound);
                return null;
            }
            PlayerEntry player = found.Value;
            Transform them = player.Player.transform;
            // Beside them, on their right, facing the way they face. Sitting, you bring your desk, so a
            // little further over, and at their height (they may be flying); standing, on the ground.
            Vector3 target = them.position + them.right * (seated ? 1.6f : 1.0f);
            if (!seated && Game.Ground(target + Vector3.up * 1.5f, 50f, out float ground))
            {
                target.y = ground;
            }
            Move(seated, target, them.eulerAngles.y);
            shell.PrintRich(Shell.Colored(Shell.TextColor, "Whoosh! Now beside ") + player.Name + Shell.Colored(Shell.TextColor, "."));
            return null;
        }

        private static void Move(bool seated, Vector3 position, float yaw)
        {
            if (seated)
            {
                SeatedMoves.BeforeMove(Game.MyFocus);
                Game.MoveSeated(position, yaw);
            }
            else
            {
                Game.Teleport(position, yaw);
            }
        }

        // A number, or ~ (where you are now) with an optional number added: ~, ~2, ~-1.5.
        private static bool TryCoord(string text, float now, out float value)
        {
            if (text.StartsWith("~", StringComparison.Ordinal))
            {
                string rest = text.Substring(1);
                if (rest.Length == 0)
                {
                    value = now;
                    return true;
                }
                if (float.TryParse(rest, NumberStyles.Float, CultureInfo.InvariantCulture, out float add))
                {
                    value = now + add;
                    return true;
                }
                value = 0f;
                return false;
            }
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static IEnumerator Emote(Shell shell, Args args)
        {
            if (Game.MyMovement == null)
            {
                shell.Warn("Join a world first.");
                return null;
            }
            int count = Game.EmoteCount;
            if (args.Count == 0 || args.Is(0, "list"))
            {
                shell.Print($"There are {count} emotes: emote 1 to emote {count}.");
                shell.Hint("They go around the emote wheel from the top, like its buttons. Try: bind 1 \"emote 1\"");
                return null;
            }
            if (!int.TryParse(args[0], out int number) || number < 1 || number > count)
            {
                shell.Warn($"Pick a number from 1 to {count}.");
                return null;
            }
            string why = Game.Emote(number - 1);
            if (why != null)
            {
                shell.Warn(why);
            }
            return null;
        }

        private static IEnumerator Sit(Shell shell, Args args)
        {
            if (Game.MyMovement == null)
            {
                shell.Warn("Join a world first.");
                return null;
            }
            List<Activity> activities = Sitting.Activities();
            if (args.Count == 0 || args.Is(0, "list"))
            {
                if (args.Count >= 2)
                {
                    if (!TryKind(args[1], out FocusType listKind))
                    {
                        shell.Warn($"There's no {args[1]} kind. sit list shows them.");
                        return null;
                    }
                    List<Activity> ofKind = activities.Where(a => a.Type == listKind).ToList();
                    if (ofKind.Count == 0)
                    {
                        shell.Warn($"No {Name(listKind)} activities here.");
                    }
                    foreach (Activity activity in ofKind)
                    {
                        shell.Print($"sit {Name(listKind)} {activity.Number}  ({activity.Details})");
                    }
                    return null;
                }
                foreach (IGrouping<FocusType, Activity> kind in activities.GroupBy(a => a.Type))
                {
                    shell.Print($"{Name(kind.Key),-10} {kind.Count()} to pick from");
                }
                shell.Hint("sit read sits you down reading; sit read 2 picks the second. sit list read shows them.");
                return null;
            }
            if (!TryKind(args[0], out FocusType type))
            {
                shell.Warn($"There's no {args[0]} kind. sit list shows them.");
                return null;
            }
            List<Activity> choices = activities.Where(a => a.Type == type).ToList();
            if (choices.Count == 0)
            {
                shell.Warn($"No {Name(type)} activities here.");
                return null;
            }
            int pick = 1;
            if (args.Count > 1 && (!int.TryParse(args[1], out pick) || pick < 1 || pick > choices.Count))
            {
                shell.Warn($"Pick a number from 1 to {choices.Count}.");
                return null;
            }
            Activity chosen = choices[pick - 1];
            string why = Sitting.Sit(chosen);
            if (why != null)
            {
                shell.Warn(why);
            }
            else
            {
                shell.Print($"Sitting: {Name(type)} {pick} ({chosen.Details}).");
            }
            return null;
        }

        private static bool TryKind(string text, out FocusType type)
        {
            foreach (FocusType kind in Enum.GetValues(typeof(FocusType)))
            {
                if (kind.ToString().StartsWith(text, StringComparison.OrdinalIgnoreCase))
                {
                    type = kind;
                    return true;
                }
            }
            type = default;
            return false;
        }

        private static string Name(FocusType type) => type.ToString().ToLowerInvariant();

        private static IEnumerator Stand(Shell shell, Args args)
        {
            PlayerFocusController focus = Game.MyFocus;
            if (focus == null)
            {
                shell.Warn("Join a world first.");
                return null;
            }
            if (!focus.IsFocus)
            {
                shell.Hint("You're already standing.");
                return null;
            }
            if (Game.DeskSwarmPiloting())
            {
                shell.Warn("You're flying with Desk Swarm. Land first.");
                return null;
            }
            Game.StandUp();
            return null;
        }

        private static IEnumerator Say(Shell shell, Args args)
        {
            TextChannelManager channel = Game.Channel;
            UIManager ui = Game.InWorld ? MonoSingleton<UIManager>.I : null;
            if (channel == null || ui == null || ui.MessageInput == null)
            {
                shell.Warn("Join a world first.");
                return null;
            }
            string message = args.Rest().Trim();
            if (message.Length == 0)
            {
                shell.Warn("Say what? Usage: say <message>");
                return null;
            }
            // Sending sets the game's menu state back to "nothing open", so only when nothing is.
            if (Game.MenuOpen())
            {
                shell.Warn("Close the open menu first.");
                return null;
            }
            // The game's own send: it reads the chat box and empties it, keeps its limits, and sends
            // to the open tab. Whatever half-typed message was in the box comes back after.
            string draft = ui.MessageInput.text;
            ui.MessageInput.text = message;
            channel.OnEnterPressed();
            if (!string.IsNullOrEmpty(draft) && draft != message)
            {
                ui.MessageInput.text = draft;
            }
            shell.Hint(channel.Islocal ? "(said in Local chat)" : "(said in Global chat)");
            return null;
        }

        private static string F(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
