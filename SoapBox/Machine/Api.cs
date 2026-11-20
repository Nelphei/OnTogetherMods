using System;
using System.Text;
using MoonSharp.Interpreter;

namespace SoapBox
{
    // The functions a cart can call (besides Lua's own string, table, math and coroutine libraries).
    // Positions are floored to whole pixels and colours wrap to 0-15. Missing arguments get sensible
    // defaults, and out-of-range values just clip, so a slip shows on screen instead of stopping the cart.
    //
    // Drawing:  cls([c])  color([c])  pset(x,y,[c])  pget(x,y)  line(x0,y0,x1,y1,[c])
    //           rect(x,y,w,h,[c])  rectf(x,y,w,h,[c])  rrect(x,y,w,h,r,[c])  rrectf(x,y,w,h,r,[c])
    //           circ(x,y,r,[c])  circf(x,y,r,[c])
    //           print(text,[x,y],[c],[scale])  spr(n,x,y,[w,h],[flipx],[flipy])
    //           sspr(sx,sy,sw,sh,dx,dy,[dw,dh],[flipx],[flipy])  map([cx,cy],[sx,sy],[cw,ch],[flags])
    //           camera([x,y])  clip([x,y,w,h])  pal([c0,c1])  palt([c,see-through])
    // Sprites:  sget(x,y)  sset(x,y,[c])  fget(n,[f])  fset(n,[f],v)  mget(cx,cy)  mset(cx,cy,n)
    // Input:    btn([b])  btnp([b])  mouse() -> x,y,left,right,wheel  mousep([b])
    //           buttons: 0 "left", 1 "right", 2 "up", 3 "down", 4 "a", 5 "b"
    // Sound:    sfx(n,[channel])  music([n])          (silent until the sound step)
    // Maths:    rnd([x or table])  srand(n)  flr  ceil  abs  sgn  min  max  mid  sqrt  sin  cos  atan2(y,x)
    // Other:    time()  log(...)
    internal sealed partial class Runner
    {
        private const int Tile = 8;
        private const int RepeatDelay = 15;   // updates a button is held before btnp repeats
        private const int RepeatEvery = 4;

        private int _pen = Palette.White;
        private int _cursorX;
        private int _cursorY;
        private Random _random = new Random();

        // The buttons as of the update running now.
        private int _held;
        private int _pressed;
        private int _mouseHeld;
        private int _mousePressed;
        private float _wheel;
        private readonly int[] _heldFor = new int[Controls.Count];

        // Takes what the player pressed since the last update.
        private void BeginUpdate()
        {
            int down = Controls.Pressed;
            Controls.Pressed = 0;
            // A tap already let go of still counts as held for this one update.
            _held = Controls.Held | down;
            _pressed = 0;
            for (int b = 0; b < Controls.Count; b++)
            {
                int bit = 1 << b;
                if ((down & bit) != 0)
                {
                    _pressed |= bit;
                    _heldFor[b] = 1;
                }
                else if ((_held & bit) != 0)
                {
                    _heldFor[b]++;
                    if (_heldFor[b] > RepeatDelay && (_heldFor[b] - RepeatDelay - 1) % RepeatEvery == 0)
                    {
                        _pressed |= bit;
                    }
                }
                else
                {
                    _heldFor[b] = 0;
                }
            }
            _mousePressed = Controls.MousePressed;
            Controls.MousePressed = 0;
            _mouseHeld = Controls.MouseHeld | _mousePressed;
            _wheel = Controls.Wheel;
            Controls.Wheel = 0f;
        }

