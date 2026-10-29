using UnityEditor;
using UnityEngine;

namespace Hushwater.EditorTools
{
    // Poly Haven textures arrive as plain images; tell Unity which ones are normal and data maps.
    public sealed class HushwaterAssetImport : AssetPostprocessor
    {
        // Marks normal maps as normal maps and keeps data maps (roughness, metal) linear.
        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Hushwater/")) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            if (file.Contains("_nor_gl") || file.Contains("_nor_"))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
            else if (file.Contains("_rough") || file.Contains("_metal") || file.Contains("_arm") || file.Contains("_ao"))
            {
                importer.sRGBTexture = false;
            }
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
        }

        // Models come in as static meshes: no animation, cameras or lights.
        private void OnPreprocessModel()
        {
            if (!assetPath.Contains("/Resources/Hushwater/")) return;
            ModelImporter importer = (ModelImporter)assetImporter;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }
    }
}
