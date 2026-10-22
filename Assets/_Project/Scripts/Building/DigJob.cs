using System;
using Myrmutation.Core;
using UnityEngine;

namespace Myrmutation.Building
{
    /// <summary>
    /// Trabajo de excavación de un bloque 3×3 de la rejilla. Lo crea el planificador y lo reparte IDigJobs.TryClaim.
    /// Admite hasta MaxWorkers obreras a la vez y el progreso se conserva aunque lo suelten.
    /// </summary>
    public class DigJob
    {
        /// <summary>Máximo de obreras excavando el mismo bloque a la vez.</summary>
        public const int MaxWorkers = 2;

        private readonly float cost;
        // Un hueco por obrera (MaxWorkers = 2). El == de Unity trata una hormiga destruida como null, así que su hueco queda libre.
        private Ant workerA;
        private Ant workerB;

        /// <summary>Bloque 3×3 que se excava (coordenadas de bloque, ver NestGrid.CellToChunk).</summary>
        public Vector2Int Chunk { get; }

        /// <summary>Punto del mundo donde se coloca la obrera para excavar.</summary>
        public Vector3 StandPosition { get; }

        /// <summary>True si la obrera debe mirar a la derecha mientras excava.</summary>
        public bool FacingRight { get; }

        /// <summary>Avance del bloque, de 0 (sin empezar) a 1 (terminado).</summary>
        public float Progress { get; private set; }

        /// <summary>True cuando Progress ha llegado a 1 o el planificador lo ha cancelado (se quedó sin plano). Las obreras lo sueltan igual.</summary>
        public bool IsDone => cancelled || Progress >= 1f;

        private bool cancelled;

        /// <summary>Lo llama solo el planificador cuando el bloque deja de tener casillas en plano (Erase, CancelRoom…).</summary>
        internal void Cancel() => cancelled = true;

        /// <summary>Obreras que tienen reservado ahora mismo este bloque (0..MaxWorkers).</summary>
        public int Workers => (workerA != null ? 1 : 0) + (workerB != null ? 1 : 0);

        /// <summary>True si el bloque admite otra obrera: no está terminado y hay hueco.</summary>
        public bool CanClaim => !IsDone && Workers < MaxWorkers;

        /// <param name="cost">Trabajo total para terminar el bloque, en fuerza × segundos. Debe ser mayor que 0.</param>
        public DigJob(Vector2Int chunk, Vector3 standPosition, bool facingRight, float cost)
        {
            if (cost <= 0f) throw new ArgumentOutOfRangeException(nameof(cost), cost, "El coste debe ser mayor que 0.");
            Chunk = chunk;
            StandPosition = standPosition;
            FacingRight = facingRight;
            this.cost = cost;
        }

        /// <summary>
        /// Reserva el bloque para 'ant' si CanClaim. Devuelve si lo ha conseguido.
        /// Falla si 'ant' es null o ya lo tenía reservado.
        /// Lo llama solo IDigJobs.TryClaim; B6 no debe usarlo.
        /// </summary>
        public bool Claim(Ant ant)
        {
            if (ant == null || !CanClaim || HasWorker(ant)) return false;
            if (workerA == null) workerA = ant;
            else workerB = ant;
            return true;
        }

        internal bool HasWorker(Ant ant) => ant != null && (workerA == ant || workerB == ant);

        /// <summary>
        /// La llama cada obrera en cada frame que excava, con su fuerza y Time.deltaTime.
        /// Avanza Progress en strength × deltaTime / coste, sin pasar de 1. Las llamadas se suman:
        /// dos obreras en el mismo frame avanzan el doble de rápido. No hace nada si el bloque ya está terminado.
        /// </summary>
        public void Work(float strength, float deltaTime)
        {
            if (IsDone) return;
            Progress = Mathf.Min(1f, Progress + strength * deltaTime / cost);
        }

        /// <summary>
        /// Libera la reserva de 'ant' y conserva Progress para quien retome el bloque. Si 'ant' no lo tenía, no hace nada.
        /// B6 la llama con su propia hormiga al interrumpir, morir o cambiar de tarea; llamarla de más es seguro.
        /// </summary>
        public void Release(Ant ant)
        {
            if (ant == null) return;
            if (workerA == ant) workerA = null;
            else if (workerB == ant) workerB = null;
        }
    }
}
