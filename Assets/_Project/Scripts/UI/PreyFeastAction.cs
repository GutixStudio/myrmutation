using Myrmutation.Ants;
using Myrmutation.Core;
using Myrmutation.Expedition;
using Myrmutation.Genetics;
using UnityEngine;

namespace Myrmutation.UI
{
    /// <summary>
    /// A5 · Botón "Comer presa" en el panel de hormiga.
    /// Une D4 (presas guardadas por la expedición) con C3 (FeastSystem.DevourPrey): sin esto, en la
    /// partida no había forma de comerse las presas y los genes de la alfa (Patas de corredora,
    /// Mandíbula cortadora) no se podían conseguir.
    /// Va en Managers, junto a FeastSystem y ExpeditionSystem.
    /// </summary>
    public class PreyFeastAction : MonoBehaviour
    {
        public const string Label = "Comer presa";

        private void OnEnable()  => AntPanelAction.Register(Label, CanEat, Eat);
        private void OnDisable() => AntPanelAction.Unregister(Label);

        // La regla de la Cámara del Festín es la de FeastSystem (Require Feast Room): un solo sitio.
        private bool CanEat(Ant ant)
        {
            var exp = ExpeditionSystem.Instance;
            var feast = FeastSystem.Instance;
            return ant != null && ant.IsAlive && !ant.IsQueen
                && exp != null && exp.HasPrey
                && feast != null && feast.FeastAvailable;
        }

        private void Eat(Ant ant)
        {
            if (!CanEat(ant)) return;
            if (!ExpeditionSystem.Instance.TryTakePrey(out var prey)) return;
            // Si el Festín no se puede hacer, la presa vuelve al almacén en vez de perderse.
            if (!FeastSystem.Instance.DevourPrey(ant, prey != null ? prey.gene : null))
                ExpeditionSystem.Instance.AddPrey(prey);
        }
    }
}
