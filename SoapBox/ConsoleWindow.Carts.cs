using System;
using System.IO;
using UnityEngine;

namespace SoapBox
{
    // Carts for the window: which one is open, running it, saving it, and the cart list. Your carts
    // live in your folder (Settings.Folder, Documents/On-Together/Soap Box by default); the ones that
    // come with Soap Box sit beside the DLL and are never changed: the first change to one saves a
    // copy in your folder, and you carry on with the copy. Saving happens by itself (a while after a
    // change, and when you run the cart, go back to the game, switch carts or close the window).
    internal sealed partial class ConsoleWindow
    {
        private const string BundledCart = "bubble-pop.soap";
        private const float AutoSaveSeconds = 20f;

        private Cart _cart;              // the cart open in the window (the runner plays a copy)
        private string _cartPath;
        private bool _cartBundled;
        private Cart _editCart;          // the cart whose code the editor holds
        private Runner _runner;
        private string _loadError;
        private readonly Shelf _shelf = new Shelf();
        private float _saveAt = -1f;
        private float _copyFailedAt = -100f;

        private string CartTitle()
        {
            return _cart == null ? "no cart" : string.IsNullOrWhiteSpace(_cart.Name) ? "cart" : _cart.Name;
        }

        // The cart open last time, or the bundled bubble pop.
        private void LoadFirstCart()
        {
            string last = S.LastCart.Value;
            if (!string.IsNullOrEmpty(last) && File.Exists(last) && OpenCart(last, IsBundledPath(last)))
            {
                return;
            }
            OpenCart(Path.Combine(Plugin.ModFolder ?? "", BundledCart), true);
        }

