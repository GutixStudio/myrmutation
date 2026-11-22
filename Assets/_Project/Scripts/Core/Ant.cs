using System.Collections.Generic;
using Myrmutation.Core.Data;
using UnityEngine;

namespace Myrmutation.Core
{
    /// <summary>
    /// Datos compartidos de una hormiga. NO meter lógica aquí: cada sistema añade su propio componente
    /// al prefab (B: AntActuator/AntSensor/AntBrain/AntNeeds · C: AntGenome · F: visual).
    /// </summary>
    public class Ant : MonoBehaviour
    {
        public static readonly List<Ant> All = new List<Ant>();

        [SerializeField] private string antName = "Glotona";
        [SerializeField] private CasteData caste;
        [SerializeField] private bool isQueen;

        public string Name { get => antName; set => antName = value; }
        public CasteData Caste { get => caste; set => caste = value; }
        public bool IsQueen => isQueen;
        public bool IsAlive { get; private set; } = true;

        /// <summary>Genes que tiene (los gestiona C: AntGenome).</summary>
        public readonly List<GeneData> Genes = new List<GeneData>();

        /// <summary>Stats finales = base de la casta + genes. Los recalcula C (AntGenome).</summary>
        public AntStats Stats;

        /// <summary>Sala a la que está asignada (la usa B para su rutina).</summary>
        public Room AssignedRoom { get; set; }

        private void Awake()
        {
            if (caste != null) Stats = caste.baseStats;
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void Start() => EventBus.Publish(new AntSpawned(this));

        public void Kill(DeathCause cause)
        {
            if (!IsAlive) return;
            IsAlive = false;
            EventBus.Publish(new AntDied(this, cause));
            if (isQueen) GameManager.Instance?.Lose();
            Destroy(gameObject);
        }
    }
}
