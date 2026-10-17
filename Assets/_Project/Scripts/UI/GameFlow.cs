using System.Collections;
using Myrmutation.Core;
using Myrmutation.Expedition;
using UnityEngine;

namespace Myrmutation.UI
{
    /// <summary>
    /// Va en la escena Game. Cuando la partida termina (victoria, derrota o botón Salir),
    /// guarda el resumen y carga la escena GameOver tras una pequeña pausa.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        [Tooltip("Segundos antes de cambiar a la pantalla de Game Over")]
        [SerializeField] private float delay = 1.2f;

        private bool ending;

        private void OnEnable() => EventBus.Subscribe<GameStateChanged>(OnStateChanged);
        private void OnDisable() => EventBus.Unsubscribe<GameStateChanged>(OnStateChanged);

        /// <summary>Para el botón Salir del HUD: termina la partida como abandono.</summary>
        public void QuitGame()
        {
            RunSummary.QuitByPlayer = true;
            if (GameManager.Instance != null) GameManager.Instance.QuitToGameOver();
            else StartCoroutine(GoToGameOver());
        }

        private void OnStateChanged(GameStateChanged e)
        {
            if (ending) return;
            if (e.State != GameState.Victory && e.State != GameState.GameOver) return;
            ending = true;

            var gm = GameManager.Instance;
            RunSummary.Result = e.State;
            RunSummary.Day = gm != null ? gm.Day : 0;
            RunSummary.Season = gm != null ? gm.CurrentSeason : Season.Spring;
            RunSummary.AntsAlive = CountWorkers();
            RunSummary.Biomass = ResourceManager.Instance != null ? ResourceManager.Instance.Get(ResourceType.Biomass) : 0;
            StartCoroutine(GoToGameOver());
        }

        // Obreras vivas, sin la reina y contando las que están de expedición (desactivadas, fuera de Ant.All).
        private static int CountWorkers()
        {
            int n = 0;
            foreach (var a in Ant.All) if (a != null && a.IsAlive && !a.IsQueen) n++;
            var exp = ExpeditionSystem.Instance;
            return exp != null ? n + exp.AntsAway.Count : n;
        }

        private IEnumerator GoToGameOver()
        {
            yield return new WaitForSecondsRealtime(delay);
            SceneLoader.Load(SceneLoader.GameOver);
        }
    }
}
