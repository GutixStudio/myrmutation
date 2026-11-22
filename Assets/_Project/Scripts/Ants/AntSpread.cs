using System.Collections.Generic;
using Myrmutation.Building;
using Myrmutation.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Myrmutation.Ants
{
    /// <summary>
    /// A5 · Evita que hormigas iguales se vean como una sola.
    /// Desplaza un poco el dibujo (hijo "Visual") de cada hormiga al azar y le da su propio orden
    /// de dibujado (SortingGroup), para que al cruzarse no se mezclen sus piezas.
    /// Ese desplazamiento solo toca el visual: la posición real, las rutas y la IA no cambian.
    /// Se ejecuta antes que AntActuator para que este guarde ya la posición desplazada como base.
    ///
    /// B8 · Reparto por el suelo de las salas (lo pide el cerebro, AntBrain):
    ///  · SpotIn(sala): sitio del suelo de la sala lo más lejos posible de las demás hormigas que ya están en ese suelo
    ///    o van hacia él. No reserva nada ni limita cuántas caben: con muchas se juntan más, pero siempre a lo ancho.
    ///    Las que ya están colocadas no se mueven cuando llega otra.
    ///  · StrollSpotIn(sala, alcance): sitio libre al azar a poca distancia (paseos de las obreras ociosas).
    ///  · Los sitios dejan 'doorMargin' con cada extremo de la huella (las puertas) y, en la Sala Real, un hueco
    ///    alrededor del centro para la reina.
    ///  · IsInDoorway: el punto está en una puerta o en su zona de acceso; el cerebro aparta de ahí a las paradas.
    /// El suelo de una sala es la fila de su WorkPosition (RoomSpawner lo pone en el centro del suelo) y su ancho
    /// sale de RoomData.footprint en casillas de la rejilla.
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

        [Header("Reparto en las salas (B8)")]
        [Tooltip("Distancia (u) entre los sitios de la sala y cada extremo de su huella: deja libres las puertas. " +
                 "Mayor que maxOffsetX para que tampoco el dibujo llegue a la puerta")]
        [SerializeField] private float doorMargin = 1f;
        [Tooltip("Hueco (u) a cada lado del centro de la Sala Real, donde está la reina")]
        [SerializeField] private float queenClearance = 0.75f;
        [Tooltip("Separación (u) a partir de la cual un sitio se considera libre para pasear")]
        [SerializeField] private float freeGap = 0.5f;

        private const float FallbackCellSize = 0.25f;
        private const float FloorTolerance = 0.4f;   // diferencia de altura con el suelo para contar como "en la sala"
        private const int AccessCells = 3;           // zona de acceso: 3 casillas fuera de cada puerta (NestGrid.DoorSize)

        private static int counter;
        private static readonly List<float> others = new List<float>();
        private static readonly List<float> freeSpots = new List<float>();

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

        // ================= REPARTO EN LAS SALAS (B8) =================

        /// <summary>Lado de una casilla de la rejilla (0,25 si no hay rejilla en la escena).</summary>
        public static float CellSize => NestGrid.Instance != null ? NestGrid.Instance.CellSize : FallbackCellSize;

        /// <summary>Medio ancho de la huella de la sala, en unidades (0 si no tiene datos).</summary>
        public static float HalfWidth(Room room) =>
            room != null && room.Data != null ? room.Data.footprint.x * CellSize * 0.5f : 0f;

        /// <summary>
        /// Sitio del suelo de la sala lo más separado de las demás hormigas; a igualdad, el más cercano a esta.
        /// Sin límite de hormigas. Si la sala es demasiado estrecha, su WorkPosition.
        /// </summary>
        public Vector3 SpotIn(Room room) => FindSpot(room, float.MaxValue, false);

        /// <summary>
        /// Sitio para pasear: a como mucho 'range' (u) en horizontal de donde está, al azar entre los libres
        /// (sin otra hormiga a menos de freeGap). Si no hay ninguno libre, el más separado.
        /// </summary>
        public Vector3 StrollSpotIn(Room room, float range) => FindSpot(room, range, true);

        /// <summary>Está en el suelo de la sala, en la parte en la que se reparten (lejos de las puertas).</summary>
        public bool IsInside(Room room, Vector3 pos)
        {
            if (room == null) return false;
            Vector3 c = room.WorkPosition;
            return Mathf.Abs(pos.y - c.y) <= FloorTolerance && Mathf.Abs(pos.x - c.x) <= HalfWidth(room) - doorMargin + 0.01f;
        }

        /// <summary>
        /// El punto está en una puerta de una sala terminada: dentro de la sala a menos de doorMargin de un extremo,
        /// o justo fuera, en su zona de acceso. Devuelve esa sala.
        /// </summary>
        public bool IsInDoorway(Vector3 pos, out Room room)
        {
            float access = AccessCells * CellSize;
            foreach (var r in Room.All)
            {
                if (r == null || !r.IsBuilt || r.Data == null) continue;
                Vector3 c = r.WorkPosition;
                if (Mathf.Abs(pos.y - c.y) > FloorTolerance) continue;
                float dx = Mathf.Abs(pos.x - c.x), half = HalfWidth(r);
                if (dx > half - doorMargin + 0.01f && dx < half + access) { room = r; return true; }
            }
            room = null;
            return false;
        }

        private Vector3 FindSpot(Room room, float range, bool randomAmongFree)
        {
            if (room == null) return transform.position;
            Vector3 center = room.WorkPosition;
            float fullHalf = HalfWidth(room);
            float half = fullHalf - doorMargin;
            if (half <= 0f) return center;

            float cs = CellSize;
            int steps = Mathf.FloorToInt(half / cs);
            bool royal = room.Data.type == RoomType.Royal;
            float nearX = transform.position.x;

            CollectOthers(center, fullHalf);
            freeSpots.Clear();
            bool found = false;
            float best = 0f, bestGap = -1f, bestDist = float.MaxValue;

            // Candidatos: una casilla tras otra (múltiplos de cs desde el centro, que es el centro de una casilla).
            for (int i = -steps; i <= steps; i++)
            {
                float dx = i * cs;
                if (royal && Mathf.Abs(dx) < queenClearance) continue;
                float dist = Mathf.Abs(center.x + dx - nearX);
                if (dist > range) continue;

                float gap = Gap(center.x + dx);
                if (gap >= freeGap) freeSpots.Add(dx);
                // Mejor: el más separado de las demás (por casillas, para que las diferencias mínimas no cuenten);
                // a igualdad, el más cercano a esta hormiga (camina menos).
                float g = Mathf.Min(Mathf.Round(gap / cs), 1000f);
                if (g > bestGap || (Mathf.Approximately(g, bestGap) && dist < bestDist))
                {
                    found = true; best = dx; bestGap = g; bestDist = dist;
                }
            }

            if (!found)
                return range < float.MaxValue ? FindSpot(room, float.MaxValue, randomAmongFree) : center;
            if (randomAmongFree && freeSpots.Count > 0) best = freeSpots[Random.Range(0, freeSpots.Count)];
            return new Vector3(center.x + best, center.y, center.z);
        }

        /// <summary>Posición en x de las demás hormigas que están en este suelo o van hacia él.</summary>
        private void CollectOthers(Vector3 center, float fullHalf)
        {
            others.Clear();
            foreach (var a in Ant.All)
            {
                if (a == null || !a.IsAlive || a.gameObject == gameObject) continue;
                Vector3 p = a.transform.position;
                // Las que van andando cuentan donde van a estar, no por donde pasan.
                if (a.TryGetComponent<AntActuator>(out var act) && act.Current == AntAction.Moving && act.HasDestination)
                    p = act.Destination;
                if (Mathf.Abs(p.y - center.y) > FloorTolerance || Mathf.Abs(p.x - center.x) > fullHalf) continue;
                others.Add(p.x);
            }
        }

        private static float Gap(float x)
        {
            float gap = float.MaxValue;
            foreach (float o in others) gap = Mathf.Min(gap, Mathf.Abs(o - x));
            return gap;
        }
    }
}
