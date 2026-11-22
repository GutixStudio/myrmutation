using Myrmutation.Core;

namespace Myrmutation.Building
{
    /// <summary>
    /// Contrato de los trabajos de excavación. La IA (B6) SOLO usa esto, nunca el planificador directamente.
    /// Flujo de una obrera: TryClaim → ir a job.StandPosition → job.Work(ant, fuerza, Time.deltaTime) cada frame → al
    /// terminar o interrumpir, job.Release(ant). Usar Work(ant, …), no Work sin hormiga: así el gestor sabe que excava y
    /// nadie le quita el bloque. Mientras va de camino, comprobar job.HasWorker(ant): si es false, otra más cercana se
    /// quedó su reserva; tratarlo como IsDone (soltar y volver a pedir).
    /// Al terminar, conviene llamar a TryClaim en ese mismo frame: devuelve ya el bloque contiguo del mismo plano si el
    /// gestor se lo tiene reservado (excavación continua), sin pasar por el reparto.
    /// </summary>
    public interface IDigJobs
    {
        /// <summary>True si queda algún bloque que se pueda reservar ahora mismo.</summary>
        bool HasWork { get; }

        /// <summary>
        /// Pide trabajo para 'ant'. El gestor reparte los bloques entre las obreras que piden y aquí reserva el que le ha
        /// tocado (le suma una obrera); si ya tiene uno sin terminar, devuelve ese. False si aún no le ha tocado nada (hay
        /// que volver a pedir en los frames siguientes) o si no conecta con la colonia (atrapada en tierra o en una sala sin
        /// salida): entonces no reserva nada y 'job' es null.
        /// </summary>
        bool TryClaim(Ant ant, out DigJob job);
    }

    /// <summary>Acceso global al gestor de trabajos de excavación activo de la escena.</summary>
    public static class Digging
    {
        public static IDigJobs Current { get; set; }
    }
}
