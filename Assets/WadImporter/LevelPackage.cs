using System.Collections.Generic;
using UnityEngine;

namespace WadImporter
{
    [CreateAssetMenu(fileName = "LevelPackage", menuName = "WAD/Level Package")]
    public class LevelPackage : ScriptableObject
    {
        [SerializeField] string wadName;
        [SerializeField] GameObject levelPrefab;
        [SerializeField] List<Mesh> meshes = new List<Mesh>();
        [SerializeField] List<Texture2D> textures = new List<Texture2D>();
        [SerializeField] List<Material> materials = new List<Material>();

        public string WadName
        {
            get => wadName;
            set => wadName = value;
        }

        public GameObject LevelPrefab
        {
            get => levelPrefab;
            set => levelPrefab = value;
        }

        public List<Mesh> Meshes => meshes;
        public List<Texture2D> Textures => textures;
        public List<Material> Materials => materials;

        public void AddMesh(Mesh mesh)
        {
            if (mesh != null && !meshes.Contains(mesh))
                meshes.Add(mesh);
        }

        public void AddTexture(Texture2D texture)
        {
            if (texture != null && !textures.Contains(texture))
                textures.Add(texture);
        }

        public void AddMaterial(Material material)
        {
            if (material != null && !materials.Contains(material))
                materials.Add(material);
        }

        public void Clear()
        {
            meshes.Clear();
            textures.Clear();
            materials.Clear();
            levelPrefab = null;
        }
    }
}