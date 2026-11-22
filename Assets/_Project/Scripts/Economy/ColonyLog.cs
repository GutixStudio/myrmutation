using System.Collections.Generic;
using Myrmutation.Colony;
using Myrmutation.Core;
using UnityEngine;

namespace Myrmutation.Economy
{
    /// <summary>
    /// D9 · Avisos de economía y partida en el registro de eventos (EventLog, E11).
    ///
    /// Publica LogMessage (GameEvents.cs) para lo que el EventLog no traduce por sí solo:
    ///  - "Larva esperando casta" (aviso, foco en la Guardería) cuando una larva necesita que la alimenten
    ///    (mira Nursery.AllBrood cada 'checkInterval'; avisa una vez por larva).
    ///  - "Comida baja" cuando la comida cubre menos de 1 día de consumo de la estación actual
    ///    (no se repite hasta que vuelva a cubrir más de 'lowFoodResetDays').
    ///  - "El invierno llega en 2 días: comida para X días" al empezar el día que toca.
    ///
    /// Los nacimientos, muertes, cambios de estación y expediciones los traduce el propio EventLog.
    /// Necesita ColonyStats (D6) para los cálculos de consumo: se añade solo en el mismo objeto.
    /// Va en la escena junto al GameManager (uno solo).
    /// </summary>
    [RequireComponent(typeof(ColonyStats))]
    public class ColonyLog : MonoBehaviour
    {
        [Header("Larvas")]
        [SerializeField] private bool logWaitingLarvae = true;

        [Header("Comida baja")]
        [SerializeField] private bool logLowFood = true;
        [Tooltip("Avisa cuando la comida cubre menos de estos días de consumo (estación actual)")]
        [SerializeField] private float lowFoodDays = 1f;
        [Tooltip("No vuelve a avisar hasta que la comida cubra más de estos días")]
        [SerializeField] private float lowFoodResetDays = 1.5f;
        [Tooltip("Cada cuántos segundos de juego se comprueba la comida")]
        [SerializeField] private float checkInterval = 0.5f;

        [Header("Invierno")]
        [SerializeField] private bool logWinterWarning = true;
        [Tooltip("Días antes del invierno en que se avisa")]
        [SerializeField] private int winterWarningDays = 2;

        private ColonyStats stats;
        private readonly HashSet<int> announcedLarvae = new HashSet<int>();
        private readonly HashSet<int> stillWaiting = new HashSet<int>();
        private bool lowFoodWarned;
        private float checkTimer;

        private void Awake() => stats = GetComponent<ColonyStats>();

        private void OnEnable() => EventBus.Subscribe<DayPassed>(OnDayPassed);

        private void OnDisable() => EventBus.Unsubscribe<DayPassed>(OnDayPassed);

        private void Update()
        {
            checkTimer += Time.deltaTime;   // tiempo de juego: en pausa no comprueba
            if (checkTimer < checkInterval) return;
            checkTimer = 0f;
            CheckWaitingLarvae();
            CheckLowFood();
        }

        private static bool Playing => GameManager.Instance == null || GameManager.Instance.State == GameState.Playing;

        // ---------- Larva esperando casta ----------
        // Se mira la lista de crías de la Guardería: avisa una vez por cada larva nueva que necesita comida.
        private void CheckWaitingLarvae()
        {
            var nursery = Nursery.Instance;
            if (!logWaitingLarvae || nursery == null) return;

            stillWaiting.Clear();
            foreach (var b in nursery.AllBrood)
            {
                if (b == null || !b.NeedsFood) continue;
                stillWaiting.Add(b.Id);
                if (!announcedLarvae.Add(b.Id)) continue;   // ya avisada

                var room = Room.FindBuilt(RoomType.Nursery);
                Transform focus = room != null ? room.transform : nursery.transform;
                Post(LogCategory.Aviso, "Larva esperando casta", focus);
            }
            announcedLarvae.IntersectWith(stillWaiting);   // olvida las que ya se han alimentado
        }

        // ---------- Comida baja ----------
        private void CheckLowFood()
        {
            if (!logLowFood || stats == null || ResourceManager.Instance == null || !Playing) return;
            float daily = stats.CurrentDailyConsumption;
            if (daily <= 0f) return;

            int food = ResourceManager.Instance.Get(ResourceType.Food);
            float days = food / daily;

            if (!lowFoodWarned && days < lowFoodDays)
            {
                lowFoodWarned = true;
                Post(LogCategory.Aviso, $"Comida baja: {food} para un consumo de {daily:0.#} al día");
            }
            else if (lowFoodWarned && days > lowFoodResetDays)
            {
                lowFoodWarned = false;
            }
        }

        // ---------- El invierno llega ----------
        private void OnDayPassed(DayPassed e)
        {
            if (!logWinterWarning || stats == null || winterWarningDays <= 0) return;
            if (stats.DaysUntilWinter != winterWarningDays) return;

            float covered = stats.WinterDaysCovered;
            string food = float.IsPositiveInfinity(covered) ? "comida de sobra"
                : Mathf.Approximately(Mathf.Round(covered * 10f) / 10f, 1f) ? "comida para 1 día"
                : $"comida para {covered:0.#} días";
            string when = winterWarningDays == 1 ? "1 día" : $"{winterWarningDays} días";
            Post(LogCategory.Aviso, $"El invierno llega en {when}: {food}");
        }

        private static void Post(LogCategory category, string text, Transform focus = null) =>
            EventBus.Publish(new LogMessage(category, text, focus));
    }
}
