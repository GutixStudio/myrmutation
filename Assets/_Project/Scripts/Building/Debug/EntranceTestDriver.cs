using System.Collections;
using Myrmutation.Ants;
using Myrmutation.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Myrmutation.Building
{
    /// <summary>
    /// Prueba de A11 (escena Test_A11). Tecla T o "Lanzar prueba" en el menú del componente: una obrera va de la Sala Real a la
    /// salida izquierda por el cráter, desaparece, vuelve por ella y regresa a la Sala Real; luego lo mismo por la derecha.
    /// Le suspende el cerebro mientras dura la prueba, como deberá hacer D7.
    /// </summary>
    public class EntranceTestDriver : MonoBehaviour
    {
        [Tooltip("Hormiga que hace la prueba. Vacío: la primera de Ant.All que no sea la reina")]
        [SerializeField] private Ant ant;
        [Tooltip("Casilla de la Sala Real de la que sale y a la que vuelve (su fila de nodos en el mapa inicial)")]
        [SerializeField] private Vector2Int homeCell = new Vector2Int(77, 40);
        [Tooltip("Segundos fuera antes de volver")]
        [SerializeField] private float awayTime = 2f;

        private bool running;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tKey.wasPressedThisFrame) Launch();
        }

        [ContextMenu("Lanzar prueba")]
        private void Launch()
        {
            if (running || NestEntrance.Instance == null || NestGrid.Instance == null) return;
            if (ant == null)
                foreach (var a in Ant.All)
                    if (a.IsAlive && !a.IsQueen) { ant = a; break; }
            if (ant != null) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            running = true;
            var entrance = NestEntrance.Instance;
            var actuator = ant.GetComponent<AntActuator>();
            ant.TryGetComponent<IAntBrain>(out var brain);
            brain?.Suspend();
            Vector3 home = NestGrid.Instance.CellToWorld(homeCell);

            foreach (var side in new[] { NestEntrance.Side.Left, NestEntrance.Side.Right })
            {
                yield return WalkTo(actuator, home);
                bool gone = false;
                if (!entrance.LeaveVia(ant, side, () => gone = true))
                {
                    Debug.LogWarning($"[EntranceTestDriver] {ant.Name}: no hay camino hasta la salida {side}", this);
                    break;
                }
                while (!gone && ant != null && ant.IsAlive) yield return null;
                yield return new WaitForSeconds(awayTime);
                entrance.ArriveVia(ant, side);
            }
            yield return WalkTo(actuator, home);
            brain?.Resume();
            running = false;
        }

        private static IEnumerator WalkTo(AntActuator actuator, Vector3 target)
        {
            if (!actuator.MoveTo(target)) yield break;
            while (actuator != null && actuator.Current == AntAction.Moving) yield return null;
        }
    }
}
