using System;
using System.Linq;

namespace KittyTarot
{
    // Where everything goes on the table for a spread, in art pixels from the table's top-left corner.
    // No Unity, so the harness can draw the same table the window does.
    //
    //   lace   title of the spread
    //   deck   [card] [card] [card] ...
    //          name   name   name
    internal sealed class TableLayout
    {
        public const int Inset = TableArt.LaceReach + 4;  // from the table's edge to the cards
        public const int TitleHeight = 12;                 // the spread's name, above the cards
        public const int PileDepth = 3;                    // the most a pile's edges show past its top card
        public const int DeckGap = 18;                     // between the deck and the spread
        public const int LabelGap = 5;                     // between a card and its name under it
        public const int LabelLine = 10;                   // between the lines of a name

        // How the fan of cards for picking arcs, and how far a card rises under the mouse or once picked.
        public const int FanArc = 8;
        public const int HoverLift = 6;
        public const int PickLift = 16;

        public readonly Spread Spread;
        public readonly int Width;
        public readonly int Height;
        public readonly int TitleY;
        public readonly (int x, int y) Deck;
        public readonly (int x, int y)[] Slots;
        public readonly (int x, int y)[] CutPiles;
        public readonly int LabelY;      // the first line of the names under the cards
        public readonly int SpreadX;     // the spread's left edge
        public readonly int Top;         // the top of the cards

        public TableLayout(Spread spread)
        {
            Spread = spread;
            TitleY = Inset - 1;
            Top = Inset + TitleHeight;
            Deck = (Inset, Top);
            SpreadX = Inset + CardFaces.Width + PileDepth + DeckGap;
            Slots = spread.Positions.Select(p => (SpreadX + p.X, Top + p.Y)).ToArray();
            int spreadWidth = Math.Max(spread.Width, 3 * CardFaces.Width + 2 * Spreads.Gap);
            CutPiles = Enumerable.Range(0, 3)
                .Select(i => (SpreadX + (spreadWidth - (3 * CardFaces.Width + 2 * Spreads.Gap)) / 2 + i * (CardFaces.Width + Spreads.Gap), Top))
                .ToArray();
            LabelY = Top + spread.Height + LabelGap;
            Width = SpreadX + spreadWidth + Inset;
            Height = LabelY + LabelLine + TarotFont.CapHeight + 2 + Inset - 1;
        }

        // The middle of the space the spread takes, for the wash's swirl.
        public (float x, float y) SpreadMiddle => (SpreadX + (Width - Inset - SpreadX) / 2f, Top + CardFaces.Height / 2f);

        // Where card i of n lies in the fan, before it rises.
        public (float x, float y) FanPos(int i, int n)
        {
            float left = Inset;
            float span = Width - 2 * Inset - CardFaces.Width;
            float u = n > 1 ? (float)i / (n - 1) : 0.5f;
            float arc = (2f * u - 1f) * (2f * u - 1f);
            return (left + u * span, Top + 4 + FanArc * arc);
        }

        // How many pile edges to show under a pile's top card.
        public static int Edges(int cards) => cards <= 1 ? 0 : Math.Min(PileDepth, (cards + 11) / 12);
    }
}
