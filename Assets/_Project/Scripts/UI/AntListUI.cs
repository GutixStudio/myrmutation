using System;
using System.Collections.Generic;
using Myrmutation.Ants;
using Myrmutation.Core;
using Myrmutation.Expedition;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Myrmutation.UI
{
    /// <summary>
    /// E17 · Lista de hormigas en un cajón lateral.
    ///
    ///  · Pestaña "Hormigas" en el borde izquierdo → abre un cajón a toda la altura. Se cierra con ✕ o tocando fuera.
    ///  · Una fila por hormiga: sprite, nombre, casta, barras de hambre y energía y lo que está haciendo
    ///    (el mismo texto que el panel de hormiga: AntPanelUI.DescribeState). Las que están de expedición, en gris.
    ///  · Filtro por casta (Todas / Menor / Media / Mayor) y orden (casta, nombre, hambre). La reina va siempre primero.
    ///  · Tocar una fila: cierra el cajón, centra la cámara en la hormiga y la selecciona (se abre su panel).
    ///  · Modo selección: casillas para marcar hasta ExpeditionSystem.MaxGroup hormigas y "Enviar a expedición".
    ///    Antes de enviar pregunta a ExpeditionSystem.CanSend, así respeta sus reglas (invierno, ya hay una fuera…)
    ///    y enseña el motivo si no se puede.
    ///  · Se rehace solo con nacimientos, muertes y expediciones (o si cambia el número de hormigas). Con el cajón
    ///    abierto, las barras y el estado de las filas visibles se refrescan cada 'refreshInterval' segundos.
    ///    Las filas son un pool: solo existen las que caben en pantalla (+2) y se reutilizan al desplazar.
    ///
    /// TODA la interfaz se crea por código al arrancar: el prefab es un objeto vacío a pantalla completa con este
    /// script, dentro del Canvas del HUD. No hace falta montar nada a mano ni tocar la escena para cambiar el aspecto.
    /// Medidas en píxeles de la resolución de referencia (1920×1080): filas y botones ≥ 88 px y textos ≥ 32 px (E11).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class AntListUI : MonoBehaviour
    {
        public enum SortMode { Caste, Name, Hunger }

        [Serializable]
        public struct CasteSprite
        {
            public CasteType caste;
            public Sprite sprite;
        }

        [Header("Sprite de la fila")]
        [Tooltip("Sprite de cada hormiga en la lista (de momento el mismo para todas). Vacío = cuadro del color de la casta")]
        [SerializeField] private Sprite antSprite;
        [Tooltip("Opcional: un sprite distinto por casta (si está, gana a antSprite)")]
        [SerializeField] private List<CasteSprite> casteSprites = new List<CasteSprite>();

        [Header("Medidas (px de referencia 1920×1080)")]
        [Tooltip("Ancho del cajón (como mucho el 92 % de la pantalla)")]
        [SerializeField] private float drawerWidth = 760f;
        [SerializeField] private float rowHeight = 100f;
        [SerializeField] private float buttonHeight = 88f;
        [SerializeField] private float fontSize = 32f;
        [SerializeField] private Vector2 tabSize = new Vector2(210f, 130f);
        [Tooltip("Altura de la pestaña respecto al centro de la pantalla")]
        [SerializeField] private float tabOffsetY = 0f;

        [Header("Colores")]
        [SerializeField] private Color panelColor = new Color(0.12f, 0.08f, 0.06f, 0.96f);
        [SerializeField] private Color blockerColor = new Color(0f, 0f, 0f, 0.35f);
        [SerializeField] private Color buttonColor = new Color(0.36f, 0.25f, 0.17f, 1f);
        [SerializeField] private Color activeColor = new Color(0.95f, 0.8f, 0.3f, 1f);
        [SerializeField] private Color textColor = new Color(0.95f, 0.92f, 0.87f, 1f);
        [SerializeField] private Color dimTextColor = new Color(0.75f, 0.70f, 0.62f, 1f);
        [SerializeField] private Color rowColor = new Color(0.20f, 0.14f, 0.10f, 1f);
        [SerializeField] private Color rowAwayColor = new Color(0.16f, 0.15f, 0.14f, 1f);
        [SerializeField] private Color warningColor = new Color(1f, 0.45f, 0.36f, 1f);
        [SerializeField] private Color barBackColor = new Color(0.06f, 0.04f, 0.03f, 1f);
        // Los mismos que el panel de hormiga (AntPanelUI)
        [SerializeField] private Color hungerColor = new Color(0.55f, 0.75f, 0.35f);
        [SerializeField] private Color hungryColor = new Color(0.9f, 0.3f, 0.2f);
        [SerializeField] private Color energyColor = new Color(0.95f, 0.8f, 0.3f);
        [SerializeField] private Color tiredColor = new Color(0.6f, 0.45f, 0.75f);

        [Header("Comportamiento")]
        [Tooltip("Cada cuántos segundos (reales) se refrescan barras y estados con el cajón abierto")]
        [SerializeField] private float refreshInterval = 0.25f;
        [Tooltip("Segundos que tarda la cámara en llegar a la hormiga")]
        [SerializeField] private float focusSeconds = 0.35f;

        // ---------- estado ----------
        private readonly List<Ant> items = new List<Ant>();          // filas actuales (filtradas y ordenadas)
        private readonly HashSet<Ant> picked = new HashSet<Ant>();    // marcadas para la expedición
        private readonly List<Ant> pickBuffer = new List<Ant>();
        private readonly List<RowView> pool = new List<RowView>();
        private CasteType? filter;                                     // null = todas
        private SortMode sort = SortMode.Caste;
        private bool isOpen, selectionMode, dirty = true;
        private float refreshTimer;
        private int lastAntCount = -1, lastAwayCount = -1;

        // ---------- interfaz creada por código ----------
        private RectTransform root, drawer, content, viewport;
        private GameObject blocker, footer;
        private Button tabButton;
        private TMP_Text tabLabel, titleText, pickText, reasonText;
        private Button sendButton, selectButton;
        private readonly List<(Button button, CasteType? caste)> filterButtons = new List<(Button, CasteType?)>();
        private readonly List<(Button button, SortMode mode)> sortButtons = new List<(Button, SortMode)>();
        private ScrollRect scroll;
        private float poolForHeight = -1f;
        private float messageUntil;   // hasta cuándo se mantiene un aviso del pie ("¡Expedición enviada!"…)

        // ---------- cámara ----------
        private Transform camTarget;
        private Vector3 camFrom, camTo;
        private float camTimer = -1f;

        private class RowView
        {
            public RectTransform rt;
            public Image bg, check, checkFill, icon, hungerFill, energyFill;
            public RectTransform hungerRt, energyRt, nameRt, stateRt;
            public TMP_Text name, state;
            public Ant ant;
        }

        /// <summary>El cajón está abierto.</summary>
        public bool IsOpen => isOpen;

        // ================= CICLO =================

        private void Awake()
        {
            root = (RectTransform)transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;
            BuildUI();
            SetOpen(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<AntSpawned>(OnAntChanged);
            EventBus.Subscribe<AntDied>(OnAntDied);
            EventBus.Subscribe<ExpeditionSent>(OnExpeditionSent);
            EventBus.Subscribe<ExpeditionReturned>(OnExpeditionReturned);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<AntSpawned>(OnAntChanged);
            EventBus.Unsubscribe<AntDied>(OnAntDied);
            EventBus.Unsubscribe<ExpeditionSent>(OnExpeditionSent);
            EventBus.Unsubscribe<ExpeditionReturned>(OnExpeditionReturned);
        }

        private void OnAntChanged(AntSpawned e) => dirty = true;
        private void OnAntDied(AntDied e) { picked.Remove(e.Ant); dirty = true; }
        private void OnExpeditionSent(ExpeditionSent e) => dirty = true;
        private void OnExpeditionReturned(ExpeditionReturned e) => dirty = true;

        private void Update()
        {
            UpdateCamera();

            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer > 0f) return;
            refreshTimer = refreshInterval;

            // Red de seguridad: si cambia el número de hormigas sin evento (p. ej. vuelven de expedición), rehacer.
            int antCount = Ant.All.Count, awayCount = AwayAnts().Count;
            if (antCount != lastAntCount || awayCount != lastAwayCount) dirty = true;

            if (dirty) RebuildData();          // también cerrado: mantiene al día el número de la pestaña
            if (!isOpen) return;
            EnsurePool();
            BindRows();
            RefreshFooter();
        }

        // ================= ABRIR / CERRAR =================

        public void Open() => SetOpen(true);
        public void Close() => SetOpen(false);
        public void Toggle() => SetOpen(!isOpen);

        private void SetOpen(bool open)
        {
            isOpen = open;
            blocker.SetActive(open);
            drawer.gameObject.SetActive(open);
            tabButton.gameObject.SetActive(!open);
            if (!open) return;
            transform.SetAsLastSibling();      // por encima del resto del HUD
            float maxW = root.rect.width * 0.92f;
            drawer.sizeDelta = new Vector2(maxW > 0f ? Mathf.Min(drawerWidth, maxW) : drawerWidth, 0f);
            dirty = true;
            refreshTimer = 0f;
            Canvas.ForceUpdateCanvases();
            RebuildData();
            EnsurePool();
            BindRows();
            RefreshFooter();
        }

        // ================= DATOS =================

        private static IReadOnlyList<Ant> AwayAnts()
        {
            var exp = ExpeditionSystem.Instance;
            return exp != null ? exp.AntsAway : (IReadOnlyList<Ant>)Array.Empty<Ant>();
        }

        private static bool IsAway(Ant a)
        {
            var away = AwayAnts();
            for (int i = 0; i < away.Count; i++) if (away[i] == a) return true;
            return false;
        }

        private void RebuildData()
        {
            dirty = false;
            lastAntCount = Ant.All.Count;
            var away = AwayAnts();
            lastAwayCount = away.Count;

            items.Clear();
            int total = 0;
            foreach (var a in Ant.All)
            {
                if (a == null || !a.IsAlive) continue;
                total++;
                if (PassesFilter(a)) items.Add(a);
            }
            foreach (var a in away)
            {
                if (a == null || !a.IsAlive || items.Contains(a) || Ant.All.Contains(a)) continue;
                total++;
                if (PassesFilter(a)) items.Add(a);
            }
            items.Sort(Compare);

            picked.RemoveWhere(a => a == null || !a.IsAlive || IsAway(a));

            tabLabel.text = $"Hormigas\n{total}";
            if (titleText != null) titleText.text = $"Hormigas · {total}";
            if (content != null) content.sizeDelta = new Vector2(0f, items.Count * rowHeight);
        }

        private bool PassesFilter(Ant a) =>
            filter == null || (a.Caste != null && a.Caste.caste == filter.Value);

        private int Compare(Ant x, Ant y)
        {
            // La reina primero; las que están fuera, al final.
            if (x.IsQueen != y.IsQueen) return x.IsQueen ? -1 : 1;
            bool ax = IsAway(x), ay = IsAway(y);
            if (ax != ay) return ax ? 1 : -1;

            int c = 0;
            switch (sort)
            {
                case SortMode.Caste:
                    c = CasteOrder(x).CompareTo(CasteOrder(y));
                    break;
                case SortMode.Hunger:
                    c = Hunger(y).CompareTo(Hunger(x));          // más hambrienta primero
                    break;
            }
            if (c == 0) c = string.Compare(x.Name, y.Name, StringComparison.CurrentCultureIgnoreCase);
            return c;
        }

        private static int CasteOrder(Ant a) => a.Caste != null ? (int)a.Caste.caste : 99;

        private static float Hunger(Ant a) =>
            a.TryGetComponent<IAntNeeds>(out var n) ? n.Hunger01 : 0f;

        private Sprite SpriteFor(Ant a)
        {
            if (a.Caste != null)
                foreach (var cs in casteSprites)
                    if (cs.caste == a.Caste.caste && cs.sprite != null) return cs.sprite;
            return antSprite;
        }

        private static string CasteName(Ant a)
        {
            if (a.IsQueen) return "Reina";
            if (a.Caste == null) return "";
            return !string.IsNullOrEmpty(a.Caste.displayName) ? a.Caste.displayName : a.Caste.caste.ToString();
        }

        private static string AwayText()
        {
            var exp = ExpeditionSystem.Instance;
            if (exp == null) return "En expedición";
            switch (exp.Phase)
            {
                case ExpeditionPhase.Leaving: return "Saliendo de expedición";
                case ExpeditionPhase.Returning: return "Volviendo de expedición";
                default: return "En expedición";
            }
        }

        // ================= FILAS (POOL) =================

        /// <summary>Crea las filas justas para llenar la vista (+2). Se rehace solo si cambia su altura.</summary>
        private void EnsurePool()
        {
            float h = viewport.rect.height;
            if (h <= 0f || Mathf.Approximately(h, poolForHeight)) return;
            poolForHeight = h;
            int needed = Mathf.CeilToInt(h / rowHeight) + 2;
            while (pool.Count < needed) pool.Add(CreateRow());
        }

        private void BindRows()
        {
            if (pool.Count == 0) return;
            float y = content.anchoredPosition.y;
            int first = Mathf.Max(0, Mathf.FloorToInt(y / rowHeight));
            for (int i = 0; i < pool.Count; i++)
            {
                var row = pool[i];
                int index = first + i;
                bool show = index < items.Count;
                row.rt.gameObject.SetActive(show);
                if (!show) { row.ant = null; continue; }
                row.rt.anchoredPosition = new Vector2(0f, -index * rowHeight);
                Bind(row, items[index]);
            }
        }

        private void Bind(RowView row, Ant a)
        {
            row.ant = a;
            bool away = IsAway(a);
            bool canPick = selectionMode && !a.IsQueen;

            row.bg.color = away ? rowAwayColor : rowColor;
            row.check.gameObject.SetActive(canPick);
            row.checkFill.enabled = picked.Contains(a);

            float left = canPick ? 92f : 16f;
            row.icon.rectTransform.anchoredPosition = new Vector2(left, 0f);
            var sp = SpriteFor(a);
            row.icon.sprite = sp;
            row.icon.color = sp != null ? (away ? new Color(1f, 1f, 1f, 0.5f) : Color.white)
                                        : (a.Caste != null ? a.Caste.tint : Color.white);
            row.nameRt.offsetMin = new Vector2(left + 84f, row.nameRt.offsetMin.y);
            row.stateRt.offsetMin = new Vector2(left + 84f, row.stateRt.offsetMin.y);

            string caste = CasteName(a);
            row.name.text = string.IsNullOrEmpty(caste) ? a.Name : $"{a.Name} <color=#{ColorUtility.ToHtmlStringRGB(dimTextColor)}>· {caste}</color>";
            row.name.color = away ? dimTextColor : textColor;
            row.state.text = away ? AwayText() : AntPanelUI.DescribeState(a);

            if (a.TryGetComponent<IAntNeeds>(out var needs))
            {
                SetBar(row.hungerRt, row.hungerFill, needs.Hunger01, needs.IsHungry ? hungryColor : hungerColor);
                SetBar(row.energyRt, row.energyFill, needs.Energy01, needs.IsTired || needs.IsExhausted ? tiredColor : energyColor);
            }
            else
            {
                SetBar(row.hungerRt, row.hungerFill, 0f, hungerColor);
                SetBar(row.energyRt, row.energyFill, 0f, energyColor);
            }
        }

        private static void SetBar(RectTransform fillRt, Image fill, float value, Color color)
        {
            fillRt.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
            fill.color = color;
        }

        private void OnRowClicked(RowView row)
        {
            var a = row.ant;
            if (a == null) return;

            if (selectionMode)
            {
                if (a.IsQueen) return;
                if (picked.Contains(a)) picked.Remove(a);
                else if (CanPick(a, out var why)) picked.Add(a);
                else ShowMessage(why);
                BindRows();
                RefreshFooter();
                return;
            }

            if (!a.gameObject.activeInHierarchy) return;   // fuera del mapa (expedición): no hay nada que enseñar
            Close();
            FocusCamera(a.transform.position);
            if (AntSelection.Instance != null) AntSelection.Instance.Select(a);
        }

        private bool CanPick(Ant a, out string why)
        {
            why = null;
            var exp = ExpeditionSystem.Instance;
            int max = ExpeditionSystem.MaxGroup;
            if (picked.Count >= max) { why = $"Como mucho {max} hormigas por expedición"; return false; }
            if (exp != null && !exp.CanVolunteer(a)) { why = $"{a.Name} no puede ir ahora"; return false; }
            return true;
        }

        // ================= MODO SELECCIÓN Y ENVÍO =================

        private void ToggleSelectionMode()
        {
            selectionMode = !selectionMode;
            if (!selectionMode) picked.Clear();
            footer.SetActive(selectionMode);
            SetButtonActive(selectButton, selectionMode);
            messageUntil = 0f;
            BindRows();
            RefreshFooter();
        }

        private void RefreshFooter()
        {
            if (!selectionMode) return;
            int max = ExpeditionSystem.MaxGroup;
            pickText.text = $"Elegidas: {picked.Count} de {max}";

            string reason = null;
            var exp = ExpeditionSystem.Instance;
            bool ok;
            if (exp == null) { ok = false; reason = "No hay expediciones en esta escena"; }
            else if (picked.Count == 0) { ok = false; }
            else ok = exp.CanSend(picked.Count, out reason);

            sendButton.interactable = ok;
            if (Time.unscaledTime >= messageUntil)
                reasonText.text = reason ?? (picked.Count == 0 ? "Marca hasta " + max + " hormigas" : "");
        }

        /// <summary>Aviso en el pie durante unos segundos (no lo pisa el refresco).</summary>
        private void ShowMessage(string message, float seconds = 2.5f)
        {
            reasonText.text = message;
            messageUntil = Time.unscaledTime + seconds;
        }

        private void SendExpedition()
        {
            var exp = ExpeditionSystem.Instance;
            if (exp == null || picked.Count == 0) return;
            pickBuffer.Clear();
            foreach (var a in items) if (picked.Contains(a)) pickBuffer.Add(a);   // en el orden de la lista
            if (!exp.CanSend(pickBuffer.Count, out var reason)) { ShowMessage(reason); return; }
            if (!exp.Send(pickBuffer)) { ShowMessage("No se ha podido enviar"); return; }

            picked.Clear();
            dirty = true;
            RebuildData();
            BindRows();
            ShowMessage("¡Expedición enviada!");
            RefreshFooter();
        }

        // ================= FILTRO Y ORDEN =================

        private void SetFilter(CasteType? caste)
        {
            filter = caste;
            foreach (var (button, c) in filterButtons) SetButtonActive(button, c == caste);
            ResetScrollAndRebuild();
        }

        private void SetSort(SortMode mode)
        {
            sort = mode;
            foreach (var (button, m) in sortButtons) SetButtonActive(button, m == mode);
            ResetScrollAndRebuild();
        }

        private void ResetScrollAndRebuild()
        {
            RebuildData();
            if (content != null) content.anchoredPosition = Vector2.zero;
            BindRows();
        }

        // ================= CÁMARA (como EventLogUI) =================

        private void FocusCamera(Vector3 p)
        {
            var cam = Camera.main;
            if (cam == null) return;
            camTarget = cam.transform;
            camFrom = camTarget.position;
            camTo = new Vector3(p.x, p.y, camFrom.z);
            camTimer = 0f;
        }

        private void UpdateCamera()
        {
            if (camTimer < 0f || camTarget == null) return;
            camTimer += Time.unscaledDeltaTime;
            float t = focusSeconds <= 0f ? 1f : Mathf.Clamp01(camTimer / focusSeconds);
            t = t * t * (3f - 2f * t);
            camTarget.position = Vector3.Lerp(camFrom, camTo, t);
            if (t >= 1f) camTimer = -1f;
            // CameraController sigue limitando la cámara a los bordes del mapa en su LateUpdate.
        }

        // ================= CONSTRUCCIÓN DE LA INTERFAZ =================

        private void BuildUI()
        {
            // Fondo que cierra al tocar fuera (y evita que el toque llegue al mapa).
            var blockImg = NewImage("Blocker", root, blockerColor);
            Stretch(blockImg.rectTransform);
            var blockBtn = blockImg.gameObject.AddComponent<Button>();
            blockBtn.transition = Selectable.Transition.None;
            blockBtn.onClick.AddListener(Close);
            blocker = blockImg.gameObject;

            // Pestaña del borde izquierdo
            tabButton = NewButton("AntListTab", root, "Hormigas", out tabLabel);
            var trt = (RectTransform)tabButton.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f);
            trt.pivot = new Vector2(0f, 0.5f);
            trt.sizeDelta = tabSize;
            trt.anchoredPosition = new Vector2(0f, tabOffsetY);
            tabButton.onClick.AddListener(Open);

            // Cajón
            var drawerImg = NewImage("Drawer", root, panelColor);
            drawer = drawerImg.rectTransform;
            drawer.anchorMin = new Vector2(0f, 0f); drawer.anchorMax = new Vector2(0f, 1f);
            drawer.pivot = new Vector2(0f, 0.5f);
            drawer.sizeDelta = new Vector2(drawerWidth, 0f);
            drawer.anchoredPosition = Vector2.zero;
            var vlg = drawer.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 16, 16);
            vlg.spacing = 10f;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            // Cabecera: título + ✕
            var header = NewRow("Header", drawer, buttonHeight);
            titleText = NewText("Title", header, "Hormigas", fontSize + 4f, textColor, TextAlignmentOptions.MidlineLeft);
            titleText.fontStyle = FontStyles.Bold;
            AddLayout(titleText.gameObject, flexibleWidth: 1f);
            selectButton = NewButton("SelectButton", header, "Expedición", out var selLabel);
            FitWidth(selectButton, selLabel);
            selectButton.onClick.AddListener(ToggleSelectionMode);
            var close = NewButton("Close", header, "X", out _);
            AddLayout(close.gameObject, preferredWidth: buttonHeight);
            close.onClick.AddListener(Close);

            // Filtro por casta
            var filterRow = NewRow("Filter", drawer, buttonHeight);
            var casteLabel = NewText("FilterLabel", filterRow, "Casta:", fontSize, dimTextColor, TextAlignmentOptions.MidlineLeft);
            AddLayout(casteLabel.gameObject, preferredWidth: 120f);
            AddFilter(filterRow, "Todas", null);
            AddFilter(filterRow, "Menor", CasteType.Minor);
            AddFilter(filterRow, "Media", CasteType.Media);
            AddFilter(filterRow, "Mayor", CasteType.Major);

            // Orden
            var sortRow = NewRow("Sort", drawer, buttonHeight);
            var sortLabel = NewText("SortLabel", sortRow, "Orden:", fontSize, dimTextColor, TextAlignmentOptions.MidlineLeft);
            AddLayout(sortLabel.gameObject, preferredWidth: 120f);
            AddSort(sortRow, "Casta", SortMode.Caste);
            AddSort(sortRow, "Nombre", SortMode.Name);
            AddSort(sortRow, "Hambre", SortMode.Hunger);

            // Lista con desplazamiento
            BuildScroll();

            // Pie (solo en modo selección)
            footer = new GameObject("Footer", typeof(RectTransform));
            footer.transform.SetParent(drawer, false);
            AddLayout(footer, flexibleHeight: 0f);
            var fvlg = footer.AddComponent<VerticalLayoutGroup>();
            fvlg.spacing = 6f; fvlg.childControlWidth = true; fvlg.childControlHeight = true;
            fvlg.childForceExpandWidth = true; fvlg.childForceExpandHeight = false;
            var info = NewRow("Info", footer.transform, fontSize * 1.4f);
            pickText = NewText("Picked", info, "", fontSize, textColor, TextAlignmentOptions.MidlineLeft);
            AddLayout(pickText.gameObject, flexibleWidth: 1f);
            reasonText = NewText("Reason", footer.transform, "", fontSize, warningColor, TextAlignmentOptions.MidlineLeft);
            AddLayout(reasonText.gameObject, preferredHeight: fontSize * 1.4f);
            sendButton = NewButton("Send", footer.transform, "Enviar a expedición", out _);
            AddLayout(sendButton.gameObject, preferredHeight: buttonHeight);
            sendButton.onClick.AddListener(SendExpedition);
            footer.SetActive(false);

            SetFilter(null);
            SetSort(SortMode.Caste);
        }

        private void BuildScroll()
        {
            var scrollImg = NewImage("List", drawer, new Color(0f, 0f, 0f, 0.15f));
            AddLayout(scrollImg.gameObject, flexibleHeight: 1f);
            scroll = scrollImg.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.inertia = true;

            var vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport = (RectTransform)vpGo.transform;
            viewport.SetParent(scrollImg.transform, false);
            Stretch(viewport);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            content = (RectTransform)contentGo.transform;
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.onValueChanged.AddListener(_ => BindRows());
        }

        private RowView CreateRow()
        {
            var row = new RowView();
            var bg = NewImage("Row", content, rowColor);
            row.bg = bg;
            row.rt = bg.rectTransform;
            row.rt.anchorMin = new Vector2(0f, 1f); row.rt.anchorMax = new Vector2(1f, 1f);
            row.rt.pivot = new Vector2(0.5f, 1f);
            row.rt.sizeDelta = new Vector2(0f, rowHeight - 6f);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() => OnRowClicked(row));

            // Casilla (modo selección)
            row.check = NewImage("Check", row.rt, barBackColor);
            var crt = row.check.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0f, 0.5f); crt.pivot = new Vector2(0f, 0.5f);
            crt.sizeDelta = new Vector2(64f, 64f); crt.anchoredPosition = new Vector2(16f, 0f);
            row.check.raycastTarget = false;
            row.checkFill = NewImage("Tick", crt, activeColor);
            var tick = row.checkFill.rectTransform;
            Stretch(tick); tick.offsetMin = new Vector2(10f, 10f); tick.offsetMax = new Vector2(-10f, -10f);
            row.checkFill.raycastTarget = false;

            // Sprite
            row.icon = NewImage("Icon", row.rt, Color.white);
            var irt = row.icon.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f); irt.pivot = new Vector2(0f, 0.5f);
            irt.sizeDelta = new Vector2(72f, 72f);
            row.icon.preserveAspect = true;
            row.icon.raycastTarget = false;

            // Nombre + casta (arriba) y estado (abajo); a la derecha quedan las barras
            const float barsWidth = 190f;
            row.name = NewText("Name", row.rt, "", fontSize, textColor, TextAlignmentOptions.BottomLeft);
            row.nameRt = row.name.rectTransform;
            row.nameRt.anchorMin = new Vector2(0f, 0.5f); row.nameRt.anchorMax = new Vector2(1f, 1f);
            row.nameRt.offsetMin = new Vector2(100f, 0f); row.nameRt.offsetMax = new Vector2(-(barsWidth + 24f), -4f);
            row.state = NewText("State", row.rt, "", fontSize, dimTextColor, TextAlignmentOptions.TopLeft);
            row.stateRt = row.state.rectTransform;
            row.stateRt.anchorMin = new Vector2(0f, 0f); row.stateRt.anchorMax = new Vector2(1f, 0.5f);
            row.stateRt.offsetMin = new Vector2(100f, 4f); row.stateRt.offsetMax = new Vector2(-(barsWidth + 24f), 0f);

            // Barras de hambre (arriba) y energía (abajo)
            row.hungerFill = NewBar(row.rt, "Hunger", barsWidth, 30f, out row.hungerRt);
            row.energyFill = NewBar(row.rt, "Energy", barsWidth, 52f, out row.energyRt);

            return row;
        }

        private Image NewBar(RectTransform parent, string name, float width, float fromTop, out RectTransform fillRt)
        {
            var back = NewImage(name + "Bar", parent, barBackColor);
            var brt = back.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f); brt.pivot = new Vector2(1f, 1f);
            brt.sizeDelta = new Vector2(width, 12f);
            brt.anchoredPosition = new Vector2(-16f, -fromTop);
            back.raycastTarget = false;
            var fill = NewImage("Fill", brt, Color.white);
            fillRt = fill.rectTransform;
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
            fill.raycastTarget = false;
            return fill;
        }

        private void AddFilter(RectTransform rowRt, string label, CasteType? caste)
        {
            var b = NewButton("Filter_" + label, rowRt, label, out var t);
            FitWidth(b, t);
            b.onClick.AddListener(() => SetFilter(caste));
            filterButtons.Add((b, caste));
        }

        private void AddSort(RectTransform rowRt, string label, SortMode mode)
        {
            var b = NewButton("Sort_" + label, rowRt, label, out var t);
            FitWidth(b, t);
            b.onClick.AddListener(() => SetSort(mode));
            sortButtons.Add((b, mode));
        }

        private void SetButtonActive(Button b, bool active)
        {
            if (b == null) return;
            if (b.targetGraphic != null) b.targetGraphic.color = active ? activeColor : buttonColor;
            var t = b.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.color = active ? new Color(0.15f, 0.1f, 0.05f) : textColor;
        }

        // ---------- utilidades ----------

        /// <summary>El botón mide lo que su texto (+ margen), no se estira.</summary>
        private void FitWidth(Button b, TMP_Text label)
        {
            float w = label.GetPreferredValues(label.text, 1000f, buttonHeight).x + 48f;
            AddLayout(b.gameObject, preferredWidth: Mathf.Max(buttonHeight, w));
        }

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

        private TMP_Text NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
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
            text.textWrappingMode = TextWrappingModes.Normal;
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
            AddLayout(go, preferredHeight: height, flexibleHeight: 0f);   // el hueco sobrante es para la lista
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
