using System;
using System.Collections.Generic;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using Myrmutation.Expedition;
using UnityEngine;

namespace Myrmutation.UI
{
    /// <summary>Una entrada del registro: el mensaje + el día de juego en que ocurrió.</summary>
    public readonly struct LogEntry
    {
        public readonly LogCategory Category;
        public readonly string Text;
        public readonly Transform Focus;          // puede volverse null si el objeto se destruye
        public readonly Vector3 FocusPosition;     // dónde estaba Focus al registrarse (para muertes, etc.)
        public readonly bool HasFocus;
        public readonly int Day;

        public LogEntry(LogCategory category, string text, Transform focus, int day)
        {
            Category = category;
            Text = text;
            Focus = focus;
            HasFocus = focus != null;
            FocusPosition = focus != null ? focus.position : Vector3.zero;
            Day = day;
        }

        /// <summary>Punto al que centrar la cámara: el objeto si sigue vivo, si no donde estaba.</summary>
        public Vector3 FocusPoint => Focus != null ? Focus.position : FocusPosition;
    }

    /// <summary>
    /// E11 · Registro de eventos de la colonia (los datos; lo que se ve es EventLogUI).
    ///
    ///  · Guarda las últimas 'capacity' (50) entradas con el día de juego.
    ///  · Cualquier sistema publica con el contrato de GameEvents.cs:
    ///        EventBus.Publish(new LogMessage(LogCategory.Mutacion, "Texto", objeto.transform));
    ///  · Además traduce a mensajes los eventos que ya existían (muertes, nacimientos, salas,
    ///    estaciones, expediciones…). Cada uno se puede activar o desactivar en el Inspector,
    ///    por si algún sistema ya publica su propio LogMessage y saldría repetido.
    ///
    /// Una por escena (va junto a la UI del HUD; la coloca A en Game.unity).
    /// </summary>
    public class EventLog : MonoBehaviour
    {
        public static EventLog Instance { get; private set; }

        [Tooltip("Entradas que se guardan (las más antiguas se descartan)")]
        [SerializeField] private int capacity = 50;

        [Header("Eventos existentes que se traducen a mensajes")]
        [SerializeField] private bool logBirths = true;
        [Tooltip("Ignora los nacimientos de los primeros segundos (las hormigas iniciales de la partida)")]
        [SerializeField] private float ignoreBirthsAtStart = 1f;
        [SerializeField] private bool logDeaths = true;
        [SerializeField] private bool logRoomsBuilt = true;
        [SerializeField] private bool logSeasons = true;
        [SerializeField] private bool logExpeditions = true;
        [Tooltip("Desactivado por defecto: C8 publica sus propios mensajes de mutación. Activar solo si no lo hace.")]
        [SerializeField] private bool logMutations = false;
        [Tooltip("Desactivado por defecto: C8 publica sus propios mensajes de herencia. Activar solo si no lo hace.")]
        [SerializeField] private bool logInheritance = false;

        private readonly List<LogEntry> entries = new List<LogEntry>();
        private float startTime;

        /// <summary>Entradas de la más antigua a la más reciente.</summary>
        public IReadOnlyList<LogEntry> Entries => entries;

        /// <summary>Se acaba de añadir una entrada.</summary>
        public event Action<LogEntry> Added;

        /// <summary>Atajo para publicar un mensaje desde cualquier script.</summary>
        public static void Post(LogCategory category, string text, Transform focus = null) =>
            EventBus.Publish(new LogMessage(category, text, focus));

        // ================= CICLO =================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            startTime = Time.unscaledTime;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void OnEnable()
        {
            EventBus.Subscribe<LogMessage>(OnLogMessage);
            EventBus.Subscribe<AntSpawned>(OnAntSpawned);
            EventBus.Subscribe<AntDied>(OnAntDied);
            EventBus.Subscribe<RoomBuilt>(OnRoomBuilt);
            EventBus.Subscribe<SeasonChanged>(OnSeasonChanged);
            EventBus.Subscribe<ExpeditionSent>(OnExpeditionSent);
            EventBus.Subscribe<ExpeditionReturned>(OnExpeditionReturned);
            EventBus.Subscribe<AntMutated>(OnAntMutated);
            EventBus.Subscribe<GeneInherited>(OnGeneInherited);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LogMessage>(OnLogMessage);
            EventBus.Unsubscribe<AntSpawned>(OnAntSpawned);
            EventBus.Unsubscribe<AntDied>(OnAntDied);
            EventBus.Unsubscribe<RoomBuilt>(OnRoomBuilt);
            EventBus.Unsubscribe<SeasonChanged>(OnSeasonChanged);
            EventBus.Unsubscribe<ExpeditionSent>(OnExpeditionSent);
            EventBus.Unsubscribe<ExpeditionReturned>(OnExpeditionReturned);
            EventBus.Unsubscribe<AntMutated>(OnAntMutated);
            EventBus.Unsubscribe<GeneInherited>(OnGeneInherited);
        }

