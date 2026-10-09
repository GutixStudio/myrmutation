using System;
using Myrmutation.Core;
using UnityEngine;

namespace Myrmutation.Ants
{
    /// <summary>
    /// Cerebro de la hormiga (B2): máquina de estados jerárquica de dos niveles.
    /// Guía para el equipo (cómo integrarse con la IA): docs/IA_Hormigas.md
    ///
    ///  NIVEL 1 · OBJETIVO (se elige por prioridad: Necesidad > Rutina)
    ///    1. Eat      · IsHungry y hay Despensa y comida  → ir a la Despensa y comer (ración doble si es Glotona)
    ///    2. Rest     · IsTired (o agotada)                → ir a su sala (o al túnel más cercano) y descansar
    ///    3. Routine  · lo demás                            → ir a su sala asignada y trabajar
    ///  Comer va por delante de descansar porque el hambre mata y el cansancio no.
    ///  Una necesidad no se abandona a medias: come hasta terminar y descansa hasta IsRested.
    ///  Al acabar una necesidad "vuelve": la Rutina la lleva otra vez a su sala.
    ///
    ///  NIVEL 2 · PASO dentro del objetivo
    ///    None → Moving (camino a su sitio) → Acting (trabajando / comiendo / descansando)
    ///    Waiting = no puede hacer nada ahora (sin sala, sin camino…): reintenta más tarde.
    ///
    /// Solo da órdenes al AntActuator y a AntNeeds (StartRest/StopRest); lo que sabe lo lee de AntSensor.
    /// Decide cada 'thinkInterval' segundos, no en cada frame (GDD: 15-40 hormigas).
    /// La reina no trabaja ni se mueve: come y descansa en su sitio.
    /// </summary>
    [RequireComponent(typeof(Ant), typeof(AntActuator), typeof(AntSensor))]
    [RequireComponent(typeof(AntNeeds))]
    public class AntBrain : MonoBehaviour, IAntBrain
    {
        [Header("Decisión")]
        [Tooltip("Cada cuántos segundos decide (GDD: 0,2-0,5)")]
        [SerializeField] private float thinkInterval = 0.3f;
        [Tooltip("Si algo falla (sin camino, no había comida), segundos antes de volver a intentarlo")]
        [SerializeField] private float retryDelay = 3f;

        [Header("Debug")]
        [SerializeField] private bool logTransitions;
        [Tooltip("Muestra el estado encima de la hormiga en la vista Scene")]
        [SerializeField] private bool showStateLabel = true;

        private Ant ant;
        private AntActuator actuator;
        private AntSensor sensor;
        private AntNeeds needs;

        private float thinkTimer;
        private float eatRetryAt;       // no intenta comer antes de este momento
        private float waitUntil;        // fin del paso Waiting
        private Room workRoom;          // sala hacia la que va / en la que trabaja
        private Room restRoom;          // sala en la que descansa (null = en el túnel)

        public BrainGoal Goal { get; private set; } = BrainGoal.Routine;
        public BrainStep Step { get; private set; } = BrainStep.None;
        public string StateName => $"{Goal}/{Step}";
        public bool IsSuspended { get; private set; }

        public event Action<BrainGoal, BrainStep> StateChanged;

        // ================= CICLO =================

        private void Awake()
        {
            ant = GetComponent<Ant>();
            actuator = GetComponent<AntActuator>();
            sensor = GetComponent<AntSensor>();
            needs = GetComponent<AntNeeds>();
            // Escalonado: que no piensen todas en el mismo frame.
            thinkTimer = UnityEngine.Random.Range(0f, thinkInterval);
        }

        private void OnEnable()
        {
            actuator.ActionFinished += OnActionFinished;
            actuator.Ate += OnAte;
        }

        private void OnDisable()
        {
            actuator.ActionFinished -= OnActionFinished;
            actuator.Ate -= OnAte;
        }

        private void Update()
        {
            if (IsSuspended || !ant.IsAlive) return;
            thinkTimer -= Time.deltaTime;
            if (thinkTimer > 0f) return;
            thinkTimer = thinkInterval;
            Think();
        }

        // ================= API =================

        public void Think()
        {
            if (IsSuspended || !ant.IsAlive) return;

            var wanted = ChooseGoal();
            if (wanted != Goal) ChangeGoal(wanted);

            switch (Goal)
            {
                case BrainGoal.Routine: TickRoutine(); break;
                case BrainGoal.Eat: TickEat(); break;
                case BrainGoal.Rest: TickRest(); break;
            }
        }

        public void Suspend()
        {
            if (IsSuspended) return;
            if (needs.IsResting) needs.StopRest();
            IsSuspended = true;
            SetStep(BrainStep.Waiting);
        }

        public void Resume()
        {
            if (!IsSuspended) return;
            IsSuspended = false;
            Goal = BrainGoal.Routine;
            SetStep(BrainStep.None);
            thinkTimer = 0f;
        }

        // ================= NIVEL 1: OBJETIVO =================

        private BrainGoal ChooseGoal()
        {
            // Comer: tiene prioridad y no se abandona hasta terminar (OnActionFinished la saca).
            if (Goal == BrainGoal.Eat) return BrainGoal.Eat;
            if (sensor.IsHungry && CanTryToEat()) return BrainGoal.Eat;

            // Descansar: hasta que IsRested (si llegó a agotarse, hasta la energía completa).
            if (Goal == BrainGoal.Rest && !sensor.IsRested) return BrainGoal.Rest;
            // (Si comió a mitad de un agotamiento, sigue sin poder trabajar: vuelve a descansar.)
            if (sensor.IsTired || !sensor.CanWork) return BrainGoal.Rest;

            return BrainGoal.Routine;
        }

