using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Myrmutation.Building
{
    /// <summary>
    /// Cámara 2D para ratón y táctil:
    /// - Arrastrar (ratón o 1 dedo) mueve la cámara.
    /// - Rueda o pellizco (2 dedos) hace zoom hacia el punto señalado.
    /// - Un toque/clic sin arrastrar lanza el evento Tapped con la posición en el mundo
    ///   (lo usan la construcción y la selección de hormigas).
    /// - Ignora los toques que empiezan sobre la UI.
    /// - No deja que la cámara salga del mapa (NestGrid) o, en la alfa, del sprite de fondo (bounds).
    /// Con ToolMode (modo herramienta, §5): el clic izquierdo y un dedo son de la herramienta; la cámara se mueve con el
    /// clic derecho o la rueda pulsada y con dos dedos, y no se lanza Tapped.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        /// <summary>Toque o clic sin arrastrar, en coordenadas de mundo.</summary>
        public static event Action<Vector2> Tapped;

        /// <summary>
        /// Modo herramienta (pintar planos, §5): el clic izquierdo y un dedo no mueven la cámara.
        /// Se mueve con clic derecho o rueda pulsada (arrastrar) y con dos dedos; la rueda y el pellizco hacen zoom.
        /// </summary>
        public static bool ToolMode { get; set; }

        // Sin recarga de dominio (Enter Play Mode Options) lo estático sobrevive entre partidas.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ToolMode = false;
            Tapped = null;
        }

        [Header("Límites")]
        [Tooltip("Solo si no hay NestGrid (escenas de la alfa): la cámara nunca muestra nada fuera de este sprite")]
        [SerializeField] private SpriteRenderer bounds;

        [Header("Zoom (tamaño ortográfico)")]
        [SerializeField] private float minZoom = 3f;
        [SerializeField] private float maxZoom = 12f;
        [Tooltip("Cuánto cambia el zoom por cada paso de rueda (0.1 = 10 %)")]
        [SerializeField] private float wheelStep = 0.12f;

        [Header("Gestos")]
        [Tooltip("Píxeles que hay que mover para que cuente como arrastre y no como toque")]
        [SerializeField] private float dragThresholdPx = 12f;
        [SerializeField] private bool debugTaps;

        private Camera cam;
        private bool pressing, dragging, pinching, wasToolMode;
        private Vector2 pressScreenPos;
        private Vector3 grabWorld;
        private float lastPinchDist;
        private float lastTouchTime = -10f;
        private static readonly List<RaycastResult> UiHits = new List<RaycastResult>();

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
        }

        private void OnEnable() => EnhancedTouchSupport.Enable();
        private void OnDisable() => EnhancedTouchSupport.Disable();

        private void LateUpdate()
        {
            // Al cambiar de modo a mitad de gesto, la pulsación sería del botón equivocado: se cancela.
            if (ToolMode != wasToolMode)
            {
                wasToolMode = ToolMode;
                pressing = false;
                dragging = false;
            }

            if (Touch.activeTouches.Count > 0)
            {
                lastTouchTime = Time.unscaledTime;
                HandleTouch();
            }
            else if (Time.unscaledTime - lastTouchTime > 0.5f)
            {
                // En navegadores móviles el toque también genera eventos de ratón: los ignoramos un momento.
                HandleMouse();
            }
            ClampToBounds();
        }

        // ---------------- Ratón ----------------
        private void HandleMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pos = mouse.position.ReadValue();

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !IsOverUI(pos))
                ZoomAt(pos, cam.orthographicSize * (1f - Mathf.Sign(scroll) * wheelStep));

            if (ToolMode)
            {
                bool held = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
                bool pressed = mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame;
                if (!pressing && pressed && !IsOverUI(pos)) BeginPress(pos);
                else if (pressing && held) DragTo(pos);
                if (pressing && !held) EndPress(pos);
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame && !IsOverUI(pos)) BeginPress(pos);
            else if (pressing && mouse.leftButton.isPressed) DragTo(pos);
            if (pressing && mouse.leftButton.wasReleasedThisFrame) EndPress(pos);
        }

        // ---------------- Táctil ----------------
        private void HandleTouch()
        {
            var touches = Touch.activeTouches;

            if (touches.Count >= 2)
            {
                // Pellizco: zoom + desplazamiento con el punto medio. Cancela cualquier toque simple.
                Vector2 a = touches[0].screenPosition, b = touches[1].screenPosition;
                Vector2 mid = (a + b) * 0.5f;
                float dist = Vector2.Distance(a, b);
                pressing = false;
                dragging = false;

                if (!pinching)
                {
                    pinching = true;
                    lastPinchDist = dist;
                    grabWorld = ScreenToWorld(mid);
                    return;
                }
                if (lastPinchDist > 1f && dist > 1f)
                    cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * (lastPinchDist / dist), minZoom, MaxAllowedZoom());
                lastPinchDist = dist;
                transform.position += grabWorld - ScreenToWorld(mid);
                return;
            }

            pinching = false;
            if (ToolMode) return;   // un dedo es de la herramienta
            var t = touches[0];
            Vector2 pos = t.screenPosition;
            switch (t.phase)
            {
                case TouchPhase.Began:
                    if (!IsOverUI(pos)) BeginPress(pos);
                    break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (pressing) DragTo(pos);
                    break;
                case TouchPhase.Ended:
                    if (pressing) EndPress(pos);
                    break;
                case TouchPhase.Canceled:
                    pressing = false;
                    dragging = false;
                    break;
            }
        }

        // ---------------- Lógica común ----------------
        private void BeginPress(Vector2 screenPos)
        {
            pressing = true;
            dragging = false;
            pressScreenPos = screenPos;
            grabWorld = ScreenToWorld(screenPos);
        }

        private void DragTo(Vector2 screenPos)
        {
            if (!dragging && (screenPos - pressScreenPos).magnitude > dragThresholdPx) dragging = true;
            if (dragging)
                transform.position += grabWorld - ScreenToWorld(screenPos);
        }

        private void EndPress(Vector2 screenPos)
        {
            if (!dragging && !ToolMode)
            {
                Vector2 world = ScreenToWorld(screenPos);
                if (debugTaps) Debug.Log($"[Camera] Tap en {world}");
                Tapped?.Invoke(world);
            }
            pressing = false;
            dragging = false;
        }

        private void ZoomAt(Vector2 screenPos, float newSize)
        {
            Vector3 before = ScreenToWorld(screenPos);
            cam.orthographicSize = Mathf.Clamp(newSize, minZoom, MaxAllowedZoom());
            transform.position += before - ScreenToWorld(screenPos);
        }

        private float MaxAllowedZoom()
        {
            if (!TryGetBounds(out Bounds b)) return maxZoom;
            return Mathf.Max(minZoom, Mathf.Min(maxZoom, b.extents.y, b.extents.x / cam.aspect));
        }

        private void ClampToBounds()
        {
            if (!TryGetBounds(out Bounds b)) return;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, MaxAllowedZoom());
            float h = cam.orthographicSize, w = h * cam.aspect;
            Vector3 p = transform.position;
            p.x = b.size.x <= w * 2f ? b.center.x : Mathf.Clamp(p.x, b.min.x + w, b.max.x - w);
            p.y = b.size.y <= h * 2f ? b.center.y : Mathf.Clamp(p.y, b.min.y + h, b.max.y - h);
            transform.position = p;
        }

        // Con NestGrid se usan sus límites (se recalculan cada frame con el aspecto actual de la pantalla).
        private bool TryGetBounds(out Bounds b)
        {
            if (NestGrid.Instance != null) { b = NestGrid.Instance.WorldBounds; return true; }
            b = bounds != null ? bounds.bounds : default;
            return bounds != null;
        }

        private Vector3 ScreenToWorld(Vector2 screenPos)
        {
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
            w.z = 0f;
            return w;
        }

        private static bool IsOverUI(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = screenPos };
            UiHits.Clear();
            es.RaycastAll(data, UiHits);
            return UiHits.Count > 0;
        }
    }
}