        private void Register()
        {
            Table g = _script.Globals;

            // ---- Drawing ----
            Set(g, "cls", a =>
            {
                Screen.Clear(Has(a, 0) ? Int(a, 0) & 15 : 0);
                _cursorX = 0;
                _cursorY = 0;
                return DynValue.Nil;
            });
            Set(g, "color", a =>
            {
                int old = _pen;
                if (Has(a, 0))
                {
                    _pen = Int(a, 0) & 15;
                }
                return DynValue.NewNumber(old);
            });
            Set(g, "pset", a =>
            {
                Screen.Pset(Int(a, 0), Int(a, 1), Colour(a, 2));
                return DynValue.Nil;
            });
            Set(g, "pget", a => DynValue.NewNumber(Screen.Pget(Int(a, 0), Int(a, 1)) & 15));
            Set(g, "line", a =>
            {
                Screen.Line(Int(a, 0), Int(a, 1), Int(a, 2), Int(a, 3), Colour(a, 4));
                return DynValue.Nil;
            });
            Set(g, "rect", a =>
            {
                Screen.Rect(Int(a, 0), Int(a, 1), Int(a, 2), Int(a, 3), Colour(a, 4));
                return DynValue.Nil;
            });
            Set(g, "rectf", a =>
            {
                Screen.RectFill(Int(a, 0), Int(a, 1), Int(a, 2), Int(a, 3), Colour(a, 4));
                return DynValue.Nil;
            });
            Set(g, "rrect", a =>
            {
                Screen.RoundRect(Int(a, 0), Int(a, 1), Int(a, 2), Int(a, 3), Int(a, 4), Colour(a, 5));
                return DynValue.Nil;
            });
            Set(g, "rrectf", a =>
            {
                Screen.RoundRectFill(Int(a, 0), Int(a, 1), Int(a, 2), Int(a, 3), Int(a, 4), Colour(a, 5));
                return DynValue.Nil;
            });
            Set(g, "circ", a =>
            {
                Screen.Circ(Int(a, 0), Int(a, 1), Int(a, 2), Colour(a, 3));
                return DynValue.Nil;
            });
            Set(g, "circf", a =>
            {
                Screen.CircFill(Int(a, 0), Int(a, 1), Int(a, 2), Colour(a, 3));
                return DynValue.Nil;
            });
            Set(g, "print", a =>
            {
                string text = a.Count > 0 ? a[0].ToPrintString() : "";
                int scale = Math.Max(1, Math.Min(8, Has(a, 4) ? Int(a, 4) : 1));
                int lines = 1;
                foreach (char c in text)
                {
                    if (c == '\n')
                    {
                        lines++;
                    }
                }
                if (Has(a, 1) || Has(a, 2))
                {
                    return DynValue.NewNumber(Screen.Print(text, Int(a, 1), Int(a, 2), Colour(a, 3), scale));
                }
                // No position: under the last text printed this way, like a console.
                int end = Screen.Print(text, _cursorX, _cursorY, Colour(a, 3), scale);
                _cursorY += lines * PixelFont.LineHeight * scale;
                return DynValue.NewNumber(end);
            });
            Set(g, "spr", a =>
            {
                int n = Int(a, 0) & 255;
                int w = Math.Max(1, Has(a, 3) ? Int(a, 3) : 1);
                int h = Math.Max(1, Has(a, 4) ? Int(a, 4) : 1);
                Screen.Blit(Cart.Sprites, n % 16 * Tile, n / 16 * Tile, Math.Min(w * Tile, Cart.SheetSize), Math.Min(h * Tile, Cart.SheetSize),
                    Int(a, 1), Int(a, 2), Bool(a, 5), Bool(a, 6));
                return DynValue.Nil;
            });
            Set(g, "sspr", a =>
            {
                int sw = Int(a, 2), sh = Int(a, 3);
                Screen.BlitScaled(Cart.Sprites, Int(a, 0), Int(a, 1), sw, sh, Int(a, 4), Int(a, 5),
                    Has(a, 6) ? Int(a, 6) : sw, Has(a, 7) ? Int(a, 7) : sh, Bool(a, 8), Bool(a, 9));
                return DynValue.Nil;
            });
            Set(g, "map", a =>
            {
                int cx = Int(a, 0), cy = Int(a, 1), sx = Int(a, 2), sy = Int(a, 3);
                int cw = Has(a, 4) ? Int(a, 4) : Width / Tile;
                int ch = Has(a, 5) ? Int(a, 5) : Height / Tile;
                int layers = Has(a, 6) ? Int(a, 6) & 255 : 0;
                for (int j = 0; j < ch; j++)
                {
                    int my = cy + j;
                    if (my < 0 || my >= Cart.MapHeight)
                    {
                        continue;
                    }
                    for (int i = 0; i < cw; i++)
                    {
                        int mx = cx + i;
                        if (mx < 0 || mx >= Cart.MapWidth)
                        {
                            continue;
                        }
                        int n = Cart.Map[my * Cart.MapWidth + mx];
                        if (n == 0 || (layers != 0 && (Cart.Flags[n] & layers) == 0))
                        {
                            continue;
                        }
                        Screen.Blit(Cart.Sprites, n % 16 * Tile, n / 16 * Tile, Tile, Tile, sx + i * Tile, sy + j * Tile);
                    }
                }
                return DynValue.Nil;
            });
            Set(g, "camera", a =>
            {
                Screen.CameraX = Int(a, 0);
                Screen.CameraY = Int(a, 1);
                return DynValue.Nil;
            });
            Set(g, "clip", a =>
            {
                if (Has(a, 0))
                {
                    Screen.Clip(Int(a, 0), Int(a, 1), Has(a, 2) ? Int(a, 2) : Width, Has(a, 3) ? Int(a, 3) : Height);
                }
                else
                {
                    Screen.ClipOff();
                }
                return DynValue.Nil;
            });
            Set(g, "pal", a =>
            {
                if (Has(a, 0))
                {
                    Screen.DrawColours[Int(a, 0) & 15] = (byte)(Has(a, 1) ? Int(a, 1) & 15 : Int(a, 0) & 15);
                }
                else
                {
                    for (int i = 0; i < 16; i++)
                    {
                        Screen.DrawColours[i] = (byte)i;
                    }
                }
                return DynValue.Nil;
            });
            Set(g, "palt", a =>
            {
                if (Has(a, 0))
                {
                    Screen.SeeThrough[Int(a, 0) & 15] = !Has(a, 1) || a[1].CastToBool();
                }
                else
                {
                    for (int i = 0; i < 16; i++)
                    {
                        Screen.SeeThrough[i] = i == 0;
                    }
                }
                return DynValue.Nil;
            });

            // ---- The sprite sheet, flags and map ----
            Set(g, "sget", a =>
            {
                int x = Int(a, 0), y = Int(a, 1);
                return DynValue.NewNumber(InSheet(x, y) ? Cart.Sprites.Pixels[y * Cart.SheetSize + x] & 15 : 0);
            });
            Set(g, "sset", a =>
            {
                int x = Int(a, 0), y = Int(a, 1);
                if (InSheet(x, y))
                {
                    Cart.Sprites.Pixels[y * Cart.SheetSize + x] = (byte)Colour(a, 2);
                }
                return DynValue.Nil;
            });
            Set(g, "fget", a =>
            {
                int flags = Cart.Flags[Int(a, 0) & 255];
                return Has(a, 1) ? DynValue.NewBoolean((flags & (1 << (Int(a, 1) & 7))) != 0) : DynValue.NewNumber(flags);
            });
            Set(g, "fset", a =>
            {
                int n = Int(a, 0) & 255;
                if (a.Count >= 3)
                {
                    int bit = 1 << (Int(a, 1) & 7);
                    Cart.Flags[n] = (byte)(a[2].CastToBool() ? Cart.Flags[n] | bit : Cart.Flags[n] & ~bit);
                }
                else
                {
                    Cart.Flags[n] = (byte)(Int(a, 1) & 255);
                }
                return DynValue.Nil;
            });
            Set(g, "mget", a =>
            {
                int x = Int(a, 0), y = Int(a, 1);
                return DynValue.NewNumber(InMap(x, y) ? Cart.Map[y * Cart.MapWidth + x] : 0);
            });
            Set(g, "mset", a =>
            {
                int x = Int(a, 0), y = Int(a, 1);
                if (InMap(x, y))
                {
                    Cart.Map[y * Cart.MapWidth + x] = (byte)(Int(a, 2) & 255);
                }
                return DynValue.Nil;
            });

            // ---- Input ----
            Set(g, "btn", a =>
            {
                if (!Has(a, 0))
                {
                    return DynValue.NewNumber(_held);
                }
                int b = Button(a[0]);
                return DynValue.NewBoolean(b >= 0 && (_held & (1 << b)) != 0);
            });
            Set(g, "btnp", a =>
            {
                if (!Has(a, 0))
                {
                    return DynValue.NewNumber(_pressed);
                }
                int b = Button(a[0]);
                return DynValue.NewBoolean(b >= 0 && (_pressed & (1 << b)) != 0);
            });
            Set(g, "mouse", a => DynValue.NewTuple(
                DynValue.NewNumber(Controls.MouseX),
                DynValue.NewNumber(Controls.MouseY),
                DynValue.NewBoolean((_mouseHeld & 1) != 0),
                DynValue.NewBoolean((_mouseHeld & 2) != 0),
                DynValue.NewNumber(_wheel)));
            Set(g, "mousep", a =>
            {
                int b = Has(a, 0) ? Int(a, 0) : 0;
                return DynValue.NewBoolean(b >= 0 && b < 2 && (_mousePressed & (1 << b)) != 0);
            });

            // ---- Sound (comes in a later step) ----
            Set(g, "sfx", a => DynValue.Nil);
            Set(g, "music", a => DynValue.Nil);

            // ---- Maths ----
            Set(g, "rnd", a =>
            {
                if (a.Count > 0 && a[0].Type == DataType.Table)
                {
                    Table t = a[0].Table;
                    int length = t.Length;
                    return length > 0 ? t.Get(_random.Next(length) + 1) : DynValue.Nil;
                }
                return DynValue.NewNumber(_random.NextDouble() * Num(a, 0, 1.0));
            });
            Set(g, "srand", a =>
            {
                _random = new Random(Int(a, 0));
                return DynValue.Nil;
            });
            Set(g, "flr", a => DynValue.NewNumber(Math.Floor(Num(a, 0))));
            Set(g, "ceil", a => DynValue.NewNumber(Math.Ceiling(Num(a, 0))));
            Set(g, "abs", a => DynValue.NewNumber(Math.Abs(Num(a, 0))));
            Set(g, "sgn", a => DynValue.NewNumber(Num(a, 0) < 0 ? -1 : 1));
            Set(g, "min", a => DynValue.NewNumber(Math.Min(Num(a, 0), Num(a, 1))));
            Set(g, "max", a => DynValue.NewNumber(Math.Max(Num(a, 0), Num(a, 1))));
            Set(g, "mid", a =>
            {
                double x = Num(a, 0), y = Num(a, 1), z = Num(a, 2);
                return DynValue.NewNumber(Math.Max(Math.Min(x, y), Math.Min(Math.Max(x, y), z)));
            });
            Set(g, "sqrt", a => DynValue.NewNumber(Math.Sqrt(Math.Max(0.0, Num(a, 0)))));
            Set(g, "sin", a => DynValue.NewNumber(Math.Sin(Num(a, 0))));
            Set(g, "cos", a => DynValue.NewNumber(Math.Cos(Num(a, 0))));
            Set(g, "atan2", a => DynValue.NewNumber(Math.Atan2(Num(a, 0), Num(a, 1))));

            // ---- Other ----
            Set(g, "time", a => DynValue.NewNumber(Time));
            Set(g, "log", a =>
            {
                var sb = new StringBuilder();
                for (int i = 0; i < a.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(' ');
                    }
                    sb.Append(a[i].ToPrintString());
                }
                _log?.Invoke("cart: " + sb);
                return DynValue.Nil;
            });
            // Lua's own forces a full collection, which would make the whole game stutter.
            Set(g, "collectgarbage", a =>
                DynValue.NewNumber(Has(a, 0) && a[0].CastToString() == "count" ? GC.GetTotalMemory(false) / 1024.0 : 0.0));
        }

