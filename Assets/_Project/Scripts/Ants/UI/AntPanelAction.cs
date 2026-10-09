using System;
using System.Collections.Generic;
using Myrmutation.Core;

namespace Myrmutation.Ants
{
    /// <summary>
    /// Botones de acción que OTROS sistemas añaden al panel de hormiga (B4), sin tocar AntPanelUI.
    ///
    /// Ejemplo (C, desde su propio script, p. ej. en OnEnable):
    ///     AntPanelAction.Register("Devorar",
    ///         ant => !ant.IsQueen,              // cuándo se muestra el botón
    ///         ant => feast.StartDevour(ant));   // qué hace al pulsarlo
    ///
    /// Y en OnDisable:  AntPanelAction.Unregister("Devorar");
    /// Registrar otra vez con el mismo texto sustituye a la anterior.
    /// </summary>
    public static class AntPanelAction
    {
        public sealed class Entry
        {
            public string Label;
            public Func<Ant, bool> IsAvailable;   // null = siempre
            public Action<Ant> OnClick;
        }

        private static readonly List<Entry> entries = new List<Entry>();

        public static IReadOnlyList<Entry> All => entries;

        /// <summary>Cambió la lista de acciones (el panel se redibuja).</summary>
        public static event Action Changed;

        public static void Register(string label, Func<Ant, bool> isAvailable, Action<Ant> onClick)
        {
            if (string.IsNullOrEmpty(label) || onClick == null) return;
            entries.RemoveAll(e => e.Label == label);
            entries.Add(new Entry { Label = label, IsAvailable = isAvailable, OnClick = onClick });
            Changed?.Invoke();
        }

        public static void Unregister(string label)
        {
            if (entries.RemoveAll(e => e.Label == label) > 0) Changed?.Invoke();
        }
    }
}
