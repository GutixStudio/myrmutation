using System;
using System.Collections.Generic;
using Myrmutation.Ants;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Myrmutation.Genetics
{
    public enum FeastOutcome { Nothing, Gene, Flaw, Inherited }

    
    public readonly struct FeastResult
    {
        public readonly Ant Eater;
        public readonly Ant Victim;          
        public readonly FeastOutcome Outcome;
        public readonly GeneData Gene;      
        public readonly int Biomass;

        public FeastResult(Ant eater, Ant victim, FeastOutcome outcome, GeneData gene, int biomass)
        {
            Eater = eater; Victim = victim; Outcome = outcome; Gene = gene; Biomass = biomass;
        }
    }

 
    [DisallowMultipleComponent]
    public class FeastSystem : MonoBehaviour
    {
        public static FeastSystem Instance { get; private set; }

        public const string DevourLabel = "Devorar";

        [Header("Reglas")]
        [Tooltip("Si está activo, solo se puede devorar con una Cámara del Festín construida (RoomType.Feast)")]
        [SerializeField] private bool requireFeastRoom = false;

        [Header("Taras posibles (arrastrar aquí Flaw_Glotona; en la alfa es la única)")]
        [SerializeField] private List<GeneData> flawPool = new List<GeneData>();

        [Header("Probabilidades (0..1) · GDD §5.7")]
        [Tooltip("Hormiga propia CON genes: probabilidad de copiar uno de sus genes (el resto es tara)")]
        [Range(0f, 1f)][SerializeField] private float ownWithGenes_Gene = 0.8f;
        [Tooltip("Hormiga propia SIN genes: probabilidad de tara (el resto es nada)")]
        [Range(0f, 1f)][SerializeField] private float ownWithoutGenes_Flaw = 0.4f;
        [Tooltip("Presa: probabilidad de su gen")]
        [Range(0f, 1f)][SerializeField] private float prey_Gene = 0.7f;
        [Tooltip("Presa: probabilidad de tara (el resto es nada)")]
        [Range(0f, 1f)][SerializeField] private float prey_Flaw = 0.2f;

        [Header("Biomasa que da")]
        [SerializeField] private int ownBiomass = 3;
        [SerializeField] private int preyBiomass = 5;

        [Header("Debug")]
        [SerializeField] private bool logResults = true;

        
        public event Action<FeastResult> Devoured;

        
        public bool IsChoosingVictim => eater != null;
        public Ant Eater => eater;

        private enum Roll { Nothing, Gene, Flaw }

        private Ant eater;
        private Ant pendingEater, pendingVictim;
        private readonly List<GeneData> candidates = new List<GeneData>();

     

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Hay más de un FeastSystem en la escena; se ignora este.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            if (Instance != this) return;
            AntPanelAction.Register(DevourLabel, CanStartDevour, StartDevour);
            EventBus.Subscribe<AntSelected>(OnAntSelected);
            EventBus.Subscribe<AntDied>(OnAntDied);
        }

        private void OnDisable()
        {
            if (Instance != this) return;
            AntPanelAction.Unregister(DevourLabel);
            EventBus.Unsubscribe<AntSelected>(OnAntSelected);
            EventBus.Unsubscribe<AntDied>(OnAntDied);
            eater = pendingEater = pendingVictim = null;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        
        private void LateUpdate()
        {
            if (pendingVictim == null) return;

            var e = pendingEater;
            var v = pendingVictim;
            pendingEater = pendingVictim = null;

            if (TryDevour(e, v) && e != null && e.IsAlive && AntSelection.Instance != null)
                AntSelection.Instance.Select(e);   
        }

       

        private bool CanStartDevour(Ant ant) => ant != null && ant.IsAlive && FeastAvailable;

        private bool FeastAvailable => !requireFeastRoom || Room.FindBuilt(RoomType.Feast) != null;

        private void StartDevour(Ant ant)
        {
            if (eater == ant) { CancelChoosing("cancelado"); return; }   
            eater = ant;
            if (logResults) Debug.Log($"[Festín] {ant.Name}: elige una víctima (toca otra hormiga; tocar en vacío cancela).");
        }

        private void OnAntSelected(AntSelected e)
        {
            if (eater == null) return;

            
            if (e.Ant == null) { CancelChoosing("cancelado"); return; }
            if (e.Ant == eater) return;

            var chosenEater = eater;
            eater = null;
            if (!CanDevour(chosenEater, e.Ant))
            {
                if (logResults) Debug.Log($"[Festín] {e.Ant.Name} no es una víctima válida.");
                return;
            }
            pendingEater = chosenEater;
            pendingVictim = e.Ant;
        }

        private void OnAntDied(AntDied e)
        {
            if (e.Ant == eater) CancelChoosing("la devoradora murió");
        }

        private void CancelChoosing(string reason)
        {
            if (eater == null) return;
            if (logResults) Debug.Log($"[Festín] {reason}.");
            eater = null;
        }

        

        public bool CanDevour(Ant eater, Ant victim)
        {
            return eater != null && victim != null
                && eater != victim
                && eater.IsAlive && victim.IsAlive
                && !victim.IsQueen          
                && FeastAvailable;
        }

        
        public bool TryDevour(Ant eater, Ant victim)
        {
            if (!CanDevour(eater, victim)) return false;

            var outcome = FeastOutcome.Nothing;
            GeneData gained = null;
            int inherited = 0;

            if (eater.IsQueen)
            {
                
                var queen = QueenGenome.Instance;
                if (queen == null)
                {
                    Debug.LogWarning("[Festín] La reina devora pero no hay QueenGenome en la escena.", this);
                    return false;
                }
                inherited = queen.InheritFrom(victim);
                if (inherited > 0) outcome = FeastOutcome.Inherited;
            }
            else
            {
                
                bool hasGenes = victim.Genes.Count > 0;
                var roll = hasGenes
                    ? RollDice(ownWithGenes_Gene, 1f - ownWithGenes_Gene)
                    : RollDice(0f, ownWithoutGenes_Flaw);
                outcome = Mutate(eater, roll, victim.Genes, out gained);
            }

            GiveBiomass(ownBiomass);
            victim.Kill(DeathCause.Eaten);

            Report(new FeastResult(eater, victim, outcome, gained, ownBiomass), inherited);
            return true;
        }

        
        public bool DevourPrey(Ant eater, GeneData preyGene)
        {
            if (eater == null || !eater.IsAlive || eater.IsQueen || !FeastAvailable) return false;

            var source = preyGene != null ? new List<GeneData> { preyGene } : new List<GeneData>();
            var outcome = Mutate(eater, RollDice(prey_Gene, prey_Flaw), source, out var gained);

            GiveBiomass(preyBiomass);
            Report(new FeastResult(eater, null, outcome, gained, preyBiomass), 0);
            return true;
        }

       

        private static Roll RollDice(float geneChance, float flawChance)
        {
            float r = Random.value;
            if (r < geneChance) return Roll.Gene;
            if (r < geneChance + flawChance) return Roll.Flaw;
            return Roll.Nothing;
        }

        
        private FeastOutcome Mutate(Ant eater, Roll roll, IReadOnlyList<GeneData> geneSource, out GeneData gained)
        {
            gained = null;
            if (roll == Roll.Nothing) return FeastOutcome.Nothing;

            var genome = eater.GetComponent<AntGenome>();
            if (genome == null)
            {
                Debug.LogWarning($"[Festín] {eater.Name} no tiene AntGenome: no puede mutar.", eater);
                return FeastOutcome.Nothing;
            }

            gained = PickNew(roll == Roll.Gene ? geneSource : flawPool, genome);
            if (gained == null || !genome.AddGene(gained))
            {
                gained = null;   
                return FeastOutcome.Nothing;
            }

            EventBus.Publish(new AntMutated(eater, gained));
            return roll == Roll.Gene ? FeastOutcome.Gene : FeastOutcome.Flaw;
        }

        
        private GeneData PickNew(IReadOnlyList<GeneData> source, AntGenome genome)
        {
            candidates.Clear();
            for (int i = 0; i < source.Count; i++)
                if (source[i] != null && !genome.HasGene(source[i])) candidates.Add(source[i]);
            return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
        }

        private static void GiveBiomass(int amount)
        {
            if (ResourceManager.Instance != null) ResourceManager.Instance.Add(ResourceType.Biomass, amount);
            else Debug.LogWarning("[Festín] No hay ResourceManager: no se puede dar biomasa.");
        }

        private void Report(FeastResult r, int inherited)
        {
            if (logResults)
            {
                string who = r.Victim != null ? r.Victim.Name : "una presa";
                string what;
                switch (r.Outcome)
                {
                    case FeastOutcome.Gene: what = $"gana el gen {r.Gene.displayName}"; break;
                    case FeastOutcome.Flaw: what = $"gana la tara {r.Gene.displayName}"; break;
                    case FeastOutcome.Inherited: what = $"la reina hereda {inherited} gen(es) para la casta"; break;
                    default: what = "no pasa nada"; break;
                }
                Debug.Log($"[Festín] {r.Eater.Name} devora a {who}: {what} (+{r.Biomass} biomasa)");
            }
            Devoured?.Invoke(r);
        }
    }
}