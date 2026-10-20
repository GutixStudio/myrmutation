using System;
using System.Collections.Generic;
using System.Reflection;
using Myrmutation.Ants;
using Myrmutation.Core;
using Myrmutation.Expedition;
using UnityEngine;

namespace Myrmutation.Economy
{
    /// <summary>
    /// D6 · Estadísticas de la colonia: producción, consumo y previsión de cara al invierno.
    ///
    /// - Escucha ResourceChanged y guarda cada cambio (subida = producido, bajada = consumido)
    ///   en una ventana móvil de 60 s de JUEGO. Produced/Consumed/NetPerMinute devuelven unidades por minuto.
    /// - DaysUntilWinter / WinterDailyConsumption / WinterDaysCovered: previsión con la población actual.
    /// - Población por casta, obreras libres y de expedición.
    /// - El reloj interno avanza con Time.deltaTime: respeta la pausa y las velocidades x2/x3.
    ///
    /// Solo lee: no modifica recursos ni hormigas. No toca Core ni los scripts de otros
    /// (lo que es privado en GameManager / SeasonRules / AntNeeds se lee por reflexión, con valores por defecto).
    /// Va en la escena junto al GameManager (uno solo: ColonyStats.Instance).
    /// </summary>
    public class ColonyStats : MonoBehaviour
    {
        public static ColonyStats Instance { get; private set; }

        [Header("Ventana móvil")]
        [Tooltip("Segundos de juego que se tienen en cuenta para el 'por minuto'")]
        [SerializeField] private float windowSeconds = 60f;
        [Tooltip("Al empezar la partida, mínimo de segundos para extrapolar a 'por minuto' (evita picos al inicio)")]
        [SerializeField] private float minSampleSeconds = 5f;
        [Tooltip("Segundos de juego del principio en los que los cambios no cuentan (recursos iniciales que meten otros scripts en su Start)")]
        [SerializeField] private float warmupSeconds = 1f;

        [Header("Previsión de invierno")]
        [Tooltip("Sumar al consumo de invierno lo que comen las hormigas (además del mantenimiento diario de D3)")]
        [SerializeField] private bool includeMeals = true;

        [Header("Valores por defecto (solo si no se pueden leer de GameManager / SeasonRules / AntNeeds)")]
        [SerializeField] private float fallbackSecondsPerDay = 20f;
        [SerializeField] private int fallbackDaysPerSeason = 5;
        [SerializeField] private int fallbackWinterUpkeepPerAnt = 2;

        private struct Sample { public float Time; public ResourceType Type; public int Delta; }

        private readonly Queue<Sample> samples = new Queue<Sample>();
        private readonly Dictionary<ResourceType, int> last = new Dictionary<ResourceType, int>();
        private readonly Dictionary<ResourceType, int> producedSum = new Dictionary<ResourceType, int>();
        private readonly Dictionary<ResourceType, int> consumedSum = new Dictionary<ResourceType, int>();
        private readonly Dictionary<CasteType, int> byCaste = new Dictionary<CasteType, int>();
        private float clock;      // segundos de juego desde que empezó a medir
        private int populationFrame = -1;
        private int free, assigned, queens, noCaste, inNest;

        /// <summary>Segundos de juego medidos (respeta pausa y velocidad).</summary>
        public float GameTime => clock;

        // ---------- Ciclo de vida ----------
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
            {
                producedSum[t] = 0;
                consumedSum[t] = 0;
            }
            foreach (CasteType c in Enum.GetValues(typeof(CasteType))) byCaste[c] = 0;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void OnEnable() => EventBus.Subscribe<ResourceChanged>(OnResourceChanged);
        private void OnDisable() => EventBus.Unsubscribe<ResourceChanged>(OnResourceChanged);

        private void Start()
        {
            // Punto de partida: las cantidades iniciales no cuentan como producción.
            var rm = ResourceManager.Instance;
            if (rm == null) return;
            foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
                if (!last.ContainsKey(t)) last[t] = rm.Get(t);
        }

        private void Update()
        {
            clock += Time.deltaTime;   // 0 en pausa, x2/x3 con velocidad
            Trim();
        }

