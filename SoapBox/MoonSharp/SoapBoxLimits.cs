// Added for Soap Box (2026-11-20); not part of MoonSharp. Limits that keep a cart's Lua code from
// freezing or crashing the game it runs inside:
//  - Deadline: Processor_InstructionLoop.cs calls Check() every 4096 instructions, and running code
//    stops once the deadline has passed (an endless loop). The host sets it before each run.
//  - Memory: Check() also looks at how much memory the game is using now and then, and stops the
//    cart once it's grown past MemoryCeiling (a table that never stops growing).
//  - Text: string concatenation (ExecConcat) and string.rep refuse to make strings longer than
//    MaxStringLength.
// The first two throw CartStoppedException, which isn't a ScriptRuntimeException, so a cart's own
// pcall can't catch it: it always reaches the host, with the line it stopped on.
using System;
using System.Diagnostics;

namespace MoonSharp.Interpreter
{
    internal static class SoapBoxLimits
    {
        // Stopwatch.GetTimestamp() value after which running code stops. 0 = no deadline.
        public static long Deadline;

        // GC.GetTotalMemory() value past which running code stops. 0 = no limit.
        public static long MemoryCeiling;

        public const int MaxStringLength = 1 << 20;

        private static int _calls;

        public static void Check()
        {
            if (Deadline != 0 && Stopwatch.GetTimestamp() > Deadline)
            {
                Deadline = 0;
                throw new CartStoppedException("this took too long (is there a loop that never ends?)");
            }
            if (MemoryCeiling != 0 && (++_calls & 63) == 0 && GC.GetTotalMemory(false) > MemoryCeiling)
            {
                // The figure counts garbage too, so collect before deciding.
                if (GC.GetTotalMemory(true) > MemoryCeiling)
                {
                    MemoryCeiling = 0;
                    throw new CartStoppedException("this used too much memory");
                }
            }
        }

        public static void CheckLength(long length)
        {
            if (length > MaxStringLength)
            {
                throw new ScriptRuntimeException("that text is too long (over " + MaxStringLength + " letters)");
            }
        }
    }

    // A cart stopped by SoapBoxLimits. Carries the line it was on, like other Lua errors.
    public class CartStoppedException : InterpreterException
    {
        public CartStoppedException(string message) : base(message)
        {
        }
    }
}
