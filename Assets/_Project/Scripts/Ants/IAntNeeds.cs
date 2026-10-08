namespace Myrmutation.Ants
{
    /// <summary>
    /// Contrato de necesidades que lee AntSensor.
    /// Lo implementará AntNeeds (hambre y energía). Mientras tanto, para pruebas: AntTestNeeds.
    /// </summary>
    public interface IAntNeeds
    {
        /// <summary>0 = saciada · 1 = muerta de hambre.</summary>
        float Hunger01 { get; }

        /// <summary>1 = descansada · 0 = agotada.</summary>
        float Energy01 { get; }
    }
}
