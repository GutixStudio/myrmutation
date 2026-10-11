using Myrmutation.Core;
using UnityEngine;

namespace Myrmutation.Colony
{
    /// <summary>
    /// D2 · La reina pone un huevo cada X segundos si hay comida (y sitio en la Guardería).
    /// Va en la hormiga reina (Ant con Is Queen marcado), junto a QueenGenome (C2).
    /// </summary>
    [RequireComponent(typeof(Ant))]
    public class Queen : MonoBehaviour
    {
        public enum LayBlock { None, Dead, NoNursery, NurseryFull, NoFood, Winter }

        [SerializeField] private float layIntervalSeconds = 15f;
        [Tooltip("Comida que cuesta cada huevo")]
        [SerializeField] private int eggFoodCost = 2;
        [Tooltip("Solo pone huevos si hay una Guardería construida")]
        [SerializeField] private bool requireBuiltNursery = true;
        [Tooltip("No pone huevos en invierno")]
        [SerializeField] private bool stopInWinter = false;
        [Tooltip("Vacío: usa Nursery.Instance")]
        [SerializeField] private Nursery nursery;

        private Ant ant;
        private float timer;

        public float LayProgress01 => Mathf.Clamp01(timer / Mathf.Max(0.01f, layIntervalSeconds));
        public LayBlock Blocked { get; private set; }
        public int EggFoodCost => eggFoodCost;

        private void Awake() => ant = GetComponent<Ant>();

        private void Start()
        {
            if (!ant.IsQueen)
                Debug.LogWarning("[Queen] Esta hormiga no tiene marcado Is Queen.", this);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            timer = Mathf.Min(timer + dt, layIntervalSeconds);
            if (timer < layIntervalSeconds) return;

            Blocked = Check();
            if (Blocked != LayBlock.None) return; // espera con el temporizador lleno

            if (!ResourceManager.Instance.TrySpend(ResourceType.Food, eggFoodCost)) { Blocked = LayBlock.NoFood; return; }
            Nest.AddEgg();
            timer = 0f;
        }

        private Nursery Nest => nursery != null ? nursery : Nursery.Instance;

        private LayBlock Check()
        {
            if (ant == null || !ant.IsAlive) return LayBlock.Dead;
            if (Nest == null || (requireBuiltNursery && Room.FindBuilt(RoomType.Nursery) == null)) return LayBlock.NoNursery;
            if (!Nest.HasSpace) return LayBlock.NurseryFull;
            if (stopInWinter && GameManager.Instance != null && GameManager.Instance.CurrentSeason == Season.Winter) return LayBlock.Winter;
            if (ResourceManager.Instance == null || !ResourceManager.Instance.Has(ResourceType.Food, eggFoodCost)) return LayBlock.NoFood;
            return LayBlock.None;
        }
    }
}
