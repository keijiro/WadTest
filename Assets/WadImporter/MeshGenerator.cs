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
            
            var vertices = ExtractSectorVertices(sectorLines);
            
            if (vertices.Length < 3)
            {
                Debug.LogWarning($"[MeshGenerator] Sector {sectorIndex}: Not enough vertices ({vertices.Length}) for triangulation, skipping floor mesh");
                return null;
            }
            
            var triangles = TriangulateSector(vertices, sectorIndex, "Floor");

            var mesh = new Mesh();
            mesh.name = $"Floor_{sectorIndex}";

            var meshVertices = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];

            for (var i = 0; i < vertices.Length; i++)
            {
                meshVertices[i] = new Vector3(vertices[i].x, sector.FloorHeightUnits, vertices[i].y);
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
            
            if (vertices.Length < 3)
            {
                Debug.LogWarning($"[MeshGenerator] Sector {sectorIndex}: Not enough vertices ({vertices.Length}) for triangulation, skipping ceiling mesh");
                return null;
            }
            
            var triangles = TriangulateSector(vertices, sectorIndex, "Ceiling");

            var mesh = new Mesh();
            mesh.name = $"Ceiling_{sectorIndex}";

            var meshVertices = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];

            for (var i = 0; i < vertices.Length; i++)
            {
                meshVertices[i] = new Vector3(vertices[i].x, sector.CeilingHeightUnits, vertices[i].y);
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

            vertices.Add(new Vector3(startVertex.x, sector.FloorHeightUnits, startVertex.y));
            vertices.Add(new Vector3(endVertex.x, sector.FloorHeightUnits, endVertex.y));
            vertices.Add(new Vector3(endVertex.x, sector.CeilingHeightUnits, endVertex.y));
            vertices.Add(new Vector3(startVertex.x, sector.CeilingHeightUnits, startVertex.y));

            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(lineLength / 64f, 0));
            uvs.Add(new Vector2(lineLength / 64f, sector.WallHeight / 64f));
            uvs.Add(new Vector2(0, sector.WallHeight / 64f));

            if (direction > 0)
            {
                triangles.AddRange(new[] { baseIndex, baseIndex + 2, baseIndex + 1 });
                triangles.AddRange(new[] { baseIndex, baseIndex + 3, baseIndex + 2 });
            }
            else
            {
                triangles.AddRange(new[] { baseIndex, baseIndex + 1, baseIndex + 2 });
                triangles.AddRange(new[] { baseIndex, baseIndex + 2, baseIndex + 3 });
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

            vertices.Add(new Vector3(startVertex.x, bottomHeight, startVertex.y));
            vertices.Add(new Vector3(endVertex.x, bottomHeight, endVertex.y));
            vertices.Add(new Vector3(endVertex.x, topHeight, endVertex.y));
            vertices.Add(new Vector3(startVertex.x, topHeight, startVertex.y));

            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(lineLength / 64f, 0));
            uvs.Add(new Vector2(lineLength / 64f, segmentHeight / 64f));
            uvs.Add(new Vector2(0, segmentHeight / 64f));

            if (direction > 0)
            {
                triangles.AddRange(new[] { baseIndex, baseIndex + 2, baseIndex + 1 });
                triangles.AddRange(new[] { baseIndex, baseIndex + 3, baseIndex + 2 });
            }
            else
            {
                triangles.AddRange(new[] { baseIndex, baseIndex + 1, baseIndex + 2 });
                triangles.AddRange(new[] { baseIndex, baseIndex + 2, baseIndex + 3 });
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
            if (sectorLines.Count < 3)
                return new Vector2[0];
            
            // Build edge connectivity map
            var edges = new Dictionary<Vector2, List<Vector2>>();
            
            foreach (var lineIndex in sectorLines)
            {
                var linedef = levelModel.linedefs[lineIndex];
                var v1 = levelModel.vertices[linedef.startVertex].ToVector2();
                var v2 = levelModel.vertices[linedef.endVertex].ToVector2();
                
                if (!edges.ContainsKey(v1))
                    edges[v1] = new List<Vector2>();
                if (!edges.ContainsKey(v2))
                    edges[v2] = new List<Vector2>();
                
                edges[v1].Add(v2);
                edges[v2].Add(v1);
            }
            
            if (edges.Count < 3)
                return new Vector2[0];
            
            // Find the leftmost vertex as starting point
            var start = edges.Keys.OrderBy(v => v.x).ThenBy(v => v.y).First();
            
            // Trace the boundary
            var result = new List<Vector2>();
            var current = start;
            var previous = new Vector2(float.MinValue, current.y); // Virtual point to the left
            
            do
            {
                result.Add(current);
                
                // Find the next vertex with the rightmost turn
                Vector2 next = Vector2.zero;
                var bestAngle = float.MinValue;
                
                foreach (var neighbor in edges[current])
                {
                    if (result.Count > 1 && neighbor == result[result.Count - 2])
                        continue; // Don't go back
                    
                    var angle = Vector2.SignedAngle(current - previous, neighbor - current);
                    if (angle > bestAngle)
                    {
                        bestAngle = angle;
                        next = neighbor;
                    }
                }
                
                if (next == Vector2.zero || (next == start && result.Count > 2))
                    break;
                
                previous = current;
                current = next;
                
            } while (result.Count < edges.Count);
            
            return result.ToArray();
        }
        
        List<Vector2> ComputeConvexHull(List<Vector2> points)
        {
            if (points.Count <= 3)
                return points;
            
            // Sort points lexicographically
            points.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            
            // Build lower hull
            var hull = new List<Vector2>();
            for (var i = 0; i < points.Count; i++)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], points[i]) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(points[i]);
            }
            
            // Build upper hull
            var t = hull.Count + 1;
            for (var i = points.Count - 2; i >= 0; i--)
            {
                while (hull.Count >= t && Cross(hull[hull.Count - 2], hull[hull.Count - 1], points[i]) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(points[i]);
            }
            
            hull.RemoveAt(hull.Count - 1); // Remove last point as it's same as first
            return hull;
        }
        
        float Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }

        int[] TriangulateSector(Vector2[] vertices, int sectorIndex = -1, string meshType = "Unknown")
        {
            if (vertices.Length < 3)
                return new int[0];

            var triangles = new List<int>();
            
            // Check if polygon is convex
            if (IsConvexPolygon(vertices))
            {
                // Simple fan triangulation for convex polygons
                for (var i = 1; i < vertices.Length - 1; i++)
                {
                    triangles.Add(0);
                    triangles.Add(i);
                    triangles.Add(i + 1);
                }
                return triangles.ToArray();
            }
            
            // For concave polygons, use improved ear clipping
            var indices = new List<int>(vertices.Length);
            for (var i = 0; i < vertices.Length; i++)
                indices.Add(i);
            
            var attempt = 0;
            while (indices.Count > 3 && attempt < vertices.Length * 2)
            {
                attempt++;
                var earFound = false;
                
                for (var i = 0; i < indices.Count; i++)
                {
                    var prev = indices[(i + indices.Count - 1) % indices.Count];
                    var curr = indices[i];
                    var next = indices[(i + 1) % indices.Count];
                    
                    if (IsValidEar(vertices, indices, prev, curr, next))
                    {
                        triangles.Add(prev);
                        triangles.Add(curr);
                        triangles.Add(next);
                        
                        indices.RemoveAt(i);
                        earFound = true;
                        break;
                    }
                }
                
                if (!earFound)
                {
                    Debug.LogWarning($"[TriangulateSector] Sector {sectorIndex} ({meshType}): Ear clipping stuck, using fan triangulation");
                    triangles.Clear();
                    
                    // Fallback to fan triangulation
                    for (var i = 1; i < vertices.Length - 1; i++)
                    {
                        triangles.Add(0);
                        triangles.Add(i);
                        triangles.Add(i + 1);
                    }
                    break;
                }
            }
            
            // Add final triangle
            if (indices.Count == 3)
            {
                triangles.Add(indices[0]);
                triangles.Add(indices[1]);
                triangles.Add(indices[2]);
            }
            
            return triangles.ToArray();
        }
        
        bool IsValidEar(Vector2[] vertices, List<int> indices, int prev, int curr, int next)
        {
            var a = vertices[prev];
            var b = vertices[curr];
            var c = vertices[next];
            
            // Check if angle is convex
            var cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            if (cross >= 0) // Reflex angle
                return false;
            
            // Check if any other vertex is inside the triangle
            for (var i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                if (idx == prev || idx == curr || idx == next)
                    continue;
                
                if (IsPointInTriangle(vertices[idx], a, b, c))
                    return false;
            }
            
            return true;
        }
        
        List<List<int>> DecomposeToConvexPolygons(Vector2[] vertices)
        {
            // Simple approach: if polygon is already convex, return as-is
            if (IsConvexPolygon(vertices))
            {
                var indices = new List<int>();
                for (var i = 0; i < vertices.Length; i++)
                    indices.Add(i);
                return new List<List<int>> { indices };
            }
            
            // For now, return the original polygon and let fan triangulation handle it
            // This is a simplified implementation - a full convex decomposition is complex
            var result = new List<int>();
            for (var i = 0; i < vertices.Length; i++)
                result.Add(i);
            return new List<List<int>> { result };
        }
        
        bool IsConvexPolygon(Vector2[] vertices)
        {
            if (vertices.Length < 3) return false;
            
            var sign = 0f;
            for (var i = 0; i < vertices.Length; i++)
            {
                var p1 = vertices[i];
                var p2 = vertices[(i + 1) % vertices.Length];
                var p3 = vertices[(i + 2) % vertices.Length];
                
                var cross = (p2.x - p1.x) * (p3.y - p1.y) - (p2.y - p1.y) * (p3.x - p1.x);
                
                if (Mathf.Abs(cross) > 1e-6) // Ignore nearly collinear points
                {
                    if (sign == 0)
                        sign = Mathf.Sign(cross);
                    else if (Mathf.Sign(cross) != sign)
                        return false; // Found a reflex angle
                }
            }
            return true;
        }
        
        Vector2 CalculateCentroid(Vector2[] vertices)
        {
            var centroid = Vector2.zero;
            for (var i = 0; i < vertices.Length; i++)
                centroid += vertices[i];
            return centroid / vertices.Length;
        }

        bool IsEar(Vector2[] vertices, List<int> vertexList, int prev, int curr, int next, bool clockwise)
        {
            var a = vertices[prev];
            var b = vertices[curr];
            var c = vertices[next];

            // Check if the triangle is convex
            var cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            var isConvex = clockwise ? cross >= 0 : cross <= 0;
            
            if (!isConvex)
            {
                // Debug.Log($"[IsEar] Vertex {curr} not convex, cross: {cross}, clockwise: {clockwise}");
                return false;
            }

            // Check if any other vertex is inside the triangle
            var insideVertices = new List<int>();
            for (var i = 0; i < vertexList.Count; i++)
            {
                var v = vertexList[i];
                if (v == prev || v == curr || v == next) continue;

                if (IsPointInTriangle(vertices[v], a, b, c))
                {
                    insideVertices.Add(v);
                }
            }

            if (insideVertices.Count > 0)
            {
                // Debug.Log($"[IsEar] Vertex {curr} has {insideVertices.Count} vertices inside triangle");
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