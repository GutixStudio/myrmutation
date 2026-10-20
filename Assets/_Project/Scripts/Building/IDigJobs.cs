using Myrmutation.Core;

namespace Myrmutation.Building
{
    /// <summary>
    /// Contrato de los trabajos de excavación. La IA (B6) SOLO usa esto, nunca el planificador directamente.
    /// Flujo de una obrera: TryClaim → ir a job.StandPosition → job.Work cada frame → al terminar o interrumpir, job.Release(ant).
    /// </summary>
    public interface IDigJobs
    {
        /// <summary>True si queda algún bloque que se pueda reservar ahora mismo.</summary>
        bool HasWork { get; }

        /// <summary>
        /// Reserva para 'ant' el bloque trabajable más cercano (le suma una obrera).
        /// False si no hay ninguno o si la hormiga no conecta con la colonia (atrapada en tierra o en una sala sin salida):
        /// entonces no reserva nada y 'job' es null.
        /// </summary>
        bool TryClaim(Ant ant, out DigJob job);
    }

    /// <summary>Acceso global al gestor de trabajos de excavación activo de la escena.</summary>
    public static class Digging
    {
        public static IDigJobs Current { get; set; }
    }
}
