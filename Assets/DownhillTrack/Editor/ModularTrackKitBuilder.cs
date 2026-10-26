using System.IO;
using UnityEditor;
using UnityEngine;

namespace DownhillCarts.Editor
{
    [InitializeOnLoad]
    public static class ModularTrackKitBuilder
    {
        public const string PrefabFolder = "Assets/DownhillTrack/Prefabs";
        private const string MaterialFolder = "Assets/DownhillTrack/Materials";
        private const string KitVersion = "6";

        static ModularTrackKitBuilder()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        private static void BuildIfMissing()
        {
            AssetImporter importer = AssetImporter.GetAtPath(PrefabFolder + "/Finish Gate.prefab");
            if (importer == null || importer.userData != KitVersion)
                BuildKit();
        }

        [MenuItem("Tools/Downhill Carts/Create Modular Track Kit")]
        public static void BuildKit()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();

            Material asphalt = GetOrCreateMaterial("Asphalt", new Color(0.075f, 0.08f, 0.09f));
            Material red = GetOrCreateMaterial("Barrier Red", new Color(0.72f, 0.035f, 0.025f));
            Material white = GetOrCreateMaterial("Barrier White", new Color(0.9f, 0.9f, 0.84f));
            Material yellow = GetOrCreateMaterial("Road Edge", new Color(0.92f, 0.82f, 0.15f));
            Material orange = GetOrCreateMaterial("Safety Orange", new Color(1f, 0.25f, 0.015f));

            CreateGatePrefab("Start Gate", TimeTrialTrigger.TriggerType.Start, red, yellow, white);
            CreateGatePrefab("Finish Gate", TimeTrialTrigger.TriggerType.Finish, white, red, yellow);
            CreateCheckpointPrefab(red, white);
            CreateGuardrailPrefab("Guardrail Red 6m", red);
            CreateGuardrailPrefab("Guardrail White 6m", white);
            CreateRoadPrefab("Road Straight 10m", 10f, 0f, asphalt);
            CreateRoadPrefab("Road Straight 20m", 20f, 0f, asphalt);
            CreateRoadPrefab("Road Bank Left 20m", 20f, 9f, asphalt);
            CreateRoadPrefab("Road Bank Right 20m", 20f, -9f, asphalt);
            CreateSlalomPrefab(orange);
            CreateRaceManagerPrefab();
            CreateAIRacerPrefab();
            AssetDatabase.SaveAssets();
            string versionedPrefab = PrefabFolder + "/Finish Gate.prefab";
            AssetImporter importer = AssetImporter.GetAtPath(versionedPrefab);
            if (importer != null)
            {
                importer.userData = KitVersion;
                AssetDatabase.WriteImportSettingsIfDirty(versionedPrefab);
            }
        }

