using Myrmutation.Ants;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Myrmutation.Building
{
    /// <summary>
    /// Prueba de A7 y A8 (escenas Test_A7 y Test_A8). Ctrl + clic izquierdo: todas las hormigas van a ese punto.
    /// Ctrl + clic derecho: excava un círculo (para comprobar que la caché de rutas se vacía), sin tocar la pared de las salas.
    /// Sin Ctrl o sobre la UI, los clics no hacen nada.
    /// Con planificador (A8): mientras Shift está pulsado la cámara va en modo herramienta (CameraController.ToolMode):
    /// arrastrar con clic izquierdo pinta plano de túnel, mantener E lo borra bajo el ratón y el clic derecho mueve la vista.
    /// R planifica la sala de prueba en el ratón (el motivo del rechazo sale en la consola).
    /// Sin planificador, con la cámara en modo herramienta (modo Construcción, A9) no hace nada.
    /// </summary>
    public class NavDebugClicker : MonoBehaviour
    {
        [SerializeField] private NestGrid grid;
        [Tooltip("Radio del círculo que excava el clic derecho, en casillas")]
        [SerializeField] private float digRadius = 1.5f;

        [Header("Planos (A8)")]
        [Tooltip("Planificador de excavación. Vacío = sin pintar planos (como en Test_A7)")]
        [SerializeField] private DigPlanner planner;
        [Tooltip("Sala que se planifica con la tecla R (la Guardería)")]
        [SerializeField] private RoomData testRoom;
        [Tooltip("Radio del pincel, en casillas")]
        [SerializeField] private float brushRadius = 1.5f;

        private Camera cam;
        private Vector3 lastStroke;

        private void Start()
        {
            cam = Camera.main;
            if (grid == null) grid = NestGrid.Instance;
        }

        private void OnDisable()
        {
            if (planner != null) CameraController.ToolMode = false;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || cam == null || grid == null) return;

            Vector3 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            world.z = 0f;   // ScreenToWorldPoint da la z de la cámara
            var keyboard = Keyboard.current;
            if (planner != null && keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame && testRoom != null)
                {
                    var result = planner.PlanRoom(testRoom, grid.WorldToCell(world));
                    if (result != RoomPlanResult.Ok) Debug.Log($"[NavDebugClicker] Sala rechazada: {DigPlanner.Describe(result)}", this);
                }
                // Con Shift la cámara pasa a modo herramienta: el izquierdo pinta y el derecho mueve la vista.
                CameraController.ToolMode = keyboard.shiftKey.isPressed;
                if (CameraController.ToolMode)
                {
                    if (mouse.leftButton.wasPressedThisFrame) lastStroke = world;
                    if (mouse.leftButton.isPressed) { planner.PaintStroke(lastStroke, world, brushRadius); lastStroke = world; }
                    if (keyboard.eKey.isPressed) planner.Erase(world, brushRadius);
                    return;
                }
            }

            // Solo con Ctrl y fuera de la UI: un clic normal (en "Construir", ✓, ✕ o un trazo) no debe mover a todas las hormigas.
            if (CameraController.ToolMode || keyboard == null || !keyboard.ctrlKey.isPressed) return;
            bool left = mouse.leftButton.wasPressedThisFrame, right = mouse.rightButton.wasPressedThisFrame;
            if ((!left && !right) || CameraController.IsOverUI(mouse.position.ReadValue())) return;
            if (left) SendAll(world);
            if (right) Dig(grid.WorldToCell(world));
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
