using System.Collections.Generic;
using Myrmutation.Ants;
using Myrmutation.Core;
using UnityEngine;

namespace Myrmutation.Economy
{
    /// <summary>
    /// D1 · Cadena de producción de la colonia: hojas → hongo → comida.
    ///
    /// - Sala de hongos (FungusGarden): sus obreras consumen hojas y producen hongo con el tiempo.
    /// - El hongo se convierte en comida: directamente (automático) o en la Despensa (con sus obreras).
    /// - La velocidad de cada sala = suma de (workRate × afinidad de la casta con la sala) de sus obreras.
    ///
    /// Obrera de una sala = hormiga viva, no reina, con AssignedRoom == sala (máx. RoomData.workerCapacity).
    /// Solo producen salas construidas (Room.IsBuilt).
    /// Usa Time.deltaTime, así que se pausa/acelera con el GameManager.
    /// Recursos a través de ResourceManager (A1); no modifica nada de Core.
    /// </summary>
    public class ColonyProduction : MonoBehaviour
    {
        public enum FungusToFoodMode
        {
            [Tooltip("El hongo se convierte en comida solo, a ritmo fijo")]
            Direct,
            [Tooltip("El hongo se convierte en comida en la Despensa, según sus obreras")]
            Storage
        }

        public static ColonyProduction Instance { get; private set; }

        [Header("Sala de hongos: hojas → hongo")]
        [Tooltip("Segundos que tarda UNA obrera con eficiencia 1 en completar un ciclo")]
        [SerializeField] private float fungusCycleSeconds = 10f;
        [SerializeField] private int leavesPerCycle = 2;
        [SerializeField] private int fungusPerCycle = 2;

        [Header("Hongo → comida")]
        [SerializeField] private FungusToFoodMode fungusToFood = FungusToFoodMode.Direct;
        [Tooltip("Hongo consumido en cada conversión")]
        [SerializeField] private int fungusPerConversion = 1;
        [Tooltip("Comida obtenida en cada conversión")]
        [SerializeField] private int foodPerConversion = 2;

        [Tooltip("Modo Direct: segundos entre conversiones")]
        [SerializeField] private float directConversionSeconds = 4f;

        [Tooltip("Modo Storage: segundos que tarda UNA obrera con eficiencia 1 en una conversión")]
        [SerializeField] private float storageCycleSeconds = 5f;
        [Tooltip("Modo Storage: si no hay Despensa construida, convertir igualmente de forma directa")]
        [SerializeField] private bool fallbackToDirectWithoutStorage = true;

        [Header("Obreras")]
        [Tooltip("Si está activo, solo cuentan las hormigas que están trabajando en la sala (AntActuator.Working). " +
                 "Desactivado: basta con estar asignada (útil mientras no exista la IA completa).")]
        [SerializeField] private bool onlyCountWorkingAnts = false;

        [Header("Debug")]
        [SerializeField] private bool logProduction = false;

        /// <summary>Estado de producción de una sala.</summary>
        public class RoomState
        {
            public float Progress;      // 0..1 del ciclo actual
            public float WorkPower;     // suma de eficiencias de sus obreras
            public int WorkerCount;
            public bool Stalled;        // ciclo completo pero falta materia prima (hojas / hongo)
        }

        private readonly Dictionary<Room, RoomState> states = new Dictionary<Room, RoomState>();
        private readonly List<Ant> workerBuffer = new List<Ant>();
        private readonly List<Room> deadRooms = new List<Room>();
        private float directTimer;

        // ---------- Ciclo de vida ----------
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            if (ResourceManager.Instance == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            CleanupStates();

            bool storageBuilt = false;
            foreach (var room in Room.All)
            {
                if (room == null || !room.IsBuilt || room.Data == null) continue;

                switch (room.Data.type)
                {
                    case RoomType.FungusGarden:
                        TickRoom(room, dt, fungusCycleSeconds,
                            ResourceType.Leaves, leavesPerCycle, ResourceType.Fungus, fungusPerCycle);
                        break;

                    case RoomType.Storage:
                        storageBuilt = true;
                        if (fungusToFood == FungusToFoodMode.Storage)
                            TickRoom(room, dt, storageCycleSeconds,
                                ResourceType.Fungus, fungusPerConversion, ResourceType.Food, foodPerConversion);
                        break;
                }
            }

            bool direct = fungusToFood == FungusToFoodMode.Direct
                          || (fallbackToDirectWithoutStorage && !storageBuilt);
            if (direct) TickDirectConversion(dt);
        }