        private static void CreateGatePrefab(string name, TimeTrialTrigger.TriggerType type, Material postMaterial, Material bannerMaterial, Material stripeMaterial)
        {
            GameObject root = new(name);
            CreateCube(root.transform, "Left Post", new Vector3(-4.75f, 2f, 0f), new Vector3(0.38f, 4f, 0.38f), postMaterial, true);
            CreateCube(root.transform, "Right Post", new Vector3(4.75f, 2f, 0f), new Vector3(0.38f, 4f, 0.38f), postMaterial, true);
            CreateCube(root.transform, "Top Banner", new Vector3(0f, 4f, 0f), new Vector3(9.9f, 0.72f, 0.45f), bannerMaterial, false);
            if (type == TimeTrialTrigger.TriggerType.Finish)
            {
                for (int row = 0; row < 2; row++)
                {
                    for (int column = 0; column < 8; column++)
                    {
                        Material tileMaterial = (row + column) % 2 == 0 ? postMaterial : bannerMaterial;
                        CreateCube(root.transform, "Finish Tile", new Vector3(-3.5f + column, 0.025f, -0.2f + row * 0.4f), new Vector3(1f, 0.035f, 0.4f), tileMaterial, false);
                    }
                }
            }
            else
            {
                CreateCube(root.transform, "Timing Line", new Vector3(0f, 0.025f, 0f), new Vector3(8f, 0.035f, 0.8f), stripeMaterial, false);
            }

            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.5f, 0f);
            trigger.size = new Vector3(8f, 3f, 1.25f);
            root.AddComponent<TimeTrialTrigger>().Configure(type);
            SavePrefab(root, name);
        }

        private static void CreateCheckpointPrefab(Material red, Material white)
        {
            GameObject root = new("Checkpoint Gate");
            CreateCube(root.transform, "Left Post", new Vector3(-4.75f, 2f, 0f), new Vector3(0.32f, 4f, 0.32f), red, false);
            CreateCube(root.transform, "Right Post", new Vector3(4.75f, 2f, 0f), new Vector3(0.32f, 4f, 0.32f), red, false);
            for (int i = 0; i < 10; i++)
            {
                Material material = i % 2 == 0 ? red : white;
                CreateCube(root.transform, "Banner Tile", new Vector3(-4.5f + i, 4f, 0f), new Vector3(1f, 0.62f, 0.38f), material, false);
            }
            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.5f, 0f);
            trigger.size = new Vector3(8f, 3f, 1.25f);
            root.AddComponent<TrackCheckpoint>();
            SavePrefab(root, "Checkpoint Gate");
        }

        private static void CreateGuardrailPrefab(string name, Material material)
        {
            GameObject root = new(name);
            root.AddComponent<TrackBarrier>();
            CreateCube(root.transform, "Rail", new Vector3(0f, 0.48f, 0f), new Vector3(0.2f, 0.65f, 6f), material, true);
            SavePrefab(root, name);
        }

        private static void CreateRoadPrefab(string name, float length, float bankAngle, Material material)
        {
            GameObject root = new(name);
            GameObject road = CreateCube(root.transform, "Road Surface", Vector3.zero, new Vector3(8f, 0.22f, length), material, true);
            road.transform.localRotation = Quaternion.Euler(0f, 0f, bankAngle);
            SavePrefab(root, name);
        }

        private static void CreateSlalomPrefab(Material material)
        {
            GameObject root = new("Slalom Marker");
            GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cone.name = "Marker";
            cone.transform.SetParent(root.transform, false);
            cone.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            cone.transform.localScale = new Vector3(0.22f, 0.3f, 0.22f);
            cone.GetComponent<Renderer>().sharedMaterial = material;
            SavePrefab(root, "Slalom Marker");
        }

        private static void CreateRaceManagerPrefab()
        {
            GameObject root = new("Time Trial Manager");
            root.AddComponent<TimeTrialManager>();
            SavePrefab(root, "Time Trial Manager");
        }

        private static void CreateAIRacerPrefab()
        {
            GameObject cartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SoapboxCart/Prefabs/SoapboxCart.prefab");
            if (cartPrefab == null)
                return;

            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(cartPrefab);
            root.name = "AI Soapbox Racer";
            CartCameraController cameraController = root.GetComponent<CartCameraController>();
            if (cameraController != null)
                Object.DestroyImmediate(cameraController);
            CartSpeedEffects speedEffects = root.GetComponent<CartSpeedEffects>();
            if (speedEffects != null)
                Object.DestroyImmediate(speedEffects);

            SoapboxCartController controller = root.GetComponent<SoapboxCartController>();
            SerializedObject serialized = new(controller);
            serialized.FindProperty("playerControlled").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<AIRacerDriver>();

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/AI Soapbox Racer.prefab");
            Object.DestroyImmediate(root);
        }

        private static GameObject CreateCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider)
                Object.DestroyImmediate(cube.GetComponent<Collider>());
            return cube;
        }

        private static void SavePrefab(GameObject root, string name)
        {
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
