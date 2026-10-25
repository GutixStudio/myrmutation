using System;
using UnityEngine;

namespace Myrmutation.Building
{
    /// <summary>
    /// Decoración de la superficie (§6): árboles y matorrales sobre la hierba. Solo se dibuja, no afecta a la navegación.
    /// Cada elemento lleva su arte (F11: tree_1…3, bush_1…2, grass_tuft_1) o, sin él, un rectángulo de color (placeholder).
    /// </summary>
    public class SurfaceDecor : MonoBehaviour
    {
        [Serializable]
        public struct Item
        {
            [Tooltip("Arte del elemento: se estira al ancho de 'rect' manteniendo la proporción, con la base en rect.y. Vacío: rectángulo de color")]
            public Sprite sprite;
            [Tooltip("En casillas: x desde la izquierda del mapa, y sobre la hierba (0 = apoyado en ella), ancho y alto")]
            public Rect rect;
            [Tooltip("Color del rectángulo cuando no hay arte")]
            public Color color;
        }

        [Tooltip("Rejilla del hormiguero. Si se deja vacío, usa NestGrid.Instance")]
        [SerializeField] private NestGrid nest;
        [Tooltip("Orden de dibujo: encima del fondo y del montículo, debajo del Tilemap y de las hormigas")]
        [SerializeField] private int sortingOrder = -8;
        [SerializeField] private Item[] items =
        {
            new Item { rect = new Rect(19.5f, 0f, 1f, 6f), color = new Color(0.4f, 0.26f, 0.13f) },    // árbol 1: tronco
            new Item { rect = new Rect(17f, 5f, 6f, 5f), color = new Color(0.25f, 0.55f, 0.2f) },      // árbol 1: copa
            new Item { rect = new Rect(37.5f, 0f, 1f, 7f), color = new Color(0.4f, 0.26f, 0.13f) },    // árbol 2: tronco
            new Item { rect = new Rect(34.5f, 6f, 7f, 6f), color = new Color(0.22f, 0.5f, 0.18f) },    // árbol 2: copa
            new Item { rect = new Rect(95.5f, 0f, 1f, 5f), color = new Color(0.4f, 0.26f, 0.13f) },    // árbol 3: tronco
            new Item { rect = new Rect(93.5f, 4f, 5f, 4f), color = new Color(0.28f, 0.58f, 0.22f) },   // árbol 3: copa
            new Item { rect = new Rect(43f, 0f, 4f, 2f), color = new Color(0.2f, 0.45f, 0.18f) },      // matorral 1
            new Item { rect = new Rect(78f, 0f, 5f, 2f), color = new Color(0.2f, 0.45f, 0.18f) },      // matorral 2
        };

        private Sprite whiteSprite;

        private void Start()
        {
            if (nest == null) nest = NestGrid.Instance;
            float cs = nest.CellSize;
            Vector3 ground = nest.transform.position + Vector3.up * (nest.TerrainRows * cs);
            foreach (var item in items)
            {
                var go = new GameObject(item.sprite != null ? item.sprite.name : "Decoración");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = sortingOrder;
                Vector3 basePoint = ground + new Vector3(item.rect.center.x, item.rect.y, 0f) * cs;
                if (item.sprite != null)
                {
                    sr.sprite = item.sprite;
                    Bounds b = item.sprite.bounds;
                    float scale = item.rect.width * cs / b.size.x;
                    go.transform.localScale = new Vector3(scale, scale, 1f);
                    go.transform.position = basePoint - new Vector3(b.center.x, b.min.y, 0f) * scale;
                }
                else
                {
                    if (whiteSprite == null) whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0f), 4f);
                    sr.sprite = whiteSprite;
                    sr.color = item.color;
                    go.transform.localScale = new Vector3(item.rect.width * cs, item.rect.height * cs, 1f);
                    go.transform.position = basePoint;
                }
            }
        }

        private void OnDestroy()
        {
            if (whiteSprite != null) Destroy(whiteSprite);
        }
    }
}
