using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SplitFloorByHeight
{
    // Triangles whose centroid Y differs by more than this are treated as separate steps/panels.
    // If the split looks wrong (too few/many pieces), tweak this and re-run.
    private const float HeightClusterTolerance = 0.05f;

    [MenuItem("Tools/Backrooms/Split Selected Floor Collider By Height")]
    public static void SplitSelectedByHeight()
    {
        GameObject target = Selection.activeGameObject;
        if (target == null)
        {
            Debug.LogError("Select the floor collider child (e.g. ColliderPart_Floor_Carpet_Mat) first.");
            return;
        }

        MeshCollider meshCollider = target.GetComponent<MeshCollider>();
        if (meshCollider == null || meshCollider.sharedMesh == null)
        {
            Debug.LogError($"{target.name} has no Mesh Collider with a mesh assigned.");
            return;
        }

        Transform parent = target.transform.parent;
        if (parent == null)
        {
            Debug.LogError("Selected object has no parent to attach the split pieces to.");
            return;
        }

        Mesh sourceMesh = meshCollider.sharedMesh;
        Vector3[] verts = sourceMesh.vertices;
        Vector3[] normals = sourceMesh.normals;
        int[] tris = sourceMesh.triangles;

        int triCount = tris.Length / 3;
        var triData = new List<(int triIndex, float centroidY)>(triCount);
        for (int t = 0; t < triCount; t++)
        {
            int i0 = tris[t * 3];
            int i1 = tris[t * 3 + 1];
            int i2 = tris[t * 3 + 2];
            float y = (verts[i0].y + verts[i1].y + verts[i2].y) / 3f;
            triData.Add((t, y));
        }
        triData.Sort((a, b) => a.centroidY.CompareTo(b.centroidY));

        var clusters = new List<List<int>>();
        var currentCluster = new List<int> { triData[0].triIndex };
        float lastY = triData[0].centroidY;

        for (int k = 1; k < triData.Count; k++)
        {
            float y = triData[k].centroidY;
            if (y - lastY > HeightClusterTolerance)
            {
                clusters.Add(currentCluster);
                currentCluster = new List<int>();
            }
            currentCluster.Add(triData[k].triIndex);
            lastY = y;
        }
        clusters.Add(currentCluster);

        int pieceIndex = 0;
        foreach (var cluster in clusters)
        {
            int[] subTris = new int[cluster.Count * 3];
            float ySum = 0f;
            for (int c = 0; c < cluster.Count; c++)
            {
                int t = cluster[c];
                subTris[c * 3] = tris[t * 3];
                subTris[c * 3 + 1] = tris[t * 3 + 1];
                subTris[c * 3 + 2] = tris[t * 3 + 2];
                ySum += (verts[tris[t * 3]].y + verts[tris[t * 3 + 1]].y + verts[tris[t * 3 + 2]].y) / 3f;
            }
            float avgY = ySum / cluster.Count;

            Mesh pieceMesh = new Mesh();
            pieceMesh.name = $"{target.name}_h{avgY:0.00}";
            pieceMesh.vertices = verts;
            pieceMesh.normals = normals;
            pieceMesh.triangles = subTris;
            pieceMesh.RecalculateBounds();

            GameObject child = new GameObject($"ColliderPart_Floor_h{avgY:0.00}_{pieceIndex}");
            Undo.RegisterCreatedObjectUndo(child, "Split Floor Collider By Height");
            child.transform.SetParent(parent, false);

            MeshCollider mc = child.AddComponent<MeshCollider>();
            mc.sharedMesh = pieceMesh;

            pieceIndex++;
        }

        // Keep the original as a disabled backup instead of deleting it, in case the tolerance needs tuning.
        Undo.RecordObject(target, "Split Floor Collider By Height");
        target.SetActive(false);
        target.name += "_SOURCE_DISABLED";

        Debug.Log($"Split floor collider into {clusters.Count} height-based pieces (tolerance {HeightClusterTolerance}). " +
                   "Toggle each ColliderPart_Floor_h* child and Play-test to find the one(s) blocking the stairwell opening. " +
                   $"Original mesh kept disabled as '{target.name}' in case you need to retry with a different tolerance.");
    }
}
