using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YunJing.Alchemy;

namespace YunJing.Editor
{
    // Generates the playable alchemy room from the B01–B12 exports in art/exports/qingxi-alchemy-b (v001).
    // Room shell, work table, flue elbow and the player capsule are graybox placeholders and are named so.
    public static class AlchemyRoomBuilder
    {
        const string Scene = "Assets/YunJing/Generated/AlchemyRoom.unity";
        const string Root = "Assets/YunJing/Generated/AlchemyRoom";
        const string Models = "Assets/YunJing/Models/AlchemyB";
        static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        static string Exports => Path.Combine(Repo, "art", "exports", "qingxi-alchemy-b");

        [Serializable] class ModelInfo { public float[] dimensions_m; }
        static readonly (string id, string folder, string[] keys)[] Assets = {
            ("B01", "B01_BronzeFurnace", new[] { "B01_BronzeFurnace_cold", "B01_BronzeFurnace_heating" }),
            ("B02", "B02_FurnaceLid", new[] { "B02_FurnaceLid" }),
            ("B03", "B03_StoneMortar", new[] { "B03_StoneMortar" }),
            ("B04", "B04_BambooSortingTrays", new[] { "B04_BambooSortingTrays" }),
            ("B05", "B05_HerbDrawerCabinet", new[] { "B05_HerbDrawerCabinet" }),
            ("B06", "B06_CeramicWaterJar", new[] { "B06_CeramicWaterJar" }),
            ("B07", "B07_WoodenFuelBox", new[] { "B07_WoodenFuelBox" }),
            ("B08", "B08_VentilatedDryingBasket", new[] { "B08_VentilatedDryingBasket" }),
            ("B09", "B09_ClothFilterFrame", new[] { "B09_ClothFilterFrame" }),
            ("B10", "B10_WaxSealedBottle", new[] { "B10_WaxSealedBottle_empty", "B10_WaxSealedBottle_filled" }),
            ("B11", "B11_BambooRecipeStand", new[] { "B11_BambooRecipeStand" }),
            ("B12", "B12_WorkshopChimney", new[] { "B12_WorkshopChimney" }),
        };
        static readonly List<string> Report = new List<string>();

