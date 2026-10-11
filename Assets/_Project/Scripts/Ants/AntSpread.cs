using UnityEngine;
using UnityEngine.Rendering;

namespace Myrmutation.Ants
{
    /// <summary>
    /// A5 · Evita que hormigas iguales se vean como una sola.
    /// Desplaza un poco el dibujo (hijo "Visual") de cada hormiga al azar y le da su propio orden
    /// de dibujado (SortingGroup), para que al cruzarse no se mezclen sus piezas.
    /// Solo toca el visual: la posición real, las rutas y la IA no cambian.
    /// Se ejecuta antes que AntActuator para que este guarde ya la posición desplazada como base.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class AntSpread : MonoBehaviour
    {
        [Tooltip("Hijo con el dibujo de la hormiga. Vacío = busca uno llamado 'Visual'")]
        [SerializeField] private Transform visual;
        [Tooltip("Desplazamiento máximo a izquierda/derecha (unidades)")]
        [SerializeField] private float maxOffsetX = 0.4f;
        [Tooltip("Desplazamiento máximo arriba/abajo (pequeño, para no salirse del túnel)")]
        [SerializeField] private float maxOffsetY = 0.08f;
        [Tooltip("Cada hormiga se dibuja como un bloque, sin mezclar sus piezas con las de otra")]
        [SerializeField] private bool useSortingGroup = true;
        [SerializeField] private int baseSortingOrder = 10;

        private static int counter;

        private void Awake()
        {
            if (visual == null) visual = transform.Find("Visual");
            if (visual == null) return;

            visual.localPosition += new Vector3(
                Random.Range(-maxOffsetX, maxOffsetX),
                Random.Range(-maxOffsetY, maxOffsetY),
                0f);

            if (!useSortingGroup) return;
            var group = visual.GetComponent<SortingGroup>();
            if (group == null) group = visual.gameObject.AddComponent<SortingGroup>();
            group.sortingOrder = baseSortingOrder + (counter++ % 50);
        }
    }
}
