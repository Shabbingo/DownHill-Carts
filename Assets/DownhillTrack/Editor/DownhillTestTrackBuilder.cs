using System.Collections.Generic;
using System.IO;
using DownhillCarts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DownhillCarts.Editor
{
    [InitializeOnLoad]
    public static class DownhillTestTrackBuilder
    {
        private const string RootFolder = "Assets/DownhillTrack";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string DataFolder = RootFolder + "/Data";
        private const string ScenePath = "Assets/Scenes/DownhillTestTrack.unity";
        private const string TrackVersion = "9";
        private const float RoadWidth = 8f;

        private struct TrackPoint
        {
            public Vector3 position;
            public Vector3 tangent;
            public Vector3 side;
            public Vector3 up;
        }

        static DownhillTestTrackBuilder()
        {
            EditorApplication.delayCall += CreateInitialTrackIfMissing;
        }

        private static void CreateInitialTrackIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                BuildTrack();
        }

        [MenuItem("Tools/Downhill Carts/Create Downhill Test Track")]
        public static void BuildTrack()
        {
            EnsureFolders();
            ModularTrackKitBuilder.BuildKit();

            Scene previousScene = SceneManager.GetActiveScene();
            Scene loadedTrack = SceneManager.GetSceneByPath(ScenePath);
            bool rebuildingLoadedScene = loadedTrack.IsValid() && loadedTrack.isLoaded;
            Scene trackScene = rebuildingLoadedScene
                ? loadedTrack
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            if (rebuildingLoadedScene)
            {
                foreach (GameObject root in trackScene.GetRootGameObjects())
                    Object.DestroyImmediate(root);
            }
            else
            {
                trackScene.name = "DownhillTestTrack";
            }
            SceneManager.SetActiveScene(trackScene);

            try
            {
                Material roadMaterial = GetOrCreateMaterial("Asphalt", new Color(0.075f, 0.08f, 0.09f), 0.12f);
                Material edgeMaterial = GetOrCreateMaterial("Road Edge", new Color(0.92f, 0.82f, 0.15f), 0.18f);
                Material redMaterial = GetOrCreateMaterial("Barrier Red", new Color(0.72f, 0.035f, 0.025f), 0.28f);
                Material whiteMaterial = GetOrCreateMaterial("Barrier White", new Color(0.9f, 0.9f, 0.84f), 0.22f);
                Material timberMaterial = GetOrCreateMaterial("Timber", new Color(0.22f, 0.095f, 0.035f), 0.05f);
                Material foliageMaterial = GetOrCreateMaterial("Pine", new Color(0.025f, 0.17f, 0.075f), 0.02f);
                Material rockMaterial = GetOrCreateMaterial("Rock", new Color(0.25f, 0.27f, 0.28f), 0.08f);
                Material orangeMaterial = GetOrCreateMaterial("Safety Orange", new Color(1f, 0.25f, 0.015f), 0.18f);

                GameObject environment = new("Mountain Environment");
                List<TrackPoint> track = CreateTrackPoints();
                Terrain terrain = CreateTerrain(environment.transform, track);

                GameObject trackRoot = new("Race Track");
                BuildRoad(trackRoot.transform, track, roadMaterial);
                BuildRoadEdges(trackRoot.transform, track, edgeMaterial);
                BuildGuardrails(trackRoot.transform, track, redMaterial, whiteMaterial);
                BuildStartAndFinish(trackRoot.transform, track, redMaterial, whiteMaterial, edgeMaterial);
                BuildCheckpointGates(trackRoot.transform, track, redMaterial, whiteMaterial);
                BuildSlalom(trackRoot.transform, track, orangeMaterial);

                PopulateScenery(environment.transform, terrain, track, timberMaterial, foliageMaterial, rockMaterial);
                CreateLightingAndCamera(track);
                CreateRaceSystem(track);
                CreateAIRacers(track);

                EditorSceneManager.SaveScene(trackScene, ScenePath);
                AssetDatabase.SaveAssets();
                AssetImporter sceneImporter = AssetImporter.GetAtPath(ScenePath);
                if (sceneImporter != null)
                {
                    sceneImporter.userData = TrackVersion;
                    AssetDatabase.WriteImportSettingsIfDirty(ScenePath);
                }
                Debug.Log($"Created downhill test track at {ScenePath}");
            }
            finally
            {
                if (!rebuildingLoadedScene && previousScene.IsValid())
                    SceneManager.SetActiveScene(previousScene);
                if (!rebuildingLoadedScene)
                    EditorSceneManager.CloseScene(trackScene, true);
            }
        }

        public static void BuildFromCommandLine()
        {
            BuildTrack();
            AssetDatabase.SaveAssets();
        }

        private static List<TrackPoint> CreateTrackPoints()
        {
            Vector3[] controls =
            {
                new(0f, 58f, 8f),
                new(0f, 54f, 35f),
                new(18f, 49f, 70f),
                new(52f, 44f, 104f),
                new(80f, 39f, 140f),
                new(70f, 34f, 176f),
                new(35f, 29f, 205f),
                new(-18f, 24f, 230f),
                new(-66f, 19f, 260f),
                new(-80f, 15f, 295f),
                new(-52f, 11f, 330f),
                new(-5f, 7f, 360f),
                new(27f, 4f, 392f),
                new(20f, 2f, 425f),
                // Hairpin one: climb around the outside, rotate through 180 degrees, then exit below it.
                new(42f, -1f, 455f),
                new(72f, -4f, 476f),
                new(98f, -7f, 470f),
                new(114f, -10f, 446f),
                new(106f, -13f, 417f),
                new(80f, -16f, 397f),
                new(50f, -19f, 402f),
                new(22f, -22f, 430f),
                new(-8f, -25f, 470f),
                new(-46f, -28f, 514f),
                new(-82f, -31f, 555f),
                // Hairpin two turns back toward the final high-speed descent.
                new(-110f, -34f, 584f),
                new(-135f, -37f, 580f),
                new(-148f, -40f, 556f),
                new(-138f, -43f, 528f),
                new(-112f, -46f, 508f),
                new(-80f, -49f, 514f),
                new(-45f, -52f, 545f),
                new(0f, -55f, 585f),
                new(50f, -58f, 632f),
                new(84f, -61f, 681f),
                new(66f, -64f, 730f),
                new(25f, -67f, 775f)
            };

            List<Vector3> samples = new();
            const int samplesPerSegment = 12;
            for (int segment = 0; segment < controls.Length - 1; segment++)
            {
                Vector3 p0 = controls[Mathf.Max(0, segment - 1)];
                Vector3 p1 = controls[segment];
                Vector3 p2 = controls[segment + 1];
                Vector3 p3 = controls[Mathf.Min(controls.Length - 1, segment + 2)];
                for (int step = 0; step < samplesPerSegment; step++)
                    samples.Add(CatmullRom(p0, p1, p2, p3, step / (float)samplesPerSegment));
            }
            samples.Add(controls[^1]);

            List<TrackPoint> result = new(samples.Count);
            Vector3 previousTangent = Vector3.forward;
            for (int i = 0; i < samples.Count; i++)
            {
                Vector3 before = samples[Mathf.Max(0, i - 1)];
                Vector3 after = samples[Mathf.Min(samples.Count - 1, i + 1)];
                Vector3 tangent = (after - before).normalized;
                float signedTurn = i == 0 ? 0f : Vector3.SignedAngle(previousTangent, tangent, Vector3.up);
                float bank = Mathf.Clamp(-signedTurn * 3.25f, -12f, 12f);
                Vector3 flatSide = Vector3.Cross(Vector3.up, tangent).normalized;
                Vector3 side = Quaternion.AngleAxis(bank, tangent) * flatSide;
                Vector3 up = Vector3.Cross(tangent, side).normalized;
                result.Add(new TrackPoint { position = samples[i], tangent = tangent, side = side, up = up });
                previousTangent = tangent;
            }
            return result;
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1)
                + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static void BuildRoad(Transform parent, List<TrackPoint> track, Material material)
        {
            GameObject road = new("Continuous Asphalt Road");
            road.transform.SetParent(parent, false);
            Mesh mesh = new() { name = "Downhill Road Mesh" };
            Vector3[] vertices = new Vector3[track.Count * 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[(track.Count - 1) * 6];
            float distance = 0f;

            for (int i = 0; i < track.Count; i++)
            {
                if (i > 0)
                    distance += Vector3.Distance(track[i - 1].position, track[i].position);
                vertices[i * 2] = track[i].position - track[i].side * RoadWidth * 0.5f;
                vertices[i * 2 + 1] = track[i].position + track[i].side * RoadWidth * 0.5f;
                uv[i * 2] = new Vector2(0f, distance / 5f);
                uv[i * 2 + 1] = new Vector2(1f, distance / 5f);

                if (i >= track.Count - 1)
                    continue;
                int triangle = i * 6;
                int vertex = i * 2;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string meshPath = DataFolder + "/Downhill Road.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            MeshFilter filter = road.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            road.AddComponent<MeshRenderer>().sharedMaterial = material;
            road.AddComponent<MeshCollider>().sharedMesh = mesh;
            road.AddComponent<RaceTrackSurface>();
            road.isStatic = true;
        }

        private static void BuildRoadEdges(Transform parent, List<TrackPoint> track, Material material)
        {
            Transform edges = NewChild(parent, "Painted Road Edges");
            for (int i = 0; i < track.Count - 1; i += 2)
            {
                int next = Mathf.Min(i + 2, track.Count - 1);
                for (int sideIndex = -1; sideIndex <= 1; sideIndex += 2)
                {
                    Vector3 start = track[i].position + track[i].side * (RoadWidth * 0.5f - 0.18f) * sideIndex + track[i].up * 0.025f;
                    Vector3 end = track[next].position + track[next].side * (RoadWidth * 0.5f - 0.18f) * sideIndex + track[next].up * 0.025f;
                    CreateBeam(edges, "Yellow Edge", start, end, 0.13f, 0.025f, material, false);
                }
            }
        }

        private static void BuildGuardrails(Transform parent, List<TrackPoint> track, Material red, Material white)
        {
            Transform rails = NewChild(parent, "Guardrails");
            for (int i = 2; i < track.Count - 3; i += 3)
            {
                int next = Mathf.Min(i + 3, track.Count - 1);
                string prefabName = (i / 3) % 2 == 0 ? "Guardrail Red 6m" : "Guardrail White 6m";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModularTrackKitBuilder.PrefabFolder}/{prefabName}.prefab");
                for (int sideIndex = -1; sideIndex <= 1; sideIndex += 2)
                {
                    Vector3 start = track[i].position + track[i].side * (RoadWidth * 0.5f + 0.22f) * sideIndex;
                    Vector3 end = track[next].position + track[next].side * (RoadWidth * 0.5f + 0.22f) * sideIndex;
                    Vector3 direction = end - start;
                    GameObject rail = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rails);
                    rail.name = prefabName;
                    rail.transform.position = (start + end) * 0.5f;
                    rail.transform.rotation = Quaternion.LookRotation(direction.normalized, (track[i].up + track[next].up).normalized);
                    rail.transform.localScale = new Vector3(1f, 1f, direction.magnitude / 6f);
                }
            }
        }

        private static void BuildStartAndFinish(Transform parent, List<TrackPoint> track, Material red, Material white, Material yellow)
        {
            InstantiateTrackPrefab("Start Gate", parent, track[5]);
            InstantiateTrackPrefab("Finish Gate", parent, track[^5]);
        }

        private static void BuildCheckpointGates(Transform parent, List<TrackPoint> track, Material red, Material white)
        {
            int[] indices = { track.Count / 4, track.Count / 2, track.Count * 3 / 4 };
            for (int i = 0; i < indices.Length; i++)
            {
                GameObject checkpoint = InstantiateTrackPrefab("Checkpoint Gate", parent, track[indices[i]]);
                checkpoint.name = $"Checkpoint {i + 1}";
                checkpoint.GetComponent<TrackCheckpoint>().Configure(i + 1);
            }
        }

        private static void BuildSlalom(Transform parent, List<TrackPoint> track, Material orange)
        {
            Transform slalom = NewChild(parent, "Slalom Cones");
            int start = track.Count / 3;
            for (int i = 0; i < 8; i++)
            {
                TrackPoint point = track[start + i * 2];
                float side = i % 2 == 0 ? -1.7f : 1.7f;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModularTrackKitBuilder.PrefabFolder}/Slalom Marker.prefab");
                GameObject cone = (GameObject)PrefabUtility.InstantiatePrefab(prefab, slalom);
                cone.transform.position = point.position + point.side * side + point.up * 0.28f;
                cone.transform.rotation = Quaternion.LookRotation(point.tangent, point.up);
            }
        }

        private static GameObject InstantiateTrackPrefab(string prefabName, Transform parent, TrackPoint point)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModularTrackKitBuilder.PrefabFolder}/{prefabName}.prefab");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = prefabName;
            instance.transform.SetPositionAndRotation(point.position, Quaternion.LookRotation(point.tangent, point.up));
            return instance;
        }

        private static Terrain CreateTerrain(Transform parent, List<TrackPoint> track)
        {
            string dataPath = DataFolder + "/Mountain Terrain.asset";
            AssetDatabase.DeleteAsset(dataPath);
            TerrainData data = new()
            {
                heightmapResolution = 257,
                size = new Vector3(360f, 170f, 830f)
            };

            int resolution = data.heightmapResolution;
            float[,] heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            {
                float worldZ = -20f + z / (resolution - 1f) * data.size.z;
                for (int x = 0; x < resolution; x++)
                {
                    float worldX = -180f + x / (resolution - 1f) * data.size.x;
                    float noise = Mathf.Sin(worldX * 0.045f) * 0.7f
                        + Mathf.Sin(worldZ * 0.031f + worldX * 0.018f) * 0.75f
                        + Mathf.Sin(worldX * 0.11f + worldZ * 0.07f) * 0.2f;
                    float middleValley = 4.2f * Mathf.Exp(-Mathf.Pow((worldZ - 250f) / 100f, 2f));
                    float extendedDescent = Mathf.Max(0f, worldZ - 400f) * 0.09f;
                    float worldHeight = 50f - (worldZ - 8f) * 0.12f - middleValley - extendedDescent + noise;
                    float uncarvedWorldHeight = worldHeight;

                    // Match the terrain to the banked road cross-section, then blend the shoulder.
                    const float fullCarveRadius = 1.25f;
                    const float shoulderBlendRadius = 13f;
                    const float roadClearance = 0.1f;
                    float closestRoadDistance = float.MaxValue;
                    float closestRoadHeight = worldHeight;
                    for (int pointIndex = 0; pointIndex < track.Count; pointIndex++)
                    {
                        TrackPoint point = track[pointIndex];
                        Vector3 horizontalSide3D = Vector3.ProjectOnPlane(point.side, Vector3.up);
                        float horizontalMagnitude = horizontalSide3D.magnitude;
                        if (horizontalMagnitude < 0.001f)
                            continue;
                        Vector3 horizontalSide = horizontalSide3D / horizontalMagnitude;
                        Vector3 sampleOffset = new Vector3(worldX - point.position.x, 0f, worldZ - point.position.z);
                        float horizontalLateral = Vector3.Dot(sampleOffset, horizontalSide);
                        float clampedHorizontalLateral = Mathf.Clamp(
                            horizontalLateral,
                            -RoadWidth * 0.5f * horizontalMagnitude,
                            RoadWidth * 0.5f * horizontalMagnitude);
                        Vector3 closestRoadXZ = point.position + horizontalSide * clampedHorizontalLateral;
                        float distance = Vector2.Distance(
                            new Vector2(worldX, worldZ),
                            new Vector2(closestRoadXZ.x, closestRoadXZ.z));
                        if (distance >= closestRoadDistance)
                            continue;
                        float sideParameter = clampedHorizontalLateral / horizontalMagnitude;
                        closestRoadDistance = distance;
                        closestRoadHeight = point.position.y + point.side.y * sideParameter;
                    }
                    if (closestRoadDistance < shoulderBlendRadius)
                    {
                        float blend = 1f - Mathf.SmoothStep(fullCarveRadius, shoulderBlendRadius, closestRoadDistance);
                        worldHeight = Mathf.Lerp(uncarvedWorldHeight, closestRoadHeight - roadClearance, blend);
                    }
                    heights[z, x] = Mathf.Clamp01((worldHeight + 90f) / data.size.y);
                }
            }
            data.SetHeights(0, 0, heights);

            TerrainLayer grassLayer = GetOrCreateTerrainLayer();
            data.terrainLayers = new[] { grassLayer };
            AssetDatabase.CreateAsset(data, dataPath);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Mountain Terrain";
            terrainObject.transform.SetParent(parent, false);
            terrainObject.transform.position = new Vector3(-180f, -90f, -20f);
            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 8f;
            return terrain;
        }

        private static TerrainLayer GetOrCreateTerrainLayer()
        {
            string texturePath = DataFolder + "/Mountain Grass.asset";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                texture = new Texture2D(2, 2) { name = "Mountain Grass" };
                Color green = new(0.11f, 0.21f, 0.09f);
                texture.SetPixels(new[] { green, green * 0.92f, green * 1.05f, green });
                texture.Apply();
                AssetDatabase.CreateAsset(texture, texturePath);
            }

            string layerPath = DataFolder + "/Mountain Grass Layer.terrainlayer";
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer
                {
                    diffuseTexture = texture,
                    tileSize = new Vector2(12f, 12f),
                    smoothness = 0f
                };
                AssetDatabase.CreateAsset(layer, layerPath);
            }
            return layer;
        }

        private static void PopulateScenery(Transform parent, Terrain terrain, List<TrackPoint> track, Material timber, Material foliage, Material rock)
        {
            Transform trees = NewChild(parent, "Mountain Pines");
            Transform rocks = NewChild(parent, "Roadside Rocks");
            System.Random random = new(4401);

            int createdTrees = 0;
            for (int attempt = 0; attempt < 360 && createdTrees < 110; attempt++)
            {
                Vector3 position = RandomTerrainPosition(random, terrain);
                if (DistanceToTrack(position, track) < 13f)
                    continue;
                float scale = Mathf.Lerp(0.75f, 1.45f, (float)random.NextDouble());
                CreatePine(trees, position, scale, timber, foliage);
                createdTrees++;
            }

            int createdRocks = 0;
            for (int attempt = 0; attempt < 190 && createdRocks < 55; attempt++)
            {
                Vector3 position = RandomTerrainPosition(random, terrain);
                float distance = DistanceToTrack(position, track);
                if (distance < 6f || distance > 28f)
                    continue;
                GameObject boulder = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                boulder.name = "Boulder";
                boulder.transform.SetParent(rocks, false);
                boulder.transform.position = position + Vector3.up * 0.6f;
                boulder.transform.rotation = Random.rotation;
                float size = Mathf.Lerp(0.7f, 2.2f, (float)random.NextDouble());
                boulder.transform.localScale = new Vector3(size, size * 0.65f, size * 1.15f);
                boulder.GetComponent<Renderer>().sharedMaterial = rock;
                boulder.isStatic = true;
                createdRocks++;
            }
        }

        private static Vector3 RandomTerrainPosition(System.Random random, Terrain terrain)
        {
            float x = Mathf.Lerp(-172f, 172f, (float)random.NextDouble());
            float z = Mathf.Lerp(-10f, 800f, (float)random.NextDouble());
            Vector3 position = new(x, 0f, z);
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            return position;
        }

        private static float DistanceToTrack(Vector3 position, List<TrackPoint> track)
        {
            float closest = float.MaxValue;
            for (int i = 0; i < track.Count; i += 4)
                closest = Mathf.Min(closest, Vector2.Distance(new Vector2(position.x, position.z), new Vector2(track[i].position.x, track[i].position.z)));
            return closest;
        }

        private static void CreatePine(Transform parent, Vector3 position, float scale, Material timber, Material foliage)
        {
            GameObject root = new("Pine");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * scale;

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(root.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            trunk.transform.localScale = new Vector3(0.18f, 1.5f, 0.18f);
            trunk.GetComponent<Renderer>().sharedMaterial = timber;

            for (int i = 0; i < 3; i++)
            {
                GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                crown.name = "Pine Crown";
                crown.transform.SetParent(root.transform, false);
                crown.transform.localPosition = new Vector3(0f, 2.4f + i * 0.85f, 0f);
                float width = 1.25f - i * 0.23f;
                crown.transform.localScale = new Vector3(width, 0.75f, width);
                crown.GetComponent<Renderer>().sharedMaterial = foliage;
                Object.DestroyImmediate(crown.GetComponent<Collider>());
            }
            root.isStatic = true;
        }

        private static void CreateLightingAndCamera(List<TrackPoint> track)
        {
            GameObject sunObject = new("Mountain Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.89f, 0.72f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sunObject.transform.rotation = Quaternion.Euler(42f, -32f, 0f);

            TrackPoint start = track[2];
            GameObject cameraObject = new("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 65f;
            camera.farClipPlane = 1200f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = start.position - start.tangent * 7f + Vector3.up * 3f;
            cameraObject.transform.rotation = Quaternion.LookRotation(start.position - cameraObject.transform.position, Vector3.up);

            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.48f, 0.58f, 0.65f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 650f;
            RenderSettings.ambientLight = new Color(0.35f, 0.4f, 0.43f);
        }

        private static void CreateRaceSystem(List<TrackPoint> track)
        {
            GameObject cartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SoapboxCart/Prefabs/SoapboxCart.prefab");
            if (cartPrefab == null)
            {
                Debug.LogWarning("Soapbox cart prefab was not found; create it from the Tools/Downhill Carts menu before testing the track.");
                return;
            }

            TrackPoint start = track[1];
            GameObject spawnObject = new("Cart Spawn Point");
            spawnObject.transform.position = start.position + start.up * 0.7f;
            spawnObject.transform.rotation = Quaternion.LookRotation(start.tangent, start.up);

            GameObject cart = (GameObject)PrefabUtility.InstantiatePrefab(cartPrefab);
            cart.name = "Player Soapbox Cart";
            cart.transform.SetPositionAndRotation(spawnObject.transform.position, spawnObject.transform.rotation);

            GameObject managerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModularTrackKitBuilder.PrefabFolder}/Time Trial Manager.prefab");
            GameObject managerObject = (GameObject)PrefabUtility.InstantiatePrefab(managerPrefab);
            managerObject.name = "Time Trial Manager";
            managerObject.GetComponent<TimeTrialManager>().Configure(cartPrefab, spawnObject.transform, 3);
        }

        private static void CreateAIRacers(List<TrackPoint> track)
        {
            GameObject lineObject = new("AI Racing Line");
            AIRacingLine racingLine = lineObject.AddComponent<AIRacingLine>();
            Vector3[] points = new Vector3[track.Count];
            for (int i = 0; i < track.Count; i++)
                points[i] = track[i].position + track[i].up * 0.35f;
            racingLine.Configure(points);

            GameObject aiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModularTrackKitBuilder.PrefabFolder}/AI Soapbox Racer.prefab");
            if (aiPrefab == null)
                return;

            Transform racers = NewChild(null, "AI Racers");
            AIRacerGroup racerGroup = racers.gameObject.AddComponent<AIRacerGroup>();
            TrackPoint start = track[1];
            Vector2[] gridOffsets =
            {
                new(-2.25f, 4f),
                new(2.25f, 4f),
                new(-2.25f, 8f),
                new(2.25f, 8f),
                new(0f, 12f)
            };
            float[] skills = { 0.74f, 0.82f, 0.9f, 0.98f, 1.05f };
            float[] laneOffsets = { -0.65f, 0.65f, -0.3f, 0.3f, 0f };
            Color[] colours =
            {
                new(0.1f, 0.55f, 1f),
                new(1f, 0.52f, 0.03f),
                new(0.15f, 0.8f, 0.3f),
                new(0.72f, 0.18f, 0.9f),
                new(1f, 0.12f, 0.28f)
            };

            for (int i = 0; i < gridOffsets.Length; i++)
            {
                GameObject racer = (GameObject)PrefabUtility.InstantiatePrefab(aiPrefab, racers);
                racer.name = $"AI Racer {i + 1}";
                Vector3 position = start.position
                    + start.side * gridOffsets[i].x
                    - start.tangent * gridOffsets[i].y
                    + start.up * 0.7f;
                racer.transform.SetPositionAndRotation(position, Quaternion.LookRotation(start.tangent, start.up));
                racer.GetComponent<AIRacerDriver>().Configure(racingLine, skills[i], colours[i], laneOffsets[i]);
            }
            racerGroup.Configure(true, gridOffsets.Length);
        }

        private static void CreateGate(Transform parent, string name, TrackPoint point, Material postMaterial, Material topMaterial)
        {
            Transform gate = NewChild(parent, name);
            Vector3 left = point.position - point.side * (RoadWidth * 0.5f + 0.75f);
            Vector3 right = point.position + point.side * (RoadWidth * 0.5f + 0.75f);
            CreateBeam(gate, "Left Post", left, left + point.up * 4.1f, 0.3f, 0.3f, postMaterial, false);
            CreateBeam(gate, "Right Post", right, right + point.up * 4.1f, 0.3f, 0.3f, postMaterial, false);
            CreateBeam(gate, "Top Banner", left + point.up * 3.8f, right + point.up * 3.8f, 0.38f, 0.75f, topMaterial, false);
        }

        private static void CreateRoadStripe(Transform parent, string name, TrackPoint point, Material material)
        {
            GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = name;
            stripe.transform.SetParent(parent, false);
            stripe.transform.position = point.position + point.up * 0.035f;
            stripe.transform.rotation = Quaternion.LookRotation(point.tangent, point.up);
            stripe.transform.localScale = new Vector3(RoadWidth, 0.025f, 0.65f);
            stripe.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(stripe.GetComponent<Collider>());
        }

        private static void CreateBeam(Transform parent, string name, Vector3 start, Vector3 end, float width, float height, Material material, bool collider)
        {
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = name;
            beam.transform.SetParent(parent, false);
            Vector3 direction = end - start;
            beam.transform.position = (start + end) * 0.5f;
            Vector3 safeUp = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.98f
                ? Vector3.forward
                : Vector3.up;
            beam.transform.rotation = Quaternion.LookRotation(direction.normalized, safeUp);
            beam.transform.localScale = new Vector3(width, height, direction.magnitude);
            beam.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider)
                Object.DestroyImmediate(beam.GetComponent<Collider>());
            beam.isStatic = true;
        }

        private static Transform NewChild(Transform parent, string name)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Material GetOrCreateMaterial(string name, Color color, float smoothness)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = name, color = color };
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(RootFolder);
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(DataFolder);
            AssetDatabase.Refresh();
        }
    }
}
