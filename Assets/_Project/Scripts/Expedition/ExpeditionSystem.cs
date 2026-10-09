using System;
using System.Collections.Generic;
using Myrmutation.Ants;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using Myrmutation.Economy;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Myrmutation.Expedition
{
    // ===================== EVENTOS (EventBus) =====================
    // Definidos aquí para no tocar Core/GameEvents.cs. Se usan igual:
    //   EventBus.Subscribe<ExpeditionReturned>(OnReturned);

    /// <summary>Ha salido una expedición.</summary>
    public struct ExpeditionSent
    {
        public IReadOnlyList<Ant> Ants; public float Duration;
        public ExpeditionSent(IReadOnlyList<Ant> ants, float duration) { Ants = ants; Duration = duration; }
    }

    /// <summary>Ha vuelto la expedición. Leaves &gt; 0 si trajo hojas; Prey != null si trajo una presa.</summary>
    public struct ExpeditionReturned
    {
        public IReadOnlyList<Ant> Ants; public int Leaves; public Prey Prey;
        public ExpeditionReturned(IReadOnlyList<Ant> ants, int leaves, Prey prey) { Ants = ants; Leaves = leaves; Prey = prey; }
    }

    /// <summary>Cambió el número de presas guardadas (las consume C en el Festín).</summary>
    public struct PreyStockChanged
    {
        public int Count;
        public PreyStockChanged(int count) { Count = count; }
    }

    // ===================== PRESA =====================

    /// <summary>
    /// Presa que se trae de una expedición. Es la fuente de genes: C (FeastSystem) la consume con
    /// ExpeditionSystem.Instance.TryTakePrey(...) y aplica sus reglas (gen 70 % · tara 20 % · nada 10 %).
    /// </summary>
    [Serializable]
    public class Prey
    {
        public string displayName = "Presa";
        [Tooltip("Gen que puede dar al devorarla")]
        public GeneData gene;
        [Tooltip("Opcional: sprite de la presa (F8). Si no hay, la UI usa el icono del gen.")]
        public Sprite sprite;
        [Tooltip("Peso relativo al elegir qué presa aparece")]
        [Min(0f)] public float weight = 1f;

        public Sprite Icon => sprite != null ? sprite : gene != null ? gene.icon : null;
    }

    // ===================== SISTEMA =====================

    /// <summary>
    /// D4 · Expedición mínima (alfa).
    ///  - Se envían 1–3 hormigas (no la reina). Desaparecen del nido durante 'duration' segundos de juego.
    ///  - Al volver traen hojas O una presa, al azar. Sin eventos de texto (eso es de la beta).
    ///  - No se pueden enviar en invierno (SeasonRules.ExpeditionsAllowed, D3).
    ///  - Una expedición a la vez.
    ///
    /// Mientras están fuera las hormigas están desactivadas (SetActive false): salen de Ant.All, así que
    /// no cuentan para producción ni mantenimiento, no se pueden seleccionar y no pasan hambre.
    /// Su cerebro se suspende antes de salir y se reanuda al volver (docs/IA_Hormigas.md §4).
    ///
    /// Cómo elegir quién va: las "apuntadas" desde el panel de hormiga (botón "Apuntar a expedición")
    /// van primero; el resto del grupo se completa con obreras sin sala y, si faltan, con cualquiera.
    ///
    /// Va en la escena Game (lo coloca A). Usa Time.deltaTime: se pausa y acelera con el GameManager.
    /// </summary>
    public class ExpeditionSystem : MonoBehaviour
    {
        public const int MinGroup = 1;
        public const int MaxGroup = 3;

        private const string ActionJoin = "Apuntar a expedición";
        private const string ActionLeave = "Quitar de expedición";

        public static ExpeditionSystem Instance { get; private set; }

        [Header("Expedición")]
        [Tooltip("Segundos de juego que pasan fuera (20 s = 1 día con el GameManager por defecto)")]
        [SerializeField] private float duration = 30f;
        [Tooltip("Opcional: punto por el que reaparecen (la entrada del nido). Vacío = donde estaban al salir.")]
        [SerializeField] private Transform returnPoint;

        [Header("Botín: probabilidad de presa")]
        [Tooltip("Probabilidad de volver con presa con 1 hormiga")]
        [Range(0f, 1f)] [SerializeField] private float preyChanceBase = 0.3f;
        [Tooltip("Probabilidad extra por cada hormiga más en el grupo")]
        [Range(0f, 1f)] [SerializeField] private float preyChancePerExtraAnt = 0.1f;

        [Header("Botín: hojas")]
        [Tooltip("Hojas por hormiga (mínimo y máximo). Se multiplica por su carryCapacity.")]
        [SerializeField] private Vector2Int leavesPerAnt = new Vector2Int(3, 5);

        [Header("Presas posibles")]
        [SerializeField] private List<Prey> preyPool = new List<Prey>();

        [Header("Panel de hormiga")]
        [Tooltip("Añade los botones Apuntar/Quitar de expedición al panel de hormiga (B4)")]
        [SerializeField] private bool registerPanelActions = true;

        [Header("Debug")]
        [SerializeField] private bool logExpeditions = true;

        // ---------- Estado ----------
        private readonly List<Ant> away = new List<Ant>();
        private readonly Dictionary<Ant, Vector3> departPositions = new Dictionary<Ant, Vector3>();
        private readonly List<Ant> volunteers = new List<Ant>();
        private readonly List<Prey> storedPrey = new List<Prey>();
        private float timer;

        // ---------- API de consulta (UI) ----------

        /// <summary>Hay una expedición fuera.</summary>
        public bool IsAway => away.Count > 0;
        /// <summary>Segundos de juego que faltan para que vuelva (0 si no hay ninguna fuera).</summary>
        public float TimeLeft => IsAway ? Mathf.Max(0f, duration - timer) : 0f;
        /// <summary>Progreso de la expedición actual (0..1).</summary>
        public float Progress01 => IsAway ? Mathf.Clamp01(timer / Mathf.Max(0.01f, duration)) : 0f;
        public float Duration => duration;
        public IReadOnlyList<Ant> AntsAway => away;
        /// <summary>Hormigas apuntadas desde el panel (irán primero).</summary>
        public IReadOnlyList<Ant> Volunteers => volunteers;
        /// <summary>Probabilidad de traer presa con un grupo de n hormigas.</summary>
        public float PreyChance(int groupSize) =>
            Mathf.Clamp01(preyChanceBase + preyChancePerExtraAnt * Mathf.Max(0, groupSize - 1));

        // ---------- API de presas (para C · FeastSystem) ----------

        public IReadOnlyList<Prey> StoredPrey => storedPrey;
        public int PreyCount => storedPrey.Count;
        public bool HasPrey => storedPrey.Count > 0;

        /// <summary>Saca la presa más antigua (o la de 'index'). False si no hay.</summary>
        public bool TryTakePrey(out Prey prey, int index = 0)
        {
            prey = null;
            if (index < 0 || index >= storedPrey.Count) return false;
            prey = storedPrey[index];
            storedPrey.RemoveAt(index);
            EventBus.Publish(new PreyStockChanged(storedPrey.Count));
            return true;
        }

        /// <summary>Añade una presa al almacén (debug o futuros sistemas).</summary>
        public void AddPrey(Prey prey)
        {
            if (prey == null) return;
            storedPrey.Add(prey);
            EventBus.Publish(new PreyStockChanged(storedPrey.Count));
        }

        // ================= CICLO =================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void OnEnable()
        {
            EventBus.Subscribe<AntDied>(OnAntDied);
            if (registerPanelActions)
            {
                AntPanelAction.Register(ActionJoin, a => CanVolunteer(a) && !volunteers.Contains(a), Volunteer);
                AntPanelAction.Register(ActionLeave, a => volunteers.Contains(a), Unvolunteer);
            }
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<AntDied>(OnAntDied);
            if (registerPanelActions)
            {
                AntPanelAction.Unregister(ActionJoin);
                AntPanelAction.Unregister(ActionLeave);
            }
        }

        private void Update()
        {
            if (!IsAway) return;
            timer += Time.deltaTime;
            if (timer >= duration) Return();
        }

        // ================= ENVIAR =================

        /// <summary>Se puede enviar ahora una expedición de 'count' hormigas. 'reason' explica por qué no.</summary>
        public bool CanSend(int count, out string reason)
        {
            reason = null;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing
                && GameManager.Instance.State != GameState.Paused) { reason = "La partida ha terminado"; return false; }
            if (!SeasonRules.ExpeditionsAllowed) { reason = "No hay expediciones en invierno"; return false; }
            if (IsAway) { reason = "Ya hay una expedición fuera"; return false; }
            if (count < MinGroup || count > MaxGroup) { reason = $"El grupo debe ser de {MinGroup} a {MaxGroup} hormigas"; return false; }
            if (CountAvailable() < count) { reason = "No hay suficientes obreras"; return false; }
            return true;
        }

        public bool CanSend(int count) => CanSend(count, out _);

        /// <summary>Envía 'count' hormigas elegidas automáticamente (apuntadas primero). Lo llama el botón.</summary>
        public bool Send(int count)
        {
            if (!CanSend(count, out var reason))
            {
                if (logExpeditions) Debug.Log($"[Expedition] No se puede enviar: {reason}", this);
                return false;
            }
            return SendGroup(PickGroup(count));
        }

        /// <summary>Envía un grupo concreto de 1–3 hormigas.</summary>
        public bool Send(IList<Ant> group)
        {
            if (group == null) return false;
            if (!CanSend(group.Count, out var reason))
            {
                if (logExpeditions) Debug.Log($"[Expedition] No se puede enviar: {reason}", this);
                return false;
            }
            foreach (var a in group) if (!CanVolunteer(a)) return false;
            return SendGroup(new List<Ant>(group));
        }

        private bool SendGroup(List<Ant> group)
        {
            if (group.Count == 0) return false;

            // Si la hormiga seleccionada se va, se cierra el panel.
            if (AntSelection.Instance != null && group.Contains(AntSelection.Current))
                AntSelection.Instance.Deselect();

            away.Clear();
            departPositions.Clear();
            foreach (var ant in group)
            {
                volunteers.Remove(ant);
                if (ant.TryGetComponent<IAntBrain>(out var brain)) brain.Suspend();
                if (ant.TryGetComponent<AntActuator>(out var actuator)) actuator.Stop();
                departPositions[ant] = ant.transform.position;
                away.Add(ant);
                ant.gameObject.SetActive(false);   // desaparece: sale de Ant.All
            }

            timer = 0f;
            if (logExpeditions) Debug.Log($"[Expedition] Sale una expedición de {away.Count} ({Names(away)}) durante {duration:0} s", this);
            EventBus.Publish(new ExpeditionSent(away.ToArray(), duration));
            return true;
        }

        // ================= VOLVER =================

        private void Return()
        {
            var group = away.ToArray();
            away.Clear();
            timer = 0f;

            foreach (var ant in group)
            {
                if (ant == null) continue;
                Vector3 pos = returnPoint != null ? returnPoint.position
                    : departPositions.TryGetValue(ant, out var p) ? p : ant.transform.position;
                pos.z = ant.transform.position.z;
                ant.transform.position = pos;
                ant.gameObject.SetActive(true);    // vuelve a Ant.All
                if (ant.TryGetComponent<IAntBrain>(out var brain)) brain.Resume();
            }
            departPositions.Clear();

            // Botín: presa O hojas
            int leaves = 0;
            Prey prey = null;
            if (preyPool.Count > 0 && Random.value < PreyChance(group.Length)) prey = PickPrey();

            if (prey != null)
            {
                AddPrey(prey);
            }
            else
            {
                leaves = RollLeaves(group);
                ResourceManager.Instance?.Add(ResourceType.Leaves, leaves);
            }

            if (logExpeditions)
                Debug.Log($"[Expedition] Vuelve la expedición ({Names(group)}) con " +
                          (prey != null ? $"una presa: {prey.displayName}" : $"{leaves} hojas"), this);
            EventBus.Publish(new ExpeditionReturned(group, leaves, prey));
        }

        private int RollLeaves(Ant[] group)
        {
            int min = Mathf.Max(0, Mathf.Min(leavesPerAnt.x, leavesPerAnt.y));
            int max = Mathf.Max(leavesPerAnt.x, leavesPerAnt.y);
            int total = 0;
            foreach (var ant in group)
            {
                float carry = ant != null && ant.Stats.carryCapacity > 0f ? ant.Stats.carryCapacity : 1f;
                total += Mathf.RoundToInt(Random.Range(min, max + 1) * carry);
            }
            return Mathf.Max(1, total);
        }

        private Prey PickPrey()
        {
            float totalWeight = 0f;
            foreach (var p in preyPool) if (p != null) totalWeight += Mathf.Max(0f, p.weight);
            if (totalWeight <= 0f) return null;

            float roll = Random.value * totalWeight;
            foreach (var p in preyPool)
            {
                if (p == null) continue;
                roll -= Mathf.Max(0f, p.weight);
                if (roll <= 0f) return p;
            }
            return preyPool[preyPool.Count - 1];
        }

        // ================= ELEGIR GRUPO =================

        /// <summary>Puede ir de expedición: viva, en el nido y no es la reina.</summary>
        public bool CanVolunteer(Ant ant) =>
            ant != null && ant.IsAlive && !ant.IsQueen && ant.gameObject.activeInHierarchy && !away.Contains(ant);

        /// <summary>Obreras que podrían salir ahora.</summary>
        public int CountAvailable()
        {
            int n = 0;
            foreach (var a in Ant.All) if (CanVolunteer(a)) n++;
            return n;
        }

        public void Volunteer(Ant ant)
        {
            if (!CanVolunteer(ant) || volunteers.Contains(ant)) return;
            if (volunteers.Count >= MaxGroup) volunteers.RemoveAt(0);   // la más antigua deja su sitio
            volunteers.Add(ant);
        }

        public void Unvolunteer(Ant ant) => volunteers.Remove(ant);

        private List<Ant> PickGroup(int count)
        {
            var group = new List<Ant>(count);

            // 1) Apuntadas desde el panel
            volunteers.RemoveAll(a => !CanVolunteer(a));
            foreach (var a in volunteers) { if (group.Count >= count) break; group.Add(a); }

            // 2) Obreras sin sala, en orden aleatorio  3) cualquiera
            var unassigned = new List<Ant>();
            var assigned = new List<Ant>();
            foreach (var a in Ant.All)
            {
                if (!CanVolunteer(a) || group.Contains(a)) continue;
                (a.AssignedRoom == null ? unassigned : assigned).Add(a);
            }
            Shuffle(unassigned);
            Shuffle(assigned);
            foreach (var a in unassigned) { if (group.Count >= count) break; group.Add(a); }
            foreach (var a in assigned) { if (group.Count >= count) break; group.Add(a); }
            return group;
        }

        // ================= EVENTOS =================

        private void OnAntDied(AntDied e) => volunteers.Remove(e.Ant);

        // ================= UTILIDADES =================

        private static void Shuffle(List<Ant> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static string Names(IEnumerable<Ant> ants)
        {
            var names = new List<string>();
            foreach (var a in ants) if (a != null) names.Add(a.Name);
            return string.Join(", ", names);
        }

#if UNITY_EDITOR
        // Al añadir el componente (o con clic derecho → "Rellenar presas de la alfa") busca los genes
        // de la alfa en Data/Genes y crea las dos presas que los dan.
        private void Reset() => FillDefaultPrey();

        [ContextMenu("Rellenar presas de la alfa")]
        private void FillDefaultPrey()
        {
            preyPool = new List<Prey>
            {
                new Prey { displayName = "Hormiga del desierto", gene = FindGene("Gene_PatasCorredora"), weight = 1f },
                new Prey { displayName = "Hormiga cortadora", gene = FindGene("Gene_MandibulaCortadora"), weight = 1f },
            };
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private static GeneData FindGene(string assetName)
        {
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets($"{assetName} t:GeneData"))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var gene = UnityEditor.AssetDatabase.LoadAssetAtPath<GeneData>(path);
                if (gene != null && gene.name == assetName) return gene;
            }
            Debug.LogWarning($"[Expedition] No encuentro el gen {assetName}: asígnalo a mano en Prey Pool.");
            return null;
        }
#endif
    }
}
