using UnityEngine;

namespace Myrmutation.Core
{
    /// <summary>
    /// Estado de la partida, velocidad del tiempo y calendario (días y estaciones).
    /// Usa Time.timeScale: cualquier sistema que use Time.deltaTime se pausa/acelera solo.
    /// La UI debe usar Time.unscaledDeltaTime.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        /// <summary>Resultado de la última partida, para que la pantalla de Game Over lo lea.</summary>
        public static GameState LastResult { get; private set; } = GameState.GameOver;

        [Header("Calendario")]
        [SerializeField] private float secondsPerDay = 20f;
        [SerializeField] private int daysPerSeason = 5;
        [Tooltip("Al terminar esta estación (Invierno) se gana la partida")]
        [SerializeField] private Season finalSeason = Season.Winter;

        public GameState State { get; private set; } = GameState.Playing;
        public int Speed { get; private set; } = 1;          // 0 = pausa, 1, 2, 3
        public int Day { get; private set; } = 1;
        public Season CurrentSeason { get; private set; } = Season.Spring;
        public float DayProgress => dayTimer / secondsPerDay; // 0..1, útil para la UI

        private float dayTimer;
        private int seasonIndex;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            if (Instance == this) { Instance = null; Time.timeScale = 1f; }
        }

        private void Update()
        {
            if (State != GameState.Playing) return;

            dayTimer += Time.deltaTime;
            if (dayTimer < secondsPerDay) return;

            dayTimer -= secondsPerDay;
            Day++;
            EventBus.Publish(new DayPassed(Day));

            if ((Day - 1) % daysPerSeason == 0) AdvanceSeason();
        }

        private void AdvanceSeason()
        {
            if (CurrentSeason == finalSeason) { Win(); return; }
            seasonIndex++;
            CurrentSeason = (Season)(seasonIndex % 4);
            EventBus.Publish(new SeasonChanged(CurrentSeason, seasonIndex));
        }

        // ---------- Velocidad ----------
        public void SetSpeed(int speed)
        {
            if (State == GameState.Victory || State == GameState.GameOver) return;
            Speed = Mathf.Clamp(speed, 0, 3);
            Time.timeScale = Speed;
            SetState(Speed == 0 ? GameState.Paused : GameState.Playing);
        }

        public void TogglePause() => SetSpeed(State == GameState.Paused ? 1 : 0);

        // ---------- Fin de partida ----------
        public void Win() => EndGame(GameState.Victory);
        public void Lose() => EndGame(GameState.GameOver);
        /// <summary>Botón "Salir" durante la partida: el enunciado pide mostrar Game Over.</summary>
        public void QuitToGameOver() => EndGame(GameState.GameOver);

        private void EndGame(GameState result)
        {
            if (State == GameState.Victory || State == GameState.GameOver) return;
            LastResult = result;
            Time.timeScale = 1f;
            SetState(result);
            // La UI (tarea E3) escucha GameStateChanged y carga la pantalla correspondiente.
        }

        private void SetState(GameState s)
        {
            if (State == s) return;
            State = s;
            EventBus.Publish(new GameStateChanged(s));
        }
    }
}