        private void OnResourceChanged(ResourceChanged e)
        {
            if (!last.TryGetValue(e.Type, out int prev)) { last[e.Type] = e.Amount; return; }
            last[e.Type] = e.Amount;

            int delta = e.Amount - prev;
            if (delta == 0 || clock < warmupSeconds) return;   // arranque: solo actualiza la referencia

            samples.Enqueue(new Sample { Time = clock, Type = e.Type, Delta = delta });
            if (delta > 0) producedSum[e.Type] += delta;
            else consumedSum[e.Type] += -delta;
        }

        private void Trim()
        {
            float from = clock - windowSeconds;
            while (samples.Count > 0 && samples.Peek().Time < from)
            {
                var s = samples.Dequeue();
                if (s.Delta > 0) producedSum[s.Type] -= s.Delta;
                else consumedSum[s.Type] -= -s.Delta;
            }
        }

        private float PerMinute(int amountInWindow)
        {
            float measured = clock - Mathf.Min(clock, warmupSeconds);
            float span = Mathf.Clamp(measured, Mathf.Max(0.01f, minSampleSeconds), windowSeconds);
            return amountInWindow * 60f / span;
        }

        // ---------- Producción y consumo (unidades por minuto de juego) ----------

        /// <summary>Unidades producidas por minuto (ventana móvil de 60 s de juego).</summary>
        public float Produced(ResourceType type) => PerMinute(producedSum[type]);

        /// <summary>Unidades consumidas por minuto (ventana móvil de 60 s de juego). Positivo.</summary>
        public float Consumed(ResourceType type) => PerMinute(consumedSum[type]);

        /// <summary>Producido − consumido por minuto. Negativo = el recurso va bajando.</summary>
        public float NetPerMinute(ResourceType type) => Produced(type) - Consumed(type);

        // ---------- Calendario ----------

        public float SecondsPerDay => ReadPrivate(GameManager.Instance, "secondsPerDay", fallbackSecondsPerDay);
        public int DaysPerSeason => Mathf.Max(1, ReadPrivate(GameManager.Instance, "daysPerSeason", fallbackDaysPerSeason));

        /// <summary>Días que faltan para que empiece el invierno (0 si ya es invierno).</summary>
        public int DaysUntilWinter
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm == null) return 0;
                if (gm.CurrentSeason == Season.Winter) return 0;

