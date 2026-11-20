using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EventType = UnityEngine.EventType;

namespace CardTable
{
    // The Go Fish tab of the Card Table window. Without a table: start one, or join someone else's.
    // At a table: the other players along the top (name, a fan of card backs with a count, their
    // books), the pond (the draw pile) and a log in the middle, your hand along the bottom. Click a
    // card to pick its rank, then a player to ask them for it.
    //
    // The table is the same size as Solitaire's and laid out the same way: in art pixels from its
    // top-left, drawn at a whole number of screen pixels each. The rest (the table list, the boxes)
    // is in 1080p units like the window around it.
    internal sealed class GoFishBoard
    {
        // ---- The table, in art pixels ----
        public const int PondX = 66;
        public const int PondY = 117;
        public const int PondRX = 54;
        public const int PondRY = 30;
        public const int BadgeSize = 11;
        private const int CardW = CardPixels.Width;
        private const int CardH = CardPixels.Height;
        private const int FeltLeft = 9;
        private const int FeltRight = 263;
        private const int SeatTop = 11;
        private const int NameHeight = 10;
        private const int FanTop = 22;
        private const int MaxFanStep = 4;
        private const int MaxFanCards = 16;
        private const int BooksTop = FanTop + CardH + 3;
        private const int SeatBottom = BooksTop + CardPixels.BookTileHeight + 2;
        private const int LogLeft = 128;
        private const int LogTop = 90;
        private const int LogRight = 261;
        private const int LogLines = 5;
        private const float LogLineHeight = 8.5f;
        private const int HandTop = 172;
        private const int Lift = 6;
        private const int HoverLift = 2;
        private const int HandLeft = 58;
        private const int HandRight = 261;
        private const int HandStep = 18;
        private const int YouLeft = 13;
        private const int YouTop = 172;
        private const int YouWidth = 44;
        private const int YouBooksTop = 184;
        private const float NameFont = 6.5f;
        private const float SmallFont = 6f;

        // ---- Timing, in seconds ----
        private const float SlideTime = 0.35f;
        private const float FlyTime = 0.45f;
        private const float BubbleTime = 1.6f;
        private const float EventGap = 0.5f;
        private const float AnswerDelay = 0.45f;
        private const float DealGap = 0.06f;
        private const float NoticeTime = 5f;
        private const float ConfirmTime = 3f;

        private static readonly Color Gold = new Color32(0xF5, 0xD0, 0x61, 0xFF);
        private static readonly Color Ink = new Color32(0x2A, 0x24, 0x33, 0xFF);
        private static readonly Color Cream = new Color32(0xF7, 0xF3, 0xEA, 0xFF);
        private static readonly Color FeltMark = new Color32(0x60, 0xAA, 0x6A, 0xFF);
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color TextShadow = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color LogBack = new Color(0f, 0f, 0f, 0.28f);
        private static readonly Color TipBack = new Color(0.09f, 0.09f, 0.1f, 0.94f);

        private struct Slide
        {
            public Vector2 From;
            public float Start;
        }

        // A card crossing the table between players, the pond and the books. Card -1 is a back.
        private struct Fly
        {
            public int Card;
            public Vector2 From;
            public Vector2 To;
            public float Start;
            public bool Early;   // shown where it starts before it sets off
        }

        private struct Bubble
        {
            public int Seat;
            public string Text;
            public float Start;
        }

        private static Settings S => Plugin.Settings;

        private readonly GoFishTables _tables;
        private readonly Store _store;
        private readonly Action _saveStats;
        private readonly List<Action> _clicks = new List<Action>();

        // The view the animations were worked out for.
        private GoFishView _view;
        private int _lastEventId;
        private int _picked = -1;
        private string _notice;
        private float _noticeUntil = -1f;
        private float _confirmLeaveUntil = -1f;
        private float _confirmNewUntil = -1f;
        private string _dismissed;   // the game whose results box was closed
        private string _counted;     // the game already in the stats
        private string _playing;     // a game seen before it ended (one joined at the end doesn't count)

        private readonly Dictionary<int, Slide> _slides = new Dictionary<int, Slide>();
        private readonly List<Fly> _flies = new List<Fly>();
        private readonly List<Bubble> _bubbles = new List<Bubble>();
        // Where each of your cards showed last, in art pixels, so cards can glide from there.
        private readonly Dictionary<int, Vector2> _handPos = new Dictionary<int, Vector2>();

        // ---- Set every OnGUI ----
        private Rect _tableRect;
        private int _scale = 1;
        private float _unit = 1f;
        private CardArt _art;

        private int _styleScale = -1;
        private float _styleUnit = -1f;
        private GUIStyle _nameStyle;
        private GUIStyle _youStyle;
        private GUIStyle _logStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _bubbleStyle;
        private GUIStyle _pondStyle;
        private GUIStyle _tipStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _textStyle;
        private GUIStyle _dimStyle;
        private GUIStyle _dimCentreStyle;
        private GUIStyle _headStyle;
        private GUIStyle _rowStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _pickedStyle;
        private GUIStyle _boxStyle;
        private GUIStyle _bigStyle;
        private GUIStyle _centreStyle;

        public GoFishBoard(GoFishTables tables, Store store, Action saveStats)
        {
            _tables = tables;
            _store = store;
            _saveStats = saveStats;
            _tables.Notice += (text, who) => Notice(text.Replace("{name}", who != 0 ? Players.Name(who) : "Someone"));
        }

        // True while it's your turn at a table (the icon shows a dot while the window is closed).
        public bool YourTurn => _tables.Current?.View?.MyTurn == true;

        // ---- Every frame ----

