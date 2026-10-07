using Myrmutation.Core;

namespace Myrmutation.UI
{
    /// <summary>Datos de la última partida para mostrarlos en la pantalla de Game Over.</summary>
    public static class RunSummary
    {
        public static GameState Result = GameState.GameOver;
        public static bool QuitByPlayer;
        public static int Day;
        public static Season Season;
        public static int AntsAlive;
    }
}
