using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace WadImporter.Editor
{
    [ScriptedImporter(1, "wad")]
    public class WadImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var wadData = File.ReadAllBytes(ctx.assetPath);
            var wadReader = new WadReader(wadData);
            var wadName = Path.GetFileNameWithoutExtension(ctx.assetPath);

            var textureBuilder = new TextureBuilder(wadReader);
            var textures = textureBuilder.BuildTextures();
            var flats = BuildFlats(wadReader, textureBuilder);
            
            var levelPackage = ScriptableObject.CreateInstance<LevelPackage>();
            levelPackage.WadName = wadName;
            
            // Create root GameObject for all levels
            var rootPrefab = new GameObject(wadName);
            
            // Find and process all maps
            var mapNames = FindAllMapLumps(wadReader);
            var allMaterials = new Dictionary<string, Material>();
            
            // Create a single PrefabAssembler for material management
            var dummyModel = new LevelModel();
            var materialAssembler = new PrefabAssembler(dummyModel, textures, flats);
            
            foreach (var mapName in mapNames)
            {
                var levelModel = BuildLevelModel(wadReader, mapName);
                
                // Create PrefabAssembler with shared materials
                var prefabAssembler = new PrefabAssembler(levelModel, textures, flats);
                prefabAssembler.ShareMaterialsFrom(materialAssembler);
                
                // Create level as child of root
                var levelParent = new GameObject(mapName);
                levelParent.transform.SetParent(rootPrefab.transform);
                
                prefabAssembler.AssemblePrefab(mapName, levelPackage, levelParent);
            }
            
            // Get all materials from the shared assembler
            allMaterials = materialAssembler.GetAllMaterials();
            
            levelPackage.LevelPrefab = rootPrefab;
            
            // Add the main object first, then set it as main
            ctx.AddObjectToAsset("main", levelPackage);
            ctx.SetMainObject(levelPackage);
            
            AddSubAssets(ctx, levelPackage, textures, flats, allMaterials);
        }

        LevelModel BuildLevelModel(WadReader wadReader, string mapName)
        {
            var levelModel = new LevelModel();
            
            // For DOOM format, level lumps are right after the map marker
            var mapIndex = wadReader.FindLumpIndex(mapName);
            Debug.Log($"Building level model for {mapName} at index {mapIndex}");
            
            if (mapIndex >= 0)
            {
                // Search for each lump type after the map marker
                // They should appear in order: THINGS, LINEDEFS, SIDEDEFS, VERTEXES, SEGS, SSECTORS, NODES, SECTORS, REJECT, BLOCKMAP
                // but we only need the first 5 for basic geometry
                
                for (var i = mapIndex + 1; i < mapIndex + 11 && i < wadReader.GetLumpCount(); i++)
                {
                    var lumpName = wadReader.GetLumpNameAtIndex(i);
                    Debug.Log($"{mapName}: Checking lump at {i}: {lumpName}");
                    
                    if (lumpName == "THINGS")
                    {
                        levelModel.things = wadReader.ReadLumpArrayAtIndex(i, WadThing.Read);
                        Debug.Log($"{mapName}: Found THINGS at {i}, count: {levelModel.things.Length}");
                    }
                    else if (lumpName == "LINEDEFS")
                    {
                        levelModel.linedefs = wadReader.ReadLumpArrayAtIndex(i, WadLinedef.Read);
                        Debug.Log($"{mapName}: Found LINEDEFS at {i}, count: {levelModel.linedefs.Length}");
                    }
                    else if (lumpName == "SIDEDEFS")
                    {
                        levelModel.sidedefs = wadReader.ReadLumpArrayAtIndex(i, WadSidedef.Read);
                        Debug.Log($"{mapName}: Found SIDEDEFS at {i}, count: {levelModel.sidedefs.Length}");
                    }
                    else if (lumpName == "VERTEXES")
                    {
                        levelModel.vertices = wadReader.ReadLumpArrayAtIndex(i, WadVertex.Read);
                        Debug.Log($"{mapName}: Found VERTEXES at {i}, count: {levelModel.vertices.Length}");
                    }
                    else if (lumpName == "SECTORS")
                    {
                        levelModel.sectors = wadReader.ReadLumpArrayAtIndex(i, WadSector.Read);
                        Debug.Log($"{mapName}: Found SECTORS at {i}, count: {levelModel.sectors.Length}");
                    }
                    else if (lumpName.StartsWith("E") && lumpName.Contains("M") && lumpName.Length <= 4)
                    {
                        Debug.Log($"{mapName}: Hit next map marker {lumpName}, stopping");
                        break; // Hit next map marker, stop searching
                    }
                }
                
                Debug.Log($"{mapName}: Final counts - Vertices: {levelModel.vertices.Length}, Linedefs: {levelModel.linedefs.Length}, Sectors: {levelModel.sectors.Length}");
            }
            
            return levelModel;
        }

        bool TryFindMapLump(WadReader wadReader, out string mapName)
        {
            var lumpNames = wadReader.GetLumpNames().ToList();
            
            foreach (var lumpName in lumpNames)
            {
                if (lumpName.StartsWith("E") && lumpName.Contains("M") && lumpName.Length <= 4)
                {
                    mapName = lumpName;
                    return true;
                }
                
                if (lumpName.StartsWith("MAP") && lumpName.Length == 5)
                {
                    mapName = lumpName;
                    return true;
                }
            }
            
            mapName = "";
            return false;
        }
        
        List<string> FindAllMapLumps(WadReader wadReader)
        {
            var mapLumps = new List<string>();
            var lumpNames = wadReader.GetLumpNames().ToList();
            
            foreach (var lumpName in lumpNames)
            {
                if ((lumpName.StartsWith("E") && lumpName.Contains("M") && lumpName.Length <= 4) ||
                    (lumpName.StartsWith("MAP") && lumpName.Length == 5))
                {
                    mapLumps.Add(lumpName);
                }
            }
            
            Debug.Log($"Found {mapLumps.Count} maps: {string.Join(", ", mapLumps)}");
            return mapLumps;
        }

        Dictionary<string, Texture2D> BuildFlats(WadReader wadReader, TextureBuilder textureBuilder)
        {
            var flats = new Dictionary<string, Texture2D>();
            var lumpNames = wadReader.GetLumpNames().ToList();
            
            var startFound = false;
            foreach (var lumpName in lumpNames)
            {
                if (lumpName == "F_START" || lumpName == "FF_START")
                {
                    startFound = true;
                    continue;
                }
                
                if (lumpName == "F_END" || lumpName == "FF_END")
                    break;
                
                if (startFound)
                {
                    var flatTexture = textureBuilder.BuildFlatTexture(lumpName);
                    if (flatTexture != null)
                        flats[lumpName] = flatTexture;
                }
            }
            
            return flats;
        }

        void AddSubAssets(AssetImportContext ctx, LevelPackage levelPackage, 
            Dictionary<string, Texture2D> textures, Dictionary<string, Texture2D> flats,
            Dictionary<string, Material> materials)
        {
            ctx.AddObjectToAsset("Prefab", levelPackage.LevelPrefab);
            
            foreach (var mesh in levelPackage.Meshes)
            {
                if (mesh != null)
                    ctx.AddObjectToAsset(mesh.name, mesh);
            }
            
            foreach (var kvp in textures)
            {
                if (kvp.Value != null)
                {
                    ctx.AddObjectToAsset($"Tex_{kvp.Key}", kvp.Value);
                    levelPackage.AddTexture(kvp.Value);
                }
            }
            
            foreach (var kvp in flats)
            {
                if (kvp.Value != null)
                {
                    ctx.AddObjectToAsset($"Flat_{kvp.Key}", kvp.Value);
                    levelPackage.AddTexture(kvp.Value);
                }
            }
            
            foreach (var kvp in materials)
            {
                if (kvp.Value != null)
                {
                    ctx.AddObjectToAsset($"Mat_{kvp.Key}", kvp.Value);
                    levelPackage.AddMaterial(kvp.Value);
                }
            }
        }
    }
}