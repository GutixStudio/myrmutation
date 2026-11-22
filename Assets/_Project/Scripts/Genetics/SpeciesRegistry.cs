using System;
using System.Collections.Generic;
using Myrmutation.Core;
using Myrmutation.Expedition;
using UnityEngine;

namespace Myrmutation.Genetics
{
    /// <summary>
    /// Registro de especies descubiertas (C9). Una especie se descubre al devorar la primera presa de
    /// esa especie; desde entonces todas sus presas muestran su gen, durante el resto de la partida.
    ///
    /// La especie se identifica por Prey.displayName (Prey no tiene id).
    /// Vive en un componente de la escena (va en el prefab FeastPanel), así que se reinicia solo
    /// al empezar partida nueva (al recargar la escena).
    /// </summary>
    [DisallowMultipleComponent]
    public class SpeciesRegistry : MonoBehaviour
    {
        public static SpeciesRegistry Instance { get; private set; }

        private readonly HashSet<string> discovered = new HashSet<string>();

        /// <summary>Se acaba de descubrir una especie.</summary>
        public event Action<Prey> Discovered;

        public int Count => discovered.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Hay más de un SpeciesRegistry en la escena; se ignora este.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool IsDiscovered(Prey prey) => prey != null && discovered.Contains(Key(prey));

        /// <summary>
        /// Marca la especie de esta presa como descubierta. Devuelve true si era la primera vez
        /// (y entonces avisa en el log: "¡Descubierto! Hormiga cortadora → Mandíbula cortadora").
        /// </summary>
        public bool Discover(Prey prey)
        {
            if (prey == null || !discovered.Add(Key(prey))) return false;

            string geneName = prey.gene != null ? prey.gene.displayName : "no tiene gen";
            EventBus.Publish(new LogMessage(LogCategory.Mutacion, $"¡Descubierto! {prey.displayName} → {geneName}"));
            Discovered?.Invoke(prey);
            return true;
        }

        private static string Key(Prey prey) =>
            string.IsNullOrEmpty(prey.displayName) ? "" : prey.displayName.Trim().ToLowerInvariant();

        [ContextMenu("Olvidar todas las especies (pruebas)")]
        private void Forget() => discovered.Clear();
    }
}