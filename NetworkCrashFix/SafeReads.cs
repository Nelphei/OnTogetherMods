using System;
using HarmonyLib;
using PurrNet.Packing;
using PurrNet.Transports;

namespace NetworkCrashFix
{
    // The game's networking library (PurrNet) reads packets with BitPacker.ReadBitsWithoutChecks. It
    // always loads 8 bytes (9 when the value straddles them) through a raw pointer, then masks off the
    // bits it wanted. Near the end of a packet that reads a few bytes past the end of the array.
    // Movement updates are copied into arrays of exactly their size (PackByteData.Read), so this
    // happens all the time. It's harmless until those bytes fall in memory the game doesn't own: then
    // the whole game crashes. That was seen on a Mac, always while reading a player's rotation.
    //
    // The fix: near the end of the array, read one byte at a time instead, with bytes past the end
    // counted as 0. The game masks those bits off anyway, so every value comes out the same. Everywhere
    // else the game's own code runs.
    internal static class SafeReads
    {
        // False while the mod is switched off: the game reads its own way.
        public static volatile bool On = true;

        // Reads near the end of a packet that the fix made safe. Only a statistic, so not locked.
        public static long Count;

        private static AccessTools.FieldRef<BitPacker, byte[]> _buffer;
        private static AccessTools.FieldRef<BitPacker, int> _position;

        // Patches the reader. Returns false, having logged why, if it left the game alone.
        public static bool Apply(Harmony harmony)
        {
            var readBits = AccessTools.Method(typeof(BitPacker), nameof(BitPacker.ReadBits), new[] { typeof(byte) });
            var withoutChecks = AccessTools.Method(typeof(BitPacker), nameof(BitPacker.ReadBitsWithoutChecks),
                new[] { typeof(byte) });
            if (readBits == null || withoutChecks == null)
            {
                Plugin.Log.LogError("Couldn't find the game's packet reader (a game update may have changed it), " +
                    "so the fix is off.");
                return false;
            }
            try
            {
                _buffer = AccessTools.FieldRefAccess<BitPacker, byte[]>("_buffer");
                _position = AccessTools.FieldRefAccess<BitPacker, int>("_positionInBits");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Couldn't reach the packet reader's data ({e.Message}), so the fix is off.");
                return false;
            }

            // Before patching, while the game's reader is still its own.
            if (!MatchesGame(out string problem))
            {
                Plugin.Log.LogError($"The fix doesn't read the same values as the game ({problem}), so it's off.");
                return false;
            }

            // The unchecked read is what ReadBits and ReadBytes call. Patching it also stops Mono copying
            // the old version into them. ReadBits, where the crashes happened, is patched too in case
            // that doesn't work on some system.
            harmony.Patch(withoutChecks, prefix: new HarmonyMethod(typeof(SafeReads), nameof(WithoutChecksPrefix)));
            harmony.Patch(readBits, prefix: new HarmonyMethod(typeof(SafeReads), nameof(ReadBitsPrefix)));
            CheckPatched();
            return true;
        }

        // BitPacker.ReadBitsWithoutChecks.
        private static bool WithoutChecksPrefix(BitPacker __instance, byte bits, ref ulong __result)
        {
            if (!On)
            {
                return true;
            }
            byte[] buffer = _buffer(__instance);
            int position = _position(__instance);
            int start = position >> 3;
            // Far enough from the end for the game's read, or already past the end (then the game
            // throws its own "index out of range" error, which doesn't crash).
            if (buffer == null || start + 9 <= buffer.Length || start >= buffer.Length)
            {
                return true;
            }
            __result = Read(buffer, buffer.Length, position, bits);
            _position(__instance) = position + bits;
            Count++;
            return false;
        }

        // BitPacker.ReadBits: the game checks the bits are there, then calls ReadBitsWithoutChecks.
        private static bool ReadBitsPrefix(BitPacker __instance, byte bits, ref ulong __result)
        {
            if (!On)
            {
                return true;
            }
            byte[] buffer = _buffer(__instance);
            int position = _position(__instance);
            int start = position >> 3;
            if (buffer == null || start + 9 <= buffer.Length)
            {
                return true;
            }
            // The game's own check: throws when the packet is too short, like it does without the fix.
            __instance.EnsureBitsExist(bits);
            buffer = _buffer(__instance);   // the check makes the array bigger while writing
            if (start >= buffer.Length)
            {
                return true;
            }
            __result = Read(buffer, buffer.Length, position, bits);
            _position(__instance) = position + bits;
            Count++;
            return false;
        }

