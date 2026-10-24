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
    /// leídos con AntSensor), si le asignan sala o si otro sistema se la lleva, y no la vuelve a tomar hasta que se recupere.
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

        private readonly Dictionary<Ant, DigJob> jobs = new Dictionary<Ant, DigJob>();
        // Bloque soltado por "parada lejos" (o null si no había ruta: ningún bloque) y hasta cuándo no se puede volver a pedir. Solo se escribe al soltar.
        private readonly Dictionary<Ant, (DigJob job, float until)> blocked = new Dictionary<Ant, (DigJob, float)>();
        private readonly HashSet<Ant> crew = new HashSet<Ant>();
        // Soltadas por hambre o cansancio: no vuelven a la cuadrilla hasta que el cerebro las daría por recuperadas.
        private readonly HashSet<Ant> recovering = new HashSet<Ant>();
        private readonly List<Ant> leaving = new List<Ant>();
        private bool appliedPause, warnedNoCaste, warnedFar;
        private int moving, working;   // del último frame, para el inspector
        // Obreras sin bloque a idleNearCells casillas o menos de algo que podrían cavar (debería ser 0 casi siempre).
        private int idleNearWork, maxIdleNearWork;
        private float idleTimer;

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
            if (ant == null || !ant.IsAlive || !ant.isActiveAndEnabled || ant.AssignedRoom != null) return true;
            // Otro sistema le ha reanudado el cerebro (p. ej. al volver de una expedición).
            if (!ant.TryGetComponent<IAntBrain>(out var brain) || !brain.IsSuspended) return true;
            if (!NeedsBreak(ant)) return false;
            recovering.Add(ant);
            return true;
        }

        private bool CanJoin(Ant ant, out IAntBrain brain)
        {
            brain = null;
            if (!ant.IsAlive || ant.IsQueen || ant.AssignedRoom != null) return false;
            if (!ant.TryGetComponent(out brain) || brain.IsSuspended || brain.Goal != BrainGoal.Routine) return false;
            if (!ant.TryGetComponent<AntSensor>(out var sensor)) return false;
            if (!recovering.Contains(ant)) return !NeedsBreak(ant);
            // Recuperada como la daría el cerebro: saciada y descansada (si se agotó, con la energía completa).
            if (sensor.IsHungry || !sensor.IsRested || !sensor.CanWork) return false;
            recovering.Remove(ant);
            return true;
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
            if (ant == null || !ant.isActiveAndEnabled) return;   // fuera de la colonia: quien se la llevó le devuelve el cerebro
            if (ant.TryGetComponent<AntActuator>(out var actuator)) actuator.Stop();
            if (ant.TryGetComponent<IAntBrain>(out var brain) && brain.IsSuspended) brain.Resume();
        }

        private void ApplyPause()
        {
            appliedPause = paused;
            if (paused) ReleaseAll();
        }

        private void OnDisable() => ReleaseAll();

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
