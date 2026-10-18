using Myrmutation.Ants;
using Myrmutation.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Myrmutation.Building
{
    /// <summary>
    /// Prueba de A7 (escena Test_A7). Clic izquierdo: todas las hormigas van a ese punto.
    /// Clic derecho: excava un círculo (para comprobar que la caché de rutas se vacía), sin tocar la pared de las salas.
    /// </summary>
    public class NavDebugClicker : MonoBehaviour
    {
        [SerializeField] private NestGrid grid;
        [Tooltip("Radio del círculo que excava el clic derecho, en casillas")]
        [SerializeField] private float digRadius = 1.5f;

        private Camera cam;

        private void Start()
        {
            cam = Camera.main;
            if (grid == null) grid = NestGrid.Instance;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || cam == null || grid == null) return;

            Vector3 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            if (mouse.leftButton.wasPressedThisFrame) SendAll(world);
            if (mouse.rightButton.wasPressedThisFrame) Dig(grid.WorldToCell(world));
        }

        private static void SendAll(Vector3 point)
        {
            foreach (var ant in Ant.All)
                if (ant.TryGetComponent<AntActuator>(out var actuator)) actuator.MoveTo(point);
        }

        private void Dig(Vector2Int center)
        {
            int r = Mathf.CeilToInt(digRadius);
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    var c = new Vector2Int(center.x + dx, center.y + dy);
                    if (dx * dx + dy * dy <= digRadius * digRadius && grid.Get(c) == CellType.Solid && !grid.IsRoomWall(c))
                        grid.Set(c, CellType.Dug);
                }
        }
    }
}
