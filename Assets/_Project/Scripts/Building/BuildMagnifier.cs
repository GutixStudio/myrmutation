using UnityEngine;
using UnityEngine.UI;

namespace Myrmutation.Building
{
    /// <summary>
    /// Lupa del pincel en táctil (§5), como la de iPhone al seleccionar texto: mientras un dedo pinta o borra, una burbuja
    /// circular por encima del dedo (o a un lado, cerca del borde de arriba) muestra ampliada la zona bajo el dedo, con el
    /// círculo del pincel en el centro. Una segunda cámara ortográfica renderiza a una RenderTexture pequeña solo mientras
    /// se ve. Todo se crea una vez al arrancar, colgado del canvas (Screen Space Overlay). Lo usa BuildModeController.
    /// </summary>
    public class BuildMagnifier : MonoBehaviour
    {
        [Tooltip("Canvas (Screen Space Overlay) donde va la burbuja. Si se deja vacío, el del padre")]
        [SerializeField] private Canvas canvas;
        [Tooltip("Cámara del juego. Si se deja vacío, usa Camera.main")]
        [SerializeField] private Camera cam;
        [Tooltip("Diámetro de la burbuja, en unidades del canvas")]
        [SerializeField] private float diameter = 140f;
        [Tooltip("Distancia del dedo al centro de la burbuja, en unidades del canvas")]
        [SerializeField] private float offset = 100f;
        [Tooltip("Aumento de la lupa")]
        [SerializeField] private float zoom = 2f;
        [Tooltip("Lado de la RenderTexture, en píxeles")]
        [SerializeField] private int textureSize = 256;
        [Tooltip("Grosor del borde blanco, en unidades del canvas")]
        [SerializeField] private float borderWidth = 2f;
        [SerializeField] private Color borderColor = Color.white;
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.35f);
        [Tooltip("Desplazamiento de la sombra, en unidades del canvas")]
        [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -4f);

        private Camera loupeCam;
        private RenderTexture target;
        private RectTransform bubble;
        private Texture2D circleTexture;
        private Sprite circle;

        /// <summary>
        /// Centro de la burbuja en píxeles de pantalla: 'offset' por encima del dedo; si no cabe arriba, a un lado
        /// (a la derecha si cabe, si no a la izquierda) a la altura del dedo, sin salirse por arriba ni por abajo.
        /// </summary>
        public static Vector2 BubbleCenter(Vector2 finger, float radius, float offset, Vector2 screen)
        {
            if (finger.y + offset + radius <= screen.y) return finger + Vector2.up * offset;
            float side = finger.x + offset + radius <= screen.x ? 1f : -1f;
            return new Vector2(finger.x + side * offset, Mathf.Clamp(finger.y, radius, Mathf.Max(radius, screen.y - radius)));
        }

        private void Awake()
        {
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            target = new RenderTexture(textureSize, textureSize, 0) { name = "Lupa" };
            circle = BuildCircle(128);

            // Sombra, borde blanco y, dentro, la máscara circular con la vista de la cámara.
            bubble = new GameObject("Lupa", typeof(RectTransform)).GetComponent<RectTransform>();
            bubble.SetParent(canvas.transform, false);
            bubble.sizeDelta = Vector2.one * diameter;
            NewImage("Sombra", bubble, shadowColor).rectTransform.anchoredPosition = shadowOffset;
            NewImage("Borde", bubble, borderColor);
            var mask = NewImage("Máscara", bubble, Color.white);
            mask.rectTransform.sizeDelta = Vector2.one * -2f * borderWidth;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var view = new GameObject("Vista", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            view.transform.SetParent(mask.transform, false);
            Stretch(view.rectTransform);
            view.texture = target;
            view.raycastTarget = false;
            bubble.gameObject.SetActive(false);

            loupeCam = new GameObject("Cámara lupa").AddComponent<Camera>();
            loupeCam.transform.SetParent(transform, false);
            loupeCam.enabled = false;
        }

        private void OnDestroy()
        {
            if (target != null) target.Release();
            Destroy(target);
            Destroy(circle);
            Destroy(circleTexture);
        }

        /// <summary>Muestra la lupa sobre el dedo en 'screen' (píxeles), ampliando la zona alrededor de 'world'.</summary>
        public void Show(Vector2 screen, Vector3 world)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            if (!loupeCam.enabled)
            {
                loupeCam.CopyFrom(cam);
                loupeCam.targetTexture = target;
                loupeCam.depth = cam.depth - 1f;
                loupeCam.enabled = true;
                bubble.gameObject.SetActive(true);
                bubble.SetAsLastSibling();   // por encima del resto de la UI
            }
            float scale = canvas.scaleFactor, radiusPx = diameter * 0.5f * scale;
            // La burbuja ocupa 2·radiusPx píxeles y enseña 2·radiusPx / zoom píxeles de la cámara del juego.
            loupeCam.orthographicSize = cam.orthographicSize * 2f * radiusPx / (zoom * Screen.height);
            loupeCam.transform.position = new Vector3(world.x, world.y, cam.transform.position.z);
            bubble.position = BubbleCenter(screen, radiusPx, offset * scale, new Vector2(Screen.width, Screen.height));
        }

        public void Hide()
        {
            if (!loupeCam.enabled) return;
            loupeCam.enabled = false;
            bubble.gameObject.SetActive(false);
        }

        // Círculo blanco con el borde suavizado, para la burbuja, la sombra y la máscara.
        private Sprite BuildCircle(int size)
        {
            circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255f));
                }
            circleTexture.SetPixels32(pixels);
            circleTexture.Apply(false, true);
            return Sprite.Create(circleTexture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }

        private Image NewImage(string name, Transform parent, Color color)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.sprite = circle;
            img.color = color;
            img.raycastTarget = false;
            Stretch(img.rectTransform);
            return img;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
        }
    }
}
