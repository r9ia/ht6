using UnityEditor;
using UnityEngine;

public static class AddGroundPlane
{
    private const string FloorMaterialPath = "Assets/Asset/BackroomsLikeAsset/material/Floor_Carpet_Mat.mat";
    private const string GroundMaterialPath = "Assets/Asset/BackroomsLikeAsset/material/Ground_Floor_Carpet_Mat.mat";

    // World-space size (in units) that one texture repeat should cover, so tiling stays
    // consistent with the modular floor tiles instead of stretching across the whole plane.
    private const float UnitsPerTextureTile = 999f;

    [MenuItem("Tools/Backrooms/Add Large Ground Plane")]
    public static void AddGround()
    {
        if (GameObject.Find("Ground") != null)
        {
            Debug.LogError("A GameObject named 'Ground' already exists in this scene. Rename/remove it first if you want a fresh one.");
            return;
        }

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.02f, 0f);
        ground.transform.localScale = new Vector3(10f, 1f, 10f); // default Plane is 10x10, so this is 100x100 units

        ApplyTiledMaterial(ground);

        Undo.RegisterCreatedObjectUndo(ground, "Add Large Ground Plane");
        EditorUtility.SetDirty(ground);

        Debug.Log("Added 100x100 unit 'Ground' plane at y = -0.02, flush with the room floor, using a tiled Backrooms floor material.");
    }

    [MenuItem("Tools/Backrooms/Fix Ground Texture Tiling")]
    public static void FixGroundTiling()
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
        {
            Debug.LogError("Could not find a 'Ground' GameObject in the scene.");
            return;
        }

        ApplyTiledMaterial(ground);
        EditorUtility.SetDirty(ground);
        Debug.Log("Updated 'Ground' material tiling so the texture repeats instead of stretching.");
    }

    private static void ApplyTiledMaterial(GameObject ground)
    {
        Material groundMat = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
        if (groundMat == null)
        {
            Material floorMat = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            if (floorMat == null)
            {
                Debug.LogWarning($"Could not find floor material at {FloorMaterialPath}, leaving default material.");
                return;
            }

            groundMat = new Material(floorMat);
            AssetDatabase.CreateAsset(groundMat, GroundMaterialPath);
        }

        // Default Plane primitive spans 10 local units; localScale converts that to world size.
        float worldWidth = 10f * ground.transform.localScale.x;
        float worldLength = 10f * ground.transform.localScale.z;
        groundMat.mainTextureScale = new Vector2(worldWidth / UnitsPerTextureTile, worldLength / UnitsPerTextureTile);
        EditorUtility.SetDirty(groundMat);

        var renderer = ground.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = groundMat;
    }
}
