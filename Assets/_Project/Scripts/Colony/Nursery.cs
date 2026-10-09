using System;
using System.Collections.Generic;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using Myrmutation.Economy;
using Myrmutation.Genetics;
using UnityEngine;

namespace Myrmutation.Colony
{
    public enum BroodStage { Egg, Larva, Pupa }

    /// <summary>Una cría en la Guardería (huevo, larva o pupa).</summary>
    public class Brood
    {
        public int Id;
        public BroodStage Stage;
        public float Timer;          // segundos acumulados en la fase actual
        public float Duration;       // segundos que dura la fase actual (0 = esperando comida)
        public CasteData Caste;      // se fija al alimentar la larva
        public float WaitingFood;    // segundos que la larva lleva sin alimentar

        public bool IsFed => Caste != null;
        public bool NeedsFood => Stage == BroodStage.Larva && !IsFed;
        public float Progress01 => Duration > 0f ? Mathf.Clamp01(Timer / Duration) : 0f;
    }

    /// <summary>
    /// D2 · Guardería: huevo → larva → pupa → adulta, con temporizadores.
    ///
    /// - La reina (Queen) llama a AddEgg().
    /// - La larva espera a que se la alimente: Feed(larva, casta) gasta foodCostToRaise y fija la casta.
    /// - Al terminar la pupa nace una hormiga del prefab con esa casta. AntGenome (C2) le copia
    ///   en su Start los genes que la reina tenga para esa casta, así que nace con el genoma de su casta.
    /// - Aplica el visual de la casta: escala (bodyScale/headScale) y tinte.
    /// - Las obreras asignadas a la Guardería aceleran la cría (workRate × afinidad).
    ///
    /// Va en un GameObject de la escena (uno solo). No modifica nada de Core.
    /// </summary>
    public class Nursery : MonoBehaviour
    {
        public static Nursery Instance { get; private set; }

        [Header("Prefab y castas")]
        [SerializeField] private Ant antPrefab;
        [Tooltip("Castas que se pueden elegir al alimentar una larva (Menor, Media, Mayor)")]
        [SerializeField] private List<CasteData> castes = new List<CasteData>();

        [Header("Capacidad")]
        [Tooltip("Huevos + larvas + pupas a la vez")]
        [SerializeField] private int capacity = 6;

        [Header("Tiempos (segundos, a velocidad x1 y sin obreras)")]
        [SerializeField] private float eggSeconds = 10f;
        [SerializeField] private float larvaSeconds = 10f;
        [SerializeField] private float pupaSeconds = 10f;

        [Header("Obreras de la Guardería")]
        [Tooltip("Velocidad = 1 + este valor × suma de eficiencias de las obreras asignadas a la Guardería")]
        [SerializeField] private float speedBonusPerWorker = 0.25f;

        [Header("Alimentación automática (opcional)")]
        [Tooltip("Si una larva lleva este tiempo sin alimentar, se alimenta sola con la casta por defecto. 0 = nunca")]
        [SerializeField] private float autoFeedAfterSeconds = 0f;
        [SerializeField] private CasteData autoFeedCaste;

        [Header("Nacimiento")]
        [Tooltip("Dónde nacen. Vacío: en la Guardería construida, o en este objeto si no hay")]
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float spawnSpread = 0.5f;
        [Tooltip("Si el prefab no lleva AntGenome (C2), se le añade al nacer para que herede los genes de su casta")]
        [SerializeField] private bool addGenomeIfMissing = true;
        [SerializeField] private List<string> names = new List<string>
        {
            "Mordisco", "Migaja", "Tragona", "Bocado", "Hebra", "Pizca", "Grano", "Zampa",
            "Chispa", "Brizna", "Ramita", "Moteada", "Pelusa", "Rebaño", "Lenteja", "Almendra"
        };

        [Header("Debug")]
        [SerializeField] private bool logEvents = false;

        private readonly List<Brood> brood = new List<Brood>();
        private readonly List<Ant> workerBuffer = new List<Ant>();
        private int nextId = 1;
        private int nameIndex;

        public event Action<Brood> BroodAdded;
        public event Action<Brood> BroodStageChanged;
        public event Action<Ant> AntBorn;

        public IReadOnlyList<Brood> AllBrood => brood;
        public IReadOnlyList<CasteData> Castes => castes;
        public int Capacity => capacity;
        public bool HasSpace => brood.Count < capacity;
        public float SpeedMultiplier { get; private set; } = 1f;

        // ---------- Ciclo de vida ----------
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || brood.Count == 0) return;

            SpeedMultiplier = 1f + speedBonusPerWorker * WorkerPower();
            float step = dt * SpeedMultiplier;

