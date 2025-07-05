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
            mesh.triangles = ReverseTriangles(triangles);
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
            mesh.triangles = triangles;
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
            // Build edge connections to preserve vertex order
            var edges = new Dictionary<Vector2, List<Vector2>>();

            foreach (var lineIndex in sectorLines)
            {
                var linedef = levelModel.linedefs[lineIndex];
                var start = levelModel.vertices[linedef.startVertex].ToVector2();
                var end = levelModel.vertices[linedef.endVertex].ToVector2();

                // Add edges in both directions for now
                if (!edges.ContainsKey(start))
                    edges[start] = new List<Vector2>();
                if (!edges.ContainsKey(end))
                    edges[end] = new List<Vector2>();

                edges[start].Add(end);
                edges[end].Add(start);
            }

            // Find a starting vertex
            if (edges.Count == 0)
                return new Vector2[0];

            var startVertex = edges.Keys.First();
            var orderedVertices = new List<Vector2>();
            var visited = new HashSet<Vector2>();
            var current = startVertex;

            // Walk around the perimeter
            while (orderedVertices.Count < edges.Count)
            {
                orderedVertices.Add(current);
                visited.Add(current);

                // Find the next unvisited vertex
                var found = false;
                foreach (var neighbor in edges[current])
                {
                    if (!visited.Contains(neighbor))
                    {
                        current = neighbor;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    // If we can't continue, just return what we have
                    break;
                }
            }

            return orderedVertices.ToArray();
        }

        int[] TriangulateSector(Vector2[] vertices)
        {
            if (vertices.Length < 3)
                return new int[0];

            // Use ear clipping algorithm for better triangulation
            var indices = new List<int>();
            var vertexList = new List<int>();

            // Initialize vertex lis
            for (var i = 0; i < vertices.Length; i++)
                vertexList.Add(i);

            // Check if vertices are in clockwise order
            var area = 0f;
            for (var i = 0; i < vertices.Length; i++)
            {
                var v1 = vertices[i];
                var v2 = vertices[(i + 1) % vertices.Length];
                area += (v2.x - v1.x) * (v2.y + v1.y);
            }

            var clockwise = area > 0;

            // Ear clipping
            while (vertexList.Count > 3)
            {
                var earFound = false;

                for (var i = 0; i < vertexList.Count; i++)
                {
                    var prev = vertexList[(i + vertexList.Count - 1) % vertexList.Count];
                    var curr = vertexList[i];
                    var next = vertexList[(i + 1) % vertexList.Count];

                    if (IsEar(vertices, vertexList, prev, curr, next, clockwise))
                    {
                        // Add triangle
                        if (clockwise)
                        {
                            indices.Add(prev);
                            indices.Add(curr);
                            indices.Add(next);
                        }
                        else
                        {
                            indices.Add(prev);
                            indices.Add(next);
                            indices.Add(curr);
                        }

                        // Remove the ear vertex
                        vertexList.RemoveAt(i);
                        earFound = true;
                        break;
                    }
                }

                if (!earFound)
                {
                    // Fallback to fan triangulation if ear clipping fails
                    Debug.LogWarning("Ear clipping failed, falling back to fan triangulation");
                    indices.Clear();
                    for (var i = 1; i < vertices.Length - 1; i++)
                    {
                        indices.Add(0);
                        indices.Add(i);
                        indices.Add(i + 1);
                    }
                    break;
                }
            }

            // Add the last triangle
            if (vertexList.Count == 3)
            {
                if (clockwise)
                {
                    indices.Add(vertexList[0]);
                    indices.Add(vertexList[1]);
                    indices.Add(vertexList[2]);
                }
                else
                {
                    indices.Add(vertexList[0]);
                    indices.Add(vertexList[2]);
                    indices.Add(vertexList[1]);
                }
            }

            return indices.ToArray();
        }

        bool IsEar(Vector2[] vertices, List<int> vertexList, int prev, int curr, int next, bool clockwise)
        {
            var a = vertices[prev];
            var b = vertices[curr];
            var c = vertices[next];

            // Check if the triangle is convex
            var cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            if (clockwise && cross < 0) return false;
            if (!clockwise && cross > 0) return false;

            // Check if any other vertex is inside the triangle
            for (var i = 0; i < vertexList.Count; i++)
            {
                var v = vertexList[i];
                if (v == prev || v == curr || v == next) continue;

                if (IsPointInTriangle(vertices[v], a, b, c))
                    return false;
            }

            return true;
        }

        bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var v0 = c - a;
            var v1 = b - a;
            var v2 = p - a;

            var dot00 = Vector2.Dot(v0, v0);
            var dot01 = Vector2.Dot(v0, v1);
            var dot02 = Vector2.Dot(v0, v2);
            var dot11 = Vector2.Dot(v1, v1);
            var dot12 = Vector2.Dot(v1, v2);

            var invDenom = 1 / (dot00 * dot11 - dot01 * dot01);
            var u = (dot11 * dot02 - dot01 * dot12) * invDenom;
            var v = (dot00 * dot12 - dot01 * dot02) * invDenom;

            return (u >= 0) && (v >= 0) && (u + v <= 1);
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