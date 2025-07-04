using System.Collections.Generic;
using UnityEngine;

namespace WadImporter
{
    public class PrefabAssembler
    {
        readonly LevelModel levelModel;
        readonly MeshGenerator meshGenerator;
        readonly MaterialFactory materialFactory;
        readonly Dictionary<string, Material> wallMaterials;
        readonly Dictionary<string, Material> flatMaterials;

        public PrefabAssembler(LevelModel levelModel, Dictionary<string, Texture2D> textures, Dictionary<string, Texture2D> flats)
        {
            this.levelModel = levelModel;
            meshGenerator = new MeshGenerator(levelModel);
            materialFactory = new MaterialFactory();
            
            wallMaterials = CreateWallMaterials(textures);
            flatMaterials = CreateFlatMaterials(flats);
        }

        public GameObject AssemblePrefab(string wadName, LevelPackage levelPackage)
        {
            var levelRoot = new GameObject($"Level_{wadName}");
            
            CreateSectorObjects(levelRoot, levelPackage);
            CreateWallObjects(levelRoot, levelPackage);
            
            return levelRoot;
        }

        void CreateSectorObjects(GameObject parent, LevelPackage levelPackage)
        {
            // Debug.Log($"Creating sector objects for {levelModel.sectors.Length} sectors");
            for (var i = 0; i < levelModel.sectors.Length; i++)
            {
                var sector = levelModel.sectors[i];
                var sectorObject = new GameObject($"Sector_{i}");
                sectorObject.transform.SetParent(parent.transform);
                
                var floorMesh = meshGenerator.GenerateFloorMesh(i);
                if (floorMesh != null && floorMesh.vertices.Length > 0)
                {
                    // Debug.Log($"Created floor mesh for sector {i} with {floorMesh.vertices.Length} vertices");
                    var floorObject = new GameObject($"Floor_{i}");
                    floorObject.transform.SetParent(sectorObject.transform);
                    
                    var floorRenderer = floorObject.AddComponent<MeshRenderer>();
                    var floorFilter = floorObject.AddComponent<MeshFilter>();
                    
                    floorFilter.sharedMesh = floorMesh;
                    floorRenderer.sharedMaterial = GetFlatMaterial(sector.floorTexture);
                    
                    levelPackage.AddMesh(floorMesh);
                }
                else
                {
                    // Debug.LogWarning($"Floor mesh for sector {i} was null or empty");
                }
                
                var ceilingMesh = meshGenerator.GenerateCeilingMesh(i);
                if (ceilingMesh != null && ceilingMesh.vertices.Length > 0)
                {
                    var ceilingObject = new GameObject($"Ceiling_{i}");
                    ceilingObject.transform.SetParent(sectorObject.transform);
                    
                    var ceilingRenderer = ceilingObject.AddComponent<MeshRenderer>();
                    var ceilingFilter = ceilingObject.AddComponent<MeshFilter>();
                    
                    ceilingFilter.sharedMesh = ceilingMesh;
                    ceilingRenderer.sharedMaterial = GetFlatMaterial(sector.ceilingTexture);
                    
                    levelPackage.AddMesh(ceilingMesh);
                }
            }
        }

        void CreateWallObjects(GameObject parent, LevelPackage levelPackage)
        {
            var wallsContainer = new GameObject("Walls");
            wallsContainer.transform.SetParent(parent.transform);
            
            for (var i = 0; i < levelModel.linedefs.Length; i++)
            {
                var linedef = levelModel.linedefs[i];
                
                var frontWallMesh = meshGenerator.GenerateWallMesh(i, false);
                if (frontWallMesh != null && frontWallMesh.vertices.Length > 0)
                {
                    var frontWallObject = new GameObject($"Wall_{i}_Front");
                    frontWallObject.transform.SetParent(wallsContainer.transform);
                    
                    var frontRenderer = frontWallObject.AddComponent<MeshRenderer>();
                    var frontFilter = frontWallObject.AddComponent<MeshFilter>();
                    
                    frontFilter.sharedMesh = frontWallMesh;
                    
                    var frontSidedef = levelModel.sidedefs[linedef.frontSidedef];
                    var wallTexture = GetWallTexture(frontSidedef, linedef.TwoSided);
                    frontRenderer.sharedMaterial = GetWallMaterial(wallTexture);
                    
                    levelPackage.AddMesh(frontWallMesh);
                }
                
                if (linedef.HasBackSide)
                {
                    var backWallMesh = meshGenerator.GenerateWallMesh(i, true);
                    if (backWallMesh != null && backWallMesh.vertices.Length > 0)
                    {
                        var backWallObject = new GameObject($"Wall_{i}_Back");
                        backWallObject.transform.SetParent(wallsContainer.transform);
                        
                        var backRenderer = backWallObject.AddComponent<MeshRenderer>();
                        var backFilter = backWallObject.AddComponent<MeshFilter>();
                        
                        backFilter.sharedMesh = backWallMesh;
                        
                        var backSidedef = levelModel.sidedefs[linedef.backSidedef];
                        var wallTexture = GetWallTexture(backSidedef, linedef.TwoSided);
                        backRenderer.sharedMaterial = GetWallMaterial(wallTexture);
                        
                        levelPackage.AddMesh(backWallMesh);
                    }
                }
            }
        }

        string GetWallTexture(WadSidedef sidedef, bool isTwoSided)
        {
            if (isTwoSided)
            {
                if (!string.IsNullOrEmpty(sidedef.upperTexture) && sidedef.upperTexture != "-")
                    return sidedef.upperTexture;
                if (!string.IsNullOrEmpty(sidedef.lowerTexture) && sidedef.lowerTexture != "-")
                    return sidedef.lowerTexture;
            }
            
            if (!string.IsNullOrEmpty(sidedef.middleTexture) && sidedef.middleTexture != "-")
                return sidedef.middleTexture;
            
            return "";
        }

        Material GetWallMaterial(string textureName)
        {
            if (string.IsNullOrEmpty(textureName) || textureName == "-")
                return materialFactory.GetDefaultMaterial();
            
            if (wallMaterials.TryGetValue(textureName, out var material))
                return material;
            
            return materialFactory.GetDefaultMaterial();
        }

        Material GetFlatMaterial(string textureName)
        {
            if (string.IsNullOrEmpty(textureName) || textureName == "-")
                return materialFactory.GetDefaultMaterial();
            
            if (flatMaterials.TryGetValue(textureName, out var material))
                return material;
            
            return materialFactory.GetDefaultMaterial();
        }

        Dictionary<string, Material> CreateWallMaterials(Dictionary<string, Texture2D> textures)
        {
            var materials = new Dictionary<string, Material>();
            
            foreach (var kvp in textures)
            {
                var material = materialFactory.CreateMaterial(kvp.Key, kvp.Value);
                materials[kvp.Key] = material;
            }
            
            return materials;
        }

        Dictionary<string, Material> CreateFlatMaterials(Dictionary<string, Texture2D> flats)
        {
            var materials = new Dictionary<string, Material>();
            
            foreach (var kvp in flats)
            {
                var material = materialFactory.CreateFlatMaterial(kvp.Key, kvp.Value);
                materials[kvp.Key] = material;
            }
            
            return materials;
        }

        public Dictionary<string, Material> GetAllMaterials()
        {
            var allMaterials = new Dictionary<string, Material>();
            
            foreach (var kvp in wallMaterials)
                allMaterials[kvp.Key] = kvp.Value;
            
            foreach (var kvp in flatMaterials)
                allMaterials[kvp.Key] = kvp.Value;
            
            return allMaterials;
        }
    }
}