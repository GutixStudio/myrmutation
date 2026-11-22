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
    ///  NIVEL 1 · OBJETIVO (se elige por prioridad: Necesidad > Excavar > Rutina > Ociosa)
    ///    1. Eat      · IsHungry y hay Despensa y comida  → ir a la Despensa y comer (ración doble si es Glotona)
    ///    2. Rest     · IsTired (o agotada)                → ir a su sala (o al túnel más cercano) y descansar
    ///    3. Dig      · obrera sin sala y hay planos (B6)  → pedir un bloque (Digging.Current), ir a él y excavarlo
    ///    4. Routine  · tiene sala (o es la reina)          → ir a su sala asignada y trabajar (la reina espera)
    ///    5. Idle     · obrera sin sala ni planos (B8)      → ir a la Sala Real (o junto a la entrada) y dar paseos cortos
    ///  Excavar solo lo hacen obreras vivas, sin sala y que no sean la reina. Las de expedición tienen el cerebro
    ///  suspendido, así que nunca llegan a pedir. Al dejar Excavar por lo que sea (hambre, cansancio, le asignan sala,
    ///  suspensión, muerte) suelta el bloque con DigJob.Release; el avance se conserva y luego vuelve a pedir.
    ///  Comer va por delante de descansar porque el hambre mata y el cansancio no.
    ///  Una necesidad no se abandona a medias: come hasta terminar y descansa hasta IsRested.
    ///  Al acabar una necesidad "vuelve": la Rutina la lleva otra vez a su sala.
    ///
    ///  NIVEL 2 · PASO dentro del objetivo
    ///    None → Moving (camino a su sitio) → Acting (trabajando / comiendo / descansando / parada entre paseos)
    ///    Waiting = no puede hacer nada ahora (sin camino…): reintenta más tarde.
    ///
    ///  REPARTO EN LAS SALAS (B8): para trabajar, comer y descansar en una sala no va a su WorkPosition (el centro),
    ///  sino a un sitio de su suelo que le da AntSpread.SpotIn: el más separado de las demás, lejos de las puertas.
    ///  Si se queda parada en una puerta (o en su zona de acceso), se aparta hacia dentro de esa sala.
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

        [Header("Ociosa (B8)")]
        [Tooltip("Segundos mínimos parada entre un paseo y el siguiente")]
        [SerializeField] private float idlePauseMin = 5f;
        [Tooltip("Segundos máximos parada entre un paseo y el siguiente")]
        [SerializeField] private float idlePauseMax = 10f;
        [Tooltip("Distancia máxima (u) de cada paseo")]
        [SerializeField] private float strollRange = 1.5f;
        [Tooltip("Velocidad de los paseos (× la de su casta)")]
        [SerializeField] private float strollSpeed = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool logTransitions;
        [Tooltip("Muestra el estado encima de la hormiga en la vista Scene")]
        [SerializeField] private bool showStateLabel = true;

        private Ant ant;
        private AntActuator actuator;
        private AntSensor sensor;
        private AntNeeds needs;
        private AntSpread spread;       // reparto en las salas (B8); sin él, va a la WorkPosition como antes

        private float thinkTimer;
        private float eatRetryAt;       // no intenta comer antes de este momento
        private float waitUntil;        // fin del paso Waiting
        private float idleUntil;        // fin de la parada entre paseos (Idle/Acting)
        private float stepAsideRetryAt; // no intenta apartarse de una puerta antes de este momento
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
            spread = GetComponent<AntSpread>();
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
                case BrainGoal.Idle: TickIdle(); break;
            }

            StepAsideFromDoor();
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
            // Obrera sin sala y sin planos: ociosa (B8). La reina sigue en Rutina (espera en su sitio).
            if (!ant.IsQueen && ant.AssignedRoom == null) return BrainGoal.Idle;
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

            // Sin sala (la reina) o sin poder trabajar: espera quieta.
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
                    GoTo(SpotIn(workRoom));
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
                    GoTo(SpotIn(storage));
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
                    Vector3 target = restRoom != null ? SpotIn(restRoom) : sensor.NearestTunnelPoint();
                    // B8: el túnel más cercano puede ser una puerta: entonces descansa dentro de esa sala.
                    if (restRoom == null && spread != null && spread.IsInDoorway(target, out var doorRoom))
                    {
                        restRoom = doorRoom;
                        target = SpotIn(doorRoom);
                    }
                    GoTo(target);
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

        // ---- Ociosa (B8): sin sala ni planos → a la Sala Real (o la entrada) y paseos cortos ----
        private void TickIdle()
        {
            switch (Step)
            {
                case BrainStep.None:
                    var royal = sensor.NearestRoom(RoomType.Royal);
                    if (royal != null)
                    {
                        // Primero entra en la sala; ya dentro, pasea por su suelo despacio.
                        if (spread == null || !spread.IsInside(royal, transform.position)) GoTo(SpotIn(royal));
                        else GoTo(spread.StrollSpotIn(royal, strollRange), strollSpeed);
                        break;
                    }
                    // Sin Sala Real (o sin camino hasta ella): junto a la entrada, en la superficie.
                    var entrance = NestEntrance.Instance;
                    if (entrance == null) { Wait(); break; }
                    Vector3 crater = entrance.Position;
                    bool near = Mathf.Abs(transform.position.x - crater.x) <= strollRange &&
                                Mathf.Abs(transform.position.y - crater.y) <= 0.5f;
                    if (!near) GoTo(crater);
                    else GoTo(crater + Vector3.right * UnityEngine.Random.Range(-strollRange, strollRange), strollSpeed);
                    break;
                case BrainStep.Moving:
                    if (actuator.Current != AntAction.Moving) SetStep(BrainStep.None);   // alguien la paró
                    break;
                case BrainStep.Acting:
                    if (Time.time >= idleUntil) SetStep(BrainStep.None);                 // otro paseo
                    break;
                case BrainStep.Waiting:
                    if (Time.time >= waitUntil) SetStep(BrainStep.None);
                    break;
            }
        }

        // ================= MOVIMIENTO Y LLEGADA =================

        /// <summary>Sitio repartido en el suelo de la sala (B8); sin AntSpread, su WorkPosition.</summary>
        private Vector3 SpotIn(Room room) => spread != null ? spread.SpotIn(room) : room.WorkPosition;

        private void GoTo(Vector3 destination, float speedScale = 1f)
        {
            if (sensor.IsAt(destination)) { HasNoPath = false; Arrive(); return; }

            if (actuator.MoveTo(destination, speedScale)) { HasNoPath = false; SetStep(BrainStep.Moving); return; }

            // Sin camino: esperar y reintentar.
            HasNoPath = true;
            if (Goal == BrainGoal.Eat) { GiveUpEating(); return; }
            if (Goal == BrainGoal.Dig) ReleaseDigJob();   // que lo coja otra que sí llegue
            Wait();
        }

        private void Wait()
        {
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
                case BrainGoal.Idle:
                    actuator.Stop();                         // parada un rato antes del siguiente paseo
                    idleUntil = Time.time + UnityEngine.Random.Range(idlePauseMin, idlePauseMax);
                    break;
            }
            SetStep(BrainStep.Acting);
        }

        /// <summary>
        /// B8: si está parada (sin acción) en una puerta o en su zona de acceso, se aparta hacia dentro de esa sala.
        /// No toca el paso del objetivo: en cuanto este le dé otra orden, la sigue. No mueve a la reina ni a las que
        /// descansan (su sitio ya lo eligió TickRest). Sin camino, espera retryDelay antes de volver a intentarlo.
        /// </summary>
        private void StepAsideFromDoor()
        {
            if (spread == null || ant.IsQueen || !actuator.IsIdle || needs.IsResting) return;
            if (Time.time < stepAsideRetryAt) return;
            if (!spread.IsInDoorway(transform.position, out var room)) return;
            if (!actuator.MoveTo(spread.SpotIn(room))) stepAsideRetryAt = Time.time + retryDelay;
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
                Wait();
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