        [MenuItem("Yun Jing/Generate alchemy room")]
        public static void Generate()
        {
            Report.Clear();
            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/YunJing/Generated/URP.asset") == null) ProjectBuilder.Generate();
            Directory.CreateDirectory(Root); CopyExports(); AssetDatabase.Refresh(); ConfigureImports();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var room = new GameObject("AlchemyRoom");
            BuildShell(room.transform);
            var props = new GameObject("Props").transform; props.SetParent(room.transform);

            // Layout follows tools/qingxi-alchemy-b/data/layout.json roles, spread ~1.6x for a third-person camera.
            var furnace = new GameObject("B01_Furnace"); furnace.transform.SetParent(props); furnace.transform.position = new Vector3(0, 0, 1.6f);
            Place("B01_BronzeFurnace_cold", "B01_Cold", furnace.transform, Vector3.zero, 0);
            Place("B01_BronzeFurnace_heating", "B01_Hot", furnace.transform, Vector3.zero, 0).SetActive(false);
            Marker("LidSeat", furnace.transform, new Vector3(0, .746f, 0));
            Place("B02_FurnaceLid", "B02_Lid", furnace.transform, new Vector3(0, .746f, 0), 0);
            Marker("FurnaceMouth", furnace.transform, new Vector3(0, .3f, -.31f));
            var fire = Marker("FurnaceFire", furnace.transform, new Vector3(0, .32f, -.42f)).AddComponent<Light>();
            fire.type = LightType.Point; fire.range = 3.2f; fire.intensity = 0; fire.color = new Color(1, .6f, .3f); fire.shadows = LightShadows.Soft;
            var glow = Marker("RevealGlow", furnace.transform, new Vector3(0, 1.15f, 0)).AddComponent<Light>();
            glow.type = LightType.Point; glow.range = 3; glow.intensity = 0; glow.color = new Color(.75f, 1f, .85f);
            Solid(furnace, new Vector3(.74f, .9f, .77f), new Vector3(0, .45f, 0));
            Station(furnace, "B01", "青銅丹爐", "煉丹", 1.35f);

            var chimney = Place("B12_WorkshopChimney", "B12_Chimney", props, new Vector3(-1.3f, 0, 2.15f), 0);
            Solid(chimney, new Vector3(.76f, 1.95f, .74f), new Vector3(0, .97f, 0));
            Marker("SmokeOutlet", chimney.transform, new Vector3(0, 1.7f, 0));
            var flue = Placeholder("Placeholder_FlueElbow", PrimitiveType.Cylinder, props, new Vector3(-.62f, .93f, 2.0f), new Vector3(.14f, .45f, .14f), new Color(.24f, .22f, .2f));
            flue.transform.rotation = Quaternion.Euler(0, 0, 90); UnityEngine.Object.DestroyImmediate(flue.GetComponent<Collider>());
            var damper = Placeholder("B12_Damper", PrimitiveType.Cube, props, new Vector3(-.95f, .93f, 1.9f), new Vector3(.02f, .16f, .16f), new Color(.35f, .3f, .22f));
            UnityEngine.Object.DestroyImmediate(damper.GetComponent<Collider>());

            var fuel = Place("B07_WoodenFuelBox", "B07_FuelBox", props, new Vector3(-1.45f, 0, .55f), 0);
            Solid(fuel, new Vector3(.7f, .46f, .44f), new Vector3(0, .23f, 0)); Station(fuel, "B07", "柴箱", "取柴");

            var jar = Place("B06_CeramicWaterJar", "B06_WaterJar", props, new Vector3(-2.55f, 0, -.55f), 70);
            Solid(jar, new Vector3(.55f, .56f, .47f), new Vector3(0, .28f, 0)); Station(jar, "B06", "陶水缸", "舀水");

            var cabinet = Place("B05_HerbDrawerCabinet", "B05_Cabinet", props, new Vector3(2.75f, 0, 1.55f), 90);
            Solid(cabinet, new Vector3(.76f, .93f, .48f), new Vector3(0, .46f, 0)); Station(cabinet, "B05", "藥櫃", "取藥辨材");

            var table = Placeholder("Placeholder_WorkTable", PrimitiveType.Cube, props, new Vector3(1.55f, .39f, -.85f), new Vector3(2.0f, .78f, .72f), new Color(.45f, .32f, .2f));
            table.transform.localScale = new Vector3(2.0f, .06f, .72f); table.transform.position = new Vector3(1.55f, .75f, -.85f);
            foreach (var x in new[] { .62f, 2.48f }) foreach (var z in new[] { -1.12f, -.58f })
                Placeholder("Placeholder_TableLeg", PrimitiveType.Cube, table.transform.parent, new Vector3(x, .36f, z), new Vector3(.07f, .72f, .07f), new Color(.38f, .27f, .17f));
            var tableStation = new GameObject("TableStation"); tableStation.transform.SetParent(props); tableStation.transform.position = new Vector3(1.2f, 0, -.85f);
            tableStation.transform.rotation = Quaternion.Euler(0, 180, 0); // player stands on the -Z (front) side of the table
            Station(tableStation, "TABLE", "工作台", "研磨・陰乾・配伍", 1.2f);
            Place("B03_StoneMortar", "B03_Mortar", props, new Vector3(.95f, .78f, -.9f), 180);
            Place("B08_VentilatedDryingBasket", "B08_Basket", props, new Vector3(1.6f, .78f, -.82f), 180);
            Place("B10_WaxSealedBottle_empty", "B10_Empty", props, new Vector3(2.15f, .78f, -1.0f), 180);
            Place("B10_WaxSealedBottle_filled", "B10_Filled", props, new Vector3(2.15f, .78f, -1.0f), 180).SetActive(false);
            var trays = Place("B04_BambooSortingTrays", "B04_Trays", props, new Vector3(.95f, 0, -1.65f), 180);
            Solid(trays, new Vector3(.63f, .67f, .47f), new Vector3(0, .33f, 0));
            var stand = Place("B11_BambooRecipeStand", "B11_Stand", props, new Vector3(2.35f, .78f, -.72f), 180);
            Station(stand, "B11", "竹簡架", "閱讀竹簡", 1.0f);
            var filter = Place("B09_ClothFilterFrame", "B09_FilterFrame", props, new Vector3(-2.6f, 0, 1.75f), 60);
            Solid(filter, new Vector3(.62f, .93f, .42f), new Vector3(0, .47f, 0));

            var player = BuildPlayer(new Vector3(0, 0, -1.5f));
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(QingxiOrbitCamera), typeof(AlchemyCameraDirector));
            cameraGo.tag = "MainCamera"; var camera = cameraGo.GetComponent<Camera>(); camera.nearClipPlane = .05f; camera.farClipPlane = 60; camera.fieldOfView = 50;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camera.transform.position = new Vector3(0, 2.2f, -5); camera.transform.LookAt(new Vector3(0, 1, 0));
            var orbit = new SerializedObject(cameraGo.GetComponent<QingxiOrbitCamera>());
            orbit.FindProperty("distance").floatValue = 3.1f; orbit.FindProperty("minDistance").floatValue = 1.8f; orbit.FindProperty("maxDistance").floatValue = 4.2f;
            orbit.FindProperty("pitch").floatValue = 24; orbit.ApplyModifiedPropertiesWithoutUndo();
            var director = cameraGo.GetComponent<AlchemyCameraDirector>(); director.Orbit = cameraGo.GetComponent<QingxiOrbitCamera>();
            director.Obstacles = ~(1 << 2);
            BuildLighting(room.transform);