        // ---------- Producción por sala ----------
        private void TickRoom(Room room, float dt, float cycleSeconds,
                              ResourceType input, int inputAmount, ResourceType output, int outputAmount)
        {
            var s = GetOrCreate(room);
            s.WorkPower = ComputeWorkPower(room, out s.WorkerCount);

            if (s.WorkPower <= 0f) return; // sin obreras: no avanza (conserva el progreso)

            if (s.Progress < 1f)
                s.Progress += s.WorkPower * dt / Mathf.Max(0.01f, cycleSeconds);

            // Puede completar varios ciclos en un frame si va muy rápido (velocidad x3, muchas obreras)
            while (s.Progress >= 1f)
            {
                if (!ResourceManager.Instance.TrySpend(input, inputAmount))
                {
                    s.Progress = 1f;      // listo, esperando materia prima
                    s.Stalled = true;
                    return;
                }
                ResourceManager.Instance.Add(output, outputAmount);
                s.Progress -= 1f;
                s.Stalled = false;
                if (logProduction)
                    Debug.Log($"[ColonyProduction] {room.Data.displayName}: -{inputAmount} {input} → +{outputAmount} {output}", room);
            }
            s.Stalled = false;
        }

        private void TickDirectConversion(float dt)
        {
            directTimer += dt;
            float interval = Mathf.Max(0.01f, directConversionSeconds);
            while (directTimer >= interval)
            {
                if (!ResourceManager.Instance.TrySpend(ResourceType.Fungus, fungusPerConversion))
                {
                    directTimer = interval; // espera a que haya hongo; convierte en cuanto llegue
                    return;
                }
                ResourceManager.Instance.Add(ResourceType.Food, foodPerConversion);
                directTimer -= interval;
                if (logProduction)
                    Debug.Log($"[ColonyProduction] Directo: -{fungusPerConversion} Hongo → +{foodPerConversion} Comida");
            }
        }

        // ---------- Obreras y eficiencia ----------
        private float ComputeWorkPower(Room room, out int count)
        {
            GetWorkers(room, workerBuffer);
            count = workerBuffer.Count;
            float power = 0f;
            foreach (var a in workerBuffer) power += GetEfficiency(a, room.Data.type);
            return power;
        }

        /// <summary>Rellena 'result' con las obreras que cuentan para la producción de la sala.</summary>
        public void GetWorkers(Room room, List<Ant> result)
        {
            result.Clear();
            if (room == null) return;
            int capacity = room.Data != null ? Mathf.Max(1, room.Data.workerCapacity) : int.MaxValue;

            foreach (var a in Ant.All)
            {
                if (result.Count >= capacity) break;
                if (a == null || !a.IsAlive || a.IsQueen || a.AssignedRoom != room) continue;
                if (onlyCountWorkingAnts && !IsWorkingIn(a, room)) continue;
                result.Add(a);
            }
        }

        private static bool IsWorkingIn(Ant ant, Room room)
        {
            var actuator = ant.GetComponent<AntActuator>();
            if (actuator == null) return true; // sin actuador no podemos saberlo: cuenta
            return actuator.Current == AntAction.Working && (actuator.WorkRoom == null || actuator.WorkRoom == room);
        }

        /// <summary>
        /// Eficiencia de una hormiga en un tipo de sala = workRate × afinidad de su casta.
        /// Pública para que la UI muestre el rendimiento al asignar.
        /// </summary>
        public static float GetEfficiency(Ant ant, RoomType roomType)
        {
            if (ant == null) return 0f;
            float workRate = ant.Stats.workRate > 0f ? ant.Stats.workRate : 1f;
            float affinity = ant.Caste != null ? ant.Caste.GetAffinity(roomType) : 1f;
            return workRate * affinity;
        }

        // ---------- Consulta (UI / panel de sala) ----------
        /// <summary>Estado de producción de la sala, o null si no produce todavía.</summary>
        public RoomState GetState(Room room) =>
            room != null && states.TryGetValue(room, out var s) ? s : null;

        /// <summary>Unidades de salida por segundo de la sala con sus obreras actuales (hongo o comida).</summary>
        public float GetOutputPerSecond(Room room)
        {
            if (room == null || room.Data == null || !room.IsBuilt) return 0f;
            var s = GetState(room);
            float power = s != null ? s.WorkPower : ComputeWorkPower(room, out _);
            switch (room.Data.type)
            {
                case RoomType.FungusGarden: return power * fungusPerCycle / Mathf.Max(0.01f, fungusCycleSeconds);
                case RoomType.Storage:
                    return fungusToFood == FungusToFoodMode.Storage
                        ? power * foodPerConversion / Mathf.Max(0.01f, storageCycleSeconds)
                        : 0f;
                default: return 0f;
            }
        }

        public bool ProducesIn(RoomType type) =>
            type == RoomType.FungusGarden || (type == RoomType.Storage && fungusToFood == FungusToFoodMode.Storage);

        // ---------- Utilidades ----------
        private RoomState GetOrCreate(Room room)
        {
            if (!states.TryGetValue(room, out var s)) { s = new RoomState(); states[room] = s; }
            return s;
        }

        private void CleanupStates()
        {
            deadRooms.Clear();
            foreach (var kv in states) if (kv.Key == null) deadRooms.Add(kv.Key);
            foreach (var r in deadRooms) states.Remove(r);
        }
    }
}
