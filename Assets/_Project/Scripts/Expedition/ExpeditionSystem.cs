using System;
using System.Collections.Generic;
using Myrmutation.Ants;
using Myrmutation.Building;
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

    /// <summary>
    /// Ha vuelto la expedición. Leaves = hojas entregadas. Preys = presas entregadas (D10: puede haber varias);
    /// Prey = la primera (o null), para lo que solo mira una.
    /// </summary>
    public struct ExpeditionReturned
    {
        private static readonly Prey[] NoPrey = new Prey[0];

        public IReadOnlyList<Ant> Ants; public int Leaves; public Prey Prey; public IReadOnlyList<Prey> Preys;

        public ExpeditionReturned(IReadOnlyList<Ant> ants, int leaves, Prey prey)
        {
            Ants = ants; Leaves = leaves; Prey = prey;
            Preys = prey != null ? new[] { prey } : NoPrey;
        }

        public ExpeditionReturned(IReadOnlyList<Ant> ants, int leaves, IReadOnlyList<Prey> preys)
        {
            Ants = ants; Leaves = leaves;
            Preys = preys ?? NoPrey;
            Prey = Preys.Count > 0 ? Preys[0] : null;
        }
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

    // ===================== ESTIMACIÓN (D10) =====================

    /// <summary>Lo que se espera que traiga un grupo antes de enviarlo (menú de expedición).</summary>
    public struct ExpeditionEstimate
    {
        /// <summary>Hormigas del grupo.</summary>
        public int Ants;
        /// <summary>Suma de carga (carryCapacity) del grupo.</summary>
        public float Carry;
        /// <summary>Suma de fuerza (strength) del grupo.</summary>
        public float Strength;
        public int LeavesMin;
        public int LeavesMax;
        /// <summary>Probabilidad de traer al menos una presa (0..1).</summary>
        public float PreyChance;
        /// <summary>Probabilidad de la 2.ª presa (0 si el grupo no tiene fuerza para ella).</summary>
        public float SecondPreyChance;
        /// <summary>Presas que como mucho puede traer.</summary>
        public int MaxPrey;
        /// <summary>Presas que trae de media.</summary>
        public float ExpectedPrey;

        /// <summary>"~18–24 hojas · 45 % presa" (+ " · 2.ª presa 20 %" si puede traer más de una).</summary>
        public override string ToString()
        {
            if (Ants <= 0) return "Sin hormigas";
            string leaves = LeavesMin == LeavesMax ? $"~{LeavesMin} hojas" : $"~{LeavesMin}–{LeavesMax} hojas";
            string prey = $"{Mathf.RoundToInt(PreyChance * 100f)} % presa";
            string second = MaxPrey > 1 && SecondPreyChance > 0f ? $" · 2.ª presa {Mathf.RoundToInt(SecondPreyChance * 100f)} %" : "";
            return $"{leaves} · {prey}{second}";
        }
    }

    // ===================== SISTEMA =====================

    /// <summary>Fase de la expedición actual (D7).</summary>
    public enum ExpeditionPhase
    {
        /// <summary>No hay expedición.</summary>
        None,
        /// <summary>Van andando hasta un extremo de la superficie.</summary>
        Leaving,
        /// <summary>Fuera del mapa: corre la duración.</summary>
        Away,
        /// <summary>Han aparecido por el extremo y van cargadas hacia la entrada.</summary>
        Returning
    }

    /// <summary>
    /// D4 · Expedición mínima (alfa) + D7 · Salen y vuelven por la entrada + D10 · Tamaño libre con botín que escala.
    ///  - Con NestEntrance en la escena (D7): al enviar, caminan hasta la entrada, suben y se van por un extremo de la
    ///    superficie, donde desaparecen. La duración cuenta desde que se ha ido la última. Al volver aparecen por el mismo
    ///    extremo y caminan hasta la entrada cargando la hoja o la presa; cada una entrega su parte del botín al llegar.
    ///  - Sin NestEntrance (escenas de prueba antiguas): desaparecen y reaparecen al momento, como en la alfa.
    ///  - D10: se envían de 1 a todas las disponibles (nunca la reina). Están fuera durante 'duration' segundos de juego.
    ///  - D10: el botín depende de la carga (carryCapacity) y la fuerza (strength) de cada hormiga, que vienen de su casta
    ///    (CasteData.baseStats, editables en el asset) y de sus genes:
    ///      · Hojas = leavesBase × √(suma de carga) × (1 ± leavesVariation). Cada hormiga extra aporta menos.
    ///      · Presa = 1 − (1 − preyChancePerStrength)^(suma de fuerza), con tope preyChanceCap.
    ///      · Más presas: la k-ésima (k = 0, 1, 2…) se tira con la fuerza que sobra, suma − k × strengthPerExtraPrey,
    ///        con su propia probabilidad, hasta maxPreyPerExpedition. Todas van a la lista de presas del Festín.
    ///    Mandar a todas no es siempre lo mejor: el botín crece cada vez menos y las que están fuera no trabajan.
    ///  - No se pueden enviar en invierno (SeasonRules.ExpeditionsAllowed, D3).
    ///  - Una expedición a la vez.
    ///
    /// Mientras están fuera las hormigas están desactivadas (SetActive false): salen de Ant.All, así que
    /// no cuentan para producción ni mantenimiento, no se pueden seleccionar y no pasan hambre.
    /// Su cerebro se suspende antes de salir y se reanuda al volver (docs/IA_Hormigas.md §4).
    ///
    /// Cómo elegir quién va: las "apuntadas" desde el panel de hormiga (botón "Apuntar a expedición")
    /// van primero; el resto del grupo se completa con obreras sin sala y, si faltan, con cualquiera.
    /// El orden es fijo, así que la estimación del menú es la del grupo que sale de verdad.
    ///
    /// Va en la escena Game (lo coloca A). Usa Time.deltaTime: se pausa y acelera con el GameManager.
    /// </summary>
    public class ExpeditionSystem : MonoBehaviour
    {
        public const int MinGroup = 1;

        private const string ActionJoin = "Apuntar a expedición";
        private const string ActionLeave = "Quitar de expedición";

        public static ExpeditionSystem Instance { get; private set; }

        [Header("Expedición")]
        [Tooltip("Segundos de juego que pasan fuera (20 s = 1 día con el GameManager por defecto)")]
        [SerializeField] private float duration = 30f;
        [Tooltip("Opcional: punto por el que reaparecen (la entrada del nido). Vacío = donde estaban al salir.")]
        [SerializeField] private Transform returnPoint;

        [Header("Botín: hojas (D10) · base × √(suma de carga) ± variación")]
        [Tooltip("Hojas con una carga total de 1 (una Menor). Cada hormiga aporta su carryCapacity (asset de casta + genes)")]
        [Min(0f)] [SerializeField] private float leavesBase = 8f;
        [Tooltip("Variación aleatoria: 0,15 = ±15 %")]
        [Range(0f, 0.5f)] [SerializeField] private float leavesVariation = 0.15f;

        [Header("Botín: presas (D10) · 1 − (1 − p)^(suma de fuerza)")]
        [Tooltip("p: probabilidad de presa por cada punto de fuerza (strength del asset de casta + genes)")]
        [Range(0f, 1f)] [SerializeField] private float preyChancePerStrength = 0.2f;
        [Tooltip("Tope de la probabilidad de cada presa")]
        [Range(0f, 1f)] [SerializeField] private float preyChanceCap = 0.8f;
        [Tooltip("Fuerza que gasta cada presa: la siguiente se tira con la fuerza que sobra")]
        [Min(0.1f)] [SerializeField] private float strengthPerExtraPrey = 4f;
        [Tooltip("Presas como mucho por expedición")]
        [Min(1)] [SerializeField] private int maxPreyPerExpedition = 3;

        [Header("Presas posibles")]
        [SerializeField] private List<Prey> preyPool = new List<Prey>();

        [Header("Salida y vuelta por la entrada (D7)")]
        [Tooltip("Si hay NestEntrance en la escena, salen andando por un extremo de la superficie y vuelven por él. " +
                 "Desactivado (o sin NestEntrance): desaparecen y reaparecen al momento, como en la alfa")]
        [SerializeField] private bool useEntrance = true;
        [Tooltip("Segundos de juego máximos para llegar a la salida. Si alguna no llega (sin camino, atascada), se va igualmente")]
        [SerializeField] private float maxWalkSeconds = 60f;
        [Tooltip("Sprite de la hoja que traen (F8). Vacío: un rectángulo verde provisional")]
        [SerializeField] private Sprite leafSprite;
        [Tooltip("Tamaño de la carga, en casillas de la rejilla (lado más largo)")]
        [SerializeField] private float carrySizeCells = 0.8f;
        [Tooltip("Posición de la carga respecto a la hormiga, en casillas (x, y)")]
        [SerializeField] private Vector2 carryOffsetCells = new Vector2(0f, 0.55f);
        [SerializeField] private Color placeholderLeafColor = new Color(0.35f, 0.7f, 0.25f);
        [SerializeField] private Color placeholderPreyColor = new Color(0.55f, 0.35f, 0.2f);

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

        // D7
        private ExpeditionPhase phase = ExpeditionPhase.None;
        private NestEntrance.Side side;
        private float walkTimer;
        private readonly List<Ant> walkingHome = new List<Ant>();
        private readonly Dictionary<Ant, int> carriedLeaves = new Dictionary<Ant, int>();
        private readonly Dictionary<Ant, List<Prey>> carriedPrey = new Dictionary<Ant, List<Prey>>();
        private readonly Dictionary<Ant, GameObject> carryVisuals = new Dictionary<Ant, GameObject>();
        private readonly List<Prey> deliveredPrey = new List<Prey>();
        private Ant[] returningGroup;
        private int deliveredLeaves;
        private static Sprite placeholderSprite;

        // ---------- API de consulta (UI) ----------

        /// <summary>Hay una expedición en curso (saliendo, fuera o volviendo).</summary>
        public bool IsAway => phase != ExpeditionPhase.None;
        /// <summary>En qué punto está la expedición (D7).</summary>
        public ExpeditionPhase Phase => phase;
        /// <summary>
        /// Segundos de juego que faltan para que vuelvan a aparecer: toda la duración mientras salen, lo que quede mientras
        /// están fuera y 0 mientras vuelven andando a la entrada.
        /// </summary>
        public float TimeLeft =>
            phase == ExpeditionPhase.Leaving ? duration :
            phase == ExpeditionPhase.Away ? Mathf.Max(0f, duration - timer) : 0f;
        /// <summary>Progreso de la expedición actual (0..1): 0 mientras salen, sube mientras están fuera, 1 mientras vuelven.</summary>
        public float Progress01 =>
            phase == ExpeditionPhase.Away ? Mathf.Clamp01(timer / Mathf.Max(0.01f, duration)) :
            phase == ExpeditionPhase.Returning ? 1f : 0f;
        public float Duration => duration;
        public IReadOnlyList<Ant> AntsAway => away;
        /// <summary>Hormigas apuntadas desde el panel (irán primero).</summary>
        public IReadOnlyList<Ant> Volunteers => volunteers;
        /// <summary>D10: tamaño máximo del grupo ahora mismo (todas las disponibles, al menos 1).</summary>
        public int MaxGroupNow => Mathf.Max(MinGroup, CountAvailable());
        /// <summary>
        /// D10: igual que MaxGroupNow, pero estático para lo que antes usaba la constante (3), p. ej. AntListUI.
        /// Sin ExpeditionSystem en la escena, 1.
        /// </summary>
        public static int MaxGroup => Instance != null ? Instance.MaxGroupNow : MinGroup;

        /// <summary>Probabilidad de traer al menos una presa con el grupo de 'groupSize' que saldría ahora.</summary>
        public float PreyChance(int groupSize) => Estimate(groupSize).PreyChance;

        /// <summary>D10: lo que traería el grupo de 'count' que saldría ahora (el mismo que elige Send(count)).</summary>
        public ExpeditionEstimate Estimate(int count) => Estimate(PickGroup(Mathf.Max(0, count)));

        /// <summary>D10: lo que traería un grupo concreto.</summary>
        public ExpeditionEstimate Estimate(IList<Ant> group)
        {
            var e = new ExpeditionEstimate();
            if (group == null) return e;
            foreach (var a in group)
            {
                if (a == null) continue;
                e.Ants++;
                e.Carry += CarryOf(a);
                e.Strength += StrengthOf(a);
            }
            if (e.Ants == 0) return e;

            float leaves = LeavesAverage(e.Carry);
            e.LeavesMin = Mathf.Max(1, Mathf.RoundToInt(leaves * (1f - leavesVariation)));
            e.LeavesMax = Mathf.Max(1, Mathf.RoundToInt(leaves * (1f + leavesVariation)));

            if (preyPool.Count > 0)
            {
                for (int k = 0; k < maxPreyPerExpedition; k++)
                {
                    float chance = PreySlotChance(e.Strength, k);
                    if (chance <= 0f) break;
                    if (k == 0) e.PreyChance = chance;
                    if (k == 1) e.SecondPreyChance = chance;
                    e.MaxPrey++;
                    e.ExpectedPrey += chance;
                }
            }
            return e;
        }

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
            float dt = Time.deltaTime;
            switch (phase)
            {
                case ExpeditionPhase.Leaving: TickLeaving(dt); break;
                case ExpeditionPhase.Away:
                    timer += dt;
                    if (timer >= duration) Return();
                    break;
                case ExpeditionPhase.Returning: TickReturning(); break;
            }
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
            if (count < MinGroup) { reason = "Elige al menos 1 hormiga"; return false; }
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

        /// <summary>Envía un grupo concreto (de 1 a todas las disponibles).</summary>
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
                if (ant.TryGetComponent<IAntBrain>(out var brain)) brain.Suspend();   // suspendido hasta que entregue el botín
                if (ant.TryGetComponent<AntActuator>(out var actuator)) actuator.Stop();
                departPositions[ant] = ant.transform.position;
                away.Add(ant);
            }

            timer = 0f;
            walkTimer = 0f;
            var entrance = Entrance;
            if (entrance != null)
            {
                // D7: van andando hasta un extremo de la superficie y desaparecen allí (NestEntrance hace el fundido).
                side = Random.value < 0.5f ? NestEntrance.Side.Left : NestEntrance.Side.Right;
                foreach (var ant in away)
                    if (!entrance.LeaveVia(ant, side, null))
                    {
                        if (logExpeditions) Debug.LogWarning($"[Expedition] {ant.Name} no tiene camino hasta la salida: se va directamente", ant);
                        ant.gameObject.SetActive(false);
                    }
                phase = ExpeditionPhase.Leaving;
            }
            else
            {
                foreach (var ant in away) ant.gameObject.SetActive(false);   // desaparece: sale de Ant.All
                phase = ExpeditionPhase.Away;
            }

            if (logExpeditions)
                Debug.Log($"[Expedition] Sale una expedición de {away.Count} ({Names(away)}) durante {duration:0} s · {Estimate(away)}" +
                          (phase == ExpeditionPhase.Leaving ? $" · por la salida {(side == NestEntrance.Side.Left ? "izquierda" : "derecha")}" : ""), this);
            EventBus.Publish(new ExpeditionSent(away.ToArray(), duration));
            return true;
        }

        // ================= SALIR (D7) =================

        private NestEntrance Entrance => useEntrance ? NestEntrance.Instance : null;

        private void TickLeaving(float dt)
        {
            walkTimer += dt;
            away.RemoveAll(a => a == null || !a.IsAlive);   // murió por el camino
            if (away.Count == 0) { Cancel("no queda ninguna hormiga"); return; }

            bool allGone = true;
            foreach (var ant in away) if (ant.gameObject.activeSelf) { allGone = false; break; }

            if (!allGone && walkTimer >= maxWalkSeconds)
            {
                // Alguna no llega (atascada o sin camino): se va igualmente para no bloquear la expedición.
                foreach (var ant in away)
                {
                    if (!ant.gameObject.activeSelf) continue;
                    if (logExpeditions) Debug.LogWarning($"[Expedition] {ant.Name} no ha llegado a la salida en {maxWalkSeconds:0} s: se va igualmente", ant);
                    if (ant.TryGetComponent<AntActuator>(out var actuator)) actuator.Stop();
                    ant.gameObject.SetActive(false);
                }
                allGone = true;
            }

            if (!allGone) return;
            phase = ExpeditionPhase.Away;
            timer = 0f;   // la duración cuenta desde que se ha ido la última
            if (logExpeditions) Debug.Log($"[Expedition] La expedición ha salido del mapa: vuelve en {duration:0} s", this);
        }

        private void Cancel(string reason)
        {
            if (logExpeditions) Debug.Log($"[Expedition] Expedición cancelada: {reason}", this);
            away.Clear();
            departPositions.Clear();
            phase = ExpeditionPhase.None;
            timer = 0f;
        }

        // ================= VOLVER =================

        private void Return()
        {
            var group = new List<Ant>();
            foreach (var ant in away) if (ant != null && ant.IsAlive) group.Add(ant);
            returningGroup = group.ToArray();
            if (group.Count == 0) { Cancel("no queda ninguna hormiga"); return; }

            // Botín (D10): hojas Y presas. Se decide ahora, pero se suma cuando cada una pasa por la entrada.
            deliveredPrey.Clear();
            deliveredLeaves = 0;
            carriedLeaves.Clear();
            carriedPrey.Clear();
            RollLoot(group, out int totalLeaves, out var preys);
            AssignLoot(group, totalLeaves, preys);

            var entrance = Entrance;
            walkingHome.Clear();
            timer = 0f;
            phase = ExpeditionPhase.Returning;

            foreach (var ant in group)
            {
                if (entrance == null)
                {
                    // Sin entrada (alfa): reaparecen donde estaban (o en returnPoint) y entregan al momento.
                    Vector3 pos = returnPoint != null ? returnPoint.position
                        : departPositions.TryGetValue(ant, out var p) ? p : ant.transform.position;
                    pos.z = ant.transform.position.z;
                    ant.transform.position = pos;
                    ant.gameObject.SetActive(true);    // vuelve a Ant.All
                    Deliver(ant);
                    continue;
                }

                // D7: aparecen por el mismo extremo, cargadas, y caminan hasta la entrada.
                if (carriedPrey.TryGetValue(ant, out var mine) && mine.Count > 0) AddCarry(ant, mine[0].Icon, placeholderPreyColor);
                else if (carriedLeaves.TryGetValue(ant, out int n) && n > 0) AddCarry(ant, leafSprite, placeholderLeafColor);

                entrance.ArriveVia(ant, side);
                walkingHome.Add(ant);
                if (!ant.TryGetComponent<AntActuator>(out var actuator) || !actuator.MoveTo(entrance.Position))
                {
                    // Sin camino hasta la entrada: se la lleva directamente para no perder el botín.
                    Vector3 pos = entrance.Position;
                    pos.z = ant.transform.position.z;
                    ant.transform.position = pos;
                }
            }

            if (entrance != null && logExpeditions)
                Debug.Log($"[Expedition] Vuelve la expedición ({Names(group)}): caminan hacia la entrada con {totalLeaves} hojas" +
                          (preys.Count > 0 ? $" y {preys.Count} presa(s) ({PreyNames(preys)})" : ""), this);

            TickReturning();
        }

        /// <summary>D10: tira el botín del grupo (hojas por carga, presas por fuerza).</summary>
        private void RollLoot(List<Ant> group, out int leaves, out List<Prey> preys)
        {
            float carry = 0f, strength = 0f;
            foreach (var a in group) { carry += CarryOf(a); strength += StrengthOf(a); }

            float average = LeavesAverage(carry);
            leaves = Mathf.Max(1, Mathf.RoundToInt(average * Random.Range(1f - leavesVariation, 1f + leavesVariation)));

            preys = new List<Prey>();
            if (preyPool.Count == 0) return;
            for (int k = 0; k < maxPreyPerExpedition; k++)
            {
                float chance = PreySlotChance(strength, k);
                if (chance <= 0f) break;
                if (Random.value >= chance) continue;   // cada presa tiene su propia tirada
                var prey = PickPrey();
                if (prey != null) preys.Add(prey);
            }
        }

        /// <summary>
        /// Reparte el botín para llevarlo: las presas, a las más fuertes; las hojas, entre las demás según su carga
        /// (si todas llevan presa, entre todas).
        /// </summary>
        private void AssignLoot(List<Ant> group, int leaves, List<Prey> preys)
        {
            var byStrength = new List<Ant>(group);
            byStrength.Sort((a, b) => StrengthOf(b).CompareTo(StrengthOf(a)));
            for (int i = 0; i < preys.Count; i++)
            {
                var ant = byStrength[i % byStrength.Count];
                if (!carriedPrey.TryGetValue(ant, out var list)) carriedPrey[ant] = list = new List<Prey>();
                list.Add(preys[i]);
            }

            var leafCarriers = new List<Ant>();
            foreach (var a in group) if (!carriedPrey.ContainsKey(a)) leafCarriers.Add(a);
            if (leafCarriers.Count == 0) leafCarriers.AddRange(group);

            float totalCarry = 0f;
            foreach (var a in leafCarriers) totalCarry += Mathf.Max(0.01f, CarryOf(a));

            // Reparto proporcional; lo que sobra por redondeo va a las primeras.
            int given = 0;
            foreach (var a in leafCarriers)
            {
                int n = Mathf.FloorToInt(leaves * Mathf.Max(0.01f, CarryOf(a)) / totalCarry);
                carriedLeaves[a] = n;
                given += n;
            }
            for (int i = 0; given < leaves; i = (i + 1) % leafCarriers.Count, given++)
                carriedLeaves[leafCarriers[i]]++;
        }

        private void TickReturning()
        {
            for (int i = walkingHome.Count - 1; i >= 0; i--)
            {
                var ant = walkingHome[i];
                if (ant == null || !ant.IsAlive)
                {
                    // Murió por el camino: su parte del botín se pierde.
                    if (ant != null) { carriedLeaves.Remove(ant); carriedPrey.Remove(ant); RemoveCarry(ant); }
                    walkingHome.RemoveAt(i);
                    away.Remove(ant);
                    continue;
                }
                if (!HasArrived(ant)) continue;
                walkingHome.RemoveAt(i);
                Deliver(ant);
            }

            if (walkingHome.Count == 0) FinishReturn();
        }

        /// <summary>Ha llegado a la entrada (o algo la ha parado por el camino).</summary>
        private static bool HasArrived(Ant ant) =>
            !ant.TryGetComponent<AntActuator>(out var actuator) || actuator.Current != AntAction.Moving;

        /// <summary>Pasa por la entrada: suma su parte del botín, suelta la carga y recupera el cerebro.</summary>
        private void Deliver(Ant ant)
        {
            RemoveCarry(ant);
            if (carriedLeaves.TryGetValue(ant, out int leaves) && leaves > 0)
            {
                ResourceManager.Instance?.Add(ResourceType.Leaves, leaves);
                deliveredLeaves += leaves;
            }
            carriedLeaves.Remove(ant);
            if (carriedPrey.TryGetValue(ant, out var preys))
            {
                foreach (var p in preys) { AddPrey(p); deliveredPrey.Add(p); }   // a la lista de presas del Festín
                carriedPrey.Remove(ant);
            }
            away.Remove(ant);
            if (ant.TryGetComponent<IAntBrain>(out var brain)) brain.Resume();
        }

        private void FinishReturn()
        {
            var group = returningGroup ?? new Ant[0];
            phase = ExpeditionPhase.None;
            away.Clear();
            departPositions.Clear();
            carriedLeaves.Clear();
            carriedPrey.Clear();
            walkingHome.Clear();
            returningGroup = null;
            timer = 0f;

            var preys = deliveredPrey.ToArray();
            if (logExpeditions)
                Debug.Log($"[Expedition] La expedición ({Names(group)}) ha entregado {deliveredLeaves} hojas" +
                          (preys.Length > 0 ? $" y {preys.Length} presa(s): {PreyNames(preys)}" : ""), this);
            EventBus.Publish(new ExpeditionReturned(group, deliveredLeaves, preys));
        }

        // ================= FÓRMULAS (D10) =================

        /// <summary>Carga de una hormiga: su carryCapacity (asset de casta + genes).</summary>
        public static float CarryOf(Ant ant) => Mathf.Max(0f, StatsOf(ant).carryCapacity);

        /// <summary>Fuerza de una hormiga: su strength (asset de casta + genes).</summary>
        public static float StrengthOf(Ant ant) => Mathf.Max(0f, StatsOf(ant).strength);

        /// <summary>Stats finales; si aún no están calculados, los de su casta (o los por defecto).</summary>
        private static AntStats StatsOf(Ant ant)
        {
            if (ant == null) return default;
            var s = ant.Stats;
            if (s.carryCapacity > 0f || s.strength > 0f) return s;
            return ant.Caste != null ? ant.Caste.baseStats : AntStats.Default;
        }

        /// <summary>Hojas de media con una carga total 'carry': base × √carga.</summary>
        private float LeavesAverage(float carry) => carry <= 0f ? 0f : leavesBase * Mathf.Sqrt(carry);

        /// <summary>
        /// Probabilidad de la presa número k (0 = la primera): 1 − (1 − p)^(fuerza que sobra), con tope.
        /// Fuerza que sobra = suma − k × strengthPerExtraPrey. 0 si no sobra.
        /// </summary>
        private float PreySlotChance(float strength, int k)
        {
            float left = strength - k * strengthPerExtraPrey;
            if (left <= 0f) return 0f;
            float chance = 1f - Mathf.Pow(1f - preyChancePerStrength, left);
            return Mathf.Clamp(chance, 0f, preyChanceCap);
        }

        // ================= CARGA (sprite de F8) =================

        private void AddCarry(Ant ant, Sprite sprite, Color placeholderColor)
        {
            RemoveCarry(ant);
            var go = new GameObject("Carga");
            go.transform.SetParent(ant.transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : PlaceholderSprite;
            sr.color = sprite != null ? Color.white : placeholderColor;

            // Encima de todas las piezas de la hormiga, en su misma capa.
            int order = 0; int layer = 0; bool found = false;
            foreach (var r in ant.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (r == sr) continue;
                if (!found || r.sortingOrder >= order) { order = r.sortingOrder; layer = r.sortingLayerID; found = true; }
            }
            sr.sortingLayerID = layer;
            sr.sortingOrder = order + 1;

            // Tamaño y posición en casillas, compensando la escala de la hormiga (las castas la cambian).
            float cell = NestGrid.Instance != null ? NestGrid.Instance.CellSize : 0.4f;
            Vector3 parentScale = ant.transform.lossyScale;
            float ps = Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));
            Vector2 size = sr.sprite.bounds.size;
            float longest = Mathf.Max(0.0001f, Mathf.Max(size.x, size.y));
            go.transform.localScale = Vector3.one * (carrySizeCells * cell / longest / ps);
            go.transform.localPosition = new Vector3(carryOffsetCells.x, carryOffsetCells.y, -0.01f) * cell / ps;

            carryVisuals[ant] = go;
        }

        private void RemoveCarry(Ant ant)
        {
            if (ant == null || !carryVisuals.TryGetValue(ant, out var go)) return;
            carryVisuals.Remove(ant);
            if (go != null) Destroy(go);
        }

        private static Sprite PlaceholderSprite
        {
            get
            {
                if (placeholderSprite != null) return placeholderSprite;
                var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
                var pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                tex.SetPixels(pixels);
                tex.Apply();
                placeholderSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                placeholderSprite.name = "CargaProvisional";
                return placeholderSprite;
            }
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
            volunteers.Add(ant);   // D10: sin límite de apuntadas
        }

        public void Unvolunteer(Ant ant) => volunteers.Remove(ant);

        /// <summary>
        /// Las 'count' que saldrían: apuntadas, luego obreras sin sala y luego el resto. Orden fijo (no al azar)
        /// para que la estimación del menú sea la del grupo que sale.
        /// </summary>
        private List<Ant> PickGroup(int count)
        {
            var group = new List<Ant>(count);
            if (count <= 0) return group;

            // 1) Apuntadas desde el panel
            volunteers.RemoveAll(a => !CanVolunteer(a));
            foreach (var a in volunteers) { if (group.Count >= count) break; group.Add(a); }

            // 2) Obreras sin sala  3) cualquiera
            var unassigned = new List<Ant>();
            var assigned = new List<Ant>();
            foreach (var a in Ant.All)
            {
                if (!CanVolunteer(a) || group.Contains(a)) continue;
                (a.AssignedRoom == null ? unassigned : assigned).Add(a);
            }
            unassigned.Sort(ById);
            assigned.Sort(ById);
            foreach (var a in unassigned) { if (group.Count >= count) break; group.Add(a); }
            foreach (var a in assigned) { if (group.Count >= count) break; group.Add(a); }
            return group;
        }

        private static int ById(Ant a, Ant b) => a.GetInstanceID().CompareTo(b.GetInstanceID());

        // ================= EVENTOS =================

        private void OnAntDied(AntDied e) => volunteers.Remove(e.Ant);

        // ================= UTILIDADES =================

        private static string Names(IEnumerable<Ant> ants)
        {
            var names = new List<string>();
            foreach (var a in ants) if (a != null) names.Add(a.Name);
            return string.Join(", ", names);
        }

        private static string PreyNames(IEnumerable<Prey> preys)
        {
            var names = new List<string>();
            foreach (var p in preys) if (p != null) names.Add(p.displayName);
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
