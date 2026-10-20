using System.Collections.Generic;
using System.Globalization;
using Myrmutation.Core;
using Myrmutation.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Myrmutation.UI
{
    /// <summary>
    /// HUD de la partida: recursos, día/estación, pausa y velocidades, y botón Salir.
    /// Los recursos y el día se actualizan con eventos; la barra del día, cada frame.
    ///
    /// E12 · Datos de gestión a la vista (leídos de ColonyStats, D6):
    ///  · Debajo de cada recurso, su ritmo neto ("+8/min" verde, "-3/min" rojo). Tocar el recurso → desglose.
    ///  · Debajo del día, la previsión: "Invierno en 3 días · comida para 4" (verde si cubre el invierno).
    ///  · Población: "Obreras 7 (2 libres)" bajo la barra superior, a la derecha. Tocarla → castas.
    /// Ritmos, previsión y población se recalculan cada 'statsRefreshInterval' segundos reales.
    /// Lo nuevo se crea por código: no hace falta tocar la escena. Si no hay ColonyStats en la escena,
    /// el HUD se ve como antes.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [Header("Recursos")]
        [SerializeField] private TMP_Text foodText;
        [SerializeField] private TMP_Text leavesText;
        [SerializeField] private TMP_Text fungusText;
        [SerializeField] private TMP_Text biomassText;

        [Header("Tiempo")]
        [SerializeField] private TMP_Text dayText;
        [Tooltip("Opcional: Image con Image Type = Filled para ver el avance del día")]
        [SerializeField] private Image dayProgress;

        [Header("Velocidad")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button speed1Button;
        [SerializeField] private Button speed2Button;
        [SerializeField] private Button speed3Button;
        [SerializeField] private Color activeColor = new Color(0.95f, 0.8f, 0.3f);
        [SerializeField] private Color normalColor = Color.white;

        [Header("Salir")]
        [SerializeField] private Button quitButton;
        [SerializeField] private GameFlow gameFlow;

        [Header("E12 · Datos de gestión")]
        [Tooltip("Opcional: texto de población. Si se deja vacío, se crea solo bajo la barra superior, a la derecha")]
        [SerializeField] private TMP_Text populationText;
        [Tooltip("Tamaño de las líneas de ritmo y previsión y de los desgloses (mínimo en móvil: 32)")]
        [SerializeField] private float detailFontSize = 32f;
        [Tooltip("Alto mínimo de lo que se puede tocar: recursos y población (mínimo en móvil: 88)")]
        [SerializeField] private float minTouchHeight = 88f;
        [Tooltip("Ancho del texto del día, que lleva debajo la previsión de invierno")]
        [SerializeField] private float dayTextWidth = 640f;
        [Tooltip("Separación máxima entre elementos de la barra superior (con 60 no cabe la previsión)")]
        [SerializeField] private float topBarSpacing = 40f;
        [Tooltip("Cada cuántos segundos reales se recalculan ritmos, previsión y población")]
        [SerializeField] private float statsRefreshInterval = 0.5f;
        [Tooltip("Segundos que se queda abierto un desglose si no se vuelve a tocar")]
        [SerializeField] private float detailAutoClose = 6f;
        [SerializeField] private Color positiveColor = new Color(0.45f, 0.85f, 0.4f);
        [SerializeField] private Color negativeColor = new Color(1f, 0.42f, 0.36f);
        [SerializeField] private Color neutralColor = new Color(0.75f, 0.75f, 0.75f);
        [SerializeField] private Color panelColor = new Color(0.12f, 0.08f, 0.06f, 0.92f);

        private const int PopulationKey = 100;   // clave del desglose de población (los recursos usan su ResourceType)

        private readonly Dictionary<ResourceType, int> amounts = new Dictionary<ResourceType, int>();
        private GameObject populationChip;        // solo si lo crea el HUD
        private RectTransform detailPanel;
        private TMP_Text detailBody;
        private RectTransform detailAnchor;
        private int detailKey = -1;
        private float detailTimer;
        private float statsTimer;

        private void Awake()
        {
            if (pauseButton != null) pauseButton.onClick.AddListener(() => SetSpeed(0));
            if (speed1Button != null) speed1Button.onClick.AddListener(() => SetSpeed(1));
            if (speed2Button != null) speed2Button.onClick.AddListener(() => SetSpeed(2));
            if (speed3Button != null) speed3Button.onClick.AddListener(() => SetSpeed(3));
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ResourceChanged>(OnResource);
            EventBus.Subscribe<DayPassed>(OnDay);
            EventBus.Subscribe<SeasonChanged>(OnSeason);
            EventBus.Subscribe<GameStateChanged>(OnState);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ResourceChanged>(OnResource);
            EventBus.Unsubscribe<DayPassed>(OnDay);
            EventBus.Unsubscribe<SeasonChanged>(OnSeason);
            EventBus.Unsubscribe<GameStateChanged>(OnState);
        }

        private void Start()
        {
            BuildManagementUI();

            var rm = ResourceManager.Instance;
            if (rm != null)
                foreach (ResourceType t in System.Enum.GetValues(typeof(ResourceType)))
                    OnResource(new ResourceChanged(t, rm.Get(t)));
            RefreshDay();
            RefreshPopulation();
            RefreshSpeedButtons();
        }

        private void Update()
        {
            if (dayProgress != null && GameManager.Instance != null)
                dayProgress.fillAmount = GameManager.Instance.DayProgress;

            // Tiempo real: el desglose se cierra y los datos se refrescan también en pausa.
            statsTimer -= Time.unscaledDeltaTime;
            if (statsTimer <= 0f)
            {
                statsTimer = statsRefreshInterval;
                RefreshStats();
            }

            if (detailPanel != null && detailPanel.gameObject.activeSelf)
            {
                detailTimer -= Time.unscaledDeltaTime;
                if (detailTimer <= 0f) HideDetail();
            }
        }

        // ---------- Eventos ----------
        private void OnResource(ResourceChanged e)
        {
            amounts[e.Type] = e.Amount;
            RefreshResource(e.Type);
        }

        private void OnDay(DayPassed e) => RefreshDay();
        private void OnSeason(SeasonChanged e) => RefreshDay();
        private void OnState(GameStateChanged e) => RefreshSpeedButtons();

        private void RefreshDay()
        {
            var gm = GameManager.Instance;
            if (dayText == null || gm == null) return;

            string s = $"Día {gm.Day} · {SeasonName(gm.CurrentSeason)}";
            var cs = ColonyStats.Instance;
            if (cs != null)
            {
                string line = WinterLine(gm, cs, out bool covered);
                s += "\n" + Small(Colored(line, covered ? positiveColor : negativeColor));
            }
            dayText.text = s;
        }

        // ---------- E12 · Ritmos, previsión y población ----------
        private void RefreshStats()
        {
            foreach (ResourceType t in System.Enum.GetValues(typeof(ResourceType)))
                RefreshResource(t);
            RefreshDay();
            RefreshPopulation();
            if (detailPanel != null && detailPanel.gameObject.activeSelf) RefreshDetail();
        }

        private void RefreshResource(ResourceType type)
        {
            var target = TextFor(type);
            if (target == null) return;

            amounts.TryGetValue(type, out int amount);
            string s = $"{Label(type)}: {amount}";
            var cs = ColonyStats.Instance;
            if (cs != null) s += "\n" + Small(RateText(cs.NetPerMinute(type)));
            target.text = s;
        }

        /// <summary>"Invierno en 3 días · comida para 4". Covered = la comida actual aguanta lo que queda de invierno.</summary>
        private static string WinterLine(GameManager gm, ColonyStats cs, out bool covered)
        {
            int dps = cs.DaysPerSeason;
            float days = cs.WinterDaysCovered;
            string food = float.IsInfinity(days) ? "sin consumo" : $"comida para {DaysCovered(days)}";

            if (gm.CurrentSeason == Season.Winter)
            {
                int left = dps - (gm.Day - 1) % dps;   // días de invierno que quedan, contando hoy
                covered = days >= left;
                return $"Invierno: {left} {DayWord(left)} más · {food}";
            }

            int until = cs.DaysUntilWinter;
            covered = days >= dps;
            return $"Invierno en {until} {DayWord(until)} · {food}";
        }

        private void RefreshPopulation()
        {
            if (populationText == null) return;
            var cs = ColonyStats.Instance;
            if (populationChip != null) populationChip.SetActive(cs != null);
            if (cs == null) { populationText.text = ""; return; }

            int workers = cs.Population - cs.Queens;
            int free = cs.FreeWorkers;
            populationText.text = $"Obreras {workers} ({free} {(free == 1 ? "libre" : "libres")})";
        }

        // ---------- E12 · Desglose al tocar ----------
        private void ToggleDetail(int key, RectTransform anchor)
        {
            if (detailPanel == null || anchor == null) return;
            if (detailPanel.gameObject.activeSelf && detailKey == key) { HideDetail(); return; }

            detailKey = key;
            detailAnchor = anchor;
            detailTimer = detailAutoClose;
            detailPanel.gameObject.SetActive(true);
            detailPanel.SetAsLastSibling();   // por encima del resto del HUD
            RefreshDetail();
            PlaceDetail();
        }

        private void HideDetail()
        {
            if (detailPanel != null) detailPanel.gameObject.SetActive(false);
            detailKey = -1;
        }

        private void RefreshDetail()
        {
            if (detailBody == null) return;
            var cs = ColonyStats.Instance;
            if (cs == null) { detailBody.text = "Sin datos de la colonia"; return; }

            if (detailKey == PopulationKey)
            {
                string s = "<b>Población</b>"
                         + $"\nMenores: {cs.CountByCaste(CasteType.Minor)}"
                         + $"\nMedias: {cs.CountByCaste(CasteType.Media)}"
                         + $"\nMayores: {cs.CountByCaste(CasteType.Major)}";
                if (cs.WithoutCaste > 0) s += $"\nSin casta: {cs.WithoutCaste}";
                s += $"\n\nTrabajando: {cs.AssignedWorkers}"
                   + $"\nLibres: {cs.FreeWorkers}";
                if (cs.OnExpedition > 0) s += $"\nDe expedición: {cs.OnExpedition}";
                if (cs.Queens > 0) s += $"\nReina: {cs.Queens}";
                detailBody.text = s;
                return;
            }

            var type = (ResourceType)detailKey;
            float produced = cs.Produced(type);
            float consumed = cs.Consumed(type);
            detailBody.text = $"<b>{Label(type)}</b> (último minuto)"
                            + $"\nProducido: {Colored("+" + Num(produced) + "/min", produced > 0f ? positiveColor : neutralColor)}"
                            + $"\nConsumido: {Colored("-" + Num(consumed) + "/min", consumed > 0f ? negativeColor : neutralColor)}"
                            + $"\nNeto: {RateText(produced - consumed)}";
        }

        /// <summary>Coloca el desglose justo debajo de lo que se ha tocado (alineado a la derecha si está en la mitad derecha).</summary>
        private void PlaceDetail()
        {
            var hud = transform as RectTransform;
            if (hud == null || detailAnchor == null) return;

            var corners = new Vector3[4];   // 0 abajo-izq, 3 abajo-der
            detailAnchor.GetWorldCorners(corners);
            Vector2 bl = hud.InverseTransformPoint(corners[0]);
            Vector2 br = hud.InverseTransformPoint(corners[3]);
            bool right = (bl.x + br.x) * 0.5f > hud.rect.center.x;

            detailPanel.anchorMin = detailPanel.anchorMax = new Vector2(0.5f, 0.5f);
            detailPanel.pivot = new Vector2(right ? 1f : 0f, 1f);
            detailPanel.anchoredPosition = (right ? br : bl) - hud.rect.center + new Vector2(0f, -8f);
        }

        // ---------- E12 · Construcción de la UI por código ----------
        private void BuildManagementUI()
        {
            foreach (ResourceType t in System.Enum.GetValues(typeof(ResourceType)))
            {
                var text = TextFor(t);
                if (text == null) continue;
                var type = t;   // copia para el lambda
                EnsureMinHeight(text.gameObject);
                MakeTappable(text.gameObject, text, () => ToggleDetail((int)type, text.rectTransform));
            }

            // Hueco para la previsión de invierno debajo del día.
            if (dayText != null)
            {
                dayText.textWrappingMode = TextWrappingModes.NoWrap;
                var le = dayText.GetComponent<LayoutElement>();
                if (le != null) le.preferredWidth = Mathf.Max(le.preferredWidth, dayTextWidth);
                var bar = dayText.transform.parent != null ? dayText.transform.parent.GetComponent<HorizontalLayoutGroup>() : null;
                if (bar != null) bar.spacing = Mathf.Min(bar.spacing, topBarSpacing);
            }

            if (populationText == null) populationText = CreatePopulationChip();
            else
            {
                EnsureMinHeight(populationText.gameObject);
                MakeTappable(populationText.gameObject, populationText, () => ToggleDetail(PopulationKey, populationText.rectTransform));
            }

            CreateDetailPanel();
        }

        /// <summary>Cajita "Obreras 7 (2 libres)" bajo la barra superior, a la derecha (arriba a la izquierda va el log de E11).</summary>
        private TMP_Text CreatePopulationChip()
        {
            var hud = transform as RectTransform;
            if (hud == null) return null;

            var go = new GameObject("PopulationChip", typeof(RectTransform), typeof(Image),
                                    typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
            var rt = (RectTransform)go.transform;
            rt.SetParent(hud, false);
            rt.SetAsFirstSibling();   // los paneles del HUD se dibujan por encima
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -(TopBarBottom(hud) + 12f));

            go.GetComponent<Image>().color = panelColor;
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 8, 8);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            go.GetComponent<LayoutElement>().minHeight = minTouchHeight;

            var text = CreateText("Text", rt, dayText != null ? dayText.fontSize : 36f);
            text.alignment = TextAlignmentOptions.Center;

            populationChip = go;
            MakeTappable(go, go.GetComponent<Image>(), () => ToggleDetail(PopulationKey, rt));
            return text;
        }

        private void CreateDetailPanel()
        {
            var hud = transform as RectTransform;
            if (hud == null) return;

            var go = new GameObject("HudDetail", typeof(RectTransform), typeof(Image),
                                    typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            detailPanel = (RectTransform)go.transform;
            detailPanel.SetParent(hud, false);

            go.GetComponent<Image>().color = panelColor;
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 16, 16);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            detailBody = CreateText("Text", detailPanel, detailFontSize);
            detailBody.alignment = TextAlignmentOptions.TopLeft;

            MakeTappable(go, go.GetComponent<Image>(), HideDetail);   // tocar el desglose lo cierra
            go.SetActive(false);
        }

        private TMP_Text CreateText(string name, RectTransform parent, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (dayText != null)
            {
                text.font = dayText.font;
                text.fontSharedMaterial = dayText.fontSharedMaterial;
                text.color = dayText.color;
            }
            text.fontSize = size;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;   // el toque lo recoge el fondo
            return text;
        }

        /// <summary>Distancia desde el borde superior del HUD hasta el borde inferior de la barra superior.</summary>
        private float TopBarBottom(RectTransform hud)
        {
            var bar = dayText != null ? dayText.transform.parent as RectTransform : null;
            if (bar == null) return 100f;
            var corners = new Vector3[4];
            bar.GetWorldCorners(corners);
            return hud.rect.yMax - hud.InverseTransformPoint(corners[0]).y;
        }

        private void EnsureMinHeight(GameObject go)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.minHeight = Mathf.Max(le.minHeight, minTouchHeight);
        }

        private static void MakeTappable(GameObject go, Graphic target, UnityAction onTap)
        {
            var button = go.GetComponent<Button>();
            if (button == null) button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = target;
            button.onClick.AddListener(onTap);
        }

        // ---------- Botones ----------
        private void SetSpeed(int s)
        {
            if (GameManager.Instance != null) GameManager.Instance.SetSpeed(s);
            RefreshSpeedButtons();
        }

        private void Quit()
        {
            if (gameFlow != null) gameFlow.QuitGame();
            else if (GameManager.Instance != null) GameManager.Instance.QuitToGameOver();
        }

        private void RefreshSpeedButtons()
        {
            int s = GameManager.Instance != null ? GameManager.Instance.Speed : 1;
            Tint(pauseButton, s == 0);
            Tint(speed1Button, s == 1);
            Tint(speed2Button, s == 2);
            Tint(speed3Button, s == 3);
        }

        private void Tint(Button b, bool active)
        {
            if (b == null || b.image == null) return;
            b.image.color = active ? activeColor : normalColor;
        }

        // ---------- Formato ----------
        private TMP_Text TextFor(ResourceType t)
        {
            return t == ResourceType.Food ? foodText
                 : t == ResourceType.Leaves ? leavesText
                 : t == ResourceType.Fungus ? fungusText
                 : biomassText;
        }

        /// <summary>"+8/min" en verde, "-3/min" en rojo o "0/min" en gris.</summary>
        private string RateText(float perMinute)
        {
            string n = Num(Mathf.Abs(perMinute));
            if (n == "0") return Colored("0/min", neutralColor);
            return perMinute > 0f ? Colored("+" + n + "/min", positiveColor)
                                  : Colored("-" + n + "/min", negativeColor);
        }

        /// <summary>Entero a partir de 10; por debajo, un decimal con coma ("2,5").</summary>
        private static string Num(float value)
        {
            if (value >= 10f) return Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);
            return (Mathf.Round(value * 10f) / 10f).ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',');
        }

        private static string DaysCovered(float days)
        {
            int n = Mathf.FloorToInt(days);
            return n > 99 ? "99+" : n.ToString(CultureInfo.InvariantCulture);
        }

        private static string DayWord(int n) => n == 1 ? "día" : "días";

        private string Small(string s) => $"<size={Mathf.RoundToInt(detailFontSize)}>{s}</size>";

        private static string Colored(string s, Color c) => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{s}</color>";

        private static string Label(ResourceType t)
        {
            switch (t)
            {
                case ResourceType.Food: return "Comida";
                case ResourceType.Leaves: return "Hojas";
                case ResourceType.Fungus: return "Hongo";
                default: return "Biomasa";
            }
        }

        private static string SeasonName(Season s)
        {
            switch (s)
            {
                case Season.Spring: return "Primavera";
                case Season.Summer: return "Verano";
                case Season.Autumn: return "Otoño";
                default: return "Invierno";
            }
        }
    }
}
