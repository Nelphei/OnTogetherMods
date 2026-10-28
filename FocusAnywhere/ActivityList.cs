using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace FocusAnywhere
{
    // One focus activity: a category, an icon, an animation and its prop, plus the camera framing.
    internal sealed class Activity
    {
        public FocusType Type;
        public Sprite Sprite;
        public int Anim;
        public PropType Prop;
        public float CameraOffset;
        public float CameraDesktopOffset;
        public float CameraOrtographic;
        public bool IsDesk;                 // the personal desk appears (land only)
        public WaterPropType WaterProp;     // the floater or water bed appears
        public bool SummerDlc;              // needs the Summer DLC
    }

    // The free focus menu reads parallel lists in GameSettings: one set for land, one for water.
    // Seat spots (benches, the play tower, bean bags...) have their own FocusAreaSettings lists.
    // This merges all of them, without repeats, and puts the result back into GameSettings, so the
    // game's own menu offers every activity. The game reads these lists only in PlayerFocusController.
    // See reference/Assembly-CSharp/GameSettings.cs and PlayerFocusController.cs (ButtonFocusItem, SetFocus).
    internal static class ActivityList
    {
        private static readonly AccessTools.FieldRef<GameSettings, List<Sprite>> LandSprites = F<List<Sprite>>("_focusSprites");
        private static readonly AccessTools.FieldRef<GameSettings, List<FocusType>> LandTypes = F<List<FocusType>>("_focusTypes");
        private static readonly AccessTools.FieldRef<GameSettings, List<int>> LandAnims = F<List<int>>("_animationStates");
        private static readonly AccessTools.FieldRef<GameSettings, List<PropType>> LandProps = F<List<PropType>>("_propTypes");
        private static readonly AccessTools.FieldRef<GameSettings, List<float>> LandCamera = F<List<float>>("_cameraOffsets");
        private static readonly AccessTools.FieldRef<GameSettings, List<float>> LandCameraDesktop = F<List<float>>("_cameraDesktopOffsets");
        private static readonly AccessTools.FieldRef<GameSettings, List<float>> LandCameraOrtho = F<List<float>>("_cameraOrtographics");
        private static readonly AccessTools.FieldRef<GameSettings, List<bool>> LandIsDesk = F<List<bool>>("_isDesk");

        private static readonly AccessTools.FieldRef<GameSettings, List<Sprite>> WaterSprites = F<List<Sprite>>("_focusSpritesWater");
        private static readonly AccessTools.FieldRef<GameSettings, List<FocusType>> WaterTypes = F<List<FocusType>>("_focusTypesWater");
        private static readonly AccessTools.FieldRef<GameSettings, List<int>> WaterAnims = F<List<int>>("_animationStatesWater");
        private static readonly AccessTools.FieldRef<GameSettings, List<PropType>> WaterPropsList = F<List<PropType>>("_propTypesWater");
        private static readonly AccessTools.FieldRef<GameSettings, List<float>> WaterCamera = F<List<float>>("_cameraOffsetsWater");
        private static readonly AccessTools.FieldRef<GameSettings, List<float>> WaterCameraDesktop = F<List<float>>("_cameraDesktopOffsetsWater");
        private static readonly AccessTools.FieldRef<GameSettings, List<float>> WaterCameraOrtho = F<List<float>>("_cameraOrtographicsWater");
        private static readonly AccessTools.FieldRef<GameSettings, List<WaterPropType>> WaterFloaters = F<List<WaterPropType>>("_waterDlcPropTypes");
        private static readonly AccessTools.FieldRef<GameSettings, List<bool>> WaterSummerDlc = F<List<bool>>("_isSummerDLC");

        // The game's own lists, read once before the first merge.
        private static List<Activity> _landOriginal;
        private static List<Activity> _waterOriginal;

        private static int _seatListCount = -1;
        private static bool _ownsSummer;
        private static bool _broken;

        public static bool Installed { get; private set; }

        // The floater or bed for each entry of the merged land list. The game only shows them in
        // water, so SetFocusPatch adds them when a floater activity is picked on land.
        public static List<WaterPropType> LandWaterProps { get; private set; } = new List<WaterPropType>();

        // Merges the lists, or merges them again if more seat lists have loaded since last time.
        // Cheap when nothing changed. Call only in a lobby.
        public static void Refresh()
        {
            if (!Plugin.Settings.AllActivities.Value || _broken)
            {
                return;
            }
            try
            {
                Install();
            }
            catch (Exception e)
            {
                // Don't retry every frame. The menu stays as the game made it (or as last merged).
                _broken = true;
                Plugin.Log.LogError($"Couldn't merge the focus activity lists: {e}");
            }
        }

        private static void Install()
        {
            GameSettings settings = ScriptableSingleton<GameSettings>.I;
            DataManager data = MonoSingleton<DataManager>.I;
            if (settings == null || data == null)
            {
                return;
            }

            List<FocusAreaSettings> seatLists = FindSeatLists();
            bool ownsSummer = data.IsHaveSummerDLC;
            if (Installed && seatLists.Count == _seatListCount && ownsSummer == _ownsSummer)
            {
                return;
            }

            if (_landOriginal == null)
            {
                _landOriginal = ReadLand(settings);
                _waterOriginal = ReadWater(settings);
            }
            List<Activity> seats = ReadSeats(seatLists);

            // Each list starts with the activities the game already offers there, in the game's
            // order, then adds the rest. Summer DLC activities stay hidden unless you own it.
            List<Activity> land = Merge(ownsSummer, _landOriginal, seats, _waterOriginal);
            List<Activity> water = Merge(ownsSummer, _waterOriginal, _landOriginal, seats);
            WriteLand(settings, land);
            WriteWater(settings, water);
            LandWaterProps = land.Select(a => a.WaterProp).ToList();

            _seatListCount = seatLists.Count;
            _ownsSummer = ownsSummer;
            Installed = true;

            Plugin.Log.LogInfo($"Focus menu: {land.Count} activities on land (was {_landOriginal.Count}), " +
                $"{water.Count} in water (was {_waterOriginal.Count}), from {seatLists.Count} seat lists.");
            if (Plugin.Settings.DebugLog.Value)
            {
                LogCategories("land", land);
                LogCategories("water", water);
            }
        }

        // How many buttons the biggest category needs.
        public static int MostInOneCategory(List<FocusType> types)
        {
            if (types == null || types.Count == 0)
            {
                return 0;
            }
            return types.GroupBy(t => t).Max(g => g.Count());
        }

        private static List<FocusAreaSettings> FindSeatLists()
        {
            // Every seat list the loaded scenes use, in a fixed order so the menu doesn't shuffle.
            return Resources.FindObjectsOfTypeAll<FocusAreaSettings>()
                .Where(s => s != null && s.FocusTypes != null)
                .OrderBy(s => s.name, StringComparer.Ordinal)
                .ThenBy(s => s.GetInstanceID())
                .ToList();
        }

        private static List<Activity> ReadLand(GameSettings s)
        {
            List<FocusType> types = LandTypes(s) ?? new List<FocusType>();
            var list = new List<Activity>();
            for (int i = 0; i < types.Count; i++)
            {
                list.Add(new Activity
                {
                    Type = types[i],
                    Sprite = At(LandSprites(s), i, null),
                    Anim = At(LandAnims(s), i, 0),
                    Prop = At(LandProps(s), i, PropType.None),
                    CameraOffset = At(LandCamera(s), i, 0.6f),
                    CameraDesktopOffset = At(LandCameraDesktop(s), i, 0.91f),
                    CameraOrtographic = At(LandCameraOrtho(s), i, 1.5f),
                    IsDesk = At(LandIsDesk(s), i, false),
                });
            }
            return list;
        }

        private static List<Activity> ReadWater(GameSettings s)
        {
            List<FocusType> types = WaterTypes(s) ?? new List<FocusType>();
            var list = new List<Activity>();
            for (int i = 0; i < types.Count; i++)
            {
                list.Add(new Activity
                {
                    Type = types[i],
                    Sprite = At(WaterSprites(s), i, null),
                    Anim = At(WaterAnims(s), i, 0),
                    Prop = At(WaterPropsList(s), i, PropType.None),
                    CameraOffset = At(WaterCamera(s), i, 0.6f),
                    CameraDesktopOffset = At(WaterCameraDesktop(s), i, 0.91f),
                    CameraOrtographic = At(WaterCameraOrtho(s), i, 1.5f),
                    WaterProp = At(WaterFloaters(s), i, WaterPropType.None),
                    SummerDlc = At(WaterSummerDlc(s), i, false),
                });
            }
            return list;
        }

        private static List<Activity> ReadSeats(List<FocusAreaSettings> seatLists)
        {
            // Classroom seats bring out the personal desk. Look up which lists belong to one.
            var classroom = new HashSet<FocusAreaSettings>();
            foreach (FocusAreaController area in UnityEngine.Object.FindObjectsByType<FocusAreaController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (area != null && area.IsClassroom && area.FocusAreaSettings != null)
                {
                    classroom.Add(area.FocusAreaSettings);
                }
            }

            var list = new List<Activity>();
            foreach (FocusAreaSettings seat in seatLists)
            {
                for (int i = 0; i < seat.FocusTypes.Count; i++)
                {
                    list.Add(new Activity
                    {
                        Type = seat.FocusTypes[i],
                        Sprite = At(seat.FocusSprites, i, null),
                        Anim = At(seat.AnimationStates, i, 0),
                        Prop = At(seat.PropTypes, i, PropType.None),
                        CameraOffset = At(seat.CameraOffset, i, 0.6f),
                        CameraDesktopOffset = At(seat.CameraDesktopOffsets, i, 0.91f),
                        CameraOrtographic = At(seat.CameraOrtographics, i, 1.5f),
                        IsDesk = classroom.Contains(seat),
                    });
                }
            }
            return list;
        }

        private static List<Activity> Merge(bool ownsSummer, params List<Activity>[] sources)
        {
            // The same animation with the same prop counts once. The first list it appears in wins,
            // so it keeps the camera and desk it has there.
            var seen = new HashSet<(FocusType, int, PropType)>();
            var merged = new List<Activity>();
            foreach (List<Activity> source in sources)
            {
                foreach (Activity a in source)
                {
                    if (a.SummerDlc && !ownsSummer)
                    {
                        continue;
                    }
                    if (seen.Add((a.Type, a.Anim, a.Prop)))
                    {
                        merged.Add(a);
                    }
                }
            }
            return merged;
        }

        private static void WriteLand(GameSettings s, List<Activity> land)
        {
            LandSprites(s) = land.Select(a => a.Sprite).ToList();
            LandTypes(s) = land.Select(a => a.Type).ToList();
            LandAnims(s) = land.Select(a => a.Anim).ToList();
            LandProps(s) = land.Select(a => a.Prop).ToList();
            LandCamera(s) = land.Select(a => a.CameraOffset).ToList();
            LandCameraDesktop(s) = land.Select(a => a.CameraDesktopOffset).ToList();
            LandCameraOrtho(s) = land.Select(a => a.CameraOrtographic).ToList();
            LandIsDesk(s) = land.Select(a => a.IsDesk).ToList();
        }

        private static void WriteWater(GameSettings s, List<Activity> water)
        {
            WaterSprites(s) = water.Select(a => a.Sprite).ToList();
            WaterTypes(s) = water.Select(a => a.Type).ToList();
            WaterAnims(s) = water.Select(a => a.Anim).ToList();
            WaterPropsList(s) = water.Select(a => a.Prop).ToList();
            WaterCamera(s) = water.Select(a => a.CameraOffset).ToList();
            WaterCameraDesktop(s) = water.Select(a => a.CameraDesktopOffset).ToList();
            WaterCameraOrtho(s) = water.Select(a => a.CameraOrtographic).ToList();
            WaterFloaters(s) = water.Select(a => a.WaterProp).ToList();
            WaterSummerDlc(s) = water.Select(a => a.SummerDlc).ToList();
        }

        private static void LogCategories(string where, List<Activity> list)
        {
            foreach (IGrouping<FocusType, Activity> group in list.GroupBy(a => a.Type))
            {
                Plugin.Log.LogInfo($"  {where} {group.Key}: {group.Count()} (anims {string.Join(", ", group.Select(a => a.Anim))})");
            }
        }

        private static T At<T>(List<T> list, int i, T fallback)
        {
            return list != null && i < list.Count ? list[i] : fallback;
        }

        private static AccessTools.FieldRef<GameSettings, T> F<T>(string field)
        {
            return AccessTools.FieldRefAccess<GameSettings, T>(field);
        }
    }
}
