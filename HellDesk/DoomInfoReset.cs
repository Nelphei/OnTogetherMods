using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ManagedDoom;

namespace HellDesk
{
    // Managed Doom was made to play one game per run: a WAD's DEHACKED patch (Freedoom has one)
    // changes DOOM's global tables (monsters, frames, weapons, ammo, cheats' armour, par times and
    // text) for good. Starting a second game in the same session, say DOOM.WAD after Freedoom, would
    // keep Freedoom's level names and story text. So the tables are copied before the first game
    // starts and put back before each later one. See Engine/Doom/DeHackEd.cs for what it changes.
    internal static class DoomInfoReset
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static List<Action> _restore;

        // Call before every new GameContent (which applies the WAD's patch).
        public static void BeforeNewGame()
        {
            if (_restore == null)
            {
                Snapshot();
            }
            else
            {
                foreach (Action restore in _restore)
                {
                    restore();
                }
            }
        }

        private static void Snapshot()
        {
            var restore = new List<Action>();
            // Runs DoomInfo's static setup, the same way DeHackEd makes sure of it.
            DoomInfo.Strings.PRESSKEY.GetHashCode();

            AddObjects(restore, DoomInfo.MobjInfos);
            AddObjects(restore, DoomInfo.States);
            AddObjects(restore, DoomInfo.WeaponInfos);
            AddInts(restore, DoomInfo.AmmoInfos.Max);
            AddInts(restore, DoomInfo.AmmoInfos.Clip);
            AddInts(restore, DoomInfo.ParTimes.Doom2);
            foreach (IList<int> episode in DoomInfo.ParTimes.Doom1)
            {
                AddInts(restore, episode);
            }
            AddStatics(restore, typeof(DoomInfo.DeHackEdConst));
            AddStrings(restore);
            _restore = restore;
        }

        // Every field of every object in the table, put back into the same objects.
        private static void AddObjects<T>(List<Action> restore, T[] table) where T : class
        {
            FieldInfo[] fields = AllFields(typeof(T));
            foreach (T item in table)
            {
                if (item == null)
                {
                    continue;
                }
                var values = new object[fields.Length];
                for (int i = 0; i < fields.Length; i++)
                {
                    values[i] = fields[i].GetValue(item);
                }
                restore.Add(() =>
                {
                    for (int i = 0; i < fields.Length; i++)
                    {
                        fields[i].SetValue(item, values[i]);
                    }
                });
            }
        }

        private static void AddInts(List<Action> restore, IList<int> list)
        {
            var copy = new int[list.Count];
            list.CopyTo(copy, 0);
            restore.Add(() =>
            {
                for (int i = 0; i < copy.Length; i++)
                {
                    list[i] = copy[i];
                }
            });
        }

        private static void AddStatics(List<Action> restore, Type type)
        {
            foreach (FieldInfo field in type.GetFields(Static))
            {
                if (field.IsInitOnly || field.IsLiteral)
                {
                    continue;
                }
                object value = field.GetValue(null);
                restore.Add(() => field.SetValue(null, value));
            }
        }

        // DoomString keeps every replaceable text in two private tables, each with its original
        // and its replaced value.
        private static void AddStrings(List<Action> restore)
        {
            FieldInfo original = typeof(DoomString).GetField("original", Instance);
            FieldInfo replaced = typeof(DoomString).GetField("replaced", Instance);
            if (original == null || replaced == null)
            {
                Plugin.Log.LogWarning("Couldn't find DOOM's text table, so switching games may keep the last game's text.");
                return;
            }
            var strings = new HashSet<DoomString>();
            foreach (string table in new[] { "valueTable", "nameTable" })
            {
                if (typeof(DoomString).GetField(table, Static)?.GetValue(null) is IDictionary dictionary)
                {
                    foreach (object value in dictionary.Values)
                    {
                        if (value is DoomString s)
                        {
                            strings.Add(s);
                        }
                    }
                }
            }
            restore.Add(() =>
            {
                foreach (DoomString s in strings)
                {
                    replaced.SetValue(s, original.GetValue(s));
                }
            });
        }

        private static FieldInfo[] AllFields(Type type)
        {
            var fields = new List<FieldInfo>();
            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (FieldInfo field in t.GetFields(Instance | BindingFlags.DeclaredOnly))
                {
                    if (!field.IsLiteral)
                    {
                        fields.Add(field);
                    }
                }
            }
            return fields.ToArray();
        }
    }
}