        public void Update(float now, bool showing)
        {
            List<Action> clicks = _clicks.ToList();
            _clicks.Clear();
            foreach (Action click in clicks)
            {
                click();
            }

            GoFishView view = _tables.Current?.View;
            if (view == null)
            {
                if (_view != null)
                {
                    ForgetTable();
                }
                if (showing && !_tables.Joining)
                {
                    _tables.Hello(now);
                }
            }
            else if (_view == null || view.Table != _view.Table || view.Seq != _view.Seq || view.GameNo != _view.GameNo ||
                     view.MySeat != _view.MySeat)
            {
                Absorb(view, now);
            }

            foreach (int card in _slides.Where(s => now >= s.Value.Start + SlideTime).Select(s => s.Key).ToList())
            {
                _slides.Remove(card);
            }
            _flies.RemoveAll(f => now >= f.Start + FlyTime);
            _bubbles.RemoveAll(b => now >= b.Start + BubbleTime);
            if (_picked >= 0 && (_view == null || !_view.MyTurn || !_view.Hand.Any(c => Cards.Rank(c) == _picked)))
            {
                _picked = -1;
            }
            CountGame();
        }

        public void Notice(string text)
        {
            _notice = text;
            _noticeUntil = Time.unscaledTime + NoticeTime;
        }

        // The title row's hint.
        public string Hint()
        {
            if (_tables.Hosting != null)
            {
                return $"your table  ·  {_tables.Hosting.SeatCount} players";
            }
            if (_tables.Joined != null)
            {
                string host = Players.Name(_tables.Joined.HostId);
                return _tables.Joining ? $"joining {host}'s table..." : $"{host}'s table  ·  {_view?.SeatCount} players";
            }
            return "ask for cards, make books of four";
        }

        public const string HelpText =
            "<b>How to play Go Fish</b>\n" +
            "Collect books: all four cards of a rank. On your turn, click a card in your hand to pick its rank, " +
            "then click a player to ask them for that rank. They hand over every card of it they have, and you " +
            "go again. If they have none, they say \"Go fish!\" and you draw a card from the pond; if it's the rank " +
            "you asked for, you go again. If your hand runs out, you draw a card on your turn. Once all 13 books " +
            "are down, the most books wins.\n\n" +
            "<b>Tables</b>\n" +
            "Start a table to play against computer players. Anyone in your session with Card Table can join it " +
            "from their Go Fish tab, taking a computer player's seat (and its cards). If someone leaves, a computer " +
            "player takes over. With other people at the table, a player who doesn't move for 45 seconds gets a " +
            "computer move. Closing the window keeps your seat (the icon shows a dot when it's your turn); Leave " +
            "table gives it up.\n\n" +
            "Click to close.";

        public string StatsText()
        {
            GoFishStats stats = _store.FishStats;
            int rate = stats.Played > 0 ? Mathf.RoundToInt(100f * stats.Won / stats.Played) : 0;
            return "<b>Go Fish stats</b>\n\n" +
                   $"Played {stats.Played}\n" +
                   $"Won {stats.Won} ({rate}%)\n" +
                   $"Books made {stats.Books}\n" +
                   $"Most books in a game {stats.MostBooks}\n\n" +
                   "Click to close.";
        }

        // ---- Following the table ----

        private static string Key(GoFishView view) => view.Table + "/" + view.GameNo;

        private void ForgetTable()
        {
            _view = null;
            _picked = -1;
            _slides.Clear();
            _flies.Clear();
            _bubbles.Clear();
            _handPos.Clear();
            _confirmLeaveUntil = -1f;
            _confirmNewUntil = -1f;
        }

