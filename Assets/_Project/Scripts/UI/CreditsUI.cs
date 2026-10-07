using UnityEngine;
using UnityEngine.UI;

namespace Myrmutation.UI
{
    /// <summary>Créditos / Contacto con botón para volver al menú (requisito del enunciado).</summary>
    public class CreditsUI : MonoBehaviour
    {
        [SerializeField] private Button backButton;

        private void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(() => SceneLoader.Load(SceneLoader.MainMenu));
        }
    }
}
