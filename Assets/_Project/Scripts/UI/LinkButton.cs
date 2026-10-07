using UnityEngine;
using UnityEngine.UI;

namespace Myrmutation.UI
{
    /// <summary>Abre una URL (redes, itch.io, GitHub) al pulsar el botón. Funciona en WebGL.</summary>
    [RequireComponent(typeof(Button))]
    public class LinkButton : MonoBehaviour
    {
        [SerializeField] private string url = "https://gutixstudio.itch.io";

        private void Awake() => GetComponent<Button>().onClick.AddListener(() => Application.OpenURL(url));
    }
}
