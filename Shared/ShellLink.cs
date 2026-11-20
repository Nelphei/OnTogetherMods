using System;
using System.Collections;
using UnityEngine;

namespace OnTogetherMods.Shared
{
    // Lets a mod add its own commands to Bubble Shell's console. If Bubble Shell isn't installed they
    // just sit unused. Like IconBar and PanelStack, every mod compiles its own copy of this file, so what
    // they share lives in AppDomain data, using built-in types only.
    //
    // Call Add() once, from Plugin.Awake. Bubble Shell picks the commands up whenever the console is
    // used, whichever mod loaded first. A command runs on the game's main thread (from Bubble Shell's
    // Update), gets the words typed after its name, and returns what to print: lines separated by \n,
    // shown as plain text (tags show as typed). A line starting with "! " is shown as a warning,
    // one starting with "~ " as a dim hint. Return null or "" to print nothing.
    internal static class ShellLink
    {
        // Name -> object[] { string mod, string usage, string help, Func<string[], string> run,
        // Func<string[], int, string[]> complete (or null) }.
        public const string CommandsKey = "OnTogetherMods.BubbleShell.Commands";

        public const int ModIndex = 0;
        public const int UsageIndex = 1;
        public const int HelpIndex = 2;
        public const int RunIndex = 3;
        public const int CompleteIndex = 4;
        public const int Size = 5;

        // mod: the mod's name, shown as its group in help. complete (optional): given the words typed so
        // far after the name and the number of the word being typed (0 = the first), what it could be.
        public static void Add(string name, string mod, string usage, string help, Func<string[], string> run,
            Func<string[], int, string[]> complete = null)
        {
            Table()[name.ToLowerInvariant()] = new object[] { mod, usage, help, run, complete };
        }

        // A command that starts something steered with the keys (flying your desk, Photo Mode's camera)
        // calls this, so the console hands the keyboard back to the game (it stays open; clicking it
        // takes the keyboard again). It counts up, and Bubble Shell notices the change.
        public const string HandBackKey = "OnTogetherMods.BubbleShell.HandBack";

        public static void HandBackKeys()
        {
            int count = AppDomain.CurrentDomain.GetData(HandBackKey) is int n ? n : 0;
            AppDomain.CurrentDomain.SetData(HandBackKey, count + 1);
        }

        public static int HandBackCount => AppDomain.CurrentDomain.GetData(HandBackKey) is int n ? n : 0;

        // True while you type in Bubble Shell's console. The game ignores the keys then, but a mod that
        // reads keys straight from Unity's Input (Photo Mode's free camera) should skip them too.
        // Bubble Shell marks the frame every frame it has the keyboard; this frame or the last counts,
        // whichever mod's Update runs first.
        public const string TypingKey = "OnTogetherMods.BubbleShell.TypingFrame";

        public static bool ConsoleTyping =>
            AppDomain.CurrentDomain.GetData(TypingKey) is int frame && Time.frameCount - frame <= 1;

        public static void MarkConsoleTyping()
        {
            AppDomain.CurrentDomain.SetData(TypingKey, Time.frameCount);
        }

        public static Hashtable Table()
        {
            if (!(AppDomain.CurrentDomain.GetData(CommandsKey) is Hashtable table))
            {
                table = new Hashtable();
                AppDomain.CurrentDomain.SetData(CommandsKey, table);
            }
            return table;
        }
    }
}