        private static bool IsBundledPath(string path)
        {
            try
            {
                return Shelf.SamePath(Path.GetDirectoryName(path), Plugin.ModFolder);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool OpenCart(string path, bool bundled)
        {
            SaveNow();
            try
            {
                Cart cart = CartFiles.Load(path);
                if (string.IsNullOrWhiteSpace(cart.Name))
                {
                    cart.Name = Path.GetFileNameWithoutExtension(path);
                }
                _cart = cart;
                _cartPath = path;
                _cartBundled = bundled;
                S.LastCart.Value = path;
                if (_mode == Mode.Edit)
                {
                    Edit.Code.Load(cart.Code);
                    _editCart = cart;
                }
                StartRunner();
                Plugin.Log.LogInfo($"Opened cart {CartTitle()} ({path}).");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Couldn't open {path}: {e}");
                string message = "couldn't open " + Path.GetFileName(path) + ": " + e.Message;
                if (_cart == null)
                {
                    _loadError = message;
                    _runner = null;
                }
                else if (_mode == Mode.Edit)
                {
                    Edit.Say(message, true, Time.unscaledTimeAsDouble, 8.0);
                }
                return false;
            }
        }

        // Runs the open cart from the start (a copy of it, so the running cart can change its
        // sprites and map without touching the one you edit).
        private void StartRunner()
        {
            _loadError = null;
            SyncCode();
            _runner = _cart == null ? null : new Runner(_cart.Clone(), message => Plugin.Log.LogInfo(message));
        }

        // The editor's text into the open cart.
        private void SyncCode()
        {
            if (_edit != null && _cart != null && ReferenceEquals(_editCart, _cart))
            {
                _cart.Code = _edit.Code.Text;
            }
        }

        // Saves the open cart if it's yours and the code has changed (or always, when asked).
        private void SaveNow(bool asked = false)
        {
            _saveAt = -1f;
            if (_cart == null || _cartBundled)
            {
                return;
            }
            bool changed = _edit != null && ReferenceEquals(_editCart, _cart) && _edit.Code.Changed;
            if (!changed && !asked)
            {
                return;
            }
            SyncCode();
            try
            {
                CartFiles.Save(_cart, _cartPath);
                _edit?.Code.MarkSaved();
                if (asked)
                {
                    Edit.Say("saved " + CartTitle(), false, Time.unscaledTimeAsDouble);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Couldn't save {_cartPath}: {e}");
                Edit.Say("couldn't save: " + e.Message, true, Time.unscaledTimeAsDouble, 8.0);
            }
        }

        // The first change to a cart that comes with Soap Box: carry on with a copy in your folder.
        private void MakeYours(bool asked)
        {
            if (!_cartBundled || _cart == null || (!asked && Time.unscaledTime - _copyFailedAt < 30f))
            {
                return;
            }
            SyncCode();
            string path = CartFiles.FreePath(S.Folder(), _cart.Name);
            try
            {
                CartFiles.Save(_cart, path);
                _cartPath = path;
                _cartBundled = false;
                S.LastCart.Value = path;
                Edit.Code.MarkSaved();
                Edit.Say("saved a copy in your carts, so this one's yours to change", false, Time.unscaledTimeAsDouble, 6.0);
                Plugin.Log.LogInfo($"Copied {CartTitle()} to {path}.");
            }
            catch (Exception e)
            {
                _copyFailedAt = Time.unscaledTime;
                Plugin.Log.LogError($"Couldn't save a copy at {path}: {e}");
                Edit.Say("couldn't save a copy: " + e.Message, true, Time.unscaledTimeAsDouble, 8.0);
            }
        }

        private void ScheduleSave()
        {
            if (_saveAt < 0f)
            {
                _saveAt = Time.unscaledTime + AutoSaveSeconds;
            }
        }

        private void AutoSave()
        {
            if (_saveAt >= 0f && Time.unscaledTime >= _saveAt)
            {
                SaveNow();
            }
        }

        // ---- The cart list ----

        private void OpenShelf()
        {
            SaveNow();
            string folder = S.Folder();
            _shelf.Fill(CartFiles.List(folder, false), CartFiles.List(Plugin.ModFolder, true), _cartPath,
                _mode == Mode.Edit && _cart != null && !_cartBundled);
            if (_mode == Mode.Play)
            {
                _card = Card.Shelf;
            }
            else
            {
                _editShelf = true;
            }
        }

        private void CloseShelf()
        {
            if (_card == Card.Shelf)
            {
                _card = Card.None;
            }
            _editShelf = false;
            _shelf.Renaming = false;
            _shelf.Version++;
        }

        private void ActivateShelf()
        {
            Shelf.Item item = _shelf.Current;
            if (item == null)
            {
                return;
            }
            switch (item.Kind)
            {
                case Shelf.Kind.NewCart:
                    NewCart();
                    break;
                case Shelf.Kind.Cart:
                    CloseShelf();
                    if (!Shelf.SamePath(item.Entry.Path, _cartPath))
                    {
                        OpenCart(item.Entry.Path, item.Entry.Bundled);
                    }
                    break;
                case Shelf.Kind.Rename:
                    _shelf.Renaming = true;
                    _shelf.Name.Set(CartTitle());
                    _shelf.Version++;
                    break;
                case Shelf.Kind.Folder:
                    OpenFolder();
                    break;
            }
        }

        private void NewCart()
        {
            SaveNow();
            string folder = S.Folder();
            var cart = new Cart
            {
                Name = CartFiles.FreeName(CartFiles.List(folder, false), "my cart"),
                Code = Cart.StarterCode,
            };
            string path = CartFiles.FreePath(folder, cart.Name);
            try
            {
                CartFiles.Save(cart, path);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Couldn't make a new cart at {path}: {e}");
                if (_mode == Mode.Edit)
                {
                    Edit.Say("couldn't make a new cart: " + e.Message, true, Time.unscaledTimeAsDouble, 8.0);
                }
                return;
            }
            CloseShelf();
            if (OpenCart(path, false))
            {
                EnterEdit();
                Edit.Say("a new cart! ctrl+r runs it", false, Time.unscaledTimeAsDouble, 6.0);
            }
        }

        private void FinishRename()
        {
            string name = _shelf.Name.Text.Trim();
            CloseShelf();
            if (name.Length == 0 || _cart == null || _cartBundled || name == _cart.Name)
            {
                return;
            }
            SyncCode();
            string old = _cartPath;
            string folder = Path.GetDirectoryName(old) ?? S.Folder();
            // Same file name (only capitals changed, say): keep the file.
            string path = string.Equals(CartFiles.SafeFileName(name), Path.GetFileNameWithoutExtension(old), StringComparison.OrdinalIgnoreCase)
                ? old
                : CartFiles.FreePath(folder, name);
            string was = _cart.Name;
            _cart.Name = name;
            try
            {
                CartFiles.Save(_cart, path);
                if (!Shelf.SamePath(path, old) && File.Exists(old))
                {
                    File.Delete(old);
                }
                _cartPath = path;
                S.LastCart.Value = path;
                Edit.Code.MarkSaved();
                Edit.Say("renamed to " + name, false, Time.unscaledTimeAsDouble);
            }
            catch (Exception e)
            {
                _cart.Name = was;
                Plugin.Log.LogError($"Couldn't rename {old} to {path}: {e}");
                Edit.Say("couldn't rename: " + e.Message, true, Time.unscaledTimeAsDouble, 8.0);
            }
        }

        // Opens your carts folder in Explorer (or Finder on a Mac) through Unity, so the mod itself
        // never starts a program: Thunderstore reviews new packages for code that does.
        private void OpenFolder()
        {
            string folder = S.Folder();
            try
            {
                Directory.CreateDirectory(folder);
                Application.OpenURL(new Uri(folder).AbsoluteUri);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        }
    }
}
