using System;

namespace KittyTarot
{
    // One spot in a spread: its name ("Past") and where its card lies, in art pixels from the spread's
    // top-left corner.
    internal sealed class Position
    {
        public readonly string Name;
        public readonly int X;
        public readonly int Y;

        public Position(string name, int x, int y)
        {
            Name = name;
            X = x;
            Y = y;
        }
    }

    // A way of laying out cards, each spot with a name.
    internal sealed class Spread
    {
        public readonly string Id;
        public readonly string Title;
        public readonly Position[] Positions;
        public readonly int Width;
        public readonly int Height;

        public Spread(string id, string title, params Position[] positions)
        {
            Id = id;
            Title = title;
            Positions = positions;
            foreach (Position p in positions)
            {
                Width = Math.Max(Width, p.X + CardFaces.Width);
                Height = Math.Max(Height, p.Y + CardFaces.Height);
            }
        }

        public int Count => Positions.Length;
    }

    internal static class Spreads
    {
        // Between cards side by side.
        public const int Gap = 14;

        private const int Step = CardFaces.Width + Gap;

        public static readonly Spread Three = new Spread("three", "Past, present, future",
            new Position("Past", 0, 0),
            new Position("Present", Step, 0),
            new Position("Future", 2 * Step, 0));

        public static readonly Spread[] All = { Three };

        public static Spread Find(string id)
        {
            foreach (Spread spread in All)
            {
                if (spread.Id == id)
                {
                    return spread;
                }
            }
            return null;
        }
    }
}
