using System;
using System.Collections.Generic;
using Myrmutation.Building;
using Myrmutation.Core;
using Myrmutation.Economy;
using TMPro;
using UnityEngine;

namespace Myrmutation.UI
{
    /// <summary>Problema que enseña el icono de una sala, de más a menos importante.</summary>
    public enum RoomAlert { None, SinAcceso, SinObreras, SinHojas, SinHongo, Llena }

    /// <summary>
    /// E14 · Estado de la sala sobre la sala. Uno por escena: dibuja un icono encima de cada sala construida
    /// (Room.All) y lo mantiene al día.
    ///
    ///  · Como mucho UN icono por sala, y SOLO si hay problema. Prioridad:
    ///    Sin acceso > Sin obreras > Sin hojas o hongo > Llena.
    ///    · Sin acceso: el WorkPoint de la sala no está conectado con la colonia (GridNavigator.IsReachable).
    ///    · Sin obreras / Sin hojas / Sin hongo / Llena: ColonyProduction.GetStatus (D8).
    ///  · Tocar el icono abre el panel de sala (E13). El toque lo atiende RoomPanelUI, que pregunta antes a
    ///    TryGetRoomAt: así el icono gana a la sala y a las hormigas aunque sobresalga de la huella.
    ///  · Tamaño constante en pantalla, como la burbuja de las hormigas (B7). Los problemas laten; "Llena" no,
    ///    porque no es grave.
    ///
    /// Va en el prefab RoomPanel (junto a RoomPanelUI), así que no hay que tocar ninguna escena.
    /// Iconos: placeholder (letra sobre círculo de color) hasta F12; asigna 'sprite' en la tabla para usar arte.
    /// </summary>
    public class RoomStatusIcon : MonoBehaviour
    {
        [Serializable]
        public class AlertIcon
        {
            public RoomAlert alert;
            [Tooltip("Icono definitivo (F12). Vacío = se usa la letra")]
            public Sprite sprite;
            [Tooltip("Placeholder hasta F12")]
            public string letter;
            [Tooltip("Color del círculo de fondo")]
            public Color color;

            public AlertIcon(RoomAlert alert, string letter, Color color)
            {
                this.alert = alert;
                this.letter = letter;
                this.color = color;
            }
        }

        public static RoomStatusIcon Instance { get; private set; }

        [Header("Qué se avisa")]
        [Tooltip("Enseñar también \"Llena\" (la de menos prioridad)")]
        [SerializeField] private bool showFull = true;
        [Tooltip("Segundos entre comprobaciones (tiempo real: funciona en pausa)")]
        [SerializeField] private float pollInterval = 0.5f;

        [Header("Tamaño y posición")]
        [Tooltip("Diámetro del icono (unidades del mundo) con el zoom de referencia")]
        [SerializeField] private float diameter = 0.6f;
        [Tooltip("Zoom (tamaño ortográfico) en el que el icono mide 'diameter'. Con otro zoom se escala para verse igual en pantalla")]
        [SerializeField] private float referenceZoom = 5f;
        [Tooltip("Hueco entre el techo de la sala y el icono (unidades del mundo)")]
        [SerializeField] private float topMargin = 0.15f;
        [Tooltip("Diámetro mínimo de la zona que se puede tocar, con el zoom de referencia (0,85 ≈ 88 px a 1080 p)")]
        [SerializeField] private float minTouchDiameter = 0.85f;
        [Tooltip("Orden de dibujado (las hormigas usan 10-60 y su burbuja 200)")]
        [SerializeField] private int sortingOrder = 150;

        [Header("Latido de los problemas")]
        [SerializeField] private float pulseAmount = 0.12f;
        [SerializeField] private float pulseSpeed = 1f;

