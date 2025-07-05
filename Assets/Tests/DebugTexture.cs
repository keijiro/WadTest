using System.IO;
using UnityEngine;
using WadImporter;
using NUnit.Framework;
using UnityEditor;

namespace Tests
{
    [TestFixture]
    public class DebugTexture
    {
        [Test]
        public void DebugTextureLoading()
        {
        var wadPath = "Assets/Tests/Resources/DOOM1.WAD";
        
        // Force import to see what happens
        AssetDatabase.ImportAsset(wadPath, ImportAssetOptions.ForceUpdate);
        
        // Try to find any created assets
        var assets = AssetDatabase.FindAssets("DOOM1");
        Debug.Log($"Found {assets.Length} assets with DOOM1 in name");
        foreach (var guid in assets)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            Debug.Log($"Asset: {assetPath}");
        }
        
        var wadData = File.ReadAllBytes(wadPath);
        var wadReader = new WadReader(wadData);
        
        // Debug palette loading
        var playpalData = wadReader.GetLumpData("PLAYPAL");
        if (playpalData != null)
        {
            Debug.Log($"PLAYPAL found: {playpalData.Length} bytes");
            Debug.Log($"First few color values: R={playpalData[0]}, G={playpalData[1]}, B={playpalData[2]}");
        }
        else
        {
            Debug.LogError("PLAYPAL not found!");
        }
        
        // Debug PNAMES loading
        var pnamesData = wadReader.GetLumpData("PNAMES");
        if (pnamesData != null)
        {
            Debug.Log($"PNAMES found: {pnamesData.Length} bytes");
            using (var stream = new MemoryStream(pnamesData))
            using (var reader = new BinaryReader(stream))
            {
                var count = reader.ReadInt32();
                Debug.Log($"PNAMES count: {count}");
                if (count > 0)
                {
                    var firstPatchName = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8)).TrimEnd('\0');
                    Debug.Log($"First patch name: '{firstPatchName}'");
                }
            }
        }
        else
        {
            Debug.LogError("PNAMES not found!");
        }
        
        // Debug TEXTURE1 loading
        var texture1Data = wadReader.GetLumpData("TEXTURE1");
        if (texture1Data != null)
        {
            Debug.Log($"TEXTURE1 found: {texture1Data.Length} bytes");
            using (var stream = new MemoryStream(texture1Data))
            using (var reader = new BinaryReader(stream))
            {
                var count = reader.ReadInt32();
                Debug.Log($"TEXTURE1 count: {count}");
                if (count > 0)
                {
                    var firstOffset = reader.ReadInt32();
                    Debug.Log($"First texture offset: {firstOffset}");
                }
            }
        }
        else
        {
            Debug.LogError("TEXTURE1 not found!");
        }
        
        // Try to build textures
        var textureBuilder = new TextureBuilder(wadReader);
        var textures = textureBuilder.BuildTextures();
        
        Debug.Log($"Built {textures.Count} textures");
        
        foreach (var kvp in textures)
        {
            var texture = kvp.Value;
            Debug.Log($"Texture '{kvp.Key}': {texture.width}x{texture.height}, format: {texture.format}");
            
            // Check a few pixels to see if they're magenta
            var pixels = texture.GetPixels();
            var magentaCount = 0;
            for (int i = 0; i < Mathf.Min(100, pixels.Length); i++)
            {
                if (pixels[i] == Color.magenta)
                    magentaCount++;
            }
            Debug.Log($"  Magenta pixels in first 100: {magentaCount}");
            
            if (magentaCount > 50)
            {
                Debug.LogWarning($"Texture '{kvp.Key}' has mostly magenta pixels - texture loading may have failed");
            }
        }
    }
}
}