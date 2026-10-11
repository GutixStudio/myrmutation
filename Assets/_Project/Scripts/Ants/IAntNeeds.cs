namespace Myrmutation.Ants
{
    /// <summary>
    /// Contrato de necesidades que lee AntSensor. Lo implementa AntNeeds.
    /// Los umbrales (cuándo tiene hambre, cuándo está cansada…) los decide quien lo implementa.
    /// </summary>
    public interface IAntNeeds
    {
        /// <summary>0 = saciada · 1 = famélica.</summary>
        float Hunger01 { get; }

        /// <summary>1 = llena · 0 = agotada.</summary>
        float Energy01 { get; }

        /// <summary>Tiene hambre: el cerebro la manda a la Despensa.</summary>
        bool IsHungry { get; }

        /// <summary>Está cansada: el cerebro la manda a descansar.</summary>
        bool IsTired { get; }

        /// <summary>Ha descansado lo suficiente: el cerebro la devuelve al trabajo.</summary>
        bool IsRested { get; }

        /// <summary>Llegó a energía 0 y aún no se ha recuperado del todo.</summary>
        bool IsExhausted { get; }

        /// <summary>Puede trabajar (false mientras está agotada).</summary>
        bool CanWork { get; }

        /// <summary>Está descansando ahora mismo.</summary>
        bool IsResting { get; }
    }
}