        [Header("Iconos")]
        [SerializeField] private Color letterColor = Color.white;
        [SerializeField] private Color outlineColor = new Color(0f, 0f, 0f, 0.6f);
        [SerializeField] private List<AlertIcon> icons = new List<AlertIcon>
        {
            new AlertIcon(RoomAlert.SinAcceso,  "X", new Color(0.55f, 0.55f, 0.60f)),
            new AlertIcon(RoomAlert.SinObreras, "0", new Color(0.85f, 0.15f, 0.15f)),
            new AlertIcon(RoomAlert.SinHojas,   "H", new Color(0.95f, 0.50f, 0.10f)),
            new AlertIcon(RoomAlert.SinHongo,   "G", new Color(0.95f, 0.50f, 0.10f)),
            new AlertIcon(RoomAlert.Llena,      "=", new Color(0.25f, 0.45f, 0.75f)),
        };

        private class View
        {
            public Room room;
            public GameObject root;
            public SpriteRenderer outline, background, icon;
            public TextMeshPro label;
            public RoomAlert alert;
            public RoomAlert shown = (RoomAlert)(-1);
            public float scale;   // diámetro actual en el mundo (con zoom y latido)
        }

        private readonly Dictionary<Room, View> views = new Dictionary<Room, View>();
        private readonly List<Room> gone = new List<Room>();
        private Camera cam;
        private float pollTimer;
        private static Sprite disc;

        // ================= CICLO =================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var v in views.Values) if (v.root != null) Destroy(v.root);
            views.Clear();
        }

        private void OnDisable()
        {
            foreach (var v in views.Values) if (v.root != null) v.root.SetActive(false);
        }

        private void Update()
        {
            pollTimer -= Time.unscaledDeltaTime;
            if (pollTimer > 0f) return;
            pollTimer = pollInterval;
            SyncRooms();
            foreach (var v in views.Values) v.alert = Evaluate(v.room, showFull);
        }

        private void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            float zoomFactor = cam != null && referenceZoom > 0f ? cam.orthographicSize / referenceZoom : 1f;