        // What ReadBitsWithoutChecks returns, reading only bytes before length. Bytes from length on
        // count as 0: the game reads whatever is in memory there, then masks those bits off.
        internal static ulong Read(byte[] buffer, int length, int position, byte bits)
        {
            int start = position >> 3;
            int shift = position & 7;
            int count = Math.Min(8, length - start);
            ulong word = 0;
            for (int i = 0; i < count; i++)
            {
                word |= (ulong)buffer[start + i] << (i * 8);
            }
            ulong value = word >> shift;
            if (bits + shift - 64 > 0 && start + 8 < length)
            {
                value |= (ulong)buffer[start + 8] << (64 - shift);
            }
            ulong mask = bits == 64 ? ulong.MaxValue : (1UL << bits) - 1;
            return value & mask;
        }

        // Compares Read with the game's own ReadBitsWithoutChecks for every start bit and bit count, in
        // an array long enough that the game's read stays inside it. Read is also given only the bytes
        // the value needs, as at the very end of a packet.
        private static bool MatchesGame(out string problem)
        {
            var random = new Random(1234);
            var data = new byte[48];
            random.NextBytes(data);
            var packer = new BitPacker(0);
            packer.MakeWrapper(new ByteData(data, 0, data.Length));
            for (int position = 0; (position >> 3) + 9 <= data.Length; position++)
            {
                for (int bits = 0; bits <= 64; bits++)
                {
                    packer.SetBitPosition(position);
                    ulong game = packer.ReadBitsWithoutChecks((byte)bits);
                    int needed = (position + bits + 7) >> 3;
                    if (Read(data, data.Length, position, (byte)bits) != game ||
                        Read(data, needed, position, (byte)bits) != game)
                    {
                        problem = $"{bits} bits from bit {position}";
                        return false;
                    }
                }
            }
            problem = null;
            return true;
        }

        // After patching: reads right at the end of a 5-byte packet, through the game's own ReadBits
        // and ReadBytes, should both come through the fix and give the right values.
        private static void CheckPatched()
        {
            bool wasOn = On;
            On = true;
            try
            {
                var data = new byte[] { 0xA5, 0x3C, 0xF0, 0x0F, 0x99 };
                var packer = new BitPacker(0);
                packer.MakeWrapper(new ByteData(data, 0, data.Length));
                packer.ResetMode(true);

                long before = Count;
                packer.SetBitPosition(3);
                ulong value = packer.ReadBits(29);
                bool readBits = Count > before && value == Read(data, data.Length, 3, 29);

                // Not on a whole byte, so ReadBytes calls ReadBitsWithoutChecks once for each byte.
                before = Count;
                packer.SetBitPosition(5);
                var bytes = new byte[4];
                packer.ReadBytes(bytes);
                bool readBytes = Count >= before + bytes.Length;
                for (int i = 0; i < bytes.Length; i++)
                {
                    readBytes &= bytes[i] == (byte)Read(data, data.Length, 5 + 8 * i, 8);
                }

                if (!readBits)
                {
                    Plugin.Log.LogWarning("Checked the fix: ReadBits doesn't go through it on this system, so the " +
                        "crash can still happen.");
                }
                if (!readBytes)
                {
                    Plugin.Log.LogWarning("Checked the fix: ReadBytes doesn't go through it on this system. ReadBits " +
                        "(where the crashes happened) " + (readBits ? "does." : "doesn't either."));
                }
                if (readBits && readBytes)
                {
                    Plugin.Log.LogInfo("Checked the fix: the game's ReadBits and ReadBytes both go through it and " +
                        "read the same values as before.");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Couldn't check the fix after patching: {e}");
            }
            finally
            {
                On = wasOn;
                Count = 0;
            }
        }
    }
}
