using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class GenerateMazeLevel
{
    private const string FloorMaterialPath = "Assets/Asset/BackroomsLikeAsset/material/Floor_Carpet_Mat.mat";
    private const string WallMaterialPath = "Assets/Asset/BackroomsLikeAsset/material/Wall_Painted_Mat.mat";
    private const string CeilingMaterialPath = "Assets/Asset/BackroomsLikeAsset/material/Ceiling_Office_Mat.mat";

    private const int GridSize = 14;
    private const float CellSize = 4f;
    private const float WallHeight = 3f;
    private const float WallThickness = 0.2f;
    private const float FloorThickness = 0.2f;
    private const float HoleSize = 2f; // must be less than CellSize

    private const int NumOpenZones = 4;
    private const int MinZoneSize = 2;
    private const int MaxZoneSize = 3;

    [MenuItem("Tools/Backrooms/Generate Maze Level")]
    public static void Generate()
    {
        GameObject existing = GameObject.Find("MazeLevel");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject root = new GameObject("MazeLevel");
        GameObject floors = new GameObject("Floors");
        GameObject walls = new GameObject("Walls");
        GameObject ceilings = new GameObject("Ceilings");
        floors.transform.SetParent(root.transform);
        walls.transform.SetParent(root.transform);
        ceilings.transform.SetParent(root.transform);

        Material floorMat = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
        Material wallMat = AssetDatabase.LoadAssetAtPath<Material>(WallMaterialPath);
        Material ceilingMat = AssetDatabase.LoadAssetAtPath<Material>(CeilingMaterialPath);

        int w = GridSize;
        int h = GridSize;

        // wallRight[x,y]: wall between cell(x,y) and cell(x+1,y). wallDown[x,y]: wall between cell(x,y) and cell(x,y+1).
        bool[,] wallRight = new bool[w - 1, h];
        bool[,] wallDown = new bool[w, h - 1];
        for (int x = 0; x < w - 1; x++)
            for (int y = 0; y < h; y++)
                wallRight[x, y] = true;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h - 1; y++)
                wallDown[x, y] = true;

        var rng = new System.Random();
        var visited = new bool[w, h];
        var stack = new Stack<Vector2Int>();
        Vector2Int start = new Vector2Int(0, 0);
        visited[0, 0] = true;
        stack.Push(start);

        while (stack.Count > 0)
        {
            Vector2Int cur = stack.Peek();
            var candidates = new List<Vector2Int>();
            if (cur.x > 0 && !visited[cur.x - 1, cur.y]) candidates.Add(new Vector2Int(cur.x - 1, cur.y));
            if (cur.x < w - 1 && !visited[cur.x + 1, cur.y]) candidates.Add(new Vector2Int(cur.x + 1, cur.y));
            if (cur.y > 0 && !visited[cur.x, cur.y - 1]) candidates.Add(new Vector2Int(cur.x, cur.y - 1));
            if (cur.y < h - 1 && !visited[cur.x, cur.y + 1]) candidates.Add(new Vector2Int(cur.x, cur.y + 1));

            if (candidates.Count == 0)
            {
                stack.Pop();
                continue;
            }

            Vector2Int next = candidates[rng.Next(candidates.Count)];
            RemoveWall(cur, next, wallRight, wallDown);
            visited[next.x, next.y] = true;
            stack.Push(next);
        }

        // Carve a few random open/wide zones for variety, avoiding the start cell.
        for (int i = 0; i < NumOpenZones; i++)
        {
            int zw = rng.Next(MinZoneSize, MaxZoneSize + 1);
            int zh = rng.Next(MinZoneSize, MaxZoneSize + 1);
            int zx = rng.Next(0, w - zw);
            int zy = rng.Next(0, h - zh);
            if (zx <= 1 && zy <= 1) continue; // keep area around start relatively normal

            for (int x = zx; x < zx + zw; x++)
            {
                for (int y = zy; y < zy + zh; y++)
                {
                    if (x < zx + zw - 1) wallRight[x, y] = false;
                    if (y < zy + zh - 1) wallDown[x, y] = false;
                }
            }
        }

        // BFS from start to find the farthest cell -> exit room.
        var dist = new int[w, h];
        for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) dist[x, y] = -1;
        var queue = new Queue<Vector2Int>();
        dist[0, 0] = 0;
        queue.Enqueue(start);
        Vector2Int farthest = start;
        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            if (dist[cur.x, cur.y] > dist[farthest.x, farthest.y]) farthest = cur;

            foreach (var n in GetOpenNeighbors(cur, w, h, wallRight, wallDown))
            {
                if (dist[n.x, n.y] == -1)
                {
                    dist[n.x, n.y] = dist[cur.x, cur.y] + 1;
                    queue.Enqueue(n);
                }
            }
        }
        Vector2Int exitCell = farthest;

        float originOffset = -(GridSize * CellSize) / 2f;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Vector3 center = new Vector3(originOffset + x * CellSize + CellSize / 2f, 0f, originOffset + y * CellSize + CellSize / 2f);

                if (x == exitCell.x && y == exitCell.y)
                {
                    BuildHoleFloor(floors.transform, center, floorMat);
                }
                else
                {
                    BuildBox(floors.transform, "Floor", center + Vector3.down * (FloorThickness / 2f),
                        new Vector3(CellSize, FloorThickness, CellSize), floorMat);
                }

                BuildBox(ceilings.transform, "Ceiling", center + Vector3.up * (WallHeight + WallThickness / 2f),
                    new Vector3(CellSize, WallThickness, CellSize), ceilingMat);

                bool wallWest = x == 0 || wallRight[x - 1, y];
                bool wallNorth = y == 0 || wallDown[x, y - 1];
                bool wallEast = x == w - 1 || wallRight[x, y];
                bool wallSouth = y == h - 1 || wallDown[x, y];

                if (wallWest)
                    BuildBox(walls.transform, "Wall_W", center + new Vector3(-CellSize / 2f, WallHeight / 2f, 0f),
                        new Vector3(WallThickness, WallHeight, CellSize), wallMat);
                if (wallNorth)
                    BuildBox(walls.transform, "Wall_N", center + new Vector3(0f, WallHeight / 2f, -CellSize / 2f),
                        new Vector3(CellSize, WallHeight, WallThickness), wallMat);
                if (wallEast && x == w - 1)
                    BuildBox(walls.transform, "Wall_E", center + new Vector3(CellSize / 2f, WallHeight / 2f, 0f),
                        new Vector3(WallThickness, WallHeight, CellSize), wallMat);
                if (wallSouth && y == h - 1)
                    BuildBox(walls.transform, "Wall_S", center + new Vector3(0f, WallHeight / 2f, CellSize / 2f),
                        new Vector3(CellSize, WallHeight, WallThickness), wallMat);
            }
        }

        // Exit trigger + timer beneath the hole.
        Vector3 exitCenter = new Vector3(originOffset + exitCell.x * CellSize + CellSize / 2f, -3f,
            originOffset + exitCell.y * CellSize + CellSize / 2f);
        GameObject exitTriggerGO = new GameObject("ExitTrigger");
        exitTriggerGO.transform.SetParent(root.transform);
        exitTriggerGO.transform.position = exitCenter;
        BoxCollider box = exitTriggerGO.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(HoleSize, 4f, HoleSize);
        exitTriggerGO.AddComponent<ExitTrigger>();

        GameObject timerGO = new GameObject("GameTimer");
        timerGO.transform.SetParent(root.transform);
        timerGO.AddComponent<GameTimer>();

        SpawnPlayer(root.transform, new Vector3(originOffset + CellSize / 2f, 1f, originOffset + CellSize / 2f));

        Undo.RegisterCreatedObjectUndo(root, "Generate Maze Level");

        float straightLineDistance = Vector3.Distance(
            new Vector3(0, 0, 0),
            new Vector3(exitCell.x * CellSize, 0, exitCell.y * CellSize));
        Debug.Log($"Generated {w}x{h} maze (cell size {CellSize}). Start at (0,0), exit room at {exitCell} " +
                  $"(graph distance {dist[exitCell.x, exitCell.y]} cells, ~{straightLineDistance:0} units straight-line). " +
                  "Re-run this command to reroll the layout.");
    }

    private static void RemoveWall(Vector2Int a, Vector2Int b, bool[,] wallRight, bool[,] wallDown)
    {
        if (a.y == b.y)
        {
            int minX = Mathf.Min(a.x, b.x);
            wallRight[minX, a.y] = false;
        }
        else
        {
            int minY = Mathf.Min(a.y, b.y);
            wallDown[a.x, minY] = false;
        }
    }

    private static List<Vector2Int> GetOpenNeighbors(Vector2Int cur, int w, int h, bool[,] wallRight, bool[,] wallDown)
    {
        var result = new List<Vector2Int>();
        if (cur.x > 0 && !wallRight[cur.x - 1, cur.y]) result.Add(new Vector2Int(cur.x - 1, cur.y));
        if (cur.x < w - 1 && !wallRight[cur.x, cur.y]) result.Add(new Vector2Int(cur.x + 1, cur.y));
        if (cur.y > 0 && !wallDown[cur.x, cur.y - 1]) result.Add(new Vector2Int(cur.x, cur.y - 1));
        if (cur.y < h - 1 && !wallDown[cur.x, cur.y]) result.Add(new Vector2Int(cur.x, cur.y + 1));
        return result;
    }

    private static void BuildBox(Transform parent, string name, Vector3 worldCenter, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = worldCenter;
        go.transform.localScale = size;
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private static void BuildHoleFloor(Transform parent, Vector3 center, Material mat)
    {
        float outer = CellSize;
        float innerHalf = HoleSize / 2f;
        float outerHalf = outer / 2f;
        float stripWidth = outerHalf - innerHalf;

        // North / South strips span the full cell width; East / West strips fill the remaining sides.
        BuildBox(parent, "ExitFloor_N", center + new Vector3(0, -FloorThickness / 2f, -(innerHalf + stripWidth / 2f)),
            new Vector3(outer, FloorThickness, stripWidth), mat);
        BuildBox(parent, "ExitFloor_S", center + new Vector3(0, -FloorThickness / 2f, innerHalf + stripWidth / 2f),
            new Vector3(outer, FloorThickness, stripWidth), mat);
        BuildBox(parent, "ExitFloor_E", center + new Vector3(innerHalf + stripWidth / 2f, -FloorThickness / 2f, 0),
            new Vector3(stripWidth, FloorThickness, HoleSize), mat);
        BuildBox(parent, "ExitFloor_W", center + new Vector3(-(innerHalf + stripWidth / 2f), -FloorThickness / 2f, 0),
            new Vector3(stripWidth, FloorThickness, HoleSize), mat);
    }

    private static void SpawnPlayer(Transform parent, Vector3 spawnPosition)
    {
        if (GameObject.Find("Player") != null)
        {
            Debug.LogWarning("A 'Player' GameObject already exists in this scene; skipping player spawn. " +
                              "Move the existing one to the maze manually if needed.");
            return;
        }

        GameObject player = new GameObject("Player");
        player.transform.SetParent(parent);
        player.transform.position = spawnPosition;

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.4f;
        controller.center = Vector3.zero;
        controller.stepOffset = 0.3f;
        controller.skinWidth = 0.08f;

        GameObject cameraGO = new GameObject("Main Camera");
        cameraGO.tag = "MainCamera";
        cameraGO.transform.SetParent(player.transform, false);
        cameraGO.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        cameraGO.AddComponent<Camera>();
        cameraGO.AddComponent<AudioListener>();

        FirstPersonController fpc = player.AddComponent<FirstPersonController>();
        SerializedObject so = new SerializedObject(fpc);
        so.FindProperty("cameraTransform").objectReferenceValue = cameraGO.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
