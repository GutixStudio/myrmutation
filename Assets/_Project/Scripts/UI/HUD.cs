using Myrmutation.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Myrmutation.UI
{
    /// <summary>
    /// HUD de la partida: recursos, día/estación, pausa y velocidades, y botón Salir.
    /// Se actualiza con eventos (no lee nada cada frame salvo la barra del día).
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
            var rm = ResourceManager.Instance;
            if (rm != null)
                foreach (ResourceType t in System.Enum.GetValues(typeof(ResourceType)))
                    OnResource(new ResourceChanged(t, rm.Get(t)));
            RefreshDay();
            RefreshSpeedButtons();
        }

        private void Update()
        {
            if (dayProgress != null && GameManager.Instance != null)
                dayProgress.fillAmount = GameManager.Instance.DayProgress;
        }

        // ---------- Eventos ----------
        private void OnResource(ResourceChanged e)
        {
            var target = e.Type == ResourceType.Food ? foodText
                       : e.Type == ResourceType.Leaves ? leavesText
                       : e.Type == ResourceType.Fungus ? fungusText
                       : biomassText;
            if (target != null) target.text = $"{Label(e.Type)}: {e.Amount}";
        }

        private void OnDay(DayPassed e) => RefreshDay();
        private void OnSeason(SeasonChanged e) => RefreshDay();
        private void OnState(GameStateChanged e) => RefreshSpeedButtons();

        private void RefreshDay()
        {
            var gm = GameManager.Instance;
            if (dayText == null || gm == null) return;
            dayText.text = $"Día {gm.Day} · {SeasonName(gm.CurrentSeason)}";
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
