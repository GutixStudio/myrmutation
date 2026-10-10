using System;
using System.Collections.Generic;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using UnityEngine;

namespace Myrmutation.Genetics
{
    
    /// Genoma de una hormiga (C2). Gestiona la lista Ant.Genes y mantiene Ant.Stats al día:
    /// Stats = stats base de la casta + modificadores de TODOS sus genes.
    /// Se recalcula solo al añadir o quitar un gen.
    ///
    /// Las crías nuevas heredan automáticamente los genes que la reina haya acumulado para su casta
    /// (ver QueenGenome). Este componente NO publica AntMutated: eso lo hace quien dispare la mutación
    /// (el Festín), porque heredar al nacer no es una mutación.

    [RequireComponent(typeof(Ant))]
    [DefaultExecutionOrder(10)] // después de Ant.Awake, que fija Stats = baseStats de la casta
    public class AntGenome : MonoBehaviour
    {
        [Tooltip("Una pieza por ranura: un gen nuevo sustituye al anterior de la misma ranura (regla de la alfa)")]
        [SerializeField] private bool replaceSameSlot = true;

        [Tooltip("Al nacer, copia los genes que la reina tiene para la casta de esta hormiga")]
        [SerializeField] private bool inheritFromQueen = true;

        [Tooltip("Genes con los que empieza (útil para probar en las escenas de Test)")]
        [SerializeField] private List<GeneData> initialGenes = new List<GeneData>();

        [Header("Solo lectura: refleja Ant.Genes en Play (editarla no hace nada)")]
        [SerializeField] private List<GeneData> currentGenes = new List<GeneData>();

        private Ant owner;
        private readonly List<StatModifier> modifierBuffer = new List<StatModifier>();

        //Se dispara tras cada recálculo (por si el visual o la UI quieren refrescarse).
        public event Action Changed;

        public Ant Owner => owner != null ? owner : (owner = GetComponent<Ant>());
        public IReadOnlyList<GeneData> Genes => Owner.Genes;

        private void Awake()
        {
            foreach (var g in initialGenes) AddGene(g);
            Recalculate();
        }

        private void Start()
        {
            if (inheritFromQueen && !Owner.IsQueen && QueenGenome.Instance != null)
                QueenGenome.Instance.ApplyTo(this);

            // Por si la casta se asignó después del Awake (p. ej. al instanciar desde la guardería).
            Recalculate();
        }

        // ---------- API pública ----------

        public bool HasGene(GeneData gene) => gene != null && Owner.Genes.Contains(gene);

        /// ¿Tiene algún gen con este behaviorTag? (p. ej. "Glotona"). Lo usa la IA de B.
        public bool HasBehaviorTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return false;
            foreach (var g in Owner.Genes)
                if (g != null && g.behaviorTag == tag) return true;
            return false;
        }

        ///Añade un gen y recalcula. Devuelve false si es nulo o ya lo tenía.
        public bool AddGene(GeneData gene)
        {
            if (gene == null || Owner.Genes.Contains(gene)) return false;

            if (replaceSameSlot && gene.slot != BodySlot.None)
                Owner.Genes.RemoveAll(g => g != null && g.slot == gene.slot);

            Owner.Genes.Add(gene);
            Recalculate();
            return true;
        }

        /// Quita un gen y recalcula. Devuelve false si no lo tenía.
        public bool RemoveGene(GeneData gene)
        {
            if (gene == null || !Owner.Genes.Remove(gene)) return false;
            Recalculate();
            return true;
        }

        ///Stats = base de la casta + modificadores de todos los genes.
        public void Recalculate()
        {
            var ant = Owner;
            var baseStats = ant.Caste != null ? ant.Caste.baseStats : AntStats.Default;

            modifierBuffer.Clear();
            foreach (var g in ant.Genes)
                if (g != null) modifierBuffer.AddRange(g.modifiers);

            ant.Stats = baseStats.WithModifiers(modifierBuffer);

            currentGenes.Clear();
            currentGenes.AddRange(ant.Genes);

            Changed?.Invoke();
        }
    }
}