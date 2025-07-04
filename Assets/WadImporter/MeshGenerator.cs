using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WadImporter
{
    public class MeshGenerator
    {
        readonly LevelModel levelModel;

        public MeshGenerator(LevelModel levelModel)
        {
            this.levelModel = levelModel;
        }

        public Mesh GenerateFloorMesh(int sectorIndex)
        {
            var sector = levelModel.sectors[sectorIndex];
            var sectorLines = GetSectorLines(sectorIndex);
            // Debug.Log($"Sector {sectorIndex}: Found {sectorLines.Count} lines");
            var vertices = ExtractSectorVertices(sectorLines);
            // Debug.Log($"Sector {sectorIndex}: Extracted {vertices.Length} unique vertices");
            var triangles = TriangulateSector(vertices);
            // Debug.Log($"Sector {sectorIndex}: Triangulated {triangles.Length / 3} triangles");

            var mesh = new Mesh();
            mesh.name = $"Floor_{sectorIndex}";

            var meshVertices = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];

            for (var i = 0; i < vertices.Length; i++)
            {
                meshVertices[i] = new Vector3(vertices[i].x, sector.FloorHeightUnits, -vertices[i].y);
                uvs[i] = new Vector2(vertices[i].x / 64f, vertices[i].y / 64f);
            }

            mesh.vertices = meshVertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();

            return mesh;
        }

        public Mesh GenerateCeilingMesh(int sectorIndex)
        {
            var sector = levelModel.sectors[sectorIndex];
            var sectorLines = GetSectorLines(sectorIndex);
            var vertices = ExtractSectorVertices(sectorLines);
            var triangles = TriangulateSector(vertices);

            var mesh = new Mesh();
            mesh.name = $"Ceiling_{sectorIndex}";

            var meshVertices = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];

            for (var i = 0; i < vertices.Length; i++)
            {
                meshVertices[i] = new Vector3(vertices[i].x, sector.CeilingHeightUnits, -vertices[i].y);
                uvs[i] = new Vector2(vertices[i].x / 64f, vertices[i].y / 64f);
            }

            mesh.vertices = meshVertices;
            mesh.triangles = ReverseTriangles(triangles);
            mesh.uv = uvs;
            mesh.RecalculateNormals();

            return mesh;
        }

        public Mesh GenerateWallMesh(int linedefIndex, bool isBack = false)
        {
            var linedef = levelModel.linedefs[linedefIndex];
            var sidedefIndex = isBack ? linedef.backSidedef : linedef.frontSidedef;
            
            if (sidedefIndex == 0xFFFF)
                return null;

            var sidedef = levelModel.sidedefs[sidedefIndex];
            var sector = levelModel.sectors[sidedef.sector];
            
            var startVertex = levelModel.vertices[linedef.startVertex];
            var endVertex = levelModel.vertices[linedef.endVertex];

            var mesh = new Mesh();
            mesh.name = $"Wall_{linedefIndex}_{(isBack ? "Back" : "Front")}";

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();

            var lineLength = Vector2.Distance(startVertex.ToVector2(), endVertex.ToVector2());
            var currentU = 0f;

            if (linedef.TwoSided && linedef.HasBackSide)
            {
                var backSector = levelModel.sectors[levelModel.sidedefs[linedef.backSidedef].sector];
                GenerateWallSegments(vertices, triangles, uvs, startVertex, endVertex, sector, backSector, sidedef, lineLength, isBack);
            }
            else
            {
                GenerateFullWall(vertices, triangles, uvs, startVertex, endVertex, sector, sidedef, lineLength, isBack);
            }

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.RecalculateNormals();

            return mesh;
        }

        void GenerateFullWall(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, 
            WadVertex startVertex, WadVertex endVertex, WadSector sector, WadSidedef sidedef, 
            float lineLength, bool isBack)
        {
            var baseIndex = vertices.Count;
            var direction = isBack ? -1 : 1;

            vertices.Add(new Vector3(startVertex.x, sector.FloorHeightUnits, -startVertex.y));
            vertices.Add(new Vector3(endVertex.x, sector.FloorHeightUnits, -endVertex.y));
            vertices.Add(new Vector3(endVertex.x, sector.CeilingHeightUnits, -endVertex.y));
            vertices.Add(new Vector3(startVertex.x, sector.CeilingHeightUnits, -startVertex.y));

            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(lineLength / 64f, 0));
            uvs.Add(new Vector2(lineLength / 64f, sector.WallHeight / 64f));
            uvs.Add(new Vector2(0, sector.WallHeight / 64f));

            if (direction > 0)
            {
                triangles.AddRange(new[] { baseIndex, baseIndex + 1, baseIndex + 2 });
                triangles.AddRange(new[] { baseIndex, baseIndex + 2, baseIndex + 3 });
            }
            else
            {
                triangles.AddRange(new[] { baseIndex, baseIndex + 2, baseIndex + 1 });
                triangles.AddRange(new[] { baseIndex, baseIndex + 3, baseIndex + 2 });
            }
        }

        void GenerateWallSegments(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs,
            WadVertex startVertex, WadVertex endVertex, WadSector frontSector, WadSector backSector,
            WadSidedef sidedef, float lineLength, bool isBack)
        {
            var direction = isBack ? -1 : 1;

            var lowerHeight = Mathf.Max(frontSector.FloorHeightUnits, backSector.FloorHeightUnits);
            var upperHeight = Mathf.Min(frontSector.CeilingHeightUnits, backSector.CeilingHeightUnits);

            if (frontSector.FloorHeightUnits < backSector.FloorHeightUnits)
            {
                GenerateWallSegment(vertices, triangles, uvs, startVertex, endVertex,
                    frontSector.FloorHeightUnits, lowerHeight, lineLength, direction);
            }

            if (frontSector.CeilingHeightUnits > backSector.CeilingHeightUnits)
            {
                GenerateWallSegment(vertices, triangles, uvs, startVertex, endVertex,
                    upperHeight, frontSector.CeilingHeightUnits, lineLength, direction);
            }
        }

        void GenerateWallSegment(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs,
            WadVertex startVertex, WadVertex endVertex, float bottomHeight, float topHeight,
            float lineLength, int direction)
        {
            var baseIndex = vertices.Count;
            var segmentHeight = topHeight - bottomHeight;

            vertices.Add(new Vector3(startVertex.x, bottomHeight, -startVertex.y));
            vertices.Add(new Vector3(endVertex.x, bottomHeight, -endVertex.y));
            vertices.Add(new Vector3(endVertex.x, topHeight, -endVertex.y));
            vertices.Add(new Vector3(startVertex.x, topHeight, -startVertex.y));

            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(lineLength / 64f, 0));
            uvs.Add(new Vector2(lineLength / 64f, segmentHeight / 64f));
            uvs.Add(new Vector2(0, segmentHeight / 64f));

            if (direction > 0)
            {
                triangles.AddRange(new[] { baseIndex, baseIndex + 1, baseIndex + 2 });
                triangles.AddRange(new[] { baseIndex, baseIndex + 2, baseIndex + 3 });
            }
            else
            {
                triangles.AddRange(new[] { baseIndex, baseIndex + 2, baseIndex + 1 });
                triangles.AddRange(new[] { baseIndex, baseIndex + 3, baseIndex + 2 });
            }
        }

        List<int> GetSectorLines(int sectorIndex)
        {
            var sectorLines = new List<int>();
            
            for (var i = 0; i < levelModel.linedefs.Length; i++)
            {
                var linedef = levelModel.linedefs[i];
                
                if (linedef.frontSidedef != 0xFFFF)
                {
                    var frontSidedef = levelModel.sidedefs[linedef.frontSidedef];
                    if (frontSidedef.sector == sectorIndex)
                        sectorLines.Add(i);
                }
                
                if (linedef.backSidedef != 0xFFFF)
                {
                    var backSidedef = levelModel.sidedefs[linedef.backSidedef];
                    if (backSidedef.sector == sectorIndex)
                        sectorLines.Add(i);
                }
            }
            
            return sectorLines;
        }

        Vector2[] ExtractSectorVertices(List<int> sectorLines)
        {
            var vertices = new HashSet<Vector2>();
            
            foreach (var lineIndex in sectorLines)
            {
                var linedef = levelModel.linedefs[lineIndex];
                vertices.Add(levelModel.vertices[linedef.startVertex].ToVector2());
                vertices.Add(levelModel.vertices[linedef.endVertex].ToVector2());
            }
            
            return vertices.ToArray();
        }

        int[] TriangulateSector(Vector2[] vertices)
        {
            if (vertices.Length < 3)
            {
                return new int[0];
            }

            // Simple fan triangulation for now
            var indices = new List<int>();
            
            for (var i = 1; i < vertices.Length - 1; i++)
            {
                indices.Add(0);
                indices.Add(i);
                indices.Add(i + 1);
            }

            
            return indices.ToArray();
        }


        int[] ReverseTriangles(int[] triangles)
        {
            var reversed = new int[triangles.Length];
            for (var i = 0; i < triangles.Length; i += 3)
            {
                reversed[i] = triangles[i];
                reversed[i + 1] = triangles[i + 2];
                reversed[i + 2] = triangles[i + 1];
            }
            return reversed;
        }
    }
}