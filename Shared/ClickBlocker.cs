using UnityEngine;
using UnityEngine.UI;

namespace OnTogetherMods.Shared
{
    // An invisible piece of game UI kept under a mod window. Mod windows are drawn with Unity's
    // immediate-mode GUI, which the game's UI can't see, so without this a click on the window
    // also clicks the game's buttons behind it and the scroll wheel zooms the camera.
    // (PlayerMovementController.Zoom skips while InputManager.MouseOverUIObject is set.)
    //
    // This only covers game UI. Clicks on the 3D world go through InputManager and need a patch
    // in each mod.
    internal sealed class ClickBlocker
    {
        private readonly GameObject _root;
        private readonly RectTransform _area;

        public ClickBlocker(string name)
        {
            _root = new GameObject(name);
            Object.DontDestroyOnLoad(_root);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            _root.AddComponent<GraphicRaycaster>();

            // A see-through image still catches clicks.
            var area = new GameObject("Area", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            area.transform.SetParent(_root.transform, false);
            area.GetComponent<Image>().color = Color.clear;
            _area = (RectTransform)area.transform;
            _area.anchorMin = Vector2.zero;
            _area.anchorMax = Vector2.zero;
            _area.pivot = Vector2.zero;
            _root.SetActive(false);
        }

        // screenRect is in screen pixels measured from the top-left, like OnGUI.
        public void Update(bool shown, Rect screenRect)
        {
            if (_root == null)
            {
                return;
            }
            if (shown)
            {
                // Game UI measures from the bottom-left.
                _area.anchoredPosition = new Vector2(screenRect.x, Screen.height - screenRect.yMax);
                _area.sizeDelta = screenRect.size;
            }
            if (_root.activeSelf != shown)
            {
                _root.SetActive(shown);
            }
        }

        public void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
            }
        }

        // True when the mouse is inside screenRect (screen pixels from the top-left).
        public static bool MouseIn(Rect screenRect)
        {
            Vector3 mouse = Input.mousePosition;
            return screenRect.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
        }
    }
}
