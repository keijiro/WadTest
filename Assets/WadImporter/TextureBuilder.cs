using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WadImporter
{
    public class TextureBuilder
    {
        readonly WadReader wadReader;
        readonly Color[] palette;
        readonly string[] pnames;

        public TextureBuilder(WadReader wadReader)
        {
            this.wadReader = wadReader;
            palette = LoadPalette();
            pnames = LoadPnames();
        }

        public Dictionary<string, Texture2D> BuildTextures()
        {
            var textures = new Dictionary<string, Texture2D>();
            
            Debug.Log($"[TextureBuilder] Starting texture build process");
            Debug.Log($"[TextureBuilder] Palette loaded: {palette?.Length} colors");
            Debug.Log($"[TextureBuilder] PNAMES loaded: {pnames?.Length} entries");
            
            var texture1 = LoadTextureDirectory("TEXTURE1");
            var texture2 = LoadTextureDirectory("TEXTURE2");
            
            Debug.Log($"[TextureBuilder] TEXTURE1 contains {texture1.Length} textures");
            Debug.Log($"[TextureBuilder] TEXTURE2 contains {texture2.Length} textures");
            
            foreach (var tex in texture1)
            {
                var builtTexture = BuildTexture(tex);
                if (builtTexture != null && !string.IsNullOrEmpty(tex.name))
                    textures[tex.name] = builtTexture;
            }
            
            foreach (var tex in texture2)
            {
                var builtTexture = BuildTexture(tex);
                if (builtTexture != null && !string.IsNullOrEmpty(tex.name))
                    textures[tex.name] = builtTexture;
            }
            
            Debug.Log($"[TextureBuilder] Successfully built {textures.Count} textures");
            return textures;
        }

        public Texture2D BuildFlatTexture(string name)
        {
            var flatData = wadReader.GetLumpData(name);
            if (flatData == null)
            {
                Debug.LogWarning($"[TextureBuilder] Flat texture data not found: {name}");
                return CreateDefaultTexture(64, 64);
            }
            
            if (flatData.Length != 64 * 64)
            {
                Debug.LogWarning($"[TextureBuilder] Flat texture {name} has invalid size: {flatData.Length} bytes (expected 4096)");
                return CreateDefaultTexture(64, 64);
            }

            Debug.Log($"[TextureBuilder] Building flat texture: {name}");
            var texture = new Texture2D(64, 64, TextureFormat.RGB24, false);
            var colors = new Color[64 * 64];

            for (var i = 0; i < flatData.Length; i++)
            {
                var paletteIndex = flatData[i];
                if (paletteIndex < palette.Length)
                    colors[i] = palette[paletteIndex];
                else
                {
                    Debug.LogWarning($"[TextureBuilder] Invalid palette index {paletteIndex} in flat {name}");
                    colors[i] = Color.magenta;
                }
            }

            texture.SetPixels(colors);
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            
            return texture;
        }

        Texture2D BuildTexture(WadTexture wadTexture)
        {
            if (wadTexture.width <= 0 || wadTexture.height <= 0)
            {
                Debug.LogWarning($"[TextureBuilder] Invalid texture dimensions: {wadTexture.name} (width={wadTexture.width}, height={wadTexture.height})");
                return CreateDefaultTexture(64, 64);
            }

            Debug.Log($"[TextureBuilder] Building texture: {wadTexture.name} ({wadTexture.width}x{wadTexture.height}) with {wadTexture.patches.Length} patches");

            var texture = new Texture2D(wadTexture.width, wadTexture.height, TextureFormat.RGBA32, false);
            var colors = new Color[wadTexture.width * wadTexture.height];
            
            for (var i = 0; i < colors.Length; i++)
                colors[i] = Color.clear;

            var patchesApplied = 0;
            foreach (var patch in wadTexture.patches)
            {
                if (patch.patch >= pnames.Length)
                {
                    Debug.LogWarning($"[TextureBuilder] Patch index {patch.patch} out of range (max: {pnames.Length - 1}) for texture {wadTexture.name}");
                    continue;
                }

                var patchName = pnames[patch.patch];
                var patchData = wadReader.GetLumpData(patchName);
                if (patchData == null)
                {
                    Debug.LogWarning($"[TextureBuilder] Patch data not found: {patchName} for texture {wadTexture.name}");
                    continue;
                }

                Debug.Log($"[TextureBuilder] Applying patch {patchName} ({patchData.Length} bytes) at ({patch.originX}, {patch.originY})");
                ApplyPatch(colors, wadTexture.width, wadTexture.height, patchData, patch.originX, patch.originY);
                patchesApplied++;
            }

            if (patchesApplied == 0)
            {
                Debug.LogWarning($"[TextureBuilder] No patches applied for texture {wadTexture.name} - using default texture");
                return CreateDefaultTexture(wadTexture.width, wadTexture.height);
            }

            texture.SetPixels(colors);
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            
            Debug.Log($"[TextureBuilder] Successfully built texture: {wadTexture.name} with {patchesApplied} patches");
            return texture;
        }

        void ApplyPatch(Color[] targetColors, int textureWidth, int textureHeight, byte[] patchData, int offsetX, int offsetY)
        {
            using (var stream = new MemoryStream(patchData))
            using (var reader = new BinaryReader(stream))
            {
                var header = WadPatchHeader.Read(reader);

                for (var x = 0; x < header.width; x++)
                {
                    var targetX = offsetX + x;
                    if (targetX < 0 || targetX >= textureWidth)
                        continue;

                    stream.Seek(header.columnOffsets[x], SeekOrigin.Begin);

                    while (true)
                    {
                        var topDelta = reader.ReadByte();
                        if (topDelta == 0xFF)
                            break;

                        var length = reader.ReadByte();
                        reader.ReadByte(); // dummy byte

                        for (var y = 0; y < length; y++)
                        {
                            var paletteIndex = reader.ReadByte();
                            var targetY = offsetY + topDelta + y;
                            
                            if (targetY >= 0 && targetY < textureHeight)
                            {
                                var pixelIndex = targetY * textureWidth + targetX;
                                if (pixelIndex >= 0 && pixelIndex < targetColors.Length)
                                {
                                    if (paletteIndex >= 0 && paletteIndex < palette.Length)
                                        targetColors[pixelIndex] = palette[paletteIndex];
                                    else
                                        Debug.LogWarning($"[TextureBuilder] Invalid palette index {paletteIndex} (max: {palette.Length - 1})");
                                }
                            }
                        }

                        reader.ReadByte(); // dummy byte
                    }
                }
            }
        }

        Color[] LoadPalette()
        {
            var playpalData = wadReader.GetLumpData("PLAYPAL");
            if (playpalData == null)
            {
                Debug.LogError("[TextureBuilder] PLAYPAL lump not found - using default palette");
                return CreateDefaultPalette();
            }
            
            if (playpalData.Length < 768)
            {
                Debug.LogError($"[TextureBuilder] PLAYPAL lump too small ({playpalData.Length} bytes, expected 768) - using default palette");
                return CreateDefaultPalette();
            }

            Debug.Log($"[TextureBuilder] Successfully loaded PLAYPAL with {playpalData.Length} bytes");
            var colors = new Color[256];
            for (var i = 0; i < 256; i++)
            {
                var r = playpalData[i * 3] / 255f;
                var g = playpalData[i * 3 + 1] / 255f;
                var b = playpalData[i * 3 + 2] / 255f;
                colors[i] = new Color(r, g, b, 1f);
            }

            return colors;
        }

        string[] LoadPnames()
        {
            var pnamesData = wadReader.ReadLumpData("PNAMES", reader =>
            {
                var count = reader.ReadInt32();
                Debug.Log($"[TextureBuilder] PNAMES contains {count} patch names");
                var names = new string[count];
                
                for (var i = 0; i < count; i++)
                {
                    var nameBytes = reader.ReadBytes(8);
                    names[i] = System.Text.Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                }
                
                return names;
            });
            
            if (pnamesData == null)
            {
                Debug.LogError("[TextureBuilder] PNAMES lump not found");
                return new string[0];
            }
            
            return pnamesData;
        }

        WadTexture[] LoadTextureDirectory(string lumpName)
        {
            return wadReader.ReadLumpData(lumpName, reader =>
            {
                if (reader.BaseStream.Length < 4)
                    return new WadTexture[0];

                var count = reader.ReadInt32();
                if (count <= 0 || count > 10000) // Reasonable limit
                    return new WadTexture[0];

                if (reader.BaseStream.Length < 4 + count * 4)
                    return new WadTexture[0];

                var offsets = new int[count];
                
                for (var i = 0; i < count; i++)
                    offsets[i] = reader.ReadInt32();
                
                var textures = new WadTexture[count];
                for (var i = 0; i < count; i++)
                {
                    if (offsets[i] >= 0 && offsets[i] < reader.BaseStream.Length)
                    {
                        reader.BaseStream.Seek(offsets[i], SeekOrigin.Begin);
                        textures[i] = WadTexture.Read(reader);
                    }
                }
                
                return textures;
            }) ?? new WadTexture[0];
        }

        Texture2D CreateDefaultTexture(int width, int height)
        {
            Debug.LogWarning($"[TextureBuilder] Creating default magenta texture ({width}x{height}) - this indicates a texture loading failure");
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var colors = new Color[width * height];
            
            for (var i = 0; i < colors.Length; i++)
                colors[i] = Color.magenta;
            
            texture.SetPixels(colors);
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            
            return texture;
        }

        Color[] CreateDefaultPalette()
        {
            Debug.LogWarning("[TextureBuilder] Creating default white palette - this indicates PLAYPAL loading failure");
            var colors = new Color[256];
            for (var i = 0; i < 256; i++)
                colors[i] = Color.white;
            return colors;
        }
    }
}