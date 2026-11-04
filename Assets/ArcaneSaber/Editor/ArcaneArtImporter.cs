using UnityEditor;
using UnityEngine;

namespace ArcaneSaber.Editor
{
    // Import settings for drop-in art in Resources/Art, so a dragged-in PNG or FBX just works.
    public sealed class ArcaneArtImporter : AssetPostprocessor
    {
        const string Folder="Assets/ArcaneSaber/Resources/Art/";
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith(Folder)) return;
            var importer=(TextureImporter)assetImporter;
            string name=System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool tiled=name=="Floor" || name=="NoteFace";
            importer.textureType=TextureImporterType.Default; importer.sRGBTexture=true;
            importer.alphaIsTransparency=name=="Logo"; importer.mipmapEnabled=name!="Logo";
            importer.wrapMode=tiled?TextureWrapMode.Repeat:TextureWrapMode.Clamp;
            importer.maxTextureSize=name=="Background"?4096:2048;
            importer.anisoLevel=tiled?8:1;
        }
        // Hit sounds are trimmed at load, which needs readable, decoded samples.
        void OnPreprocessAudio()
        {
            if(!assetPath.StartsWith("Assets/ArcaneSaber/Resources/Sounds/")) return;
            var importer=(AudioImporter)assetImporter; var settings=importer.defaultSampleSettings;
            settings.loadType=AudioClipLoadType.DecompressOnLoad; settings.preloadAudioData=true;
            importer.defaultSampleSettings=settings; importer.loadInBackground=false;
        }
        void OnPreprocessModel()
        {
            if(!assetPath.StartsWith(Folder)) return;
            var importer=(ModelImporter)assetImporter;
            importer.importAnimation=false; importer.importCameras=false; importer.importLights=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        }
    }
}
