namespace OutfitSlots
{
    // Putting on a slot's outfit from the quick-swap window or the icon.
    internal static class Swap
    {
        // Puts on the outfit in a slot (0-8). Returns what to tell the player.
        public static string Wear(int slot)
        {
            if (!Slots.Active)
            {
                return "Outfit Slots is off";
            }
            DataManager data = Game.Data;
            PlayerDataZip zip = data != null ? data.PlayerDataZip : null;
            if (zip == null || data.CustomizationData == null)
            {
                return "Your save hasn't loaded yet";
            }
            Slots.EnsureNames(zip);
            if (zip.SelectedStyleIndex == slot)
            {
                return "You're already wearing " + Slots.Label(zip, slot);
            }

            CustomizationUIController screen = Game.Customization;
            if (screen != null && Game.IsOpen(screen))
            {
                return "Pick it in the outfit screen's own list";
            }
            if (screen != null && screen.isActiveAndEnabled)
            {
                // Exactly what the game does when you open the outfit screen, pick a style and close
                // it: the outfit is saved, everyone sees you in it, and your ID card photo is retaken.
                screen.ButtonSelectStyle(slot);
                screen.CloseAvatarPanel(notFromStyleChanged: true);
            }
            else
            {
                WearDirectly(data, zip, screen, slot);
            }
            return zip.SelectedStyleIndex == slot ? "Wearing " + Slots.Label(zip, slot) : "Couldn't change outfit";
        }

        // Without a working outfit screen: change the save and the player directly.
        private static void WearDirectly(DataManager data, PlayerDataZip zip, CustomizationUIController screen, int slot)
        {
            zip.SelectedStyleIndex = slot;
            data.CustomizationData = new CustomizationData(zip.CurrentCustomizationDataIDs);
            if (screen != null)
            {
                // So closing the outfit screen later doesn't put the old outfit back.
                screen._customizationData = new CustomizationData(data.CustomizationData);
            }
            data.SavePlayerZipData();

            if (Game.InWorld)
            {
                // Only looked up on click: on the main menu this would search the scene.
                TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
                if (channel != null && channel.MainCustomizationController != null)
                {
                    channel.MainCustomizationController.ApplyCustomization(data.CustomizationData);
                }
            }
        }

        // The slot after the one you're on, going round, skipping slots you haven't used.
        public static int NextSlot()
        {
            PlayerDataZip zip = Game.Data != null ? Game.Data.PlayerDataZip : null;
            int current = zip != null ? zip.SelectedStyleIndex : 0;
            for (int step = 1; step <= Slots.Total; step++)
            {
                int slot = (current + step) % Slots.Total;
                if (!Slots.IsEmpty(slot))
                {
                    return slot;
                }
            }
            return current;
        }
    }
}
