using System;
using System.Collections.Generic;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using UnityEngine;

namespace Myrmutation.Genetics
{
    /// 
    /// Genoma por casta que guarda la reina (C2). Cuando la reina devora a una mutante, sus genes
    /// (y taras) pasan al genoma de SU casta, y todas las crías futuras de esa casta nacen con ellos.
    ///
    /// Va en la reina. Solo debe haber uno (QueenGenome.Instance).
    /// Publica GeneInherited cada vez que un gen entra en el genoma de una casta.
    /// 
    public class QueenGenome : MonoBehaviour
    {
        public static QueenGenome Instance { get; private set; }

        [Serializable]
        private struct CasteGene
        {
            public CasteType caste;
            public GeneData gene;
        }

        [Tooltip("Una pieza por ranura: un gen nuevo sustituye al anterior de la misma ranura en ese genoma")]
        [SerializeField] private bool replaceSameSlot = true;

        [Tooltip("Genes que la casta ya tiene al empezar (pruebas, y genes iniciales de la beta)")]
        [SerializeField] private List<CasteGene> startingGenes = new List<CasteGene>();

        private readonly Dictionary<CasteType, List<GeneData>> genomes = new Dictionary<CasteType, List<GeneData>>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Hay más de un QueenGenome en la escena; se ignora este.", this);
                Destroy(this); // OJO: Destroy(this), no gameObject: este componente vive en la reina
                return;
            }
            Instance = this;

            foreach (CasteType c in Enum.GetValues(typeof(CasteType)))
                genomes[c] = new List<GeneData>();

            foreach (var s in startingGenes) AddGene(s.caste, s.gene, notify: false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------- Consulta ----------

        public IReadOnlyList<GeneData> GetGenes(CasteType caste) => genomes[caste];

        public bool HasGene(CasteType caste, GeneData gene) => gene != null && genomes[caste].Contains(gene);

        // ---------- Herencia ----------

        /// 
        /// La reina devora a una mutante: todos sus genes pasan al genoma de su casta.
        /// Devuelve cuántos genes nuevos entraron. No mata ni toca a la mutante (eso lo hace el Festín).
        /// 
        public int InheritFrom(Ant mutant)
        {
            if (mutant == null || mutant.Caste == null) return 0;

            int added = 0;
            foreach (var gene in mutant.Genes)
                if (AddGene(mutant.Caste.caste, gene)) added++;
            return added;
        }

        /// Añade un gen al genoma de una casta. Devuelve false si es nulo o ya estaba.
        public bool AddGene(CasteType caste, GeneData gene, bool notify = true)
        {
            if (gene == null) return false;
            var genome = genomes[caste];
            if (genome.Contains(gene)) return false;

            if (replaceSameSlot && gene.slot != BodySlot.None)
                genome.RemoveAll(g => g != null && g.slot == gene.slot);

            genome.Add(gene);
            if (notify) EventBus.Publish(new GeneInherited(caste, gene));
            return true;
        }

        /// Copia a una hormiga los genes del genoma de su casta. Lo llama AntGenome al nacer.
        public void ApplyTo(AntGenome target)
        {
            var caste = target.Owner.Caste;
            if (caste == null) return;

            foreach (var gene in genomes[caste.caste])
                target.AddGene(gene);
        }
    }
}