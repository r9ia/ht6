using UnityEngine;

namespace DreadDirector.Horror
{
    /// <summary>
    /// Keeps the debug creature visible and turns Director intensity into animation speed,
    /// agitation, and a bounded lunge toward the player. This is presentation behavior,
    /// not navigation, combat, or player damage.
    /// </summary>
    public sealed class ApparitionController : MonoBehaviour
    {
        [Header("Scene references")]
        public GameObject ApparitionVisual;
        public Transform Target;

        [Header("Intensity response")]
        [Range(0f, 1f)] public float StartingIntensity;
        [Range(0f, 1f)] public float AttackThreshold = 0.7f;
        [Min(0.1f)] public float IntensityResponse = 2.5f;
        [Min(0f)] public float MinimumAnimationSpeed = 0.65f;
        [Min(0f)] public float MaximumAnimationSpeed = 2.4f;
        [Min(0f)] public float MaximumBobMetres = 0.12f;
        [Min(0f)] public float MaximumYawDegrees = 12f;

        [Header("Debug attack lunge")]
        [Min(0.1f)] public float AttackStopDistance = 1.5f;
        [Min(0.1f)] public float AttackStartDistance = 5f;
        [Min(0.1f)] public float MinimumMoveSpeed = 1.25f;
        [Min(0.1f)] public float MaximumMoveSpeed = 5f;
        [Min(0f)] public float LungeDistance = 0.8f;
        [Min(0.1f)] public float ReturnSpeed = 2.5f;
        [Min(0.1f)] public float TurnSpeed = 7f;

        public float CurrentIntensity => currentIntensity;
        public bool IsAttacking => currentIntensity >= AttackThreshold && Target != null;

        private Animator[] animators;
        private Vector3 homePosition;
        private Vector3 movementPosition;
        private Vector3 homeScale;
        private Quaternion homeRotation;
        private Quaternion movementRotation;
        private float requestedIntensity;
        private float currentIntensity;
        private bool initialized;

        private void Awake()
        {
            if (ApparitionVisual == null)
            {
                enabled = false;
                return;
            }

            // Debug requirement: the monster is present from the first rendered frame.
            ApparitionVisual.SetActive(true);
            ResolveTarget();

            var visualTransform = ApparitionVisual.transform;
            homePosition = visualTransform.position;
            movementPosition = homePosition;
            homeRotation = visualTransform.rotation;
            movementRotation = homeRotation;
            homeScale = visualTransform.localScale;
            animators = ApparitionVisual.GetComponentsInChildren<Animator>(true);
            currentIntensity = Mathf.Clamp01(StartingIntensity);
            requestedIntensity = currentIntensity;
            ApplyAnimationSpeed();
            initialized = true;
        }

        private void Update()
        {
            if (!initialized || ApparitionVisual == null)
            {
                return;
            }

            ResolveTarget();
            currentIntensity = Mathf.MoveTowards(
                currentIntensity,
                requestedIntensity,
                IntensityResponse * Time.deltaTime);

            var visualTransform = ApparitionVisual.transform;
            var attackBlend = IsAttacking
                ? Mathf.InverseLerp(AttackThreshold, 1f, currentIntensity)
                : 0f;

            if (IsAttacking)
            {
                MoveForAttack(attackBlend);
            }
            else
            {
                movementPosition = Vector3.MoveTowards(
                    movementPosition,
                    homePosition,
                    ReturnSpeed * Time.deltaTime);
                movementRotation = Quaternion.Slerp(
                    movementRotation,
                    homeRotation,
                    TurnSpeed * Time.deltaTime);
            }

            var agitationFrequency = Mathf.Lerp(1.2f, 7f, currentIntensity);
            var agitationWave = Mathf.Sin(Time.time * agitationFrequency);
            var bob = agitationWave * MaximumBobMetres * currentIntensity;
            var yaw = agitationWave * MaximumYawDegrees * currentIntensity;
            visualTransform.position = movementPosition + Vector3.up * bob;
            visualTransform.rotation = movementRotation * Quaternion.Euler(0f, yaw, 0f);
            visualTransform.localScale = homeScale * (1f + Mathf.Abs(agitationWave) * 0.025f * currentIntensity);
            ApplyAnimationSpeed();
        }

        /// <summary>Applies a normalized Director intensity without hiding the creature.</summary>
        public void SetIntensity(float intensity)
        {
            requestedIntensity = Mathf.Clamp01(intensity);
            if (ApparitionVisual != null && !ApparitionVisual.activeSelf)
            {
                ApparitionVisual.SetActive(true);
            }
        }

        /// <summary>Compatibility entry point used by escalation events.</summary>
        public void Reveal(float intensity)
        {
            SetIntensity(intensity);
        }

        /// <summary>Calms the creature and sends it home while keeping it visible.</summary>
        public void Retreat()
        {
            SetIntensity(0f);
        }

        /// <summary>Recovery resets agitation; no reveal re-arm is needed for debug visibility.</summary>
        public void Rearm()
        {
            SetIntensity(0f);
        }

        private void MoveForAttack(float attackBlend)
        {
            var targetPosition = Target.position;
            targetPosition.y = movementPosition.y;
            var awayFromTarget = movementPosition - targetPosition;
            if (awayFromTarget.sqrMagnitude < 0.001f)
            {
                awayFromTarget = homePosition - targetPosition;
            }

            awayFromTarget.y = 0f;
            awayFromTarget.Normalize();

            // Increasing intensity reduces stand-off distance. The pulse produces an
            // obvious attack lunge while AttackStopDistance prevents camera overlap.
            var pulse = (Mathf.Sin(Time.time * Mathf.Lerp(3f, 8f, attackBlend)) + 1f) * 0.5f;
            var standOffDistance = Mathf.Lerp(AttackStartDistance, AttackStopDistance, attackBlend);
            standOffDistance += (1f - pulse) * LungeDistance * attackBlend;
            var destination = targetPosition + awayFromTarget * standOffDistance;
            var moveSpeed = Mathf.Lerp(MinimumMoveSpeed, MaximumMoveSpeed, attackBlend);
            movementPosition = Vector3.MoveTowards(movementPosition, destination, moveSpeed * Time.deltaTime);

            var towardTarget = targetPosition - movementPosition;
            towardTarget.y = 0f;
            if (towardTarget.sqrMagnitude > 0.001f)
            {
                // Both the imported Demon and procedural fallback are authored facing -Z.
                var targetRotation = Quaternion.LookRotation(-towardTarget.normalized, Vector3.up);
                movementRotation = Quaternion.Slerp(
                    movementRotation,
                    targetRotation,
                    TurnSpeed * Time.deltaTime);
            }
        }

        private void ResolveTarget()
        {
            if (Target != null || Camera.main == null)
            {
                return;
            }

            Target = Camera.main.transform.parent != null
                ? Camera.main.transform.parent
                : Camera.main.transform;
        }

        private void ApplyAnimationSpeed()
        {
            var speed = Mathf.Lerp(MinimumAnimationSpeed, MaximumAnimationSpeed, currentIntensity);
            for (var index = 0; index < animators.Length; index++)
            {
                if (animators[index] != null)
                {
                    animators[index].speed = speed;
                }
            }
        }
    }
}
