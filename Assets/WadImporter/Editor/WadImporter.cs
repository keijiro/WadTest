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

            var levelModel = BuildLevelModel(wadReader);
            var textureBuilder = new TextureBuilder(wadReader);
            
            var textures = textureBuilder.BuildTextures();
            var flats = BuildFlats(wadReader, textureBuilder);
            
            var prefabAssembler = new PrefabAssembler(levelModel, textures, flats);
            
            var levelPackage = ScriptableObject.CreateInstance<LevelPackage>();
            levelPackage.WadName = wadName;
            
            var levelPrefab = prefabAssembler.AssemblePrefab(wadName, levelPackage);
            levelPackage.LevelPrefab = levelPrefab;
            
            // Add the main object first, then set it as main
            ctx.AddObjectToAsset("main", levelPackage);
            ctx.SetMainObject(levelPackage);
            
            AddSubAssets(ctx, levelPackage, textures, flats, prefabAssembler.GetAllMaterials());
        }

        LevelModel BuildLevelModel(WadReader wadReader)
        {
            var levelModel = new LevelModel();
            
            if (TryFindMapLump(wadReader, out var mapName))
            {
                // For DOOM WADs, the level data lumps use standard names, not prefixed names
                // Try prefixed names first (for newer formats), then fall back to standard names
                levelModel.vertices = wadReader.ReadLumpArray($"{mapName}_VERTEXES", WadVertex.Read);
                if (levelModel.vertices.Length == 0)
                    levelModel.vertices = wadReader.ReadLumpArray("VERTEXES", WadVertex.Read);
                
                levelModel.linedefs = wadReader.ReadLumpArray($"{mapName}_LINEDEFS", WadLinedef.Read);
                if (levelModel.linedefs.Length == 0)
                    levelModel.linedefs = wadReader.ReadLumpArray("LINEDEFS", WadLinedef.Read);
                
                levelModel.sidedefs = wadReader.ReadLumpArray($"{mapName}_SIDEDEFS", WadSidedef.Read);
                if (levelModel.sidedefs.Length == 0)
                    levelModel.sidedefs = wadReader.ReadLumpArray("SIDEDEFS", WadSidedef.Read);
                
                levelModel.sectors = wadReader.ReadLumpArray($"{mapName}_SECTORS", WadSector.Read);
                if (levelModel.sectors.Length == 0)
                    levelModel.sectors = wadReader.ReadLumpArray("SECTORS", WadSector.Read);
                
                levelModel.things = wadReader.ReadLumpArray($"{mapName}_THINGS", WadThing.Read);
                if (levelModel.things.Length == 0)
                    levelModel.things = wadReader.ReadLumpArray("THINGS", WadThing.Read);
            }
            else
            {
                levelModel.vertices = wadReader.ReadLumpArray("VERTEXES", WadVertex.Read);
                levelModel.linedefs = wadReader.ReadLumpArray("LINEDEFS", WadLinedef.Read);
                levelModel.sidedefs = wadReader.ReadLumpArray("SIDEDEFS", WadSidedef.Read);
                levelModel.sectors = wadReader.ReadLumpArray("SECTORS", WadSector.Read);
                levelModel.things = wadReader.ReadLumpArray("THINGS", WadThing.Read);
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
                    ctx.AddObjectToAsset($"Mesh_{mesh.name}", mesh);
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