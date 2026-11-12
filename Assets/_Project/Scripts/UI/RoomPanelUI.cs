using System.Collections.Generic;
using Myrmutation.Ants;
using Myrmutation.Building;
using Myrmutation.Core;
using Myrmutation.Economy;
using Myrmutation.Expedition;
using Myrmutation.Genetics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Myrmutation.UI
{
    /// <summary>
    /// E13 · Panel de sala. Se abre al tocar una sala del mapa (fuera del modo Construcción).
    ///
    ///  · Va en el mismo hueco que el panel de hormiga (arriba a la derecha) y solo hay uno abierto:
    ///    al abrir este se deselecciona la hormiga, y al seleccionar una hormiga este se cierra.
    ///  · Nombre, descripción corta (RoomData.description) y obreras n/capacidad con su casta y su
    ///    rendimiento en la sala (x1.5…). Tocar una obrera la selecciona (se abre su panel).
    ///    En la Sala Real, que no admite obreras, se listan las obreras libres que esperan allí (B8).
    ///  · Producción ("+12 hongo/min · -12 hojas/min") y estado ("Sin hojas", "Llena"…) de D8
    ///    (ColonyProduction.RatePerMinute, InputPerMinute, GetStatus y Describe).
    ///  · "Añadir obrera libre": asigna la obrera libre con mejor rendimiento en esta sala
    ///    (ColonyProduction.GetEfficiency = workRate × afinidad de su casta).
    ///
    /// Si toca una hormiga, gana la hormiga (se abre su panel). Mientras el panel de hormiga está
    /// eligiendo sala en el mapa ("Asignar a sala"), tocar una sala no abre este panel.
    /// La Sala del Festín abre su propio menú (C9) si está en la escena, así que este panel no se abre ahí.
    /// E14 puede abrirlo con RoomPanelUI.Instance.Open(sala).
    ///
    /// TODA la interfaz se crea por código al arrancar (como AntListUI): el prefab es un objeto vacío a
    /// pantalla completa con este script, dentro del Canvas del HUD.
    /// Medidas en píxeles de la resolución de referencia (1920×1080): botones y filas ≥ 88 px, textos ≥ 32 px (E11).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class RoomPanelUI : MonoBehaviour
    {
        public static RoomPanelUI Instance { get; private set; }

        [Header("Hueco (el mismo que el panel de hormiga en Game)")]
        [SerializeField] private Vector2 panelSize = new Vector2(480f, 760f);
        [Tooltip("Desde la esquina superior derecha de la pantalla")]
        [SerializeField] private Vector2 panelOffset = new Vector2(-20f, -120f);

        [Header("Medidas (px de referencia 1920×1080)")]
        [SerializeField] private float fontSize = 32f;
        [SerializeField] private float buttonHeight = 88f;
        [SerializeField] private float rowHeight = 88f;

        [Header("Colores")]
        [SerializeField] private Color panelColor = new Color(0.12f, 0.08f, 0.06f, 0.96f);
        [SerializeField] private Color buttonColor = new Color(0.36f, 0.25f, 0.17f, 1f);
        [SerializeField] private Color rowColor = new Color(0.20f, 0.14f, 0.10f, 1f);
        [SerializeField] private Color textColor = new Color(0.95f, 0.92f, 0.87f, 1f);
        [SerializeField] private Color dimTextColor = new Color(0.75f, 0.70f, 0.62f, 1f);
        [SerializeField] private Color okColor = new Color(0.49f, 0.76f, 0.35f, 1f);
        [SerializeField] private Color warningColor = new Color(1f, 0.45f, 0.36f, 1f);

        [Header("Comportamiento")]
        [Tooltip("Si hay un menú del Festín (C9) en la escena, tocar la Sala del Festín abre ese menú y no este panel")]
        [SerializeField] private bool leaveFeastToFeastMenu = true;
        [Tooltip("Cada cuántos segundos (reales) se refrescan producción, estado y obreras con el panel abierto")]
        [SerializeField] private float refreshInterval = 0.25f;

        // ---------- estado ----------
        private Room room;
        private readonly List<Ant> workers = new List<Ant>();
        private readonly List<Ant> shownWorkers = new List<Ant>();
        private readonly List<GameObject> rows = new List<GameObject>();
        private float refreshTimer;
        private float messageUntil;
        private bool wasPickingRoom;
        private FeastMenuUI feastMenu;

        // ---------- interfaz creada por código ----------
        private RectTransform root, panel, listContent;
        private Image iconImage;
        private TMP_Text titleText, descText, productionText, statusText, countText, emptyText, hintText;
        private Button addButton;

        /// <summary>El panel está abierto.</summary>
        public bool IsOpen => panel != null && panel.gameObject.activeSelf;

        /// <summary>Sala que enseña el panel (null si está cerrado).</summary>
        public Room Current => IsOpen ? room : null;

        // ================= CICLO =================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            root = (RectTransform)transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;
            BuildUI();
            panel.gameObject.SetActive(false);
        }

        private void Start()
        {
            feastMenu = FindAnyObjectByType<FeastMenuUI>(FindObjectsInactive.Include);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            CameraController.Tapped += OnTap;
            EventBus.Subscribe<AntSelected>(OnAntSelected);
        }

        private void OnDisable()
        {
            CameraController.Tapped -= OnTap;
            EventBus.Unsubscribe<AntSelected>(OnAntSelected);
        }

        private void Update()
        {
            // Se guarda antes de que CameraController lance Tapped (en su LateUpdate): si AntSelection atiende
            // el toque primero, el modo "elegir sala" ya se habrá acabado cuando llegue a OnTap.
            var sel = AntSelection.Instance;
            wasPickingRoom = sel != null && sel.IsPickingRoom;

            if (!IsOpen) return;
            if (room == null || CameraController.ToolMode) { Close(); return; }   // sala destruida o modo Construcción

            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer > 0f) return;
            refreshTimer = refreshInterval;
            Refresh();
        }

        // ================= ABRIR / CERRAR =================

        public void Open(Room target)
        {
            if (target == null || target.Data == null) return;
            AntSelection.Instance?.Deselect();   // solo un panel abierto en el hueco de la derecha
            room = target;
            shownWorkers.Clear();
            messageUntil = 0f;
            hintText.text = "";
            panel.gameObject.SetActive(true);
            refreshTimer = refreshInterval;
            Refresh();
        }

        public void Close()
        {
            room = null;
            ClearRows();
            shownWorkers.Clear();
            if (panel != null) panel.gameObject.SetActive(false);
        }

        private void OnAntSelected(AntSelected e)
        {
            if (e.Ant != null) Close();   // se abre el panel de hormiga en el mismo hueco
        }

        private void OnTap(Vector2 world)
        {
            if (panel == null || CameraController.ToolMode) return;
            var sel = AntSelection.Instance;
            if (wasPickingRoom || (sel != null && sel.IsPickingRoom)) return;   // el panel de hormiga está eligiendo sala
            if (sel != null && sel.AntsAt(world).Count > 0) return;            // gana la hormiga: su panel cierra este

            var target = RoomSpawner.RoomAt(world);
            if (target == null || target.Data == null) { Close(); return; }
            if (leaveFeastToFeastMenu && feastMenu != null && target.Data.type == RoomType.Feast) { Close(); return; }
            if (IsOpen && target == room) return;
            Open(target);
        }

        // ================= DATOS =================

        private void Refresh()
        {
            if (room == null || room.Data == null) return;
            var data = room.Data;
            bool royal = data.type == RoomType.Royal;
            int cap = Mathf.Max(1, data.workerCapacity);

            titleText.text = string.IsNullOrEmpty(data.displayName) ? room.name : data.displayName;
            descText.text = data.description ?? "";
            descText.gameObject.SetActive(!string.IsNullOrEmpty(data.description));
            iconImage.sprite = data.icon;
            iconImage.gameObject.SetActive(data.icon != null);

            // Obreras (en la Sala Real: las libres que esperan allí)
            if (royal) CollectFree(workers); else CollectAssigned(room, workers);
            countText.text = royal ? $"Obreras libres: {workers.Count}" : $"Obreras: {workers.Count}/{cap}";
            if (!SameAnts(workers, shownWorkers)) RebuildRows(royal);

            // Producción y estado (D8)
            var prod = ColonyProduction.Instance;
            bool producer = prod != null && ColonyProduction.IsProducer(data.type);
            productionText.gameObject.SetActive(producer);
            if (producer) productionText.text = ProductionLine(prod, room, data.type);

            statusText.gameObject.SetActive(prod != null && !royal);
            if (prod != null && !royal)
            {
                var status = prod.GetStatus(room);
                statusText.text = "Estado: " + ColonyProduction.Describe(status, data.type);
                statusText.color = status == RoomStatus.Ok ? okColor : warningColor;
            }

            // Añadir obrera libre
            addButton.gameObject.SetActive(!royal);
            if (!royal)
            {
                bool full = workers.Count >= cap;
                bool anyFree = BestFreeAnt(data.type) != null;
                addButton.interactable = !full && anyFree;
                if (Time.unscaledTime >= messageUntil)
                    hintText.text = full ? "La sala está llena" : !anyFree ? "No hay obreras libres" : "";
            }
            else if (Time.unscaledTime >= messageUntil) hintText.text = "";
        }

        private static string ProductionLine(ColonyProduction prod, Room r, RoomType type)
        {
            float output = prod.RatePerMinute(r);
            float input = prod.InputPerMinute(r);
            return type == RoomType.FungusGarden
                ? $"+{Amount(output)} hongo/min · -{Amount(input)} hojas/min"
                : $"+{Amount(output)} comida/min · -{Amount(input)} hongo/min";
        }

        private static string Amount(float perMinute) =>
            perMinute >= 10f ? Mathf.RoundToInt(perMinute).ToString() : perMinute.ToString("0.#");

        /// <summary>Asignadas a la sala, como cuenta D8 (vivas, no reina, AssignedRoom == sala).</summary>
        private static void CollectAssigned(Room r, List<Ant> result)
        {
            result.Clear();
            foreach (var a in Ant.All)
                if (a != null && a.IsAlive && !a.IsQueen && a.AssignedRoom == r) result.Add(a);
        }

        private static void CollectFree(List<Ant> result)
        {
            result.Clear();
            foreach (var a in Ant.All) if (IsFree(a)) result.Add(a);
        }

        /// <summary>Libre: viva, no reina, sin sala, en el nido (no de expedición) y con el cerebro activo.</summary>
        private static bool IsFree(Ant a)
        {
            if (a == null || !a.IsAlive || a.IsQueen || a.AssignedRoom != null) return false;
            if (!a.gameObject.activeInHierarchy) return false;
            var exp = ExpeditionSystem.Instance;
            if (exp != null)
            {
                var away = exp.AntsAway;
                for (int i = 0; i < away.Count; i++) if (away[i] == a) return false;
            }
            if (a.TryGetComponent<AntBrain>(out var brain) && brain.IsSuspended) return false;
            return true;
        }

        /// <summary>La obrera libre con mejor rendimiento en este tipo de sala (workRate × afinidad).</summary>
        private static Ant BestFreeAnt(RoomType type)
        {
            Ant best = null;
            float bestEff = float.MinValue;
            foreach (var a in Ant.All)
            {
                if (!IsFree(a)) continue;
                float eff = ColonyProduction.GetEfficiency(a, type);
                if (eff > bestEff) { best = a; bestEff = eff; }
            }
            return best;
        }

        private void AddFreeWorker()
        {
            if (room == null || room.Data == null || room.Data.type == RoomType.Royal) return;
            CollectAssigned(room, workers);
            if (workers.Count >= Mathf.Max(1, room.Data.workerCapacity)) return;
            var ant = BestFreeAnt(room.Data.type);
            if (ant == null) return;

            ant.AssignedRoom = room;
            if (ant.TryGetComponent<AntBrain>(out var brain)) brain.Think();   // que vaya ya
            hintText.text = $"{ant.Name} ({CasteName(ant)}) se une a la sala";
            messageUntil = Time.unscaledTime + 2.5f;
            Refresh();
        }

        private static bool SameAnts(List<Ant> a, List<Ant> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static string CasteName(Ant a)
        {
            if (a.IsQueen) return "Reina";
            if (a.Caste == null) return "Sin casta";
            return !string.IsNullOrEmpty(a.Caste.displayName) ? a.Caste.displayName : a.Caste.caste.ToString();
        }

        // ================= FILAS DE OBRERAS =================

        private void RebuildRows(bool royal)
        {
            ClearRows();
            shownWorkers.Clear();
            shownWorkers.AddRange(workers);
            emptyText.gameObject.SetActive(workers.Count == 0);
            emptyText.text = royal ? "No hay obreras libres" : "Sin obreras";
            foreach (var a in workers) rows.Add(CreateRow(a, royal));
        }

        private void ClearRows()
        {
            foreach (var go in rows) if (go != null) Destroy(go);
            rows.Clear();
        }

        private GameObject CreateRow(Ant ant, bool royal)
        {
            var bg = NewImage("Worker_" + ant.Name, listContent, rowColor);
            AddLayout(bg.gameObject, preferredHeight: rowHeight);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var target = ant;
            button.onClick.AddListener(() => { if (target != null && target.IsAlive) AntSelection.Instance?.Select(target); });

            // Cuadro del color de la casta
            var swatch = NewImage("Caste", bg.rectTransform, ant.Caste != null ? ant.Caste.tint : Color.white);
            var srt = swatch.rectTransform;
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0f, 0.5f);
            srt.sizeDelta = new Vector2(48f, 48f);
            srt.anchoredPosition = new Vector2(16f, 0f);
            swatch.raycastTarget = false;

            string label = $"{ant.Name} <color=#{ColorUtility.ToHtmlStringRGB(dimTextColor)}>· {CasteName(ant)}</color>";
            if (!royal && room != null && room.Data != null)
            {
                float eff = ColonyProduction.GetEfficiency(ant, room.Data.type);
                string effText = $"x{eff:0.0#}";
                string color = eff > 1.01f ? ColorUtility.ToHtmlStringRGB(okColor)
                             : eff < 0.99f ? ColorUtility.ToHtmlStringRGB(warningColor) : null;
                label += color != null ? $"  <color=#{color}>{effText}</color>" : $"  {effText}";
            }
            var text = NewText("Label", bg.rectTransform, label, fontSize, textColor, TextAlignmentOptions.MidlineLeft);
            var trt = text.rectTransform;
            Stretch(trt);
            trt.offsetMin = new Vector2(80f, 0f);
            trt.offsetMax = new Vector2(-12f, 0f);
            return bg.gameObject;
        }

        // ================= CONSTRUCCIÓN DE LA INTERFAZ =================

        private void BuildUI()
        {
            var panelImg = NewImage("RoomPanel", root, panelColor);
            panel = panelImg.rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 1f);
            panel.sizeDelta = panelSize;
            panel.anchoredPosition = panelOffset;
            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 16, 16);
            vlg.spacing = 8f;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            // Cabecera: icono + nombre + ✕
            var header = NewRow("Header", panel, buttonHeight);
            iconImage = NewImage("Icon", header, Color.white);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            AddLayout(iconImage.gameObject, preferredWidth: 72f);
            titleText = NewText("Title", header, "", fontSize + 4f, textColor, TextAlignmentOptions.MidlineLeft);
            titleText.fontStyle = FontStyles.Bold;
            AddLayout(titleText.gameObject, flexibleWidth: 1f);
            var close = NewButton("Close", header, "X", out _);
            AddLayout(close.gameObject, preferredWidth: buttonHeight);
            close.onClick.AddListener(Close);

            // Descripción, producción y estado (alto según el texto)
            descText = NewText("Description", panel, "", fontSize, dimTextColor, TextAlignmentOptions.TopLeft);
            descText.textWrappingMode = TextWrappingModes.Normal;
            descText.overflowMode = TextOverflowModes.Overflow;
            productionText = NewText("Production", panel, "", fontSize, textColor, TextAlignmentOptions.MidlineLeft);
            productionText.textWrappingMode = TextWrappingModes.Normal;
            productionText.overflowMode = TextOverflowModes.Overflow;
            statusText = NewText("Status", panel, "", fontSize, okColor, TextAlignmentOptions.MidlineLeft);

            // Obreras
            countText = NewText("Count", panel, "", fontSize, textColor, TextAlignmentOptions.MidlineLeft);
            countText.fontStyle = FontStyles.Bold;
            BuildList();

            // Pie: aviso + botón
            hintText = NewText("Hint", panel, "", fontSize, warningColor, TextAlignmentOptions.MidlineLeft);
            AddLayout(hintText.gameObject, preferredHeight: fontSize * 1.4f);
            addButton = NewButton("AddWorker", panel, "Añadir obrera libre", out _);
            AddLayout(addButton.gameObject, preferredHeight: buttonHeight);
            addButton.onClick.AddListener(AddFreeWorker);
        }

        private void BuildList()
        {
            var scrollImg = NewImage("Workers", panel, new Color(0f, 0f, 0f, 0.15f));
            AddLayout(scrollImg.gameObject, flexibleHeight: 1f);
            var scroll = scrollImg.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var viewport = (RectTransform)vpGo.transform;
            viewport.SetParent(scrollImg.transform, false);
            Stretch(viewport);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            listContent = (RectTransform)contentGo.transform;
            listContent.SetParent(viewport, false);
            listContent.anchorMin = new Vector2(0f, 1f); listContent.anchorMax = new Vector2(1f, 1f);
            listContent.pivot = new Vector2(0.5f, 1f);
            listContent.sizeDelta = Vector2.zero;
            listContent.anchoredPosition = Vector2.zero;
            var cvlg = contentGo.AddComponent<VerticalLayoutGroup>();
            cvlg.spacing = 6f;
            cvlg.childControlWidth = true; cvlg.childControlHeight = true;
            cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;
            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            emptyText = NewText("Empty", listContent, "Sin obreras", fontSize, dimTextColor, TextAlignmentOptions.Center);
            AddLayout(emptyText.gameObject, preferredHeight: rowHeight);

            scroll.viewport = viewport;
            scroll.content = listContent;
        }

        // ---------- utilidades (como AntListUI) ----------

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static TMP_Text NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }

        private Button NewButton(string name, Transform parent, string label, out TMP_Text text)
        {
            var img = NewImage(name, parent, buttonColor);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            text = NewText("Label", img.transform, label, fontSize, textColor, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(8f, 4f);
            text.rectTransform.offsetMax = new Vector2(-8f, -4f);
            return b;
        }

        private static RectTransform NewRow(string name, Transform parent, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = true;
            AddLayout(go, preferredHeight: height);
            return (RectTransform)go.transform;
        }

        private static void AddLayout(GameObject go, float preferredWidth = -1f, float preferredHeight = -1f,
                                      float flexibleWidth = -1f, float flexibleHeight = -1f)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (preferredWidth >= 0f) le.preferredWidth = preferredWidth;
            if (preferredHeight >= 0f) { le.preferredHeight = preferredHeight; le.minHeight = preferredHeight; }
            if (flexibleWidth >= 0f) le.flexibleWidth = flexibleWidth;
            if (flexibleHeight >= 0f) le.flexibleHeight = flexibleHeight;
        }
    }
}
