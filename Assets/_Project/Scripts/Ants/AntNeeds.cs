using Myrmutation.Core;
using Myrmutation.Core.Data;
using UnityEngine;

namespace Myrmutation.Ants
{
    /// <summary>
    /// Necesidades de la hormiga (B3): SOLO los números. Qué hacer con ellos lo decide el cerebro (B2).
    ///
    /// ENERGÍA  1 (llena) → 0 (agotada)
    ///   · Baja a Stats.energyRate por segundo mientras no descansa, multiplicado según lo que hace:
    ///     trabajando x1 · andando o parada x0,3 (ir de una sala a otra apenas cansa).
    ///   · Sube a restRecoveryPerSecond (x el multiplicador de la sala) mientras descansa.
    ///   · IsTired (≤ 0,25) → a descansar · IsRested (≥ 0,95) → a trabajar. Dos umbrales = sin bucles.
    ///   · Si llega a 0 queda AGOTADA (IsExhausted): no puede trabajar (CanWork = false) hasta
    ///     recuperar la energía por completo (1). En ese caso IsRested también espera a 1. No muere.
    ///
    /// HAMBRE   0 (saciada) → 1 (famélica)
    ///   · Sube a Stats.hungerRate x hungerRateScale (0,5) por segundo (los genes la cambian a través de Stats).
    ///   · IsHungry (≥ 0,6) → a la Despensa.
    ///   · Si pasa starvationDelay segundos a 1 → Ant.Kill(Starvation) (si es la reina, Ant.Kill ya da Game Over).
    ///
    /// COMER    La comida la gasta AntActuator.Eat(RationSize) en el ResourceManager.
    ///          Al terminar, AntActuator lanza Ate y aquí se quita el hambre.
    ///          Glotona (tara): RationSize = 2, gasta el doble por la misma comida.
    ///
    /// Usa Time.deltaTime: respeta la pausa y las velocidades del GameManager.
    /// </summary>
    [RequireComponent(typeof(Ant))]
    public class AntNeeds : MonoBehaviour, IAntNeeds
    {
        /// <summary>behaviorTag del gen Glotona (GeneData.behaviorTag).</summary>
        public const string GluttonTag = "Glotona";

        [Header("Valores iniciales")]
        [Range(0f, 1f)] [SerializeField] private float startHunger = 0f;
        [Range(0f, 1f)] [SerializeField] private float startEnergy = 1f;

        [Header("Energía")]
        [Tooltip("Energía que recupera por segundo al descansar (0,1 = 10 s de 0 a 1)")]
        [SerializeField] private float restRecoveryPerSecond = 0.1f;
        [Tooltip("Con esta energía o menos está cansada (IsTired)")]
        [Range(0f, 1f)] [SerializeField] private float tiredThreshold = 0.25f;
        [Tooltip("Al llegar a esta energía ha descansado (IsRested)")]
        [Range(0f, 1f)] [SerializeField] private float restedThreshold = 0.95f;

        [Header("Gasto de energía según la actividad (x Stats.energyRate)")]
        [Tooltip("Trabajando en su sala")]
        [SerializeField] private float workingEnergyFactor = 1f;
        [Tooltip("Andando por los túneles")]
        [SerializeField] private float walkingEnergyFactor = 0.3f;
        [Tooltip("Parada o comiendo")]
        [SerializeField] private float idleEnergyFactor = 0.3f;

        [Header("Hambre")]
        [Tooltip("Multiplica Stats.hungerRate (0,5 = el hambre sube a la mitad de velocidad). " +
                 "Ajuste de balance de B; si D lo ajusta en las castas, volver a 1")]
        [SerializeField] private float hungerRateScale = 0.5f;
        [Tooltip("Con este hambre o más tiene hambre (IsHungry)")]
        [Range(0f, 1f)] [SerializeField] private float hungryThreshold = 0.6f;
        [Tooltip("Segundos que aguanta con el hambre al máximo antes de morir")]
        [SerializeField] private float starvationDelay = 10f;
        [Tooltip("Hambre que quita una comida completa (1 = queda saciada)")]
        [Range(0f, 1f)] [SerializeField] private float mealRelief = 1f;

        private Ant ant;
        private AntActuator actuator;
        private float hunger;
        private float energy;
        private float starvingTime;
        private bool exhausted;   // se activa al llegar a 0 y se quita al volver a 1

        // ================= ESTADO =================

        public float Hunger01 => hunger;
        public float Energy01 => energy;

        public bool IsHungry => hunger >= hungryThreshold;
        public bool IsTired => energy <= tiredThreshold;

        /// <summary>Puede volver al trabajo: 0,95 normalmente, o 1 si llegó a agotarse.</summary>
        public bool IsRested => exhausted ? energy >= 1f : energy >= restedThreshold;

        /// <summary>Llegó a 0 y aún no se ha recuperado del todo.</summary>
        public bool IsExhausted => exhausted;

        /// <summary>Puede trabajar (false mientras está agotada).</summary>
        public bool CanWork => !exhausted;

        public bool IsResting { get; private set; }

        /// <summary>Sala en la que descansa (null = en un nodo del túnel).</summary>
        public Room RestRoom { get; private set; }

        /// <summary>Lleva el hambre al máximo y está muriendo.</summary>
        public bool IsStarving => hunger >= 1f;

