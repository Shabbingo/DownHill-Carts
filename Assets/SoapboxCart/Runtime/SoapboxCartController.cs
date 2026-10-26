using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace DownhillCarts
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SoapboxCartController : MonoBehaviour
    {
        [Serializable] public sealed class FloatEvent : UnityEvent<float> { }
        [Serializable] public sealed class CrashEvent : UnityEvent<Vector3, float> { }

        [Header("Control")]
        [SerializeField] private bool playerControlled = true;

        [Header("Wheels")]
        [SerializeField] private WheelCollider frontLeft;
        [SerializeField] private WheelCollider frontRight;
        [SerializeField] private WheelCollider rearLeft;
        [SerializeField] private WheelCollider rearRight;
        [SerializeField] private Transform frontLeftVisual;
        [SerializeField] private Transform frontRightVisual;
        [SerializeField] private Transform rearLeftVisual;
        [SerializeField] private Transform rearRightVisual;

        [Header("Suspension")]
        [SerializeField, Min(0.01f)] private float suspensionDistance = 0.24f;
        [SerializeField, Min(100f)] private float suspensionSpring = 9000f;
        [SerializeField, Min(0f)] private float suspensionDamper = 1400f;
        [SerializeField, Range(0f, 1f)] private float suspensionTargetPosition = 0.52f;
        [SerializeField, Min(0f)] private float forceApplicationPoint = 0.08f;
        [SerializeField, Min(0f)] private float groundedDownforce = 2.5f;
        [SerializeField, Min(0f)] private float speedDownforce = 0.018f;

        [Header("Steering and lean")]
        [SerializeField, Range(5f, 45f)] private float maximumSteerAngle = 26f;
        [SerializeField, Range(2f, 30f)] private float highSpeedSteerAngle = 13f;
        [SerializeField, Min(1f)] private float fullSteeringReductionSpeedKph = 60f;
        [SerializeField, Min(0.01f)] private float steeringInputSmoothTime = 0.22f;
        [SerializeField, Min(0.01f)] private float steeringReturnSmoothTime = 0.16f;
        [SerializeField, Min(1f)] private float steeringWheelSpeed = 75f;
        [SerializeField, Min(0f)] private float leanForce = 520f;
        [SerializeField, Range(0f, 1f)] private float damagedSteeringMinimum = 0.3f;
        [SerializeField] private Transform riderVisual;
        [SerializeField, Range(0f, 30f)] private float visualLeanAngle = 13f;

        [Header("Braking")]
        [SerializeField, Min(0f)] private float maximumBrakeTorque = 900f;
        [SerializeField, Min(0.01f)] private float brakeApplyTime = 0.45f;
        [SerializeField, Min(0.01f)] private float brakeReleaseTime = 0.25f;

        [Header("Pumping")]
        [SerializeField, Min(0f)] private float pumpImpulse = 135f;
        [SerializeField, Min(0f)] private float pumpCooldown = 0.28f;
        [SerializeField, Min(0f)] private float pumpSpeedLimit = 8f;
        [SerializeField, Range(0f, 25f)] private float pumpVisualAngle = 10f;
        [SerializeField, Min(0.01f)] private float pumpVisualDuration = 0.2f;

        [Header("Speed")]
        [SerializeField, Min(1f)] private float maximumSpeedKph = 105f;
        [SerializeField, Range(1f, 3f)] private float downhillGravityMultiplier = 2.2f;
        [SerializeField, Min(0f)] private float minimumDownhillAcceleration = 1.4f;
        [SerializeField, Min(0f)] private float minimumDownhillGravityComponent = 0.12f;
        [SerializeField, Range(0f, 1f)] private float rollingLinearDamping = 0.008f;
        [SerializeField, Min(0f)] private float overspeedDrag = 5f;

        [Header("Health and crashes")]
        [SerializeField, Min(1f)] private float maximumHealth = 100f;
        [SerializeField, Min(0f)] private float damageStartSpeedKph = 18f;
        [SerializeField, Min(0f)] private float damagePerImpactSpeed = 2.2f;
        [SerializeField, Min(0f)] private float fatalImpactSpeedKph = 48f;
        [SerializeField, Range(0f, 1f)] private float fatalImpactAlignment = 0.72f;
        [SerializeField] private CartRiderRagdoll riderRagdoll;
        [SerializeField] private FloatEvent onHealthChanged = new();
        [SerializeField] private CrashEvent onFatalCrash = new();

        private Rigidbody body;
        private float health;
        private float rawSteerInput;
        private float steerInput;
        private float steerInputVelocity;
        private float brakeAmount;
        private float nextPumpTime;
        private float pumpVisualTimer;
        private bool pumpWasHeld;
        private bool pumpQueued;
        private bool driveable = true;
        private float externalSteerInput;
        private bool externalBrakeInput;
        private bool externalPumpPressed;

        public float Health => health;
        public float HealthNormalized => maximumHealth <= 0f ? 0f : health / maximumHealth;
        public float SpeedKph => body == null ? 0f : body.linearVelocity.magnitude * 3.6f;
        public bool IsDriveable => driveable;
        public bool IsPlayerControlled => playerControlled;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.linearDamping = rollingLinearDamping;
            health = maximumHealth;
            ConfigureWheelPhysics();
            if (playerControlled && GetComponent<CartCameraController>() == null)
                gameObject.AddComponent<CartCameraController>();
            if (playerControlled && GetComponent<CartSpeedEffects>() == null)
                gameObject.AddComponent<CartSpeedEffects>();
        }

        private void Update()
        {
            ReadInput();
            UpdateWheelVisuals();
            UpdateRiderVisual();
        }

        private void FixedUpdate()
        {
            ApplyQueuedPump();
            ApplySteeringAndLean();
            ApplyBrakes();
            ApplyDownhillGravityAssist();
            ApplyGroundingForce();
            LimitSpeed();
        }

        private void ConfigureWheelPhysics()
        {
            ConfigureWheel(frontLeft);
            ConfigureWheel(frontRight);
            ConfigureWheel(rearLeft);
            ConfigureWheel(rearRight);

            // Extra simulation steps keep the small wheels stable over uneven ground at speed.
            if (frontLeft != null)
                frontLeft.ConfigureVehicleSubsteps(5f, 12, 15);
        }

        private void ConfigureWheel(WheelCollider wheel)
        {
            if (wheel == null)
                return;

            wheel.suspensionDistance = suspensionDistance;
            wheel.forceAppPointDistance = forceApplicationPoint;
            JointSpring spring = wheel.suspensionSpring;
            spring.spring = suspensionSpring;
            spring.damper = suspensionDamper;
            spring.targetPosition = suspensionTargetPosition;
            wheel.suspensionSpring = spring;
        }

        private void ReadInput()
        {
            if (!driveable)
            {
                rawSteerInput = 0f;
                SmoothSteeringInput();
                SetBrakeTarget(false);
                return;
            }

            if (!playerControlled)
            {
                rawSteerInput = Mathf.Clamp(externalSteerInput, -1f, 1f);
                SmoothSteeringInput();
                SetBrakeTarget(externalBrakeInput);
                if (externalPumpPressed)
                    QueuePumpIfAvailable();
                externalPumpPressed = false;
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                rawSteerInput = 0f;
                SmoothSteeringInput();
                SetBrakeTarget(false);
                return;
            }

            rawSteerInput = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            SmoothSteeringInput();
            SetBrakeTarget(keyboard.sKey.isPressed);

            bool pumpHeld = keyboard.wKey.isPressed;
            if (pumpHeld && !pumpWasHeld)
                QueuePumpIfAvailable();
            pumpWasHeld = pumpHeld;
        }

        private void QueuePumpIfAvailable()
        {
            if (Time.time < nextPumpTime || SpeedKph > pumpSpeedLimit * 3.6f)
                return;
            pumpQueued = true;
            nextPumpTime = Time.time + pumpCooldown;
            pumpVisualTimer = pumpVisualDuration;
        }

        public void SetPlayerControlled(bool controlled)
        {
            playerControlled = controlled;
        }

        public void SetExternalInput(float steering, bool braking, bool pumpPressed)
        {
            externalSteerInput = Mathf.Clamp(steering, -1f, 1f);
            externalBrakeInput = braking;
            externalPumpPressed |= pumpPressed;
        }

        public void ResetCartForRespawn()
        {
            health = maximumHealth;
            driveable = true;
            rawSteerInput = 0f;
            steerInput = 0f;
            steerInputVelocity = 0f;
            brakeAmount = 0f;
            pumpQueued = false;
            onHealthChanged.Invoke(HealthNormalized);
        }

        private void SmoothSteeringInput()
        {
            bool movingAwayFromCentre = Mathf.Abs(rawSteerInput) > Mathf.Abs(steerInput);
            float smoothTime = movingAwayFromCentre ? steeringInputSmoothTime : steeringReturnSmoothTime;
            steerInput = Mathf.SmoothDamp(
                steerInput,
                rawSteerInput,
                ref steerInputVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.deltaTime);
        }

        private void ApplyQueuedPump()
        {
            if (!pumpQueued || !driveable)
                return;

            pumpQueued = false;
            Vector3 forwardAlongGround = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (forwardAlongGround.sqrMagnitude < 0.01f)
                forwardAlongGround = transform.forward;
            body.AddForce(forwardAlongGround * pumpImpulse, ForceMode.Impulse);
        }

        private void SetBrakeTarget(bool braking)
        {
            float target = braking ? 1f : 0f;
            float duration = braking ? brakeApplyTime : brakeReleaseTime;
            brakeAmount = Mathf.MoveTowards(brakeAmount, target, Time.deltaTime / duration);
        }

        private void ApplySteeringAndLean()
        {
            float healthHandling = Mathf.Lerp(damagedSteeringMinimum, 1f, HealthNormalized);
            float speedReduction = Mathf.InverseLerp(0f, fullSteeringReductionSpeedKph, SpeedKph);
            float availableSteerAngle = Mathf.Lerp(maximumSteerAngle, highSpeedSteerAngle, speedReduction);
            float targetAngle = steerInput * availableSteerAngle * healthHandling;
            float step = steeringWheelSpeed * Time.fixedDeltaTime;
            frontLeft.steerAngle = Mathf.MoveTowards(frontLeft.steerAngle, targetAngle, step);
            frontRight.steerAngle = Mathf.MoveTowards(frontRight.steerAngle, targetAngle, step);

            if (Mathf.Abs(steerInput) > 0.01f)
            {
                Vector3 riderWeightPosition = transform.position
                    + transform.right * steerInput * 0.42f
                    + transform.up * 0.55f;
                body.AddForceAtPosition(-transform.up * leanForce, riderWeightPosition);
            }
        }

        private void ApplyBrakes()
        {
            float torque = driveable ? brakeAmount * maximumBrakeTorque : maximumBrakeTorque;
            frontLeft.brakeTorque = torque;
            frontRight.brakeTorque = torque;
            rearLeft.brakeTorque = torque * 0.65f;
            rearRight.brakeTorque = torque * 0.65f;
        }

        private void ApplyGroundingForce()
        {
            bool anyWheelGrounded = frontLeft.isGrounded || frontRight.isGrounded
                || rearLeft.isGrounded || rearRight.isGrounded;
            if (!anyWheelGrounded)
                return;

            float speed = body.linearVelocity.magnitude;
            float acceleration = groundedDownforce + speed * speed * speedDownforce;
            body.AddForce(-transform.up * acceleration, ForceMode.Acceleration);
        }

        private void ApplyDownhillGravityAssist()
        {
            if (downhillGravityMultiplier <= 1f || !TryGetAverageGroundNormal(out Vector3 groundNormal))
                return;

            Vector3 forwardAlongRoad = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
            float naturalDownhillAcceleration = Vector3.Dot(Physics.gravity, forwardAlongRoad);
            if (naturalDownhillAcceleration < minimumDownhillGravityComponent)
                return;

            float targetDownhillAcceleration = Mathf.Max(
                naturalDownhillAcceleration * downhillGravityMultiplier,
                minimumDownhillAcceleration);
            float additionalAcceleration = targetDownhillAcceleration - naturalDownhillAcceleration;
            body.AddForce(forwardAlongRoad * additionalAcceleration, ForceMode.Acceleration);
        }

        private bool TryGetAverageGroundNormal(out Vector3 groundNormal)
        {
            groundNormal = Vector3.zero;
            int groundedWheels = AddGroundNormal(frontLeft, ref groundNormal)
                + AddGroundNormal(frontRight, ref groundNormal)
                + AddGroundNormal(rearLeft, ref groundNormal)
                + AddGroundNormal(rearRight, ref groundNormal);

            if (groundedWheels == 0)
                return false;
            groundNormal.Normalize();
            return true;
        }

        private static int AddGroundNormal(WheelCollider wheel, ref Vector3 normalSum)
        {
            if (wheel == null || !wheel.GetGroundHit(out WheelHit hit))
                return 0;
            normalSum += hit.normal;
            return 1;
        }

        private void LimitSpeed()
        {
            float maxSpeed = maximumSpeedKph / 3.6f;
            float speed = body.linearVelocity.magnitude;
            if (speed <= maxSpeed)
                return;

            body.AddForce(-body.linearVelocity.normalized * (speed - maxSpeed) * overspeedDrag, ForceMode.Acceleration);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!driveable || collision.contactCount == 0)
                return;

            float impactSpeed = collision.relativeVelocity.magnitude;
            float damageThreshold = damageStartSpeedKph / 3.6f;
            if (impactSpeed > damageThreshold)
                ApplyDamage((impactSpeed - damageThreshold) * damagePerImpactSpeed);

            ContactPoint contact = collision.GetContact(0);
            Vector3 impactDirection = collision.relativeVelocity.sqrMagnitude > 0.001f
                ? collision.relativeVelocity.normalized
                : -contact.normal;
            float alignment = Mathf.Abs(Vector3.Dot(impactDirection, contact.normal));
            if (impactSpeed >= fatalImpactSpeedKph / 3.6f && alignment >= fatalImpactAlignment)
                TriggerFatalCrash(contact.point, impactSpeed);
        }

        public void ApplyDamage(float amount)
        {
            if (amount <= 0f || !driveable)
                return;

            health = Mathf.Max(0f, health - amount);
            onHealthChanged.Invoke(HealthNormalized);
            if (health <= 0f)
                DisableCart();
        }

        public void Repair(float amount)
        {
            if (amount <= 0f)
                return;

            health = Mathf.Min(maximumHealth, health + amount);
            if (health > 0f)
                driveable = true;
            onHealthChanged.Invoke(HealthNormalized);
        }

        private void TriggerFatalCrash(Vector3 point, float impactSpeed)
        {
            DisableCart();
            if (riderRagdoll != null)
                riderRagdoll.Activate(body.linearVelocity, point);
            onFatalCrash.Invoke(point, impactSpeed);
        }

        private void DisableCart()
        {
            driveable = false;
            steerInput = 0f;
            brakeAmount = 1f;
        }

        private void UpdateWheelVisuals()
        {
            SyncWheel(frontLeft, frontLeftVisual);
            SyncWheel(frontRight, frontRightVisual);
            SyncWheel(rearLeft, rearLeftVisual);
            SyncWheel(rearRight, rearRightVisual);
        }

        private static void SyncWheel(WheelCollider wheel, Transform visual)
        {
            if (wheel == null || visual == null)
                return;
            wheel.GetWorldPose(out Vector3 position, out Quaternion rotation);
            visual.SetPositionAndRotation(position, rotation * Quaternion.Euler(0f, 0f, 90f));
        }

        private void UpdateRiderVisual()
        {
            if (riderVisual == null)
                return;

            pumpVisualTimer = Mathf.Max(0f, pumpVisualTimer - Time.deltaTime);
            float pumpLean = pumpVisualTimer > 0f
                ? Mathf.Sin((pumpVisualTimer / pumpVisualDuration) * Mathf.PI) * pumpVisualAngle
                : 0f;
            Quaternion target = Quaternion.Euler(pumpLean, 0f, -steerInput * visualLeanAngle);
            riderVisual.localRotation = Quaternion.Slerp(riderVisual.localRotation, target, 8f * Time.deltaTime);
        }

        private void OnValidate()
        {
            maximumHealth = Mathf.Max(1f, maximumHealth);
            highSpeedSteerAngle = Mathf.Min(highSpeedSteerAngle, maximumSteerAngle);
            health = Application.isPlaying ? Mathf.Min(health, maximumHealth) : maximumHealth;
        }
    }
}