        // A new view of the table: works out what moved, so it can be shown moving.
        private void Absorb(GoFishView view, float now)
        {
            GoFishView old = _view;
            bool newTable = old == null || old.Table != view.Table || old.MySeat != view.MySeat;
            bool newGame = newTable || old.GameNo != view.GameNo;
            _view = view;
            if (!view.Over)
            {
                _playing = Key(view);
            }
            int me = view.MySeat;
            bool animate = S.Animations.Value;

            if (newGame)
            {
                _slides.Clear();
                _flies.Clear();
                _bubbles.Clear();
                _picked = -1;
                _confirmNewUntil = -1f;
                _lastEventId = view.Log.Count > 0 ? view.Log.Max(e => e.Id) : 0;
                if (animate && view.Log.Any(e => e.Kind == FishEventKind.Deal))
                {
                    // Dealt from the pond, one card after another.
                    float start = now;
                    foreach (int card in SortedHand())
                    {
                        _slides[card] = new Slide { From = PondSpot(), Start = start };
                        start += DealGap;
                    }
                }
                foreach (FishEvent e in view.Log.Where(e => e.Kind == FishEventKind.Join && e.Seat != me))
                {
                    Say(e.Seat, "Hello!", now);
                }
                return;
            }

            List<FishEvent> fresh = view.Log.Where(e => e.Id > _lastEventId).OrderBy(e => e.Id).ToList();
            if (fresh.Count > 0)
            {
                _lastEventId = fresh[fresh.Count - 1].Id;
            }
            List<int> added = view.Hand.Except(old.Hand).ToList();
            List<int> removed = old.Hand.Except(view.Hand).ToList();
            var placed = new HashSet<int>();
            int lastAskFrom = -1;
            float t = now;
            foreach (FishEvent e in fresh)
            {
                switch (e.Kind)
                {
                    case FishEventKind.Ask:
                    {
                        Say(e.Seat, $"Any {Plural(e.Rank)}?", t);
                        Say(e.Target, e.Count == 0 ? "Go fish!" : "Here you go!", t + AnswerDelay);
                        if (e.Count == 0 || !animate)
                        {
                            break;
                        }
                        float at = t + AnswerDelay;
                        if (e.Seat == me)
                        {
                            lastAskFrom = e.Target;
                            foreach (int card in added.Where(c => Cards.Rank(c) == e.Rank && !placed.Contains(c)))
                            {
                                _slides[card] = new Slide { From = CardSpot(e.Target), Start = at };
                                placed.Add(card);
                            }
                        }
                        else if (e.Target == me)
                        {
                            foreach (int card in removed.Where(c => Cards.Rank(c) == e.Rank))
                            {
                                AddFly(card, HandSpot(card), CardSpot(e.Seat), at, early: true);
                            }
                        }
                        else
                        {
                            for (int i = 0; i < e.Count; i++)
                            {
                                AddFly(-1, CardSpot(e.Target), CardSpot(e.Seat), at + i * 0.06f, early: false);
                            }
                        }
                        break;
                    }
                    case FishEventKind.Fish:
                    case FishEventKind.Draw:
                        if (e.Count > 0 && animate)
                        {
                            if (e.Seat == me)
                            {
                                int card = added.Where(c => !placed.Contains(c))
                                    .OrderBy(c => e.Rank >= 0 && Cards.Rank(c) == e.Rank ? 0 : 1).DefaultIfEmpty(-1).First();
                                if (card >= 0)
                                {
                                    _slides[card] = new Slide { From = PondSpot(), Start = t };
                                    placed.Add(card);
                                }
                            }
                            else
                            {
                                AddFly(-1, PondSpot(), CardSpot(e.Seat), t, early: false);
                            }
                        }
                        if (e.Kind == FishEventKind.Fish && e.Rank >= 0)
                        {
                            Say(e.Seat, $"Got {Single(e.Rank)}!", t + 0.3f);
                        }
                        break;
                    case FishEventKind.Book:
                        if (animate)
                        {
                            // The four cards go down together.
                            for (int suit = 0; suit < 4; suit++)
                            {
                                int card = Cards.Make(suit, e.Rank);
                                Vector2 from = e.Seat != me ? CardSpot(e.Seat)
                                    : old.Hand.Contains(card) ? HandSpot(card)
                                    : lastAskFrom >= 0 ? CardSpot(lastAskFrom) : PondSpot();
                                AddFly(card, from, BookSpot(e.Seat), t + suit * 0.05f, early: e.Seat == me && old.Hand.Contains(card));
                            }
                        }
                        break;
                    case FishEventKind.Join:
                        if (e.Seat != me)
                        {
                            Say(e.Seat, "Hello!", t);
                        }
                        break;
                }
                if (e.Kind == FishEventKind.Ask || e.Kind == FishEventKind.Fish || e.Kind == FishEventKind.Draw || e.Kind == FishEventKind.Book)
                {
                    t += EventGap;
                }
            }

            if (!animate)
            {
                _slides.Clear();
                return;
            }
            // Cards from events that scrolled out of the log come from the pond; the rest of your hand
            // glides over to make room.
            foreach (int card in added.Where(c => !placed.Contains(c)))
            {
                _slides[card] = new Slide { From = PondSpot(), Start = now };
            }
            foreach (int card in view.Hand.Intersect(old.Hand))
            {
                if (!_slides.ContainsKey(card) && _handPos.TryGetValue(card, out Vector2 was))
                {
                    _slides[card] = new Slide { From = was, Start = now };
                }
            }
        }

        private void AddFly(int card, Vector2 from, Vector2 to, float start, bool early)
        {
            _flies.Add(new Fly { Card = card, From = from, To = to, Start = start, Early = early });
        }

        private void Say(int seat, string text, float start)
        {
            if (seat >= 0)
            {
                _bubbles.Add(new Bubble { Seat = seat, Text = text, Start = start });
            }
        }

        // Puts a game you played to the end into the stats, once.
        private void CountGame()
        {
            GoFishView view = _view;
            if (view == null || !view.Over || view.MySeat < 0 || Key(view) == _counted || Key(view) != _playing)
            {
                return;
            }
            _counted = Key(view);
            GoFishStats stats = _store.FishStats;
            int books = view.Seats[view.MySeat].Books.Count;
            stats.Played++;
            if (view.Winners().Contains(view.MySeat))
            {
                stats.Won++;
            }
            stats.Books += books;
            stats.MostBooks = Math.Max(stats.MostBooks, books);
            _saveStats();
        }

        // ---- Where things are, in art pixels ----

        private int Opponents => _view.SeatCount - 1;

        private float SeatWidth => (FeltRight - FeltLeft) / (float)Math.Max(1, Opponents);

        // Seats along the top in turn order, starting after yours.
        private int SeatAt(int column) => (_view.MySeat + 1 + column) % _view.SeatCount;

        private int ColumnOf(int seat) => (seat - _view.MySeat - 1 + 2 * _view.SeatCount) % _view.SeatCount;

        private float SeatLeft(int seat) => Mathf.Round(FeltLeft + SeatWidth * ColumnOf(seat));

        private float SeatCentre(int seat) => FeltLeft + SeatWidth * (ColumnOf(seat) + 0.5f);

        private Rect SeatArea(int seat)
        {
            float left = SeatLeft(seat);
            float right = Mathf.Round(FeltLeft + SeatWidth * (ColumnOf(seat) + 1));
            return new Rect(left + 2, SeatTop, right - left - 4, SeatBottom - SeatTop);
        }

        private void Fan(int seat, out int left, out int step, out int shown)
        {
            shown = Math.Min(_view.Seats[seat].Cards, MaxFanCards);
            float room = SeatWidth - 6;
            step = shown > 1 ? Mathf.Clamp((int)((room - CardW) / (shown - 1)), 1, MaxFanStep) : 0;
            int width = CardW + step * Math.Max(0, shown - 1);
            left = Mathf.RoundToInt(SeatCentre(seat) - width / 2f);
        }

        // A card's top-left at a seat (yours: the middle of your hand).
        private Vector2 CardSpot(int seat)
        {
            if (seat == _view.MySeat)
            {
                return new Vector2((HandLeft + HandRight - CardW) / 2f, HandTop);
            }
            return new Vector2(SeatCentre(seat) - CardW / 2f, FanTop);
        }

        private static Vector2 PondSpot() => new Vector2(PondX - CardW / 2f, PondY - CardH / 2f - 1);

        private Vector2 BookSpot(int seat)
        {
            return seat == _view.MySeat ? new Vector2(YouLeft, YouBooksTop - 12) : new Vector2(SeatCentre(seat) - CardW / 2f, BooksTop - 24);
        }

