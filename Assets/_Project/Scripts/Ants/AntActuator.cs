using System;
using System.Collections.Generic;
using Myrmutation.Building;
using Myrmutation.Core;
using UnityEngine;

namespace Myrmutation.Ants
{
    public enum AntAction { Idle, Moving, Working, Eating }

    /// <summary>
    /// Capa de ACTUACIÓN de la IA (GDD §11): ejecuta órdenes, no decide nada.
    /// El cerebro (AntBrain) decide qué hacer y llama a estos métodos; AntSensor le cuenta cómo está el mundo.
    ///
    /// Órdenes (tarjeta B1):  MoverA → MoveTo / MoveAlong · Parar → Stop · Trabajar → Work · Comer → Eat
    ///
    /// Usa Time.deltaTime, así que respeta la pausa y las velocidades x1/x2/x3 del GameManager.
    /// </summary>
    [RequireComponent(typeof(Ant))]
    public class AntActuator : MonoBehaviour
    {
        [Header("Movimiento")]
        [Tooltip("Distancia a la que se considera que ha llegado a cada punto de la ruta")]
        [SerializeField] private float arriveDistance = 0.05f;
        [Tooltip("Velocidad si la hormiga no tiene casta asignada (Stats.speed = 0)")]
        [SerializeField] private float fallbackSpeed = 1.5f;

        [Header("Comer")]
        [Tooltip("Segundos que tarda en comer una ración")]
        [SerializeField] private float eatDuration = 1.5f;
        [Tooltip("Raciones de comida que gasta Eat() si no se indica otra cantidad")]
        [SerializeField] private int defaultRation = 1;

        [Header("Visual")]
        [Tooltip("Hijo Visual del prefab: se voltea y gira según la dirección y se balancea al trabajar/comer")]
        [SerializeField] private Transform visual;
        [SerializeField] private float bobAmplitude = 0.04f;
        [SerializeField] private float bobFrequency = 10f;

        [Header("Orientación")]
        [Tooltip("Gira el cuerpo para ir paralela al túnel (en cuestas y túneles verticales)")]
        [SerializeField] private bool alignToPath = true;
        [Tooltip("Grados por segundo al girar (más alto = giro más brusco)")]
        [SerializeField] private float turnSpeed = 540f;
        [Tooltip("Al pararse vuelve a ponerse horizontal (suelo de las salas)")]
        [SerializeField] private bool levelWhenStopped = true;

        /// <summary>Acción en curso.</summary>
        public AntAction Current { get; private set; } = AntAction.Idle;
        public bool IsIdle => Current == AntAction.Idle;

        /// <summary>Último destino pedido con MoveTo/MoveAlong (lo usa AntSensor.IsAtTarget).</summary>
        public Vector3 Destination { get; private set; }
        public bool HasDestination { get; private set; }

        /// <summary>Sala en la que está trabajando (null si no trabaja o trabaja "en el sitio").</summary>
        public Room WorkRoom { get; private set; }

        /// <summary>Mirando a la derecha (true) o a la izquierda (false).</summary>
        public bool FacingRight { get; private set; } = true;

        /// <summary>Velocidad real: la de sus stats o, si no tiene casta, la de reserva.</summary>
        public float Speed => ant != null && ant.Stats.speed > 0f ? ant.Stats.speed : fallbackSpeed;

        /// <summary>La acción ha terminado sola (ha llegado, ha acabado de trabajar o de comer). Stop() NO lo lanza.</summary>
        public event Action<AntAction> ActionFinished;

        /// <summary>Ha terminado de comer. Parámetro: raciones consumidas (0 = no había comida).</summary>
        public event Action<int> Ate;

        private Ant ant;
        private readonly List<Vector3> path = new List<Vector3>();
        private int pathIndex;
        private float actionTimer;      // < 0 = sin límite (trabajar hasta que la paren)
        private int pendingRation;
        private Vector3 visualBasePos;
        private float bobTime;
        private float targetAngle;      // giro (grados) que busca el Visual

        private void Awake()
        {
            ant = GetComponent<Ant>();
            if (visual != null) visualBasePos = visual.localPosition;
        }

        // ================= ÓRDENES =================

        /// <summary>MoverA(destino): calcula la ruta por los túneles y va hasta allí. False si no hay camino.</summary>
        public bool MoveTo(Vector3 destination)
        {
            var route = new List<Vector3>();
            if (Navigation.Current != null)
            {
                if (!Navigation.Current.TryGetPath(transform.position, destination, route))
                {
                    Debug.LogWarning($"[AntActuator] {ant.Name}: no hay camino hasta {destination}", this);
                    return false;
                }
            }
            else
            {
                // Sin navegador en la escena (p. ej. una prueba sin túneles): línea recta.
                route.Add(destination);
            }
            return MoveAlong(route);
        }

        /// <summary>MoverA(ruta): recorre los puntos en orden. El último es el destino.</summary>
        public bool MoveAlong(IList<Vector3> route)
        {
            if (route == null || route.Count == 0) return false;

            EndCurrentAction();
            path.Clear();
            float z = transform.position.z;
            foreach (var p in route) path.Add(new Vector3(p.x, p.y, z));
            pathIndex = 0;

            Destination = path[path.Count - 1];
            HasDestination = true;
            Current = AntAction.Moving;
            return true;
        }

        /// <summary>Parar: cancela lo que esté haciendo y se queda quieta. No lanza ActionFinished.</summary>
        public void Stop()
        {
            EndCurrentAction();
            path.Clear();
            Current = AntAction.Idle;
        }

