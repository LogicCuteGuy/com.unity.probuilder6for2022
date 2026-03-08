using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UObject = UnityEngine.Object;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.ProBuilder.Shapes;
using UnityEngine.ProBuilder.Tests.Framework;

static class TriangulateElementsTests
{
    [Test]
    public static void FlipEdgeOnQuad_ChangesDiagonal()
    {
        var pb = ShapeFactory.Instantiate<Cube>();

        try
        {
            var face = pb.facesInternal[0];
            var before = GetDiagonal(face);

            Assert.IsTrue(pb.FlipEdge(face));

            var after = GetDiagonal(face);
            CollectionAssert.AreNotEqual(before, after);
        }
        finally
        {
            UObject.DestroyImmediate(pb.gameObject);
        }
    }

    [Test]
    public static void ToTriangles_AfterFlipEdge_UsesFlippedDiagonal()
    {
        var pb = ShapeFactory.Instantiate<Cube>();

        try
        {
            var face = pb.facesInternal[0];

            Assert.IsTrue(pb.FlipEdge(face));
            var expectedDiagonal = GetDiagonal(face);

            var triangles = pb.ToTriangles(new[] { face });

            Assert.NotNull(triangles);
            Assert.That(triangles.Length, Is.EqualTo(2));

            var actualDiagonal = GetSharedEdge(triangles[0], triangles[1]);
            CollectionAssert.AreEqual(expectedDiagonal, actualDiagonal);

            pb.Refresh();
            TestUtility.AssertMeshIsValid(pb);
        }
        finally
        {
            UObject.DestroyImmediate(pb.gameObject);
        }
    }

    static int[] GetDiagonal(Face face)
    {
        var counts = new Dictionary<int, int>();

        foreach (var index in face.indexesInternal)
        {
            if (!counts.ContainsKey(index))
                counts[index] = 0;

            counts[index]++;
        }

        return counts.Where(kvp => kvp.Value == 2).Select(kvp => kvp.Key).OrderBy(x => x).ToArray();
    }

    static int[] GetSharedEdge(Face a, Face b)
    {
        var ia = new HashSet<int>(a.distinctIndexesInternal);
        var ib = new HashSet<int>(b.distinctIndexesInternal);
        ia.IntersectWith(ib);
        return ia.OrderBy(x => x).ToArray();
    }
}
