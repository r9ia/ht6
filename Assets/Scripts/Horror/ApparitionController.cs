using UnityEngine;

namespace DreadDirector.Horror
{
    /// <summary>
    /// Drives the creature as a relentless stalker: it chases the player continuously, with
    /// speed and agitation scaled by Director intensity, and only backs off when the Director
    /// reports panic. When it closes in, it lunges and raises a jump-scare event. This is
    /// presentation behavior only — no navigation mesh, combat, or player damage.
    /// </summary>
    public sealed class ApparitionController : MonoBehaviour
    {
        [Header("Scene references")]
        public GameObject ApparitionVisual;
        public Transform Target;

        [Header("Intensity response")]
        [Range(0f, 1f)] public float StartingIntensity;
        [Min(0.1f)] public float IntensityResponse = 2.5f;
        [Min(0f)] public float MinimumAnimationSpeed = 0.65f;
        [Min(0f)] public float MaximumAnimationSpeed = 2.4f;
        [Min(0f)] public float MaximumBobMetres = 0.12f;
        [Min(0f)] public float MaximumYawDegrees = 12f;

        [Header("Chase")]
        [Min(0.1f)] public float StopDistance = 1.4f;
        [Min(0f)] public float MinimumChaseSpeed = 1.15f;   // always creeps toward the player
        [Min(0.1f)] public float MaximumChaseSpeed = 6.5f;
        [Min(0.1f)] public float TurnSpeed = 8f;
        [Min(0.1f)] public float RetreatSpeed = 3.5f;

        [Header("Jump scare")]
        [Min(0.1f)] public float JumpScareDistance = 2.2f;
        [Min(0f)] public float JumpScareCooldown = 6f;
        [Range(0f, 1f)] public float JumpScareMinIntensity = 0.15f;
        [Min(0.2f)] public float JumpScareFaceDistance = 2.5f;

        [Header("Contact kill")]
        [Min(0f)] public float KillDistance = 1.1f;

        /// <summary>Raised when the creature lunges into the player's face; carries intensity 0..1.</summary>
        public event System.Action<float> JumpScareTriggered;

        /// <summary>Raised once when the creature reaches the player during an active hunt (lethal contact).</summary>
        public event System.Action PlayerCaught;

        public float CurrentIntensity => currentIntensity;
        public bool BackingOff => backingOff;

        private Animator[] animators;
        private Vector3 homePosition;
        private Vector3 movementPosition;
        private Vector3 homeScale;
        private Quaternion movementRotation;
        private float requestedIntensity;
        private float currentIntensity;
        private bool backingOff;
        private bool initialized;
        private bool hasCaught;
        private float nextJumpScareTime;

        private void Awake()
        {
            if (ApparitionVisual == null)
            {
                enabled = false;
                return;
            }

            // Debug requirement: the creature is present from the first rendered frame.
            ApparitionVisual.SetActive(true);
            ResolveTarget();

            var visualTransform = ApparitionVisual.transform;
            homePosition = visualTransform.position;
            movementPosition = homePosition;
            movementRotation = visualTransform.rotation;
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
            currentIntensity = Mathf.MoveTowards(currentIntensity, requestedIntensity, IntensityResponse * Time.deltaTime);

            if (backingOff || Target == null)
            {
                // Panic / back-off: retreat toward the home spot and stop hunting.
                movementPosition = Vector3.MoveTowards(movementPosition, homePosition, RetreatSpeed * Time.deltaTime);
            }
            else
            {
                Chase();
            }

            // Nervous agitation layered on top of the movement position.
            var agitationFrequency = Mathf.Lerp(1.2f, 7f, currentIntensity);
            var agitationWave = Mathf.Sin(Time.time * agitationFrequency);
            var bob = agitationWave * MaximumBobMetres * currentIntensity;
            var yaw = agitationWave * MaximumYawDegrees * currentIntensity;
            var visualTransform = ApparitionVisual.transform;
            visualTransform.position = movementPosition + Vector3.up * bob;
            visualTransform.rotation = movementRotation * Quaternion.Euler(0f, yaw, 0f);
            visualTransform.localScale = homeScale * (1f + Mathf.Abs(agitationWave) * 0.025f * currentIntensity);
            ApplyAnimationSpeed();
        }

