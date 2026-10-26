using UnityEngine;
using UnityEngine.InputSystem;

namespace DownhillCarts
{
    public sealed class CartCameraController : MonoBehaviour
    {
        public enum CameraView
        {
            ThirdPerson,
            FirstPerson
        }

        [Header("References")]
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private Transform firstPersonMount;
        [SerializeField] private Transform thirdPersonLookTarget;

        [Header("View")]
        [SerializeField] private CameraView startingView = CameraView.ThirdPerson;
        [SerializeField] private Key toggleKey = Key.C;
        [SerializeField] private Vector3 thirdPersonOffset = new(0f, 1.85f, -4.15f);
        [SerializeField, Min(0.01f)] private float thirdPersonSmoothTime = 0.14f;
        [SerializeField, Min(0f)] private float lookAheadDistance = 2.5f;
        [SerializeField, Min(0f)] private float speedPullbackDistance = 0.2f;

        [Header("Camera motion")]
        [SerializeField, Min(0f)] private float motionBeginsKph = 12f;
        [SerializeField, Min(1f)] private float fullMotionKph = 95f;
        [SerializeField, Range(0f, 0.2f)] private float maximumShakeDistance = 0.05f;
        [SerializeField, Range(0f, 3f)] private float maximumShakeAngle = 0.55f;
        [SerializeField, Min(0f)] private float shakeFrequency = 18f;
        [SerializeField, Min(0f)] private float turnRollStrength = 1.4f;
        [SerializeField, Range(0f, 12f)] private float maximumTurnRoll = 5f;

        [Header("Field of view")]
        [SerializeField, Range(30f, 100f)] private float firstPersonFov = 75f;
        [SerializeField, Range(30f, 100f)] private float thirdPersonFov = 72f;
        [SerializeField, Min(0f)] private float firstPersonSpeedFovIncrease = 18f;
        [SerializeField, Min(0f)] private float thirdPersonSpeedFovIncrease = 26f;
        [SerializeField, Min(1f)] private float fullFovSpeedKph = 95f;
        [SerializeField, Min(0f)] private float fovResponse = 8f;

        private Rigidbody cartBody;
        private Vector3 followVelocity;
        private Vector3 thirdPersonSmoothedPosition;
        private CameraView currentView;

        public CameraView CurrentView => currentView;

        private void Awake()
        {
            cartBody = GetComponent<Rigidbody>();
            currentView = startingView;
            EnsureMounts();
            AcquireCamera();
            SnapToCurrentView();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            bool keyboardToggle = keyboard != null && keyboard[toggleKey].wasPressedThisFrame;
            bool gamepadToggle = gamepad != null && gamepad.rightStickButton.wasPressedThisFrame;

            if (keyboardToggle || gamepadToggle)
                SetView(currentView == CameraView.ThirdPerson ? CameraView.FirstPerson : CameraView.ThirdPerson);
        }

        private void LateUpdate()
        {
            if (controlledCamera == null)
            {
                AcquireCamera();
                if (controlledCamera == null)
                    return;
            }

            if (currentView == CameraView.FirstPerson)
                UpdateFirstPerson();
            else
                UpdateThirdPerson();

            ApplySpeedMotion();
            UpdateFieldOfView();
        }

        public void SetView(CameraView view)
        {
            currentView = view;
            followVelocity = Vector3.zero;
            SnapToCurrentView();
        }

        public void ToggleView()
        {
            SetView(currentView == CameraView.ThirdPerson ? CameraView.FirstPerson : CameraView.ThirdPerson);
        }

