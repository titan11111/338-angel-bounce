using UnityEditor;
using UnityEngine;

public sealed class AngelArtImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Art/")) return;
        var t = (TextureImporter)assetImporter;
        t.textureType = TextureImporterType.Default;
        t.alphaIsTransparency = !assetPath.EndsWith("Canyon.png");
        t.isReadable = true; t.mipmapEnabled = false;
        t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp;
        t.textureCompression = TextureImporterCompression.Uncompressed;
        t.maxTextureSize = 2048;
    }
    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Resources/Audio/") || !assetPath.EndsWith(".mp3")) return;
        var a = (AudioImporter)assetImporter;
        var settings = a.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = .7f;
        settings.preloadAudioData = false;
        a.defaultSampleSettings = settings;
    }
}