        private void Chase()
        {
            var targetPosition = Target.position;
            targetPosition.y = movementPosition.y;
            var toTarget = targetPosition - movementPosition;
            toTarget.y = 0f;
            var distance = toTarget.magnitude;
            var direction = distance > 0.001f ? toTarget / distance : Vector3.forward;

            // Move toward the player. The effective stop distance shrinks as intensity rises, so a
            // calm creature hovers at StopDistance but an aggressive one closes all the way in for
            // the kill. Speed also scales with intensity and never reaches zero.
            var effectiveStop = Mathf.Lerp(StopDistance, Mathf.Min(KillDistance * 0.5f, StopDistance), currentIntensity);
            var destination = targetPosition - direction * effectiveStop;
            var speed = Mathf.Lerp(MinimumChaseSpeed, MaximumChaseSpeed, currentIntensity);
            movementPosition = Vector3.MoveTowards(movementPosition, destination, speed * Time.deltaTime);

            // Face the player (both the imported Demon and procedural fallback are authored facing -Z).
            if (distance > 0.001f)
            {
                var targetRotation = Quaternion.LookRotation(-direction, Vector3.up);
                movementRotation = Quaternion.Slerp(movementRotation, targetRotation, TurnSpeed * Time.deltaTime);
            }

            // Lethal contact: once the creature actually reaches the player it catches them (once).
            if (!hasCaught && distance <= KillDistance)
            {
                hasCaught = true;
                PlayerCaught?.Invoke();
                return;
            }

            if (distance <= JumpScareDistance && currentIntensity >= JumpScareMinIntensity && Time.time >= nextJumpScareTime)
            {
                TriggerJumpScare();
            }
        }

        /// <summary>Applies a normalized Director intensity and resumes the chase.</summary>
        public void SetIntensity(float intensity)
        {
            requestedIntensity = Mathf.Clamp01(intensity);
            backingOff = false;
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

        /// <summary>Panic / back-off: the creature retreats and stops hunting until re-armed.</summary>
        public void Retreat()
        {
            backingOff = true;
            requestedIntensity = 0f;
        }

        /// <summary>Recovery: calm the creature but keep it slowly stalking (not backing off).</summary>
        public void Rearm()
        {
            backingOff = false;
            requestedIntensity = 0.12f;
        }

        /// <summary>Full reset used on respawn: send the creature home and calm it completely.</summary>
        public void ResetToHome()
        {
            hasCaught = false;
            backingOff = false;
            requestedIntensity = Mathf.Clamp01(StartingIntensity);
            currentIntensity = requestedIntensity;
            movementPosition = homePosition;
            movementRotation = Quaternion.identity;
            nextJumpScareTime = 0f;
            if (ApparitionVisual != null)
            {
                ApparitionVisual.transform.position = homePosition;
            }
        }

        /// <summary>Snaps the creature into the player's face, ramps intensity, and raises the event.</summary>
        public void TriggerJumpScare()
        {
            nextJumpScareTime = Time.time + JumpScareCooldown;
            currentIntensity = 1f;
            requestedIntensity = Mathf.Max(requestedIntensity, 0.85f);

            if (Target != null)
            {
                var toMonster = movementPosition - Target.position;
                toMonster.y = 0f;
                var side = toMonster.sqrMagnitude > 0.001f ? toMonster.normalized : Vector3.forward;
                var facePosition = Target.position + side * JumpScareFaceDistance;
                movementPosition = new Vector3(facePosition.x, movementPosition.y, facePosition.z);
                movementRotation = Quaternion.LookRotation(-side, Vector3.up);
            }

            JumpScareTriggered?.Invoke(currentIntensity);
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
