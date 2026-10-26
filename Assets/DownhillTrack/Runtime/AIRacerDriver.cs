using UnityEngine;

namespace DownhillCarts
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(SoapboxCartController), typeof(Rigidbody))]
    public sealed class AIRacerDriver : MonoBehaviour
    {
        [SerializeField] private AIRacingLine racingLine;
        [SerializeField, Range(0.65f, 1.15f)] private float skill = 0.9f;
        [SerializeField] private Color racerColor = new(0.1f, 0.55f, 1f);
        [SerializeField, Range(-2f, 2f)] private float preferredLaneOffset;
        [SerializeField, Range(0f, 1.5f)] private float laneWander = 0.22f;
        [SerializeField, Min(10f)] private float straightSpeedKph = 96f;
        [SerializeField, Min(5f)] private float hairpinSpeedKph = 35f;
        [SerializeField, Min(0f)] private float steeringLookAhead = 5f;
        [SerializeField, Min(1f)] private float barrierProbeDistance = 4.5f;
        [SerializeField, Range(0f, 2f)] private float barrierAvoidanceStrength = 0.52f;
        [SerializeField, Min(0.01f)] private float barrierSteeringSmoothTime = 0.24f;
        [Header("Traffic avoidance")]
        [SerializeField, Min(1f)] private float trafficProbeDistance = 11f;
        [SerializeField, Range(0f, 1f)] private float trafficAvoidanceStrength = 0.58f;
        [SerializeField, Min(0.5f)] private float trafficBrakeDistance = 4.5f;
        [SerializeField, Min(0.01f)] private float trafficSteeringSmoothTime = 0.3f;
        [SerializeField, Min(0f)] private float recoveryDelay = 6f;

        private SoapboxCartController controller;
        private Rigidbody body;
        private int currentPoint;
        private float nextPumpTime;
        private float stoppedTimer;
        private float lanePhase;
        private float smoothedBarrierSteering;
        private float barrierSteeringVelocity;
        private float smoothedTrafficSteering;
        private float trafficSteeringVelocity;
        private bool finished;
        private readonly RaycastHit[] sensorHits = new RaycastHit[12];

        public void Configure(AIRacingLine line, float racerSkill, Color color, float laneOffset)
        {
            racingLine = line;
            skill = racerSkill;
            racerColor = color;
            preferredLaneOffset = laneOffset;
            lanePhase = laneOffset * 2.37f + racerSkill * 5.1f;
        }

        private void Awake()
        {
            controller = GetComponent<SoapboxCartController>();
            body = GetComponent<Rigidbody>();
            controller.SetPlayerControlled(false);
        }

        private void Start()
        {
            if (racingLine == null)
                racingLine = FindAnyObjectByType<AIRacingLine>();
            if (racingLine != null)
                currentPoint = racingLine.FindClosestPoint(transform.position);
            ApplyRacerColour();
        }

        private void Update()
        {
            if (racingLine == null || racingLine.PointCount < 2)
                return;

            if (finished)
            {
                controller.SetExternalInput(0f, true, false);
                return;
            }

            currentPoint = racingLine.FindClosestPoint(transform.position, Mathf.Max(0, currentPoint - 2));
            if (currentPoint >= racingLine.PointCount - 3)
            {
                finished = true;
                controller.SetExternalInput(0f, true, false);
                return;
            }

            float speedKph = body.linearVelocity.magnitude * 3.6f;
            int lookAheadPoints = Mathf.RoundToInt(steeringLookAhead + speedKph * 0.045f);
            int targetIndex = Mathf.Min(currentPoint + lookAheadPoints, racingLine.PointCount - 1);
            float cornerSeverity = MeasureUpcomingCorner(targetIndex);
            Vector3 lineDirection = racingLine.GetDirection(targetIndex);
            Vector3 lineRight = Vector3.Cross(Vector3.up, lineDirection).normalized;
            float wander = Mathf.Sin(targetIndex * 0.075f + lanePhase) * laneWander;
            float lateralOffset = (preferredLaneOffset + wander) * (1f - cornerSeverity * 0.82f);
            Vector3 target = racingLine.GetPoint(targetIndex) + lineRight * lateralOffset;
            Vector3 localTarget = transform.InverseTransformPoint(target);
            float targetAngle = Mathf.Atan2(localTarget.x, Mathf.Max(0.1f, localTarget.z)) * Mathf.Rad2Deg;
            float steering = Mathf.Clamp(targetAngle / Mathf.Lerp(34f, 24f, skill), -1f, 1f);

            float desiredSpeed = Mathf.Lerp(straightSpeedKph, hairpinSpeedKph, cornerSeverity);
            desiredSpeed *= Mathf.Lerp(0.9f, 1.07f, skill);
            float trafficAvoidance = GetTrafficAvoidance(speedKph, out bool racerTooClose);
            smoothedTrafficSteering = Mathf.SmoothDamp(
                smoothedTrafficSteering,
                trafficAvoidance,
                ref trafficSteeringVelocity,
                trafficSteeringSmoothTime,
                1.6f,
                Time.deltaTime);
            float barrierAvoidance = GetBarrierAvoidance(speedKph, out bool barrierAhead);
            smoothedBarrierSteering = Mathf.SmoothDamp(
                smoothedBarrierSteering,
                barrierAvoidance,
                ref barrierSteeringVelocity,
                barrierSteeringSmoothTime,
                2.2f,
                Time.deltaTime);
            steering = Mathf.Clamp(steering + smoothedTrafficSteering + smoothedBarrierSteering, -1f, 1f);
            bool braking = speedKph > desiredSpeed || barrierAhead || racerTooClose;
            bool pump = !braking && speedKph < 24f && Time.time >= nextPumpTime;
            if (pump)
                nextPumpTime = Time.time + Mathf.Lerp(0.65f, 0.4f, skill);

            controller.SetExternalInput(steering, braking, pump);
            UpdateRecovery(speedKph);
        }

        private float GetTrafficAvoidance(float speedKph, out bool racerTooClose)
        {
            racerTooClose = false;
            float probeDistance = trafficProbeDistance * Mathf.Lerp(0.75f, 1.3f, Mathf.InverseLerp(15f, 100f, speedKph));
            Vector3 origin = transform.position + transform.up * 0.5f + transform.forward * 0.8f;
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                0.72f,
                transform.forward,
                sensorHits,
                probeDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            AIRacerDriver closestRacer = null;
            float closestDistance = probeDistance;
            Vector3 closestLocalPosition = Vector3.zero;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = sensorHits[i].collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                    continue;
                AIRacerDriver otherRacer = hitCollider.GetComponentInParent<AIRacerDriver>();
                if (otherRacer == null || otherRacer == this || sensorHits[i].distance >= closestDistance)
                    continue;

                Vector3 localPosition = transform.InverseTransformPoint(otherRacer.transform.position);
                if (localPosition.z <= 0f)
                    continue;
                closestRacer = otherRacer;
                closestDistance = sensorHits[i].distance;
                closestLocalPosition = localPosition;
            }

            if (closestRacer == null)
                return 0f;

            float passingDirection;
            if (closestLocalPosition.x > 0.18f)
                passingDirection = -1f;
            else if (closestLocalPosition.x < -0.18f)
                passingDirection = 1f;
            else if (Mathf.Abs(preferredLaneOffset) > 0.1f)
                passingDirection = Mathf.Sign(preferredLaneOffset);
            else
                passingDirection = Mathf.Sin(lanePhase) >= 0f ? 1f : -1f;

            Rigidbody otherBody = closestRacer.body;
            float closingSpeed = otherBody == null
                ? 0f
                : Vector3.Dot(body.linearVelocity - otherBody.linearVelocity, transform.forward);
            racerTooClose = closestDistance < trafficBrakeDistance
                || (closingSpeed > 2f && closestDistance < trafficBrakeDistance * 1.65f);
            float proximity = 1f - Mathf.Clamp01(closestDistance / probeDistance);
            return passingDirection * proximity * trafficAvoidanceStrength;
        }

        private float GetBarrierAvoidance(float speedKph, out bool barrierAhead)
        {
            float distance = barrierProbeDistance * Mathf.Lerp(0.8f, 1.08f, Mathf.InverseLerp(15f, 100f, speedKph));
            Vector3 origin = transform.position + transform.up * 0.55f + transform.forward * 0.6f;
            Vector3 leftDirection = (transform.forward - transform.right * 0.3f).normalized;
            Vector3 rightDirection = (transform.forward + transform.right * 0.3f).normalized;
            float leftProximity = ProbeBarrier(origin, leftDirection, distance);
            float rightProximity = ProbeBarrier(origin, rightDirection, distance);
            float forwardProximity = ProbeBarrier(origin, transform.forward, distance * 0.8f);

            barrierAhead = forwardProximity > 0.58f || leftProximity > 0.82f || rightProximity > 0.82f;
            // A wall on the left produces positive/right steering, and vice versa.
            return Mathf.Clamp((leftProximity - rightProximity) * barrierAvoidanceStrength, -1f, 1f);
        }

        private float ProbeBarrier(Vector3 origin, Vector3 direction, float distance)
        {
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                0.18f,
                direction,
                sensorHits,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            float closest = distance;
            bool found = false;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = sensorHits[i].collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                    continue;
                if (hitCollider.GetComponentInParent<TrackBarrier>() == null)
                    continue;
                closest = Mathf.Min(closest, sensorHits[i].distance);
                found = true;
            }
            return found ? 1f - closest / distance : 0f;
        }

        private float MeasureUpcomingCorner(int fromIndex)
        {
            int middleIndex = Mathf.Min(fromIndex + 7, racingLine.PointCount - 1);
            int farIndex = Mathf.Min(fromIndex + 15, racingLine.PointCount - 1);
            Vector3 firstDirection = (racingLine.GetPoint(middleIndex) - racingLine.GetPoint(fromIndex)).normalized;
            Vector3 secondDirection = (racingLine.GetPoint(farIndex) - racingLine.GetPoint(middleIndex)).normalized;
            float angle = Vector3.Angle(firstDirection, secondDirection);
            return Mathf.InverseLerp(4f, 48f, angle);
        }

        private void UpdateRecovery(float speedKph)
        {
            bool needsRecovery = speedKph < 1.2f || !controller.IsDriveable
                || Vector3.Dot(transform.up, Vector3.up) < 0.1f;
            stoppedTimer = needsRecovery ? stoppedTimer + Time.deltaTime : 0f;
            if (stoppedTimer < recoveryDelay)
                return;

            int respawnIndex = Mathf.Clamp(currentPoint + 2, 0, racingLine.PointCount - 2);
            Vector3 point = racingLine.GetPoint(respawnIndex);
            Vector3 next = racingLine.GetPoint(respawnIndex + 1);
            transform.SetPositionAndRotation(point + Vector3.up * 0.8f, Quaternion.LookRotation(next - point, Vector3.up));
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            controller.ResetCartForRespawn();
            currentPoint = respawnIndex;
            stoppedTimer = 0f;
        }

        private void ApplyRacerColour()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            MaterialPropertyBlock properties = new();
            foreach (Renderer renderer in renderers)
            {
                string objectName = renderer.gameObject.name;
                if (!objectName.Contains("Frame") && !objectName.Contains("Axle"))
                    continue;
                renderer.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", racerColor);
                properties.SetColor("_Color", racerColor);
                renderer.SetPropertyBlock(properties);
            }
        }
    }
}
