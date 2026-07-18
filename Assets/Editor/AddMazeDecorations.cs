using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AddMazeDecorations
{
    private const string PropsFolder = "Assets/Asset/BackroomsLikeAsset/prefab/Props";
    private const string DebrisFolder = "Assets/Asset/BackroomsLikeAsset/prefab/Debris";

    // Fraction of eligible floor cells that get a decoration.
    private const float DecorationChance = 0.5f;
    // Cells within this many units of the start spawn are left clear.
    private const float StartClearRadius = 6f;

    [MenuItem("Tools/Backrooms/Add Maze Decorations")]
    public static void Decorate()
    {
        GameObject mazeRoot = GameObject.Find("MazeLevel");
        if (mazeRoot == null)
        {
            Debug.LogError("No 'MazeLevel' found in the scene. Run 'Generate Maze Level' first.");
            return;
        }

        Transform floorsParent = mazeRoot.transform.Find("Floors");
        if (floorsParent == null || floorsParent.childCount == 0)
        {
            Debug.LogError("MazeLevel has no floor cells to decorate.");
            return;
        }

        Transform existingDecor = mazeRoot.transform.Find("Decorations");
        if (existingDecor != null)
        {
            Object.DestroyImmediate(existingDecor.gameObject);
        }

        List<GameObject> propPrefabs = LoadPrefabsInFolder(PropsFolder);
        List<GameObject> debrisPrefabs = LoadPrefabsInFolder(DebrisFolder);
        if (propPrefabs.Count == 0 && debrisPrefabs.Count == 0)
        {
            Debug.LogError("Could not load any prop/debris prefabs from the expected folders.");
            return;
        }

        float cellSize = floorsParent.GetChild(0).localScale.x;
        Vector3 startPos = floorsParent.GetChild(0).position;

        var candidateCenters = new List<Vector3>();
        foreach (Transform child in floorsParent)
        {
            if (child.name != "Floor") continue; // skip the exit room's ExitFloor_* rim pieces
            Vector3 c = child.position;
            c.y = 0f;
            if (Vector3.Distance(c, startPos) < StartClearRadius) continue;
            candidateCenters.Add(c);
        }

        GameObject decorRoot = new GameObject("Decorations");
        decorRoot.transform.SetParent(mazeRoot.transform);

        var rng = new System.Random();
        float margin = cellSize * 0.3f; // keep decorations away from walls
        int placed = 0;

        foreach (Vector3 cellCenter in candidateCenters)
        {
            if (rng.NextDouble() > DecorationChance) continue;

            bool useDebris = propPrefabs.Count == 0 || (debrisPrefabs.Count > 0 && rng.NextDouble() < 0.4);
            GameObject prefab = useDebris
                ? debrisPrefabs[rng.Next(debrisPrefabs.Count)]
                : propPrefabs[rng.Next(propPrefabs.Count)];

            float half = cellSize / 2f - margin;
            float offsetX = (float)(rng.NextDouble() * 2f - 1f) * half;
            float offsetZ = (float)(rng.NextDouble() * 2f - 1f) * half;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, decorRoot.transform);
            instance.transform.position = cellCenter + new Vector3(offsetX, 0f, offsetZ);
            instance.transform.rotation = Quaternion.Euler(0f, rng.Next(0, 360), 0f);
            placed++;
        }

        Undo.RegisterCreatedObjectUndo(decorRoot, "Add Maze Decorations");
        Debug.Log($"Placed {placed} decorations across {candidateCenters.Count} eligible floor cells " +
                  $"({propPrefabs.Count} prop types, {debrisPrefabs.Count} debris types available). " +
                  "Wall-mounted debris was skipped since it needs wall-facing alignment. Re-run to reroll placement.");
    }

    private static List<GameObject> LoadPrefabsInFolder(string folder)
    {
        var result = new List<GameObject>();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) result.Add(prefab);
        }
        return result;
    }
}