            var services = new GameObject("AlchemyServices");
            var api = services.AddComponent<GameApi>();
            var workshop = services.AddComponent<AlchemyWorkshop>();
            workshop.Api = api; workshop.Walker = player.GetComponent<AlchemyWalker>(); workshop.Director = director;
            workshop.ParticleAdditive = ParticleMaterial("ParticleAdditive", true); workshop.ParticleAlpha = ParticleMaterial("ParticleAlpha", false);
            services.AddComponent<AlchemyPlaytest>();
            services.AddComponent<AlchemyBootstrap>().Target = player.transform;

            PlayerSettings.companyName = "YunJing"; PlayerSettings.productName = "雲境 · 青溪丹房";
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
            if (!EditorSceneManager.SaveScene(scene, Scene)) throw new IOException("Could not save " + Scene);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(Repo, "artifacts", "alchemy-room-build.txt"), string.Join("\n", Report));
            Debug.Log("ALCHEMY_ROOM_GENERATED " + Scene + "\n" + string.Join("\n", Report));
        }

        static void CopyExports()
        {
            foreach (var a in Assets)
            {
                var src = Path.Combine(Exports, a.folder, "v001"); var dst = Path.Combine(Repo, "apps", "game", Models, a.folder);
                if (!Directory.Exists(src)) throw new DirectoryNotFoundException(src);
                Directory.CreateDirectory(Path.Combine(dst, "textures"));
                foreach (var file in Directory.GetFiles(src, "*.fbx").Concat(Directory.GetFiles(Path.Combine(src, "textures"), "*.png")).Concat(Directory.GetFiles(src, "*_model_info.json")))
                {
                    var rel = file.Substring(src.Length + 1); var target = Path.Combine(dst, rel);
                    if (!File.Exists(target) || new FileInfo(target).Length != new FileInfo(file).Length) File.Copy(file, target, true);
                }
            }
        }

        static void ConfigureImports()
        {
            foreach (var a in Assets)
            {
                foreach (var key in a.keys)
                {
                    var path = $"{Models}/{a.folder}/{key}.fbx";
                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (importer == null) throw new FileNotFoundException("FBX not imported", path);
                    bool dirty = !importer.bakeAxisConversion || importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation;
                    importer.bakeAxisConversion = true; importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.importAnimation = false; importer.importCameras = false; importer.importLights = false; importer.addCollider = false;
                    if (dirty) importer.SaveAndReimport();
                }
                foreach (var tex in Directory.GetFiles(Path.Combine(Repo, "apps", "game", Models, a.folder, "textures"), "*.png"))
                {
                    var path = $"{Models}/{a.folder}/textures/{Path.GetFileName(tex)}";
                    var ti = AssetImporter.GetAtPath(path) as TextureImporter; if (ti == null) continue;
                    bool normal = path.EndsWith("_Normal.png"), data = normal || path.EndsWith("_ORM.png");
                    var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    if (ti.textureType != type || ti.sRGBTexture == data || ti.maxTextureSize != 1024)
                    { ti.textureType = type; ti.sRGBTexture = !data; ti.maxTextureSize = 1024; ti.mipmapEnabled = true; ti.SaveAndReimport(); }
                }
            }
        }

        static Material ModelMaterial(string folder, string key)
        {
            var path = $"{Root}/{key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            string tex(string ch) => $"{Models}/{folder}/textures/T_{key}_{ch}.png";
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex("BaseColor"))); mat.SetColor("_BaseColor", Color.white);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(tex("Normal"));
            if (normal != null) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
            bool bronze = key.StartsWith("B01") || key.StartsWith("B02");
            mat.SetFloat("_Metallic", bronze ? .55f : 0); mat.SetFloat("_Smoothness", bronze ? .42f : key.StartsWith("B10") ? .6f : .22f);
            var emissive = AssetDatabase.LoadAssetAtPath<Texture2D>(tex("Emissive"));
            if (emissive != null)
            {
                mat.SetTexture("_EmissionMap", emissive); mat.SetColor("_EmissionColor", new Color(1.2f, .7f, .32f)); mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat); return mat;
        }

        static GameObject Place(string key, string name, Transform parent, Vector3 position, float yaw)
        {
            var a = Assets.First(x => x.keys.Contains(key));
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{a.folder}/{key}.fbx");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source); model.name = name;
            model.transform.SetParent(parent, false); model.transform.localPosition = position; model.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var mat = ModelMaterial(a.folder, key);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) { r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray(); r.shadowCastingMode = ShadowCastingMode.On; }
            // The FBX carries _LOD0/_LOD1/_LOD2 meshes; Unity usually builds the LODGroup on import, otherwise add one.
            var levels = new List<LOD>(); float[] cut = { .22f, .07f, .012f };
            for (int i = 0; i < 3; i++)
            {
                var rs = model.GetComponentsInChildren<Renderer>().Where(r => r.name.EndsWith("_LOD" + i)).ToArray();
                if (rs.Length > 0) levels.Add(new LOD(cut[i], rs));
            }
            if (levels.Count > 1 && model.GetComponent<LODGroup>() == null) { var group = model.AddComponent<LODGroup>(); group.SetLODs(levels.ToArray()); group.RecalculateBounds(); }
            var info = JsonUtility.FromJson<ModelInfo>(File.ReadAllText(Path.Combine(Exports, a.folder, "v001", key + "_model_info.json")));
            var b = Bounds(model, "_LOD0"); float expectH = info.dimensions_m[2];
            Report.Add($"{key}: imported size {b.size.x:0.###}×{b.size.z:0.###}×{b.size.y:0.###} m (expected {info.dimensions_m[0]}×{info.dimensions_m[1]}×{expectH}), min y {b.min.y - model.transform.position.y:0.###}");
            if (Mathf.Abs(b.size.y - expectH) > .02f) throw new InvalidDataException($"{key} imported height {b.size.y} does not match model_info {expectH}");
            return model;
        }

        static Bounds Bounds(GameObject go, string suffix)
        {
            var rs = go.GetComponentsInChildren<Renderer>().Where(r => r.name.EndsWith(suffix)).ToArray();
            if (rs.Length == 0) rs = go.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        // uGUI and legacy Text shaders are not referenced by any material asset, so player builds strip them (magenta UI).
        static void IncludeUiShaders()
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var list = settings.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in new[] { "UI/Default", "Sprites/Default" }) // GUI/Text Shader lives in default resources and must not be listed
            {
                var shader = Shader.Find(name); if (shader == null) continue;
                bool present = false; for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) present = true;
                if (!present) { list.InsertArrayElementAtIndex(list.arraySize); list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader; }
            }
            settings.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
        }

        static GameObject Marker(string name, Transform parent, Vector3 local) { var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = local; return go; }

        static void Solid(GameObject go, Vector3 size, Vector3 center) { var c = go.AddComponent<BoxCollider>(); c.size = size; c.center = center; }

        static void Station(GameObject go, string id, string label, string verb, float radius = 1.25f)
        {
            var s = go.AddComponent<AlchemyStation>(); s.Id = id; s.Label = label; s.Verb = verb; s.Radius = radius;
        }

        static Material Flat(string name, Color color, float smooth = .2f)
        {
            var path = $"{Root}/{name}.mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Smoothness", smooth); EditorUtility.SetDirty(mat); return mat;
        }

        static GameObject Placeholder(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Flat("Graybox_" + name.Replace("Placeholder_", ""), color); return go;
        }

        static void BuildShell(Transform room)
        {
            var shell = new GameObject("Placeholder_RoomShell").transform; shell.SetParent(room);
            var floor = Flat("Graybox_Floor", new Color(.42f, .36f, .28f), .15f); var wall = Flat("Graybox_Wall", new Color(.78f, .72f, .6f), .1f);
            var beam = Flat("Graybox_Beam", new Color(.33f, .23f, .15f), .2f); var jade = Flat("Graybox_Tile", new Color(.23f, .38f, .36f), .3f);
            Box(shell, "Floor", new Vector3(0, -.1f, 0), new Vector3(7.2f, .2f, 6.4f), floor);
            Box(shell, "Wall_North", new Vector3(0, 1.5f, 3.2f), new Vector3(7.2f, 3, .2f), wall);
            Box(shell, "Wall_West", new Vector3(-3.6f, 1.5f, 0), new Vector3(.2f, 3, 6.4f), wall);
            Box(shell, "Wall_East", new Vector3(3.6f, 1.5f, 0), new Vector3(.2f, 3, 6.4f), wall);
            Box(shell, "Wall_South_L", new Vector3(-2.35f, 1.5f, -3.2f), new Vector3(2.5f, 3, .2f), wall);
            Box(shell, "Wall_South_R", new Vector3(2.35f, 1.5f, -3.2f), new Vector3(2.5f, 3, .2f), wall);
            Box(shell, "Wall_South_Lintel", new Vector3(0, 2.6f, -3.2f), new Vector3(2.2f, .8f, .2f), wall);
            Box(shell, "Door_Closed", new Vector3(0, 1.1f, -3.18f), new Vector3(2.2f, 2.2f, .12f), beam); // keeps the orbit camera inside
            Box(shell, "Ceiling", new Vector3(0, 3.05f, 0), new Vector3(7.2f, .1f, 6.4f), beam);
            for (int i = 0; i < 4; i++) Box(shell, "Beam", new Vector3(-2.7f + i * 1.8f, 2.85f, 0), new Vector3(.18f, .22f, 6.4f), beam);
            Box(shell, "Hearth_Tile", new Vector3(-.3f, .005f, 1.7f), new Vector3(2.6f, .02f, 1.6f), jade);
            foreach (var x in new[] { -3.45f, 3.45f }) foreach (var z in new[] { -3.05f, 3.05f }) Box(shell, "Pillar", new Vector3(x, 1.5f, z), new Vector3(.24f, 3, .24f), beam);
        }

        static void Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.position = pos; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static GameObject BuildPlayer(Vector3 at)
        {
            var player = new GameObject("Player"); player.transform.position = at; player.layer = 2; // Ignore Raycast: camera casts skip the player
            var cc = player.AddComponent<CharacterController>(); cc.height = 1.7f; cc.radius = .28f; cc.center = new Vector3(0, .85f, 0);
            var body = new GameObject("Body"); body.transform.SetParent(player.transform, false); body.transform.localPosition = new Vector3(0, 1, 0);
            var robe = GameObject.CreatePrimitive(PrimitiveType.Capsule); robe.name = "Placeholder_Robe"; robe.transform.SetParent(body.transform, false);
            robe.transform.localScale = new Vector3(.55f, .62f, .45f); robe.transform.localPosition = new Vector3(0, -.32f, 0);
            UnityEngine.Object.DestroyImmediate(robe.GetComponent<Collider>()); robe.GetComponent<Renderer>().sharedMaterial = Flat("Graybox_Robe", new Color(.35f, .62f, .56f));
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere); head.name = "Placeholder_Head"; head.transform.SetParent(body.transform, false);
            head.transform.localScale = Vector3.one * .26f; head.transform.localPosition = new Vector3(0, .5f, 0);
            UnityEngine.Object.DestroyImmediate(head.GetComponent<Collider>()); head.GetComponent<Renderer>().sharedMaterial = Flat("Graybox_Skin", new Color(.93f, .8f, .68f));
            var seal = GameObject.CreatePrimitive(PrimitiveType.Sphere); seal.name = "Seal"; seal.transform.SetParent(body.transform, false);
            seal.transform.localScale = Vector3.one * .1f; seal.transform.localPosition = new Vector3(0, .12f, .3f);
            UnityEngine.Object.DestroyImmediate(seal.GetComponent<Collider>());
            var sealMat = Flat("Graybox_Seal", new Color(.6f, 1f, .85f)); sealMat.EnableKeyword("_EMISSION"); sealMat.SetColor("_EmissionColor", new Color(.6f, 2f, 1.4f));
            seal.GetComponent<Renderer>().sharedMaterial = sealMat; seal.SetActive(false);
            foreach (Transform t in player.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            player.AddComponent<AlchemyWalker>();
            return player;
        }

        static void BuildLighting(Transform room)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.5f, .5f, .46f); RenderSettings.ambientEquatorColor = new Color(.34f, .29f, .23f); RenderSettings.ambientGroundColor = new Color(.16f, .13f, .1f);
            RenderSettings.fog = false;
            var sun = new GameObject("Window light").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = .55f;
            sun.color = new Color(1, .93f, .8f); sun.transform.rotation = Quaternion.Euler(55, 30, 0); sun.shadows = LightShadows.None; // roofed room: soft fill only
            foreach (var p in new[] { new Vector3(-2.2f, 2.5f, -1.5f), new Vector3(2.2f, 2.5f, -1.2f), new Vector3(1.8f, 2.4f, 1.8f), new Vector3(-2f, 2.4f, 2f) })
            {
                var lamp = new GameObject("Lantern light").AddComponent<Light>(); lamp.transform.SetParent(room); lamp.transform.position = p;
                lamp.type = LightType.Point; lamp.range = 5.5f; lamp.intensity = 2.1f; lamp.color = new Color(1, .82f, .6f); lamp.shadows = p.x > 2 && p.z < 0 ? LightShadows.Soft : LightShadows.None;
                var shade = GameObject.CreatePrimitive(PrimitiveType.Cylinder); shade.name = "Placeholder_Lantern"; shade.transform.SetParent(lamp.transform, false);
                shade.transform.localScale = new Vector3(.22f, .16f, .22f); UnityEngine.Object.DestroyImmediate(shade.GetComponent<Collider>());
                var m = Flat("Graybox_LanternPaper", new Color(1, .86f, .62f)); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(1.6f, 1.1f, .6f));
                shade.GetComponent<Renderer>().sharedMaterial = m;
            }
            var profilePath = $"{Root}/AlchemyVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
            if (!profile.TryGet<Bloom>(out var bloom)) bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(.55f); bloom.threshold.Override(1.05f); bloom.scatter.Override(.6f);
            if (!profile.TryGet<Tonemapping>(out var tone)) tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            if (!profile.TryGet<ColorAdjustments>(out var color)) color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(.35f); color.saturation.Override(6); color.colorFilter.Override(new Color(1, .97f, .92f));
            if (!profile.TryGet<Vignette>(out var vignette)) vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(.22f);
            EditorUtility.SetDirty(profile);
            var volume = new GameObject("Post volume").AddComponent<Volume>(); volume.isGlobal = true; volume.sharedProfile = profile;
        }

        static Material ParticleMaterial(string name, bool additive)
        {
            var path = $"{Root}/{name}.mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", additive ? 2 : 0);
            mat.SetOverrideTag("RenderType", "Transparent"); mat.renderQueue = 3000;
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); mat.SetInt("_DstBlend", (int)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            mat.SetInt("_ZWrite", 0); mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat); return mat;
        }

        // ---------------- batch entry points

        /// Generates the room and checks the C# furnace port against tests/fixtures/alchemy-trace.json.
        public static void Verify()
        {
            Generate(); VerifySim();
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var stations = UnityEngine.Object.FindObjectsByType<AlchemyStation>(FindObjectsSortMode.None);
            var ids = string.Join(",", stations.Select(s => s.Id).OrderBy(x => x));
            if (ids != "B01,B05,B06,B07,B11,TABLE") throw new InvalidDataException("Unexpected stations: " + ids);
            Debug.Log("ALCHEMY_VERIFY_PASS stations " + ids);
        }

        [Serializable] class Fixture { public AlchemyHerbLock[] herbs; public AlchemyEvent[] log; public double[] trace; public int completeTick; public int score; }
        [Serializable] class ContentRecipe { public string id; public AlchemyPhase[] phases; }
        [Serializable] class Content { public AlchemyFire fire; public ContentRecipe[] recipes; }

        public static void VerifySim()
        {
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Repo, "tests", "fixtures", "alchemy-trace.json")));
            var content = JsonUtility.FromJson<Content>(File.ReadAllText(Path.Combine(Repo, "packages", "content", "alchemy.json")));
            var phases = content.recipes.First(r => r.id == "huichun").phases;
            var furnace = Furnace.Replay(content.fire, phases, new[] { "qingling-herb", "water-lotus" }, fixture.log);
            if (furnace.Trace.Count != fixture.trace.Length) throw new InvalidDataException($"Trace length {furnace.Trace.Count} != {fixture.trace.Length}");
            for (int i = 0; i < furnace.Trace.Count; i++)
                if (Math.Abs(furnace.Trace[i] - fixture.trace[i]) > 1e-9) throw new InvalidDataException($"Trace[{i}] {furnace.Trace[i]} != {fixture.trace[i]}");
            if (furnace.CompleteTick != fixture.completeTick) throw new InvalidDataException($"completeTick {furnace.CompleteTick} != {fixture.completeTick}");
            Debug.Log($"ALCHEMY_SIM_PARITY_PASS {furnace.Trace.Count} samples, completeTick {furnace.CompleteTick}");
        }

        public static void BuildWindows()
        {
            Generate(); VerifySim(); IncludeUiShaders();
            var path = Path.Combine(Repo, "artifacts", "alchemy-windows", "YunJingAlchemy.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var options = new BuildPlayerOptions { scenes = new[] { Scene }, target = BuildTarget.StandaloneWindows64, locationPathName = path, options = BuildOptions.Development };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build failed: " + report.summary.result);
            Debug.Log("ALCHEMY_BUILD_PASS " + path + " " + report.summary.totalSize + " bytes");
        }
    }
}
