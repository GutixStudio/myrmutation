#if UNITY_EDITOR
using Myrmutation.Core;
using Myrmutation.Core.Data;
using UnityEditor;
using UnityEngine;

namespace Myrmutation.Building.EditorTools
{
    /// <summary>
    /// Menú Myrmutation → Salas → Crear prefabs de salas (A10). Por cada RoomData de Data/Rooms crea o rehace
    /// Prefabs/Rooms/&lt;nombre&gt;.prefab y lo asigna en RoomData.prefab. Pivote en la esquina inferior izquierda de la huella:
    /// - Art: RoomData.sprite (640×400) estirado a la huella; fuera de su óvalo se ve la tierra (§3). Placeholder hasta F10
    /// - WorkPoint: centro del suelo, a la altura de GroundPoint de la fila de nodos (RoomSpawner lo recoloca exacto)
    /// - DoorLeft / DoorRight: centro de las 3 filas inferiores de la primera y la última columna
    /// También en batchmode: -executeMethod Myrmutation.Building.EditorTools.RoomPrefabsMenu.CreatePrefabs
    /// </summary>
    public static class RoomPrefabsMenu
    {
        private const string DataFolder = "Assets/_Project/Data/Rooms";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Rooms";
        private const string Tag = "[A10]";

        // Valores por defecto de NestGrid.cellSize y GridNavigator.lowerAmount: solo para colocar el WorkPoint del prefab.
        private const float CellSize = 0.25f;
        private const float LowerAmount = 0.2f;
        // Por debajo de las hormigas (10+) y por encima del Tilemap del hormiguero (0).
        private const int ArtOrder = 3;

        [MenuItem("Myrmutation/Salas/Crear prefabs de salas")]
        public static void CreatePrefabs()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Rooms");

            foreach (string guid in AssetDatabase.FindAssets("t:RoomData", new[] { DataFolder }))
            {
                var data = AssetDatabase.LoadAssetAtPath<RoomData>(AssetDatabase.GUIDToAssetPath(guid));
                var root = Build(data);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{data.name}.prefab");
                Object.DestroyImmediate(root);

                data.prefab = prefab;
                EditorUtility.SetDirty(data);
                Debug.Log($"{Tag} {prefab.name}: {data.footprint.x}×{data.footprint.y} casillas");
            }
            AssetDatabase.SaveAssets();
        }

        private static GameObject Build(RoomData data)
        {
            var root = new GameObject(data.name);
            if (data.sprite != null) Stretched(root, "Art", data.sprite, (Vector2)data.footprint * CellSize, ArtOrder);

            var work = Marker(root, "WorkPoint", new Vector2((data.footprint.x / 2 + 0.5f) * CellSize, 1.5f * CellSize - LowerAmount));
            Marker(root, "DoorLeft", new Vector2(0.5f, 1.5f) * CellSize);
            Marker(root, "DoorRight", new Vector2(data.footprint.x - 0.5f, 1.5f) * CellSize);

            var room = root.AddComponent<Room>();
            var so = new SerializedObject(room);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("workPoint").objectReferenceValue = work;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // Sprite con su esquina inferior izquierda en el pivote del prefab y escalado a 'size' unidades.
        private static void Stretched(GameObject root, string name, Sprite sprite, Vector2 size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            Bounds b = sprite.bounds;
            var scale = new Vector3(size.x / b.size.x, size.y / b.size.y, 1f);
            go.transform.localScale = scale;
            go.transform.localPosition = new Vector3(-b.min.x * scale.x, -b.min.y * scale.y, 0f);
        }

        private static Transform Marker(GameObject root, string name, Vector2 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root.transform, false);
            t.localPosition = local;
            return t;
        }
    }
}
#endif