        private Vector2 HandSpot(int card) => _handPos.TryGetValue(card, out Vector2 at) ? at : CardSpot(_view.MySeat);

        private List<int> SortedHand() => _view.Hand.OrderBy(Cards.Rank).ThenBy(Cards.Suit).ToList();

        // Where each card of your hand goes (before lifting).
        private List<Vector2> HandLayout(List<int> hand)
        {
            int count = hand.Count;
            int step = count > 1 ? Math.Min(HandStep, (HandRight - HandLeft - CardW) / (count - 1)) : 0;
            int width = CardW + step * Math.Max(0, count - 1);
            int left = HandLeft + (HandRight - HandLeft - width) / 2;
            return Enumerable.Range(0, count).Select(i => new Vector2(left + i * step, HandTop)).ToList();
        }

        // ---- Drawing ----

        private Rect ArtRect(float x, float y, float w, float h)
        {
            return new Rect(_tableRect.x + Mathf.Round(x) * _scale, _tableRect.y + Mathf.Round(y) * _scale,
                Mathf.Round(w) * _scale, Mathf.Round(h) * _scale);
        }

        private void Tex(Texture2D texture, Vector2 at)
        {
            GUI.DrawTexture(ArtRect(at.x, at.y, texture.width, texture.height), texture);
        }

        private void Fill(float x, float y, float w, float h, Color colour)
        {
            Color was = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(ArtRect(x, y, w, h), _art.Pixel);
            GUI.color = was;
        }

        // A one-pixel frame with its corners left out.
        private void Frame(float x, float y, float w, float h, Color colour)
        {
            Fill(x + 1, y, w - 2, 1, colour);
            Fill(x + 1, y + h - 1, w - 2, 1, colour);
            Fill(x, y + 1, 1, h - 2, colour);
            Fill(x + w - 1, y + 1, 1, h - 2, colour);
        }

        private void Text(Rect rect, string text, GUIStyle style, bool shadow = true)
        {
            if (shadow)
            {
                Color was = style.normal.textColor;
                style.normal.textColor = TextShadow;
                float off = Mathf.Max(1f, Mathf.Round(_scale / 2f));
                GUI.Label(new Rect(rect.x + off, rect.y + off, rect.width, rect.height), RichName.Plain(text), style);
                style.normal.textColor = was;
            }
            GUI.Label(rect, text, style);
        }

        private float U(float units) => Mathf.Round(units * _unit);

        private static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        public void DrawTable(Rect table, int scale, float unit, CardArt art, bool blocked)
        {
            _tableRect = table;
            _scale = Math.Max(1, scale);
            _unit = unit;
            _art = art;
            MakeStyles();
            float now = Time.unscaledTime;

            if (_tables.Current == null || _view == null || _view.MySeat < 0)
            {
                DrawLobby(now);
                return;
            }

            GoFishView view = _view;
            int me = view.MySeat;
            bool myTurn = view.MyTurn;
            bool pending = _tables.Joined != null && _tables.Joined.AskPending(now);
            bool overBox = view.Over && _dismissed != Key(view);
            bool input = !blocked && !overBox;

            GUI.DrawTexture(table, art.FishTable);

            // Your hand, and what's under the mouse.
            Event e = Event.current;
            Vector2 mouse = (e.mousePosition - table.position) / _scale;
            List<int> hand = SortedHand();
            List<Vector2> spots = HandLayout(hand);
            int hoverCard = -1;
            if (input && table.Contains(e.mousePosition))
            {
                for (int i = hand.Count - 1; i >= 0; i--)
                {
                    Vector2 at = spots[i] - new Vector2(0f, Cards.Rank(hand[i]) == _picked ? Lift : 0);
                    if (new Rect(at.x, at.y, CardW, CardH + Lift).Contains(mouse))
                    {
                        hoverCard = hand[i];
                        break;
                    }
                }
            }
            int hoverRank = myTurn && !pending && hoverCard >= 0 ? Cards.Rank(hoverCard) : -1;
            int hoverSeat = -1;
            if (input && myTurn && !pending && _picked >= 0 && hoverCard < 0)
            {
                for (int s = 0; s < view.SeatCount; s++)
                {
                    if (s != me && view.Seats[s].Cards > 0 && SeatArea(s).Contains(mouse))
                    {
                        hoverSeat = s;
                    }
                }
            }

            if (input && e.type == EventType.MouseDown && e.button == 0 && table.Contains(e.mousePosition))
            {
                if (hoverRank >= 0)
                {
                    _picked = _picked == hoverRank ? -1 : hoverRank;
                }
                else if (hoverSeat >= 0)
                {
                    int rank = _picked;
                    _picked = -1;
                    _tables.Current.Ask(hoverSeat, rank, now);
                }
                else if (hoverCard < 0)
                {
                    _picked = -1;
                }
                e.Use();
            }

            // The other players.
            for (int column = 0; column < Opponents; column++)
            {
                DrawSeat(SeatAt(column), hoverSeat);
            }

            // The pond.
            Vector2 pond = PondSpot();
            Vector2[] pile = { new Vector2(-18, 3), new Vector2(0, -2), new Vector2(15, 1) };
            for (int i = 0; i < Math.Min(3, view.Pond); i++)
            {
                Tex(art.Back, pond + pile[i]);
            }
            Text(ArtRect(PondX - 50, PondY + PondRY + 3, 100, 9), view.Pond > 0 ? $"{view.Pond} in the pond" : "The pond is empty", _pondStyle);

            DrawLog();

            // You.
            if (myTurn)
            {
                Fill(YouLeft, YouTop + 4, 3, 3, Gold);
            }
            Text(ArtRect(YouLeft + (myTurn ? 5 : 0), YouTop, YouWidth, NameHeight), myTurn ? "<color=#F5D061FF>You</color>" : "You", _youStyle);
            DrawBooks(view.Seats[me].Books, YouLeft, YouBooksTop, YouWidth, alignLeft: true);

            // Your cards.
            for (int i = 0; i < hand.Count; i++)
            {
                int card = hand[i];
                int rank = Cards.Rank(card);
                Vector2 target = spots[i] - new Vector2(0f, rank == _picked ? Lift : rank == hoverRank ? HoverLift : 0);
                Vector2 at = target;
                if (_slides.TryGetValue(card, out Slide slide))
                {
                    float t = (now - slide.Start) / SlideTime;
                    at = t <= 0f ? slide.From : t < 1f ? Vector2.Lerp(slide.From, target, Ease(t)) : target;
                }
                Tex(art.Faces[card], at);
                if (rank == _picked)
                {
                    Tex(art.Glow, at);
                }
                if (e.type == EventType.Repaint)
                {
                    _handPos[card] = at;
                }
            }

            // Cards on their way across the table.
            foreach (Fly fly in _flies)
            {
                float t = (now - fly.Start) / FlyTime;
                if (t < 0f && !fly.Early)
                {
                    continue;
                }
                Vector2 at = Vector2.Lerp(fly.From, fly.To, Ease(Mathf.Clamp01(t)));
                Tex(fly.Card >= 0 ? art.Faces[fly.Card] : art.Back, at);
            }

            // Speech bubbles: the newest started one for each seat.
            foreach (IGrouping<int, Bubble> seat in _bubbles.Where(b => now >= b.Start).GroupBy(b => b.Seat))
            {
                Bubble bubble = seat.OrderBy(b => b.Start).Last();
                DrawBubble(bubble.Seat, bubble.Text);
            }

            if (hoverSeat >= 0 && _picked >= 0)
            {
                DrawTip($"Ask {SeatName(hoverSeat, false)} for {Plural(_picked)}", e.mousePosition);
            }

            if (overBox && !blocked)
            {
                DrawOver(now);
            }
        }

