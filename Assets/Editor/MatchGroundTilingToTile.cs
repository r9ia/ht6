using UnityEditor;
using UnityEngine;

public static class MatchGroundTilingToTile
{
    private const string GroundMaterialPath = "Assets/Asset/BackroomsLikeAsset/material/Ground_Floor_Carpet_Mat.mat";
    private const string FloorMaterialNameHint = "Floor_Carpet_Mat";

    [MenuItem("Tools/Backrooms/Match Ground Tiling To Selected Tile Floor")]
    public static void MatchTiling()
    {
        GameObject tile = Selection.activeGameObject;
        if (tile == null)
        {
            Debug.LogError("Select the tile GameObject with the floor mesh (e.g. Tiles_TStairs_02_A) first.");
            return;
        }

        MeshFilter filter = tile.GetComponent<MeshFilter>();
        MeshRenderer renderer = tile.GetComponent<MeshRenderer>();
        if (filter == null || filter.sharedMesh == null || renderer == null)
        {
            Debug.LogError($"{tile.name} needs a MeshFilter (with mesh) and MeshRenderer.");
            return;
        }

        Mesh mesh = filter.sharedMesh;
        if (mesh.uv == null || mesh.uv.Length == 0)
        {
            Debug.LogError($"{mesh.name} has no UV data.");
            return;
        }

        Material[] mats = renderer.sharedMaterials;
        int floorSubmesh = -1;
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] != null && mats[i].name.Contains(FloorMaterialNameHint))
            {
                floorSubmesh = i;
                break;
            }
        }
        if (floorSubmesh == -1)
        {
            Debug.LogError($"Could not find a submesh using a material containing '{FloorMaterialNameHint}' on {tile.name}.");
            return;
        }

        int[] tris = mesh.GetTriangles(floorSubmesh);
        Vector3[] verts = mesh.vertices;
        Vector2[] uvs = mesh.uv;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        float minU = float.MaxValue, maxU = float.MinValue;
        float minV = float.MaxValue, maxV = float.MinValue;

        foreach (int idx in tris)
        {
            Vector3 p = verts[idx];
            Vector2 uv = uvs[idx];
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
            minU = Mathf.Min(minU, uv.x); maxU = Mathf.Max(maxU, uv.x);
            minV = Mathf.Min(minV, uv.y); maxV = Mathf.Max(maxV, uv.y);
        }

        float worldRangeX = (maxX - minX) * tile.transform.lossyScale.x;
        float worldRangeZ = (maxZ - minZ) * tile.transform.lossyScale.z;
        float uvRangeU = maxU - minU;
        float uvRangeV = maxV - minV;

        if (worldRangeX <= 0f || worldRangeZ <= 0f || uvRangeU <= 0f || uvRangeV <= 0f)
        {
            Debug.LogError("Degenerate floor bounds/UVs, cannot compute tiling density.");
            return;
        }

        float repeatsPerUnitX = uvRangeU / worldRangeX;
        float repeatsPerUnitZ = uvRangeV / worldRangeZ;

        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
        {
            Debug.LogError("Could not find a 'Ground' GameObject in the scene.");
            return;
        }

        float groundWorldWidth = 10f * ground.transform.localScale.x;
        float groundWorldLength = 10f * ground.transform.localScale.z;

        Material groundMat = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
        if (groundMat == null)
        {
            Debug.LogError($"Could not find {GroundMaterialPath}. Run 'Add Large Ground Plane' or 'Fix Ground Texture Tiling' first.");
            return;
        }

        Vector2 tiling = new Vector2(repeatsPerUnitX * groundWorldWidth, repeatsPerUnitZ * groundWorldLength);
        groundMat.mainTextureScale = tiling;
        EditorUtility.SetDirty(groundMat);

        var groundRenderer = ground.GetComponent<MeshRenderer>();
        groundRenderer.sharedMaterial = groundMat;

        Debug.Log($"Measured tile floor density: {repeatsPerUnitX:0.###} repeats/unit (X), {repeatsPerUnitZ:0.###} repeats/unit (Z). " +
                  $"Applied tiling {tiling} to Ground material to match.");
    }
}
