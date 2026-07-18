using UnityEditor;
using UnityEngine;

public static class SplitColliderByMaterial
{
    [MenuItem("Tools/Backrooms/Split Collider By Material For Selected")]
    public static void SplitSelected()
    {
        GameObject target = Selection.activeGameObject;
        if (target == null)
        {
            Debug.LogError("Select a tile GameObject in the Hierarchy first.");
            return;
        }

        var meshFilter = target.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogError($"{target.name} has no MeshFilter with a mesh assigned.");
            return;
        }

        Mesh sourceMesh = meshFilter.sharedMesh;
        var renderer = target.GetComponent<MeshRenderer>();
        Material[] mats = renderer != null ? renderer.sharedMaterials : null;

        var existingCollider = target.GetComponent<MeshCollider>();
        if (existingCollider != null)
        {
            Undo.DestroyObjectImmediate(existingCollider);
        }

        int createdCount = 0;
        for (int i = 0; i < sourceMesh.subMeshCount; i++)
        {
            int[] tris = sourceMesh.GetTriangles(i);
            if (tris.Length == 0) continue;

            string matName = (mats != null && i < mats.Length && mats[i] != null) ? mats[i].name : $"sub{i}";

            Mesh subMesh = new Mesh();
            subMesh.name = $"{target.name}_{matName}_Collider";
            subMesh.vertices = sourceMesh.vertices;
            subMesh.normals = sourceMesh.normals;
            subMesh.triangles = tris;
            subMesh.RecalculateBounds();

            GameObject child = new GameObject($"ColliderPart_{matName}");
            Undo.RegisterCreatedObjectUndo(child, "Split Collider By Material");
            child.transform.SetParent(target.transform, false);

            MeshCollider mc = child.AddComponent<MeshCollider>();
            mc.sharedMesh = subMesh;
            createdCount++;
        }

        EditorUtility.SetDirty(target);
        Debug.Log($"Split '{target.name}' collider into {createdCount} parts by material (see ColliderPart_* children). " +
                  "Disable the one matching the floor material and Play-test to confirm the stairwell opening now behaves correctly.");
    }
}