        private void DrawSeat(int seat, int hoverSeat)
        {
            SeatView who = _view.Seats[seat];
            Rect area = SeatArea(seat);
            bool turn = !_view.Over && _view.Turn == seat;
            if (seat == hoverSeat)
            {
                Frame(area.x, area.y, area.width, area.height, Gold);
            }
            else if (turn)
            {
                Frame(area.x, area.y, area.width, area.height, FeltMark);
            }

            string name = SeatName(seat, true);
            Rect nameRect = ArtRect(area.x + 1, SeatTop + 1, area.width - 2, NameHeight);
            if (turn)
            {
                float width = _nameStyle.CalcSize(new GUIContent(RichName.Plain(name))).x / _scale;
                Fill(Mathf.Max(area.x + 2, SeatCentre(seat) - width / 2f - 5), SeatTop + 5, 3, 3, Gold);
            }
            Text(nameRect, name, _nameStyle);

            Fan(seat, out int left, out int step, out int shown);
            for (int i = 0; i < shown; i++)
            {
                Tex(_art.Back, new Vector2(left + i * step, FanTop));
            }
            if (who.Cards > 0)
            {
                int width = CardW + step * Math.Max(0, shown - 1);
                var badge = new Vector2(left + width - BadgeSize + 3, FanTop + CardH - BadgeSize + 2);
                Tex(_art.Badge, badge);
                GUI.Label(ArtRect(badge.x, badge.y, BadgeSize, BadgeSize), who.Cards.ToString(), _badgeStyle);
            }
            DrawBooks(who.Books, area.x + 2, BooksTop, area.width - 4, alignLeft: false);
        }

        // Finished books as little tiles, centred (or from the left), with "+N" when they don't fit.
        private void DrawBooks(List<int> books, float left, float top, float width, bool alignLeft)
        {
            if (books.Count == 0)
            {
                return;
            }
            List<Texture2D> tiles = books.Select(r => _art.BookTiles[r]).ToList();
            if (alignLeft)
            {
                // Rows from the left, wrapping.
                float x = left;
                float y = top;
                foreach (Texture2D tile in tiles)
                {
                    if (x + tile.width > left + width && x > left)
                    {
                        x = left;
                        y += tile.height + 1;
                    }
                    Tex(tile, new Vector2(x, y));
                    x += tile.width;
                }
                return;
            }
            int fit = tiles.Count;
            while (fit > 0 && tiles.Take(fit).Sum(t => t.width) + (fit < tiles.Count ? 12 : 0) > width)
            {
                fit--;
            }
            float total = tiles.Take(fit).Sum(t => t.width) + (fit < tiles.Count ? 12 : 0);
            float bx = Mathf.Round(left + (width - total) / 2f);
            for (int i = 0; i < fit; i++)
            {
                Tex(tiles[i], new Vector2(bx, top));
                bx += tiles[i].width;
            }
            if (fit < tiles.Count)
            {
                Text(ArtRect(bx + 1, top, 12, CardPixels.BookTileHeight), "+" + (tiles.Count - fit), _badgeStyle);
            }
        }

        private void DrawLog()
        {
            List<string> lines = _view.Log.Where(ShownInLog).Select(LogLine).ToList();
            if (lines.Count > LogLines)
            {
                lines = lines.Skip(lines.Count - LogLines).ToList();
            }
            if (lines.Count == 0)
            {
                return;
            }
            Fill(LogLeft, LogTop, LogRight - LogLeft, lines.Count * LogLineHeight + 6, LogBack);
            Color was = GUI.color;
            for (int i = 0; i < lines.Count; i++)
            {
                bool newest = i == lines.Count - 1;
                GUI.color = new Color(1f, 1f, 1f, newest ? 1f : 0.62f + 0.07f * i);
                Rect rect = ArtRect(LogLeft + 4, LogTop + 3 + i * LogLineHeight, LogRight - LogLeft - 8, LogLineHeight);
                Text(rect, lines[i], _logStyle);
            }
            GUI.color = was;
        }