            foreach (var v in views.Values)
            {
                if (v.root == null) continue;
                bool show = v.alert != RoomAlert.None && v.room != null;
                if (v.root.activeSelf != show) v.root.SetActive(show);
                if (!show) continue;
                if (v.alert != v.shown) ApplyIcon(v);
                Place(v, zoomFactor);
            }
        }

        // ================= CONSULTAS (las usa el panel de sala) =================

        /// <summary>La sala está conectada con la colonia. Sin navegador en la escena, se da por buena.</summary>
        public static bool HasAccess(Room room)
        {
            if (room == null) return false;
            var nav = Navigation.Current as GridNavigator;
            if (nav == null) return true;
            return nav.Nearest(room.WorkPosition, out var node) && nav.IsReachable(node);
        }

        /// <summary>El problema más importante de la sala (None si no tiene ninguno).</summary>
        public static RoomAlert Evaluate(Room room, bool includeFull = true)
        {
            if (room == null || room.Data == null || !room.IsBuilt) return RoomAlert.None;
            if (!HasAccess(room)) return RoomAlert.SinAcceso;

            var prod = ColonyProduction.Instance;
            if (prod == null) return RoomAlert.None;
            switch (prod.GetStatus(room))
            {
                case RoomStatus.SinObreras: return RoomAlert.SinObreras;
                case RoomStatus.SinEntrada: return room.Data.type == RoomType.Storage ? RoomAlert.SinHongo : RoomAlert.SinHojas;
                case RoomStatus.Llena: return includeFull ? RoomAlert.Llena : RoomAlert.None;
                default: return RoomAlert.None;
            }
        }

        /// <summary>Sala cuyo icono (visible) está bajo el punto, o null. El más cercano si se solapan.</summary>
        public static bool TryGetRoomAt(Vector2 world, out Room room)
        {
            room = null;
            var s = Instance;
            if (s == null || !s.isActiveAndEnabled) return false;

            float zoomFactor = s.cam != null && s.referenceZoom > 0f ? s.cam.orthographicSize / s.referenceZoom : 1f;
            float best = float.MaxValue;
            foreach (var v in s.views.Values)
            {
                if (v.room == null || v.root == null || !v.root.activeSelf) continue;
                float r = Mathf.Max(v.scale, s.minTouchDiameter * zoomFactor) * 0.5f;
                float d = ((Vector2)v.root.transform.position - world).sqrMagnitude;
                if (d <= r * r && d < best) { best = d; room = v.room; }
            }
            return room != null;
        }

        // ================= SALAS =================

        private void SyncRooms()
        {
            gone.Clear();
            foreach (var kv in views)
                if (kv.Key == null || !Room.All.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var r in gone)
            {
                if (views.TryGetValue(r, out var v) && v.root != null) Destroy(v.root);
                views.Remove(r);
            }

            foreach (var r in Room.All)
                if (r != null && !views.ContainsKey(r)) views[r] = CreateView(r);
        }

        /// <summary>Centro del techo de la sala: el prefab tiene el pivote en la esquina inferior izquierda de la huella.</summary>
        private static Vector3 TopCenter(Room room)
        {
            var grid = NestGrid.Instance;
            Vector3 p = room.transform.position;
            if (grid == null || room.Data == null) return room.WorkPosition + Vector3.up * 2f;
            Vector2 size = (Vector2)room.Data.footprint * grid.CellSize;
            if (room.Data.prefab == null) return new Vector3(p.x, p.y + size.y, p.z);   // sin prefab: está en el suelo, centrada
            return new Vector3(p.x + size.x * 0.5f, p.y + size.y, p.z);
        }

        // ================= DIBUJO =================

        private View CreateView(Room room)
        {
            var v = new View { room = room };
            v.root = new GameObject($"RoomStatusIcon ({(room.Data != null ? room.Data.displayName : room.name)})");
            v.root.SetActive(false);
            v.outline = NewDisc("Outline", v.root.transform, sortingOrder, 1.15f);
            v.outline.color = outlineColor;
            v.background = NewDisc("Background", v.root.transform, sortingOrder + 1, 1f);

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(v.root.transform, false);
            v.icon = iconGo.AddComponent<SpriteRenderer>();
            v.icon.sortingOrder = sortingOrder + 2;

            var labelGo = new GameObject("Letter");
            labelGo.transform.SetParent(v.root.transform, false);
            v.label = labelGo.AddComponent<TextMeshPro>();
            v.label.rectTransform.sizeDelta = Vector2.one;
            v.label.alignment = TextAlignmentOptions.Center;
            v.label.fontSize = 6f;
            v.label.fontStyle = FontStyles.Bold;
            v.label.textWrappingMode = TextWrappingModes.NoWrap;
            v.label.color = letterColor;
            v.label.sortingOrder = sortingOrder + 3;
            return v;
        }

        private void ApplyIcon(View v)
        {
            v.shown = v.alert;
            var entry = icons.Find(i => i.alert == v.alert);
            v.background.color = entry != null ? entry.color : Color.gray;
            bool hasSprite = entry != null && entry.sprite != null;
            v.icon.sprite = hasSprite ? entry.sprite : null;
            v.icon.enabled = hasSprite;
            if (hasSprite)
            {
                var size = entry.sprite.bounds.size;   // cabe dentro del círculo (70 % del diámetro)
                v.icon.transform.localScale = Vector3.one * (0.7f / Mathf.Max(size.x, size.y, 0.0001f));
            }
            v.label.enabled = !hasSprite;
            v.label.text = entry != null ? entry.letter : "";
        }

        private void Place(View v, float zoomFactor)
        {
            float scale = diameter * zoomFactor;
            if (v.alert != RoomAlert.Llena)
                scale *= 1f + pulseAmount * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f));
            v.scale = scale;

            // Colgado del techo, por dentro: la parte de arriba del icono queda a topMargin del techo.
            Vector3 top = TopCenter(v.room);
            v.root.transform.position = new Vector3(top.x, top.y - topMargin - scale * 0.5f, top.z);
            v.root.transform.localScale = Vector3.one * scale;
        }

        private static SpriteRenderer NewDisc(string name, Transform parent, int order, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Disc();
            sr.sortingOrder = order;
            return sr;
        }

        private static Sprite Disc()
        {
            if (disc != null) return disc;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "RoomStatusIconDisc"
            };
            float r = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            disc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            disc.name = "RoomStatusIconDisc";
            return disc;
        }
    }
}
