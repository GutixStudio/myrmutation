using System.Collections;
using Myrmutation.Core;
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
            RunSummary.AntsAlive = Ant.All.Count;
            StartCoroutine(GoToGameOver());
        }

        private IEnumerator GoToGameOver()
        {
            yield return new WaitForSecondsRealtime(delay);
            SceneLoader.Load(SceneLoader.GameOver);
        }
    }
}
