using System;
using System.Collections;
using System.Collections.Generic;
using Myrmutation.Ants;
using Myrmutation.Core;
using UnityEngine;

namespace Myrmutation.Building
{
    /// <summary>
    /// Entrada del hormiguero (diseno-construccion-beta.md §6): el cráter del montículo y las dos salidas de la superficie,
    /// en los extremos de la línea por la que se anda. La cámara ve todo el ancho, así que al irse y al volver por una salida
    /// las hormigas hacen un fundido. Para las expediciones (D7) y, más adelante, los eventos que llegan por la entrada.
    /// </summary>
    public class NestEntrance : MonoBehaviour
    {
        public enum Side { Left, Right }

        public static NestEntrance Instance { get; private set; }

        [Tooltip("Rejilla del hormiguero. Si se deja vacío, usa NestGrid.Instance")]
        [SerializeField] private NestGrid grid;
        [Tooltip("Navegador. Si se deja vacío, usa Navigation.Current")]
        [SerializeField] private GridNavigator navigator;
        [Tooltip("Segundos del fundido al irse y al volver (tiempo de juego)")]
        [SerializeField] private float fadeTime = 0.6f;

        /// <summary>El cráter: GroundPoint del nodo de la superficie en la columna central del montículo.</summary>
        public Vector3 Position => Point(Grid.MoundCenterX);

        /// <summary>Extremo izquierdo de la superficie por el que se anda.</summary>
        public Vector3 ExitLeft => Point(1);

        /// <summary>Extremo derecho de la superficie por el que se anda.</summary>
        public Vector3 ExitRight => Point(Grid.Width - 2);

        public Vector3 Exit(Side side) => side == Side.Left ? ExitLeft : ExitRight;

        private NestGrid Grid => grid != null ? grid : grid = NestGrid.Instance;

        private Vector3 Point(int x)
        {
            if (navigator == null) navigator = Navigation.Current as GridNavigator;
            return navigator.GroundPoint(navigator.SurfaceNode(x));
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Reset()
        {
            grid = GetComponent<NestGrid>();
            navigator = GetComponent<GridNavigator>();
        }

        /// <summary>
        /// La hormiga va andando hasta la salida de ese lado; al llegar se desvanece, se desactiva (sale de Ant.All) y se llama a onGone.
        /// Quien llame debe suspender antes su cerebro (IAntBrain.Suspend) y reanudarlo al volver: si no, el cerebro puede llevársela
        /// a otro sitio. Si no llega (otra orden, muere), no se llama a onGone. False si no hay camino (la hormiga no se mueve).
        /// </summary>
        public bool LeaveVia(Ant ant, Side side, Action onGone)
        {
            if (!ant.TryGetComponent<AntActuator>(out var actuator) || !actuator.MoveTo(Exit(side))) return false;
            StartCoroutine(Leave(ant, actuator, onGone));
            return true;
        }

        /// <summary>
        /// La hormiga aparece en la salida de ese lado (se activa: vuelve a Ant.All) con un fundido; luego quien llame la manda
        /// adonde quiera, p. ej. MoveTo(Position). Su cerebro debe seguir suspendido hasta entonces (ver LeaveVia).
        /// </summary>
        public void ArriveVia(Ant ant, Side side)
        {
            Vector3 p = Exit(side);
            p.z = ant.transform.position.z;
            ant.transform.position = p;
            var renderers = new List<SpriteRenderer>();
            ant.GetComponentsInChildren(true, renderers);
            SetAlpha(renderers, 0f);
            ant.gameObject.SetActive(true);
            StartCoroutine(Fade(ant, renderers, 0f, 1f));
        }

        private IEnumerator Leave(Ant ant, AntActuator actuator, Action onGone)
        {
            Vector3 exit = actuator.Destination;
            while (ant != null && ant.IsAlive && actuator.Current == AntAction.Moving && actuator.Destination == exit) yield return null;
            float near = Grid.CellSize;
            if (ant == null || !ant.IsAlive || (ant.transform.position - exit).sqrMagnitude > near * near) yield break;

            var renderers = new List<SpriteRenderer>();
            ant.GetComponentsInChildren(true, renderers);
            yield return Fade(ant, renderers, 1f, 0f);
            if (ant == null) yield break;
            ant.gameObject.SetActive(false);
            SetAlpha(renderers, 1f);   // por si otro sistema la vuelve a activar sin ArriveVia
            onGone?.Invoke();
        }

        private IEnumerator Fade(Ant ant, List<SpriteRenderer> renderers, float from, float to)
        {
            for (float t = 0f; t < fadeTime; t += Time.deltaTime)
            {
                if (ant == null) yield break;
                SetAlpha(renderers, Mathf.Lerp(from, to, t / fadeTime));
                yield return null;
            }
            SetAlpha(renderers, to);
        }

        private static void SetAlpha(List<SpriteRenderer> renderers, float alpha)
        {
            foreach (var r in renderers)
            {
                if (r == null) continue;
                Color c = r.color;
                c.a = alpha;
                r.color = c;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || Grid == null || Navigation.Current == null) return;
            float r = Grid.CellSize;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(Position, r);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(ExitLeft, r);
            Gizmos.DrawWireSphere(ExitRight, r);
        }
    }
}
