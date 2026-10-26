using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DownhillCarts
{
    public sealed class TimeTrialManager : MonoBehaviour
    {
        private const string BestTimeKey = "DownhillCarts.DownhillTestTrack.BestTime";

        [SerializeField] private GameObject cartPrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField, Min(0)] private int requiredCheckpointCount = 3;
        [SerializeField, Min(0f)] private float recoveryPenaltySeconds = 5f;
        [SerializeField, Min(0f)] private float fatalRecoveryDelay = 2f;
        [SerializeField, Min(0.1f)] private float stuckSpeedKph = 2f;
        [SerializeField, Min(0.1f)] private float stuckPromptDelay = 5f;
        [SerializeField, Min(0.1f)] private float recoveryHoldDuration = 0.8f;
        [SerializeField, Min(0.1f)] private float offTrackRecoveryDelay = 4f;

        private SoapboxCartController currentCart;
        private WheelCollider[] currentCartWheels;
        private Text timerText;
        private Text bestTimeText;
        private Text resultsText;
        private Text recoveryPromptText;
        private GameObject resultsPanel;
        private float raceStartTime;
        private float elapsedTime;
        private float bestTime;
        private float accumulatedPenalties;
        private float stoppedTimer;
        private float upsideDownTimer;
        private float fatalTimer;
        private float offTrackTimer;
        private float recoveryHoldTimer;
        private Vector3 recoveryPosition;
        private Quaternion recoveryRotation;
        private int highestCheckpoint;
        private bool raceRunning;
        private bool recoveryAvailable;
        private bool trackSurfaceDetectionAvailable;

        public void Configure(GameObject prefab, Transform startPoint, int checkpointCount)
        {
            cartPrefab = prefab;
            spawnPoint = startPoint;
            requiredCheckpointCount = checkpointCount;
        }

        private void Awake()
        {
            bestTime = PlayerPrefs.GetFloat(BestTimeKey, 0f);
            CreateInterface();
        }

        private void Start()
        {
            AssignCurrentCart(FindPlayerCart());
            trackSurfaceDetectionAvailable = FindAnyObjectByType<RaceTrackSurface>() != null;
            ResetRecoveryPointToStart();
            UpdateBestTimeDisplay();
            timerText.text = "CROSS START TO BEGIN";
        }

        private void Update()
        {
            if (raceRunning)
            {
                elapsedTime = Time.time - raceStartTime + accumulatedPenalties;
                timerText.text = FormatTime(elapsedTime);
                UpdateRecoveryState();
            }

            if (resultsPanel.activeSelf && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                RestartRace();
        }

        public void BeginRace(SoapboxCartController cart)
        {
            if (raceRunning)
                return;

            AssignCurrentCart(cart);
            elapsedTime = 0f;
            accumulatedPenalties = 0f;
            highestCheckpoint = 0;
            raceStartTime = Time.time;
            raceRunning = true;
            resultsPanel.SetActive(false);
            ResetRecoveryPointToStart();
            ResetRecoveryDetection();
            timerText.text = FormatTime(0f);
        }

        public void RegisterCheckpoint(SoapboxCartController cart, int checkpointOrder, Vector3 position, Quaternion rotation)
        {
            if (!raceRunning || cart != currentCart || checkpointOrder != highestCheckpoint + 1)
                return;

            highestCheckpoint = checkpointOrder;
            recoveryPosition = position;
            recoveryRotation = rotation;
        }

        public void FinishRace(SoapboxCartController cart)
        {
            if (!raceRunning || cart != currentCart)
                return;

            elapsedTime = Time.time - raceStartTime + accumulatedPenalties;
            raceRunning = false;
            recoveryPromptText.gameObject.SetActive(false);

            if (highestCheckpoint < requiredCheckpointCount)
            {
                timerText.text = FormatTime(elapsedTime);
                resultsText.text = $"INVALID RUN\n\nMISSED CHECKPOINT\n\n{highestCheckpoint} / {requiredCheckpointCount} CLEARED";
                resultsPanel.SetActive(true);
                return;
            }

            bool newRecord = bestTime <= 0f || elapsedTime < bestTime;
            if (newRecord)
            {
                bestTime = elapsedTime;
                PlayerPrefs.SetFloat(BestTimeKey, bestTime);
                PlayerPrefs.Save();
            }

            timerText.text = FormatTime(elapsedTime);
            resultsText.text = newRecord
                ? $"FINISH!\n\n{FormatTime(elapsedTime)}\n\nNEW FASTEST TIME!"
                : $"FINISH!\n\n{FormatTime(elapsedTime)}\n\nBEST  {FormatTime(bestTime)}";
            UpdateBestTimeDisplay();
            resultsPanel.SetActive(true);
        }

        public void RestartRace()
        {
            raceRunning = false;
            elapsedTime = 0f;
            accumulatedPenalties = 0f;
            highestCheckpoint = 0;
            resultsPanel.SetActive(false);
            recoveryPromptText.gameObject.SetActive(false);
            timerText.text = "CROSS START TO BEGIN";

            if (currentCart != null)
                Destroy(currentCart.gameObject);

            if (cartPrefab != null && spawnPoint != null)
            {
                GameObject newCart = Instantiate(cartPrefab, spawnPoint.position, spawnPoint.rotation);
                newCart.name = "Player Soapbox Cart";
                AssignCurrentCart(newCart.GetComponent<SoapboxCartController>());
                Physics.SyncTransforms();
            }
            ResetRecoveryPointToStart();
            ResetRecoveryDetection();
        }

        private void UpdateRecoveryState()
        {
            if (currentCart == null)
                return;

            Rigidbody cartBody = currentCart.GetComponent<Rigidbody>();
            if (cartBody == null)
                return;

            if (!currentCart.IsDriveable)
            {
                fatalTimer += Time.deltaTime;
                recoveryPromptText.gameObject.SetActive(true);
                recoveryPromptText.text = $"CRASHED — RECOVERING IN {Mathf.Max(0f, fatalRecoveryDelay - fatalTimer):0.0}";
                if (fatalTimer >= fatalRecoveryDelay)
                    RecoverCart();
                return;
            }

            if (trackSurfaceDetectionAvailable)
            {
                offTrackTimer = IsTouchingRaceTrack() ? 0f : offTrackTimer + Time.deltaTime;
                if (offTrackTimer >= offTrackRecoveryDelay)
                {
                    RecoverCart();
                    return;
                }
            }

            float speedKph = cartBody.linearVelocity.magnitude * 3.6f;
            stoppedTimer = speedKph < stuckSpeedKph ? stoppedTimer + Time.deltaTime : 0f;
            upsideDownTimer = Vector3.Dot(currentCart.transform.up, Vector3.up) < 0.25f
                ? upsideDownTimer + Time.deltaTime
                : 0f;
            recoveryAvailable = stoppedTimer >= stuckPromptDelay || upsideDownTimer >= 2f;

            if (!recoveryAvailable)
            {
                recoveryHoldTimer = 0f;
                recoveryPromptText.gameObject.SetActive(false);
                return;
            }

            Keyboard keyboard = Keyboard.current;
            bool holdingRecovery = keyboard != null && keyboard.rKey.isPressed;
            recoveryHoldTimer = holdingRecovery ? recoveryHoldTimer + Time.deltaTime : 0f;
            float holdProgress = Mathf.Clamp01(recoveryHoldTimer / recoveryHoldDuration);
            recoveryPromptText.gameObject.SetActive(true);
            recoveryPromptText.text = $"HOLD R TO RECOVER  +{recoveryPenaltySeconds:0}s  [{holdProgress * 100f:0}%]";
            if (recoveryHoldTimer >= recoveryHoldDuration)
                RecoverCart();
        }

        private void RecoverCart()
        {
            accumulatedPenalties += recoveryPenaltySeconds;
            SpawnFreshCart(recoveryPosition, recoveryRotation);
            ResetRecoveryDetection();
            recoveryPromptText.gameObject.SetActive(false);
        }

        private void SpawnFreshCart(Vector3 position, Quaternion rotation)
        {
            if (currentCart != null)
                Destroy(currentCart.gameObject);
            if (cartPrefab == null)
                return;

            GameObject newCart = Instantiate(cartPrefab, position, rotation);
            newCart.name = "Player Soapbox Cart";
            AssignCurrentCart(newCart.GetComponent<SoapboxCartController>());
            Physics.SyncTransforms();
        }

        private void AssignCurrentCart(SoapboxCartController cart)
        {
            currentCart = cart;
            currentCartWheels = cart == null ? null : cart.GetComponentsInChildren<WheelCollider>(true);
        }

        private static SoapboxCartController FindPlayerCart()
        {
            SoapboxCartController[] carts = FindObjectsByType<SoapboxCartController>();
            foreach (SoapboxCartController cart in carts)
            {
                if (cart.IsPlayerControlled)
                    return cart;
            }
            return null;
        }

        private bool IsTouchingRaceTrack()
        {
            if (currentCartWheels == null)
                return false;

            foreach (WheelCollider wheel in currentCartWheels)
            {
                if (wheel != null
                    && wheel.GetGroundHit(out WheelHit hit)
                    && hit.collider.GetComponentInParent<RaceTrackSurface>() != null)
                    return true;
            }
            return false;
        }

        private void ResetRecoveryPointToStart()
        {
            if (spawnPoint == null)
                return;
            recoveryPosition = spawnPoint.position;
            recoveryRotation = spawnPoint.rotation;
        }

        private void ResetRecoveryDetection()
        {
            stoppedTimer = 0f;
            upsideDownTimer = 0f;
            fatalTimer = 0f;
            offTrackTimer = 0f;
            recoveryHoldTimer = 0f;
            recoveryAvailable = false;
        }

        private void UpdateBestTimeDisplay()
        {
            bestTimeText.text = bestTime > 0f ? $"BEST  {FormatTime(bestTime)}" : "BEST  --:--.---";
        }

        private static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float remainingSeconds = seconds - minutes * 60f;
            return $"{minutes:00}:{remainingSeconds:00.000}";
        }

        private void CreateInterface()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject canvasObject = new("Time Trial UI");
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject timerPanel = CreatePanel(canvasObject.transform, "Timer Panel", new Vector2(520f, 92f), new Vector2(0f, -28f));
            RectTransform timerRect = timerPanel.GetComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.5f, 1f);
            timerRect.anchorMax = new Vector2(0.5f, 1f);
            timerRect.pivot = new Vector2(0.5f, 1f);
            timerText = CreateText(timerPanel.transform, "Timer", font, 38, TextAnchor.MiddleCenter);

            GameObject bestPanel = CreatePanel(canvasObject.transform, "Best Time Panel", new Vector2(350f, 64f), new Vector2(-28f, -28f));
            RectTransform bestRect = bestPanel.GetComponent<RectTransform>();
            bestRect.anchorMin = Vector2.one;
            bestRect.anchorMax = Vector2.one;
            bestRect.pivot = Vector2.one;
            bestTimeText = CreateText(bestPanel.transform, "Best Time", font, 25, TextAnchor.MiddleCenter);

            resultsPanel = CreatePanel(canvasObject.transform, "Finish Results", new Vector2(620f, 390f), Vector2.zero);
            RectTransform resultsRect = resultsPanel.GetComponent<RectTransform>();
            resultsRect.anchorMin = new Vector2(0.5f, 0.5f);
            resultsRect.anchorMax = new Vector2(0.5f, 0.5f);
            resultsRect.pivot = new Vector2(0.5f, 0.5f);

            resultsText = CreateText(resultsPanel.transform, "Results", font, 42, TextAnchor.UpperCenter);
            RectTransform resultsTextRect = resultsText.rectTransform;
            resultsTextRect.anchorMin = new Vector2(0f, 0.28f);
            resultsTextRect.anchorMax = new Vector2(1f, 0.94f);
            resultsTextRect.offsetMin = new Vector2(30f, 0f);
            resultsTextRect.offsetMax = new Vector2(-30f, 0f);

            GameObject buttonObject = new("Restart Button");
            buttonObject.transform.SetParent(resultsPanel.transform, false);
            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = new Color(0.92f, 0.22f, 0.04f, 1f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(RestartRace);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.08f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.08f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.sizeDelta = new Vector2(310f, 78f);
            Text buttonText = CreateText(buttonObject.transform, "Restart Label", font, 30, TextAnchor.MiddleCenter);
            buttonText.text = "RACE AGAIN  [R]";

            resultsPanel.SetActive(false);

            recoveryPromptText = CreateText(canvasObject.transform, "Recovery Prompt", font, 28, TextAnchor.MiddleCenter);
            RectTransform recoveryRect = recoveryPromptText.rectTransform;
            recoveryRect.anchorMin = new Vector2(0.5f, 0f);
            recoveryRect.anchorMax = new Vector2(0.5f, 0f);
            recoveryRect.pivot = new Vector2(0.5f, 0f);
            recoveryRect.sizeDelta = new Vector2(760f, 72f);
            recoveryRect.anchoredPosition = new Vector2(0f, 90f);
            recoveryPromptText.color = new Color(1f, 0.82f, 0.12f);
            recoveryPromptText.gameObject.SetActive(false);
            EnsureEventSystem();
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 size, Vector2 position)
        {
            GameObject panel = new(name);
            panel.transform.SetParent(parent, false);
            Image image = panel.AddComponent<Image>();
            image.color = new Color(0.025f, 0.035f, 0.045f, 0.88f);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return panel;
        }

        private static Text CreateText(Transform parent, string name, Font font, int size, TextAnchor alignment)
        {
            GameObject textObject = new(name);
            textObject.transform.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(14f, 8f);
            rect.offsetMax = new Vector2(-14f, -8f);
            return text;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;
            GameObject eventSystem = new("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }
    }
}
