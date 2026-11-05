using System;

namespace Myrmutation.Ants
{
    /// <summary>
    /// Qué persigue la hormiga ahora mismo (nivel alto de la máquina de estados).
    /// Dig (B6): obrera sin sala excavando los planos de construcción.
    /// Idle (B8): obrera sin sala y sin planos: espera paseando por la Sala Real (o junto a la entrada).
    /// Los nuevos van al final para no mover los valores anteriores.
    /// </summary>
    public enum BrainGoal { Routine, Eat, Rest, Dig, Idle }

    /// <summary>En qué punto de ese objetivo está (nivel bajo).</summary>
    public enum BrainStep { None, Moving, Acting, Waiting }

    /// <summary>
    /// Capa de DECISIÓN de la IA (GDD §11). Intercambiable: cualquier cerebro que cumpla esto
    /// puede sustituir a AntBrain sin tocar el sensor ni el actuador.
    /// </summary>
    public interface IAntBrain
    {
        BrainGoal Goal { get; }
        BrainStep Step { get; }

        /// <summary>"Objetivo/Paso", para depurar y para la UI.</summary>
        string StateName { get; }

        /// <summary>
        /// Suspendido: el cerebro no da órdenes. Para que otro sistema tome el control un rato
        /// (expedición, Festín…). Al reanudar vuelve a decidir desde cero.
        /// </summary>
        bool IsSuspended { get; }
        void Suspend();
        void Resume();

        /// <summary>
        /// El último intento de ir a algún sitio falló por falta de camino.
        /// Se quita en cuanto vuelve a encontrar ruta (o se queda sin sitio al que ir). Lo usa AntStatusBubble (B7).
        /// </summary>
        bool HasNoPath { get; }

        /// <summary>Decide ya, sin esperar al siguiente ciclo.</summary>
        void Think();

        /// <summary>Cambió de objetivo o de paso.</summary>
        event Action<BrainGoal, BrainStep> StateChanged;
    }
}