        private void DrawBubble(int seat, string text)
        {
            bool mine = seat == _view.MySeat;
            Vector2 size = _bubbleStyle.CalcSize(new GUIContent(text));
            int w = Mathf.CeilToInt(size.x / _scale) + 8;
            const int h = 12;
            float centre = mine ? YouLeft + w / 2f : SeatCentre(seat);
            int left = Mathf.Clamp(Mathf.RoundToInt(centre - w / 2f), FeltLeft + 1, FeltRight - w - 1);
            int top = mine ? HandTop - 16 : FanTop + 10;
            Fill(left + 1, top + 1, w - 2, h - 2, Cream);
            Frame(left, top, w, h, Ink);
            // A little tail toward the speaker: up at their name, or down at yours.
            int tail = Mathf.Clamp(Mathf.RoundToInt(mine ? left + 8 : centre), left + 3, left + w - 4);
            int edge = mine ? top + h - 1 : top;
            int dir = mine ? 1 : -1;
            Fill(tail - 1, edge, 3, 1, Cream);
            Fill(tail - 2, edge, 1, 1, Ink);
            Fill(tail + 2, edge, 1, 1, Ink);
            Fill(tail, edge + dir, 1, 1, Cream);
            Fill(tail - 1, edge + dir, 1, 1, Ink);
            Fill(tail + 1, edge + dir, 1, 1, Ink);
            Fill(tail, edge + 2 * dir, 1, 1, Ink);
            GUI.Label(ArtRect(left, top, w, h), text, _bubbleStyle);
        }

        private void DrawTip(string text, Vector2 mouse)
        {
            Vector2 size = _tipStyle.CalcSize(new GUIContent(RichName.Plain(text)));
            var rect = new Rect(mouse.x + U(14f), mouse.y + U(18f), size.x + U(16f), size.y + U(8f));
            rect.x = Mathf.Min(rect.x, _tableRect.xMax - rect.width - U(4f));
            rect.y = Mathf.Min(rect.y, _tableRect.yMax - rect.height - U(4f));
            Color was = GUI.color;
            GUI.color = TipBack;
            GUI.DrawTexture(rect, _art.Pixel);
            GUI.color = was;
            GUI.Label(new Rect(rect.x + U(8f), rect.y, rect.width - U(16f), rect.height), text, _tipStyle);
        }

        private void DrawOver(float now)
        {
            GoFishView view = _view;
            int me = view.MySeat;
            List<int> winners = view.Winners();
            bool iWon = winners.Contains(me);
            string title = winners.Count > 1
                ? iWon ? "You tied for the win!" : "It's a tie!"
                : iWon ? "You won!" : $"{SeatName(winners[0], true)} won!";
            List<int> order = Enumerable.Range(0, view.SeatCount).OrderByDescending(s => view.Seats[s].Books.Count).ThenBy(s => s).ToList();

            float rowH = U(24f);
            float w = Mathf.Min(_tableRect.width - U(24f), U(340f));
            float h = U(60f) + order.Count * rowH + U(52f);
            var box = new Rect(Mathf.Round(_tableRect.center.x - w / 2f), Mathf.Round(_tableRect.center.y - h / 2f), w, h);
            GUI.Box(box, GUIContent.none, _boxStyle);
            GUI.Box(box, GUIContent.none, _boxStyle);
            float x = box.x + U(20f);
            float width = box.width - U(40f);
            float y = box.y + U(12f);
            GUI.Label(new Rect(box.x + U(8f), y, box.width - U(16f), U(36f)), $"<b>{title}</b>", _bigStyle);
            y += U(46f);
            foreach (int s in order)
            {
                int books = view.Seats[s].Books.Count;
                GUI.Label(new Rect(x, y, width - U(90f), rowH), SeatName(s, true), _rowStyle);
                GUI.Label(new Rect(x + width - U(90f), y, U(90f), rowH), books == 1 ? "1 book" : $"{books} books", _rowStyle);
                y += rowH;
            }
            y += U(10f);
            GoFishHost host = _tables.Hosting;
            if (host != null)
            {
                float bw = U(140f);
                if (GUI.Button(new Rect(Mathf.Round(box.center.x - bw / 2f), y, bw, U(28f)), "New game", _buttonStyle))
                {
                    _clicks.Add(() => host.NewGame(Time.unscaledTime));
                }
            }
            else if (_tables.Joined != null)
            {
                GUI.Label(new Rect(x, y, width, U(28f)), $"Waiting for {Players.Name(_tables.Joined.HostId)} to deal again", _centreStyle);
            }
            // A click anywhere else on the table closes the box (after the button, which gets its click first).
            if (GUI.Button(_tableRect, GUIContent.none, GUIStyle.none))
            {
                string key = Key(view);
                _clicks.Add(() => _dismissed = key);
            }
        }

        // ---- Without a table: start one, or join one ----

