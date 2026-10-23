using System.Collections.Generic;
using Myrmutation.Ants;
using Myrmutation.Core;
using UnityEngine;

namespace Myrmutation.Economy
{
    /// <summary>
    /// D1 + D8 · Cadena de producción de la colonia: hojas → hongo → comida.
    ///
    /// - Sala de hongos (FungusGarden): sus obreras consumen hojas y producen hongo con el tiempo.
    /// - Despensa (Storage): sus obreras convierten el hongo en comida (D8). Sin obreras sigue al
    ///   idleStoragePercent (25 %) del ritmo de una obrera, para no bloquear el inicio.
    /// - La velocidad de cada sala = suma de (workRate × afinidad de la casta con la sala) de sus obreras.
    /// - GetStatus(sala) explica por qué una sala no produce; RatePerMinute(sala) dice cuánto produce.
    ///
    /// Obrera de una sala = hormiga viva, no reina, con AssignedRoom == sala (máx. RoomData.workerCapacity).
    /// Solo producen salas construidas (Room.IsBuilt).
    /// Usa Time.deltaTime, así que se pausa/acelera con el GameManager.
    /// Recursos a través de ResourceManager (A1); no modifica nada de Core.
    /// </summary>
    /// <summary>Por qué una sala produce o no (D8). Orden de prioridad: SinObreras > SinEntrada > Llena > Ok.</summary>
    public enum RoomStatus
    {
        /// <summary>Produce con normalidad.</summary>
        Ok,
        /// <summary>No tiene ninguna obrera asignada (la Despensa sigue, pero solo al 25 %).</summary>
        SinObreras,
        /// <summary>Le falta materia prima: hojas (Sala de hongos) u hongo (Despensa).</summary>
        SinEntrada,
        /// <summary>Tiene el cupo de obreras completo (RoomData.workerCapacity): añadir más no sirve.</summary>
        Llena
    }

    public class ColonyProduction : MonoBehaviour
    {
        public static ColonyProduction Instance { get; private set; }

        [Header("Sala de hongos: hojas → hongo")]
        [Tooltip("Segundos que tarda UNA obrera con eficiencia 1 en completar un ciclo")]
        [SerializeField] private float fungusCycleSeconds = 10f;
        [SerializeField] private int leavesPerCycle = 2;
        [SerializeField] private int fungusPerCycle = 2;

        [Header("Despensa: hongo → comida")]
        [Tooltip("Hongo consumido en cada conversión")]
        [SerializeField] private int fungusPerConversion = 1;
        [Tooltip("Comida obtenida en cada conversión")]
        [SerializeField] private int foodPerConversion = 2;
        [Tooltip("Segundos que tarda UNA obrera con eficiencia 1 en una conversión")]
        [SerializeField] private float storageCycleSeconds = 5f;
        [Tooltip("Ritmo de una Despensa SIN obreras, en % del de una obrera con eficiencia 1 (para no bloquear el inicio)")]
        [Range(0f, 100f)]
        [SerializeField] private float idleStoragePercent = 25f;
        [Tooltip("Si no hay ninguna Despensa construida, el hongo se convierte igualmente al ritmo sin obreras")]
        [SerializeField] private bool convertWithoutStorage = true;

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
            public float WorkPower;     // suma de eficiencias de sus obreras (sin el 25 % de la Despensa vacía)
            public float EffectivePower;// la que se usa para avanzar (incluye el 25 % de la Despensa vacía)
            public int WorkerCount;
            public bool Stalled;        // ciclo completo pero falta materia prima (hojas / hongo)
        }

        private readonly Dictionary<Room, RoomState> states = new Dictionary<Room, RoomState>();
        private readonly List<Ant> workerBuffer = new List<Ant>();
        private readonly List<Room> deadRooms = new List<Room>();
        private readonly RoomState noStorageState = new RoomState();   // conversión sin Despensa construida

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
                    {
                        var s = GetOrCreate(room);
                        s.WorkPower = ComputeWorkPower(room, out s.WorkerCount);
                        s.EffectivePower = s.WorkPower;
                        TickCycle(s, room, dt, fungusCycleSeconds,
                            ResourceType.Leaves, leavesPerCycle, ResourceType.Fungus, fungusPerCycle);
                        break;
                    }

