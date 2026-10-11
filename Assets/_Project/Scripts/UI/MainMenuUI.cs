using UnityEngine;
using UnityEngine.UI;

namespace Myrmutation.UI
{
    /// <summary>Menú principal: Nueva partida y Contactar (requisitos del enunciado).</summary>
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button contactButton;

        private void Awake()
        {
            Time.timeScale = 1f;
            if (newGameButton != null) newGameButton.onClick.AddListener(() =>
            {
                RunSummary.QuitByPlayer = false;
                SceneLoader.Load(SceneLoader.Game);
            });
            if (contactButton != null) contactButton.onClick.AddListener(() => SceneLoader.Load(SceneLoader.Credits));
        }
    }
}