        private void DrawLobby(float now)
        {
            GUI.DrawTexture(_tableRect, _art.Table);
            float x = _tableRect.x + U(24f);
            float w = _tableRect.width - U(48f);
            float y = _tableRect.y + U(18f);
            float bottom = _tableRect.yMax - U(18f);
            float rowH = U(28f);

            GUI.Label(new Rect(x, y, w, U(34f)), "<b>Go Fish</b>", _titleStyle);
            y += U(34f);
            GUI.Label(new Rect(x, y, w, U(22f)), "Ask for cards and make books of four. Most books wins.", _dimCentreStyle);
            y += U(36f);

            // Start a table.
            GUI.Label(new Rect(x, y, w, U(22f)), "<b>Start a table</b>", _headStyle);
            y += U(26f);
            float bx = x;
            GUI.Label(new Rect(bx, y, U(64f), rowH), "Players", _textStyle);
            bx += U(64f);
            int seats = Mathf.Clamp(S.GoFishSeats.Value, GoFish.MinSeats, GoFish.MaxSeats);
            for (int n = GoFish.MinSeats; n <= GoFish.MaxSeats; n++)
            {
                int pick = n;
                if (GUI.Button(new Rect(bx, y, U(32f), rowH), n.ToString(), n == seats ? _pickedStyle : _buttonStyle))
                {
                    _clicks.Add(() => S.GoFishSeats.Value = pick);
                }
                bx += U(36f);
            }
            float startW = U(130f);
            if (GUI.Button(new Rect(x + w - startW, y, startW, rowH), "Start a table", _buttonStyle))
            {
                _clicks.Add(() =>
                {
                    _tables.StartTable(Mathf.Clamp(S.GoFishSeats.Value, GoFish.MinSeats, GoFish.MaxSeats), Time.unscaledTime);
                    _notice = null;
                });
            }
            y += rowH + U(4f);
            bool inLobby = _tables.InLobby;
            string note = "Computer players fill the empty seats." +
                          (inLobby ? " Anyone here with Card Table can join from their Go Fish tab, taking a computer player's seat." : "");
            float noteH = _dimStyle.CalcHeight(new GUIContent(note), w);
            GUI.Label(new Rect(x, y, w, noteH), note, _dimStyle);
            y += noteH + U(18f);

            // Other tables.
            GUI.Label(new Rect(x, y, w, U(22f)), "<b>Tables here</b>", _headStyle);
            y += U(26f);
            GoFishClient joining = _tables.Joining ? _tables.Joined : null;
            if (joining != null)
            {
                GUI.Label(new Rect(x, y, w - U(100f), rowH), $"Joining {Players.Name(joining.HostId)}'s table...", _rowStyle);
                if (GUI.Button(new Rect(x + w - U(90f), y, U(90f), rowH), "Cancel", _buttonStyle))
                {
                    _clicks.Add(() => _tables.LeaveTable(Time.unscaledTime));
                }
                y += rowH + U(6f);
            }
            else if (!inLobby)
            {
                GUI.Label(new Rect(x, y, w, U(40f)), "Other players' tables show here when you're in a session.", _dimStyle);
                y += U(40f);
            }
            else if (_tables.Others.Count == 0)
            {
                GUI.Label(new Rect(x, y, w, U(40f)), "Nobody here has a table open. Start one, and others with Card Table will see it here.", _dimStyle);
                y += U(40f);
            }
            else
            {
                foreach (TableInfo info in _tables.Others.OrderBy(t => t.Host))
                {
                    if (y + rowH > bottom - U(26f))
                    {
                        break;
                    }
                    float joinW = U(90f);
                    string people = info.Humans.Count == 1 ? "1 person" : $"{info.Humans.Count} people";
                    string what = $"{Players.Name(info.Host)}'s table  <color=#FFFFFF99>{people}, {info.Seats} seats{(info.Over ? ", between games" : "")}</color>";
                    GUI.Label(new Rect(x, y, w - joinW - U(8f), rowH), what, _rowStyle);
                    bool free = info.FreeSeats > 0;
                    bool was = GUI.enabled;
                    GUI.enabled = free;
                    TableInfo pick = info;
                    if (GUI.Button(new Rect(x + w - joinW, y, joinW, rowH), free ? "Join" : "Full", _buttonStyle))
                    {
                        _clicks.Add(() => _tables.Join(pick, Time.unscaledTime));
                    }
                    GUI.enabled = was;
                    y += rowH + U(4f);
                }
            }

            if (_notice != null && now < _noticeUntil)
            {
                GUI.Label(new Rect(x, bottom - U(24f), w, U(24f)), _notice, _centreStyle);
            }
        }

        // ---- The row under the table ----

        public void DrawStatus(Rect row, float unit)
        {
            _unit = unit;
            MakeStyles();
            float now = Time.unscaledTime;
            float right = row.xMax;
            GoFishView view = _tables.Current != null ? _view : null;

            if (_tables.Hosting != null)
            {
                GoFishHost host = _tables.Hosting;
                bool sure = now <= _confirmLeaveUntil;
                right = StatusButton(row, right, sure ? "Sure? Click again" : "Leave table", () =>
                {
                    // Closing a table with people at it, or a game halfway through, takes a second click.
                    if ((host.HasGuests || !host.Over) && Time.unscaledTime > _confirmLeaveUntil)
                    {
                        _confirmLeaveUntil = Time.unscaledTime + ConfirmTime;
                        _confirmNewUntil = -1f;
                        return;
                    }
                    _confirmLeaveUntil = -1f;
                    _tables.LeaveTable(Time.unscaledTime);
                });
                bool sureNew = now <= _confirmNewUntil;
                right = StatusButton(row, right, sureNew ? "Sure? Click again" : "New game", () =>
                {
                    if (!host.Over && Time.unscaledTime > _confirmNewUntil)
                    {
                        _confirmNewUntil = Time.unscaledTime + ConfirmTime;
                        _confirmLeaveUntil = -1f;
                        return;
                    }
                    _confirmNewUntil = -1f;
                    host.NewGame(Time.unscaledTime);
                });
            }
            else if (_tables.Joined != null)
            {
                right = StatusButton(row, right, _tables.Joining ? "Cancel" : "Leave table", () => _tables.LeaveTable(Time.unscaledTime));
            }

            string text;
            if (_notice != null && now < _noticeUntil && view != null)
            {
                text = _notice;
            }
            else if (view == null)
            {
                text = "";
            }
            else if (view.Over)
            {
                List<int> winners = view.Winners();
                text = winners.Contains(view.MySeat)
                    ? winners.Count > 1 ? "Game over: you tied for the win" : "Game over: you won!"
                    : $"Game over: {string.Join(" and ", winners.Select(s => SeatName(s, true)))} {(winners.Count > 1 ? "tied" : "won")}";
                if (_tables.Joined != null)
                {
                    text += $"  ·  waiting for {Players.Name(_tables.Joined.HostId)}";
                }
            }
            else if (view.MyTurn)
            {
                bool pending = _tables.Joined != null && _tables.Joined.AskPending(now);
                text = pending ? "Asking..."
                    : _picked >= 0 ? $"<color=#F5D061FF><b>Your turn</b></color>  ·  now click who to ask for {Plural(_picked)}"
                    : "<color=#F5D061FF><b>Your turn</b></color>  ·  pick a card, then who to ask";
            }
            else
            {
                text = $"{SeatName(view.Turn, true)}'s turn";
            }
            GUI.Label(new Rect(row.x, row.y, Mathf.Max(0f, right - row.x), row.height), text, _statusStyle);
        }

