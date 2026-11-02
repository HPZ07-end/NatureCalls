using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NatureCalls.Editor
{
    public static class WCLayoutBoundsInspector
    {
        private static readonly string[] Paths =
        {
            "Assets/WC/Prefabs/props/SM_toilet_bowl Variant.prefab",
            "Assets/WC/Prefabs/props/SM_toilet_cubicle Variant.prefab",
            "Assets/WC/Prefabs/props/SM_urinal Variant.prefab",
            "Assets/WC/Prefabs/props/SM_sink Variant.prefab",
            "Assets/WC/Prefabs/props/SM_mirror.prefab",
            "Assets/WC/Prefabs/props/SM_toilet_paper Variant.prefab",
            "Assets/UnityTechnologies/Basic Asset Pack Interior/Prefabs/Furniture/ShelfWallSmall.prefab",
            "Assets/UnityTechnologies/Basic Asset Pack Interior/Prefabs/Furniture/ShelvesMediumA.prefab",
            "Assets/UnityTechnologies/Basic Asset Pack Interior/Prefabs/Walls/Wall2m.prefab",
            "Assets/Stylized NPC - Peasant Nolant/Prefabs/Peasant Nolant Blue(Free Version).prefab"
        };

        [MenuItem("NatureCalls/Log WC Layout Prefab Bounds %#l")]
        public static void LogBounds()
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var path in Paths)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) throw new InvalidOperationException($"Missing prefab: {path}");
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0)
                    {
                        Debug.Log($"WC_BOUNDS|{path}|NO_RENDERERS");
                        continue;
                    }
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    Debug.Log($"WC_BOUNDS|{path}|size={Format(bounds.size)}|center={Format(bounds.center)}");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static string Format(Vector3 value) => $"({value.x:F3},{value.y:F3},{value.z:F3})";
    }
}