        // ================= API =================

        public void Add(LogCategory category, string text, Transform focus = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            int day = GameManager.Instance != null ? GameManager.Instance.Day : 0;
            var entry = new LogEntry(category, text, focus, day);

            entries.Add(entry);
            int max = Mathf.Max(1, capacity);
            while (entries.Count > max) entries.RemoveAt(0);

            Added?.Invoke(entry);
        }

        public void Clear() => entries.Clear();

        // ================= CONTRATO =================

        private void OnLogMessage(LogMessage m) => Add(m.Category, m.Text, m.Focus);

        // ================= EVENTOS EXISTENTES =================

        private void OnAntSpawned(AntSpawned e)
        {
            if (!logBirths || e.Ant == null) return;
            if (Time.unscaledTime - startTime < ignoreBirthsAtStart) return;
            string caste = e.Ant.IsQueen ? "reina" : e.Ant.Caste != null ? e.Ant.Caste.displayName.ToLower() : null;
            Add(LogCategory.Nacimiento,
                caste != null ? $"Ha nacido {e.Ant.Name} ({caste})" : $"Ha nacido {e.Ant.Name}",
                e.Ant.transform);
        }

        private void OnAntDied(AntDied e)
        {
            if (!logDeaths || e.Ant == null) return;
            if (e.Ant.IsQueen)
            {
                Add(LogCategory.Muerte, "¡La reina ha muerto!", e.Ant.transform);
                return;
            }
            string how;
            switch (e.Cause)
            {
                case DeathCause.Starvation: how = "ha muerto de hambre"; break;
                case DeathCause.Eaten: how = "ha sido devorada"; break;
                case DeathCause.Combat: how = "ha muerto en combate"; break;
                case DeathCause.Infection: how = "ha muerto por una infección"; break;
                default: how = "ha muerto"; break;
            }
            Add(LogCategory.Muerte, $"{e.Ant.Name} {how}", e.Ant.transform);
        }

        private void OnRoomBuilt(RoomBuilt e)
        {
            if (!logRoomsBuilt || e.Room == null) return;
            string name = e.Room.Data != null ? e.Room.Data.displayName : "Una sala";
            Add(LogCategory.Construccion, $"{name} terminada", e.Room.transform);
        }

        private void OnSeasonChanged(SeasonChanged e)
        {
            if (!logSeasons) return;
            switch (e.Season)
            {
                case Season.Spring: Add(LogCategory.Estacion, "Llega la primavera"); break;
                case Season.Summer: Add(LogCategory.Estacion, "Llega el verano"); break;
                case Season.Autumn: Add(LogCategory.Estacion, "Llega el otoño"); break;
                default: Add(LogCategory.Estacion, "Llega el invierno: más consumo y sin expediciones"); break;
            }
        }

        private void OnExpeditionSent(ExpeditionSent e)
        {
            if (!logExpeditions) return;
            int n = e.Ants != null ? e.Ants.Count : 0;
            Add(LogCategory.Expedicion, n == 1 ? "Sale una expedición de 1 hormiga" : $"Sale una expedición de {n} hormigas");
        }

        private void OnExpeditionReturned(ExpeditionReturned e)
        {
            if (!logExpeditions) return;
            Transform focus = e.Ants != null && e.Ants.Count > 0 && e.Ants[0] != null ? e.Ants[0].transform : null;
            if (e.Prey != null) Add(LogCategory.Expedicion, $"Vuelve la expedición con una presa: {e.Prey.displayName}", focus);
            else if (e.Leaves > 0) Add(LogCategory.Expedicion, $"Vuelve la expedición con {e.Leaves} hojas", focus);
            else Add(LogCategory.Expedicion, "Vuelve la expedición con las manos vacías", focus);
        }

        private void OnAntMutated(AntMutated e)
        {
            if (!logMutations || e.Ant == null || e.Gene == null) return;
            string what = e.Gene.isFlaw ? $"la tara {e.Gene.displayName}" : e.Gene.displayName;
            Add(LogCategory.Mutacion, $"{e.Ant.Name} ha mutado: {what}", e.Ant.transform);
        }

        private void OnGeneInherited(GeneInherited e)
        {
            if (!logInheritance || e.Gene == null) return;
            Add(LogCategory.Mutacion, $"La reina transmite {e.Gene.displayName} a la casta {CasteName(e.Caste)}");
        }

        private static string CasteName(CasteType c) =>
            c == CasteType.Minor ? "menor" : c == CasteType.Media ? "media" : "mayor";
    }
}