        private float StatusButton(Rect row, float right, string label, Action action)
        {
            float width = Mathf.Max(U(40f), _buttonStyle.CalcSize(new GUIContent(label)).x + U(16f));
            var rect = new Rect(right - width, row.y + U(2f), width, row.height - U(4f));
            if (GUI.Button(rect, label, _buttonStyle))
            {
                _clicks.Add(action);
            }
            return rect.x - U(4f);
        }

        // ---- Words ----

        private string SeatName(int seat, bool capital)
        {
            if (seat == _view.MySeat)
            {
                return capital ? "You" : "you";
            }
            SeatView who = _view.Seats[seat];
            return who.IsBot ? Players.Bot(who.Bot) : Players.Name(who.Who);
        }

        private static bool ShownInLog(FishEvent e)
        {
            // A plain "Go fish" draw is already in the ask's line.
            return !(e.Kind == FishEventKind.Fish && e.Rank < 0 && e.Count > 0);
        }

        private string LogLine(FishEvent e)
        {
            string who = e.Seat >= 0 && e.Seat < _view.SeatCount ? SeatName(e.Seat, true) : "Someone";
            switch (e.Kind)
            {
                case FishEventKind.Deal:
                    return $"New game: {e.Count} cards each";
                case FishEventKind.Ask:
                    string target = e.Target >= 0 && e.Target < _view.SeatCount ? SeatName(e.Target, false) : "someone";
                    return $"{who} asked {target} for {Plural(e.Rank)}: {(e.Count == 0 ? "Go fish" : "got " + e.Count)}";
                case FishEventKind.Fish:
                    return e.Count == 0 ? "The pond is empty" : $"{who} fished {Single(e.Rank)}, so goes again";
                case FishEventKind.Draw:
                    return $"{who} drew a card from the pond";
                case FishEventKind.Book:
                    return $"{who} made a book of {Plural(e.Rank)}!";
                case FishEventKind.Out:
                    return $"{who} ran out of cards";
                case FishEventKind.Join:
                    return e.Seat == _view.MySeat ? "You sat down" : $"{Players.Name(e.Who)} sat down";
                case FishEventKind.Leave:
                    return $"{Players.Name(e.Who)} left, and {who} took over";
                case FishEventKind.Over:
                    return "Game over";
                default:
                    return "";
            }
        }

        private static string Plural(int rank)
        {
            switch (rank)
            {
                case 1: return "Aces";
                case 11: return "Jacks";
                case 12: return "Queens";
                case 13: return "Kings";
                default: return rank + "s";
            }
        }

        private static string Single(int rank)
        {
            switch (rank)
            {
                case 1: return "an Ace";
                case 8: return "an 8";
                case 11: return "a Jack";
                case 12: return "a Queen";
                case 13: return "a King";
                default: return "a " + rank;
            }
        }

        // ---- Styles ----

        private void MakeStyles()
        {
            if (_nameStyle != null && _styleScale == _scale && Mathf.Approximately(_styleUnit, _unit))
            {
                return;
            }
            _styleScale = _scale;
            _styleUnit = _unit;
            int Art(float size) => Mathf.Max(8, Mathf.RoundToInt(size * _scale));
            int Ui(float size) => Mathf.Max(8, Mathf.RoundToInt(size * _unit));

            GUIStyle Label(int size, Color colour, TextAnchor anchor, bool bold = false)
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = size,
                    alignment = anchor,
                    richText = true,
                    wordWrap = false,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                };
                style.normal.textColor = colour;
                return style;
            }

            _nameStyle = Label(Art(NameFont), Color.white, TextAnchor.MiddleCenter, bold: true);
            _youStyle = Label(Art(NameFont), Color.white, TextAnchor.MiddleLeft, bold: true);
            _logStyle = Label(Art(NameFont), Color.white, TextAnchor.MiddleLeft);
            _badgeStyle = Label(Art(SmallFont), Color.white, TextAnchor.MiddleCenter, bold: true);
            _bubbleStyle = Label(Art(NameFont), Ink, TextAnchor.MiddleCenter, bold: true);
            _pondStyle = Label(Art(NameFont), new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter);
            _tipStyle = Label(Ui(14f), Color.white, TextAnchor.MiddleLeft);
            _titleStyle = Label(Ui(26f), Gold, TextAnchor.MiddleCenter);
            _textStyle = Label(Ui(15f), Color.white, TextAnchor.MiddleLeft);
            _rowStyle = Label(Ui(15f), Color.white, TextAnchor.MiddleLeft);
            _headStyle = Label(Ui(15f), Color.white, TextAnchor.MiddleLeft);
            _dimStyle = Label(Ui(14f), Dim, TextAnchor.UpperLeft);
            _dimStyle.wordWrap = true;
            _dimCentreStyle = Label(Ui(14f), Dim, TextAnchor.MiddleCenter);
            _statusStyle = Label(Ui(14f), new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleLeft);
            _bigStyle = Label(Ui(26f), Gold, TextAnchor.MiddleCenter);
            _centreStyle = Label(Ui(15f), Color.white, TextAnchor.MiddleCenter);

            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Ui(14f) };
            _pickedStyle = new GUIStyle(_buttonStyle);
            _pickedStyle.normal = GUI.skin.button.onNormal;
            _pickedStyle.hover = GUI.skin.button.onHover;
            _pickedStyle.active = GUI.skin.button.onActive;
            _boxStyle = new GUIStyle(GUI.skin.box);
        }
    }
}
