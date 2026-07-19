using DreadDirector.Horror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DreadDirector.Player
{
    /// <summary>Builds a simple viewmodel knife procedurally (no external model needed) and holds it in front of the camera.</summary>
    public sealed class HeldKnife : MonoBehaviour
    {
        [Header("Pose")]
        public Vector3 HoldPosition = new Vector3(0.26f, -0.26f, 0.42f);
        public Vector3 HoldRotationEuler = new Vector3(15f, -30f, 8f);

        [Header("Blade")]
        [Min(0.01f)] public float BladeLength = 0.16f;
        [Min(0.005f)] public float BladeWidth = 0.028f;
        [Min(0.001f)] public float BladeThickness = 0.004f;
        public Color BladeColor = new Color(0.75f, 0.77f, 0.8f);

        [Header("Handle")]
        public Vector3 HandleSize = new Vector3(0.024f, 0.024f, 0.11f);
        public Color HandleColor = new Color(0.08f, 0.07f, 0.06f);
        public Vector3 GuardSize = new Vector3(0.07f, 0.012f, 0.012f);
        public Color GuardColor = new Color(0.22f, 0.22f, 0.24f);

        [Header("Idle motion")]
        public float BobSpeed = 1.6f;
        public float BobAmount = 0.006f;
        public float SwayAmount = 0.012f;
        public float SwaySmooth = 8f;

        [Header("Swing")]
        [Tooltip("Auto-found if empty.")]
        public ApparitionController Apparition;
        [Min(0.05f)] public float SwingDuration = 0.22f;
        [Range(10f, 160f)] public float SwingArcDegrees = 75f;
        [Min(0f)] public float SwingThrust = 0.08f;
        [Range(0f, 1f)] public float SwingImpactTime = 0.45f;
        [Min(0.1f)] public float SwingHitDistance = 4.2f;
        [Range(1f, 179f)] public float SwingHitAngle = 130f;

        private Transform knifeRoot;
        private Vector3 swayOffset;
        private bool isSwinging;
        private float swingElapsed;
        private bool swingConnected;

        private void Awake()
        {
            if (Apparition == null)
            {
                Apparition = FindAnyObjectByType<ApparitionController>();
            }

            BuildKnife();

            Debug.Log($"[HeldKnife] Loaded. HitDistance={SwingHitDistance:0.00} HitAngle={SwingHitAngle:0} " +
                $"Apparition={(Apparition != null ? "found" : "MISSING")}");
        }

        /// <summary>Starts the slash animation, e.g. from a WiFi swing trigger sent by a handheld
        /// device (see <c>KnifeSwingUdpReceiver</c>). Ignored if a swing is already in progress.</summary>
        public void TriggerSwing()
        {
            if (isSwinging)
            {
                return;
            }

            isSwinging = true;
            swingElapsed = 0f;
            swingConnected = false;
        }

        private void Update()
        {
            if (knifeRoot == null)
            {
                return;
            }

            var targetSway = Vector3.zero;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var delta = mouse.delta.ReadValue();
                targetSway = new Vector3(-delta.x, -delta.y, 0f) * SwayAmount * 0.01f;
            }

            swayOffset = Vector3.Lerp(swayOffset, targetSway, Time.deltaTime * SwaySmooth);

            var bob = Mathf.Sin(Time.time * BobSpeed) * BobAmount;
            var idlePosition = HoldPosition + swayOffset + new Vector3(0f, bob, 0f);

            if (isSwinging)
            {
                UpdateSwing(idlePosition);
            }
            else
            {
                knifeRoot.localPosition = idlePosition;
                knifeRoot.localRotation = Quaternion.Euler(HoldRotationEuler);
            }
        }

        private void UpdateSwing(Vector3 idlePosition)
        {
            swingElapsed += Time.deltaTime;
            var t = Mathf.Clamp01(swingElapsed / SwingDuration);

            // Sweep the blade across in yaw, with a small forward thrust that peaks mid-swing.
            var eased = Mathf.SmoothStep(0f, 1f, t);
            var yaw = Mathf.Lerp(SwingArcDegrees * 0.5f, -SwingArcDegrees * 0.5f, eased);
            var thrust = Mathf.Sin(t * Mathf.PI) * SwingThrust;

            knifeRoot.localRotation = Quaternion.Euler(HoldRotationEuler + new Vector3(0f, yaw, 0f));
            knifeRoot.localPosition = idlePosition + new Vector3(0f, 0f, thrust);

            // Keep checking every frame from the impact point to the end of the swing (not just
            // one instant), so a moving target that's briefly out of range/angle can still be
            // caught a moment later without needing frame-perfect timing.
            if (!swingConnected && t >= SwingImpactTime)
            {
                swingConnected = TryResolveHit();
            }

            if (t >= 1f)
            {
                isSwinging = false;
                if (!swingConnected)
                {
                    Debug.Log("[HeldKnife] Swing missed: monster stayed out of reach/angle the whole swing.");
                }
            }
        }

        /// <summary>Attempted every frame during the swing's active window: if the creature is in
        /// front of the player and within reach, the swing lands and scares it off.</summary>
        private bool TryResolveHit()
        {
            if (Apparition == null || Apparition.ApparitionVisual == null)
            {
                Debug.Log("[HeldKnife] Swing missed: no ApparitionController/ApparitionVisual reference found.");
                return false;
            }

            var toMonster = Apparition.ApparitionVisual.transform.position - transform.position;
            var distance = toMonster.magnitude;
            if (distance > SwingHitDistance)
            {
                return false;
            }

            var angle = Vector3.Angle(transform.forward, toMonster);
            if (angle > SwingHitAngle * 0.5f)
            {
                return false;
            }

            Debug.Log($"[HeldKnife] Swing HIT at {distance:0.00}m, {angle:0}° off-center. Sending it home.");
            Apparition.Retreat();
            return true;
        }

        private void BuildKnife()
        {
            knifeRoot = new GameObject("Held Knife").transform;
            knifeRoot.SetParent(transform, false);
            knifeRoot.localPosition = HoldPosition;
            knifeRoot.localRotation = Quaternion.Euler(HoldRotationEuler);

            var guard = CreateCubePart("Guard", knifeRoot, Vector3.zero, GuardSize, GuardColor);

            var handleCenter = new Vector3(0f, 0f, -(HandleSize.z * 0.5f) - (GuardSize.z * 0.5f));
            CreateCubePart("Handle", knifeRoot, handleCenter, HandleSize, HandleColor);

            var bladeGo = new GameObject("Blade");
            bladeGo.transform.SetParent(knifeRoot, false);
            bladeGo.transform.localPosition = new Vector3(0f, 0f, GuardSize.z * 0.5f);

            var meshFilter = bladeGo.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = BuildBladeMesh();
            var meshRenderer = bladeGo.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = CreateMaterial(BladeColor, doubleSided: true);
        }

        private static Transform CreateCubePart(string name, Transform parent, Vector3 localPosition, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = CreateMaterial(color, doubleSided: false);
            return go.transform;
        }

        private Mesh BuildBladeMesh()
        {
            var halfWidth = BladeWidth * 0.5f;
            var halfThickness = BladeThickness * 0.5f;

            var topBackA = new Vector3(halfThickness, halfWidth, 0f);
            var botBackA = new Vector3(halfThickness, -halfWidth, 0f);
            var tipA = new Vector3(halfThickness, 0f, BladeLength);
            var topBackB = new Vector3(-halfThickness, halfWidth, 0f);
            var botBackB = new Vector3(-halfThickness, -halfWidth, 0f);
            var tipB = new Vector3(-halfThickness, 0f, BladeLength);

            var vertices = new[] { topBackA, botBackA, tipA, topBackB, botBackB, tipB };

            // Material is set to double-sided, so exact winding direction per triangle is not critical.
            var triangles = new[]
            {
                0, 1, 2, // face A
                5, 4, 3, // face B
                0, 2, 5, 0, 5, 3, // spine edge
                1, 4, 2, 4, 5, 2, // cutting edge
                0, 3, 1, 3, 4, 1, // back edge (guard side)
            };

            var mesh = new Mesh { name = "KnifeBladeMesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateMaterial(Color color, bool doubleSided)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            if (doubleSided && material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.45f);
            }

            return material;
        }
    }
}
