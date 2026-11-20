using System;
using System.Diagnostics;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Debugging;

namespace SoapBox
{
    // Runs one cart: its Lua code in MoonSharp (sandboxed: no files, no loading code, no game),
    // 60 updates a second, drawing onto Screen. The cart's functions:
    //   _init()    once, after the cart's top-level code
    //   _update()  60 times a second
    //   _draw()    after updates, at most once per game frame; the picture shows when it returns
    //
    // All the cart's code runs inside one long-lived Lua coroutine (the driver below) that MoonSharp
    // pauses every few thousand instructions. Tick() gives the cart a slice of time each game frame
    // and carries on next frame when it's used up, so a slow cart runs slow instead of making the game
    // stutter, and a cart stuck in a loop never freezes it. Code MoonSharp can't pause (inside a cart's
    // own coroutines, a sort's compare function...) is stopped after StuckSeconds (SoapBoxLimits).
    //
    // No Unity here: the window (or the test program) calls Tick, fills in Controls and shows Shown.
    internal sealed partial class Runner
    {
        public const int Width = 192;
        public const int Height = 128;
        public const int UpdatesPerSecond = 60;

        private const double Step = 1.0 / UpdatesPerSecond;
        // Updates run back to back at most this many times before a draw.
        private const int MaxCatchUp = 4;
        // How much running-behind is remembered, in seconds; beyond it the cart just runs slow.
        private const double MaxBehind = 0.25;
        private const long CheckEvery = 5000;     // Lua instructions between looks at the clock
        private const double StuckSeconds = 2.0;  // longest MoonSharp can't pause for
        private const double GiveUpSeconds = 10.0; // longest one part of the cart may take, paused or not
        private const long MemoryAllowance = 768L * 1024 * 1024;

        // The driver: runs one part of the cart each time it's resumed, then yields Done.
        // Commands: 0 the cart's top-level code, 1 _init, 2 _update, 3 _draw.
        private const string DriverCode = @"
local yield, main = coroutine.yield, ...
local done = {}
return done, function()
  local cmd = yield()
  while true do
    if cmd == 0 then main()
    elseif cmd == 1 then if _init then _init() end
    elseif cmd == 2 then if _update then _update() end
    elseif cmd == 3 then if _draw then _draw() end
    end
    cmd = yield(done)
  end
end";

        public readonly Cart Cart;
        public readonly Surface Screen = new Surface(Width, Height);   // the cart draws here
        public readonly Surface Shown = new Surface(Width, Height);    // its last finished picture
        public readonly Controls Controls = new Controls();

        // Goes up each time Shown changes.
        public int ShownVersion { get; private set; }

        // Set once the cart has stopped with an error. ErrorLine is 0 when the line isn't known.
        public string Error { get; private set; }
        public int ErrorLine { get; private set; }

        // Seconds of cart time (updates run / 60). Stands still while the cart is paused.
        public double Time => _updates * Step;

        // True while one part of the cart has run for over a second without finishing.
        public bool Busy => _phase != Phase.None && _phaseRun > Stopwatch.Frequency;

        private enum Phase
        {
            None = -1,
            Main = 0,
            Init = 1,
            Update = 2,
            Draw = 3,
        }

        private readonly Action<string> _log;
        private readonly Script _script;
        private readonly Coroutine _driver;
        private readonly DynValue _done;
        private readonly long _memoryCeiling;

        private Phase _phase = Phase.None;
        private bool _resumeEmpty;   // the driver was paused mid-part, so it carries on (no command)
        private long _phaseRun;      // Stopwatch ticks the current part has spent running
        private bool _mainDone;
        private bool _initDone;
        private bool _needDraw;
        private int _updatesSinceDraw;
        private double _behind;
        private long _updates;

        public Runner(Cart cart, Action<string> log)
        {
            Cart = cart;
            _log = log;
            _memoryCeiling = GC.GetTotalMemory(false) + MemoryAllowance;
            Screen.Clear(0);
            Shown.Clear(0);

            _script = new Script(CoreModules.Preset_SoftSandbox);
            Register();

            try
            {
                DynValue main = _script.LoadString(cart.Code ?? "", null, "cart");
                DynValue made = _script.Call(_script.LoadString(DriverCode, null, "driver"), main);
                _done = made.Tuple[0];
                _driver = _script.CreateCoroutine(made.Tuple[1]).Coroutine;
                _driver.AutoYieldCounter = CheckEvery;
                _driver.Resume();   // runs up to the driver's first yield
            }
            catch (InterpreterException e)
            {
                Fail(e);
            }
        }

