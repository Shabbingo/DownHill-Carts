using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DownhillCarts
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CartSpeedEffects : MonoBehaviour
    {
        [Header("Speed range")]
        [SerializeField, Min(0f)] private float effectsBeginKph = 12f;
        [SerializeField, Min(1f)] private float fullEffectsKph = 95f;

        [Header("Wind")]
        [SerializeField, Range(0f, 1f)] private float maximumWindVolume = 0.5f;
        [SerializeField, Range(0.1f, 3f)] private float minimumWindPitch = 0.65f;
        [SerializeField, Range(0.1f, 3f)] private float maximumWindPitch = 1.35f;

        [Header("Speed lines")]
        [SerializeField] private bool showSpeedLines = true;
        [SerializeField, Min(0f)] private float maximumLinesPerSecond = 120f;
        [SerializeField] private Color lineColor = new(1f, 1f, 1f, 0.32f);

        [Header("Post processing")]
        [SerializeField] private bool enableMotionBlur = true;
        [SerializeField, Range(0f, 1f)] private float maximumMotionBlur = 0.38f;

        [Header("HUD")]
        [SerializeField] private bool showSpeedometer = true;

        private Rigidbody cartBody;
        private AudioSource windSource;
        private AudioLowPassFilter windFilter;
        private ParticleSystem speedLines;
        private Camera attachedCamera;
        private MotionBlur motionBlur;
        private GUIStyle speedStyle;
        private GUIStyle speedShadowStyle;
        private float emissionAccumulator;

        private void Awake()
        {
            cartBody = GetComponent<Rigidbody>();
            CreateWindAudio();
            CreatePostProcessing();
        }

        private void Start()
        {
            AttachSpeedLines();
        }

        private void Update()
        {
            float intensity = GetSpeedIntensity();
            UpdateWind(intensity);
            if (motionBlur != null)
                motionBlur.intensity.value = enableMotionBlur ? maximumMotionBlur * intensity : 0f;

            if (Camera.main != attachedCamera)
                AttachSpeedLines();
            UpdateSpeedLines(intensity);
        }

        private float GetSpeedIntensity()
        {
            float speedKph = cartBody.linearVelocity.magnitude * 3.6f;
            return Mathf.InverseLerp(effectsBeginKph, fullEffectsKph, speedKph);
        }

        private void CreateWindAudio()
        {
            windSource = gameObject.AddComponent<AudioSource>();
            windSource.playOnAwake = false;
            windSource.loop = true;
            windSource.spatialBlend = 0f;
            windSource.volume = 0f;
            windSource.clip = CreateWindClip();

            windFilter = gameObject.AddComponent<AudioLowPassFilter>();
            windFilter.cutoffFrequency = 500f;
            windFilter.lowpassResonanceQ = 1.2f;
            windSource.Play();
        }

        private void CreatePostProcessing()
        {
            GameObject volumeObject = new("Cart Speed Post Processing");
            volumeObject.transform.SetParent(transform, false);
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;
            volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            motionBlur = volume.profile.Add<MotionBlur>(true);
            motionBlur.intensity.value = 0f;
            motionBlur.clamp.value = 0.12f;
        }

        private static AudioClip CreateWindClip()
        {
            const int sampleRate = 22050;
            const int sampleCount = sampleRate * 2;
            float[] samples = new float[sampleCount];
            System.Random random = new(7319);
            float filteredNoise = 0f;

            for (int i = 0; i < samples.Length; i++)
            {
                float whiteNoise = (float)(random.NextDouble() * 2.0 - 1.0);
                filteredNoise = filteredNoise * 0.94f + whiteNoise * 0.06f;
                samples[i] = filteredNoise * 0.8f;
            }

            AudioClip clip = AudioClip.Create("Procedural Cart Wind", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void UpdateWind(float intensity)
        {
            windSource.volume = Mathf.Lerp(0f, maximumWindVolume, intensity * intensity);
            windSource.pitch = Mathf.Lerp(minimumWindPitch, maximumWindPitch, intensity);
            windFilter.cutoffFrequency = Mathf.Lerp(450f, 6500f, intensity);
        }

        private void AttachSpeedLines()
        {
            attachedCamera = Camera.main;
            if (attachedCamera == null)
                return;

            if (speedLines != null)
                Destroy(speedLines.gameObject);

            GameObject effectObject = new("Cart Speed Lines");
            effectObject.transform.SetParent(attachedCamera.transform, false);
            speedLines = effectObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = speedLines.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 220;
            main.startLifetime = 0.45f;
            main.startSize = 0.025f;
            main.startColor = lineColor;
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = speedLines.emission;
            emission.enabled = false;

            ParticleSystemRenderer renderer = speedLines.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 7f;
            renderer.velocityScale = 0.15f;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit");
            if (shader != null)
            {
                Material material = new(shader) { color = lineColor };
                renderer.material = material;
            }

            speedLines.Play();
        }

        private void UpdateSpeedLines(float intensity)
        {
            if (!showSpeedLines || speedLines == null || intensity <= 0.01f)
                return;

            emissionAccumulator += maximumLinesPerSecond * intensity * Time.deltaTime;
            int count = Mathf.FloorToInt(emissionAccumulator);
            emissionAccumulator -= count;

            for (int i = 0; i < count; i++)
            {
                Vector2 screenPosition = Random.insideUnitCircle;
                if (screenPosition.magnitude < 0.42f)
                    screenPosition = screenPosition.normalized * 0.42f;

                ParticleSystem.EmitParams particle = new()
                {
                    position = new Vector3(screenPosition.x * 7.5f, screenPosition.y * 4.2f, Random.Range(10f, 17f)),
                    velocity = new Vector3(0f, 0f, -Mathf.Lerp(18f, 48f, intensity)),
                    startLifetime = Mathf.Lerp(0.55f, 0.32f, intensity),
                    startSize = Mathf.Lerp(0.018f, 0.04f, intensity),
                    startColor = lineColor
                };
                speedLines.Emit(particle, 1);
            }
        }

        private void OnDestroy()
        {
            if (speedLines != null)
                Destroy(speedLines.gameObject);
        }

        private void OnGUI()
        {
            if (!showSpeedometer || cartBody == null)
                return;

            EnsureSpeedometerStyles();
            string speed = $"{Mathf.RoundToInt(cartBody.linearVelocity.magnitude * 3.6f):000}  km/h";
            Rect rect = new(Screen.width - 275f, Screen.height - 95f, 245f, 60f);
            Rect shadowRect = new(rect.x + 3f, rect.y + 3f, rect.width, rect.height);
            GUI.Label(shadowRect, speed, speedShadowStyle);
            GUI.Label(rect, speed, speedStyle);
        }

        private void EnsureSpeedometerStyles()
        {
            if (speedStyle != null)
                return;

            speedStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 32,
                fontStyle = FontStyle.Bold
            };
            speedStyle.normal.textColor = Color.white;
            speedShadowStyle = new GUIStyle(speedStyle);
            speedShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
        }
    }
}
