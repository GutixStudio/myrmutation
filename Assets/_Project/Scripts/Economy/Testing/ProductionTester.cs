using System.Collections.Generic;
using Myrmutation.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Myrmutation.Economy.Testing
{
    /// <summary>
    /// SOLO PRUEBAS (Scenes/Test/D.unity). Prepara una prueba de ColonyProduction sin depender de
    /// construcción ni de la IA: marca salas como construidas, asigna hormigas y añade hojas.
    /// Muestra un panel con recursos y estado de cada sala. No ponerlo en Game.unity.
    /// </summary>
    public class ProductionTester : MonoBehaviour
    {
        [System.Serializable]
        private struct Assignment
        {
            public Room room;
            [Tooltip("Hormigas libres que se asignan a esta sala al empezar")]
            public int ants;
        }

        [Tooltip("Salas a las que se llama CompleteBuild() al empezar")]
        [SerializeField] private List<Room> roomsToBuild = new List<Room>();
        [SerializeField] private List<Assignment> assignments = new List<Assignment>();

        [Header("Recursos")]
        [SerializeField] private int startLeaves = 20;
        [SerializeField] private int leavesPerKeyPress = 10;
        [SerializeField] private Key addLeavesKey = Key.L;

        [Header("Panel")]
        [SerializeField] private bool showPanel = true;

        private readonly List<Ant> buffer = new List<Ant>();

        private void Start()
        {
            foreach (var r in roomsToBuild) if (r != null) r.CompleteBuild();

            foreach (var asg in assignments)
            {
                if (asg.room == null) continue;
                int left = asg.ants;
                foreach (var a in Ant.All)
                {
                    if (left <= 0) break;
                    if (a == null || a.IsQueen || !a.IsAlive || a.AssignedRoom != null) continue;
                    a.AssignedRoom = asg.room;
                    left--;
                }
                if (left > 0) Debug.LogWarning($"[ProductionTester] Faltan {left} hormigas libres para {asg.room.name}", this);
            }

            if (startLeaves > 0) ResourceManager.Instance?.Add(ResourceType.Leaves, startLeaves);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[addLeavesKey].wasPressedThisFrame) AddLeaves();
        }

        [ContextMenu("Añadir hojas")]
        private void AddLeaves() => ResourceManager.Instance?.Add(ResourceType.Leaves, leavesPerKeyPress);

        [ContextMenu("Desasignar todas")]
        private void UnassignAll()
        {
            foreach (var a in Ant.All) if (a != null && !a.IsQueen) a.AssignedRoom = null;
        }

        private void OnGUI()
        {
            if (!showPanel) return;
            var rm = ResourceManager.Instance;
            var cp = ColonyProduction.Instance;

            GUILayout.BeginArea(new Rect(10, 10, 360, 600), GUI.skin.box);
            GUILayout.Label("<b>D1 · Producción</b>", new GUIStyle(GUI.skin.label) { richText = true });

            if (rm == null) GUILayout.Label("⚠ No hay ResourceManager en la escena");
            else
                GUILayout.Label($"Comida {rm.Get(ResourceType.Food)} · Hojas {rm.Get(ResourceType.Leaves)} · " +
                                $"Hongo {rm.Get(ResourceType.Fungus)} · Biomasa {rm.Get(ResourceType.Biomass)}");

            if (cp == null) { GUILayout.Label("⚠ No hay ColonyProduction en la escena"); GUILayout.EndArea(); return; }

            foreach (var room in Room.All)
            {
                if (room == null || room.Data == null) continue;
                GUILayout.Space(6);
                GUILayout.Label($"{room.Data.displayName} ({(room.IsBuilt ? "construida" : "sin construir")})");

                cp.GetWorkers(room, buffer);
                foreach (var a in buffer)
                    GUILayout.Label($"   · {a.Name} [{(a.Caste != null ? a.Caste.caste.ToString() : "sin casta")}] " +
                                    $"eficiencia x{ColonyProduction.GetEfficiency(a, room.Data.type):0.00}");

                var s = cp.GetState(room);
                if (s != null)
                    GUILayout.Label($"   Potencia {s.WorkPower:0.00} · Ciclo {s.Progress * 100f:0}%" +
                                    $"{(s.Stalled ? " · ESPERANDO MATERIA PRIMA" : "")} · " +
                                    $"{cp.GetOutputPerSecond(room) * 60f:0.0}/min");
            }

            GUILayout.Space(6);
            if (GUILayout.Button($"+{leavesPerKeyPress} hojas ({addLeavesKey})")) AddLeaves();
            GUILayout.EndArea();
        }
    }
}
