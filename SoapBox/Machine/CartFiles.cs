using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SoapBox
{
    // Cart files on disk: your carts (a folder in Documents) and the ones that come with Soap Box
    // (beside the DLL). No Unity, so the test program can use it too.
    internal static class CartFiles
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        public sealed class Entry
        {
            public string Name;
            public string Path;
            public bool Bundled;
        }

        public static Cart Load(string path)
        {
            return Cart.Parse(File.ReadAllText(path, Encoding.UTF8));
        }

        // Written beside the old file and swapped in, so quitting halfway can't leave half a cart.
        public static void Save(Cart cart, string path)
        {
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }
            string temp = path + ".tmp";
            File.WriteAllText(temp, cart.Write(), Utf8);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(temp, path);
        }

        // The carts in a folder, by name. A file that can't be read is left out.
        public static List<Entry> List(string folder, bool bundled)
        {
            var found = new List<Entry>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return found;
            }
            foreach (string path in Directory.GetFiles(folder, "*" + Cart.Extension))
            {
                try
                {
                    string name = Cart.ReadName(File.ReadAllText(path, Encoding.UTF8));
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = System.IO.Path.GetFileNameWithoutExtension(path);
                    }
                    found.Add(new Entry { Name = name, Path = path, Bundled = bundled });
                }
                catch (Exception)
                {
                    // Not readable right now; it shows again next time.
                }
            }
            return found.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // A file for a cart called name in folder that isn't taken: "name.soap", "name 2.soap"...
        public static string FreePath(string folder, string name)
        {
            string stem = SafeFileName(name);
            string path = Path.Combine(folder, stem + Cart.Extension);
            for (int n = 2; File.Exists(path); n++)
            {
                path = Path.Combine(folder, stem + " " + n + Cart.Extension);
            }
            return path;
        }

        // A cart name no cart in the list has: "name", "name 2"...
        public static string FreeName(IEnumerable<Entry> carts, string name)
        {
            var taken = new HashSet<string>(carts.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
            string free = name;
            for (int n = 2; taken.Contains(free); n++)
            {
                free = name + " " + n;
            }
            return free;
        }

        // A cart name as a file name: letters the file system refuses become "-".
        public static string SafeFileName(string name)
        {
            var sb = new StringBuilder();
            char[] bad = Path.GetInvalidFileNameChars();
            foreach (char c in (name ?? "").Trim())
            {
                sb.Append(Array.IndexOf(bad, c) >= 0 || c < ' ' ? '-' : c);
            }
            string safe = sb.ToString().Trim().TrimEnd('.');
            return safe.Length == 0 ? "cart" : safe.Length > 60 ? safe.Substring(0, 60) : safe;
        }
    }
}
