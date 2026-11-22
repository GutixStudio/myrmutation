using System.Collections.Generic;
using Myrmutation.Ants;
using Myrmutation.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Myrmutation.Building
{
    /// <summary>
    /// Prueba de A8 (escena Test_A8) mientras no está B6: hace de cuadrilla de excavación.
    /// Cada hormiga libre pide un bloque con Digging.Current.TryClaim, va a StandPosition y excava hasta terminarlo
    /// (o hasta que el planificador lo cancele: IsDone).
    /// Tecla X o "Parado" en el inspector: suelta todos los bloques y para (otra vez, sigue).
    /// Sin cuadrilla, maneja a todas las hormigas, que necesitan AntActuator y el AntBrain apagado (Test_A8).
    /// Con cuadrilla (Game.unity mientras no está B6): toma hasta crewSize obreras sin sala, que no sean la reina, y les
    /// suspende el cerebro; se lo devuelve en cuanto el cerebro iría a comer o a descansar (los umbrales de AntNeeds,
    /// leídos con AntSensor), si le asignan sala o si otro sistema se la lleva, y no la vuelve a tomar hasta que se recupere
    /// según por qué salió: si fue por hambre, en cuanto deja de tenerla; si fue cansada o agotada, cuando ha descansado.
    /// Con logCrew, apunta en la consola cuánto pasa fuera cada obrera y en qué (temporal, para medir).
    /// shortcut: cuadrilla fija con el cerebro suspendido mientras no está B6; quitar DigTestDriver de Game.unity cuando se mezcle B6.
    /// </summary>
    public class DigTestDriver : MonoBehaviour
    {
        [Tooltip("Parado: no manda a nadie, suelta los bloques y, con cuadrilla, devuelve el cerebro a todas (también con la tecla X)")]
        [SerializeField] private bool paused;
        [Header("Cuadrilla (Game.unity mientras no está B6)")]
        [Tooltip("Solo maneja una cuadrilla de obreras sin sala, con el cerebro suspendido. Desactivado: todas las hormigas (Test_A8, con el AntBrain apagado)")]
        [SerializeField] private bool useCrew;
        [Tooltip("Obreras que forman la cuadrilla como mucho")]
        [SerializeField] private int crewSize = 3;
        [Header("Excavación")]
        [Tooltip("Distancia a StandPosition, en unidades, a la que la obrera empieza a excavar")]
        [SerializeField] private float workDistance = 0.15f;
        [Tooltip("Fuerza si la hormiga no tiene casta (Stats.strength = 0)")]
        [SerializeField] private float fallbackStrength = 1f;
        [Tooltip("Segundos que una obrera que soltó un bloque por quedarse lejos no puede volver a pedir ese mismo bloque, o que una obrera sin ruta no pide ninguno")]
        [SerializeField] private float retryDelay = 1f;
        [Header("Métrica")]
        [Tooltip("Segundos (sin escala) entre recuentos de obreras libres junto a un bloque que podrían cavar")]
        [SerializeField] private float idleCheckInterval = 0.5f;
        [Tooltip("Casillas (Chebyshev) a las que una obrera libre cuenta como 'junto a' un bloque que se podría trabajar")]
        [SerializeField] private int idleNearCells = 3;
        [Tooltip("Temporal: apunta en la consola cada salida de la cuadrilla, sus cambios de objetivo (comer, descansar, rutina) y la vuelta, con los tiempos")]
        [SerializeField] private bool logCrew;

        private enum LeaveReason { Hunger, Tired }

        // Una obrera fuera de la cuadrilla por hambre o cansancio. Los tiempos son de juego (Time.time) y solo se usan con logCrew.
        private sealed class Away
        {
            public LeaveReason Reason;
            public float LeftAt, GoalSince, EatTime, RestTime, RoutineTime;
            public BrainGoal Goal;
        }

        private readonly Dictionary<Ant, DigJob> jobs = new Dictionary<Ant, DigJob>();
        // Bloque soltado por "parada lejos" (o null si no había ruta: ningún bloque) y hasta cuándo no se puede volver a pedir. Solo se escribe al soltar.
        private readonly Dictionary<Ant, (DigJob job, float until)> blocked = new Dictionary<Ant, (DigJob, float)>();
        private readonly HashSet<Ant> crew = new HashSet<Ant>();
        // Soltadas por hambre o cansancio: no vuelven a la cuadrilla hasta recuperarse según el motivo.
        private readonly Dictionary<Ant, Away> recovering = new Dictionary<Ant, Away>();
        private readonly List<Ant> leaving = new List<Ant>();
        private readonly List<Ant> untracked = new List<Ant>();
        private bool appliedPause, warnedNoCaste, warnedFar;
        private int moving, working;   // del último frame, para el inspector
        // Obreras sin bloque a idleNearCells casillas o menos de algo que podrían cavar (debería ser 0 casi siempre).
        private int idleNearWork, maxIdleNearWork;
        private float idleTimer;
        // Temporal (logCrew): comida de la despensa en el día de juego en curso.
        private int lastFood = -1, dayStartFood = -1, foodConsumed, foodAdded;

        private void OnEnable()
        {
            EventBus.Subscribe<ResourceChanged>(OnResourceChanged);
            EventBus.Subscribe<DayPassed>(OnDayPassed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ResourceChanged>(OnResourceChanged);
            EventBus.Unsubscribe<DayPassed>(OnDayPassed);
            ReleaseAll();
        }

        private void Update()
        {
            moving = working = 0;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.xKey.wasPressedThisFrame) paused = !paused;
            if (paused != appliedPause) ApplyPause();
            if (paused || Digging.Current == null) return;
            if (useCrew) UpdateCrew();
            CountIdleNearWork();

            foreach (var ant in Ant.All)
            {
                if (useCrew && !crew.Contains(ant)) continue;
                if (!ant.IsAlive || !ant.TryGetComponent<AntActuator>(out var actuator)) continue;

                if (!jobs.TryGetValue(ant, out var job))
                {
                    if (!actuator.IsIdle) continue;
                    bool waiting = blocked.TryGetValue(ant, out var b) && Time.time < b.until;
                    if ((waiting && b.job == null) || !Digging.Current.TryClaim(ant, out job)) continue;
                    if (waiting && b.job == job) { job.Release(ant); continue; }
                    if (actuator.MoveTo(job.StandPosition)) jobs[ant] = job;
                    else
                    {
                        job.Release(ant);
                        blocked[ant] = (null, Time.time + retryDelay);
                    }
                    continue;
                }

                // Terminado, cancelado o cedido a otra más cercana (aún no lo excavaba).
                if (job.IsDone || !job.HasWorker(ant))
                {
                    Drop(ant, job);
                    // Excavación continua: si el planificador le guarda otro bloque, va directa sin pasar por Idle.
                    if (!Digging.Current.TryClaim(ant, out job))
                    {
                        if (actuator.Current == AntAction.Moving) actuator.Stop();   // iba a un sitio que ya no es suyo
                        continue;
                    }
                    if (actuator.MoveTo(job.StandPosition)) jobs[ant] = job;
                    else job.Release(ant);
                    continue;
                }
                if (actuator.Current == AntAction.Moving) { moving++; continue; }
                // Parada lejos del sitio: la ruta no ha llegado. Se suelta y no lo vuelve a pedir en retryDelay,
                // para que un fallo así no sea un bucle invisible de pedir y soltar cada frame.
                float far = ((Vector2)(ant.transform.position - job.StandPosition)).magnitude;
                if (far > workDistance)
                {
                    if (!warnedFar)
                    {
                        warnedFar = true;
                        Debug.LogWarning($"[DigTestDriver] {ant.Name} se ha parado a {far:0.00} u de StandPosition (máximo {workDistance}): suelta el bloque {job.Chunk} y no lo pide en {retryDelay} s.", ant);
                    }
                    blocked[ant] = (job, Time.time + retryDelay);
                    Drop(ant, job);
                    continue;
                }

                job.Work(ant, Strength(ant), Time.deltaTime);
                working++;
            }
        }

        private void CountIdleNearWork()
        {
            idleTimer += Time.unscaledDeltaTime;
            if (idleTimer < idleCheckInterval || !(Digging.Current is DigPlanner planner)) return;
            idleTimer = 0f;
            idleNearWork = 0;
            foreach (var ant in Ant.All)
                if (ant.IsAlive && (!useCrew || crew.Contains(ant)) && !jobs.ContainsKey(ant) && planner.HasWorkNear(ant.transform.position, idleNearCells)) idleNearWork++;
            maxIdleNearWork = Mathf.Max(maxIdleNearWork, idleNearWork);
        }

        private float Strength(Ant ant)
        {
            if (ant.Stats.strength > 0f) return ant.Stats.strength;
            if (!warnedNoCaste)
            {
                warnedNoCaste = true;
                Debug.LogWarning($"[DigTestDriver] {ant.Name} no tiene casta (fuerza 0): se usa fallbackStrength = {fallbackStrength}.", ant);
            }
            return fallbackStrength;
        }

        private void Drop(Ant ant, DigJob job)
        {
            job.Release(ant);
            jobs.Remove(ant);
        }

        // ---------- Cuadrilla ----------

        private void UpdateCrew()
        {
            StopTrackingGone();
            if (logCrew) TrackAway();
            leaving.Clear();
            foreach (var ant in crew) if (MustLeave(ant)) leaving.Add(ant);
            foreach (var ant in leaving) Leave(ant);

            foreach (var ant in Ant.All)
            {
                if (crew.Count >= crewSize) return;
                if (!crew.Contains(ant) && CanJoin(ant, out var brain)) Join(ant, brain);
            }
        }

        private bool MustLeave(Ant ant)
        {
            if (ant == null || !ant.IsAlive || !InColony(ant) || ant.AssignedRoom != null) return true;
            // Otro sistema le ha reanudado el cerebro (p. ej. al volver de una expedición).
            if (!ant.TryGetComponent<IAntBrain>(out var brain) || !brain.IsSuspended) return true;
            if (!NeedsBreak(ant)) return false;
            ant.TryGetComponent<AntSensor>(out var sensor);
            var away = new Away
            {
                Reason = sensor.IsTired || !sensor.CanWork ? LeaveReason.Tired : LeaveReason.Hunger,
                LeftAt = Time.time,
                GoalSince = Time.time,
                Goal = BrainGoal.Routine,
            };
            recovering[ant] = away;
            if (logCrew)
                Debug.Log($"[DigTestDriver] {Label(ant)} sale de la cuadrilla por {(away.Reason == LeaveReason.Hunger ? "hambre" : "cansancio")}: " +
                          $"hambre {sensor.Hunger01:0.00}, energía {sensor.Energy01:0.00}, comida en la despensa {Food()}", ant);
            return true;
        }

        private bool CanJoin(Ant ant, out IAntBrain brain)
        {
            brain = null;
            if (!ant.IsAlive || ant.IsQueen || ant.AssignedRoom != null) return false;
            if (!ant.TryGetComponent(out brain) || brain.IsSuspended || brain.Goal != BrainGoal.Routine) return false;
            if (!ant.TryGetComponent<AntSensor>(out var sensor)) return false;
            if (!recovering.TryGetValue(ant, out var away)) return !NeedsBreak(ant);
            // shortcut: regla de vuelta temporal hasta B6 (revisar con Nicolás). Por hambre, en cuanto ha comido;
            // cansada o agotada, descansada como la daría el cerebro (si se agotó, con la energía completa).
            bool recovered = away.Reason == LeaveReason.Hunger
                ? !NeedsBreak(ant)
                : !sensor.IsHungry && sensor.IsRested && sensor.CanWork;
            if (!recovered) return false;
            recovering.Remove(ant);
            if (logCrew) LogBack(ant, away, sensor);
            return true;
        }

        // Temporal (logCrew): tiempo en cada objetivo del cerebro mientras está fuera; solo escribe al cambiar.
        private void TrackAway()
        {
            foreach (var pair in recovering)
            {
                Ant ant = pair.Key;
                if (ant == null || !ant.TryGetComponent<IAntBrain>(out var brain) || brain.IsSuspended) continue;
                Away away = pair.Value;
                if (brain.Goal == away.Goal) continue;
                AddGoalTime(away);
                Debug.Log($"[DigTestDriver] {Label(ant)}: {away.Goal} → {brain.Goal} a los {Time.time - away.LeftAt:0.0} s fuera, " +
                          $"comida en la despensa {Food()}", ant);
                away.Goal = brain.Goal;
            }
        }

        private static void AddGoalTime(Away away)
        {
            float t = Time.time - away.GoalSince;
            away.GoalSince = Time.time;
            if (away.Goal == BrainGoal.Eat) away.EatTime += t;
            else if (away.Goal == BrainGoal.Rest) away.RestTime += t;
            else away.RoutineTime += t;
        }

        private static void LogBack(Ant ant, Away away, AntSensor sensor)
        {
            AddGoalTime(away);
            Debug.Log($"[DigTestDriver] {Label(ant)} vuelve a la cuadrilla tras {Time.time - away.LeftAt:0.0} s " +
                      $"(salió por {(away.Reason == LeaveReason.Hunger ? "hambre" : "cansancio")}; comer {away.EatTime:0.0} s, " +
                      $"descansar {away.RestTime:0.0} s, rutina {away.RoutineTime:0.0} s): hambre {sensor.Hunger01:0.00}, " +
                      $"energía {sensor.Energy01:0.00}, comida en la despensa {Food()}", ant);
        }

        // Fuera de la colonia (expedición, salida por la entrada) o muerta: deja de seguirla. Si vuelve, entra como una más.
        private void StopTrackingGone()
        {
            untracked.Clear();
            foreach (var pair in recovering)
                if (pair.Key == null || !pair.Key.IsAlive || !InColony(pair.Key)) untracked.Add(pair.Key);
            foreach (var ant in untracked)
            {
                if (logCrew && ant != null) Debug.Log($"[DigTestDriver] {Label(ant)} sale de la colonia o muere: deja de seguirla", ant);
                recovering.Remove(ant);
            }
        }

        // Dentro de la colonia (en Ant.All): activa y con Ant habilitado. No usa isActiveAndEnabled, que depende de OnEnable.
        private static bool InColony(Ant ant) => ant.enabled && ant.gameObject.activeInHierarchy;

        // El nombre se repite entre hormigas (Nursery): el id las distingue en el log.
        private static string Label(Ant ant) => $"{ant.Name}#{ant.GetInstanceID()}";

        private static string Food() =>
            ResourceManager.Instance != null ? ResourceManager.Instance.Get(ResourceType.Food).ToString() : "—";

        // Temporal (logCrew): entradas y consumo de comida por día de juego, a partir del total que publica ResourceManager.
        private void OnResourceChanged(ResourceChanged e)
        {
            if (e.Type != ResourceType.Food) return;
            if (lastFood >= 0)
            {
                int delta = e.Amount - lastFood;
                if (delta > 0) foodAdded += delta;
                else foodConsumed -= delta;
            }
            if (dayStartFood < 0) dayStartFood = e.Amount;
            lastFood = e.Amount;
        }

        private void OnDayPassed(DayPassed e)
        {
            if (logCrew)
                Debug.Log($"[DigTestDriver] Resumen del día {e.Day - 1}: comida al empezar {(dayStartFood >= 0 ? dayStartFood.ToString() : "—")}, " +
                          $"consumo {foodConsumed}, entradas {foodAdded}, ahora {Food()}", this);
            dayStartFood = lastFood;
            foodConsumed = foodAdded = 0;
        }

        // Lo que haría que AntBrain.ChooseGoal la mandase a comer o a descansar.
        private static bool NeedsBreak(Ant ant) =>
            ant.TryGetComponent<AntSensor>(out var sensor) && (sensor.IsHungry || sensor.IsTired || !sensor.CanWork);

        private void Join(Ant ant, IAntBrain brain)
        {
            brain.Suspend();
            if (ant.TryGetComponent<AntActuator>(out var actuator)) actuator.Stop();
            crew.Add(ant);
        }

        private void Leave(Ant ant)
        {
            if (jobs.TryGetValue(ant, out var job)) Drop(ant, job);
            crew.Remove(ant);
            if (ant == null || !InColony(ant)) return;   // fuera de la colonia: quien se la llevó le devuelve el cerebro
            if (ant.TryGetComponent<AntActuator>(out var actuator)) actuator.Stop();
            if (ant.TryGetComponent<IAntBrain>(out var brain) && brain.IsSuspended) brain.Resume();
        }

        private void ApplyPause()
        {
            appliedPause = paused;
            if (paused) ReleaseAll();
        }

        private void ReleaseAll()
        {
            foreach (var pair in jobs)
            {
                if (pair.Key == null) continue;
                pair.Value.Release(pair.Key);
                if (pair.Key.TryGetComponent<AntActuator>(out var actuator)) actuator.Stop();
            }
            jobs.Clear();
            blocked.Clear();
            leaving.Clear();
            leaving.AddRange(crew);
            foreach (var ant in leaving) Leave(ant);
        }

#if UNITY_EDITOR
        // Contadores de solo lectura en el inspector, para ver si la cuadrilla avanza.
        [UnityEditor.CustomEditor(typeof(DigTestDriver))]
        private class Inspector : UnityEditor.Editor
        {
            public override bool RequiresConstantRepaint() => Application.isPlaying;

            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();
                if (!Application.isPlaying) return;
                var d = (DigTestDriver)target;
                using (new UnityEditor.EditorGUI.DisabledScope(true))
                {
                    UnityEditor.EditorGUILayout.IntField("Con trabajo", d.jobs.Count);
                    UnityEditor.EditorGUILayout.IntField("De camino", d.moving);
                    UnityEditor.EditorGUILayout.IntField("Excavando", d.working);
                    UnityEditor.EditorGUILayout.IntField("Libres junto a trabajo", d.idleNearWork);
                    UnityEditor.EditorGUILayout.IntField("Máx. libres junto a trabajo", d.maxIdleNearWork);
                    if (d.useCrew) UnityEditor.EditorGUILayout.IntField("En la cuadrilla", d.crew.Count);
                }
            }
        }
#endif
    }
}