        private static void Set(Table g, string name, Func<CallbackArguments, DynValue> f)
        {
            g[name] = DynValue.NewCallback((context, args) => f(args), name);
        }

        private static bool Has(CallbackArguments a, int i)
        {
            return i < a.Count && !a[i].IsNil();
        }

        private static double Num(CallbackArguments a, int i, double fallback = 0.0)
        {
            if (i >= a.Count)
            {
                return fallback;
            }
            DynValue v = a[i];
            if (v.Type == DataType.Number)
            {
                return v.Number;
            }
            double? d = v.CastToNumber();
            return d ?? fallback;
        }

        private static int Int(CallbackArguments a, int i, int fallback = 0)
        {
            double d = Num(a, i, fallback);
            if (double.IsNaN(d))
            {
                return 0;
            }
            return (int)Math.Floor(Math.Max(-1e9, Math.Min(1e9, d)));
        }

        private int Colour(CallbackArguments a, int i)
        {
            return Has(a, i) ? Int(a, i) & 15 : _pen;
        }

        private static bool Bool(CallbackArguments a, int i)
        {
            return i < a.Count && a[i].CastToBool();
        }

        // A button by number (0-5) or name ("left"... "b"), or -1.
        private static int Button(DynValue v)
        {
            if (v.Type == DataType.Number)
            {
                int b = (int)Math.Floor(v.Number);
                return b >= 0 && b < Controls.Count ? b : -1;
            }
            if (v.Type == DataType.String)
            {
                return Array.IndexOf(Controls.Names, v.String.Trim().ToLowerInvariant());
            }
            return -1;
        }

        private static bool InSheet(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Cart.SheetSize && y < Cart.SheetSize;
        }

        private static bool InMap(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Cart.MapWidth && y < Cart.MapHeight;
        }
    }
}
