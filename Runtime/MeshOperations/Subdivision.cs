using UnityEngine;
using System.Collections.Generic;

namespace UnityEngine.ProBuilder.MeshOperations
{
    /// <summary>
    /// Direction mode used by <see cref="Subdivision.Subdivide(ProBuilderMesh, IList{Face}, SubdivisionAxis)"/>.
    /// </summary>
    public enum SubdivisionAxis
    {
        /// <summary>
        /// Split in both directions (original behavior).
        /// </summary>
        XY = 0,

        /// <summary>
        /// Split from left to right across the face.
        /// </summary>
        X = 1,

        /// <summary>
        /// Split from top to bottom across the face.
        /// </summary>
        Y = 2
    }

    /// <summary>
    /// Subdivide a ProBuilder mesh.
    /// </summary>
    static class Subdivision
    {
        /// <summary>
        /// Subdivide all faces on the mesh.
        /// </summary>
        /// <remarks>More accurately, this inserts a vertex at the center of each face and connects each edge at it's center.</remarks>
        /// <param name="pb"></param>
        /// <returns></returns>
        public static ActionResult Subdivide(this ProBuilderMesh pb)
        {
            return pb.Subdivide(pb.facesInternal, SubdivisionAxis.XY) != null ? new ActionResult(ActionResult.Status.Success, "Subdivide") : new ActionResult(ActionResult.Status.Failure, "Subdivide Failed");
        }

        /// <summary>
        /// Subdivide a mesh, optionally restricting to the specified faces.
        /// </summary>
        /// <param name="pb"></param>
        /// <param name="faces">The faces to be affected by subdivision.</param>
        /// <returns>The faces created as a result of the subdivision.</returns>
        public static Face[] Subdivide(this ProBuilderMesh pb, IList<Face> faces)
        {
            return Subdivide(pb, faces, SubdivisionAxis.XY);
        }

        /// <summary>
        /// Subdivide a mesh, optionally restricting to the specified faces and axis mode.
        /// </summary>
        /// <param name="pb"></param>
        /// <param name="faces">The faces to be affected by subdivision.</param>
        /// <param name="axis">The axis mode used to split each face.</param>
        /// <returns>The faces created as a result of the subdivision.</returns>
        public static Face[] Subdivide(this ProBuilderMesh pb, IList<Face> faces, SubdivisionAxis axis)
        {
            if (axis == SubdivisionAxis.XY)
                return ConnectElements.Connect(pb, faces);

            return DirectionalSubdivide(pb, faces, axis);
        }

        static Face[] DirectionalSubdivide(ProBuilderMesh pb, IList<Face> faces, SubdivisionAxis axis)
        {
            var split = MeshValidation.EnsureFacesAreComposedOfContiguousTriangles(pb, faces);
            var faceMask = new HashSet<Face>(faces);

            if (split.Count > 0)
            {
                foreach (var face in split)
                    faceMask.Add(face);
            }

            var edges = new List<Edge>(faceMask.Count * 2);

            foreach (var face in faceMask)
            {
                Edge a;
                Edge b;

                if (TryGetDirectionalCutEdges(pb, face, axis, out a, out b))
                {
                    edges.Add(a);
                    edges.Add(b);
                }
            }

            if (edges.Count < 2)
                return null;

            Face[] addedFaces;
            Edge[] ignore;
            ConnectElements.Connect(pb, edges, out addedFaces, out ignore, true, false, faceMask);
            return addedFaces;
        }

        static bool TryGetDirectionalCutEdges(ProBuilderMesh pb, Face face, SubdivisionAxis axis, out Edge a, out Edge b)
        {
            a = default(Edge);
            b = default(Edge);

            var perimeter = WingedEdge.SortEdgesByAdjacency(face);

            if (perimeter == null || perimeter.Count < 3)
                return false;

            var distinct = face.distinctIndexesInternal;

            if (distinct == null || distinct.Length < 3)
                return false;

            var projected = Projection.PlanarProject(pb.positionsInternal, distinct, Math.Normal(pb, face));
            var lookup = new Dictionary<int, Vector2>(distinct.Length);

            for (int i = 0; i < distinct.Length; i++)
                lookup[distinct[i]] = projected[i];

            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;
            var minEdge = default(Edge);
            var maxEdge = default(Edge);

            for (int i = 0; i < perimeter.Count; i++)
            {
                Vector2 av;
                Vector2 bv;

                if (!lookup.TryGetValue(perimeter[i].a, out av) || !lookup.TryGetValue(perimeter[i].b, out bv))
                    continue;

                var mid = (av + bv) * .5f;
                var value = axis == SubdivisionAxis.X ? mid.x : mid.y;

                if (value < min)
                {
                    min = value;
                    minEdge = perimeter[i];
                }

                if (value > max)
                {
                    max = value;
                    maxEdge = perimeter[i];
                }
            }

            if (minEdge.Equals(maxEdge))
                return false;

            a = minEdge;
            b = maxEdge;
            return true;
        }
    }
}
