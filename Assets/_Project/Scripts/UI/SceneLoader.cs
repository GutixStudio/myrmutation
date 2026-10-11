using UnityEngine.SceneManagement;

namespace Myrmutation.UI
{
    /// <summary>Nombres de escena en un solo sitio. Deben coincidir con los de Build Profiles → Scene List.</summary>
    public static class SceneLoader
    {
        public const string MainMenu = "MainMenu";
        public const string Credits = "Credits";
        public const string Game = "Game";
        public const string GameOver = "GameOver";

        public static void Load(string sceneName) => SceneManager.LoadScene(sceneName);
    }
}
