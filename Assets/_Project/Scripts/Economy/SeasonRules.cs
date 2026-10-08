using System.Collections.Generic;
using Myrmutation.Core;
using UnityEngine;

namespace Myrmutation.Economy
{
    /// <summary>
    /// Reglas de las estaciones (D3). El calendario y la victoria los lleva el GameManager (A1):
    /// al terminar el Invierno llama a Win(), y Ant.Kill() de la reina llama a Lose().
    /// Este componente añade lo propio del invierno:
    ///  - Mantenimiento diario de comida por hormiga (mayor en invierno).
    ///  - Multiplicador de hambre que puede leer AntNeeds (B3).
    ///  - Bloqueo de expediciones (lo consulta ExpeditionSystem, D4).
    ///  - Red de seguridad: si muere la reina → Game Over.
    /// Va en la escena Game, junto al GameManager.
    /// </summary>
    public class SeasonRules : MonoBehaviour
    {
        public static SeasonRules Instance { get; private set; }

        [Header("Mantenimiento diario (comida por hormiga viva y día)")]
        [Tooltip("Primavera, Verano, Otoño, Invierno")]
        [SerializeField] private int[] foodPerAntPerDay = { 0, 0, 0, 2 };
        [Tooltip("La reina también paga mantenimiento")]
        [SerializeField] private bool queenPaysUpkeep = true;
        [Tooltip("Si no hay comida para pagar el mantenimiento, muere de hambre una obrera por cada día impagado")]
        [SerializeField] private bool starveIfNotEnoughFood = true;

        [Header("Hambre (multiplicador para AntNeeds)")]
        [Tooltip("Primavera, Verano, Otoño, Invierno")]
        [SerializeField] private float[] hungerMultiplier = { 1f, 1f, 1f, 1.5f };

        [Header("Expediciones")]
        [SerializeField] private bool expeditionsInWinter = false;

        // ---------- API estática (segura aunque no haya SeasonRules en la escena) ----------

        public static Season CurrentSeason =>
            GameManager.Instance != null ? GameManager.Instance.CurrentSeason : Season.Spring;

        public static bool IsWinter => CurrentSeason == Season.Winter;

        /// <summary>D4 lo consulta antes de enviar una expedición.</summary>
        public static bool ExpeditionsAllowed =>
            Instance == null ? !IsWinter : (!IsWinter || Instance.expeditionsInWinter);

        /// <summary>B3 (AntNeeds): hambre += Stats.hungerRate * HungerMultiplier * deltaTime.</summary>
        public static float HungerMultiplier =>
            Instance == null ? 1f : Instance.GetSeasonValue(Instance.hungerMultiplier, 1f);

        /// <summary>Comida que se cobrará al pasar el día (para mostrarlo en el HUD si se quiere).</summary>
        public static int UpkeepToday => Instance == null ? 0 : Instance.CalculateUpkeep();

        // ---------- Ciclo de vida ----------

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void OnEnable()
        {
            EventBus.Subscribe<DayPassed>(OnDayPassed);
            EventBus.Subscribe<SeasonChanged>(OnSeasonChanged);
            EventBus.Subscribe<AntDied>(OnAntDied);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DayPassed>(OnDayPassed);
            EventBus.Unsubscribe<SeasonChanged>(OnSeasonChanged);
            EventBus.Unsubscribe<AntDied>(OnAntDied);
        }

        // ---------- Eventos ----------

        private void OnDayPassed(DayPassed e)
        {
            if (!IsPlaying()) return;
            PayUpkeep();
        }

        private void OnSeasonChanged(SeasonChanged e)
        {
            if (e.Season == Season.Winter)
                Debug.Log($"[SeasonRules] Llega el invierno: mantenimiento {GetSeasonValue(foodPerAntPerDay, 0)} comida/hormiga/día, " +
                          $"hambre x{HungerMultiplier}, expediciones {(ExpeditionsAllowed ? "permitidas" : "bloqueadas")}.");
        }

        private void OnAntDied(AntDied e)
        {
            // Ant.Kill() ya llama a Lose() si es la reina; esto cubre cualquier otro camino. Lose() es idempotente.
            if (e.Ant != null && e.Ant.IsQueen) GameManager.Instance?.Lose();
        }

        // ---------- Mantenimiento ----------

        private int CalculateUpkeep()
        {
            int perAnt = GetSeasonValue(foodPerAntPerDay, 0);
            if (perAnt <= 0) return 0;
            return perAnt * CountPayingAnts();
        }

        private int CountPayingAnts()
        {
            int n = 0;
            foreach (var ant in Ant.All)
                if (ant != null && ant.IsAlive && (queenPaysUpkeep || !ant.IsQueen)) n++;
            return n;
        }

        private void PayUpkeep()
        {
            var rm = ResourceManager.Instance;
            int perAnt = GetSeasonValue(foodPerAntPerDay, 0);
            if (rm == null || perAnt <= 0) return;

            int cost = perAnt * CountPayingAnts();
            if (cost <= 0) return;

            int available = rm.Get(ResourceType.Food);
            if (available >= cost) { rm.TrySpend(ResourceType.Food, cost); return; }

            // No llega: se gasta lo que hay y las hormigas que no han comido mueren.
            rm.TrySpend(ResourceType.Food, available);
            if (!starveIfNotEnoughFood) return;

            int unfedAnts = Mathf.CeilToInt((cost - available) / (float)perAnt);
            StarveAnts(unfedAnts);
        }

        /// <summary>Mueren primero obreras al azar; la reina solo si no queda ninguna (→ Game Over).</summary>
        private void StarveAnts(int count)
        {
            var workers = new List<Ant>();
            Ant queen = null;
            foreach (var ant in Ant.All)
            {
                if (ant == null || !ant.IsAlive) continue;
                if (ant.IsQueen) queen = ant; else workers.Add(ant);
            }

            for (int i = 0; i < count; i++)
            {
                if (workers.Count == 0)
                {
                    if (queen != null) queen.Kill(DeathCause.Starvation);
                    return;
                }
                int idx = Random.Range(0, workers.Count);
                var victim = workers[idx];
                workers.RemoveAt(idx);
                victim.Kill(DeathCause.Starvation);
            }
        }

        // ---------- Utilidades ----------

        private static bool IsPlaying()
        {
            var gm = GameManager.Instance;
            return gm != null && gm.State == GameState.Playing;
        }

        private T GetSeasonValue<T>(T[] values, T fallback)
        {
            int i = (int)CurrentSeason;
            return values != null && i < values.Length ? values[i] : fallback;
        }
    }
}
