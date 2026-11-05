using System;
using Myrmutation.Building;
using Myrmutation.Core;
using UnityEngine;

namespace Myrmutation.Ants
{
    /// <summary>
    /// Cerebro de la hormiga (B2): máquina de estados jerárquica de dos niveles.
    /// Guía para el equipo (cómo integrarse con la IA): docs/IA_Hormigas.md
    ///
    ///  NIVEL 1 · OBJETIVO (se elige por prioridad: Necesidad > Excavar > Rutina)
    ///    1. Eat      · IsHungry y hay Despensa y comida  → ir a la Despensa y comer (ración doble si es Glotona)
    ///    2. Rest     · IsTired (o agotada)                → ir a su sala (o al túnel más cercano) y descansar
    ///    3. Dig      · obrera sin sala y hay planos (B6)  → pedir un bloque (Digging.Current), ir a él y excavarlo
    ///    4. Routine  · lo demás                            → ir a su sala asignada y trabajar (sin sala: esperar)
    ///  Excavar solo lo hacen obreras vivas, sin sala y que no sean la reina. Las de expedición tienen el cerebro
    ///  suspendido, así que nunca llegan a pedir. Al dejar Excavar por lo que sea (hambre, cansancio, le asignan sala,
    ///  suspensión, muerte) suelta el bloque con DigJob.Release; el avance se conserva y luego vuelve a pedir.
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

        [Header("Excavar (B6)")]
        [Tooltip("Fuerza si la hormiga no tiene casta (Stats.strength = 0)")]
        [SerializeField] private float fallbackStrength = 1f;

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
        private DigJob digJob;          // bloque reservado (va hacia él o lo excava); null = ninguno

        public BrainGoal Goal { get; private set; } = BrainGoal.Routine;
        public BrainStep Step { get; private set; } = BrainStep.None;
        public string StateName => $"{Goal}/{Step}";
        public bool IsSuspended { get; private set; }
        public bool HasNoPath { get; private set; }

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
            ReleaseDigJob();
        }

        private void Update()
        {
            if (!ant.IsAlive) { ReleaseDigJob(); return; }
            if (IsSuspended) return;
            if (Goal == BrainGoal.Dig) UpdateDig();
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
                case BrainGoal.Dig: TickDig(); break;
            }
        }

        public void Suspend()
        {
            if (IsSuspended) return;
            if (needs.IsResting) needs.StopRest();
            ReleaseDigJob();
            if (actuator.Current == AntAction.Digging) actuator.Stop();   // que no se quede cabeceando
            IsSuspended = true;
            HasNoPath = false;
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

            if (WantsToDig()) return BrainGoal.Dig;
            return BrainGoal.Routine;
        }

        /// <summary>Obrera sin sala y hay planos que excavar (o ya tiene un bloque reservado).</summary>
        private bool WantsToDig()
        {
            if (ant.IsQueen || ant.AssignedRoom != null) return false;
            var dig = Digging.Current;
            if (dig == null) return false;
            return digJob != null || dig.HasWork;
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
            ReleaseDigJob();
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
                HasNoPath = false;   // no va a ningún sitio: no hay camino que buscar
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

        // ---- Excavar (B6): pedir bloque → ir a StandPosition → excavar hasta IsDone ----
        private void TickDig()
        {
            switch (Step)
            {
                case BrainStep.None:
                    // Sin bloque: pide (hay que seguir pidiendo en cada ciclo hasta que el reparto le dé uno).
                    if (digJob == null && !ClaimDigJob()) { HasNoPath = false; break; }
                    GoTo(digJob.StandPosition);
                    break;
                case BrainStep.Moving:
                    if (actuator.Current != AntAction.Moving) SetStep(BrainStep.None);   // alguien la paró
                    break;
                case BrainStep.Acting:
                    if (actuator.Current != AntAction.Digging) SetStep(BrainStep.None);  // alguien la paró
                    break;
                case BrainStep.Waiting:
                    if (Time.time >= waitUntil) SetStep(BrainStep.None);
                    break;
            }
        }

        /// <summary>
        /// Cada frame mientras excava: suma su fuerza al bloque y, si se ha terminado, cancelado o se lo ha quitado otra
        /// más cercana (aún no lo excavaba), lo suelta y pide el siguiente en el mismo frame (excavación continua).
        /// </summary>
        private void UpdateDig()
        {
            // Le acaban de asignar sala: que lo decida ya, no en el siguiente ciclo.
            if (ant.AssignedRoom != null) { thinkTimer = 0f; return; }
            if (digJob == null) return;

            if (digJob.IsDone || !digJob.HasWorker(ant))
            {
                ReleaseDigJob();
                if (ClaimDigJob()) GoTo(digJob.StandPosition);
                else { actuator.Stop(); SetStep(BrainStep.None); }
                return;
            }

            if (Step == BrainStep.Acting && actuator.Current == AntAction.Digging)
                digJob.Work(ant, Strength, Time.deltaTime);
        }

        private bool ClaimDigJob()
        {
            var dig = Digging.Current;
            if (dig == null || !dig.TryClaim(ant, out var job)) return false;
            digJob = job;
            return true;
        }

        /// <summary>Suelta el bloque reservado (el avance se conserva). Llamarla de más es seguro.</summary>
        private void ReleaseDigJob()
        {
            if (digJob == null) return;
            digJob.Release(ant);
            digJob = null;
        }

        private float Strength => ant.Stats.strength > 0f ? ant.Stats.strength : fallbackStrength;

        // ================= MOVIMIENTO Y LLEGADA =================

        private void GoTo(Vector3 destination)
        {
            if (sensor.IsAt(destination)) { HasNoPath = false; Arrive(); return; }

            if (actuator.MoveTo(destination)) { HasNoPath = false; SetStep(BrainStep.Moving); return; }

            // Sin camino: esperar y reintentar.
            HasNoPath = true;
            if (Goal == BrainGoal.Eat) { GiveUpEating(); return; }
            if (Goal == BrainGoal.Dig) ReleaseDigJob();   // que lo coja otra que sí llegue
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
                case BrainGoal.Dig:
                    if (digJob == null) { SetStep(BrainStep.None); return; }
                    actuator.Dig(digJob.FacingRight);        // mirando al bloque; el avance lo suma UpdateDig
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

            // La ruta acabó lejos del bloque: soltarlo y esperar, para no pedir y soltar el mismo en bucle.
            if (action == AntAction.Moving && Step == BrainStep.Moving && Goal == BrainGoal.Dig)
            {
                ReleaseDigJob();
                waitUntil = Time.time + retryDelay;
                SetStep(BrainStep.Waiting);
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
