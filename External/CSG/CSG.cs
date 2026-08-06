// Boolean (CSG) operations for Unity ProBuilder.
//
// This implementation uses a triangle-based approach inspired by Blender's
// Mesh Arrangements algorithm. Instead of BSP trees, it:
// 1. Triangulates both input meshes
// 2. Finds all triangle-triangle intersections
// 3. Splits triangles at intersection lines
// 4. Classifies each resulting triangle as inside/outside using ray casting
// 5. Applies the boolean operation by keeping/removing triangles
//
// This approach is more robust than classic BSP-based CSG, especially for:
// - Coplanar faces
// - Non-manifold geometry
// - Floating point precision issues
//
// Based on research from:
// - Blender's Mesh Arrangements for Solid Geometry
// - Zhou, Grinspun, Zorin, Jacobson (2016)

using UnityEngine;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Unity.ProBuilder.Editor.Tests")]

namespace UnityEngine.ProBuilder.Csg
{
    /// <summary>
    /// Base class for CSG operations. Contains GameObject level methods for Subtraction, Intersection, and Union
    /// operations. The GameObjects passed to these functions will not be modified.
    /// 
    /// This implementation uses a triangle-based boolean algorithm inspired by Blender's
    /// Mesh Arrangements approach, which is more robust than classic BSP tree methods.
    /// </summary>
    static class CSG
    {
        public enum BooleanOp
        {
            Intersection,
            Union,
            Subtraction
        }

        public enum SolverType
        {
            Float,
            Exact
        }

        const float k_DefaultEpsilon = 0.00001f;
        static float s_Epsilon = k_DefaultEpsilon;

        /// <summary>
        /// Tolerance used for epsilon-based comparisons in boolean operations.
        /// </summary>
        public static float epsilon
        {
            get => s_Epsilon;
            set => s_Epsilon = value;
        }


#if UNITY_EDITOR   
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetStaticsOnLoad()
        {
            s_Epsilon = k_DefaultEpsilon;
        }
#endif

        /// <summary>
        /// Performs a boolean operation on two GameObjects.
        /// </summary>
        /// <returns>A new mesh.</returns>
        public static Model Perform(BooleanOp op, GameObject lhs, GameObject rhs, SolverType solver = SolverType.Exact)
        {
            switch (op)
            {
                case BooleanOp.Intersection:
                    return Intersect(lhs, rhs, solver);
                case BooleanOp.Union:
                    return Union(lhs, rhs, solver);
                case BooleanOp.Subtraction:
                    return Subtract(lhs, rhs, solver);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Returns a new mesh by merging @lhs with @rhs.
        /// </summary>
        /// <param name="lhs">The base mesh of the boolean operation.</param>
        /// <param name="rhs">The input mesh of the boolean operation.</param>
        /// <param name="solver">The CSG solver to use.</param>
        /// <returns>A new mesh if the operation succeeds, or null if an error occurs.</returns>
        public static Model Union(GameObject lhs, GameObject rhs, SolverType solver = SolverType.Exact)
        {
            Model csg_model_a = new Model(lhs);
            Model csg_model_b = new Model(rhs);

            List<Polygon> polygonsA = csg_model_a.ToPolygons();
            List<Polygon> polygonsB = csg_model_b.ToPolygons();

            List<Polygon> result;
            if (solver == SolverType.Float)
            {
                Node nodeA = new Node(polygonsA);
                Node nodeB = new Node(polygonsB);
                Node nodeResult = Node.Union(nodeA, nodeB);
                result = nodeResult.AllPolygons();
            }
            else
            {
                result = MeshBooleanSolver.PerformBoolean(polygonsA, polygonsB, BooleanOp.Union);
            }

            return new Model(result);
        }
        
        /// <summary>
        /// Returns a new mesh by subtracting @lhs with @rhs.
        /// </summary>
        /// <param name="lhs">The base mesh of the boolean operation.</param>
        /// <param name="rhs">The input mesh of the boolean operation.</param>
        /// <param name="solver">The CSG solver to use.</param>
        /// <returns>A new mesh if the operation succeeds, or null if an error occurs.</returns>
        public static Model Subtract(GameObject lhs, GameObject rhs, SolverType solver = SolverType.Exact)
        {
            Model csg_model_a = new Model(lhs);
            Model csg_model_b = new Model(rhs);

            List<Polygon> polygonsA = csg_model_a.ToPolygons();
            List<Polygon> polygonsB = csg_model_b.ToPolygons();

            List<Polygon> result;
            if (solver == SolverType.Float)
            {
                Node nodeA = new Node(polygonsA);
                Node nodeB = new Node(polygonsB);
                Node nodeResult = Node.Subtract(nodeA, nodeB);
                result = nodeResult.AllPolygons();
            }
            else
            {
                result = MeshBooleanSolver.PerformBoolean(polygonsA, polygonsB, BooleanOp.Subtraction);
            }

            return new Model(result);
        }

        /// <summary>
        /// Returns a new mesh by intersecting @lhs with @rhs.
        /// </summary>
        /// <param name="lhs">The base mesh of the boolean operation.</param>
        /// <param name="rhs">The input mesh of the boolean operation.</param>
        /// <param name="solver">The CSG solver to use.</param>
        /// <returns>A new mesh if the operation succeeds, or null if an error occurs.</returns>
        public static Model Intersect(GameObject lhs, GameObject rhs, SolverType solver = SolverType.Exact)
        {
            Model csg_model_a = new Model(lhs);
            Model csg_model_b = new Model(rhs);

            List<Polygon> polygonsA = csg_model_a.ToPolygons();
            List<Polygon> polygonsB = csg_model_b.ToPolygons();

            List<Polygon> result;
            if (solver == SolverType.Float)
            {
                Node nodeA = new Node(polygonsA);
                Node nodeB = new Node(polygonsB);
                Node nodeResult = Node.Intersect(nodeA, nodeB);
                result = nodeResult.AllPolygons();
            }
            else
            {
                result = MeshBooleanSolver.PerformBoolean(polygonsA, polygonsB, BooleanOp.Intersection);
            }

            return new Model(result);
        }
    }
}
