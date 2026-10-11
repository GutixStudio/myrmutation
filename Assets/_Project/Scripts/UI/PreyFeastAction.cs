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

        [Tooltip("Igual que 'Require Feast Room' de FeastSystem: si está activo, solo con la Cámara del Festín construida")]
        [SerializeField] private bool requireFeastRoom = false;

        private void OnEnable()  => AntPanelAction.Register(Label, CanEat, Eat);
        private void OnDisable() => AntPanelAction.Unregister(Label);

        private bool CanEat(Ant ant)
        {
            var exp = ExpeditionSystem.Instance;
            return ant != null && ant.IsAlive && !ant.IsQueen
                && exp != null && exp.HasPrey
                && FeastSystem.Instance != null
                && (!requireFeastRoom || Room.FindBuilt(RoomType.Feast) != null);
        }

        private void Eat(Ant ant)
        {
            if (!CanEat(ant)) return;
            if (!ExpeditionSystem.Instance.TryTakePrey(out var prey)) return;
            FeastSystem.Instance.DevourPrey(ant, prey != null ? prey.gene : null);
        }
    }
}