                int dps = DaysPerSeason;
                int dayInSeason = (gm.Day - 1) % dps;                    // 0..dps-1
                int seasonsLeft = (int)Season.Winter - (int)gm.CurrentSeason;
                return (dps - dayInSeason) + (seasonsLeft - 1) * dps;
            }
        }

        /// <summary>Segundos de juego que faltan para el invierno (más preciso que los días, para barras).</summary>
        public float SecondsUntilWinter
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm == null || gm.CurrentSeason == Season.Winter) return 0f;
                return Mathf.Max(0f, (DaysUntilWinter - gm.DayProgress) * SecondsPerDay);
            }
        }

        // ---------- Previsión de invierno ----------

        /// <summary>Comida por hormiga y día que cobra D3 en invierno (mantenimiento).</summary>
        public int WinterUpkeepPerAnt
        {
            get
            {
                var perSeason = ReadPrivate<int[]>(SeasonRules.Instance, "foodPerAntPerDay", null);
                int i = (int)Season.Winter;
                return perSeason != null && i < perSeason.Length ? perSeason[i] : fallbackWinterUpkeepPerAnt;
            }
        }

        /// <summary>
        /// Comida que gastaría la población actual en un día de invierno:
        /// mantenimiento de D3 + (opcional) lo que comen según su hambre.
        /// </summary>
        public float WinterDailyConsumption
        {
            get
            {
                bool queenPays = ReadPrivate(SeasonRules.Instance, "queenPaysUpkeep", true);
                int upkeep = WinterUpkeepPerAnt;
                float spd = SecondsPerDay;
                float total = 0f;

                foreach (var ant in AllAnts())
                {
                    if (queenPays || !ant.IsQueen) total += upkeep;
                    if (includeMeals) total += MealsPerDay(ant, spd);
                }
                return total;
            }
        }

        /// <summary>Días de invierno que aguanta la comida actual con la población actual (∞ si no hay consumo).</summary>
        public float WinterDaysCovered
        {
            get
            {
                float daily = WinterDailyConsumption;
                int food = ResourceManager.Instance != null ? ResourceManager.Instance.Get(ResourceType.Food) : 0;
                return daily <= 0f ? float.PositiveInfinity : food / daily;
            }
        }

        /// <summary>Comida necesaria para pasar el invierno entero con la población actual.</summary>
        public float WinterFoodNeeded => WinterDailyConsumption * DaysPerSeason;

        /// <summary>¿Llega la comida actual para todo el invierno?</summary>
        public bool IsReadyForWinter => WinterDaysCovered >= DaysPerSeason;

        /// <summary>Comida prevista al empezar el invierno si sigue el ritmo actual (NetPerMinute de la comida).</summary>
        public float ProjectedFoodAtWinter
        {
            get
            {
                int food = ResourceManager.Instance != null ? ResourceManager.Instance.Get(ResourceType.Food) : 0;
                return Mathf.Max(0f, food + NetPerMinute(ResourceType.Food) / 60f * SecondsUntilWinter);
            }
        }

        private static float MealsPerDay(Ant ant, float secondsPerDay)
        {
            var needs = ant.GetComponent<AntNeeds>();
            if (needs == null) return 0f;

            float scale = ReadPrivate(needs, "hungerRateScale", 0.5f);
            float threshold = ReadPrivate(needs, "hungryThreshold", 0.6f);
            float relief = ReadPrivate(needs, "mealRelief", 1f);
            float hungerPerDay = Mathf.Max(0f, ant.Stats.hungerRate) * scale * secondsPerDay;
            float hungerPerMeal = Mathf.Max(0.05f, Mathf.Min(threshold, relief));
            return hungerPerDay / hungerPerMeal * needs.RationSize;
        }

        // ---------- Población ----------

        /// <summary>Hormigas vivas, incluidas las que están de expedición y la reina.</summary>
        public int Population { get { RefreshPopulation(); return inNest + OnExpedition; } }

        /// <summary>Hormigas de una casta (incluye las de expedición). La reina no cuenta.</summary>
        public int CountByCaste(CasteType caste) { RefreshPopulation(); return byCaste[caste]; }

        /// <summary>Obreras sin casta asignada (p. ej. las colocadas a mano en escena).</summary>
        public int WithoutCaste { get { RefreshPopulation(); return noCaste; } }

        /// <summary>Obreras en el nido sin sala asignada.</summary>
        public int FreeWorkers { get { RefreshPopulation(); return free; } }

        /// <summary>Obreras en el nido con sala asignada (trabajando, construyendo…).</summary>
        public int AssignedWorkers { get { RefreshPopulation(); return assigned; } }

        /// <summary>Obreras fuera, de expedición.</summary>
        public int OnExpedition => ExpeditionSystem.Instance != null ? ExpeditionSystem.Instance.AntsAway.Count : 0;

        public int Queens { get { RefreshPopulation(); return queens; } }

        private void RefreshPopulation()
        {
            if (populationFrame == Time.frameCount) return;
            populationFrame = Time.frameCount;

            free = assigned = queens = noCaste = inNest = 0;
            foreach (CasteType c in Enum.GetValues(typeof(CasteType))) byCaste[c] = 0;

            foreach (var ant in Ant.All)
            {
                if (ant == null || !ant.IsAlive) continue;
                inNest++;
                if (ant.IsQueen) { queens++; continue; }
                if (ant.AssignedRoom == null) free++; else assigned++;
                CountCaste(ant);
            }

            if (ExpeditionSystem.Instance != null)
                foreach (var ant in ExpeditionSystem.Instance.AntsAway)
                    if (ant != null && ant.IsAlive && !ant.IsQueen) CountCaste(ant);
        }

        private void CountCaste(Ant ant)
        {
            if (ant.Caste != null) byCaste[ant.Caste.caste]++;
            else noCaste++;
        }

        /// <summary>Todas las hormigas vivas: en el nido y de expedición.</summary>
        private static IEnumerable<Ant> AllAnts()
        {
            foreach (var ant in Ant.All)
                if (ant != null && ant.IsAlive) yield return ant;

            if (ExpeditionSystem.Instance != null)
                foreach (var ant in ExpeditionSystem.Instance.AntsAway)
                    if (ant != null && ant.IsAlive) yield return ant;
        }

        // ---------- Utilidades ----------

        /// <summary>Lee un campo privado de otro script sin modificarlo. Si no existe, devuelve 'fallback'.</summary>
        private static T ReadPrivate<T>(object target, string field, T fallback)
        {
            if (target == null || (target is UnityEngine.Object uo && uo == null)) return fallback;
            var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return f != null && f.GetValue(target) is T value ? value : fallback;
        }
    }
}
