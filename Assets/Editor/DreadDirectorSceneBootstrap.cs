using DreadDirector.Director;
using DreadDirector.Horror;
using DreadDirector.Network;
using DreadDirector.Player;
using DreadDirector.Presentation;
using DreadDirector.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DreadDirector.Editor
{
    /// <summary>Creates the complete Night Watch vertical slice and imports Georgia's authored level without Inspector wiring.</summary>
    public static class DreadDirectorSceneBootstrap
    {
        private const string ScenePath = "Assets/Scenes/DreadDirectorNightWatch.unity";
        private const string GeneratedFolder = "Assets/Generated/DreadDirector";
        private const string AutomaticBuildRevisionKey = "DreadDirector.AutomaticSceneBuildRevision";
        private const string AutomaticBuildRevision = "georgia-rooms-v1";

        [InitializeOnLoadMethod]
        private static void ScheduleMissingSceneBuild()
        {
            EditorApplication.delayCall += BuildMissingSceneWhenIdle;
        }

        private static void BuildMissingSceneWhenIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += BuildMissingSceneWhenIdle;
                return;
            }

            if (EditorPrefs.GetString(AutomaticBuildRevisionKey, string.Empty) == AutomaticBuildRevision &&
                System.IO.File.Exists(ScenePath))
            {
                return;
            }

            EditorPrefs.SetString(AutomaticBuildRevisionKey, AutomaticBuildRevision);
            BuildNightWatchScene();
        }

        [MenuItem("Dread Director/Build Night Watch Scene")]
        public static void BuildNightWatchScene()
        {
            EnsureFolder("Assets/Generated");
            EnsureFolder(GeneratedFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.008f, 0.012f, 0.03f);
            RenderSettings.fogDensity = 0.018f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.025f, 0.035f, 0.07f);

            var apparitionMaterial = GetOrCreateMaterial("Apparition", new Color(0.28f, 0.01f, 0.025f), new Color(0.8f, 0f, 0.015f));

            var usingGeorgiaLevel = DreadDirectorOptionalAssets.TryInstantiateGeorgiaLevel(scene, out var playerSpawn);
            if (!usingGeorgiaLevel)
            {
                var wallMaterial = GetOrCreateMaterial("Walls", new Color(0.075f, 0.09f, 0.13f), Color.black);
                var floorMaterial = GetOrCreateMaterial("Floor", new Color(0.028f, 0.033f, 0.045f), Color.black);
                var deskMaterial = GetOrCreateMaterial("Desk", new Color(0.08f, 0.045f, 0.025f), Color.black);
                var screenMaterial = GetOrCreateMaterial("Monitor", new Color(0.015f, 0.08f, 0.075f), new Color(0.01f, 0.9f, 0.7f));
                BuildRoom(wallMaterial, floorMaterial, deskMaterial, screenMaterial);
                DreadDirectorOptionalAssets.TryInstantiateEnvironment();
            }

            var playerCamera = BuildPlayerCamera(playerSpawn);
            var roomLight = BuildRoomLight(playerSpawn.position);
            var apparitionVisual = BuildApparition(apparitionMaterial, playerSpawn, usingGeorgiaLevel);
            var systems = BuildSystems(roomLight, apparitionVisual, playerCamera.parent);
            ValidateGeneratedScene(systems, roomLight, apparitionVisual, usingGeorgiaLevel);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = systems;
            Debug.Log($"[Dread Director] Built and saved {ScenePath}. Press Play, then use 1-4 to drive the demo.");
        }

        /// <summary>Batch-mode entry point used by CI or local validation.</summary>
        public static void BuildNightWatchSceneBatch()
        {
            BuildNightWatchScene();
        }

        private static void BuildRoom(Material wallMaterial, Material floorMaterial, Material deskMaterial, Material screenMaterial)
        {
            var room = new GameObject("Night Watch Security Room");
            CreatePrimitive(PrimitiveType.Plane, "Floor", room.transform, new Vector3(0f, 0f, 0f), new Vector3(1.2f, 1f, 1f), floorMaterial);
            CreatePrimitive(PrimitiveType.Cube, "Back Wall", room.transform, new Vector3(0f, 2.5f, 5.6f), new Vector3(12f, 5f, 0.2f), wallMaterial);
            CreatePrimitive(PrimitiveType.Cube, "Left Wall", room.transform, new Vector3(-6f, 2.5f, 0f), new Vector3(0.2f, 5f, 11.2f), wallMaterial);
            CreatePrimitive(PrimitiveType.Cube, "Right Wall", room.transform, new Vector3(6f, 2.5f, 0f), new Vector3(0.2f, 5f, 11.2f), wallMaterial);
            CreatePrimitive(PrimitiveType.Cube, "Ceiling", room.transform, new Vector3(0f, 5f, 0f), new Vector3(12f, 0.15f, 11.2f), wallMaterial);

            var desk = new GameObject("Security Desk");
            desk.transform.SetParent(room.transform);
            CreatePrimitive(PrimitiveType.Cube, "Desktop", desk.transform, new Vector3(0f, 0.95f, 1.25f), new Vector3(4.7f, 0.16f, 1.35f), deskMaterial);
            CreatePrimitive(PrimitiveType.Cube, "Desk Leg Left", desk.transform, new Vector3(-2f, 0.45f, 1.25f), new Vector3(0.18f, 0.9f, 1.15f), deskMaterial);
            CreatePrimitive(PrimitiveType.Cube, "Desk Leg Right", desk.transform, new Vector3(2f, 0.45f, 1.25f), new Vector3(0.18f, 0.9f, 1.15f), deskMaterial);
            CreatePrimitive(PrimitiveType.Cube, "Monitor Body", desk.transform, new Vector3(0f, 1.75f, 1.55f), new Vector3(2.15f, 1.35f, 0.18f), wallMaterial);
            CreatePrimitive(PrimitiveType.Cube, "Monitor Screen", desk.transform, new Vector3(0f, 1.75f, 1.445f), new Vector3(1.9f, 1.08f, 0.02f), screenMaterial);
            CreatePrimitive(PrimitiveType.Cube, "Monitor Stand", desk.transform, new Vector3(0f, 1.18f, 1.6f), new Vector3(0.35f, 0.45f, 0.3f), wallMaterial);
            CreatePrimitive(PrimitiveType.Cube, "Paper Stack", desk.transform, new Vector3(-1.45f, 1.08f, 1.1f), new Vector3(0.72f, 0.06f, 0.52f), wallMaterial);

            var window = CreatePrimitive(PrimitiveType.Cube, "Black Window", room.transform, new Vector3(-3.6f, 2.55f, 5.42f), new Vector3(3.1f, 2.5f, 0.03f), screenMaterial);
            window.GetComponent<Renderer>().material.SetColor("_EmissionColor", new Color(0.005f, 0.04f, 0.08f));
        }

        private static Transform BuildPlayerCamera(Pose spawn)
        {
            var player = new GameObject("Player");
            player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);

            var characterController = player.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.3f;
            characterController.center = new Vector3(0f, 0.9f, 0f);

            var cameraObject = new GameObject("Player Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform);
            cameraObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            cameraObject.transform.localRotation = Quaternion.identity;
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 62f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.004f, 0.007f, 0.018f);
            camera.nearClipPlane = 0.05f;
            cameraObject.AddComponent<AudioListener>();

            var movement = player.AddComponent<FirstPersonController>();
            movement.CameraTransform = cameraObject.transform;
            movement.MoveSpeed = 2.5f;
            return cameraObject.transform;
        }

        private static Light BuildRoomLight(Vector3 playerSpawn)
        {
            var lightObject = new GameObject("Failing Fluorescent Light");
            lightObject.transform.position = playerSpawn + new Vector3(0f, 2.6f, 1.2f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 13f;
            light.intensity = 1.25f;
            light.color = new Color(0.68f, 0.78f, 1f);
            light.shadows = LightShadows.Soft;
            return light;
        }

        private static GameObject BuildApparition(Material apparitionMaterial, Pose playerSpawn, bool usingGeorgiaLevel)
        {
            var apparition = new GameObject("Debug Monster Visual");
            apparition.transform.position = usingGeorgiaLevel
                ? playerSpawn.position + new Vector3(4.5f, 0f, 8f)
                : new Vector3(3.4f, 0f, 3.9f);
            if (DreadDirectorOptionalAssets.TryPopulateCreature(apparition.transform))
            {
                apparition.SetActive(true);
                return apparition;
            }

            CreatePrimitive(PrimitiveType.Capsule, "Body", apparition.transform, new Vector3(0f, 1.1f, 0f), new Vector3(0.72f, 1.55f, 0.72f), apparitionMaterial);
            CreatePrimitive(PrimitiveType.Sphere, "Head", apparition.transform, new Vector3(0f, 2.3f, 0f), new Vector3(0.72f, 0.72f, 0.72f), apparitionMaterial);
            var leftEye = CreatePrimitive(PrimitiveType.Sphere, "Left Eye", apparition.transform, new Vector3(-0.16f, 2.34f, -0.33f), new Vector3(0.1f, 0.1f, 0.1f), apparitionMaterial);
            var rightEye = CreatePrimitive(PrimitiveType.Sphere, "Right Eye", apparition.transform, new Vector3(0.16f, 2.34f, -0.33f), new Vector3(0.1f, 0.1f, 0.1f), apparitionMaterial);
            leftEye.GetComponent<Renderer>().material.SetColor("_EmissionColor", Color.red * 2f);
            rightEye.GetComponent<Renderer>().material.SetColor("_EmissionColor", Color.red * 2f);
            apparition.SetActive(true);
            return apparition;
        }

        private static GameObject BuildSystems(Light roomLight, GameObject apparitionVisual, Transform playerTarget)
        {
            var systems = new GameObject("Dread Director Systems");
            var bridge = systems.AddComponent<DirectorGameBridge>();
            var calibration = systems.AddComponent<CalibrationController>();
            var receiver = systems.AddComponent<DirectorUdpReceiver>();
            var fakeInput = systems.AddComponent<FakeDirectorInput>();
            var flicker = systems.AddComponent<LightFlicker>();
            var apparition = systems.AddComponent<ApparitionController>();
            var sting = systems.AddComponent<AudioStingController>();
            var hud = systems.AddComponent<BiometricDebugHud>();

            var voiceAnchor = new GameObject("Monster Voice");
            voiceAnchor.transform.SetParent(apparitionVisual.transform, false);
            voiceAnchor.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var narration = voiceAnchor.AddComponent<NarrationBridgeClient>();
            var voiceSource = voiceAnchor.GetComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.spatialBlend = 1f;
            voiceSource.dopplerLevel = 0f;
            voiceSource.rolloffMode = AudioRolloffMode.Linear;
            voiceSource.minDistance = narration.MinimumDistance;
            voiceSource.maxDistance = narration.MaximumDistance;

            calibration.DurationSeconds = 60f;
            calibration.Bridge = bridge;
            receiver.Port = 7777;
            receiver.Bridge = bridge;
            fakeInput.Bridge = bridge;
            flicker.RoomLight = roomLight;
            apparition.ApparitionVisual = apparitionVisual;
            apparition.Target = playerTarget;
            apparition.AttackThreshold = 0.7f;
            bridge.Calibration = calibration;
            bridge.LightFlicker = flicker;
            bridge.Apparition = apparition;
            bridge.AudioSting = sting;
            bridge.DebugHud = hud;
            bridge.Narration = narration;
            hud.Bridge = bridge;
            hud.Calibration = calibration;
            narration.DebugHud = hud;
            return systems;
        }

        private static void ValidateGeneratedScene(GameObject systems, Light roomLight, GameObject apparitionVisual, bool usingGeorgiaLevel)
        {
            var bridge = systems.GetComponent<DirectorGameBridge>();
            var receiver = systems.GetComponent<DirectorUdpReceiver>();
            var fakeInput = systems.GetComponent<FakeDirectorInput>();
            var narration = Object.FindAnyObjectByType<NarrationBridgeClient>();
            var playerController = Object.FindAnyObjectByType<FirstPersonController>();
            var voiceSource = narration != null ? narration.GetComponent<AudioSource>() : null;
            if (Camera.main == null || roomLight == null || bridge == null || receiver == null || fakeInput == null ||
                narration == null || voiceSource == null || playerController == null)
            {
                throw new System.InvalidOperationException("Night Watch scene is missing a required camera, player controller, monster voice, or Dread Director component.");
            }

            if (bridge.Calibration == null || bridge.LightFlicker == null || bridge.Apparition == null ||
                bridge.AudioSting == null || bridge.DebugHud == null || bridge.Narration != narration ||
                receiver.Bridge != bridge || fakeInput.Bridge != bridge || narration.DebugHud != bridge.DebugHud ||
                playerController.CameraTransform != Camera.main.transform || bridge.Apparition.Target != playerController.transform ||
                !narration.transform.IsChildOf(apparitionVisual.transform) || voiceSource.spatialBlend < 0.99f)
            {
                throw new System.InvalidOperationException("Night Watch scene contains an unwired Dread Director, player, or spatial monster-voice reference.");
            }

            if (apparitionVisual == null || !apparitionVisual.activeSelf)
            {
                throw new System.InvalidOperationException("Night Watch debug monster must exist and begin visible.");
            }

            if (usingGeorgiaLevel)
            {
                var levelRoot = GameObject.Find("Georgia Backrooms Level");
                if (levelRoot == null || levelRoot.transform.Find("RoomTiles") == null ||
                    levelRoot.GetComponentsInChildren<Renderer>(true).Length == 0 ||
                    levelRoot.GetComponentsInChildren<Collider>(true).Length == 0 ||
                    GameObject.Find("Night Watch Security Room") != null)
                {
                    throw new System.InvalidOperationException("Georgia's Backrooms world was not imported cleanly or still overlaps the primitive fallback room.");
                }
            }

            Debug.Log($"[Dread Director] Scene validation passed: {(usingGeorgiaLevel ? "Georgia Backrooms level" : "fallback room")}, player movement, camera, UDP/fake routing, effects, HUD, spatial monster voice, and visible intensity-driven monster are wired.");
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string objectName, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var primitive = GameObject.CreatePrimitive(type);
            primitive.name = objectName;
            primitive.transform.SetParent(parent);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            return primitive;
        }

        private static Material GetOrCreateMaterial(string name, Color baseColor, Color emissionColor)
        {
            var path = $"{GeneratedFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = baseColor;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", baseColor);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emissionColor);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            var name = System.IO.Path.GetFileName(assetPath);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
