using System;

namespace Myrmutation.Ants
{
    /// <summary>Qué persigue la hormiga ahora mismo (nivel alto de la máquina de estados).</summary>
    public enum BrainGoal { Routine, Eat, Rest }

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

        /// <summary>Decide ya, sin esperar al siguiente ciclo.</summary>
        void Think();

        /// <summary>Cambió de objetivo o de paso.</summary>
        event Action<BrainGoal, BrainStep> StateChanged;
    }
}
