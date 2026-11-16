namespace SessionManager
{
    // This mod's icon in the icon bar (Shared/IconBar.cs): 16 x 16 pixels, one palette letter each,
    // '.' see-through. A host's crown. Keep the crown's array first in this file: Thunderstore/pack.py
    // renders the first 16 rows after that array's name.
    internal static class Icon
    {
        public static readonly string[] Pixels =
        {
            "................",
            "................",
            "..K....KK....K..",
            ".KYK..KYYK..KYK.",
            ".KYK..KYYK..KYK.",
            ".KYYK.KYYK.KYYK.",
            ".KYYYKYYYYKYYYK.",
            ".KYYYYYYYYYYYYK.",
            ".KYYYYYYYYYYYYK.",
            ".KyyyyyyyyyyyyK.",
            ".KyyRyyUUyyRyyK.",
            ".KyyyyyyyyyyyyK.",
            ".KKKKKKKKKKKKKK.",
            "................",
            "................",
            "................",
        };

        // Two other designs Soapy liked, kept to try in the game (Icon in the settings file).

        // A crown over three friends.
        public static readonly string[] Friends =
        {
            "................",
            "...K...KK...K...",
            "..KYK.KYYK.KYK..",
            "..KYYKYYYYKYYK..",
            "..KYYYYYYYYYYK..",
            "..KyyRyyyyRyyK..",
            "..KKKKKKKKKKKK..",
            "................",
            ".KKKK.KKKK.KKKK.",
            ".KHHK.KddK.KyyK.",
            ".KPPK.KPPK.KPPK.",
            ".KKKK.KKKK.KKKK.",
            "KUUUUKSSSSKNNNNK",
            "KUUUUKSSSSKNNNNK",
            "KKKKKKKKKKKKKKKK",
            "................",
        };

        // A clipboard guest list: ticks for players let in, a cross for one turned away.
        public static readonly string[] Clipboard =
        {
            "................",
            ".....KKKKKK.....",
            "..KKKgGGGGgKKK..",
            "..KBKKKKKKKKBK..",
            "..KBWWWWWWWWBK..",
            "..KBWSWgggWNBK..",
            "..KBWWWWWWWWBK..",
            "..KBWUWgggWNBK..",
            "..KBWWWWWWWWBK..",
            "..KBWYWgggWRBK..",
            "..KBWWWWWWWWBK..",
            "..KBWVWgggWNBK..",
            "..KBWWWWWWWWBK..",
            "..KBBBBBBBBBBK..",
            "..KKKKKKKKKKKK..",
            "................",
        };

        // The picture picked in the settings.
        public static string[] For(IconStyle style)
        {
            switch (style)
            {
                case IconStyle.Friends:
                    return Friends;
                case IconStyle.Clipboard:
                    return Clipboard;
                default:
                    return Pixels;
            }
        }
    }
}
