using UnityEngine;
using UnityEngine.UI;

namespace TraceOverlay
{
    // The see-through picture laid over a board. It's a small piece of world-space game UI placed
    // a few millimetres in front of the board, so it looks right from any angle and in every window
    // mode. It only exists on your computer, has nothing to click, and the board's own click
    // detection (a physics raycast in QuadPainterGPU.Update) goes straight through it.
    //
    // The board is mapped with the same texture coordinates the game paints with, so a spot on the
    // picture lines up with the square your chalk lands on. See reference/Assembly-CSharp/QuadPainterGPU.cs.
    internal sealed class BoardGuide
    {
        // Canvas units per metre. UI sizes are in canvas units, so this keeps them precise.
        private const float UnitsPerMetre = 1000f;

        // How far in front of the board the picture floats, in metres.
        private const float Lift = 0.005f;

        private GameObject _root;
        private RectTransform _imageRect;
        private RawImage _image;
        private QuadPainterGPU _board;
        private Vector2 _boardSize;

        public bool HasBoard => _board != null && _root != null;

        // Places the guide over this board. cameraPosition is where the drawing camera sits,
        // so the guide floats on the side you look at.
        public void Attach(QuadPainterGPU board, Vector3 cameraPosition)
        {
            if (_root == null)
            {
                Create();
            }
            _board = board;

            if (!Measure(board, out Vector3 center, out Vector3 across, out Vector3 up))
            {
                Plugin.Log.LogWarning("Couldn't read the board's shape, so the guide may not line up. Board: " + board.name);
            }
            _boardSize = new Vector2(across.magnitude, up.magnitude);

            // UI is seen from the front when its forward points away from you. Right follows the
            // board's texture across, and up follows the texture up, so the picture lines up with the
            // painting grid even if the board is turned or mirrored.
            Vector3 forward = Vector3.Cross(across, up).normalized;
            if (forward.sqrMagnitude < 0.5f)
            {
                forward = board.transform.forward;
            }
            Vector3 toCamera = (cameraPosition - center).normalized;

            var rect = (RectTransform)_root.transform;
            rect.SetPositionAndRotation(center + toCamera * Lift, Quaternion.LookRotation(forward, up.normalized));
            rect.localScale = Vector3.one / UnitsPerMetre;
            rect.sizeDelta = _boardSize * UnitsPerMetre;
            _root.layer = board.gameObject.layer;
            _imageRect.gameObject.layer = board.gameObject.layer;
        }

        public void Show(Texture2D texture, float opacity, bool fillBoard)
        {
            if (!HasBoard || texture == null)
            {
                Hide();
                return;
            }

            _image.texture = texture;
            _image.color = new Color(1f, 1f, 1f, opacity);

            float boardAspect = _boardSize.y > 0f ? _boardSize.x / _boardSize.y : 16f / 9f;
            float imageAspect = (float)texture.width / texture.height;
            Vector2 board = _boardSize * UnitsPerMetre;
            if (fillBoard)
            {
                // Cover the board and crop the image's overhanging sides.
                _imageRect.sizeDelta = board;
                _image.uvRect = imageAspect > boardAspect
                    ? Centered(boardAspect / imageAspect, 1f)
                    : Centered(1f, imageAspect / boardAspect);
            }
            else
            {
                // Show the whole image, as big as fits.
                _imageRect.sizeDelta = imageAspect > boardAspect
                    ? new Vector2(board.x, board.x / imageAspect)
                    : new Vector2(board.y * imageAspect, board.y);
                _image.uvRect = new Rect(0f, 0f, 1f, 1f);
            }

            if (!_root.activeSelf)
            {
                _root.SetActive(true);
            }
        }

        public void Hide()
        {
            if (_root != null && _root.activeSelf)
            {
                _root.SetActive(false);
            }
        }

        public void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
            }
            _root = null;
            _board = null;
        }

        private void Create()
        {
            _root = new GameObject("TraceOverlay Guide", typeof(RectTransform));
            Object.DontDestroyOnLoad(_root);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // No GraphicRaycaster: the game's UI checks never see this canvas.

            var imageObject = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageObject.transform.SetParent(_root.transform, false);
            _imageRect = (RectTransform)imageObject.transform;
            _imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            _imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            _imageRect.pivot = new Vector2(0.5f, 0.5f);
            _imageRect.anchoredPosition = Vector2.zero;
            _image = imageObject.GetComponent<RawImage>();
            _image.raycastTarget = false;
            _root.SetActive(false);
        }

        private static Rect Centered(float width, float height)
        {
            return new Rect((1f - width) / 2f, (1f - height) / 2f, width, height);
        }

        // Works out, in world space, the middle of the board and the vectors along its painting
        // texture: across (u from 0 to 1) and up (v from 0 to 1). Falls back to treating the board as
        // a standard Unity quad if its mesh can't be read.
        private static bool Measure(QuadPainterGPU board, out Vector3 center, out Vector3 across, out Vector3 up)
        {
            Transform t = board.transform;
            Mesh mesh = MeshOf(board);
            if (mesh != null && mesh.isReadable)
            {
                Vector3[] vertices = mesh.vertices;
                Vector2[] uvs = mesh.uv;
                int[] triangles = mesh.triangles;
                for (int i = 0; i + 2 < triangles.Length && uvs.Length == vertices.Length; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    Vector3 e1 = vertices[b] - vertices[a];
                    Vector3 e2 = vertices[c] - vertices[a];
                    Vector2 d1 = uvs[b] - uvs[a];
                    Vector2 d2 = uvs[c] - uvs[a];
                    float det = d1.x * d2.y - d2.x * d1.y;
                    if (Mathf.Abs(det) < 1e-6f)
                    {
                        continue;
                    }
                    // How the surface moves as u and v change, in the mesh's own space.
                    Vector3 du = (e1 * d2.y - e2 * d1.y) / det;
                    Vector3 dv = (e2 * d1.x - e1 * d2.x) / det;
                    Vector3 middle = vertices[a] + du * (0.5f - uvs[a].x) + dv * (0.5f - uvs[a].y);

                    center = t.TransformPoint(middle);
                    across = t.TransformVector(du);
                    up = t.TransformVector(dv);
                    if (across.sqrMagnitude > 1e-8f && up.sqrMagnitude > 1e-8f)
                    {
                        return true;
                    }
                }
            }

            // A Unity quad: one unit square in X and Y, centred on the object.
            center = t.position;
            across = t.TransformVector(Vector3.right);
            up = t.TransformVector(Vector3.up);
            return false;
        }

        private static Mesh MeshOf(QuadPainterGPU board)
        {
            // The game's painting raycast needs a readable collider mesh, so try that one too.
            MeshFilter filter = board.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null && filter.sharedMesh.isReadable)
            {
                return filter.sharedMesh;
            }
            MeshCollider collider = board.GetComponent<MeshCollider>();
            return collider != null ? collider.sharedMesh : null;
        }
    }
}
