using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DreadDirector.Editor
{
    /// <summary>Discovers approved imported presentation assets while preserving a no-package fallback.</summary>
    internal static class DreadDirectorOptionalAssets
    {
        private static readonly string[] EnvironmentKeywords = { "backroom", "room", "corridor", "hall", "modular" };
        private static readonly string[] CreatureKeywords = { "creep", "horror", "creature", "monster", "character" };
        private static readonly string[] GeorgiaWorldRootNames = { "RoomTiles", "Props", "Directional Light", "Global Volume" };
        private const string GeorgiaLevelScene = "Assets/Asset/BackroomsLikeAsset/Rooms.unity";
        private const string PreferredEnvironmentPrefab = "Assets/LoafbrrAssets/BackroomsLikeAssetRe/prefab/Level/TstLevel.prefab";
        private const string PreferredCreatureAsset = "Assets/ThirdParty/Quaternius/UltimateMonsters/Demon/Demon.fbx";

        [MenuItem("Dread Director/Validate Optional Asset Discovery")]
        public static void ValidateDiscovery()
        {
            var environmentPath = FindEnvironmentPrefabPath();
            var creaturePath = FindCreatureAssetPath();
            Debug.Log($"[Dread Director] Optional environment: {environmentPath ?? "not found (procedural fallback will be used)"}.");
            Debug.Log($"[Dread Director] Optional creature: {creaturePath ?? "not found (primitive apparition will be used)"}.");
        }

        public static bool TryInstantiateGeorgiaLevel(Scene destinationScene, out Pose playerSpawn)
        {
            playerSpawn = new Pose(new Vector3(0f, 0f, -5.2f), Quaternion.identity);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GeorgiaLevelScene) == null)
            {
                return false;
            }

            var templateScene = EditorSceneManager.OpenScene(GeorgiaLevelScene, OpenSceneMode.Additive);
            try
            {
                var templateRoots = templateScene.GetRootGameObjects();
                foreach (var root in templateRoots)
                {
                    if (!string.Equals(root.name, "Player", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var position = root.transform.position;
                    var controller = root.GetComponent<CharacterController>();
                    if (controller != null)
                    {
                        position.y += controller.center.y - (controller.height * 0.5f);
                    }

                    playerSpawn = new Pose(position, root.transform.rotation);
                    break;
                }

                var levelRoot = new GameObject("Georgia Backrooms Level");
                SceneManager.MoveGameObjectToScene(levelRoot, destinationScene);
                foreach (var root in templateRoots)
                {
                    if (Array.IndexOf(GeorgiaWorldRootNames, root.name) < 0)
                    {
                        continue;
                    }

                    SceneManager.MoveGameObjectToScene(root, destinationScene);
                    root.transform.SetParent(levelRoot.transform, true);
                }

                var rendererCount = levelRoot.GetComponentsInChildren<Renderer>(true).Length;
                var colliderCount = levelRoot.GetComponentsInChildren<Collider>(true).Length;
                if (levelRoot.transform.Find("RoomTiles") == null || rendererCount == 0 || colliderCount == 0)
                {
                    Object.DestroyImmediate(levelRoot);
                    Debug.LogWarning($"[Dread Director] Georgia level at {GeorgiaLevelScene} did not contain its expected world roots; using the fallback room.");
                    return false;
                }

                Debug.Log($"[Dread Director] Imported Georgia level from {GeorgiaLevelScene} ({rendererCount} renderers, {colliderCount} colliders).");
                return true;
            }
            finally
            {
                EditorSceneManager.CloseScene(templateScene, true);
                SceneManager.SetActiveScene(destinationScene);
            }
        }

        public static bool TryInstantiateEnvironment()
        {
            var path = FindEnvironmentPrefabPath();
            if (!TryInstantiate(path, "Optional Backrooms Set Dressing", out var instance, out var bounds))
            {
                return false;
            }

            NormalizeToBounds(instance, bounds, 14f, Vector3.zero);
            Debug.Log($"[Dread Director] Added optional environment prefab from {path}.");
            return true;
        }

        public static bool TryPopulateCreature(Transform parent)
        {
            var path = FindCreatureAssetPath();
            if (!TryInstantiate(path, "Imported Creature", out var instance, out var bounds))
            {
                return false;
            }

            instance.transform.SetParent(parent, true);
            instance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            NormalizeToBounds(instance, bounds, 2.7f, parent.position);
            ConfigureCreatureAnimation(instance, path);
            Debug.Log($"[Dread Director] Added optional creature prefab from {path}.");
            return true;
        }

        private static void ConfigureCreatureAnimation(GameObject instance, string assetPath)
        {
            AnimationClip selectedClip = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (!(asset is AnimationClip clip) || clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (selectedClip == null)
                {
                    selectedClip = clip;
                }

                if (clip.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    selectedClip = clip;
                    break;
                }
            }

            if (selectedClip == null)
            {
                Debug.LogWarning($"[Dread Director] Imported creature at {assetPath} has no usable animation clip; reveal logic remains functional.");
                return;
            }

            const string controllerPath = "Assets/Generated/DreadDirector/ImportedCreature.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, selectedClip);
            }
            else
            {
                var stateMachine = controller.layers[0].stateMachine;
                var states = stateMachine.states;
                var state = states.Length > 0 ? states[0].state : stateMachine.AddState("Creature Idle");
                state.motion = selectedClip;
                stateMachine.defaultState = state;
                EditorUtility.SetDirty(controller);
            }

            var animator = instance.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                animator = instance.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            Debug.Log($"[Dread Director] Creature animation wired with clip '{selectedClip.name}'.");
        }

        private static string FindEnvironmentPrefabPath()
        {
            var preferred = AssetDatabase.LoadAssetAtPath<GameObject>(PreferredEnvironmentPrefab);
            if (preferred != null && preferred.GetComponentsInChildren<Renderer>(true).Length > 0)
            {
                return PreferredEnvironmentPrefab;
            }

            return FindBestPrefabPath(EnvironmentKeywords, CreatureKeywords);
        }

        private static string FindCreatureAssetPath()
        {
            var preferred = AssetDatabase.LoadAssetAtPath<GameObject>(PreferredCreatureAsset);
            if (preferred != null && preferred.GetComponentsInChildren<Renderer>(true).Length > 0)
            {
                return PreferredCreatureAsset;
            }

            return FindBestPrefabPath(CreatureKeywords, new[] { "backroom", "environment", "room" });
        }

        private static string FindBestPrefabPath(string[] positiveKeywords, string[] negativeKeywords)
        {
            string bestPath = null;
            var bestScore = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var normalized = path.ToLowerInvariant();
                if (normalized.Contains("/generated/") || normalized.Contains("/tutorialinfo/"))
                {
                    continue;
                }

                var score = Score(normalized, positiveKeywords, negativeKeywords);
                if (score <= bestScore)
                {
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponentsInChildren<Renderer>(true).Length == 0)
                {
                    continue;
                }

                bestScore = score;
                bestPath = path;
            }

            return bestPath;
        }

        private static int Score(string path, string[] positiveKeywords, string[] negativeKeywords)
        {
            var score = 0;
            for (var index = 0; index < positiveKeywords.Length; index++)
            {
                if (path.Contains(positiveKeywords[index]))
                {
                    score += (positiveKeywords.Length - index) * 10;
                }
            }

            foreach (var keyword in negativeKeywords)
            {
                if (path.Contains(keyword))
                {
                    score -= 30;
                }
            }

            if (path.Contains("prefab"))
            {
                score += 3;
            }

            return score;
        }

        private static bool TryInstantiate(string path, string objectName, out GameObject instance, out Bounds bounds)
        {
            instance = null;
            bounds = default;
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                return false;
            }

            instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null || !TryGetRendererBounds(instance, out bounds))
            {
                if (instance != null)
                {
                    Object.DestroyImmediate(instance);
                }

                instance = null;
                return false;
            }

            instance.name = objectName;
            return true;
        }

        private static void NormalizeToBounds(GameObject instance, Bounds initialBounds, float targetSize, Vector3 targetBottomCenter)
        {
            var largestDimension = Mathf.Max(initialBounds.size.x, initialBounds.size.y, initialBounds.size.z);
            if (largestDimension <= 0.001f)
            {
                return;
            }

            var factor = Mathf.Clamp(targetSize / largestDimension, 0.05f, 4f);
            instance.transform.localScale *= factor;
            if (!TryGetRendererBounds(instance, out var scaledBounds))
            {
                return;
            }

            var currentBottomCenter = new Vector3(scaledBounds.center.x, scaledBounds.min.y, scaledBounds.center.z);
            instance.transform.position += targetBottomCenter - currentBottomCenter;
        }

        private static bool TryGetRendererBounds(GameObject instance, out Bounds bounds)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return true;
        }
    }
}
