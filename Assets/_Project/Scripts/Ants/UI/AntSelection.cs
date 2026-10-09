using System;
using Myrmutation.Building;
using Myrmutation.Core;
using UnityEngine;

namespace Myrmutation.Ants
{
    /// <summary>
    /// Selección de hormigas (B4). Una por escena.
    ///
    ///  · Tocar / clic cerca de una hormiga → la selecciona y publica AntSelected(hormiga).
    ///  · Tocar en otro sitio → deselecciona y publica AntSelected(null).   ← null = "ninguna"
    ///  · Modo "elegir sala": mientras está activo (lo abre el panel al pulsar Asignar), tocar
    ///    una sala del mapa la elige en vez de cambiar la selección.
    ///
    /// No usa colliders en las hormigas: busca la más cercana al punto tocado. Así no bloquea
    /// los toques sobre las zonas de construcción (BuildZone.At usa el primer collider).
    /// Los toques sobre la UI ya los ignora CameraController.
    /// </summary>
    public class AntSelection : MonoBehaviour
    {
        public static AntSelection Instance { get; private set; }

        /// <summary>Hormiga seleccionada (null si ninguna).</summary>
        public static Ant Current { get; private set; }

        [Tooltip("Distancia máxima (en unidades del mundo) entre el toque y la hormiga para seleccionarla")]
        [SerializeField] private float pickRadius = 0.6f;

        [Tooltip("Opcional: objeto que sigue a la hormiga seleccionada (p. ej. un círculo bajo ella)")]
        [SerializeField] private Transform selectionMarker;

        private Action<Room> roomPickCallback;
        private Action roomPickCancelled;

        /// <summary>Está esperando a que el jugador toque una sala del mapa.</summary>
        public bool IsPickingRoom => roomPickCallback != null;

        // ================= CICLO =================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (selectionMarker != null) selectionMarker.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            Current = null;
        }

        private void OnEnable()
        {
            CameraController.Tapped += OnTap;
            EventBus.Subscribe<AntDied>(OnAntDied);
        }

        private void OnDisable()
        {
            CameraController.Tapped -= OnTap;
            EventBus.Unsubscribe<AntDied>(OnAntDied);
        }

        private void LateUpdate()
        {
            if (selectionMarker == null) return;
            bool show = Current != null;
            if (selectionMarker.gameObject.activeSelf != show) selectionMarker.gameObject.SetActive(show);
            if (show) selectionMarker.position = Current.transform.position;
        }

        // ================= API =================

        public void Select(Ant ant)
        {
            if (ant != null && !ant.IsAlive) ant = null;
            if (Current == ant) return;
            CancelRoomPick();
            Current = ant;
            EventBus.Publish(new AntSelected(ant));   // null = deseleccionada
        }

        public void Deselect() => Select(null);

        /// <summary>
        /// Empieza el modo "elegir sala en el mapa": el siguiente toque sobre una sala la elige.
        /// Tocar fuera de una sala cancela el modo (onCancelled) sin deseleccionar.
        /// </summary>
        public void BeginRoomPick(Action<Room> onPicked, Action onCancelled = null)
        {
            roomPickCallback = onPicked;
            roomPickCancelled = onCancelled;
        }

        public void CancelRoomPick()
        {
            if (roomPickCallback == null) return;
            var cancelled = roomPickCancelled;
            roomPickCallback = null;
            roomPickCancelled = null;
            cancelled?.Invoke();
        }

        /// <summary>Hormiga viva más cercana a un punto del mundo, dentro de pickRadius (o null).</summary>
        public Ant FindAntAt(Vector2 world)
        {
            Ant best = null;
            float bestD = pickRadius * pickRadius;
            foreach (var a in Ant.All)
            {
                if (a == null || !a.IsAlive) continue;
                float d = ((Vector2)a.transform.position - world).sqrMagnitude;
                if (d <= bestD) { bestD = d; best = a; }
            }
            return best;
        }

        // ================= ENTRADA =================

        private void OnTap(Vector2 world)
        {
            if (IsPickingRoom)
            {
                var zone = BuildZone.At(world);
                if (zone != null && zone.Room != null)
                {
                    var picked = roomPickCallback;
                    roomPickCallback = null;
                    roomPickCancelled = null;
                    picked(zone.Room);
                }
                else CancelRoomPick();
                return;
            }

            Select(FindAntAt(world));
        }

        private void OnAntDied(AntDied e)
        {
            if (e.Ant == Current) Deselect();
        }
    }
}
