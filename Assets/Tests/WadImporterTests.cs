using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using WadImporter;

namespace Tests
{
    [TestFixture]
    public class WadImporterTests
    {
        const string TestWadPath = "Assets/Tests/Resources/DOOM1.WAD";
        const string TestImportPath = "Assets/ImportedLevels/DOOM1.asset";
        
        [SetUp]
        public void Setup()
        {
            if (AssetDatabase.LoadAssetAtPath<LevelPackage>(TestImportPath) != null)
                AssetDatabase.DeleteAsset(TestImportPath);
        }
        
        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.LoadAssetAtPath<LevelPackage>(TestImportPath) != null)
                AssetDatabase.DeleteAsset(TestImportPath);
        }
        
        [Test]
        public void ImporterCreatesMainAsset()
        {
            AssetDatabase.ImportAsset(TestWadPath, ImportAssetOptions.ForceUpdate);
            
            var assets = AssetDatabase.FindAssets("t:LevelPackage DOOM1");
            Assert.AreEqual(1, assets.Length, "Expected exactly one LevelPackage asset");
            
            var assetPath = AssetDatabase.GUIDToAssetPath(assets[0]);
            var mainObject = AssetDatabase.LoadMainAssetAtPath(assetPath);
            Assert.IsNotNull(mainObject, "Main asset should not be null");
            Assert.IsTrue(mainObject is LevelPackage, "Main asset should be LevelPackage");
        }
        
        [Test]
        public void PrefabIntegrity()
        {
            AssetDatabase.ImportAsset(TestWadPath, ImportAssetOptions.ForceUpdate);
            
            var assets = AssetDatabase.FindAssets("t:LevelPackage DOOM1");
            var assetPath = AssetDatabase.GUIDToAssetPath(assets[0]);
            var levelPackage = AssetDatabase.LoadAssetAtPath<LevelPackage>(assetPath);
            
            Assert.IsNotNull(levelPackage.LevelPrefab, "Level prefab should not be null");
            
            var meshFilters = levelPackage.LevelPrefab.GetComponentsInChildren<MeshFilter>();
            Assert.Greater(meshFilters.Length, 0, "Prefab should contain MeshFilter components");
            
            foreach (var meshFilter in meshFilters)
            {
                Assert.IsNotNull(meshFilter.sharedMesh, "MeshFilter should have a mesh");
                Assert.Greater(meshFilter.sharedMesh.triangles.Length, 0, "Mesh should have triangles");
            }
            
            var meshRenderers = levelPackage.LevelPrefab.GetComponentsInChildren<MeshRenderer>();
            Assert.Greater(meshRenderers.Length, 0, "Prefab should contain MeshRenderer components");
            
            foreach (var meshRenderer in meshRenderers)
            {
                Assert.IsNotNull(meshRenderer.sharedMaterial, "MeshRenderer should have a material");
                Assert.IsNotNull(meshRenderer.sharedMaterial.mainTexture, "Material should have a texture");
            }
        }
        
        [Test]
        public void TextureDimensions()
        {
            AssetDatabase.ImportAsset(TestWadPath, ImportAssetOptions.ForceUpdate);
            
            var assets = AssetDatabase.FindAssets("t:LevelPackage DOOM1");
            var assetPath = AssetDatabase.GUIDToAssetPath(assets[0]);
            var levelPackage = AssetDatabase.LoadAssetAtPath<LevelPackage>(assetPath);
            
            Assert.Greater(levelPackage.Textures.Count, 0, "Should have textures");
            
            foreach (var texture in levelPackage.Textures)
            {
                Assert.IsNotNull(texture, "Texture should not be null");
                
                var isPowerOfTwo = IsPowerOfTwo(texture.width) && IsPowerOfTwo(texture.height);
                Assert.IsTrue(isPowerOfTwo, $"Texture dimensions should be power of two: {texture.width}x{texture.height}");
                
                var isValidSize = (texture.width >= 8 && texture.width <= 512) && 
                                 (texture.height >= 8 && texture.height <= 512);
                Assert.IsTrue(isValidSize, $"Texture dimensions should be between 8 and 512: {texture.width}x{texture.height}");
            }
        }
        
        bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }
    }
}