        // Gives the cart up to budget seconds of work for seconds of game time.
        public void Tick(double seconds, double budget)
        {
            if (Error != null || _driver == null)
            {
                return;
            }
            _behind = Math.Min(_behind + Math.Max(0.0, seconds), MaxBehind);
            long until = Stopwatch.GetTimestamp() + (long)(budget * Stopwatch.Frequency);

            while (Error == null)
            {
                if (_phase == Phase.None)
                {
                    if (!_mainDone)
                    {
                        Begin(Phase.Main);
                    }
                    else if (!_initDone)
                    {
                        Begin(Phase.Init);
                    }
                    else if (_behind >= Step && !(_needDraw && _updatesSinceDraw >= MaxCatchUp))
                    {
                        _behind -= Step;
                        BeginUpdate();
                        Begin(Phase.Update);
                    }
                    else if (_needDraw)
                    {
                        Begin(Phase.Draw);
                    }
                    else
                    {
                        return;
                    }
                }

                if (!Run(until))
                {
                    return;   // out of time this frame; the same part carries on next frame
                }

                Phase finished = _phase;
                _phase = Phase.None;
                switch (finished)
                {
                    case Phase.Main:
                        _mainDone = true;
                        _needDraw = true;   // show whatever the top-level code drew
                        break;
                    case Phase.Init:
                        _initDone = true;
                        _needDraw = true;
                        break;
                    case Phase.Update:
                        _updates++;
                        _updatesSinceDraw++;
                        _needDraw = true;
                        break;
                    case Phase.Draw:
                        _needDraw = false;
                        _updatesSinceDraw = 0;
                        Buffer.BlockCopy(Screen.Pixels, 0, Shown.Pixels, 0, Screen.Pixels.Length);
                        ShownVersion++;
                        break;
                }
                if (Stopwatch.GetTimestamp() >= until)
                {
                    return;
                }
            }
        }

        private void Begin(Phase phase)
        {
            _phase = phase;
            _resumeEmpty = false;
            _phaseRun = 0;
        }

        // Resumes the driver until the current part finishes (true) or time runs out (false).
        private bool Run(long until)
        {
            while (true)
            {
                long started = Stopwatch.GetTimestamp();
                SoapBoxLimits.Deadline = started + (long)(StuckSeconds * Stopwatch.Frequency);
                SoapBoxLimits.MemoryCeiling = _memoryCeiling;
                DynValue result;
                try
                {
                    result = _resumeEmpty ? _driver.Resume() : _driver.Resume(DynValue.NewNumber((int)_phase));
                }
                catch (InterpreterException e)
                {
                    Fail(e);
                    return false;
                }
                catch (Exception e)
                {
                    // Not a Lua error: most likely recursion so deep MoonSharp's stack ran out.
                    Fail(e is IndexOutOfRangeException ? "too many functions inside each other (does one call itself forever?)" : e.Message, 0);
                    _log?.Invoke("Cart stopped: " + e);
                    return false;
                }
                finally
                {
                    SoapBoxLimits.Deadline = 0;
                    SoapBoxLimits.MemoryCeiling = 0;
                }
                long now = Stopwatch.GetTimestamp();
                _phaseRun += now - started;

                switch (_driver.State)
                {
                    case CoroutineState.ForceSuspended:
                        _resumeEmpty = true;
                        if (_phaseRun > (long)(GiveUpSeconds * Stopwatch.Frequency))
                        {
                            Fail("this took too long (is there a loop that never ends?)", CurrentLine());
                            return false;
                        }
                        if (now >= until)
                        {
                            return false;
                        }
                        continue;
                    case CoroutineState.Suspended:
                        _resumeEmpty = false;
                        if (result.Type != DataType.Table || !ReferenceEquals(result.Table, _done.Table))
                        {
                            Fail("coroutine.yield() was called outside a coroutine", 0);
                            return false;
                        }
                        return true;
                    default:
                        Fail("the cart stopped running", 0);
                        return false;
                }
            }
        }

        // The cart line the paused driver is on, or 0.
        private int CurrentLine()
        {
            try
            {
                foreach (WatchItem item in _driver.GetStackTrace(0))
                {
                    SourceRef where = item.Location;
                    if (where != null && _script.GetSourceCode(where.SourceIdx)?.Name == "cart")
                    {
                        return where.FromLine;
                    }
                }
            }
            catch (Exception)
            {
                // Only for the message.
            }
            return 0;
        }

        private void Fail(InterpreterException e)
        {
            string message = e.DecoratedMessage ?? e.Message;
            int line = 0;
            // MoonSharp writes "cart:(line,column-column): message".
            if (message.StartsWith("cart:(", StringComparison.Ordinal))
            {
                int comma = message.IndexOf(',');
                int close = message.IndexOf("):", StringComparison.Ordinal);
                if (comma > 6 && int.TryParse(message.Substring(6, comma - 6), out int parsed))
                {
                    line = parsed;
                }
                if (close > 0)
                {
                    message = message.Substring(close + 2).Trim();
                }
            }
            Fail(message, line);
        }

        private void Fail(string message, int line)
        {
            if (Error != null)
            {
                return;
            }
            Error = string.IsNullOrWhiteSpace(message) ? "something went wrong" : message;
            ErrorLine = line;
            _phase = Phase.None;
            _log?.Invoke(line > 0 ? $"Cart error on line {line}: {Error}" : $"Cart error: {Error}");
        }
    }
}
