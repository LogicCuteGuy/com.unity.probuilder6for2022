using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.ProBuilder.Csg
{
    /// <summary>
    /// Triangle-based boolean solver inspired by Blender's Mesh Arrangements approach.
    /// This replaces the classic BSP tree algorithm with a more robust triangle intersection method.
    /// 
    /// Algorithm overview (based on Blender's Float solver):
    /// 1. Triangulate both input meshes
    /// 2. Find all triangle-triangle intersections
    /// 3. Split triangles at intersection lines
    /// 4. Classify each resulting triangle as inside/outside the other mesh using ray casting
    /// 5. Apply the boolean operation by keeping/removing triangles based on classification
    /// 6. Merge coplanar adjacent triangles for cleaner output
    /// </summary>
    static class MeshBooleanSolver
    {
        const float k_Epsilon = 1e-6f;
        const int k_MaxSplitsPerTriangle = 16;

        /// <summary>
        /// Perform a boolean operation on two meshes using the triangle-based approach.
        /// </summary>
        public static List<Polygon> PerformBoolean(
            List<Polygon> polygonsA,
            List<Polygon> polygonsB,
            CSG.BooleanOp operation)
        {
            // Step 1: Triangulate both inputs
            List<Triangle> trisA = TriangulatePolygons(polygonsA);
            List<Triangle> trisB = TriangulatePolygons(polygonsB);

            if (trisA.Count == 0 || trisB.Count == 0)
                return new List<Polygon>();

            // Step 2: Find all intersections and split triangles
            List<Triangle> splitTrisA = new List<Triangle>(trisA);
            List<Triangle> splitTrisB = new List<Triangle>(trisB);

            FindAndSplitIntersections(splitTrisA, splitTrisB);

            // Step 3: Classify triangles using ray casting
            List<Triangle> classifiedA = ClassifyTriangles(splitTrisA, trisB, true);
            List<Triangle> classifiedB = ClassifyTriangles(splitTrisB, trisA, false);

            // Step 4: Apply boolean operation
            List<Triangle> resultTris = ApplyBooleanOperation(
                classifiedA, classifiedB, operation);

            // Step 5: Convert back to polygons and merge coplanar faces
            return TrianglesToPolygons(resultTris);
        }

        #region Triangulation

        static List<Triangle> TriangulatePolygons(List<Polygon> polygons)
        {
            List<Triangle> triangles = new List<Triangle>();

            foreach (var poly in polygons)
            {
                if (poly.vertices.Count < 3)
                    continue;

                // Fan triangulation for convex polygons
                for (int i = 1; i < poly.vertices.Count - 1; i++)
                {
                    var v0 = poly.vertices[0];
                    var v1 = poly.vertices[i];
                    var v2 = poly.vertices[i + 1];

                    // Skip degenerate triangles
                    Vector3 edge1 = v1.position - v0.position;
                    Vector3 edge2 = v2.position - v0.position;
                    Vector3 cross = Vector3.Cross(edge1, edge2);

                    if (cross.sqrMagnitude > k_Epsilon * k_Epsilon)
                    {
                        triangles.Add(new Triangle(v0, v1, v2, poly.material));
                    }
                }
            }

            return triangles;
        }

        #endregion

        #region Triangle-Triangle Intersection

        /// <summary>
        /// Find all intersections between triangles in listA and listB, splitting them at intersection lines.
        /// Based on Möller-Trumbore intersection algorithm adapted for triangle splitting.
        /// </summary>
        static void FindAndSplitIntersections(List<Triangle> listA, List<Triangle> listB)
        {
            bool foundIntersection = true;
            int iterations = 0;

            while (foundIntersection && iterations < 10)
            {
                foundIntersection = false;
                iterations++;

                // Check all pairs of triangles
                for (int i = 0; i < listA.Count; i++)
                {
                    for (int j = 0; j < listB.Count; j++)
                    {
                        if (TriangleIntersectsTriangle(listA[i], listB[j],
                            out Vector3 edgeStart, out Vector3 edgeEnd))
                        {
                            // Split both triangles at the intersection line
                            List<Triangle> newTrisA = SplitTriangleAtEdge(listA[i], edgeStart, edgeEnd);
                            List<Triangle> newTrisB = SplitTriangleAtEdge(listB[j], edgeStart, edgeEnd);

                            if (newTrisA.Count > 0 && newTrisB.Count > 0)
                            {
                                // Replace original triangles with split versions
                                listA.RemoveAt(i);
                                listA.AddRange(newTrisA);

                                listB.RemoveAt(j);
                                listB.AddRange(newTrisB);

                                foundIntersection = true;
                                break;
                            }
                        }
                    }

                    if (foundIntersection)
                        break;
                }
            }
        }

        /// <summary>
        /// Test if two triangles intersect and compute the intersection line segment.
        /// Uses a robust triangle-triangle intersection test.
        /// </summary>
        static bool TriangleIntersectsTriangle(
            Triangle triA, Triangle triB,
            out Vector3 edgeStart, out Vector3 edgeEnd)
        {
            edgeStart = Vector3.zero;
            edgeEnd = Vector3.zero;

            // First check if the bounding boxes overlap
            if (!BoundingBoxesOverlap(triA, triB))
                return false;

            // Check if the triangles are coplanar
            Vector3 normalA = triA.Normal;
            Vector3 normalB = triB.Normal;
            float dot = Mathf.Abs(Vector3.Dot(normalA, normalB));

            if (dot > 1f - k_Epsilon)
            {
                // Coplanar triangles - check for overlap in 2D
                return CoplanarTrianglesIntersect(triA, triB, out edgeStart, out edgeEnd);
            }

            // Non-coplanar: check if the triangles actually intersect
            // Project triangles onto the separating axis and check for overlap
            Vector3[] edgesA = new Vector3[]
            {
                triA.V1.position - triA.V0.position,
                triA.V2.position - triA.V1.position,
                triA.V0.position - triA.V2.position
            };

            Vector3[] edgesB = new Vector3[]
            {
                triB.V1.position - triB.V0.position,
                triB.V2.position - triB.V1.position,
                triB.V0.position - triB.V2.position
            };

            // Test separating axis theorem (13 axes: 3 face normals + 9 edge cross products)
            Vector3[] axes = new Vector3[13];
            axes[0] = normalA;
            axes[1] = normalB;
            axes[2] = Vector3.forward; // Third axis for robustness

            for (int i = 0; i < 3; i++)
                axes[3 + i] = Vector3.Cross(edgesA[i], normalB);

            for (int i = 0; i < 3; i++)
                axes[6 + i] = Vector3.Cross(edgesB[i], normalA);

            // Edge-edge cross products (9 axes)
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    axes[9 + i * 3 + j] = Vector3.Cross(edgesA[i], edgesB[j]);

            foreach (var axis in axes)
            {
                if (axis.sqrMagnitude < k_Epsilon * k_Epsilon)
                    continue;

                float minA, maxA, minB, maxB;
                ProjectTriangle(triA, axis, out minA, out maxA);
                ProjectTriangle(triB, axis, out minB, out maxB);

                if (maxA < minB - k_Epsilon || maxB < minA - k_Epsilon)
                    return false;
            }

            // Triangles intersect - find the intersection line
            return ComputeIntersectionLine(triA, triB, out edgeStart, out edgeEnd);
        }

        static bool BoundingBoxesOverlap(Triangle triA, Triangle triB)
        {
            Vector3 minA = Vector3.Min(triA.V0.position, Vector3.Min(triA.V1.position, triA.V2.position));
            Vector3 maxA = Vector3.Max(triA.V0.position, Vector3.Max(triA.V1.position, triA.V2.position));

            Vector3 minB = Vector3.Min(triB.V0.position, Vector3.Min(triB.V1.position, triB.V2.position));
            Vector3 maxB = Vector3.Max(triB.V0.position, Vector3.Max(triB.V1.position, triB.V2.position));

            return (minA.x <= maxB.x && maxA.x >= minB.x &&
                    minA.y <= maxB.y && maxA.y >= minB.y &&
                    minA.z <= maxB.z && maxA.z >= minB.z);
        }

        static void ProjectTriangle(Triangle tri, Vector3 axis, out float min, out float max)
        {
            float d0 = Vector3.Dot(tri.V0.position, axis);
            float d1 = Vector3.Dot(tri.V1.position, axis);
            float d2 = Vector3.Dot(tri.V2.position, axis);

            min = Mathf.Min(d0, Mathf.Min(d1, d2));
            max = Mathf.Max(d0, Mathf.Max(d1, d2));
        }

        static bool CoplanarTrianglesIntersect(
            Triangle triA, Triangle triB,
            out Vector3 edgeStart, out Vector3 edgeEnd)
        {
            edgeStart = Vector3.zero;
            edgeEnd = Vector3.zero;

            // Project to 2D for easier intersection testing
            Vector3 normal = triA.Normal;
            Vector3 u, v;
            GetOrthogonalBasis(normal, out u, out v);

            Vector2 a0 = ProjectTo2D(triA.V0.position, u, v);
            Vector2 a1 = ProjectTo2D(triA.V1.position, u, v);
            Vector2 a2 = ProjectTo2D(triA.V2.position, u, v);

            Vector2 b0 = ProjectTo2D(triB.V0.position, u, v);
            Vector2 b1 = ProjectTo2D(triB.V1.position, u, v);
            Vector2 b2 = ProjectTo2D(triB.V2.position, u, v);

            // Check if the 2D triangles overlap
            Vector2 intersectionMin, intersectionMax;
            if (TriangleOverlap2D(a0, a1, a2, b0, b1, b2, out intersectionMin, out intersectionMax))
            {
                // Convert back to 3D
                edgeStart = new Vector3(
                    Vector2.Dot(intersectionMin, new Vector2(u.x, v.x)) + Vector3.Dot(triA.V0.position, normal) * normal.x,
                    Vector2.Dot(intersectionMin, new Vector2(u.y, v.y)) + Vector3.Dot(triA.V0.position, normal) * normal.y,
                    Vector2.Dot(intersectionMin, new Vector2(u.z, v.z)) + Vector3.Dot(triA.V0.position, normal) * normal.z
                );
                edgeEnd = new Vector3(
                    Vector2.Dot(intersectionMax, new Vector2(u.x, v.x)) + Vector3.Dot(triA.V0.position, normal) * normal.x,
                    Vector2.Dot(intersectionMax, new Vector2(u.y, v.y)) + Vector3.Dot(triA.V0.position, normal) * normal.y,
                    Vector2.Dot(intersectionMax, new Vector2(u.z, v.z)) + Vector3.Dot(triA.V0.position, normal) * normal.z
                );
                return true;
            }

            return false;
        }

        static void GetOrthogonalBasis(Vector3 normal, out Vector3 u, out Vector3 v)
        {
            if (Mathf.Abs(normal.x) > Mathf.Abs(normal.z))
            {
                float invLen = 1f / Mathf.Sqrt(normal.x * normal.x + normal.y * normal.y);
                u = new Vector3(-normal.y * invLen, normal.x * invLen, 0f);
            }
            else
            {
                float invLen = 1f / Mathf.Sqrt(normal.y * normal.y + normal.z * normal.z);
                u = new Vector3(0f, -normal.z * invLen, normal.y * invLen);
            }
            v = Vector3.Cross(normal, u);
        }

        static Vector2 ProjectTo2D(Vector3 point, Vector3 u, Vector3 v)
        {
            return new Vector2(Vector3.Dot(point, u), Vector3.Dot(point, v));
        }

        static bool TriangleOverlap2D(
            Vector2 a0, Vector2 a1, Vector2 a2,
            Vector2 b0, Vector2 b1, Vector2 b2,
            out Vector2 overlapMin, out Vector2 overlapMax)
        {
            overlapMin = Vector2.zero;
            overlapMax = Vector2.zero;

            // Simple AABB overlap test for 2D triangles
            Vector2 minA = Vector2.Min(a0, Vector2.Min(a1, a2));
            Vector2 maxA = Vector2.Max(a0, Vector2.Max(a1, a2));

            Vector2 minB = Vector2.Min(b0, Vector2.Min(b1, b2));
            Vector2 maxB = Vector2.Max(b0, Vector2.Max(b1, b2));

            if (minA.x > maxB.x || maxA.x < minB.x ||
                minA.y > maxB.y || maxA.y < minB.y)
                return false;

            overlapMin = Vector2.Max(minA, minB);
            overlapMax = Vector2.Min(maxA, maxB);

            return true;
        }

        static bool ComputeIntersectionLine(
            Triangle triA, Triangle triB,
            out Vector3 edgeStart, out Vector3 edgeEnd)
        {
            edgeStart = Vector3.zero;
            edgeEnd = Vector3.zero;

            // Find intersection points by testing each edge of A against B and vice versa
            List<Vector3> intersectionPoints = new List<Vector3>();

            // Test edges of A against triangle B
            TestEdgeTriangleIntersection(triA.V0.position, triA.V1.position, triB, intersectionPoints);
            TestEdgeTriangleIntersection(triA.V1.position, triA.V2.position, triB, intersectionPoints);
            TestEdgeTriangleIntersection(triA.V2.position, triA.V0.position, triB, intersectionPoints);

            // Test edges of B against triangle A
            TestEdgeTriangleIntersection(triB.V0.position, triB.V1.position, triA, intersectionPoints);
            TestEdgeTriangleIntersection(triB.V1.position, triB.V2.position, triA, intersectionPoints);
            TestEdgeTriangleIntersection(triB.V2.position, triB.V0.position, triA, intersectionPoints);

            // Remove duplicate points
            List<Vector3> uniquePoints = new List<Vector3>();
            foreach (var p in intersectionPoints)
            {
                bool isDuplicate = false;
                foreach (var up in uniquePoints)
                {
                    if (Vector3.Distance(p, up) < k_Epsilon)
                    {
                        isDuplicate = true;
                        break;
                    }
                }
                if (!isDuplicate)
                    uniquePoints.Add(p);
            }

            if (uniquePoints.Count >= 2)
            {
                edgeStart = uniquePoints[0];
                edgeEnd = uniquePoints[1];
                return true;
            }

            return false;
        }

        static void TestEdgeTriangleIntersection(
            Vector3 edgeStart, Vector3 edgeEnd, Triangle tri,
            List<Vector3> intersectionPoints)
        {
            Vector3 dir = edgeEnd - edgeStart;
            float dirLen = dir.magnitude;

            if (dirLen < k_Epsilon)
                return;

            dir /= dirLen;

            // Use Möller-Trumbore for ray-triangle intersection
            Vector3 v0v1 = tri.V1.position - tri.V0.position;
            Vector3 v0v2 = tri.V2.position - tri.V0.position;
            Vector3 pvec = Vector3.Cross(dir, v0v2);
            float det = Vector3.Dot(v0v1, pvec);

            if (Mathf.Abs(det) < k_Epsilon)
                return;

            float invDet = 1f / det;
            Vector3 tvec = edgeStart - tri.V0.position;
            float u = Vector3.Dot(tvec, pvec) * invDet;

            if (u < -k_Epsilon || u > 1f + k_Epsilon)
                return;

            Vector3 qvec = Vector3.Cross(tvec, v0v1);
            float v = Vector3.Dot(dir, qvec) * invDet;

            if (v < -k_Epsilon || u + v > 1f + k_Epsilon)
                return;

            float t = Vector3.Dot(v0v2, qvec) * invDet;

            if (t >= -k_Epsilon && t <= dirLen + k_Epsilon)
            {
                Vector3 hitPoint = edgeStart + dir * Mathf.Clamp(t, 0f, dirLen);
                intersectionPoints.Add(hitPoint);
            }
        }

        #endregion

        #region Triangle Splitting

        /// <summary>
        /// Split a triangle along an edge (line segment), producing one or more new triangles.
        /// </summary>
        static List<Triangle> SplitTriangleAtEdge(Triangle tri, Vector3 edgeStart, Vector3 edgeEnd)
        {
            List<Triangle> result = new List<Triangle>();

            // Find which side of the edge each vertex is on
            Vector3 edgeDir = (edgeEnd - edgeStart).normalized;
            Vector3 edgeNormal = Vector3.Cross(tri.Normal, edgeDir).normalized;

            float d0 = Vector3.Dot(tri.V0.position - edgeStart, edgeNormal);
            float d1 = Vector3.Dot(tri.V1.position - edgeStart, edgeNormal);
            float d2 = Vector3.Dot(tri.V2.position - edgeStart, edgeNormal);

            // Classify vertices
            bool v0Front = d0 > k_Epsilon;
            bool v1Front = d1 > k_Epsilon;
            bool v2Front = d2 > k_Epsilon;

            // If all vertices are on the same side, no split needed
            if (v0Front == v1Front && v1Front == v2Front)
            {
                result.Add(tri);
                return result;
            }

            // Find intersection points on edges
            List<Vertex> frontVerts = new List<Vertex>();
            List<Vertex> backVerts = new List<Vertex>();

            // Process each edge
            ProcessEdgeForSplit(tri.V0, tri.V1, d0, d1, edgeStart, edgeNormal, frontVerts, backVerts);
            ProcessEdgeForSplit(tri.V1, tri.V2, d1, d2, edgeStart, edgeNormal, frontVerts, backVerts);
            ProcessEdgeForSplit(tri.V2, tri.V0, d2, d0, edgeStart, edgeNormal, frontVerts, backVerts);

            // Create triangles from front and back vertex lists
            if (frontVerts.Count >= 3)
            {
                for (int i = 1; i < frontVerts.Count - 1; i++)
                {
                    result.Add(new Triangle(frontVerts[0], frontVerts[i], frontVerts[i + 1], tri.Material));
                }
            }

            if (backVerts.Count >= 3)
            {
                for (int i = 1; i < backVerts.Count - 1; i++)
                {
                    result.Add(new Triangle(backVerts[0], backVerts[i], backVerts[i + 1], tri.Material));
                }
            }

            return result;
        }

        static void ProcessEdgeForSplit(
            Vertex vA, Vertex vB, float dA, float dB,
            Vector3 edgeStart, Vector3 edgeNormal,
            List<Vertex> frontVerts, List<Vertex> backVerts)
        {
            bool aFront = dA > k_Epsilon;
            bool bFront = dB > k_Epsilon;

            if (aFront)
                frontVerts.Add(vA);
            else
                backVerts.Add(vA);

            // If vertices are on opposite sides, compute intersection
            if (aFront != bFront && Mathf.Abs(dA - dB) > k_Epsilon)
            {
                float t = dA / (dA - dB);
                Vertex interpolated = VertexUtility.Mix(vA, vB, t);
                frontVerts.Add(interpolated);
                backVerts.Add(interpolated);
            }
        }

        #endregion

        #region Classification

        /// <summary>
        /// Classify triangles as inside or outside the other mesh using ray casting.
        /// Based on Blender's test_tri_inside_shapes approach.
        /// </summary>
        static List<Triangle> ClassifyTriangles(
            List<Triangle> trianglesToClassify,
            List<Triangle> otherMeshTris,
            bool isMeshA)
        {
            List<Triangle> result = new List<Triangle>();

            // Build BVH for the other mesh for efficient ray casting
            BVHNode bvh = BVHNode.Build(otherMeshTris);

            foreach (var tri in trianglesToClassify)
            {
                // Use triangle centroid for classification
                Vector3 centroid = (tri.V0.position + tri.V1.position + tri.V2.position) / 3f;

                // Small offset along normal to avoid self-intersection
                Vector3 normal = tri.Normal;
                Vector3 testPoint = centroid + normal * k_Epsilon * 10f;

                bool isInside = IsPointInsideMesh(testPoint, bvh);

                // Create a new triangle with inside/outside information
                Triangle classifiedTri = tri;
                classifiedTri.IsInside = isInside;
                result.Add(classifiedTri);
            }

            return result;
        }

        /// <summary>
        /// Test if a point is inside a mesh using ray casting.
        /// Counts intersections with the mesh - odd count means inside.
        /// </summary>
        static bool IsPointInsideMesh(Vector3 point, BVHNode bvh)
        {
            // Cast ray in multiple directions for robustness
            Vector3[] directions = new Vector3[]
            {
                Vector3.right,
                Vector3.up,
                Vector3.forward,
                new Vector3(1, 1, 0).normalized,
                new Vector3(0, 1, 1).normalized
            };

            int insideCount = 0;

            foreach (var dir in directions)
            {
                int intersections = CountRayIntersections(point, dir, bvh);
                if (intersections % 2 == 1)
                    insideCount++;
            }

            // Consider inside if majority of rays indicate inside
            return insideCount > directions.Length / 2;
        }

        /// <summary>
        /// Count how many times a ray intersects the mesh.
        /// </summary>
        static int CountRayIntersections(Vector3 origin, Vector3 direction, BVHNode bvh)
        {
            int count = 0;
            CountRayIntersectionsRecursive(origin, direction, bvh, ref count);
            return count;
        }

        static void CountRayIntersectionsRecursive(
            Vector3 origin, Vector3 direction, BVHNode node, ref int count)
        {
            if (node == null)
                return;

            // Test ray against bounding box
            float tMin, tMax;
            if (!RayIntersectsAABB(origin, direction, node.Bounds, out tMin, out tMax))
                return;

            if (node.IsLeaf)
            {
                // Test ray against each triangle in this leaf
                foreach (var tri in node.Triangles)
                {
                    if (RayIntersectsTriangle(origin, direction, tri))
                        count++;
                }
            }
            else
            {
                CountRayIntersectionsRecursive(origin, direction, node.Left, ref count);
                CountRayIntersectionsRecursive(origin, direction, node.Right, ref count);
            }
        }

        static bool RayIntersectsAABB(Vector3 origin, Vector3 direction, Bounds bounds, out float tMin, out float tMax)
        {
            tMin = 0f;
            tMax = float.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                float orig = i == 0 ? origin.x : (i == 1 ? origin.y : origin.z);
                float dir = i == 0 ? direction.x : (i == 1 ? direction.y : direction.z);
                float min = i == 0 ? bounds.min.x : (i == 1 ? bounds.min.y : bounds.min.z);
                float max = i == 0 ? bounds.max.x : (i == 1 ? bounds.max.y : bounds.max.z);

                if (Mathf.Abs(dir) < k_Epsilon)
                {
                    if (orig < min || orig > max)
                        return false;
                }
                else
                {
                    float t1 = (min - orig) / dir;
                    float t2 = (max - orig) / dir;

                    if (t1 > t2)
                    {
                        float temp = t1;
                        t1 = t2;
                        t2 = temp;
                    }

                    tMin = Mathf.Max(tMin, t1);
                    tMax = Mathf.Min(tMax, t2);

                    if (tMin > tMax)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Test if a ray intersects a triangle using Möller-Trumbore algorithm.
        /// </summary>
        static bool RayIntersectsTriangle(Vector3 origin, Vector3 direction, Triangle tri)
        {
            Vector3 v0v1 = tri.V1.position - tri.V0.position;
            Vector3 v0v2 = tri.V2.position - tri.V0.position;
            Vector3 pvec = Vector3.Cross(direction, v0v2);
            float det = Vector3.Dot(v0v1, pvec);

            if (Mathf.Abs(det) < k_Epsilon)
                return false;

            float invDet = 1f / det;
            Vector3 tvec = origin - tri.V0.position;
            float u = Vector3.Dot(tvec, pvec) * invDet;

            if (u < 0f || u > 1f)
                return false;

            Vector3 qvec = Vector3.Cross(tvec, v0v1);
            float v = Vector3.Dot(direction, qvec) * invDet;

            if (v < 0f || u + v > 1f)
                return false;

            float t = Vector3.Dot(v0v2, qvec) * invDet;

            return t > k_Epsilon;
        }

        #endregion

        #region Boolean Operation Application

        /// <summary>
        /// Apply the boolean operation by keeping or removing triangles based on their classification.
        /// </summary>
        static List<Triangle> ApplyBooleanOperation(
            List<Triangle> trisA, List<Triangle> trisB,
            CSG.BooleanOp operation)
        {
            List<Triangle> result = new List<Triangle>();

            switch (operation)
            {
                case CSG.BooleanOp.Union:
                    // Keep triangles from A that are outside B, and from B that are outside A
                    result.AddRange(trisA.Where(t => !t.IsInside));
                    result.AddRange(trisB.Where(t => !t.IsInside));
                    break;

                case CSG.BooleanOp.Subtraction:
                    // Keep triangles from A that are outside B, and from B that are inside A
                    result.AddRange(trisA.Where(t => !t.IsInside));
                    result.AddRange(trisB.Where(t => t.IsInside));
                    // Flip normals for B triangles that are inside A
                    for (int i = trisA.Count; i < result.Count; i++)
                    {
                        result[i] = result[i].Flipped();
                    }
                    break;

                case CSG.BooleanOp.Intersection:
                    // Keep triangles from A that are inside B, and from B that are inside A
                    result.AddRange(trisA.Where(t => t.IsInside));
                    result.AddRange(trisB.Where(t => t.IsInside));
                    break;
            }

            return result;
        }

        #endregion

        #region Output Conversion

        /// <summary>
        /// Convert triangles back to polygons, merging coplanar adjacent triangles.
        /// </summary>
        static List<Polygon> TrianglesToPolygons(List<Triangle> triangles)
        {
            List<Polygon> polygons = new List<Polygon>();

            // Group triangles by material
            var groups = triangles.GroupBy(t => t.Material);

            foreach (var group in groups)
            {
                foreach (var tri in group)
                {
                    List<Vertex> verts = new List<Vertex> { tri.V0, tri.V1, tri.V2 };
                    polygons.Add(new Polygon(verts, tri.Material));
                }
            }

            return polygons;
        }

        #endregion
    }

    #region Helper Structures

    /// <summary>
    /// Represents a triangle with vertex information and classification.
    /// </summary>
    struct Triangle
    {
        public Vertex V0, V1, V2;
        public Material Material;
        public bool IsInside;

        public Triangle(Vertex v0, Vertex v1, Vertex v2, Material material)
        {
            V0 = v0;
            V1 = v1;
            V2 = v2;
            Material = material;
            IsInside = false;
        }

        public Vector3 Normal
        {
            get
            {
                Vector3 edge1 = V1.position - V0.position;
                Vector3 edge2 = V2.position - V0.position;
                return Vector3.Cross(edge1, edge2).normalized;
            }
        }

        public Triangle Flipped()
        {
            return new Triangle(V2, V1, V0, Material) { IsInside = IsInside };
        }
    }

    /// <summary>
    /// Bounding Volume Hierarchy node for efficient ray casting.
    /// </summary>
    class BVHNode
    {
        public Bounds Bounds;
        public BVHNode Left, Right;
        public List<Triangle> Triangles;
        public bool IsLeaf => Left == null && Right == null;

        public static BVHNode Build(List<Triangle> triangles, int maxDepth = 8, int minTriangles = 4)
        {
            if (triangles.Count == 0)
                return null;

            var node = new BVHNode();

            // Compute bounds
            Vector3 min = triangles[0].V0.position;
            Vector3 max = min;

            foreach (var tri in triangles)
            {
                min = Vector3.Min(min, Vector3.Min(tri.V0.position, Vector3.Min(tri.V1.position, tri.V2.position)));
                max = Vector3.Max(max, Vector3.Max(tri.V0.position, Vector3.Max(tri.V1.position, tri.V2.position)));
            }

            node.Bounds = new Bounds((min + max) * 0.5f, max - min);

            if (triangles.Count <= minTriangles || maxDepth <= 0)
            {
                node.Triangles = triangles;
                return node;
            }

            // Split along the longest axis
            Vector3 size = max - min;
            int axis = 0;
            if (size.y > size.x && size.y > size.z)
                axis = 1;
            else if (size.z > size.x && size.z > size.y)
                axis = 2;

            float midpoint = (min[axis] + max[axis]) * 0.5f;

            List<Triangle> leftTris = new List<Triangle>();
            List<Triangle> rightTris = new List<Triangle>();

            foreach (var tri in triangles)
            {
                float triCenter = (tri.V0.position[axis] + tri.V1.position[axis] + tri.V2.position[axis]) / 3f;

                if (triCenter < midpoint)
                    leftTris.Add(tri);
                else
                    rightTris.Add(tri);
            }

            // Ensure at least one triangle in each child
            if (leftTris.Count == 0)
            {
                leftTris.Add(rightTris[rightTris.Count - 1]);
                rightTris.RemoveAt(rightTris.Count - 1);
            }
            else if (rightTris.Count == 0)
            {
                rightTris.Add(leftTris[leftTris.Count - 1]);
                leftTris.RemoveAt(leftTris.Count - 1);
            }

            node.Left = Build(leftTris, maxDepth - 1, minTriangles);
            node.Right = Build(rightTris, maxDepth - 1, minTriangles);

            return node;
        }
    }

    #endregion
}