            for (int i = brood.Count - 1; i >= 0; i--)
            {
                var b = brood[i];

                if (b.NeedsFood)
                {
                    b.WaitingFood += dt;
                    if (autoFeedAfterSeconds > 0f && b.WaitingFood >= autoFeedAfterSeconds && autoFeedCaste != null)
                        Feed(b, autoFeedCaste);
                    continue;
                }

                b.Timer += step;
                if (b.Timer < b.Duration) continue;

                switch (b.Stage)
                {
                    case BroodStage.Egg:   SetStage(b, BroodStage.Larva, 0f); break; // espera comida
                    case BroodStage.Larva: SetStage(b, BroodStage.Pupa, pupaSeconds); break;
                    case BroodStage.Pupa:  brood.RemoveAt(i); Hatch(b); break;
                }
            }
        }

        // ---------- API ----------
        /// <summary>La reina pone un huevo. Devuelve null si la Guardería está llena.</summary>
        public Brood AddEgg()
        {
            if (!HasSpace) return null;
            var b = new Brood { Id = nextId++, Stage = BroodStage.Egg, Duration = eggSeconds };
            brood.Add(b);
            if (logEvents) Debug.Log($"[Nursery] Huevo #{b.Id}", this);
            BroodAdded?.Invoke(b);
            return b;
        }

        /// <summary>
        /// Alimenta una larva y fija su casta. Gasta foodCostToRaise de comida.
        /// Devuelve false si no es una larva sin alimentar o si no hay comida suficiente.
        /// </summary>
        public bool Feed(Brood larva, CasteData caste)
        {
            if (larva == null || caste == null || !larva.NeedsFood) return false;
            var rm = ResourceManager.Instance;
            if (rm == null || !rm.TrySpend(ResourceType.Food, caste.foodCostToRaise)) return false;

            larva.Caste = caste;
            larva.Timer = 0f;
            larva.Duration = larvaSeconds;
            if (logEvents) Debug.Log($"[Nursery] Larva #{larva.Id} alimentada como {caste.caste} (-{caste.foodCostToRaise} comida)", this);
            BroodStageChanged?.Invoke(larva);
            return true;
        }

        public bool CanAfford(CasteData caste) =>
            caste != null && ResourceManager.Instance != null &&
            ResourceManager.Instance.Has(ResourceType.Food, caste.foodCostToRaise);

        public CasteData GetCaste(CasteType type)
        {
            foreach (var c in castes) if (c != null && c.caste == type) return c;
            return null;
        }

        /// <summary>Aplica el visual de la casta: escala del cuerpo, de la cabeza y tinte.</summary>
        public static void ApplyCasteVisual(Ant ant, CasteData caste)
        {
            if (ant == null || caste == null) return;

            var visual = FindChild(ant.transform, "Visual");
            var body = visual != null ? visual : ant.transform;
            body.localScale = Vector3.Scale(body.localScale, Vector3.one * caste.bodyScale);

            var head = FindChild(ant.transform, "Head");
            if (head != null) head.localScale = Vector3.Scale(head.localScale, Vector3.one * caste.headScale);

            foreach (var sr in ant.GetComponentsInChildren<SpriteRenderer>(true))
                sr.color *= caste.tint;
        }

        // ---------- Interno ----------
        private void SetStage(Brood b, BroodStage stage, float duration)
        {
            b.Stage = stage;
            b.Timer = 0f;
            b.Duration = duration;
            b.WaitingFood = 0f;
            if (logEvents) Debug.Log($"[Nursery] #{b.Id} → {stage}", this);
            BroodStageChanged?.Invoke(b);
        }

        private void Hatch(Brood b)
        {
            if (antPrefab == null)
            {
                Debug.LogWarning("[Nursery] Falta el prefab de hormiga: la cría se pierde.", this);
                return;
            }

            Vector3 pos = SpawnPosition() + (Vector3)(UnityEngine.Random.insideUnitCircle * spawnSpread);
            pos.z = 0f;
            var ant = Instantiate(antPrefab, pos, Quaternion.identity);

            // Antes de su Start: AntGenome.Start copiará los genes de la reina para esta casta.
            ant.Caste = b.Caste;
            ant.Stats = b.Caste.baseStats;
            ant.Name = NextName();
            if (addGenomeIfMissing && ant.GetComponent<AntGenome>() == null)
                ant.gameObject.AddComponent<AntGenome>(); // su Start copia los genes de la reina para esta casta
            ant.gameObject.name = $"Ant_{ant.Name}";
            ApplyCasteVisual(ant, b.Caste);

            if (logEvents) Debug.Log($"[Nursery] Nace {ant.Name} ({b.Caste.caste})", ant);
            AntBorn?.Invoke(ant);
        }

        private Vector3 SpawnPosition()
        {
            if (spawnPoint != null) return spawnPoint.position;
            var room = Room.FindBuilt(RoomType.Nursery);
            return room != null ? room.WorkPosition : transform.position;
        }

        private string NextName()
        {
            if (names == null || names.Count == 0) return "Glotona";
            string n = names[nameIndex % names.Count];
            int round = nameIndex / names.Count;
            nameIndex++;
            return round == 0 ? n : $"{n} {round + 1}";
        }

        private float WorkerPower()
        {
            var room = Room.FindBuilt(RoomType.Nursery);
            if (room == null) return 0f;

            workerBuffer.Clear();
            int cap = room.Data != null ? Mathf.Max(1, room.Data.workerCapacity) : int.MaxValue;
            foreach (var a in Ant.All)
            {
                if (workerBuffer.Count >= cap) break;
                if (a != null && a.IsAlive && !a.IsQueen && a.AssignedRoom == room) workerBuffer.Add(a);
            }

            float power = 0f;
            foreach (var a in workerBuffer) power += ColonyProduction.GetEfficiency(a, RoomType.Nursery);
            return power;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name == name) return t;
            return null;
        }
    }
}
