using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WadImporter
{
    public class MaterialFactory
    {
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        Material defaultMaterial;

        public MaterialFactory()
        {
            CreateDefaultMaterial();
        }

        public Material CreateMaterial(string name, Texture2D texture)
        {
            if (materials.TryGetValue(name, out var existingMaterial))
                return existingMaterial;

            var material = new Material(GetUnlitShader())
            {
                name = $"Mat_{name}",
                mainTexture = texture
            };

            material.SetFloat("_Surface", 0); // Opaque
            material.SetFloat("_Blend", 0); // Alpha
            material.SetFloat("_AlphaClip", 0); // No alpha clipping
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1);
            material.SetInt("_Cull", (int)CullMode.Back);
            
            material.renderQueue = (int)RenderQueue.Geometry;
            material.enableInstancing = true;

            materials[name] = material;
            return material;
        }

        public Material CreateFlatMaterial(string name, Texture2D texture)
        {
            if (materials.TryGetValue(name, out var existingMaterial))
                return existingMaterial;

            var material = new Material(GetUnlitShader())
            {
                name = $"Flat_{name}",
                mainTexture = texture
            };

            material.SetFloat("_Surface", 0); // Opaque
            material.SetFloat("_Blend", 0); // Alpha
            material.SetFloat("_AlphaClip", 0); // No alpha clipping
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1);
            material.SetInt("_Cull", (int)CullMode.Back);
            
            material.renderQueue = (int)RenderQueue.Geometry;
            material.enableInstancing = true;

            materials[name] = material;
            return material;
        }

        public Material GetDefaultMaterial()
        {
            return defaultMaterial;
        }

        public Dictionary<string, Material> GetAllMaterials()
        {
            return new Dictionary<string, Material>(materials);
        }

        void CreateDefaultMaterial()
        {
            var defaultTexture = CreateDefaultTexture();
            defaultMaterial = new Material(GetUnlitShader())
            {
                name = "Default_WAD_Material",
                mainTexture = defaultTexture
            };

            defaultMaterial.SetFloat("_Surface", 0); // Opaque
            defaultMaterial.SetFloat("_Blend", 0); // Alpha
            defaultMaterial.SetFloat("_AlphaClip", 0); // No alpha clipping
            defaultMaterial.SetFloat("_SrcBlend", (float)BlendMode.One);
            defaultMaterial.SetFloat("_DstBlend", (float)BlendMode.Zero);
            defaultMaterial.SetFloat("_ZWrite", 1);
            defaultMaterial.SetInt("_Cull", (int)CullMode.Back);
            
            defaultMaterial.renderQueue = (int)RenderQueue.Geometry;
            defaultMaterial.enableInstancing = true;
        }

        Texture2D CreateDefaultTexture()
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGB24, false);
            var colors = new Color[64 * 64];
            
            for (var i = 0; i < colors.Length; i++)
            {
                var x = i % 64;
                var y = i / 64;
                var checker = (x / 8 + y / 8) % 2 == 0;
                colors[i] = checker ? Color.magenta : Color.black;
            }
            
            texture.SetPixels(colors);
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            
            return texture;
        }

        Shader GetUnlitShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit");
        }
    }
}