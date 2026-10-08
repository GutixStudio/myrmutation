using UnityEngine;

namespace Myrmutation.Ants.Testing
{
    /// <summary>
    /// SOLO PRUEBAS (Scenes/Test/B.unity). Necesidades falsas con deslizadores para probar AntSensor
    /// mientras no exista AntNeeds. No ponerlo en el prefab Ant.
    /// </summary>
    public class AntTestNeeds : MonoBehaviour, IAntNeeds
    {
        [Range(0f, 1f)] [SerializeField] private float hunger = 0f;
        [Range(0f, 1f)] [SerializeField] private float energy = 1f;

        [Tooltip("Si está activo, el hambre sube y la energía baja con el tiempo")]
        [SerializeField] private bool drainOverTime;
        [SerializeField] private float drainPerSecond = 0.05f;

        [Tooltip("Hambre que quita cada ración comida")]
        [SerializeField] private float hungerPerRation = 0.5f;

        public float Hunger01 => hunger;
        public float Energy01 => energy;

        private AntActuator actuator;

        private void Awake() => actuator = GetComponent<AntActuator>();
        private void OnEnable() { if (actuator != null) actuator.Ate += OnAte; }
        private void OnDisable() { if (actuator != null) actuator.Ate -= OnAte; }

        private void Update()
        {
            if (!drainOverTime) return;
            hunger = Mathf.Clamp01(hunger + drainPerSecond * Time.deltaTime);
            energy = Mathf.Clamp01(energy - drainPerSecond * Time.deltaTime);
        }

        private void OnAte(int rations) => hunger = Mathf.Clamp01(hunger - rations * hungerPerRation);
    }
}