        private bool CanTryToEat()
        {
            if (Time.time < eatRetryAt) return false;
            var res = ResourceManager.Instance;
            if (res == null || !res.Has(ResourceType.Food, needs.RationSize)) return false;
            return ant.IsQueen || sensor.NearestRoom(RoomType.Storage) != null;
        }

        private void ChangeGoal(BrainGoal next)
        {
            // Salir del objetivo actual
            if (Goal == BrainGoal.Rest && needs.IsResting) needs.StopRest();
            actuator.Stop();
            workRoom = null;
            restRoom = null;

            if (logTransitions) Debug.Log($"[AntBrain] {ant.Name}: {Goal} → {next}", this);
            Goal = next;
            SetStep(BrainStep.None);
        }

        // ================= NIVEL 2: PASOS =================

        // ---- Rutina: ir a su sala asignada y trabajar ----
        private void TickRoutine()
        {
            var assigned = ant.IsQueen ? null : ant.AssignedRoom;

            // Sin sala (o es la reina): espera quieta.
            if (assigned == null || !needs.CanWork)
            {
                if (Step != BrainStep.Waiting) { actuator.Stop(); workRoom = null; SetStep(BrainStep.Waiting); }
                return;
            }

            // Le han cambiado de sala: ir a la nueva.
            if (assigned != workRoom) { workRoom = assigned; SetStep(BrainStep.None); }

            switch (Step)
            {
                case BrainStep.Waiting:
                    if (Time.time >= waitUntil) SetStep(BrainStep.None);
                    break;
                case BrainStep.None:
                    GoTo(workRoom.WorkPosition);
                    break;
                case BrainStep.Moving:
                    if (actuator.Current != AntAction.Moving) SetStep(BrainStep.None);   // alguien la paró
                    break;
                case BrainStep.Acting:
                    if (actuator.Current != AntAction.Working) SetStep(BrainStep.None);  // alguien la paró
                    break;
            }
        }

        // ---- Comer: ir a la Despensa y comer ----
        private void TickEat()
        {
            switch (Step)
            {
                case BrainStep.None:
                    if (ant.IsQueen) { Arrive(); break; }   // a la reina le llevan la comida
                    var storage = sensor.NearestRoom(RoomType.Storage);
                    if (storage == null) { GiveUpEating(); break; }
                    GoTo(storage.WorkPosition);
                    break;
                case BrainStep.Moving:
                    if (actuator.Current != AntAction.Moving) SetStep(BrainStep.None);
                    break;
                case BrainStep.Acting:
                    if (actuator.Current != AntAction.Eating) GiveUpEating();
                    break;
                case BrainStep.Waiting:
                    GiveUpEating();
                    break;
            }
        }

        // ---- Descansar: en su sala asignada o en el túnel más cercano ----
        private void TickRest()
        {
            switch (Step)
            {
                case BrainStep.None:
                    if (ant.IsQueen) { restRoom = null; Arrive(); break; }
                    restRoom = ant.AssignedRoom;
                    GoTo(restRoom != null ? restRoom.WorkPosition : sensor.NearestTunnelPoint());
                    break;
                case BrainStep.Moving:
                    if (actuator.Current != AntAction.Moving) SetStep(BrainStep.None);
                    break;
                case BrainStep.Acting:
                    if (!needs.IsResting) SetStep(BrainStep.None);   // alguien interrumpió el descanso
                    break;
                case BrainStep.Waiting:
                    if (Time.time >= waitUntil) SetStep(BrainStep.None);
                    break;
            }
        }

        // ================= MOVIMIENTO Y LLEGADA =================

        private void GoTo(Vector3 destination)
        {
            if (sensor.IsAt(destination)) { Arrive(); return; }

            if (actuator.MoveTo(destination)) { SetStep(BrainStep.Moving); return; }

            // Sin camino: esperar y reintentar.
            if (Goal == BrainGoal.Eat) { GiveUpEating(); return; }
            waitUntil = Time.time + retryDelay;
            SetStep(BrainStep.Waiting);
        }

        /// <summary>Ha llegado a su sitio: empezar la acción del objetivo actual.</summary>
        private void Arrive()
        {
            switch (Goal)
            {
                case BrainGoal.Routine:
                    actuator.Work(workRoom);                 // hasta nueva orden
                    break;
                case BrainGoal.Eat:
                    actuator.Eat(needs.RationSize);         // Glotona: 2 raciones
                    break;
                case BrainGoal.Rest:
                    actuator.Stop();                         // descansar solo cuenta quieta
                    needs.StartRest(restRoom);
                    break;
            }
            SetStep(BrainStep.Acting);
        }

        private void OnActionFinished(AntAction action)
        {
            if (IsSuspended) return;

            if (action == AntAction.Moving && Step == BrainStep.Moving && sensor.IsAtTarget)
            {
                Arrive();
                return;
            }

            if (action == AntAction.Eating && Goal == BrainGoal.Eat)
            {
                // Ha comido: vuelve a la rutina. No decide aquí mismo porque AntNeeds aún
                // no ha restado el hambre (llega justo después, con el evento Ate).
                ChangeGoal(BrainGoal.Routine);
                thinkTimer = 0f;
            }
        }

        private void OnAte(int rations)
        {
            if (rations <= 0) eatRetryAt = Time.time + retryDelay;   // no había comida
        }

        private void GiveUpEating()
        {
            eatRetryAt = Time.time + retryDelay;
            ChangeGoal(BrainGoal.Routine);
        }

        private void SetStep(BrainStep step)
        {
            if (Step == step) return;
            Step = step;
            StateChanged?.Invoke(Goal, Step);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showStateLabel || !Application.isPlaying) return;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.6f, IsSuspended ? "(suspendido)" : StateName);
        }
#endif
    }
}
