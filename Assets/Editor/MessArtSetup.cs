using System.IO;
using System.Linq;
using NatureCalls.Facility;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NatureCalls.Editor
{
    /// <summary>
    /// Imports the Blender mess models (scripts/blender/mess_models.py) and builds their URP materials and the
    /// MessArt catalog that Facility/MessLook and HardStool load at runtime. Re-running keeps every GUID.
    /// </summary>
    public static class MessArtSetup
    {
        public const string Folder = "Assets/Art/Mess";
        public const string MaterialFolder = Folder + "/Materials";
        public const string CatalogPath = Folder + "/Resources/MessArt.asset";

        [MenuItem("NatureCalls/Mess Art")]
        public static void Apply()
        {
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(Folder + "/Resources");
            AssetDatabase.Refresh();
            foreach (var path in Directory.GetFiles(Folder, "*.fbx")) ConfigureModel(path.Replace('\\', '/'));
            foreach (var path in Directory.GetFiles(Folder, "*.png")) ConfigureSprite(path.Replace('\\', '/'));

            var art = AssetDatabase.LoadAssetAtPath<MessArt>(CatalogPath);
            if (art == null) { art = ScriptableObject.CreateInstance<MessArt>(); AssetDatabase.CreateAsset(art, CatalogPath); }
            art.SoftSwirl = Mesh("StoolSoftSwirl"); art.SoftSquashed = Mesh("StoolSoftSquashed");
            art.HardLog = Mesh("StoolHardLog"); art.HardPellets = Mesh("StoolHardPellets");
            art.LooseSplat = Mesh("StoolLooseSplat"); art.Vomit = Mesh("Vomit");
            art.UrinePuddle = Mesh("UrinePuddle"); art.ToiletSmear = Mesh("ToiletSmear");

            // Cartoon, saturated and oily: high smoothness on wet things, lower on the dry hard stool.
            art.Stool = Lit("Stool", new Color(.50f, .25f, .07f), .86f);
            art.StoolDry = Lit("StoolDry", new Color(.33f, .17f, .05f), .32f);
            art.Loose = Lit("Loose", new Color(.70f, .47f, .12f), .93f);
            art.Bubble = Lit("Bubble", new Color(.90f, .70f, .30f), .97f);
            art.VomitBase = Lit("Vomit", new Color(.74f, .80f, .22f), .9f);
            art.VomitChunk = Lit("VomitChunk", new Color(.95f, .62f, .22f), .6f);
            art.Urine = Lit("Urine", new Color(1f, .87f, .12f, .62f), .97f, transparent: true);
            art.Smear = Lit("Smear", new Color(.40f, .21f, .05f), .8f);
            art.Backflow = Lit("Backflow", new Color(.36f, .24f, .10f), .95f);
            art.BackflowBubble = Lit("BackflowBubble", new Color(.55f, .45f, .22f), .97f);
            art.Stink = Particle("Stink", "StinkSprite");
            art.Sparkle = Particle("Sparkle", "ComfortSparkle", additive: true);
            art.Splash = Particle("Splash", "DropSprite");
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();

            foreach (var mesh in new[] { art.SoftSwirl, art.SoftSquashed, art.HardLog, art.HardPellets, art.LooseSplat, art.Vomit, art.UrinePuddle, art.ToiletSmear })
                Debug.Log(mesh == null ? "MESS_ART_MISSING" : $"MESS_ART_MESH|{mesh.name}|tris={mesh.triangles.Length / 3}|submeshes={mesh.subMeshCount}|bounds={mesh.bounds.center:F3}/{mesh.bounds.size:F3}");
            Debug.Log("MESS_ART_OK|" + CatalogPath);
        }

        private static void ConfigureModel(string path)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer) return;
            bool changed = importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation ||
                           importer.animationType != ModelImporterAnimationType.None || !importer.bakeAxisConversion ||
                           importer.importCameras || importer.importLights || importer.importBlendShapes;
            if (!changed) return;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.bakeAxisConversion = true;
            importer.importCameras = false; importer.importLights = false; importer.importBlendShapes = false;
            importer.SaveAndReimport();
        }

        private static void ConfigureSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            if (importer.alphaIsTransparency && importer.wrapMode == TextureWrapMode.Clamp) return;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        private static Mesh Mesh(string model) =>
            AssetDatabase.LoadAllAssetsAtPath($"{Folder}/{model}.fbx").OfType<Mesh>().FirstOrDefault();

        private static Material Load(string name, string shaderName)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(shaderName);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            else if (material.shader != shader) material.shader = shader;
            return material;
        }

        private static Material Lit(string name, Color color, float smoothness, bool transparent = false)
        {
            var material = Load(name, "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_SpecularHighlights", 1);
            material.SetFloat("_EnvironmentReflections", 1);
            Surface(material, transparent, false);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Particle(string name, string texture, bool additive = false)
        {
            var material = Load(name, "Universal Render Pipeline/Particles/Unlit");
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{texture}.png"));
            material.SetColor("_BaseColor", Color.white);
            Surface(material, true, additive);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Surface(Material material, bool transparent, bool additive)
        {
            material.SetFloat("_Surface", transparent ? 1 : 0);
            material.SetFloat("_Blend", additive ? 2 : 0);
            material.SetFloat("_ZWrite", transparent ? 0 : 1);
            material.SetFloat("_SrcBlend", (float)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
            material.SetFloat("_DstBlend", (float)(transparent ? (additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha) : BlendMode.Zero));
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            if (transparent) material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); else material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = transparent ? (int)RenderQueue.Transparent : -1;
            material.SetShaderPassEnabled("ShadowCaster", !transparent);
        }
    }
}
