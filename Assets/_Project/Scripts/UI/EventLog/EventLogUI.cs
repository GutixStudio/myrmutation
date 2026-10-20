using System;
using System.Collections.Generic;
using Myrmutation.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Myrmutation.UI
{
    /// <summary>
    /// E11 · Interfaz del registro de eventos (los datos están en EventLog).
    ///
    ///  · AVISOS FLOTANTES: los 'maxToasts' (3) últimos, arriba a la izquierda bajo la barra superior.
    ///    Duran 'toastSeconds' (6 s) y se desvanecen. Ancho máximo: 35 % de la pantalla.
    ///    Tocar un aviso abre el panel completo.
    ///  · PANEL COMPLETO: al tocar un aviso o el botón del HUD. Lista (lo más reciente arriba)
    ///    con filtro por categoría. Tocar una entrada con foco centra la cámara en ella.
    ///  · Color e icono por categoría (placeholders hasta F12: se cambian en 'styles').
    ///  · Móvil: textos de al menos 32 px y botones de al menos 88 px de alto (resolución
    ///    de referencia 1920×1080). Se fuerza por código en todo lo que se crea.
    ///
    /// Usa tiempo sin escala: los avisos se desvanecen también con el juego en pausa.
    /// </summary>
    public class EventLogUI : MonoBehaviour
    {
        [Serializable]
        public class CategoryStyle
        {
            public LogCategory category;
            public string label;
            public Color color = Color.white;
            [Tooltip("Placeholder hasta F12. Si está vacío se usa un cuadro del color de la categoría")]
            public Sprite icon;
        }

        [Header("Avisos flotantes")]
        [Tooltip("Contenedor de los avisos (con Vertical Layout Group). Se coloca solo arriba a la izquierda")]
        [SerializeField] private RectTransform toastContainer;
        [Tooltip("Aviso plantilla (desactivado): Button con Image de fondo, un hijo Image 'Icon' y un hijo TMP")]
        [SerializeField] private Button toastTemplate;
        [SerializeField] private int maxToasts = 3;
        [SerializeField] private float toastSeconds = 6f;
        [SerializeField] private float fadeSeconds = 0.6f;
        [Tooltip("Ancho máximo de los avisos, en fracción de la pantalla")]
        [Range(0.1f, 1f)] [SerializeField] private float maxWidthFraction = 0.35f;
        [Tooltip("Separación desde arriba (alto de la barra superior del HUD + margen), en px de referencia")]
        [SerializeField] private float topOffset = 140f;
        [SerializeField] private float leftMargin = 20f;

        [Header("Panel completo")]
        [SerializeField] private GameObject panel;
        [Tooltip("Botón del HUD que abre/cierra el panel")]
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [Tooltip("Contenedor de los botones de filtro (con Horizontal Layout Group)")]
        [SerializeField] private Transform filterContainer;
        [SerializeField] private Button filterButtonTemplate;
        [Tooltip("Content del Scroll View (con Vertical Layout Group y Content Size Fitter vertical)")]
        [SerializeField] private Transform listContent;
        [Tooltip("Entrada plantilla (desactivada): igual que el aviso")]
        [SerializeField] private Button entryTemplate;
        [Tooltip("Opcional: texto que se muestra si no hay entradas")]
        [SerializeField] private GameObject emptyLabel;
        [SerializeField] private Color filterActiveColor = new Color(0.95f, 0.8f, 0.3f);
        [SerializeField] private Color filterNormalColor = Color.white;

        [Header("Cámara")]
        [Tooltip("Segundos que tarda la cámara en centrarse en una entrada")]
        [SerializeField] private float focusSeconds = 0.35f;
        [Tooltip("Cerrar el panel al centrar la cámara (recomendado en móvil, el panel tapa el mapa)")]
        [SerializeField] private bool closePanelOnFocus = true;

        [Header("Móvil (px en la resolución de referencia 1920×1080)")]
        [SerializeField] private float minFontSize = 32f;
        [SerializeField] private float minButtonHeight = 88f;

        [Header("Colores e iconos por categoría (placeholders hasta F12)")]
        [SerializeField] private List<CategoryStyle> styles = new List<CategoryStyle>();

        private class Toast { public GameObject Go; public CanvasGroup Group; public float Born; }

        private readonly List<Toast> toasts = new List<Toast>();
        private readonly List<GameObject> listItems = new List<GameObject>();
        private readonly Dictionary<Button, LogCategory?> filterButtons = new Dictionary<Button, LogCategory?>();
        private LogCategory? filter;            // null = todas
        private EventLog log;

        private Transform camTarget;
        private Vector3 camFrom, camTo;
        private float camTimer = -1f;

        // ================= CICLO =================

        private void Awake()
        {
            if (toastTemplate != null) toastTemplate.gameObject.SetActive(false);
            if (entryTemplate != null) entryTemplate.gameObject.SetActive(false);
            if (filterButtonTemplate != null) filterButtonTemplate.gameObject.SetActive(false);
            if (panel != null) panel.SetActive(false);

            if (openButton != null) openButton.onClick.AddListener(TogglePanel);
            if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);

            if (styles.Count == 0) styles = DefaultStyles();
            PlaceToastContainer();
            BuildFilters();
        }

        private void OnEnable()
        {
            log = EventLog.Instance != null ? EventLog.Instance : FindAnyObjectByType<EventLog>();
            if (log != null) log.Added += OnAdded;
            else Debug.LogWarning("[EventLogUI] No hay EventLog en la escena.", this);
        }

        private void OnDisable()
        {
            if (log != null) log.Added -= OnAdded;
        }

        private void Update()
        {
            UpdateToasts();
            UpdateCamera();
        }

        // ================= AVISOS FLOTANTES =================

        private void PlaceToastContainer()
        {
            if (toastContainer == null) return;
            // Arriba a la izquierda, bajo la barra superior, con el ancho máximo pedido.
            // Horizontal: del margen izquierdo hasta el 35 % de la pantalla.
            // Vertical: colgado de arriba; la altura la pone su Content Size Fitter.
            toastContainer.anchorMin = new Vector2(0f, 1f);
            toastContainer.anchorMax = new Vector2(maxWidthFraction, 1f);
            toastContainer.pivot = new Vector2(0f, 1f);
            toastContainer.offsetMin = new Vector2(leftMargin, toastContainer.offsetMin.y);
            toastContainer.offsetMax = new Vector2(0f, toastContainer.offsetMax.y);
            var p = toastContainer.anchoredPosition;
            p.y = -topOffset;
            toastContainer.anchoredPosition = p;
        }

        private void OnAdded(LogEntry e)
        {
            ShowToast(e);
            if (panel != null && panel.activeSelf) RebuildList();
        }

        private void ShowToast(LogEntry e)
        {
            if (toastTemplate == null || toastContainer == null) return;

            var b = Instantiate(toastTemplate, toastContainer);
            b.gameObject.SetActive(true);
            b.transform.SetAsFirstSibling();                     // el más nuevo arriba
            Fill(b, e, showDay: false);
            b.onClick.AddListener(OpenPanel);

            var group = b.GetComponent<CanvasGroup>();
            if (group == null) group = b.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;

            toasts.Add(new Toast { Go = b.gameObject, Group = group, Born = Time.unscaledTime });
            while (toasts.Count > Mathf.Max(1, maxToasts)) RemoveToast(0);
        }

        private void UpdateToasts()
        {
            float now = Time.unscaledTime;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                if (t.Go == null) { toasts.RemoveAt(i); continue; }
                float age = now - t.Born;
                if (age >= toastSeconds) { RemoveToast(i); continue; }
                float fadeStart = toastSeconds - fadeSeconds;
                t.Group.alpha = age <= fadeStart ? 1f : Mathf.Clamp01((toastSeconds - age) / Mathf.Max(0.01f, fadeSeconds));
            }
        }

        private void RemoveToast(int i)
        {
            if (toasts[i].Go != null) Destroy(toasts[i].Go);
            toasts.RemoveAt(i);
        }

        // ================= PANEL COMPLETO =================

        public void OpenPanel()
        {
            if (panel == null) return;
            panel.SetActive(true);
            RefreshFilterColors();
            RebuildList();
        }

        public void ClosePanel()
        {
            if (panel != null) panel.SetActive(false);
        }

        public void TogglePanel()
        {
            if (panel != null && panel.activeSelf) ClosePanel();
            else OpenPanel();
        }

        private void BuildFilters()
        {
            if (filterButtonTemplate == null || filterContainer == null) return;
            AddFilterButton(null, "Todo");
            foreach (LogCategory c in Enum.GetValues(typeof(LogCategory)))
                AddFilterButton(c, StyleOf(c).label);
            RefreshFilterColors();
        }

        private void AddFilterButton(LogCategory? category, string label)
        {
            var b = Instantiate(filterButtonTemplate, filterContainer);
            b.gameObject.SetActive(true);
            var text = b.GetComponentInChildren<TMP_Text>(true);
            if (text != null) { text.text = label; ApplyMinFont(text); }
            ApplyMinHeight(b.gameObject);
            b.onClick.AddListener(() => { filter = category; RefreshFilterColors(); RebuildList(); });
            filterButtons[b] = category;
        }

        private void RefreshFilterColors()
        {
            foreach (var kv in filterButtons)
                if (kv.Key != null && kv.Key.image != null)
                    kv.Key.image.color = kv.Value == filter ? filterActiveColor : filterNormalColor;
        }

        private void RebuildList()
        {
            foreach (var go in listItems) if (go != null) Destroy(go);
            listItems.Clear();
            if (log == null || entryTemplate == null || listContent == null) return;

            var all = log.Entries;
            for (int i = all.Count - 1; i >= 0; i--)          // lo más reciente arriba
            {
                var e = all[i];
                if (filter.HasValue && e.Category != filter.Value) continue;

                var b = Instantiate(entryTemplate, listContent);
                b.gameObject.SetActive(true);
                Fill(b, e, showDay: true);
                if (e.HasFocus)
                {
                    var entry = e;
                    b.onClick.AddListener(() => FocusOn(entry));
                }
                else b.interactable = false;                   // sin foco: no hace nada al tocarla
                listItems.Add(b.gameObject);
            }

            if (emptyLabel != null) emptyLabel.SetActive(listItems.Count == 0);
        }

        // ================= CÁMARA =================

        private void FocusOn(LogEntry e)
        {
            var cam = Camera.main;
            if (cam == null) return;
            camTarget = cam.transform;
            camFrom = camTarget.position;
            Vector3 p = e.FocusPoint;
            camTo = new Vector3(p.x, p.y, camFrom.z);
            camTimer = 0f;
            if (closePanelOnFocus) ClosePanel();
        }

        private void UpdateCamera()
        {
            if (camTimer < 0f || camTarget == null) return;
            camTimer += Time.unscaledDeltaTime;
            float t = focusSeconds <= 0f ? 1f : Mathf.Clamp01(camTimer / focusSeconds);
            t = t * t * (3f - 2f * t);                           // suavizado
            camTarget.position = Vector3.Lerp(camFrom, camTo, t);
            if (t >= 1f) camTimer = -1f;
            // CameraController sigue limitando la cámara a los bordes del mapa en su LateUpdate.
        }

        // ================= RELLENAR UN AVISO / ENTRADA =================

        private void Fill(Button b, LogEntry e, bool showDay)
        {
            var style = StyleOf(e.Category);

            var label = b.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                string hex = ColorUtility.ToHtmlStringRGB(style.color);
                string day = showDay ? $"<color=#999999>Día {e.Day} · </color>" : "";
                label.text = $"{day}<color=#{hex}><b>{style.label}</b></color>  {e.Text}";
                label.textWrappingMode = TextWrappingModes.Normal;
                ApplyMinFont(label);
            }

            var icon = FindChildImage(b.transform, "Icon");
            if (icon != null)
            {
                if (style.icon != null) { icon.sprite = style.icon; icon.color = Color.white; }
                else icon.color = style.color;
            }

            ApplyMinHeight(b.gameObject);
        }

        private static Image FindChildImage(Transform root, string name)
        {
            foreach (var img in root.GetComponentsInChildren<Image>(true))
                if (img.transform != root && img.name == name) return img;
            return null;
        }

        private void ApplyMinFont(TMP_Text t)
        {
            if (t.enableAutoSizing) t.fontSizeMin = Mathf.Max(t.fontSizeMin, minFontSize);
            if (t.fontSize < minFontSize) t.fontSize = minFontSize;
        }

        private void ApplyMinHeight(GameObject go)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.minHeight = Mathf.Max(le.minHeight, minButtonHeight);
        }

        // ================= ESTILOS =================

        private CategoryStyle StyleOf(LogCategory c)
        {
            foreach (var s in styles) if (s != null && s.category == c) return s;
            foreach (var s in DefaultStyles()) if (s.category == c) return s;
            return new CategoryStyle { category = c, label = c.ToString(), color = Color.white };
        }

        private static List<CategoryStyle> DefaultStyles() => new List<CategoryStyle>
        {
            new CategoryStyle { category = LogCategory.Info,         label = "Info",         color = new Color(0.80f, 0.80f, 0.80f) },
            new CategoryStyle { category = LogCategory.Nacimiento,   label = "Nacimiento",   color = new Color(0.55f, 0.85f, 0.45f) },
            new CategoryStyle { category = LogCategory.Muerte,       label = "Muerte",       color = new Color(0.90f, 0.35f, 0.30f) },
            new CategoryStyle { category = LogCategory.Construccion, label = "Construcción", color = new Color(0.85f, 0.65f, 0.35f) },
            new CategoryStyle { category = LogCategory.Mutacion,     label = "Mutación",     color = new Color(0.70f, 0.45f, 0.90f) },
            new CategoryStyle { category = LogCategory.Expedicion,   label = "Expedición",   color = new Color(0.40f, 0.75f, 0.90f) },
            new CategoryStyle { category = LogCategory.Aviso,        label = "Aviso",        color = new Color(0.95f, 0.80f, 0.30f) },
            new CategoryStyle { category = LogCategory.Estacion,     label = "Estación",     color = new Color(0.60f, 0.85f, 0.85f) },
        };
    }
}