        /// <summary>Segundos que le quedan antes de morir de hambre (starvationDelay si no se está muriendo).</summary>
        public float StarvationTimeLeft => Mathf.Max(0f, starvationDelay - starvingTime);

        /// <summary>Raciones de comida que gasta en cada comida (Glotona: 2). Pasar a AntActuator.Eat().</summary>
        public int RationSize => HasGene(GluttonTag) ? 2 : 1;

        // ================= CICLO =================

        private void Awake()
        {
            ant = GetComponent<Ant>();
            actuator = GetComponent<AntActuator>();
            hunger = startHunger;
            energy = startEnergy;
        }

        private void OnEnable() { if (actuator != null) actuator.Ate += OnAte; }
        private void OnDisable() { if (actuator != null) actuator.Ate -= OnAte; }

        private void Update()
        {
            if (!ant.IsAlive) return;
            float dt = Time.deltaTime;
            var stats = CurrentStats();

            // Descansar solo cuenta si está quieta: si el cerebro le da otra orden, deja de descansar.
            if (IsResting && actuator != null && !actuator.IsIdle) StopRest();

            // Energía
            if (IsResting) energy += restRecoveryPerSecond * RestMultiplier(RestRoom) * dt;
            else energy -= stats.energyRate * EnergyDrainFactor() * dt;
            energy = Mathf.Clamp01(energy);
            if (energy <= 0f) exhausted = true;
            else if (energy >= 1f) exhausted = false;

            // Agotada: deja de trabajar en el acto, aunque el cerebro no lo haya visto aún.
            if (exhausted && actuator != null && actuator.Current == AntAction.Working) actuator.Stop();

            // Hambre
            hunger = Mathf.Clamp01(hunger + stats.hungerRate * hungerRateScale * dt);
            if (IsStarving)
            {
                starvingTime += dt;
                if (starvingTime >= starvationDelay) ant.Kill(DeathCause.Starvation);
            }
            else starvingTime = 0f;
        }

        // ================= DESCANSO =================

        /// <summary>
        /// Empieza a descansar. room = sala asignada donde descansa (null = en el nodo del túnel donde esté).
        /// El cerebro debe llevarla allí ANTES de llamar a esto y dejarla quieta.
        /// </summary>
        public void StartRest(Room room = null)
        {
            RestRoom = room;
            IsResting = true;
        }

        /// <summary>Deja de descansar (el cerebro lo llama cuando IsRested).</summary>
        public void StopRest()
        {
            IsResting = false;
            RestRoom = null;
        }

        /// <summary>
        /// Cuánto más rápido se descansa en una sala. Alfa: siempre x1.
        /// Beta: el Dormitorio dará x2 (falta añadir su RoomType).
        /// </summary>
        public static float RestMultiplier(Room room) => 1f;

        // ================= COMER =================

        private void OnAte(int rations)
        {
            if (rations <= 0) return;   // no había comida: no se le quita el hambre
            float portion = Mathf.Clamp01((float)rations / RationSize);
            hunger = Mathf.Clamp01(hunger - mealRelief * portion);
            if (!IsStarving) starvingTime = 0f;
        }

        // ================= AUXILIARES =================

        /// <summary>
        /// Cuánto gasta según lo que está haciendo (multiplica a Stats.energyRate).
        ///
        /// TODO (TRANSPORTE · quien implemente que las hormigas carguen cosas):
        ///   Al andar CARGADA debe gastar más energía, en proporción al peso de la carga.
        ///   Propuesta: en el caso Moving, multiplicar por (1 + cargaActual / Stats.carryCapacity),
        ///   o sea x2 con la carga al máximo. La carga actual la tendrá que exponer el sistema de
        ///   transporte (p. ej. una propiedad CarriedWeight en el componente que lleve la carga);
        ///   leerla aquí con GetComponent y sumarla a este cálculo. Hablarlo con B.
        /// </summary>
        private float EnergyDrainFactor()
        {
            if (actuator == null) return workingEnergyFactor;
            switch (actuator.Current)
            {
                case AntAction.Working: return workingEnergyFactor;
                case AntAction.Moving: return walkingEnergyFactor;   // TODO transporte: x (1 + carga / carryCapacity)
                default: return idleEnergyFactor;                    // parada o comiendo
            }
        }

        /// <summary>Stats de la hormiga; si aún no tiene (sin casta), los de por defecto.</summary>
        private AntStats CurrentStats()
        {
            var s = ant.Stats;
            bool empty = s.hungerRate <= 0f && s.energyRate <= 0f && s.speed <= 0f;
            return empty ? AntStats.Default : s;
        }

        private bool HasGene(string behaviorTag)
        {
            foreach (var g in ant.Genes)
                if (g != null && g.behaviorTag == behaviorTag) return true;
            return false;
        }

#if UNITY_EDITOR
        // Para probar desde el Inspector (botón derecho sobre el componente).
        [ContextMenu("Pruebas/Hambre al máximo")] private void DebugStarve() => hunger = 1f;
        [ContextMenu("Pruebas/Energía a 0")] private void DebugExhaust() => energy = 0f;
        [ContextMenu("Pruebas/Rellenar todo")] private void DebugFill() { hunger = 0f; energy = 1f; }
#endif
    }
}