        private void AcquireCamera()
        {
            if (controlledCamera == null)
                controlledCamera = Camera.main;

            if (controlledCamera == null)
            {
                GameObject cameraObject = new("Main Camera");
                cameraObject.tag = "MainCamera";
                controlledCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            SimpleFollowCamera oldFollow = controlledCamera.GetComponent<SimpleFollowCamera>();
            if (oldFollow != null)
                oldFollow.enabled = false;
            controlledCamera.nearClipPlane = 0.05f;
        }

        private void EnsureMounts()
        {
            if (firstPersonMount == null)
            {
                GameObject firstPersonObject = new("First Person View");
                firstPersonMount = firstPersonObject.transform;
                firstPersonMount.SetParent(transform, false);
                firstPersonMount.localPosition = new Vector3(0f, 1.83f, -0.04f);
            }

            if (thirdPersonLookTarget == null)
            {
                GameObject thirdPersonObject = new("Third Person Look Target");
                thirdPersonLookTarget = thirdPersonObject.transform;
                thirdPersonLookTarget.SetParent(transform, false);
                thirdPersonLookTarget.localPosition = new Vector3(0f, 0.85f, 0.5f);
            }
        }

        private void UpdateFirstPerson()
        {
            if (firstPersonMount == null)
                return;
            controlledCamera.transform.SetPositionAndRotation(firstPersonMount.position, firstPersonMount.rotation);
        }

        private void UpdateThirdPerson()
        {
            Transform lookTransform = thirdPersonLookTarget != null ? thirdPersonLookTarget : transform;
            float speedFactor = GetMotionIntensity();
            Vector3 dynamicOffset = thirdPersonOffset + Vector3.back * speedPullbackDistance * speedFactor;
            Vector3 desiredPosition = transform.TransformPoint(dynamicOffset);
            thirdPersonSmoothedPosition = Vector3.SmoothDamp(
                thirdPersonSmoothedPosition,
                desiredPosition,
                ref followVelocity,
                thirdPersonSmoothTime);
            controlledCamera.transform.position = thirdPersonSmoothedPosition;

            Vector3 lookPoint = lookTransform.position + transform.forward * lookAheadDistance;
            Vector3 lookDirection = lookPoint - controlledCamera.transform.position;
            if (lookDirection.sqrMagnitude > 0.001f)
                controlledCamera.transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
        }

        private void ApplySpeedMotion()
        {
            float intensity = GetMotionIntensity();
            if (intensity <= 0f || cartBody == null)
                return;

            float time = Time.time * shakeFrequency;
            float horizontalNoise = Mathf.PerlinNoise(time, 1.37f) * 2f - 1f;
            float verticalNoise = Mathf.PerlinNoise(2.41f, time) * 2f - 1f;
            float firstPersonMultiplier = currentView == CameraView.FirstPerson ? 0.55f : 1f;
            float shakeDistance = maximumShakeDistance * intensity * firstPersonMultiplier;
            controlledCamera.transform.position += controlledCamera.transform.right * horizontalNoise * shakeDistance
                + controlledCamera.transform.up * verticalNoise * shakeDistance;

            float localYawSpeed = transform.InverseTransformDirection(cartBody.angularVelocity).y;
            float turnRoll = Mathf.Clamp(-localYawSpeed * turnRollStrength, -maximumTurnRoll, maximumTurnRoll) * intensity;
            float shakeAngle = verticalNoise * maximumShakeAngle * intensity * firstPersonMultiplier;
            controlledCamera.transform.rotation *= Quaternion.Euler(shakeAngle, 0f, turnRoll + horizontalNoise * shakeAngle);
        }

        private float GetMotionIntensity()
        {
            float speedKph = cartBody == null ? 0f : cartBody.linearVelocity.magnitude * 3.6f;
            return Mathf.InverseLerp(motionBeginsKph, fullMotionKph, speedKph);
        }

        private void UpdateFieldOfView()
        {
            float baseFov = currentView == CameraView.FirstPerson ? firstPersonFov : thirdPersonFov;
            float fovIncrease = currentView == CameraView.FirstPerson
                ? firstPersonSpeedFovIncrease
                : thirdPersonSpeedFovIncrease;
            float speedKph = cartBody == null ? 0f : cartBody.linearVelocity.magnitude * 3.6f;
            float speedFactor = Mathf.Clamp01(speedKph / fullFovSpeedKph);
            float targetFov = baseFov + fovIncrease * speedFactor;
            controlledCamera.fieldOfView = Mathf.Lerp(controlledCamera.fieldOfView, targetFov, fovResponse * Time.deltaTime);
        }

        private void SnapToCurrentView()
        {
            if (controlledCamera == null)
                return;

            if (currentView == CameraView.FirstPerson && firstPersonMount != null)
            {
                controlledCamera.transform.SetPositionAndRotation(firstPersonMount.position, firstPersonMount.rotation);
            }
            else
            {
                thirdPersonSmoothedPosition = transform.TransformPoint(thirdPersonOffset);
                controlledCamera.transform.position = thirdPersonSmoothedPosition;
                UpdateThirdPerson();
            }
        }
    }
}
