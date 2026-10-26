using System.IO;
using UnityEditor;
using UnityEngine;

namespace DownhillCarts.Editor
{
    [InitializeOnLoad]
    public static class SoapboxCartPrefabBuilder
    {
        private const string RootFolder = "Assets/SoapboxCart";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string PrefabPath = PrefabFolder + "/SoapboxCart.prefab";
        private const string BuilderVersion = "10";

        static SoapboxCartPrefabBuilder()
        {
            EditorApplication.delayCall += CreateInitialPrefabIfMissing;
        }

        private static void CreateInitialPrefabIfMissing()
        {
            AssetImporter importer = AssetImporter.GetAtPath(PrefabPath);
            if (importer == null || importer.userData != BuilderVersion)
                CreateCart();
        }

        [MenuItem("Tools/Downhill Carts/Create Test Soapbox Cart")]
        public static void CreateCart()
        {
            EnsureFolders();
            Material frameMaterial = GetOrCreateMaterial("Cart Frame", new Color(0.75f, 0.08f, 0.04f));
            Material woodMaterial = GetOrCreateMaterial("Wood Seat", new Color(0.34f, 0.15f, 0.05f));
            Material wheelMaterial = GetOrCreateMaterial("Rubber Wheels", new Color(0.035f, 0.035f, 0.04f));
            Material riderMaterial = GetOrCreateMaterial("Rider Placeholder", new Color(0.08f, 0.25f, 0.75f));

            GameObject root = new("SoapboxCart");
            try
            {
                Rigidbody body = root.AddComponent<Rigidbody>();
                body.mass = 115f;
                body.linearDamping = 0.008f;
                body.angularDamping = 0.65f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.centerOfMass = new Vector3(0f, -0.28f, 0.05f);

                BoxCollider chassisCollider = root.AddComponent<BoxCollider>();
                chassisCollider.center = new Vector3(0f, 0.42f, 0f);
                chassisCollider.size = new Vector3(1.18f, 0.3f, 2.65f);

                Transform visuals = NewChild(root.transform, "Visuals");
                CreateCube(visuals, "Main Frame", new Vector3(0f, 0.42f, 0f), new Vector3(1.1f, 0.22f, 2.55f), frameMaterial);
                CreateCube(visuals, "Front Axle", new Vector3(0f, 0.38f, 0.88f), new Vector3(1.65f, 0.12f, 0.12f), frameMaterial);
                CreateCube(visuals, "Rear Axle", new Vector3(0f, 0.38f, -0.88f), new Vector3(1.65f, 0.12f, 0.12f), frameMaterial);
                CreateCube(visuals, "Seat", new Vector3(0f, 0.72f, -0.32f), new Vector3(0.82f, 0.14f, 0.72f), woodMaterial);
                CreateCube(visuals, "Seat Back", new Vector3(0f, 1.03f, -0.63f), new Vector3(0.82f, 0.62f, 0.12f), woodMaterial);
                CreateCube(visuals, "Steering Column", new Vector3(0f, 0.82f, 0.58f), new Vector3(0.08f, 0.62f, 0.08f), frameMaterial, Quaternion.Euler(18f, 0f, 0f));
                CreateCube(visuals, "Steering Bar", new Vector3(0f, 1.12f, 0.5f), new Vector3(0.72f, 0.07f, 0.07f), frameMaterial);

                Transform rider = NewChild(visuals, "Rider Placeholder");
                rider.localPosition = new Vector3(0f, 0.8f, -0.25f);
                GameObject torso = CreateCapsule(rider, "Torso", new Vector3(0f, 0.48f, 0.05f), new Vector3(0.45f, 0.72f, 0.35f), riderMaterial);
                GameObject head = CreateSphere(rider, "Head", new Vector3(0f, 1.02f, 0.13f), Vector3.one * 0.34f, riderMaterial);
                CartRiderRagdoll ragdoll = rider.gameObject.AddComponent<CartRiderRagdoll>();
                ragdoll.Configure(new[] { torso.GetComponent<Collider>(), head.GetComponent<Collider>() });

                WheelCollider fl = CreateWheelCollider(root.transform, "Front Left Wheel Collider", new Vector3(-0.78f, 0.42f, 0.88f));
                WheelCollider fr = CreateWheelCollider(root.transform, "Front Right Wheel Collider", new Vector3(0.78f, 0.42f, 0.88f));
                WheelCollider rl = CreateWheelCollider(root.transform, "Rear Left Wheel Collider", new Vector3(-0.78f, 0.42f, -0.88f));
                WheelCollider rr = CreateWheelCollider(root.transform, "Rear Right Wheel Collider", new Vector3(0.78f, 0.42f, -0.88f));

                Transform flVisual = CreateWheelVisual(visuals, "Front Left Wheel", fl.transform.localPosition, wheelMaterial);
                Transform frVisual = CreateWheelVisual(visuals, "Front Right Wheel", fr.transform.localPosition, wheelMaterial);
                Transform rlVisual = CreateWheelVisual(visuals, "Rear Left Wheel", rl.transform.localPosition, wheelMaterial);
                Transform rrVisual = CreateWheelVisual(visuals, "Rear Right Wheel", rr.transform.localPosition, wheelMaterial);

                Transform cameraTargets = NewChild(root.transform, "Camera Targets");
                Transform firstPersonMount = NewChild(cameraTargets, "First Person View");
                firstPersonMount.localPosition = new Vector3(0f, 1.83f, -0.04f);
                Transform thirdPersonTarget = NewChild(cameraTargets, "Third Person Look Target");
                thirdPersonTarget.localPosition = new Vector3(0f, 0.85f, 0.5f);

                SoapboxCartController controller = root.AddComponent<SoapboxCartController>();
                SerializedObject serialized = new(controller);
                SetObject(serialized, "frontLeft", fl);
                SetObject(serialized, "frontRight", fr);
                SetObject(serialized, "rearLeft", rl);
                SetObject(serialized, "rearRight", rr);
                SetObject(serialized, "frontLeftVisual", flVisual);
                SetObject(serialized, "frontRightVisual", frVisual);
                SetObject(serialized, "rearLeftVisual", rlVisual);
                SetObject(serialized, "rearRightVisual", rrVisual);
                SetObject(serialized, "riderVisual", rider);
                SetObject(serialized, "riderRagdoll", ragdoll);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                CartCameraController cameraController = root.AddComponent<CartCameraController>();
                SerializedObject cameraSerialized = new(cameraController);
                SetObject(cameraSerialized, "firstPersonMount", firstPersonMount);
                SetObject(cameraSerialized, "thirdPersonLookTarget", thirdPersonTarget);
                cameraSerialized.ApplyModifiedPropertiesWithoutUndo();
                root.AddComponent<CartSpeedEffects>();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetImporter importer = AssetImporter.GetAtPath(PrefabPath);
                if (importer != null)
                {
                    importer.userData = BuilderVersion;
                    importer.SaveAndReimport();
                }
                Selection.activeObject = prefab;
                Debug.Log($"Created test cart at {PrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        public static void BuildFromCommandLine()
        {
            CreateCart();
            AssetDatabase.SaveAssets();
        }

        private static WheelCollider CreateWheelCollider(Transform parent, string name, Vector3 position)
        {
            GameObject wheelObject = new(name);
            wheelObject.transform.SetParent(parent, false);
            wheelObject.transform.localPosition = position;
            WheelCollider wheel = wheelObject.AddComponent<WheelCollider>();
            wheel.radius = 0.34f;
            wheel.mass = 12f;
            wheel.suspensionDistance = 0.24f;
            JointSpring spring = wheel.suspensionSpring;
            spring.spring = 9000f;
            spring.damper = 1400f;
            spring.targetPosition = 0.52f;
            wheel.suspensionSpring = spring;
            wheel.forceAppPointDistance = 0.08f;
            WheelFrictionCurve forward = wheel.forwardFriction;
            forward.stiffness = 1.25f;
            wheel.forwardFriction = forward;
            WheelFrictionCurve sideways = wheel.sidewaysFriction;
            sideways.stiffness = 1.35f;
            wheel.sidewaysFriction = sideways;
            return wheel;
        }

        private static Transform CreateWheelVisual(Transform parent, string name, Vector3 position, Material material)
        {
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = name;
            wheel.transform.SetParent(parent, false);
            wheel.transform.localPosition = position;
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.transform.localScale = new Vector3(0.68f, 0.16f, 0.68f);
            Object.DestroyImmediate(wheel.GetComponent<Collider>());
            wheel.GetComponent<Renderer>().sharedMaterial = material;
            return wheel.transform;
        }

        private static void CreateCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material, Quaternion rotation = default)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SetupPrimitive(item, parent, name, position, scale, material);
            item.transform.localRotation = rotation == default ? Quaternion.identity : rotation;
        }

        private static GameObject CreateCapsule(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            SetupPrimitive(item, parent, name, position, scale, material, false);
            return item;
        }

        private static GameObject CreateSphere(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            SetupPrimitive(item, parent, name, position, scale, material, false);
            return item;
        }

        private static void SetupPrimitive(GameObject item, Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool removeCollider = true)
        {
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            if (removeCollider)
                Object.DestroyImmediate(item.GetComponent<Collider>());
            item.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Transform NewChild(Transform parent, string name)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static void SetObject(SerializedObject serialized, string name, Object value)
        {
            serialized.FindProperty(name).objectReferenceValue = value;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();
        }
    }
}
