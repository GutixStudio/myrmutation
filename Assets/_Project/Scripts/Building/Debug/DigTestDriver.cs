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
    /// Tecla X: suelta todos los bloques y para (otra vez X, sigue). Las hormigas necesitan AntActuator y el AntBrain apagado.
    /// </summary>
    public class DigTestDriver : MonoBehaviour
    {
        [Tooltip("Distancia a StandPosition, en unidades, a la que la obrera empieza a excavar")]
        [SerializeField] private float workDistance = 0.15f;
        [Tooltip("Fuerza si la hormiga no tiene casta (Stats.strength = 0)")]
        [SerializeField] private float fallbackStrength = 1f;
        [Tooltip("Segundos que una obrera que soltó un bloque por quedarse lejos no puede volver a pedir ese mismo bloque, o que una obrera sin ruta no pide ninguno")]
        [SerializeField] private float retryDelay = 1f;

        private readonly Dictionary<Ant, DigJob> jobs = new Dictionary<Ant, DigJob>();
        // Bloque soltado por "parada lejos" (o null si no había ruta: ningún bloque) y hasta cuándo no se puede volver a pedir. Solo se escribe al soltar.
        private readonly Dictionary<Ant, (DigJob job, float until)> blocked = new Dictionary<Ant, (DigJob, float)>();
        private bool paused, warnedNoCaste, warnedFar;
        private int moving, working;   // del último frame, para el inspector

        private void Update()
        {
            moving = working = 0;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.xKey.wasPressedThisFrame) TogglePause();
            if (paused || Digging.Current == null) return;

            foreach (var ant in Ant.All)
            {
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

                if (job.IsDone) { Drop(ant, job); continue; }
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

                job.Work(Strength(ant), Time.deltaTime);
                working++;
            }
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

        private void TogglePause()
        {
            paused = !paused;
            if (!paused) return;
            foreach (var pair in jobs)
            {
                if (pair.Key == null) continue;
                pair.Value.Release(pair.Key);
                if (pair.Key.TryGetComponent<AntActuator>(out var actuator)) actuator.Stop();
            }
            jobs.Clear();
            blocked.Clear();
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
                    UnityEditor.EditorGUILayout.Toggle("Parado (X)", d.paused);
                }
            }
        }
#endif
    }
}