                    case RoomType.Storage:
                    {
                        storageBuilt = true;
                        var s = GetOrCreate(room);
                        s.WorkPower = ComputeWorkPower(room, out s.WorkerCount);
                        s.EffectivePower = StoragePower(s.WorkPower);
                        TickCycle(s, room, dt, storageCycleSeconds,
                            ResourceType.Fungus, fungusPerConversion, ResourceType.Food, foodPerConversion);
                        break;
                    }
                }
            }

            // Sin ninguna Despensa construida: conversión al ritmo sin obreras (no bloquea el inicio).
            if (!storageBuilt && convertWithoutStorage)
            {
                noStorageState.WorkPower = 0f;
                noStorageState.EffectivePower = StoragePower(0f);
                TickCycle(noStorageState, null, dt, storageCycleSeconds,
                    ResourceType.Fungus, fungusPerConversion, ResourceType.Food, foodPerConversion);
            }
        }

        /// <summary>Potencia de una Despensa: la de sus obreras, o el 25 % de una obrera si no tiene ninguna.</summary>
        private float StoragePower(float workersPower) =>
            workersPower > 0f ? workersPower : idleStoragePercent / 100f;

        // ---------- Producción por sala ----------
        private void TickCycle(RoomState s, Room room, float dt, float cycleSeconds,
                               ResourceType input, int inputAmount, ResourceType output, int outputAmount)
        {
            if (s.EffectivePower <= 0f) return; // sin obreras: no avanza (conserva el progreso)

            if (s.Progress < 1f)
                s.Progress += s.EffectivePower * dt / Mathf.Max(0.01f, cycleSeconds);

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
                {
                    string where = room != null && room.Data != null ? room.Data.displayName : "Sin Despensa";
                    Debug.Log($"[ColonyProduction] {where}: -{inputAmount} {input} → +{outputAmount} {output}", room);
                }
            }
            s.Stalled = false;
        }

        // ---------- Obreras y eficiencia ----------
        /// <summary>Suma de eficiencias de las obreras que cuentan en la sala (también la usa la Guardería).</summary>
        public float ComputeWorkPower(Room room, out int count)
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

        /// <summary>
        /// Por qué la sala produce o no. Prioridad: SinObreras > SinEntrada > Llena > Ok.
        /// Las salas que no producen (Real, Guardería, Festín) solo pueden dar Ok o Llena.
        /// Una sala sin construir da Ok (su estado lo lleva la construcción).
        /// </summary>
        public RoomStatus GetStatus(Room room)
        {
            if (room == null || room.Data == null || !room.IsBuilt) return RoomStatus.Ok;

            int assigned = CountAssigned(room);
            bool full = assigned >= Mathf.Max(1, room.Data.workerCapacity);
            if (!IsProducer(room.Data.type)) return full ? RoomStatus.Llena : RoomStatus.Ok;

            // Asignadas, no "trabajando ahora": si una se va a comer o a descansar, la sala no pasa a SinObreras.
            if (assigned == 0) return RoomStatus.SinObreras;
            if (!HasInput(room.Data.type)) return RoomStatus.SinEntrada;
            if (full) return RoomStatus.Llena;
            return RoomStatus.Ok;
        }

        /// <summary>
        /// Producción de la sala en unidades por minuto de juego, con sus obreras actuales:
        /// hongo (Sala de hongos) o comida (Despensa, incluido el 25 % sin obreras).
        /// 0 si le falta materia prima o no es una sala de producción.
        /// </summary>
        public float RatePerMinute(Room room)
        {
            if (room == null || room.Data == null || !room.IsBuilt || !IsProducer(room.Data.type)) return 0f;
            if (!HasInput(room.Data.type)) return 0f;
            return CyclesPerMinute(room) * (room.Data.type == RoomType.FungusGarden ? fungusPerCycle : foodPerConversion);
        }

        /// <summary>Materia prima que gasta la sala por minuto (hojas o hongo). Para mostrar "+12 hongo/min · −12 hojas/min".</summary>
        public float InputPerMinute(Room room)
        {
            if (room == null || room.Data == null || !room.IsBuilt || !IsProducer(room.Data.type)) return 0f;
            if (!HasInput(room.Data.type)) return 0f;
            return CyclesPerMinute(room) * (room.Data.type == RoomType.FungusGarden ? leavesPerCycle : fungusPerConversion);
        }

        /// <summary>Unidades de salida por segundo (compatibilidad con D1). Igual que RatePerMinute / 60.</summary>
        public float GetOutputPerSecond(Room room) => RatePerMinute(room) / 60f;

        /// <summary>Texto corto del estado para la UI.</summary>
        public static string Describe(RoomStatus status, RoomType type)
        {
            switch (status)
            {
                case RoomStatus.SinObreras: return type == RoomType.Storage ? "Sin obreras (25 %)" : "Sin obreras";
                case RoomStatus.SinEntrada: return type == RoomType.Storage ? "Sin hongo" : "Sin hojas";
                case RoomStatus.Llena: return "Llena";
                default: return IsProducer(type) ? "Produciendo" : "Sin problemas";
            }
        }

        /// <summary>¿Es una sala de producción? (Sala de hongos o Despensa)</summary>
        public static bool IsProducer(RoomType type) => type == RoomType.FungusGarden || type == RoomType.Storage;

        private bool HasInput(RoomType type)
        {
            var rm = ResourceManager.Instance;
            if (rm == null) return false;
            return type == RoomType.FungusGarden
                ? rm.Has(ResourceType.Leaves, leavesPerCycle)
                : rm.Has(ResourceType.Fungus, fungusPerConversion);
        }

        private float CyclesPerMinute(Room room)
        {
            float power = ComputeWorkPower(room, out _);
            if (room.Data.type == RoomType.Storage) power = StoragePower(power);
            float cycle = room.Data.type == RoomType.FungusGarden ? fungusCycleSeconds : storageCycleSeconds;
            return power * 60f / Mathf.Max(0.01f, cycle);
        }

        /// <summary>Hormigas asignadas a la sala (sin límite de cupo), para saber si está llena.</summary>
        private static int CountAssigned(Room room)
        {
            int n = 0;
            foreach (var a in Ant.All)
                if (a != null && a.IsAlive && !a.IsQueen && a.AssignedRoom == room) n++;
            return n;
        }

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
