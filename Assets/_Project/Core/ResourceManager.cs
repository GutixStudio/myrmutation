using System;
using System.Collections.Generic;
using UnityEngine;

namespace Myrmutation.Core
{
    /// <summary>Recursos de la colonia. Implementación básica: D (D1) puede ampliarla sin cambiar la API pública.</summary>
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        [Serializable] private struct StartAmount { public ResourceType type; public int amount; }

        [SerializeField] private List<StartAmount> startingResources = new List<StartAmount>
        {
            new StartAmount { type = ResourceType.Food, amount = 50 },
            new StartAmount { type = ResourceType.Leaves, amount = 0 },
            new StartAmount { type = ResourceType.Fungus, amount = 0 },
            new StartAmount { type = ResourceType.Biomass, amount = 0 },
        };

        private readonly Dictionary<ResourceType, int> amounts = new Dictionary<ResourceType, int>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            foreach (ResourceType t in Enum.GetValues(typeof(ResourceType))) amounts[t] = 0;
            foreach (var s in startingResources) amounts[s.type] = s.amount;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            foreach (var kv in amounts) EventBus.Publish(new ResourceChanged(kv.Key, kv.Value));
        }

        public int Get(ResourceType type) => amounts[type];

        public bool Has(ResourceType type, int amount) => amounts[type] >= amount;

        public void Add(ResourceType type, int amount)
        {
            if (amount == 0) return;
            amounts[type] = Mathf.Max(0, amounts[type] + amount);
            EventBus.Publish(new ResourceChanged(type, amounts[type]));
        }

        /// <summary>Gasta si hay suficiente. Devuelve false (y no gasta nada) si no llega.</summary>
        public bool TrySpend(ResourceType type, int amount)
        {
            if (!Has(type, amount)) return false;
            Add(type, -amount);
            return true;
        }
    }
}
