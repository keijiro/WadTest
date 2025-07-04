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
            
            var texture1 = LoadTextureDirectory("TEXTURE1");
            var texture2 = LoadTextureDirectory("TEXTURE2");
            
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
            
            return textures;
        }

        public Texture2D BuildFlatTexture(string name)
        {
            var flatData = wadReader.GetLumpData(name);
            if (flatData == null || flatData.Length != 64 * 64)
                return CreateDefaultTexture(64, 64);

            var texture = new Texture2D(64, 64, TextureFormat.RGB24, false);
            var colors = new Color[64 * 64];

            for (var i = 0; i < flatData.Length; i++)
            {
                var paletteIndex = flatData[i];
                colors[i] = palette[paletteIndex];
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
                return CreateDefaultTexture(64, 64);

            var texture = new Texture2D(wadTexture.width, wadTexture.height, TextureFormat.RGBA32, false);
            var colors = new Color[wadTexture.width * wadTexture.height];
            
            for (var i = 0; i < colors.Length; i++)
                colors[i] = Color.clear;

            foreach (var patch in wadTexture.patches)
            {
                if (patch.patch >= pnames.Length)
                    continue;

                var patchName = pnames[patch.patch];
                var patchData = wadReader.GetLumpData(patchName);
                if (patchData == null)
                    continue;

                ApplyPatch(colors, wadTexture.width, wadTexture.height, patchData, patch.originX, patch.originY);
            }

            texture.SetPixels(colors);
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            
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
                                    targetColors[pixelIndex] = palette[paletteIndex];
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
            if (playpalData == null || playpalData.Length < 768)
                return CreateDefaultPalette();

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
            return wadReader.ReadLumpData("PNAMES", reader =>
            {
                var count = reader.ReadInt32();
                var names = new string[count];
                
                for (var i = 0; i < count; i++)
                {
                    var nameBytes = reader.ReadBytes(8);
                    names[i] = System.Text.Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                }
                
                return names;
            }) ?? new string[0];
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
            var colors = new Color[256];
            for (var i = 0; i < 256; i++)
                colors[i] = Color.white;
            return colors;
        }
    }
}