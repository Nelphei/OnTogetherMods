using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Cinemachine;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace PhotoMode
{
    // Free camera, UI hiding and screenshots. Everything here is local to this player:
    // your character stays where it is, and other players see nothing different.
    internal sealed class PhotoModeController : MonoBehaviour
    {
        // Read by the Harmony patches to pause the game's own input and camera control.
        internal static bool IsActive { get; private set; }

        private const float RefreshInterval = 0.5f;
        private const float ZoomStepDegrees = 3f;
        private const float MinFov = 10f;
        private const float MaxFov = 100f;

        private readonly UiHider _ui = new UiHider();

        private Camera _camera;
        private CinemachineBrain _brain;
        private bool _brainWasEnabled;
        private Transform _player;

        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private float _startFov;
        private float _yaw;
        private float _pitch;

        private bool _looking;
        private bool _capturing;
        private bool _exitRequested;
        private bool _showHelp;
        private float _refreshTimer;

        private string _toastText;
        private float _toastUntil;
        private volatile string _saveResult;
        private string _lastPhotoStamp;
        private int _photoStampCount;
        private GUIStyle _panelStyle;

        private static Settings S => Plugin.Settings;

        private void Update()
        {
            string saved = _saveResult;
            if (saved != null)
            {
                _saveResult = null;
                Toast(saved);
            }

            if (!IsActive)
            {
                if (Input.GetKeyDown(S.ToggleKey.Value))
                {
                    TryEnter();
                }
                return;
            }

            // The scene changed under us, e.g. after leaving the lobby.
            if (_camera == null)
            {
                Exit();
                return;
            }

            if (Input.GetKeyDown(S.ToggleKey.Value) || Input.GetKeyDown(KeyCode.Escape))
            {
                // Exit in LateUpdate, after the game's InputManager has run for this frame.
                // Otherwise it could see this same Escape press and open the pause menu.
                _exitRequested = true;
                return;
            }

            if (Input.GetKeyDown(S.ResetKey.Value))
            {
                _camera.fieldOfView = _startFov;
                SetCamera(_startPosition, _startRotation);
            }
            if (Input.GetKeyDown(S.NameTagsKey.Value))
            {
                _ui.SetNameTagsHidden(!_ui.NameTagsHidden);
                Toast(_ui.NameTagsHidden ? "Name tags hidden" : "Name tags shown");
            }
            if (Input.GetKeyDown(S.ChatKey.Value))
            {
                SetChatShown(!_ui.ChatShown, quiet: false);
            }
            if (Input.GetKeyDown(S.HelpKey.Value))
            {
                _showHelp = !_showHelp;
            }
            if (Input.GetKeyDown(S.CaptureKey.Value) && !_capturing)
            {
                StartCoroutine(Capture());
            }

            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = RefreshInterval;
                _ui.Refresh();
            }
        }

        private void LateUpdate()
        {
            if (!IsActive)
            {
                return;
            }
            if (_exitRequested || _camera == null)
            {
                Exit();
                return;
            }

            float dt = Time.unscaledDeltaTime;
            UpdateLook();
            UpdateMove(dt);
            UpdateZoom();
        }

        private void OnDestroy()
        {
            if (IsActive)
            {
                Exit();
            }
        }

        private void TryEnter()
        {
            TextChannelManager chat = NetworkSingleton<TextChannelManager>.I;
            if (chat == null || chat.MainPlayer == null || MonoSingleton<MainSceneManager>.I == null)
            {
                return; // Not in a lobby yet (main menu or loading). Stay quiet.
            }

            TaskManager tasks = MonoSingleton<TaskManager>.I;
            if (tasks != null && tasks.IsWriting)
            {
                return; // Typing in chat or a text field.
            }

            // Landscape, Portrait and Desk draw the world into a texture behind UI masks,
            // so hiding the UI would hide the world too. See OverlayManager.cs.
            OverlayManager overlay = MonoSingleton<OverlayManager>.I;
            if (overlay != null && overlay.ResolutionMode != ResolutionMode.Normal)
            {
                Toast("Photo mode only works in the normal window mode.");
                return;
            }

            // The swan ride moves the camera itself every frame. See SwanController.cs.
            SwanManager swans = NetworkSingleton<SwanManager>.I;
            if (swans != null && swans.IsPlayerOnSwan())
            {
                Toast("Photo mode isn't available on the swan.");
                return;
            }

            // The settings menu treats its canvas being on as "open". Hiding it would confuse it.
            SettingsController settings = MonoSingleton<SettingsController>.I;
            if (settings != null && settings.GetCanvas != null && settings.GetCanvas.enabled)
            {
                Toast("Close the settings menu first.");
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                Plugin.Log.LogWarning("No main camera found, so photo mode can't start.");
                return;
            }

            _camera = camera;
            _player = chat.MainPlayer;

            // Cinemachine moves the camera every frame. Pause it, the same way the swan ride does.
            _brain = camera.GetComponent<CinemachineBrain>();
            _brainWasEnabled = _brain != null && _brain.enabled;
            if (_brain != null)
            {
                _brain.enabled = false;
            }
            else
            {
                Plugin.Log.LogWarning("Main camera has no CinemachineBrain. The game may fight the free camera.");
            }

            _startPosition = camera.transform.position;
            _startRotation = camera.transform.rotation;
            _startFov = camera.fieldOfView;
            SetCamera(_startPosition, _startRotation);

            _showHelp = S.ShowHelp.Value;
            _exitRequested = false;
            _refreshTimer = RefreshInterval;
            IsActive = true;
            _ui.Hide(S.HideNameTags.Value);
            if (S.ShowChat.Value)
            {
                SetChatShown(true, quiet: true);
            }

            Plugin.Log.LogInfo("Photo mode on.");
        }

        private void SetChatShown(bool shown, bool quiet)
        {
            // When chat is minimised in the game, its objects are switched off, so showing
            // them would show nothing. Say so instead of silently doing nothing.
            UIManager ui = MonoSingleton<UIManager>.I;
            if (shown && ui != null && ui.IsMessagePanelHidden)
            {
                if (!quiet)
                {
                    Toast("Chat is minimised. Open it in the game first, then start photo mode.");
                }
                return;
            }

            if (!_ui.SetChatShown(shown))
            {
                Toast("Couldn't find the chat box. See the BepInEx log.");
                return;
            }
            if (!quiet)
            {
                Toast(shown ? "Chat shown" : "Chat hidden");
            }
        }

        private void Exit()
        {
            IsActive = false;
            _exitRequested = false;
            _ui.RestoreAll();

            if (_camera != null)
            {
                _camera.transform.SetPositionAndRotation(_startPosition, _startRotation);
                _camera.fieldOfView = _startFov;
            }
            if (_brain != null)
            {
                _brain.enabled = _brainWasEnabled;
            }

            StopLooking();
            _camera = null;
            _brain = null;
            _player = null;

            Plugin.Log.LogInfo("Photo mode off.");
        }

        private void UpdateLook()
        {
            bool held = Input.GetMouseButton(1);
            if (held && !_looking)
            {
                _looking = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (!held && _looking)
            {
                StopLooking();
            }

            if (_looking)
            {
                // Turn more slowly when zoomed in, so framing a close-up stays precise.
                float sensitivity = S.LookSensitivity.Value * (_camera.fieldOfView / 60f);
                _yaw += Input.GetAxisRaw("Mouse X") * sensitivity;
                _pitch -= Input.GetAxisRaw("Mouse Y") * sensitivity;
                _pitch = Mathf.Clamp(_pitch, -89f, 89f);
            }

            _camera.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void StopLooking()
        {
            _looking = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void UpdateMove(float dt)
        {
            Vector3 input = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) input.z += 1f;
            if (Input.GetKey(KeyCode.S)) input.z -= 1f;
            if (Input.GetKey(KeyCode.D)) input.x += 1f;
            if (Input.GetKey(KeyCode.A)) input.x -= 1f;
            if (Input.GetKey(KeyCode.E)) input.y += 1f;
            if (Input.GetKey(KeyCode.Q)) input.y -= 1f;
            if (input == Vector3.zero)
            {
                return;
            }

            float speed = S.MoveSpeed.Value;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                speed *= S.FastMultiplier.Value;
            }
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                speed *= S.SlowMultiplier.Value;
            }

            // W/S fly where the camera points; Q/E always go straight down/up.
            Transform t = _camera.transform;
            Vector3 direction = t.forward * input.z + t.right * input.x + Vector3.up * input.y;
            Vector3 position = t.position + Vector3.ClampMagnitude(direction, 1f) * speed * dt;

            float maxDistance = S.MaxDistance.Value;
            if (maxDistance > 0f && _player != null)
            {
                Vector3 offset = position - _player.position;
                if (offset.magnitude > maxDistance)
                {
                    position = _player.position + offset.normalized * maxDistance;
                }
            }

            t.position = position;
        }

        private void UpdateZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f && !_camera.orthographic)
            {
                _camera.fieldOfView = Mathf.Clamp(_camera.fieldOfView - scroll * ZoomStepDegrees, MinFov, MaxFov);
            }
        }

        private void SetCamera(Vector3 position, Quaternion rotation)
        {
            _camera.transform.SetPositionAndRotation(position, rotation);
            Vector3 euler = rotation.eulerAngles;
            _yaw = euler.y;
            _pitch = Mathf.Clamp(euler.x > 180f ? euler.x - 360f : euler.x, -89f, 89f);
        }

        private IEnumerator Capture()
        {
            // While _capturing is set, OnGUI draws nothing, so the help panel and toasts
            // stay out of the photo. Also catch any UI that popped up since the last refresh.
            _capturing = true;
            _ui.Refresh();

            // Wait until this frame has finished drawing, then grab it.
            yield return new WaitForEndOfFrame();

            Texture2D shot = null;
            try
            {
                shot = ScreenCapture.CaptureScreenshotAsTexture(Mathf.Clamp(S.ResolutionMultiplier.Value, 1, 4));
                int width = shot.width;
                int height = shot.height;
                Color32[] pixels = shot.GetPixels32();
                string path = NextPhotoPath();

                // Encoding a big PNG takes a moment, so do it off the main thread to avoid a hitch.
                Task.Run(() => SavePhoto(path, pixels, width, height));
                PlayShutter();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Couldn't take a photo: {e}");
                Toast("Couldn't take the photo. See the BepInEx log.");
            }
            finally
            {
                if (shot != null)
                {
                    Destroy(shot);
                }
                _capturing = false;
            }
        }

        // Runs on a background thread. Unity APIs are off limits here except EncodeArrayToPNG,
        // which is thread safe. The result is handed back to Update through _saveResult.
        private void SavePhoto(string path, Color32[] pixels, int width, int height)
        {
            try
            {
                // Keep RGB only. Some render setups leave alpha below 1, which would make
                // parts of the PNG see-through.
                byte[] rgb = new byte[pixels.Length * 3];
                for (int i = 0, j = 0; i < pixels.Length; i++, j += 3)
                {
                    rgb[j] = pixels[i].r;
                    rgb[j + 1] = pixels[i].g;
                    rgb[j + 2] = pixels[i].b;
                }
                byte[] png = ImageConversion.EncodeArrayToPNG(rgb, GraphicsFormat.R8G8B8_SRGB, (uint)width, (uint)height);
                File.WriteAllBytes(path, png);

                Plugin.Log.LogInfo($"Saved {width}x{height} photo to {path}");
                _saveResult = $"Saved {Path.GetFileName(path)}";
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Couldn't save photo to {path}: {e}");
                _saveResult = "Couldn't save the photo. See the BepInEx log.";
            }
        }

        private string NextPhotoPath()
        {
            string folder = PhotoFolder();
            Directory.CreateDirectory(folder);

            // Several photos in the same second get _2, _3... The previous one may still be
            // writing on another thread, so count rather than relying on File.Exists alone.
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _photoStampCount = stamp == _lastPhotoStamp ? _photoStampCount + 1 : 1;
            _lastPhotoStamp = stamp;

            while (true)
            {
                string suffix = _photoStampCount > 1 ? $"_{_photoStampCount}" : "";
                string path = Path.Combine(folder, $"OnTogether_{stamp}{suffix}.png");
                if (!File.Exists(path))
                {
                    return path;
                }
                _photoStampCount++;
            }
        }

        private static string PhotoFolder()
        {
            string custom = S.SaveFolder.Value?.Trim();
            if (!string.IsNullOrEmpty(custom))
            {
                return Environment.ExpandEnvironmentVariables(custom);
            }
            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (string.IsNullOrEmpty(pictures))
            {
                pictures = Application.persistentDataPath;
            }
            return Path.Combine(pictures, "On-Together");
        }

        private static void PlayShutter()
        {
            if (!S.ShutterSound.Value)
            {
                return;
            }
            SFXManager sfx = MonoSingleton<SFXManager>.I;
            if (sfx != null)
            {
                sfx.PlayUIClick();
            }
        }

        private void Toast(string text)
        {
            _toastText = text;
            _toastUntil = Time.unscaledTime + 3f;
        }

        private void OnGUI()
        {
            if (_capturing)
            {
                return;
            }

            bool help = IsActive && _showHelp;
            bool toast = _toastText != null && Time.unscaledTime < _toastUntil;
            if (!help && !toast)
            {
                return;
            }

            if (_panelStyle == null)
            {
                _panelStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 15,
                    richText = true,
                    wordWrap = false,
                    padding = new RectOffset(12, 12, 10, 10),
                };
                _panelStyle.normal.textColor = Color.white;
            }

            // Lay out at 1080p and scale to the real screen, so text isn't tiny on 4K.
            float scale = Screen.height / 1080f;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;

            if (help)
            {
                string text =
                    "<b>PHOTO MODE</b>\n" +
                    "Hold right mouse: look      WASD: move      Q / E: down / up\n" +
                    "Shift: faster      Ctrl: slower      Scroll: zoom\n" +
                    $"{S.CaptureKey.Value}: take photo      {S.NameTagsKey.Value}: name tags      " +
                    $"{S.ChatKey.Value}: chat      {S.ResetKey.Value}: reset camera\n" +
                    $"{S.HelpKey.Value}: hide this panel      {S.ToggleKey.Value} or Esc: exit";
                Vector2 size = _panelStyle.CalcSize(new GUIContent(text));
                GUI.Box(new Rect(20f, 1080f - size.y - 20f, size.x, size.y), text, _panelStyle);
            }

            if (toast)
            {
                Vector2 size = _panelStyle.CalcSize(new GUIContent(_toastText));
                GUI.Box(new Rect((width - size.x) / 2f, 40f, size.x, size.y), _toastText, _panelStyle);
            }

            GUI.matrix = previous;
        }
    }
}
