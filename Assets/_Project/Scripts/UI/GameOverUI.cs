using Myrmutation.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Myrmutation.UI
{
    /// <summary>Pantalla de fin de partida: aparece al perder, al ganar y al pulsar Salir.</summary>
    public class GameOverUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;

        private void Awake()
        {
            Time.timeScale = 1f;
            if (retryButton != null) retryButton.onClick.AddListener(() => { RunSummary.QuitByPlayer = false; SceneLoader.Load(SceneLoader.Game); });
            if (menuButton != null) menuButton.onClick.AddListener(() => SceneLoader.Load(SceneLoader.MainMenu));
        }

        private void Start()
        {
            bool won = RunSummary.Result == GameState.Victory;
            if (titleText != null)
                titleText.text = won ? "¡La colonia ha sobrevivido!"
                              : RunSummary.QuitByPlayer ? "Has abandonado la colonia"
                              : "La reina ha caído";

            if (summaryText != null)
                summaryText.text = $"Días: {RunSummary.Day}\nEstación: {SeasonName(RunSummary.Season)}\nGlotonas vivas: {RunSummary.AntsAlive}";
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