        /// <summary>
        /// Trabajar en una sala (o en el sitio si room es null).
        /// duration &lt;= 0: trabaja hasta que se le dé otra orden.
        /// Qué produce el trabajo lo decide cada sistema (construcción, hongos…) mirando
        /// Current == Working y WorkRoom.
        /// </summary>
        public void Work(Room room = null, float duration = -1f)
        {
            EndCurrentAction();
            path.Clear();
            WorkRoom = room;
            actionTimer = duration > 0f ? duration : -1f;
            Current = AntAction.Working;
        }

        /// <summary>
        /// Comer: tarda eatDuration y al terminar gasta comida de la colonia (ResourceManager)
        /// y lanza Ate(raciones). ration &lt;= 0 usa defaultRation.
        /// </summary>
        public void Eat(int ration = 0)
        {
            EndCurrentAction();
            path.Clear();
            pendingRation = ration > 0 ? ration : defaultRation;
            actionTimer = eatDuration;
            Current = AntAction.Eating;
        }

        // ================= BUCLE =================

        private void Update()
        {
            switch (Current)
            {
                case AntAction.Moving: UpdateMove(); break;
                case AntAction.Working: UpdateWork(); break;
                case AntAction.Eating: UpdateEat(); break;
            }
            UpdateRotation();
        }

        private void UpdateMove()
        {
            Vector3 target = path[pathIndex];
            Vector3 pos = transform.position;
            Face(target - pos);

            transform.position = Vector3.MoveTowards(pos, target, Speed * Time.deltaTime);

            if ((transform.position - target).sqrMagnitude > arriveDistance * arriveDistance) return;

            transform.position = target;
            pathIndex++;
            if (pathIndex < path.Count) return;

            path.Clear();
            Finish(AntAction.Moving);
        }

        private void UpdateWork()
        {
            Bob(1f);
            if (actionTimer < 0f) return;   // sin límite
            actionTimer -= Time.deltaTime;
            if (actionTimer <= 0f) Finish(AntAction.Working);
        }

        private void UpdateEat()
        {
            Bob(0.5f);
            actionTimer -= Time.deltaTime;
            if (actionTimer > 0f) return;

            int eaten = 0;
            var resources = ResourceManager.Instance;
            if (resources == null)
                Debug.LogWarning("[AntActuator] No hay ResourceManager en la escena: no se puede gastar comida.", this);
            else if (resources.TrySpend(ResourceType.Food, pendingRation))
                eaten = pendingRation;

            Finish(AntAction.Eating);
            Ate?.Invoke(eaten);
        }

        // ================= AUXILIARES =================

        private void Finish(AntAction finished)
        {
            EndCurrentAction();
            Current = AntAction.Idle;
            ActionFinished?.Invoke(finished);
        }

        /// <summary>Limpia el estado de la acción actual (sin cambiar Current).</summary>
        private void EndCurrentAction()
        {
            WorkRoom = null;
            pendingRation = 0;
            actionTimer = 0f;
            bobTime = 0f;
            if (visual != null) visual.localPosition = visualBasePos;
        }

        /// <summary>
        /// Orienta a la hormiga hacia donde va: la voltea a izquierda/derecha y fija el giro
        /// para que el cuerpo vaya paralelo al tramo de túnel (las paredes son paralelas al tramo).
        /// En tramos verticales mantiene el lado al que miraba.
        /// </summary>
        private void Face(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.000001f) return;

            if (Mathf.Abs(dir.x) > 0.001f)
            {
                bool right = dir.x > 0f;
                if (right != FacingRight)
                {
                    FacingRight = right;
                    if (visual != null)
                    {
                        var s = visual.localScale;
                        s.x = Mathf.Abs(s.x) * (right ? 1f : -1f);
                        visual.localScale = s;
                        // Al voltear, el mismo giro apuntaría al revés: se refleja para que no dé un salto.
                        visual.localRotation = Quaternion.Euler(0f, 0f, -visual.localEulerAngles.z);
                    }
                }
            }

            if (!alignToPath) { targetAngle = 0f; return; }
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;   // 0 = derecha, 90 = arriba
            // Volteada (mirando a la izquierda) su "delante" es -X: se resta media vuelta.
            targetAngle = Mathf.DeltaAngle(0f, FacingRight ? angle : angle - 180f);
        }

        /// <summary>Gira el Visual poco a poco hacia targetAngle (horizontal si está parada).</summary>
        private void UpdateRotation()
        {
            if (visual == null) return;
            if (Current != AntAction.Moving && levelWhenStopped) targetAngle = 0f;

            float z = visual.localEulerAngles.z;
            float next = Mathf.MoveTowardsAngle(z, targetAngle, turnSpeed * Time.deltaTime);
            if (Mathf.Approximately(Mathf.DeltaAngle(z, next), 0f)) return;
            visual.localRotation = Quaternion.Euler(0f, 0f, next);
        }

        private void Bob(float speedFactor)
        {
            if (visual == null) return;
            bobTime += Time.deltaTime * bobFrequency * speedFactor;
            visual.localPosition = visualBasePos + Vector3.up * (Mathf.Abs(Mathf.Sin(bobTime)) * bobAmplitude);
        }

        private void OnDrawGizmosSelected()
        {
            if (path.Count == 0) return;
            Gizmos.color = Color.yellow;
            Vector3 prev = transform.position;
            for (int i = pathIndex; i < path.Count; i++)
            {
                Gizmos.DrawLine(prev, path[i]);
                prev = path[i];
            }
            Gizmos.DrawWireSphere(Destination, 0.15f);
        }
    }
}
