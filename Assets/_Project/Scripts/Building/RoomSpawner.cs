using System.Collections;
using System.Collections.Generic;
using Myrmutation.Ants;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using UnityEngine;

namespace Myrmutation.Building
{
    /// <summary>
    /// Pone las salas de la beta (diseno-construccion-beta.md §3, "Al terminar una sala"): al arrancar, las del mapa
    /// inicial (NestGrid.StartingRooms) ya terminadas; al recibir RoomDug, el prefab de la sala excavada con un fundido.
    /// Cada prefab lleva su Room, que se registra sola en Room.All (como en la alfa), y aquí se llama a CompleteBuild().
    /// La Sala Real, la Guardería y la Despensa iniciales no cuentan para el +25 % del coste (eso lo lleva DigPlanner).
    /// RoomAt dice qué sala hay bajo un punto (por su huella): lo usan la selección de hormigas y las pruebas de B.
    /// </summary>
    public class RoomSpawner : MonoBehaviour
    {
        public static RoomSpawner Instance { get; private set; }

        [Tooltip("Rejilla del hormiguero. Si se deja vacío, usa NestGrid.Instance")]
        [SerializeField] private NestGrid grid;
        [Tooltip("Navegador de la rejilla. Si se deja vacío, usa Navigation.Current")]
        [SerializeField] private GridNavigator navigator;
        [Tooltip("Segundos (sin escala) del fundido de entrada de una sala recién excavada")]
        [SerializeField] private float fadeDuration = 0.6f;

        private readonly HashSet<RoomData> warnedNoPrefab = new HashSet<RoomData>();
        private readonly List<(Room room, RectInt rect)> placed = new List<(Room, RectInt)>();

        private void OnEnable()
        {
            Instance = this;
            EventBus.Subscribe<RoomDug>(OnRoomDug);
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            EventBus.Unsubscribe<RoomDug>(OnRoomDug);
        }

        /// <summary>Sala cuya huella contiene el punto, o null.</summary>
        public static Room RoomAt(Vector2 world)
        {
            var s = Instance;
            if (s == null || !s.Resolve()) return null;
            Vector2Int cell = s.grid.WorldToCell(world);
            foreach (var (room, rect) in s.placed)
                if (room != null && rect.Contains(cell)) return room;
            return null;
        }

        private void Start()
        {
            if (!Resolve()) return;
            foreach (var s in grid.StartingRooms)
                if (s.data != null) Spawn(s.data, s.origin, false);
        }

        private void OnRoomDug(RoomDug e)
        {
            if (!Resolve() || e.Room == null) return;
            Spawn(e.Room, e.Origin, true);
            SettleAnts();
        }

        /// <summary>Crea la sala terminada con su esquina inferior izquierda en la casilla 'origin'.</summary>
        public Room Spawn(RoomData data, Vector2Int origin, bool fadeIn)
        {
            float cs = grid.CellSize;
            Vector3 corner = grid.CellToWorld(origin) - new Vector3(cs, cs, 0f) * 0.5f;
            // Las rutas acaban en la fila de nodos de la sala (la 2.ª desde abajo), un poco bajadas: ahí andan y trabajan.
            Vector3 floor = navigator.GroundPoint(new Vector2Int(origin.x + data.footprint.x / 2, origin.y + 1));

            Room room;
            if (data.prefab != null)
            {
                var go = Instantiate(data.prefab, corner, Quaternion.identity);
                go.name = data.prefab.name;
                room = go.GetComponent<Room>();
                if (room == null) room = go.AddComponent<Room>();
                Transform work = go.transform.Find("WorkPoint");
                if (work != null) work.position = floor;
                if (fadeIn) StartCoroutine(FadeIn(go.GetComponentsInChildren<SpriteRenderer>()));
            }
            else
            {
                if (warnedNoPrefab.Add(data))
                    Debug.LogWarning($"[RoomSpawner] {data.name} no tiene prefab: se crea la sala sin arte.", data);
                room = new GameObject("Room_" + data.type).AddComponent<Room>();
                room.transform.position = floor;   // sin WorkPoint, WorkPosition es la posición de la sala
            }
            room.Data = data;
            room.CompleteBuild();
            placed.Add((room, DigPlanner.RoomRect(data, origin)));
            return room;
        }

        private IEnumerator FadeIn(SpriteRenderer[] renderers)
        {
            var alphas = new float[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) alphas[i] = renderers[i].color.a;
            for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
            {
                SetAlpha(renderers, alphas, t / fadeDuration);
                yield return null;
            }
            SetAlpha(renderers, alphas, 1f);
        }

        private static void SetAlpha(SpriteRenderer[] renderers, float[] alphas, float k)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = renderers[i].color;
                c.a = alphas[i] * k;
                renderers[i].color = c;
            }
        }

        /// <summary>
        /// La huella acaba de pasar a Room y dentro de una sala solo se anda por la fila de nodos: las hormigas que estaban
        /// dentro a otra altura (excavando) bajan al suelo ya, sin esperar a la recolocación de GridNavigator, y las rutas
        /// que la cruzaban ya no valen. Si el destino ha quedado dentro de la sala fuera del suelo (el sitio de un bloque
        /// de la sala, ya terminado), no se recalcula hacia allí: se para y la IA (o DigTestDriver) ve IsDone y pide otro.
        /// shortcut: recalcula todas las hormigas en marcha (la ruta de AntActuator es privada); filtrar por la huella si se nota.
        /// </summary>
        private void SettleAnts()
        {
            foreach (var ant in Ant.All)
            {
                if (ant == null) continue;
                if (navigator.OffRoomFloor(ant.transform.position, out var floor)) ant.transform.position = floor;
                if (!ant.TryGetComponent<AntActuator>(out var actuator) || actuator.Current != AntAction.Moving) continue;
                if (navigator.OffRoomFloor(actuator.Destination, out _)) actuator.Stop();
                else actuator.MoveTo(actuator.Destination);
            }
        }

        private bool Resolve()
        {
            if (grid == null) grid = NestGrid.Instance;
            if (navigator == null) navigator = Navigation.Current as GridNavigator;
            return grid != null && navigator != null;
        }
    }
}
