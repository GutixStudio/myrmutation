using Myrmutation.Core;
using Myrmutation.Core.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Myrmutation.Building
{
    public enum BuildTool { Tunnel, Room, Erase }

    /// <summary>
    /// Modo Construcción (diseno-construccion-beta.md §5): pausa al entrar y recupera la velocidad al salir, guarda una
    /// copia del plano para Cancelar (decisiones.md) y aplica las herramientas Túnel, Salas y Borrar con el ratón o un dedo.
    /// La cámara va en modo herramienta (CameraController.ToolMode). La UI está en BuildModeUI y el dibujo en BlueprintOverlay.
    /// </summary>
    public class BuildModeController : MonoBehaviour
    {
        [Tooltip("Planificador. Si se deja vacío, usa Digging.Current")]
        [SerializeField] private DigPlanner planner;
        [SerializeField] private BlueprintOverlay overlay;
        [Tooltip("Cámara del juego. Si se deja vacío, usa Camera.main")]
        [SerializeField] private Camera cam;

        [Header("Pincel")]
        [Tooltip("Radio del pincel de túnel y de borrar, en casillas")]
        [SerializeField] private float brushRadius = 1.5f;
        [Tooltip("En táctil, la herramienta va estos píxeles por encima del dedo para que se vea")]
        [SerializeField] private float touchOffsetPx = 80f;

        public bool IsOpen { get; private set; }
        public BuildTool Tool { get; private set; } = BuildTool.Tunnel;
        public RoomData SelectedRoom { get; private set; }
        /// <summary>Táctil: el fantasma de sala espera a ✓ / ✕.</summary>
        public bool HasPendingRoom { get; private set; }
        /// <summary>Validez del fantasma de sala en su posición actual.</summary>
        public RoomPlanResult GhostResult { get; private set; }
        public bool HasGhost { get; private set; }
        public bool IsRunning => Time.timeScale > 0f;
        public DigPlanner Planner => planner;

        private int previousSpeed;
        private bool stroking, gestureOnUI;
        private Vector3 lastWorld;
        private RectInt ghostRect;
        private float lastTouchTime = -10f;

        private void OnDisable()
        {
            if (IsOpen) Confirm();
        }

        // ================= ENTRAR Y SALIR =================

        public void Open()
        {
            if (IsOpen) return;
            if (planner == null) planner = Digging.Current as DigPlanner;
            if (planner == null) { Debug.LogError("[BuildMode] No hay DigPlanner en la escena", this); return; }

            IsOpen = true;
            planner.BeginEdit();
            var gm = GameManager.Instance;
            previousSpeed = gm != null ? gm.Speed : 1;
            if (gm != null) gm.SetSpeed(0);
            CameraController.ToolMode = true;
            if (overlay != null) overlay.SetOpen(true);
            SelectTool(BuildTool.Tunnel);
        }

        /// <summary>✓: se queda con los planos y sale.</summary>
        public void Confirm()
        {
            if (!IsOpen) return;
            planner.Commit();
            Close();
        }

        /// <summary>✕: deja los planos como estaban al entrar (lo excavado no se rellena) y sale.</summary>
        public void Cancel()
        {
            if (!IsOpen) return;
            planner.Revert();
            Close();
        }

        /// <summary>▶ / pausa dentro del modo. Reanuda a la velocidad que había al entrar (×1 si ya estaba en pausa).</summary>
        public void TogglePause()
        {
            var gm = GameManager.Instance;
            if (!IsOpen || gm == null) return;
            gm.SetSpeed(gm.Speed > 0 ? 0 : Mathf.Max(1, previousSpeed));
        }

        private void Close()
        {
            IsOpen = false;
            stroking = false;
            ClearGhost();
            if (GameManager.Instance != null) GameManager.Instance.SetSpeed(previousSpeed);
            CameraController.ToolMode = false;
            if (overlay != null) overlay.SetOpen(false);
        }

        // ================= HERRAMIENTAS =================

        public void SelectTool(BuildTool tool)
        {
            Tool = tool;
            stroking = false;
            if (tool != BuildTool.Room) SelectedRoom = null;
            ClearGhost();
        }

        public void SelectRoom(RoomData data)
        {
            SelectTool(BuildTool.Room);
            SelectedRoom = data;
        }

        /// <summary>✓ del fantasma en táctil.</summary>
        public void PlacePendingRoom()
        {
            if (!HasPendingRoom) return;
            if (planner.PlanRoom(SelectedRoom, ghostRect.position) == RoomPlanResult.Ok) ClearGhost();
            else UpdateGhost(ghostRect.position, true);
        }

        /// <summary>✕ del fantasma en táctil.</summary>
        public void DiscardPendingRoom() => ClearGhost();

        private void ClearGhost()
        {
            HasGhost = false;
            HasPendingRoom = false;
            GhostResult = RoomPlanResult.Ok;
            if (overlay != null) overlay.HidePreview();
        }

        // ================= ENTRADA =================

        private void Update()
        {
            if (!IsOpen) return;
            if (cam == null) cam = Camera.main;

            var touches = Touch.activeTouches;
            if (touches.Count > 0)
            {
                lastTouchTime = Time.unscaledTime;
                if (touches.Count > 1) { stroking = false; HidePointerPreview(); return; }   // dos dedos: cámara
                Touch t = touches[0];
                if (t.phase == TouchPhase.Began) gestureOnUI = CameraController.IsOverUI(t.screenPosition);
                if (gestureOnUI) return;
                bool ended = t.phase is TouchPhase.Ended or TouchPhase.Canceled;
                Pointer(ScreenToWorld(t.screenPosition + Vector2.up * touchOffsetPx), t.phase == TouchPhase.Began, !ended, true);
                if (ended) HidePointerPreview();
                return;
            }

            // En navegadores móviles el toque también genera eventos de ratón: los ignoramos un momento (como la cámara).
            var mouse = Mouse.current;
            if (mouse == null || Time.unscaledTime - lastTouchTime < 0.5f) return;
            Vector2 pos = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) gestureOnUI = CameraController.IsOverUI(pos);
            if (!mouse.leftButton.isPressed && CameraController.IsOverUI(pos)) { stroking = false; HidePointerPreview(); return; }
            if (gestureOnUI && mouse.leftButton.isPressed) return;
            Pointer(ScreenToWorld(pos), mouse.leftButton.wasPressedThisFrame, mouse.leftButton.isPressed, false);
        }

        /// <summary>Un puntero en 'world': began al pulsar, held mientras sigue pulsado. Con ratón también llega sin pulsar (vista previa).</summary>
        private void Pointer(Vector3 world, bool began, bool held, bool touch)
        {
            switch (Tool)
            {
                case BuildTool.Tunnel:
                case BuildTool.Erase:
                    bool erase = Tool == BuildTool.Erase;
                    if (overlay != null) overlay.ShowBrush(world, brushRadius, erase);
                    if (began)
                    {
                        stroking = !(erase && planner.CancelRoom(NestGrid.Instance.WorldToCell(world)));   // tocar una sala en plano la cancela
                        lastWorld = world;
                    }
                    if (!held) { stroking = false; return; }
                    if (!stroking) return;
                    if (erase) planner.EraseStroke(lastWorld, world, brushRadius);
                    else planner.PaintStroke(lastWorld, world, brushRadius);
                    lastWorld = world;
                    break;

                case BuildTool.Room:
                    if (SelectedRoom == null) return;
                    if (touch)
                    {
                        if (held) { UpdateGhost(GhostOrigin(world), false); HasPendingRoom = true; }
                        return;
                    }
                    UpdateGhost(GhostOrigin(world), false);
                    if (began && planner.PlanRoom(SelectedRoom, ghostRect.position) == RoomPlanResult.Ok) UpdateGhost(ghostRect.position, true);
                    break;
            }
        }

        // El fantasma va centrado en el puntero; DigPlanner.RoomRect lo ajusta a bloques.
        private Vector2Int GhostOrigin(Vector3 world) =>
            NestGrid.Instance.WorldToCell(world) - SelectedRoom.footprint / 2;

        private void UpdateGhost(Vector2Int origin, bool force)
        {
            RectInt rect = DigPlanner.RoomRect(SelectedRoom, origin);
            if (!force && HasGhost && rect.position == ghostRect.position) return;   // solo se revalida al cambiar de bloque
            ghostRect = rect;
            HasGhost = true;
            GhostResult = planner.CheckRoom(SelectedRoom, rect.position);
            if (overlay != null) overlay.ShowGhost(rect, GhostResult == RoomPlanResult.Ok);
        }

        // Al soltar el dedo el pincel desaparece; el fantasma pendiente se queda para ✓ / ✕.
        private void HidePointerPreview()
        {
            if (Tool != BuildTool.Room && overlay != null) overlay.HidePreview();
            if (Tool == BuildTool.Room && !HasPendingRoom) ClearGhost();
        }

        private Vector3 ScreenToWorld(Vector2 screen)
        {
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            w.z = 0f;
            return w;
        }
    }
}
