using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NatureCalls.Editor
{
    /// <summary>
    /// Creates the reusable, non-gameplay foundation for the public-restroom module kit.
    /// It deliberately does not alter Playground or the existing functional WCStall prefab.
    /// </summary>
    public static class MapModuleKitBuilder
    {
        private const string MaterialsFolder = "Assets/Materials/MapModules";
        private const string PrefabsFolder = "Assets/Prefabs/MapModules/Structure";
        private const string CorridorsFolder = "Assets/Prefabs/MapModules/Corridors";
        private const string HazardsFolder = "Assets/Prefabs/MapModules/Hazards";
        private const string SystemsFolder = "Assets/Prefabs/MapModules/Systems";
        private const string StallsFolder = "Assets/Prefabs/MapModules/Stalls";
        private const string SuppliesFolder = "Assets/Prefabs/MapModules/Supplies";
        private const string PreviewScenePath = "Assets/Scenes/MapModuleKit.unity";
        private const string PrototypeScenePath = "Assets/Scenes/MapLayoutPrototype.unity";

        private const string FloorTilePath = PrefabsFolder + "/FloorTile_0_5.prefab";
        private const string WallTilePath = PrefabsFolder + "/WallTile_0_5.prefab";
        private const string WallSegmentPath = PrefabsFolder + "/WallSegment_0_5x3.prefab";
        private const string DoorFramePath = PrefabsFolder + "/Door_Frame.prefab";
        private const string StraightCorridorPath = CorridorsFolder + "/Corridor_Straight.prefab";
        private const string StallConnectorPath = CorridorsFolder + "/Corridor_StallConnector.prefab";
        private const string CornerCorridorPath = CorridorsFolder + "/Corridor_Corner.prefab";
        private const string JunctionPath = CorridorsFolder + "/Junction_Cross.prefab";
        private const string ShortcutPath = CorridorsFolder + "/Shortcut.prefab";
        private const string DirtyHazardPath = HazardsFolder + "/Hazard_DirtyArea.prefab";
        private const string PipeLeakPath = SystemsFolder + "/Pipe_Leak.prefab";
        private const string ValveZonePath = SystemsFolder + "/Valve_Zone.prefab";
        private const string VentUnitPath = SystemsFolder + "/Vent_Unit.prefab";
        private const string NormalStallPath = StallsFolder + "/Stall_Normal.prefab";
        private const string DirtyStallPath = StallsFolder + "/Stall_Dirty.prefab";
        private const string BrokenStallPath = StallsFolder + "/Stall_Broken.prefab";
        private const string EmergencyReliefPath = StallsFolder + "/Relief_Emergency.prefab";
        private const string PaperSupplyPath = SuppliesFolder + "/Supply_Paper.prefab";
        private const string ToolsSupplyPath = SuppliesFolder + "/Supply_Tools.prefab";
        private const string CleaningSupplyPath = SuppliesFolder + "/Supply_Cleaning.prefab";
        private const string DropStationPath = SuppliesFolder + "/Drop_Station.prefab";
        private const string ToiletBowlPath = "Assets/WC/Prefabs/props/SM_toilet_bowl Variant.prefab";
        private const string PlungerPath = "Assets/WC/Prefabs/props/SM_vantus Variant.prefab";

        [MenuItem("NatureCalls/Build Map Module Kit Foundation")]
        public static void BuildFoundation()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before building map modules.");

            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);

            var floor = EnsureMaterial("MapModuleFloorTile", new Color(.54f, .57f, .60f), .28f, .04f);
            var wall = EnsureMaterial("MapModuleWallTile", new Color(.82f, .84f, .85f), .12f, .02f);
            var grout = EnsureMaterial("MapModuleGrout", new Color(.16f, .19f, .22f), .08f, 0f);

            var floorTile = EnsureFloorTile(floor, grout);
            var wallTile = EnsureWallTile(wall, grout);
            var wallSegment = EnsureWallSegment(wall, grout);
            var doorFrame = EnsureDoorFrame(wall);
            EnsurePreviewScene(floorTile, wallTile, wallSegment, doorFrame);

            AssetDatabase.SaveAssets();
            Debug.Log("NatureCalls Map Module Kit foundation created: 0.5m tiles, 3m wall segment, and Door_Frame.");
        }

        [MenuItem("NatureCalls/Build Map Module Kit Corridors")]
        public static void BuildCorridors()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before building map modules.");

            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(CorridorsFolder);

            var floor = EnsureMaterial("MapModuleFloorTile", new Color(.54f, .57f, .60f), .28f, .04f);
            var wall = EnsureMaterial("MapModuleWallTile", new Color(.82f, .84f, .85f), .12f, .02f);
            var grout = EnsureMaterial("MapModuleGrout", new Color(.16f, .19f, .22f), .08f, 0f);
            var floorTile = EnsureFloorTile(floor, grout);
            var wallSegment = EnsureWallSegment(wall, grout);

            var straight = EnsureStraightCorridor(floorTile, wallSegment);
            var corner = EnsureCornerCorridor(floorTile, wallSegment);
            EnsureCorridorPreview(straight, corner);

            AssetDatabase.SaveAssets();
            Debug.Log("NatureCalls Map Module Kit corridors created: Corridor_Straight and Corridor_Corner.");
        }

        [MenuItem("NatureCalls/Build Map Module Kit Stall Connector")]
        public static void BuildStallConnector()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before building map modules.");

            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(CorridorsFolder);
            var floor = EnsureMaterial("MapModuleFloorTile", new Color(.54f, .57f, .60f), .28f, .04f);
            var wall = EnsureMaterial("MapModuleWallTile", new Color(.82f, .84f, .85f), .12f, .02f);
            var grout = EnsureMaterial("MapModuleGrout", new Color(.16f, .19f, .22f), .08f, 0f);
            var floorTile = EnsureFloorTile(floor, grout);
            var wallSegment = EnsureWallSegment(wall, grout);
            var doorFrame = EnsureDoorFrame(wall);

            var connector = EnsureStallConnector(floorTile, wallSegment, doorFrame);
            EnsureStallConnectorPreview(connector);
            ReplacePrototypeDoorwayOverrides(connector);

            AssetDatabase.SaveAssets();
            Debug.Log("NatureCalls Map Module Kit connector created: Corridor_StallConnector.");
        }

        [MenuItem("NatureCalls/Build Map Module Kit Junction")]
        public static void BuildJunction()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before building map modules.");

            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(CorridorsFolder);

            var floor = EnsureMaterial("MapModuleFloorTile", new Color(.54f, .57f, .60f), .28f, .04f);
            var wall = EnsureMaterial("MapModuleWallTile", new Color(.82f, .84f, .85f), .12f, .02f);
            var grout = EnsureMaterial("MapModuleGrout", new Color(.16f, .19f, .22f), .08f, 0f);
            var floorTile = EnsureFloorTile(floor, grout);
            var wallSegment = EnsureWallSegment(wall, grout);

            var junction = EnsureJunction(floorTile, wallSegment);
            EnsureJunctionPreview(junction);

            AssetDatabase.SaveAssets();
            Debug.Log("NatureCalls Map Module Kit junction created: Junction_Cross.");
        }

        [MenuItem("NatureCalls/Build Map Module Kit Normal Stall")]
        public static void BuildNormalStall()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before building map modules.");

            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(StallsFolder);

            var floor = EnsureMaterial("MapModuleFloorTile", new Color(.54f, .57f, .60f), .28f, .04f);
            var wall = EnsureMaterial("MapModuleWallTile", new Color(.82f, .84f, .85f), .12f, .02f);
            var grout = EnsureMaterial("MapModuleGrout", new Color(.16f, .19f, .22f), .08f, 0f);
            var partition = EnsureMaterial("MapModuleStallPartition", new Color(.69f, .73f, .74f), .18f, .02f);
            var door = EnsureMaterial("MapModuleStallDoor", new Color(.39f, .47f, .50f), .22f, .02f);
            var marker = EnsureMaterial("MapModuleInteractionMarker", new Color(.18f, .78f, .72f), .38f, .0f);
            marker.EnableKeyword("_EMISSION");
            marker.SetColor("_EmissionColor", new Color(.02f, .22f, .18f));

            var floorTile = EnsureFloorTile(floor, grout);
            var wallTile = EnsureWallTile(wall, grout);
            var toiletBowl = AssetDatabase.LoadAssetAtPath<GameObject>(ToiletBowlPath);
            if (toiletBowl == null)
                throw new FileNotFoundException("The existing toilet-bowl visual could not be found.", ToiletBowlPath);

            var stall = EnsureNormalStall(floorTile, wallTile, toiletBowl, partition, door, marker);
            EnsureNormalStallPreview(stall);

            AssetDatabase.SaveAssets();
            Debug.Log("NatureCalls Map Module Kit stall created: Stall_Normal.");
        }

        [MenuItem("NatureCalls/Build Map Module Kit Dirty Stall")]
        public static void BuildDirtyStall()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before building map modules.");

            EnsureFolder(MaterialsFolder);
            EnsureFolder(StallsFolder);
            var normalStall = AssetDatabase.LoadAssetAtPath<GameObject>(NormalStallPath);
            if (normalStall == null)
                throw new FileNotFoundException("Build Stall_Normal before building its dirty variation.", NormalStallPath);

            var dirt = EnsureMaterial("MapModuleDirt", new Color(.27f, .20f, .12f), .05f, 0f);
            var dirtyStall = EnsureDirtyStall(normalStall, dirt);
            EnsureDirtyStallPreview(dirtyStall);

            AssetDatabase.SaveAssets();
            Debug.Log("NatureCalls Map Module Kit stall created: Stall_Dirty.");
        }

        [MenuItem("NatureCalls/Build Map Module Kit Broken Stall")]
        public static void BuildBrokenStall()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before building map modules.");

            EnsureFolder(MaterialsFolder);
            EnsureFolder(StallsFolder);
            var normalStall = AssetDatabase.LoadAssetAtPath<GameObject>(NormalStallPath);
            if (normalStall == null)
                throw new FileNotFoundException("Build Stall_Normal before building its broken variation.", NormalStallPath);

            var fault = EnsureMaterial("MapModuleFault", new Color(.78f, .20f, .12f), .2f, 0f);
            var repairMarker = EnsureMaterial("MapModuleRepairMarker", new Color(.95f, .62f, .08f), .3f, .0f);
            repairMarker.EnableKeyword("_EMISSION");
            repairMarker.SetColor("_EmissionColor", new Color(.28f, .12f, .01f));
            var brokenStall = EnsureBrokenStall(normalStall, fault, repairMarker);
            EnsureBrokenStallPreview(brokenStall);

            AssetDatabase.SaveAssets();
            Debug.Log("NatureCalls Map Module Kit stall created: Stall_Broken.");
        }

        [MenuItem("NatureCalls/Build Map Module Kit Emergency Relief")]
        public static void BuildEmergencyRelief()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before building map modules.");

            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(StallsFolder);
            var floor = EnsureMaterial("MapModuleFloorTile", new Color(.54f, .57f, .60f), .28f, .04f);
            var grout = EnsureMaterial("MapModuleGrout", new Color(.16f, .19f, .22f), .08f, 0f);
            var screen = EnsureMaterial("MapModuleEmergencyScreen", new Color(.50f, .57f, .61f), .18f, .02f);
            var fixture = EnsureMaterial("MapModuleEmergencyFixture", new Color(.87f, .88f, .84f), .2f, .0f);
            var marker = EnsureMaterial("MapModuleInteractionMarker", new Color(.18f, .78f, .72f), .38f, .0f);
            marker.EnableKeyword("_EMISSION");
            marker.SetColor("_EmissionColor", new Color(.02f, .22f, .18f));

            var floorTile = EnsureFloorTile(floor, grout);
            var emergencyRelief = EnsureEmergencyRelief(floorTile, screen, fixture, marker);
            EnsureEmergencyReliefPreview(emergencyRelief);

            AssetDatabase.SaveAssets();
            Debug.Log("NatureCalls Map Module Kit facility created: Relief_Emergency.");
        }

        [MenuItem("NatureCalls/Build Map Module Kit Dirty Shortcut")]
        public static void BuildDirtyShortcut()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before building map modules.");

            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(CorridorsFolder);
            EnsureFolder(HazardsFolder);
            var floor = EnsureMaterial("MapModuleFloorTile", new Color(.54f, .57f, .60f), .28f, .04f);
            var wall = EnsureMaterial("MapModuleWallTile", new Color(.82f, .84f, .85f), .12f, .02f);
            var grout = EnsureMaterial("MapModuleGrout", new Color(.16f, .19f, .22f), .08f, 0f);
            var dirt = EnsureMaterial("MapModuleDirt", new Color(.27f, .20f, .12f), .05f, 0f);
            var cleanMarker = EnsureMaterial("MapModuleCleaningMarker", new Color(.28f, .66f, .92f), .35f, .0f);
            cleanMarker.EnableKeyword("_EMISSION");
            cleanMarker.SetColor("_EmissionColor", new Color(.02f, .08f, .24f));

            var floorTile = EnsureFloorTile(floor, grout);
            var wallSegment = EnsureWallSegment(wall, grout);
            var dirtyHazard = EnsureDirtyHazard(dirt, cleanMarker);
            var shortcut = EnsureDirtyShortcut(floorTile, wallSegment, dirtyHazard);
            EnsureDirtyShortcutPreview(shortcut, dirtyHazard);

            AssetDatabase.SaveAssets();
            Debug.Log("NatureCalls Map Module Kit corridor and hazard created: Shortcut and Hazard_DirtyArea.");
        }

        [MenuItem("NatureCalls/Build Map Module Kit Paper Supply")]
        public static void BuildPaperSupply()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before building map modules.");

            EnsureFolder(MaterialsFolder);
            EnsureFolder(SuppliesFolder);
            var cabinet = EnsureMaterial("MapModuleSupplyCabinet", new Color(.46f, .55f, .58f), .25f, .02f);
            var paper = EnsureMaterial("MapModulePaper", new Color(.94f, .94f, .89f), .1f, 0f);
            var marker = EnsureMaterial("MapModuleInteractionMarker", new Color(.18f, .78f, .72f), .38f, .0f);
            marker.EnableKeyword("_EMISSION");
            marker.SetColor("_EmissionColor", new Color(.02f, .22f, .18f));

            var paperSupply = EnsurePaperSupply(cabinet, paper, marker);
            EnsurePaperSupplyPreview(paperSupply);

            AssetDatabase.SaveAssets();
            Debug.Log("NatureCalls Map Module Kit supply created: Supply_Paper.");
        }

        [MenuItem("NatureCalls/Build Map Module Kit Tools Supply")]
        public static void BuildToolsSupply()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before building map modules.");

            EnsureFolder(MaterialsFolder);
            EnsureFolder(SuppliesFolder);
            var cabinet = EnsureMaterial("MapModuleSupplyCabinet", new Color(.46f, .55f, .58f), .25f, .02f);
            var metal = EnsureMaterial("MapModuleToolMetal", new Color(.27f, .31f, .34f), .45f, .65f);
            var handle = EnsureMaterial("MapModuleToolHandle", new Color(.52f, .30f, .14f), .18f, 0f);
            var marker = EnsureMaterial("MapModuleInteractionMarker", new Color(.18f, .78f, .72f), .38f, .0f);
            marker.EnableKeyword("_EMISSION");
            marker.SetColor("_EmissionColor", new Color(.02f, .22f, .18f));

            var plunger = AssetDatabase.LoadAssetAtPath<GameObject>(PlungerPath);
            if (plunger == null)
                throw new FileNotFoundException("The existing plunger visual could not be found.", PlungerPath);

            var toolsSupply = EnsureToolsSupply(cabinet, metal, handle, marker, plunger);
            EnsureToolsSupplyPreview(toolsSupply);

            AssetDatabase.SaveAssets();
            Debug.Log("NatureCalls Map Module Kit supply created: Supply_Tools.");
        }

        [MenuItem("NatureCalls/Build Map Module Kit Cleaning Supply")]
        public static void BuildCleaningSupply()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before building map modules.");

            EnsureFolder(MaterialsFolder);
            EnsureFolder(SuppliesFolder);
            var cabinet = EnsureMaterial("MapModuleSupplyCabinet", new Color(.46f, .55f, .58f), .25f, .02f);
            var handle = EnsureMaterial("MapModuleToolHandle", new Color(.52f, .30f, .14f), .18f, 0f);
            var mopHead = EnsureMaterial("MapModuleMopHead", new Color(.70f, .70f, .62f), .04f, 0f);
            var bucket = EnsureMaterial("MapModuleBucket", new Color(.24f, .48f, .70f), .28f, .04f);
            var cleaner = EnsureMaterial("MapModuleCleanerBottle", new Color(.90f, .56f, .18f), .22f, .0f);
            var marker = EnsureMaterial("MapModuleInteractionMarker", new Color(.18f, .78f, .72f), .38f, .0f);
            marker.EnableKeyword("_EMISSION");
            marker.SetColor("_EmissionColor", new Color(.02f, .22f, .18f));

            var cleaningSupply = EnsureCleaningSupply(cabinet, handle, mopHead, bucket, cleaner, marker);
            EnsureCleaningSupplyPreview(cleaningSupply);

            AssetDatabase.SaveAssets();
            Debug.Log("NatureCalls Map Module Kit supply created: Supply_Cleaning.");
        }

        [MenuItem("NatureCalls/Build Map Module Kit Drop Station")]
        public static void BuildDropStation()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before building map modules.");

            EnsureFolder(MaterialsFolder);
            EnsureFolder(SuppliesFolder);
            var counter = EnsureMaterial("MapModuleDropStation", new Color(.36f, .44f, .48f), .22f, .08f);
            var surface = EnsureMaterial("MapModuleDropSurface", new Color(.68f, .74f, .75f), .28f, .03f);
            var marker = EnsureMaterial("MapModuleInteractionMarker", new Color(.18f, .78f, .72f), .38f, .0f);
            marker.EnableKeyword("_EMISSION");
            marker.SetColor("_EmissionColor", new Color(.02f, .22f, .18f));

            var dropStation = EnsureDropStation(counter, surface, marker);
            EnsureDropStationPreview(dropStation);

            AssetDatabase.SaveAssets();
            Debug.Log("NatureCalls Map Module Kit supply created: Drop_Station.");
        }

        [MenuItem("NatureCalls/Build Map Module Kit Public Systems")]
        public static void BuildPublicSystems()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before building map modules.");

            EnsureFolder(MaterialsFolder);
            EnsureFolder(SystemsFolder);
            var pipe = EnsureMaterial("MapModulePipe", new Color(.31f, .38f, .42f), .55f, .65f);
            var water = EnsureMaterial("MapModuleWaterLeak", new Color(.14f, .44f, .72f), .65f, .04f);
            var valve = EnsureMaterial("MapModuleValve", new Color(.72f, .20f, .14f), .3f, .45f);
            var vent = EnsureMaterial("MapModuleVent", new Color(.48f, .54f, .57f), .35f, .45f);
            var marker = EnsureMaterial("MapModuleInteractionMarker", new Color(.18f, .78f, .72f), .38f, .0f);
            marker.EnableKeyword("_EMISSION");
            marker.SetColor("_EmissionColor", new Color(.02f, .22f, .18f));

            var pipeLeak = EnsurePipeLeak(pipe, water, marker);
            var valveZone = EnsureValveZone(pipe, valve, marker);
            var ventUnit = EnsureVentUnit(vent, marker);
            EnsurePublicSystemsPreview(pipeLeak, valveZone, ventUnit);

            AssetDatabase.SaveAssets();
            Debug.Log("NatureCalls Map Module Kit systems created: Pipe_Leak, Valve_Zone, and Vent_Unit.");
        }

        [MenuItem("NatureCalls/Build Map Module Kit Layout Prototype")]
        public static void BuildLayoutPrototype()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before building map modules.");
            if (File.Exists(PrototypeScenePath))
                throw new InvalidOperationException("MapLayoutPrototype already exists. Review it in Unity rather than overwriting it.");

            var junction = LoadRequiredPrefab(JunctionPath);
            var corridor = LoadRequiredPrefab(StraightCorridorPath);
            var stallConnector = LoadRequiredPrefab(StallConnectorPath);
            var normalStall = LoadRequiredPrefab(NormalStallPath);
            var dirtyStall = LoadRequiredPrefab(DirtyStallPath);
            var brokenStall = LoadRequiredPrefab(BrokenStallPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Map Layout Prototype - visual assembly test");

            var routes = new GameObject("Connected route skeleton");
            routes.transform.SetParent(root.transform, false);
            Place(junction, routes.transform, "Junction_Cross center", Vector3.zero, 0f);
            Place(stallConnector, routes.transform, "Corridor north", new Vector3(0f, 0f, 4.5f), 0f);
            Place(stallConnector, routes.transform, "Corridor south", new Vector3(0f, 0f, -4.5f), 180f);
            Place(stallConnector, routes.transform, "Corridor east", new Vector3(4.5f, 0f, 0f), -90f);
            Place(corridor, routes.transform, "Corridor west", new Vector3(-4.5f, 0f, 0f), 90f);

            var stallTests = new GameObject("Stall doorway connection tests - connector prefab");
            stallTests.transform.SetParent(root.transform, false);
            Place(normalStall, stallTests.transform, "Stall_Normal - doorway test", new Vector3(2.5f, 0f, 4.75f), 90f);
            Place(dirtyStall, stallTests.transform, "Stall_Dirty - doorway test", new Vector3(-2.5f, 0f, -4.75f), -90f);
            Place(brokenStall, stallTests.transform, "Stall_Broken - doorway test", new Vector3(4.75f, 0f, 2.5f), 0f);

            var light = new GameObject("Prototype Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            light.transform.SetParent(root.transform, true);

            var cameraObject = new GameObject("Prototype Camera");
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.fieldOfView = 60f;
            camera.transform.position = new Vector3(12f, 14f, -15f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(-.45f, -.6f, .55f));

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, PrototypeScenePath))
                throw new IOException("Could not save " + PrototypeScenePath);
            Debug.Log("NatureCalls Map Layout Prototype created. Review connected corridors and temporary stall-doorway overrides.");
        }

        [MenuItem("NatureCalls/Place Layout Prototype Support Modules")]
        public static void PlaceLayoutPrototypeSupportModules()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before placing map modules.");
            if (!File.Exists(PrototypeScenePath))
                throw new FileNotFoundException("Build MapLayoutPrototype before placing support modules.", PrototypeScenePath);

            var paperSupply = LoadRequiredPrefab(PaperSupplyPath);
            var toolsSupply = LoadRequiredPrefab(ToolsSupplyPath);
            var cleaningSupply = LoadRequiredPrefab(CleaningSupplyPath);
            var dropStation = LoadRequiredPrefab(DropStationPath);
            var emergencyRelief = LoadRequiredPrefab(EmergencyReliefPath);
            var pipeLeak = LoadRequiredPrefab(PipeLeakPath);
            var valveZone = LoadRequiredPrefab(ValveZonePath);
            var ventUnit = LoadRequiredPrefab(VentUnitPath);

            var scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
            var root = GameObject.Find("Map Layout Prototype - visual assembly test");
            if (root == null)
                throw new InvalidOperationException("MapLayoutPrototype root is missing.");
            if (root.transform.Find("Accessible support module layout") != null)
                throw new InvalidOperationException("MapLayoutPrototype already has an accessible support-module layout.");

            var support = new GameObject("Accessible support module layout");
            support.transform.SetParent(root.transform, false);

            // West corridor: supplies are against the two side walls, leaving the center passage open.
            Place(paperSupply, support.transform, "Supply_Paper - west corridor", new Vector3(-3.1f, 0f, .72f), 0f);
            Place(toolsSupply, support.transform, "Supply_Tools - west corridor", new Vector3(-3.1f, 0f, -.72f), 180f);
            Place(cleaningSupply, support.transform, "Supply_Cleaning - west corridor", new Vector3(-5.65f, 0f, -.48f), 180f);

            // Junction and east route: hand-off space remains at a corner; maintenance equipment is near the broken stall.
            Place(dropStation, support.transform, "Drop_Station - junction corner", new Vector3(-1.5f, 0f, 1.5f), 0f);
            Place(pipeLeak, support.transform, "Pipe_Leak - east maintenance wall", new Vector3(3.25f, 0f, -.95f), 0f);
            Place(valveZone, support.transform, "Valve_Zone - east maintenance wall", new Vector3(4.5f, 0f, -.95f), 0f);
            Place(ventUnit, support.transform, "Vent_Unit - east maintenance wall", new Vector3(5.6f, 0f, -.95f), 0f);

            // South end: the open side faces north toward the south connector.
            Place(emergencyRelief, support.transform, "Relief_Emergency - south end", new Vector3(0f, 0f, -7.25f), 180f);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + PrototypeScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("NatureCalls placed accessible support modules in MapLayoutPrototype.");
        }

        [MenuItem("NatureCalls/Arrange Layout Prototype West Supplies")]
        public static void ArrangeLayoutPrototypeWestSupplies()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before moving map modules.");
            if (!File.Exists(PrototypeScenePath))
                throw new FileNotFoundException("Place support modules before arranging west supplies.", PrototypeScenePath);

            var scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
            var root = GameObject.Find("Map Layout Prototype - visual assembly test");
            if (root == null)
                throw new InvalidOperationException("MapLayoutPrototype root is missing.");

            UpdatePrototypeTransform(root.transform, "Supply_Tools - west corridor",
                new Vector3(-3.1f, 0f, -.72f), Quaternion.Euler(0f, 180f, 0f));

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + PrototypeScenePath);
            Debug.Log("NatureCalls arranged west supplies with Paper and Tools facing each other.");
        }

        [MenuItem("NatureCalls/Repair Door Frame And Clean Layout Prototype")]
        public static void RepairDoorFrameAndCleanLayoutPrototype()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before repairing map modules.");

            EnsureFolder(MaterialsFolder);
            var wall = EnsureMaterial("MapModuleWallTile", new Color(.82f, .84f, .85f), .12f, .02f);
            EnsureDoorFrame(wall);

            if (File.Exists(PrototypeScenePath))
            {
                var scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
                var root = GameObject.Find("Map Layout Prototype - visual assembly test");
                if (root == null)
                    throw new InvalidOperationException("MapLayoutPrototype root is missing.");
                var staging = root.transform.Find("Support module placement tests");
                if (staging != null)
                    UnityEngine.Object.DestroyImmediate(staging.gameObject);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("Could not save " + PrototypeScenePath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("NatureCalls repaired Door_Frame and removed unconnected staging modules from MapLayoutPrototype.");
        }

        [MenuItem("NatureCalls/Align Layout Prototype Doorways")]
        public static void AlignLayoutPrototypeDoorways()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before aligning map modules.");
            if (!File.Exists(PrototypeScenePath))
                throw new FileNotFoundException("Build MapLayoutPrototype before aligning its doorways.", PrototypeScenePath);

            var scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
            var root = GameObject.Find("Map Layout Prototype - visual assembly test");
            if (root == null)
                throw new InvalidOperationException("MapLayoutPrototype root is missing.");

            // The three removed .5m wall segments are centered at local Z -0.75, -0.25, and 0.25.
            // Their 1.5m opening is centered at -0.25, rather than at the corridor origin.
            UpdatePrototypePosition(root.transform, "North doorway frame", new Vector3(1.15f, 0f, 4.25f));
            UpdatePrototypePosition(root.transform, "Stall_Normal - doorway test", new Vector3(2.5f, 0f, 4.25f));
            UpdatePrototypePosition(root.transform, "South doorway frame", new Vector3(-1.15f, 0f, -4.75f));
            UpdatePrototypePosition(root.transform, "Stall_Dirty - doorway test", new Vector3(-2.5f, 0f, -4.75f));
            UpdatePrototypePosition(root.transform, "East doorway frame", new Vector3(4.25f, 0f, 1.15f));
            UpdatePrototypePosition(root.transform, "Stall_Broken - doorway test", new Vector3(4.25f, 0f, 2.5f));

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + PrototypeScenePath);
            Debug.Log("NatureCalls aligned MapLayoutPrototype doorways to the corridor wall grid.");
        }

        [MenuItem("NatureCalls/Move Layout Prototype North East Doorways Outward")]
        public static void MoveLayoutPrototypeNorthEastDoorwaysOutward()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before moving map modules.");
            if (!File.Exists(PrototypeScenePath))
                throw new FileNotFoundException("Build MapLayoutPrototype before moving its doorways.", PrototypeScenePath);

            var wallSegment = LoadRequiredPrefab(WallSegmentPath);
            var scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
            var root = GameObject.Find("Map Layout Prototype - visual assembly test");
            if (root == null)
                throw new InvalidOperationException("MapLayoutPrototype root is missing.");

            var northCorridor = FindDescendantByName(root.transform, "Corridor north");
            var eastCorridor = FindDescendantByName(root.transform, "Corridor east");
            if (northCorridor == null || eastCorridor == null)
                throw new InvalidOperationException("MapLayoutPrototype is missing a required corridor.");

            MoveCorridorOpening(northCorridor, "Right wall ", wallSegment, 3, 4, 5, 6, 7);
            MoveCorridorOpening(eastCorridor, "Left wall ", wallSegment, 3, 4, 5, 6, 7);
            UpdatePrototypePosition(root.transform, "North doorway frame", new Vector3(1.15f, 0f, 5.25f));
            UpdatePrototypePosition(root.transform, "Stall_Normal - doorway test", new Vector3(2.5f, 0f, 5.25f));
            UpdatePrototypePosition(root.transform, "East doorway frame", new Vector3(5.25f, 0f, 1.15f));
            UpdatePrototypePosition(root.transform, "Stall_Broken - doorway test", new Vector3(5.25f, 0f, 2.5f));

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + PrototypeScenePath);
            Debug.Log("NatureCalls moved north and east doorway tests outward without changing other stalls.");
        }

        [MenuItem("NatureCalls/Standardize Layout Prototype Doorways")]
        public static void StandardizeLayoutPrototypeDoorways()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before moving map modules.");
            if (!File.Exists(PrototypeScenePath))
                throw new FileNotFoundException("Build MapLayoutPrototype before standardizing its doorways.", PrototypeScenePath);

            var wallSegment = LoadRequiredPrefab(WallSegmentPath);
            var scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
            var root = GameObject.Find("Map Layout Prototype - visual assembly test");
            if (root == null)
                throw new InvalidOperationException("MapLayoutPrototype root is missing.");

            var northCorridor = FindDescendantByName(root.transform, "Corridor north");
            var southCorridor = FindDescendantByName(root.transform, "Corridor south");
            var eastCorridor = FindDescendantByName(root.transform, "Corridor east");
            if (northCorridor == null || southCorridor == null || eastCorridor == null)
                throw new InvalidOperationException("MapLayoutPrototype is missing a required corridor.");

            SetCorridorOpening(northCorridor, "Right wall ", wallSegment, 4, 5, 6);
            SetCorridorOpening(southCorridor, "Left wall ", wallSegment, 2, 3, 4);
            SetCorridorOpening(eastCorridor, "Left wall ", wallSegment, 4, 5, 6);

            UpdatePrototypePosition(root.transform, "North doorway frame", new Vector3(1.15f, 0f, 4.75f));
            UpdatePrototypePosition(root.transform, "Stall_Normal - doorway test", new Vector3(2.5f, 0f, 4.75f));
            UpdatePrototypePosition(root.transform, "South doorway frame", new Vector3(-1.15f, 0f, -5.25f));
            UpdatePrototypePosition(root.transform, "Stall_Dirty - doorway test", new Vector3(-2.5f, 0f, -5.25f));
            UpdatePrototypePosition(root.transform, "East doorway frame", new Vector3(4.75f, 0f, 1.15f));
            UpdatePrototypePosition(root.transform, "Stall_Broken - doorway test", new Vector3(4.75f, 0f, 2.5f));

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + PrototypeScenePath);
            Debug.Log("NatureCalls standardized all layout prototype doorways to the shared 0.5m-grid connector layout.");
        }

        [MenuItem("NatureCalls/Fix Layout Prototype South Doorway Alignment")]
        public static void FixLayoutPrototypeSouthDoorwayAlignment()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before moving map modules.");
            if (!File.Exists(PrototypeScenePath))
                throw new FileNotFoundException("Build MapLayoutPrototype before correcting its south doorway.", PrototypeScenePath);

            var wallSegment = LoadRequiredPrefab(WallSegmentPath);
            var scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
            var root = GameObject.Find("Map Layout Prototype - visual assembly test");
            if (root == null)
                throw new InvalidOperationException("MapLayoutPrototype root is missing.");

            var southCorridor = FindDescendantByName(root.transform, "Corridor south");
            if (southCorridor == null)
                throw new InvalidOperationException("MapLayoutPrototype is missing Corridor south.");
            SetCorridorOpening(southCorridor, "Left wall ", wallSegment, 3, 4, 5);
            UpdatePrototypePosition(root.transform, "South doorway frame", new Vector3(-1.15f, 0f, -4.75f));
            UpdatePrototypePosition(root.transform, "Stall_Dirty - doorway test", new Vector3(-2.5f, 0f, -4.75f));

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + PrototypeScenePath);
            Debug.Log("NatureCalls corrected the south doorway to the shared layout alignment.");
        }

        private static GameObject EnsureFloorTile(Material floor, Material grout)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(FloorTilePath);
            if (existing != null) return existing;

            var root = new GameObject("FloorTile_0_5");
            try
            {
                Box(root.transform, "Ceramic", new Vector3(0f, .07f, 0f), new Vector3(.49f, .14f, .49f), floor);
                Box(root.transform, "Grout", Vector3.zero, new Vector3(.5f, .025f, .5f), grout);
                return SavePrefab(root, FloorTilePath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject EnsureWallTile(Material wall, Material grout)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(WallTilePath);
            if (existing != null) return existing;

            var root = new GameObject("WallTile_0_5");
            try
            {
                Box(root.transform, "Ceramic", new Vector3(0f, .25f, 0f), new Vector3(.49f, .49f, .18f), wall);
                Box(root.transform, "Grout", new Vector3(0f, .25f, .095f), new Vector3(.5f, .5f, .01f), grout);
                return SavePrefab(root, WallTilePath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject EnsureWallSegment(Material wall, Material grout)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(WallSegmentPath);
            if (existing != null) return existing;

            var root = new GameObject("WallSegment_0_5x3");
            try
            {
                for (int row = 0; row < 6; row++)
                {
                    float y = .25f + row * .5f;
                    Box(root.transform, "WallTile " + (row + 1), new Vector3(0f, y, 0f), new Vector3(.49f, .49f, .18f), wall);
                    Box(root.transform, "Grout " + (row + 1), new Vector3(0f, y, .095f), new Vector3(.5f, .5f, .01f), grout);
                }
                return SavePrefab(root, WallSegmentPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject EnsureDoorFrame(Material wall)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(DoorFramePath);
            if (existing != null)
            {
                UpdateDoorFrame();
                return AssetDatabase.LoadAssetAtPath<GameObject>(DoorFramePath);
            }

            var root = new GameObject("Door_Frame");
            try
            {
                // Outer width is 1.5m; the 1m opening is intentionally compatible with the team specification.
                Box(root.transform, "Left Jamb", new Vector3(-.625f, 1f, 0f), new Vector3(.25f, 2f, .2f), wall);
                Box(root.transform, "Right Jamb", new Vector3(.625f, 1f, 0f), new Vector3(.25f, 2f, .2f), wall);
                Box(root.transform, "Header", new Vector3(0f, 2.5f, 0f), new Vector3(1.5f, 1f, .2f), wall);
                return SavePrefab(root, DoorFramePath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void UpdateDoorFrame()
        {
            var root = PrefabUtility.LoadPrefabContents(DoorFramePath);
            try
            {
                UpdateFramePart(root.transform, "Left Jamb", new Vector3(-.625f, 1f, 0f), new Vector3(.25f, 2f, .2f));
                UpdateFramePart(root.transform, "Right Jamb", new Vector3(.625f, 1f, 0f), new Vector3(.25f, 2f, .2f));
                UpdateFramePart(root.transform, "Header", new Vector3(0f, 2.5f, 0f), new Vector3(1.5f, 1f, .2f));
                PrefabUtility.SaveAsPrefabAsset(root, DoorFramePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void UpdateFramePart(Transform root, string name, Vector3 position, Vector3 scale)
        {
            var part = root.Find(name);
            if (part == null)
                throw new InvalidOperationException("Door_Frame is missing " + name + ".");
            part.localPosition = position;
            part.localScale = scale;
        }

        private static GameObject EnsureStraightCorridor(GameObject floorTile, GameObject wallSegment)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StraightCorridorPath);
            if (existing != null) return existing;

            var root = new GameObject("Corridor_Straight");
            try
            {
                BuildFloorGrid(root.transform, floorTile, 5, 8, new Vector2(2.5f, 4f));
                for (int row = 0; row < 8; row++)
                {
                    float z = -1.75f + row * .5f;
                    Place(wallSegment, root.transform, "Left wall " + (row + 1), new Vector3(-1.15f, 0f, z), 90f);
                    Place(wallSegment, root.transform, "Right wall " + (row + 1), new Vector3(1.15f, 0f, z), -90f);
                }
                return SavePrefab(root, StraightCorridorPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject EnsureStallConnector(GameObject floorTile, GameObject wallSegment, GameObject doorFrame)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StallConnectorPath);
            if (existing != null) return existing;

            var root = new GameObject("Corridor_StallConnector");
            try
            {
                // Same 2.5m x 4m x 3m outer envelope as Corridor_Straight.
                // The right wall has the shared three-segment opening, centered at local Z = .25m.
                BuildFloorGrid(root.transform, floorTile, 5, 8, new Vector2(2.5f, 4f));
                for (int row = 0; row < 8; row++)
                {
                    float z = -1.75f + row * .5f;
                    int segmentNumber = row + 1;
                    Place(wallSegment, root.transform, "Left wall " + segmentNumber, new Vector3(-1.15f, 0f, z), 90f);
                    if (segmentNumber >= 4 && segmentNumber <= 6) continue;
                    Place(wallSegment, root.transform, "Right wall " + segmentNumber, new Vector3(1.15f, 0f, z), -90f);
                }

                Place(doorFrame, root.transform, "Stall doorway frame", new Vector3(1.15f, 0f, .25f), 90f);
                return SavePrefab(root, StallConnectorPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject EnsureCornerCorridor(GameObject floorTile, GameObject wallSegment)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CornerCorridorPath);
            if (existing != null) return existing;

            var root = new GameObject("Corridor_Corner");
            try
            {
                // A 4m square L-turn: the north-west block is closed, preventing full sight across the bend.
                for (int x = 0; x < 8; x++)
                {
                    for (int z = 0; z < 8; z++)
                    {
                        float localX = -1.75f + x * .5f;
                        float localZ = -1.75f + z * .5f;
                        if (localZ > .25f && localX < -.25f) continue;
                        Place(floorTile, root.transform, "Floor " + x + "," + z, new Vector3(localX, 0f, localZ), 0f);
                    }
                }

                for (int row = 0; row < 8; row++)
                {
                    float z = -1.75f + row * .5f;
                    Place(wallSegment, root.transform, "West outer wall " + (row + 1), new Vector3(-1.9f, 0f, z), 90f);
                }
                for (int column = 0; column < 8; column++)
                {
                    float x = -1.75f + column * .5f;
                    Place(wallSegment, root.transform, "North outer wall " + (column + 1), new Vector3(x, 0f, 1.9f), 0f);
                }

                for (int row = 0; row < 3; row++)
                    Place(wallSegment, root.transform, "Inner corner vertical " + (row + 1), new Vector3(-.4f, 0f, .75f + row * .5f), 90f);
                for (int column = 0; column < 3; column++)
                    Place(wallSegment, root.transform, "Inner corner horizontal " + (column + 1), new Vector3(-1.25f + column * .5f, 0f, .4f), 0f);

                return SavePrefab(root, CornerCorridorPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject EnsureJunction(GameObject floorTile, GameObject wallSegment)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(JunctionPath);
            if (existing != null)
            {
                UpdateJunctionOpening(wallSegment);
                return AssetDatabase.LoadAssetAtPath<GameObject>(JunctionPath);
            }

            var root = new GameObject("Junction_Cross");
            try
            {
                BuildFloorGrid(root.transform, floorTile, 10, 10, new Vector2(5f, 5f));

                // Two 1m wall stubs at each corner leave a 3m opening on every side,
                // comfortably accepting the 2.5m-wide corridor modules.
                for (int index = 0; index < 2; index++)
                {
                    float offset = 2.25f - index * .5f;
                    Place(wallSegment, root.transform, "North west wall " + (index + 1), new Vector3(-offset, 0f, 2.4f), 0f);
                    Place(wallSegment, root.transform, "North east wall " + (index + 1), new Vector3(offset, 0f, 2.4f), 0f);
                    Place(wallSegment, root.transform, "South west wall " + (index + 1), new Vector3(-offset, 0f, -2.4f), 0f);
                    Place(wallSegment, root.transform, "South east wall " + (index + 1), new Vector3(offset, 0f, -2.4f), 0f);
                    Place(wallSegment, root.transform, "West north wall " + (index + 1), new Vector3(-2.4f, 0f, offset), 90f);
                    Place(wallSegment, root.transform, "West south wall " + (index + 1), new Vector3(-2.4f, 0f, -offset), 90f);
                    Place(wallSegment, root.transform, "East north wall " + (index + 1), new Vector3(2.4f, 0f, offset), 90f);
                    Place(wallSegment, root.transform, "East south wall " + (index + 1), new Vector3(2.4f, 0f, -offset), 90f);
                }
                AddJunctionOpeningExtensions(root.transform, wallSegment);
                return SavePrefab(root, JunctionPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void UpdateJunctionOpening(GameObject wallSegment)
        {
            var root = PrefabUtility.LoadPrefabContents(JunctionPath);
            try
            {
                AddJunctionOpeningExtensions(root.transform, wallSegment);
                PrefabUtility.SaveAsPrefabAsset(root, JunctionPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void AddJunctionOpeningExtensions(Transform root, GameObject wallSegment)
        {
            if (root.Find("North west opening extension") == null)
            {
                // Half-wall pieces reduce the original 3m opening to the straight-corridor width.
                PlaceHalfWall(root, wallSegment, "North west opening extension", new Vector3(-1.375f, 0f, 2.4f), 0f);
                PlaceHalfWall(root, wallSegment, "North east opening extension", new Vector3(1.375f, 0f, 2.4f), 0f);
                PlaceHalfWall(root, wallSegment, "South west opening extension", new Vector3(-1.375f, 0f, -2.4f), 0f);
                PlaceHalfWall(root, wallSegment, "South east opening extension", new Vector3(1.375f, 0f, -2.4f), 0f);
                PlaceHalfWall(root, wallSegment, "West north opening extension", new Vector3(-2.4f, 0f, 1.375f), 90f);
                PlaceHalfWall(root, wallSegment, "West south opening extension", new Vector3(-2.4f, 0f, -1.375f), 90f);
                PlaceHalfWall(root, wallSegment, "East north opening extension", new Vector3(2.4f, 0f, 1.375f), 90f);
                PlaceHalfWall(root, wallSegment, "East south opening extension", new Vector3(2.4f, 0f, -1.375f), 90f);
            }

            AddJunctionOpeningInsets(root, wallSegment);
        }

        private static void AddJunctionOpeningInsets(Transform root, GameObject wallSegment)
        {
            if (root.Find("North west opening inset") != null) return;

            // The user-approved 0.18m inset on each side reduces every opening from about 2.5m to about 2.14m.
            const float insetScale = .37f;
            PlaceFractionWall(root, wallSegment, "North west opening inset", new Vector3(-1.162f, 0f, 2.4f), 0f, insetScale);
            PlaceFractionWall(root, wallSegment, "North east opening inset", new Vector3(1.162f, 0f, 2.4f), 0f, insetScale);
            PlaceFractionWall(root, wallSegment, "South west opening inset", new Vector3(-1.162f, 0f, -2.4f), 0f, insetScale);
            PlaceFractionWall(root, wallSegment, "South east opening inset", new Vector3(1.162f, 0f, -2.4f), 0f, insetScale);
            PlaceFractionWall(root, wallSegment, "West north opening inset", new Vector3(-2.4f, 0f, 1.162f), 90f, insetScale);
            PlaceFractionWall(root, wallSegment, "West south opening inset", new Vector3(-2.4f, 0f, -1.162f), 90f, insetScale);
            PlaceFractionWall(root, wallSegment, "East north opening inset", new Vector3(2.4f, 0f, 1.162f), 90f, insetScale);
            PlaceFractionWall(root, wallSegment, "East south opening inset", new Vector3(2.4f, 0f, -1.162f), 90f, insetScale);
        }

        private static GameObject EnsureNormalStall(GameObject floorTile, GameObject wallTile, GameObject toiletBowl,
            Material partition, Material door, Material marker)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(NormalStallPath);
            if (existing != null)
            {
                UpdateNormalStallDoor(existing);
                return AssetDatabase.LoadAssetAtPath<GameObject>(NormalStallPath);
            }

            var root = new GameObject("Stall_Normal");
            try
            {
                // The root footprint is 2m wide by 2.5m deep. The visual door is deliberately static;
                // gameplay can later replace it with a networked interactive door without changing this shell.
                BuildFloorGrid(root.transform, floorTile, 4, 5, new Vector2(2f, 2.5f));

                var rearWall = new GameObject("Rear tiled wall");
                rearWall.transform.SetParent(root.transform, false);
                for (int column = 0; column < 4; column++)
                    for (int row = 0; row < 5; row++)
                        Place(wallTile, rearWall.transform, "Tile " + (column + 1) + "," + (row + 1),
                            new Vector3(-.75f + column * .5f, .25f + row * .5f, 1.16f), 0f);

                Box(root.transform, "Left partition", new Vector3(-.925f, 1.125f, 0f), new Vector3(.15f, 2.25f, 2.5f), partition);
                Box(root.transform, "Right partition", new Vector3(.925f, 1.125f, 0f), new Vector3(.15f, 2.25f, 2.5f), partition);
                // The two fixed panels leave the specified 1m clear door opening in the 2m-wide stall.
                Box(root.transform, "Left front panel", new Vector3(-.75f, 1.125f, -1.175f), new Vector3(.5f, 2.25f, .15f), partition);
                Box(root.transform, "Right front panel", new Vector3(.75f, 1.125f, -1.175f), new Vector3(.5f, 2.25f, .15f), partition);
                Box(root.transform, "Door visual - replace with interactive door", new Vector3(0f, .975f, -1.225f), new Vector3(.95f, 1.95f, .05f), door);

                var toilet = Place(toiletBowl, root.transform, "Toilet visual", new Vector3(0f, .14f, .58f), 180f);
                ScaleHierarchyToBounds(toilet, new Vector3(.45f, .8f, .75f));

                var paperHolder = new GameObject("Toilet paper holder");
                paperHolder.transform.SetParent(root.transform, false);
                Box(paperHolder.transform, "Holder backplate", new Vector3(.77f, .71f, .35f), new Vector3(.03f, .18f, .22f), partition);
                var paper = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                paper.name = "Toilet paper roll";
                paper.transform.SetParent(paperHolder.transform, false);
                paper.transform.localPosition = new Vector3(.70f, .72f, .35f);
                paper.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                paper.transform.localScale = new Vector3(.06f, .05f, .06f);
                paper.GetComponent<Renderer>().sharedMaterial = EnsureMaterial("MapModulePaper", new Color(.94f, .94f, .89f), .1f, 0f);

                var interaction = new GameObject("InteractionPoint_Reserved");
                interaction.transform.SetParent(root.transform, false);
                interaction.transform.localPosition = new Vector3(0f, .16f, -.05f);
                var interactionVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                interactionVisual.name = "Interaction marker visual";
                interactionVisual.transform.SetParent(interaction.transform, false);
                interactionVisual.transform.localScale = new Vector3(.18f, .012f, .18f);
                interactionVisual.GetComponent<Renderer>().sharedMaterial = marker;

                return SavePrefab(root, NormalStallPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void UpdateNormalStallDoor(GameObject existing)
        {
            var root = PrefabUtility.LoadPrefabContents(NormalStallPath);
            try
            {
                var door = root.transform.Find("Door visual - replace with interactive door");
                if (door == null)
                    throw new InvalidOperationException("Stall_Normal is missing its visual door.");

                door.localPosition = new Vector3(0f, .975f, -1.225f);
                door.localScale = new Vector3(.95f, 1.95f, .05f);
                var leftPanel = root.transform.Find("Left front panel");
                var rightPanel = root.transform.Find("Right front panel");
                if (leftPanel == null || rightPanel == null)
                    throw new InvalidOperationException("Stall_Normal is missing one or both fixed front panels.");

                leftPanel.localPosition = new Vector3(-.75f, 1.125f, -1.175f);
                leftPanel.localScale = new Vector3(.5f, 2.25f, .15f);
                rightPanel.localPosition = new Vector3(.75f, 1.125f, -1.175f);
                rightPanel.localScale = new Vector3(.5f, 2.25f, .15f);
                PrefabUtility.SaveAsPrefabAsset(root, NormalStallPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static GameObject EnsureDirtyStall(GameObject normalStall, Material dirt)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(DirtyStallPath);
            if (existing != null) return existing;

            var root = new GameObject("Stall_Dirty");
            try
            {
                Place(normalStall, root.transform, "Stall_Normal shell", Vector3.zero, 0f);

                var dirtyArea = new GameObject("Hazard_DirtyArea_Reserved");
                dirtyArea.transform.SetParent(root.transform, false);
                dirtyArea.transform.localPosition = new Vector3(0f, .155f, .05f);

                CreatePuddle(dirtyArea.transform, "Main dirt puddle", Vector3.zero, new Vector3(.62f, .018f, .48f), dirt);
                CreatePuddle(dirtyArea.transform, "Small dirt puddle 1", new Vector3(-.42f, 0f, -.28f), new Vector3(.20f, .012f, .16f), dirt);
                CreatePuddle(dirtyArea.transform, "Small dirt puddle 2", new Vector3(.40f, 0f, .30f), new Vector3(.15f, .012f, .13f), dirt);

                return SavePrefab(root, DirtyStallPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject EnsureBrokenStall(GameObject normalStall, Material fault, Material repairMarker)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BrokenStallPath);
            if (existing != null) return existing;

            var root = new GameObject("Stall_Broken");
            try
            {
                Place(normalStall, root.transform, "Stall_Normal shell", Vector3.zero, 0f);

                var indicator = new GameObject("OutOfService_Visual");
                indicator.transform.SetParent(root.transform, false);
                indicator.transform.localPosition = new Vector3(0f, 1.05f, -1.26f);
                Box(indicator.transform, "Fault stripe A", Vector3.zero, new Vector3(1.14f, .12f, .04f), fault)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 40f);
                Box(indicator.transform, "Fault stripe B", Vector3.zero, new Vector3(1.14f, .12f, .04f), fault)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, -40f);

                var repairPoint = new GameObject("RepairPoint_Reserved");
                repairPoint.transform.SetParent(root.transform, false);
                repairPoint.transform.localPosition = new Vector3(.58f, .12f, -1.38f);
                var repairVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                repairVisual.name = "Repair marker visual";
                repairVisual.transform.SetParent(repairPoint.transform, false);
                repairVisual.transform.localScale = new Vector3(.16f, .012f, .16f);
                repairVisual.GetComponent<Renderer>().sharedMaterial = repairMarker;

                return SavePrefab(root, BrokenStallPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject EnsureEmergencyRelief(GameObject floorTile, Material screen, Material fixture, Material marker)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(EmergencyReliefPath);
            if (existing != null) return existing;

            var root = new GameObject("Relief_Emergency");
            try
            {
                // 1.5m x 1.5m footprint and 2m screens. The front deliberately remains open for fast access.
                BuildFloorGrid(root.transform, floorTile, 3, 3, new Vector2(1.5f, 1.5f));
                Box(root.transform, "Rear privacy screen", new Vector3(0f, 1f, .70f), new Vector3(1.5f, 2f, .1f), screen);
                Box(root.transform, "Left privacy screen", new Vector3(-.70f, 1f, 0f), new Vector3(.1f, 2f, 1.5f), screen);
                Box(root.transform, "Right privacy screen", new Vector3(.70f, 1f, 0f), new Vector3(.1f, 2f, 1.5f), screen);

                var basin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                basin.name = "Emergency relief fixture visual";
                basin.transform.SetParent(root.transform, false);
                basin.transform.localPosition = new Vector3(0f, .10f, .30f);
                basin.transform.localScale = new Vector3(.28f, .10f, .28f);
                basin.GetComponent<Renderer>().sharedMaterial = fixture;

                var interaction = new GameObject("InteractionPoint_Reserved");
                interaction.transform.SetParent(root.transform, false);
                interaction.transform.localPosition = new Vector3(0f, .03f, -.18f);
                var interactionVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                interactionVisual.name = "Interaction marker visual";
                interactionVisual.transform.SetParent(interaction.transform, false);
                interactionVisual.transform.localScale = new Vector3(.16f, .012f, .16f);
                interactionVisual.GetComponent<Renderer>().sharedMaterial = marker;

                return SavePrefab(root, EmergencyReliefPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject EnsureDirtyHazard(Material dirt, Material cleanMarker)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(DirtyHazardPath);
            if (existing != null) return existing;

            var root = new GameObject("Hazard_DirtyArea");
            try
            {
                // The empty reserved volume records the approved 1.5m x 1.5m x 1m gameplay extent.
                var volume = new GameObject("HazardVolume_Reserved");
                volume.transform.SetParent(root.transform, false);
                volume.transform.localScale = new Vector3(1.5f, 1f, 1.5f);

                CreatePuddle(root.transform, "Main dirty area", new Vector3(0f, .02f, 0f), new Vector3(.73f, .012f, .73f), dirt);
                CreatePuddle(root.transform, "Dirty edge 1", new Vector3(-.53f, .018f, -.36f), new Vector3(.24f, .010f, .20f), dirt);
                CreatePuddle(root.transform, "Dirty edge 2", new Vector3(.48f, .018f, .42f), new Vector3(.20f, .010f, .16f), dirt);

                var cleaningPoint = new GameObject("CleaningPoint_Reserved");
                cleaningPoint.transform.SetParent(root.transform, false);
                cleaningPoint.transform.localPosition = new Vector3(.48f, .025f, -.48f);
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "Cleaning marker visual";
                marker.transform.SetParent(cleaningPoint.transform, false);
                marker.transform.localScale = new Vector3(.10f, .012f, .10f);
                marker.GetComponent<Renderer>().sharedMaterial = cleanMarker;

                return SavePrefab(root, DirtyHazardPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject EnsureDirtyShortcut(GameObject floorTile, GameObject wallSegment, GameObject dirtyHazard)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ShortcutPath);
            if (existing != null) return existing;

            var root = new GameObject("Shortcut");
            try
            {
                // 2m x 4m x 3m route: shorter connection, but its middle is visibly contaminated.
                BuildFloorGrid(root.transform, floorTile, 4, 8, new Vector2(2f, 4f));
                for (int row = 0; row < 8; row++)
                {
                    float z = -1.75f + row * .5f;
                    Place(wallSegment, root.transform, "Left wall " + (row + 1), new Vector3(-.9f, 0f, z), 90f);
                    Place(wallSegment, root.transform, "Right wall " + (row + 1), new Vector3(.9f, 0f, z), -90f);
                }
                Place(dirtyHazard, root.transform, "Hazard_DirtyArea north", new Vector3(0f, 0f, .75f), 0f);
                Place(dirtyHazard, root.transform, "Hazard_DirtyArea south", new Vector3(0f, 0f, -.75f), 0f);
                return SavePrefab(root, ShortcutPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject EnsurePaperSupply(Material cabinet, Material paper, Material marker)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PaperSupplyPath);
            if (existing != null) return existing;

            var root = new GameObject("Supply_Paper");
            try
            {
                // Cabinet outer dimensions follow the team table: 1m wide, .5m deep, 1.5m tall.
                Box(root.transform, "Cabinet back", new Vector3(0f, .75f, .22f), new Vector3(1f, 1.5f, .06f), cabinet);
                Box(root.transform, "Cabinet left side", new Vector3(-.47f, .75f, 0f), new Vector3(.06f, 1.5f, .5f), cabinet);
                Box(root.transform, "Cabinet right side", new Vector3(.47f, .75f, 0f), new Vector3(.06f, 1.5f, .5f), cabinet);
                Box(root.transform, "Cabinet top", new Vector3(0f, 1.47f, 0f), new Vector3(1f, .06f, .5f), cabinet);
                Box(root.transform, "Cabinet base", new Vector3(0f, .03f, 0f), new Vector3(1f, .06f, .5f), cabinet);

                for (int shelf = 1; shelf <= 2; shelf++)
                    Box(root.transform, "Shelf " + shelf, new Vector3(0f, shelf * .46f, -.02f), new Vector3(.9f, .05f, .42f), cabinet);

                for (int shelf = 0; shelf < 3; shelf++)
                    for (int roll = 0; roll < 3; roll++)
                        CreatePaperRoll(root.transform, "Paper roll " + (shelf + 1) + "," + (roll + 1),
                            new Vector3(-.28f + roll * .28f, .23f + shelf * .46f, -.12f), paper);

                var display = new GameObject("InventoryDisplay_Reserved");
                display.transform.SetParent(root.transform, false);
                display.transform.localPosition = new Vector3(0f, 1.23f, -.265f);
                Box(display.transform, "Display backing", Vector3.zero, new Vector3(.44f, .18f, .02f), cabinet);

                var interaction = new GameObject("InteractionPoint_Reserved");
                interaction.transform.SetParent(root.transform, false);
                interaction.transform.localPosition = new Vector3(0f, .03f, -.42f);
                var interactionVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                interactionVisual.name = "Interaction marker visual";
                interactionVisual.transform.SetParent(interaction.transform, false);
                interactionVisual.transform.localScale = new Vector3(.16f, .012f, .16f);
                interactionVisual.GetComponent<Renderer>().sharedMaterial = marker;

                return SavePrefab(root, PaperSupplyPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject EnsureToolsSupply(Material cabinet, Material metal, Material handle, Material marker, GameObject plungerPrefab)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ToolsSupplyPath);
            if (existing != null) return existing;

            var root = new GameObject("Supply_Tools");
            try
            {
                // The rack outer dimensions follow the team table: 1.5m wide, .5m deep, 1.5m tall.
                Box(root.transform, "Rack back", new Vector3(0f, .75f, .22f), new Vector3(1.5f, 1.5f, .06f), cabinet);
                Box(root.transform, "Rack left side", new Vector3(-.72f, .75f, 0f), new Vector3(.06f, 1.5f, .5f), cabinet);
                Box(root.transform, "Rack right side", new Vector3(.72f, .75f, 0f), new Vector3(.06f, 1.5f, .5f), cabinet);
                Box(root.transform, "Rack top", new Vector3(0f, 1.47f, 0f), new Vector3(1.5f, .06f, .5f), cabinet);
                Box(root.transform, "Rack base", new Vector3(0f, .03f, 0f), new Vector3(1.5f, .06f, .5f), cabinet);

                var plunger = Place(plungerPrefab, root.transform, "Plunger visual", new Vector3(-.42f, .30f, -.12f), 0f);
                ScaleHierarchyToBounds(plunger, new Vector3(.15f, .6f, .15f));
                CreateWrenchPlaceholder(root.transform, new Vector3(.22f, .83f, -.18f), metal, handle);

                var interaction = new GameObject("InteractionPoint_Reserved");
                interaction.transform.SetParent(root.transform, false);
                interaction.transform.localPosition = new Vector3(0f, .03f, -.42f);
                var interactionVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                interactionVisual.name = "Interaction marker visual";
                interactionVisual.transform.SetParent(interaction.transform, false);
                interactionVisual.transform.localScale = new Vector3(.16f, .012f, .16f);
                interactionVisual.GetComponent<Renderer>().sharedMaterial = marker;

                return SavePrefab(root, ToolsSupplyPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject EnsureCleaningSupply(Material cabinet, Material handle, Material mopHead, Material bucket,
            Material cleaner, Material marker)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CleaningSupplyPath);
            if (existing != null) return existing;

            var root = new GameObject("Supply_Cleaning");
            try
            {
                // Rack outer dimensions follow the team table: 1.5m wide, 1m deep, 1.5m tall.
                Box(root.transform, "Rack back", new Vector3(0f, .75f, .47f), new Vector3(1.5f, 1.5f, .06f), cabinet);
                Box(root.transform, "Rack left side", new Vector3(-.72f, .75f, 0f), new Vector3(.06f, 1.5f, 1f), cabinet);
                Box(root.transform, "Rack right side", new Vector3(.72f, .75f, 0f), new Vector3(.06f, 1.5f, 1f), cabinet);
                Box(root.transform, "Rack base", new Vector3(0f, .03f, 0f), new Vector3(1.5f, .06f, 1f), cabinet);

                CreateMop(root.transform, new Vector3(-.38f, 0f, .16f), handle, mopHead);
                CreateBucket(root.transform, new Vector3(.24f, .175f, .06f), bucket);
                CreateCleanerBottle(root.transform, new Vector3(.45f, .19f, .27f), cleaner);

                var interaction = new GameObject("InteractionPoint_Reserved");
                interaction.transform.SetParent(root.transform, false);
                interaction.transform.localPosition = new Vector3(0f, .03f, -.62f);
                var interactionVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                interactionVisual.name = "Interaction marker visual";
                interactionVisual.transform.SetParent(interaction.transform, false);
                interactionVisual.transform.localScale = new Vector3(.16f, .012f, .16f);
                interactionVisual.GetComponent<Renderer>().sharedMaterial = marker;

                return SavePrefab(root, CleaningSupplyPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject EnsureDropStation(Material counter, Material surface, Material marker)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(DropStationPath);
            if (existing != null) return existing;

            var root = new GameObject("Drop_Station");
            try
            {
                // Overall dimensions are 1.5m wide, .5m deep, and .8m high.
                Box(root.transform, "Counter top", new Vector3(0f, .76f, 0f), new Vector3(1.5f, .08f, .5f), counter);
                Box(root.transform, "Drop surface visual", new Vector3(0f, .805f, 0f), new Vector3(1.22f, .012f, .34f), surface);
                foreach (float x in new[] { -.62f, .62f })
                    Box(root.transform, "Counter leg " + (x < 0 ? "left" : "right"), new Vector3(x, .36f, 0f), new Vector3(.10f, .72f, .36f), counter);
                Box(root.transform, "Lower brace", new Vector3(0f, .18f, .16f), new Vector3(1.3f, .10f, .08f), counter);

                var dropSurface = new GameObject("DropSurface_Reserved");
                dropSurface.transform.SetParent(root.transform, false);
                dropSurface.transform.localPosition = new Vector3(0f, .82f, 0f);
                dropSurface.transform.localScale = new Vector3(1.22f, .05f, .34f);

                var interaction = new GameObject("InteractionPoint_Reserved");
                interaction.transform.SetParent(root.transform, false);
                interaction.transform.localPosition = new Vector3(0f, .03f, -.42f);
                var interactionVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                interactionVisual.name = "Interaction marker visual";
                interactionVisual.transform.SetParent(interaction.transform, false);
                interactionVisual.transform.localScale = new Vector3(.16f, .012f, .16f);
                interactionVisual.GetComponent<Renderer>().sharedMaterial = marker;

                return SavePrefab(root, DropStationPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject EnsurePipeLeak(Material pipe, Material water, Material marker)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PipeLeakPath);
            if (existing != null) return existing;

            var root = new GameObject("Pipe_Leak");
            try
            {
                // The pipe body is 1m wide, .25m deep, and .25m high; place the prefab on a wall in a map.
                var pipeBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pipeBody.name = "Leaking pipe body";
                pipeBody.transform.SetParent(root.transform, false);
                pipeBody.transform.localPosition = new Vector3(0f, .52f, 0f);
                pipeBody.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                pipeBody.transform.localScale = new Vector3(.125f, .5f, .125f);
                pipeBody.GetComponent<Renderer>().sharedMaterial = pipe;

                var waterDrop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                waterDrop.name = "Water leak visual";
                waterDrop.transform.SetParent(root.transform, false);
                waterDrop.transform.localPosition = new Vector3(.12f, .30f, .05f);
                waterDrop.transform.localScale = new Vector3(.07f, .15f, .07f);
                waterDrop.GetComponent<Renderer>().sharedMaterial = water;
                CreatePuddle(root.transform, "Water puddle visual", new Vector3(.12f, .02f, .12f), new Vector3(.32f, .012f, .25f), water);

                CreateReservedMarker(root.transform, "RepairPoint_Reserved", new Vector3(-.35f, .10f, -.18f), marker, "Repair marker visual");
                return SavePrefab(root, PipeLeakPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject EnsureValveZone(Material pipe, Material valve, Material marker)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ValveZonePath);
            if (existing != null) return existing;

            var root = new GameObject("Valve_Zone");
            try
            {
                // The wall-mounted housing fits the specified .5m x .3m x .5m envelope.
                Box(root.transform, "Valve housing", new Vector3(0f, .25f, 0f), new Vector3(.5f, .5f, .3f), pipe);
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = "Valve wheel visual";
                wheel.transform.SetParent(root.transform, false);
                wheel.transform.localPosition = new Vector3(0f, .25f, -.19f);
                wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                wheel.transform.localScale = new Vector3(.18f, .04f, .18f);
                wheel.GetComponent<Renderer>().sharedMaterial = valve;

                var controlled = new GameObject("ControlledFacilities_Reserved");
                controlled.transform.SetParent(root.transform, false);
                CreateReservedMarker(root.transform, "ValveControl_Reserved", new Vector3(.34f, .03f, -.25f), marker, "Valve control marker visual");
                return SavePrefab(root, ValveZonePath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject EnsureVentUnit(Material vent, Material marker)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(VentUnitPath);
            if (existing != null) return existing;

            var root = new GameObject("Vent_Unit");
            try
            {
                // The grille's outer body follows the specified 1m x .3m x .5m envelope.
                Box(root.transform, "Vent housing", new Vector3(0f, .25f, 0f), new Vector3(1f, .5f, .3f), vent);
                for (int index = 0; index < 5; index++)
                    Box(root.transform, "Vent slat " + (index + 1), new Vector3(-.32f + index * .16f, .25f, -.17f), new Vector3(.10f, .34f, .04f), vent);

                var effectArea = new GameObject("VentEffectArea_Reserved");
                effectArea.transform.SetParent(root.transform, false);
                effectArea.transform.localScale = new Vector3(1f, .5f, 1f);
                CreateReservedMarker(root.transform, "VentControl_Reserved", new Vector3(.58f, .03f, -.25f), marker, "Vent control marker visual");
                return SavePrefab(root, VentUnitPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void EnsurePreviewScene(GameObject floorTile, GameObject wallTile, GameObject wallSegment, GameObject doorFrame)
        {
            if (File.Exists(PreviewScenePath)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Map Module Kit Foundation Preview");

            var floorPreview = new GameObject("0.5m floor tile grid");
            floorPreview.transform.SetParent(root.transform, false);
            for (int x = -4; x <= 4; x++)
                for (int z = -3; z <= 3; z++)
                    Place(floorTile, floorPreview.transform, "Tile " + x + "," + z, new Vector3(x * .5f, 0f, z * .5f), 0f);

            var structurePreview = new GameObject("Structure examples");
            structurePreview.transform.SetParent(root.transform, false);
            Place(wallTile, structurePreview.transform, "WallTile_0_5", new Vector3(-3f, 0f, 0f), 0f);
            Place(wallSegment, structurePreview.transform, "WallSegment_0_5x3", new Vector3(3f, 0f, 0f), 0f);
            Place(doorFrame, structurePreview.transform, "Door_Frame", new Vector3(0f, 0f, 2.2f), 0f);

            var light = new GameObject("Preview Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            light.transform.SetParent(root.transform, true);

            var cameraObject = new GameObject("Preview Camera");
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.fieldOfView = 60f;
            camera.transform.position = new Vector3(6f, 5.5f, -7f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(-.55f, -.4f, .65f));

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, PreviewScenePath))
                throw new IOException("Could not save " + PreviewScenePath);
        }

        private static void EnsureCorridorPreview(GameObject straight, GameObject corner)
        {
            if (!File.Exists(PreviewScenePath))
                throw new FileNotFoundException("Build the module foundation before adding corridor previews.", PreviewScenePath);

            var scene = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
            if (GameObject.Find("Corridor module previews") != null) return;

            var root = new GameObject("Corridor module previews");
            Place(straight, root.transform, "Corridor_Straight", new Vector3(-4f, 0f, -4.5f), 0f);
            Place(corner, root.transform, "Corridor_Corner", new Vector3(4.5f, 0f, -4.5f), 0f);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + PreviewScenePath);
        }

        private static void EnsureStallConnectorPreview(GameObject connector)
        {
            if (!File.Exists(PreviewScenePath))
                throw new FileNotFoundException("Build the module foundation before adding connector previews.", PreviewScenePath);

            var scene = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
            if (GameObject.Find("Stall connector module preview") != null) return;

            var root = new GameObject("Stall connector module preview");
            Place(connector, root.transform, "Corridor_StallConnector", new Vector3(-8f, 0f, -4.5f), 0f);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + PreviewScenePath);
        }

        private static void ReplacePrototypeDoorwayOverrides(GameObject connector)
        {
            if (!File.Exists(PrototypeScenePath)) return;

            var scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
            var root = GameObject.Find("Map Layout Prototype - visual assembly test");
            if (root == null)
                throw new InvalidOperationException("MapLayoutPrototype root is missing.");
            var routes = root.transform.Find("Connected route skeleton");
            if (routes == null)
                throw new InvalidOperationException("MapLayoutPrototype route skeleton is missing.");

            ReplacePrototypeCorridor(routes, connector, "Corridor north", new Vector3(0f, 0f, 4.5f), 0f);
            ReplacePrototypeCorridor(routes, connector, "Corridor south", new Vector3(0f, 0f, -4.5f), 180f);
            ReplacePrototypeCorridor(routes, connector, "Corridor east", new Vector3(4.5f, 0f, 0f), -90f);

            DestroyPrototypeObjectIfPresent(root.transform, "North doorway frame");
            DestroyPrototypeObjectIfPresent(root.transform, "South doorway frame");
            DestroyPrototypeObjectIfPresent(root.transform, "East doorway frame");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + PrototypeScenePath);
        }

        private static void ReplacePrototypeCorridor(Transform routes, GameObject connector, string name, Vector3 position, float rotation)
        {
            var existing = routes.Find(name);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            Place(connector, routes, name, position, rotation);
        }

        private static void DestroyPrototypeObjectIfPresent(Transform root, string name)
        {
            var target = FindDescendantByName(root, name);
            if (target != null)
                UnityEngine.Object.DestroyImmediate(target.gameObject);
        }

        private static void EnsureJunctionPreview(GameObject junction)
        {
            if (!File.Exists(PreviewScenePath))
                throw new FileNotFoundException("Build the module foundation before adding junction previews.", PreviewScenePath);

            var scene = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
            if (GameObject.Find("Junction module preview") != null) return;

            var root = new GameObject("Junction module preview");
            Place(junction, root.transform, "Junction_Cross", new Vector3(0f, 0f, -11f), 0f);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + PreviewScenePath);
        }

        private static void EnsureNormalStallPreview(GameObject stall)
        {
            if (!File.Exists(PreviewScenePath))
                throw new FileNotFoundException("Build the module foundation before adding stall previews.", PreviewScenePath);

            var scene = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
            if (GameObject.Find("Stall module preview") != null) return;

            var root = new GameObject("Stall module preview");
            Place(stall, root.transform, "Stall_Normal", new Vector3(-5.5f, 0f, -11f), 0f);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + PreviewScenePath);
        }

        private static void EnsureDirtyStallPreview(GameObject stall)
        {
            if (!File.Exists(PreviewScenePath))
                throw new FileNotFoundException("Build the module foundation before adding stall previews.", PreviewScenePath);

            var scene = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
            if (GameObject.Find("Dirty stall module preview") != null) return;

            var root = new GameObject("Dirty stall module preview");
            Place(stall, root.transform, "Stall_Dirty", new Vector3(-9f, 0f, -11f), 0f);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + PreviewScenePath);
        }

        private static void EnsureBrokenStallPreview(GameObject stall)
        {
            if (!File.Exists(PreviewScenePath))
                throw new FileNotFoundException("Build the module foundation before adding stall previews.", PreviewScenePath);

            var scene = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
            if (GameObject.Find("Broken stall module preview") != null) return;

            var root = new GameObject("Broken stall module preview");
            Place(stall, root.transform, "Stall_Broken", new Vector3(-12f, 0f, -11f), 0f);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + PreviewScenePath);
        }

        private static void EnsureEmergencyReliefPreview(GameObject facility)
        {
            if (!File.Exists(PreviewScenePath))
                throw new FileNotFoundException("Build the module foundation before adding facility previews.", PreviewScenePath);

            var scene = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
            if (GameObject.Find("Emergency relief module preview") != null) return;

            var root = new GameObject("Emergency relief module preview");
            Place(facility, root.transform, "Relief_Emergency", new Vector3(-15f, 0f, -11f), 0f);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + PreviewScenePath);
        }

        private static void EnsureDirtyShortcutPreview(GameObject shortcut, GameObject dirtyHazard)
        {
            if (!File.Exists(PreviewScenePath))
                throw new FileNotFoundException("Build the module foundation before adding corridor previews.", PreviewScenePath);

            var scene = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
            if (GameObject.Find("Dirty shortcut module preview") != null) return;

            var root = new GameObject("Dirty shortcut module preview");
            Place(shortcut, root.transform, "Shortcut", new Vector3(-4f, 0f, -17f), 0f);
            Place(dirtyHazard, root.transform, "Hazard_DirtyArea", new Vector3(1.5f, 0f, -17f), 0f);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + PreviewScenePath);
        }

        private static void EnsurePaperSupplyPreview(GameObject supply)
        {
            if (!File.Exists(PreviewScenePath))
                throw new FileNotFoundException("Build the module foundation before adding supply previews.", PreviewScenePath);

            var scene = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
            if (GameObject.Find("Paper supply module preview") != null) return;

            var root = new GameObject("Paper supply module preview");
            Place(supply, root.transform, "Supply_Paper", new Vector3(5f, 0f, -11f), 0f);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + PreviewScenePath);
        }

        private static void EnsureToolsSupplyPreview(GameObject supply)
        {
            if (!File.Exists(PreviewScenePath))
                throw new FileNotFoundException("Build the module foundation before adding supply previews.", PreviewScenePath);

            var scene = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
            if (GameObject.Find("Tools supply module preview") != null) return;

            var root = new GameObject("Tools supply module preview");
            Place(supply, root.transform, "Supply_Tools", new Vector3(8f, 0f, -11f), 0f);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + PreviewScenePath);
        }

        private static void EnsureCleaningSupplyPreview(GameObject supply)
        {
            if (!File.Exists(PreviewScenePath))
                throw new FileNotFoundException("Build the module foundation before adding supply previews.", PreviewScenePath);

            var scene = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
            if (GameObject.Find("Cleaning supply module preview") != null) return;

            var root = new GameObject("Cleaning supply module preview");
            Place(supply, root.transform, "Supply_Cleaning", new Vector3(11f, 0f, -11f), 0f);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + PreviewScenePath);
        }

        private static void EnsureDropStationPreview(GameObject station)
        {
            if (!File.Exists(PreviewScenePath))
                throw new FileNotFoundException("Build the module foundation before adding supply previews.", PreviewScenePath);

            var scene = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
            if (GameObject.Find("Drop station module preview") != null) return;

            var root = new GameObject("Drop station module preview");
            Place(station, root.transform, "Drop_Station", new Vector3(5f, 0f, -17f), 0f);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + PreviewScenePath);
        }

        private static void EnsurePublicSystemsPreview(GameObject pipeLeak, GameObject valveZone, GameObject ventUnit)
        {
            if (!File.Exists(PreviewScenePath))
                throw new FileNotFoundException("Build the module foundation before adding system previews.", PreviewScenePath);

            var scene = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
            if (GameObject.Find("Public systems module preview") != null) return;

            var root = new GameObject("Public systems module preview");
            Place(pipeLeak, root.transform, "Pipe_Leak", new Vector3(8f, 0f, -17f), 0f);
            Place(valveZone, root.transform, "Valve_Zone", new Vector3(10f, 0f, -17f), 0f);
            Place(ventUnit, root.transform, "Vent_Unit", new Vector3(12f, 0f, -17f), 0f);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + PreviewScenePath);
        }

        private static void BuildFloorGrid(Transform parent, GameObject floorTile, int columns, int rows, Vector2 dimensions)
        {
            float startX = -dimensions.x * .5f + .25f;
            float startZ = -dimensions.y * .5f + .25f;
            for (int column = 0; column < columns; column++)
                for (int row = 0; row < rows; row++)
                    Place(floorTile, parent, "Floor " + column + "," + row,
                        new Vector3(startX + column * .5f, 0f, startZ + row * .5f), 0f);
        }

        private static Material EnsureMaterial(string name, Color color, float smoothness, float metallic)
        {
            var path = MaterialsFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit shader is unavailable.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (saved == null) throw new IOException("Could not save " + path);
            return saved;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        private static GameObject Place(GameObject prefab, Transform parent, string name, Vector3 position, float yRotation)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);
            return instance;
        }

        private static void PlaceHalfWall(Transform parent, GameObject wallSegment, string name, Vector3 position, float yRotation)
        {
            PlaceFractionWall(parent, wallSegment, name, position, yRotation, .5f);
        }

        private static void PlaceFractionWall(Transform parent, GameObject wallSegment, string name, Vector3 position, float yRotation, float widthFraction)
        {
            var extension = Place(wallSegment, parent, name, position, yRotation);
            extension.transform.localScale = new Vector3(widthFraction, 1f, 1f);
        }

        private static void ScaleHierarchyToBounds(GameObject target, Vector3 desiredSize)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);

            var actual = bounds.size;
            if (actual.x < .0001f || actual.y < .0001f || actual.z < .0001f) return;
            target.transform.localScale = Vector3.Scale(target.transform.localScale,
                new Vector3(desiredSize.x / actual.x, desiredSize.y / actual.y, desiredSize.z / actual.z));
        }

        private static void CreatePuddle(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var puddle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            puddle.name = name;
            puddle.transform.SetParent(parent, false);
            puddle.transform.localPosition = position;
            puddle.transform.localScale = scale;
            puddle.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreatePaperRoll(Transform parent, string name, Vector3 position, Material paper)
        {
            var roll = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            roll.name = name;
            roll.transform.SetParent(parent, false);
            roll.transform.localPosition = position;
            roll.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // 0.12m diameter and 0.10m axial depth, with the axis oriented along the cabinet depth.
            roll.transform.localScale = new Vector3(.06f, .05f, .06f);
            roll.GetComponent<Renderer>().sharedMaterial = paper;
        }

        private static void CreateWrenchPlaceholder(Transform parent, Vector3 position, Material metal, Material handle)
        {
            var wrench = new GameObject("Wrench visual - placeholder");
            wrench.transform.SetParent(parent, false);
            wrench.transform.localPosition = position;
            wrench.transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            Box(wrench.transform, "Handle", new Vector3(0f, -.13f, 0f), new Vector3(.07f, .32f, .045f), handle);
            Box(wrench.transform, "Head", new Vector3(0f, .07f, 0f), new Vector3(.18f, .08f, .05f), metal);
            Box(wrench.transform, "Open jaw left", new Vector3(-.07f, .14f, 0f), new Vector3(.05f, .10f, .05f), metal);
            Box(wrench.transform, "Open jaw right", new Vector3(.07f, .14f, 0f), new Vector3(.05f, .10f, .05f), metal);
        }

        private static void CreateMop(Transform parent, Vector3 position, Material handle, Material mopHead)
        {
            var mop = new GameObject("Mop visual");
            mop.transform.SetParent(parent, false);
            mop.transform.localPosition = position;
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "Mop shaft";
            shaft.transform.SetParent(mop.transform, false);
            shaft.transform.localPosition = new Vector3(0f, .65f, 0f);
            shaft.transform.localScale = new Vector3(.025f, .55f, .025f);
            shaft.GetComponent<Renderer>().sharedMaterial = handle;
            Box(mop.transform, "Mop head", new Vector3(0f, .075f, 0f), new Vector3(.35f, .15f, .15f), mopHead);
        }

        private static void CreateBucket(Transform parent, Vector3 position, Material material)
        {
            var bucket = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bucket.name = "Water bucket";
            bucket.transform.SetParent(parent, false);
            bucket.transform.localPosition = position;
            bucket.transform.localScale = new Vector3(.15f, .175f, .15f);
            bucket.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateCleanerBottle(Transform parent, Vector3 position, Material material)
        {
            var bottle = new GameObject("Cleaner bottle");
            bottle.transform.SetParent(parent, false);
            bottle.transform.localPosition = position;
            Box(bottle.transform, "Bottle body", Vector3.zero, new Vector3(.16f, .30f, .12f), material);
            Box(bottle.transform, "Bottle neck", new Vector3(0f, .19f, 0f), new Vector3(.07f, .10f, .07f), material);
        }

        private static void CreateReservedMarker(Transform parent, string name, Vector3 position, Material material, string visualName)
        {
            var reservedPoint = new GameObject(name);
            reservedPoint.transform.SetParent(parent, false);
            reservedPoint.transform.localPosition = position;
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = visualName;
            marker.transform.SetParent(reservedPoint.transform, false);
            marker.transform.localScale = new Vector3(.12f, .012f, .12f);
            marker.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static GameObject LoadRequiredPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                throw new FileNotFoundException("Required map module prefab is missing.", path);
            return prefab;
        }

        private static Transform FindChildByName(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null)
                throw new InvalidOperationException("Expected prototype object is missing: " + name);
            return child;
        }

        private static void RemoveWallSegments(Transform corridor, string prefix, params int[] segmentNumbers)
        {
            foreach (var number in segmentNumbers)
            {
                var segment = corridor.Find(prefix + number);
                if (segment == null)
                    throw new InvalidOperationException("Expected corridor wall segment is missing: " + prefix + number);
                UnityEngine.Object.DestroyImmediate(segment.gameObject);
            }
        }

        private static void MoveCorridorOpening(Transform corridor, string prefix, GameObject wallSegment,
            int oldFirst, int oldSecond, int sharedSegment, int newFirst, int newSecond)
        {
            RestoreWallSegmentIfMissing(corridor, prefix, oldFirst, wallSegment);
            RestoreWallSegmentIfMissing(corridor, prefix, oldSecond, wallSegment);
            RemoveWallSegmentsIfPresent(corridor, prefix, sharedSegment, newFirst, newSecond);
        }

        private static void SetCorridorOpening(Transform corridor, string prefix, GameObject wallSegment, params int[] openingSegments)
        {
            for (int segmentNumber = 1; segmentNumber <= 8; segmentNumber++)
            {
                if (Array.IndexOf(openingSegments, segmentNumber) >= 0)
                    RemoveWallSegmentsIfPresent(corridor, prefix, segmentNumber);
                else
                    RestoreWallSegmentIfMissing(corridor, prefix, segmentNumber, wallSegment);
            }
        }

        private static void RemoveWallSegmentsIfPresent(Transform corridor, string prefix, params int[] segmentNumbers)
        {
            foreach (var number in segmentNumbers)
            {
                var segment = corridor.Find(prefix + number);
                if (segment != null)
                    UnityEngine.Object.DestroyImmediate(segment.gameObject);
            }
        }

        private static void RestoreWallSegmentIfMissing(Transform corridor, string prefix, int segmentNumber, GameObject wallSegment)
        {
            if (corridor.Find(prefix + segmentNumber) != null) return;

            bool isRight = prefix.StartsWith("Right", StringComparison.Ordinal);
            float x = isRight ? 1.15f : -1.15f;
            float rotation = isRight ? -90f : 90f;
            float z = -1.75f + (segmentNumber - 1) * .5f;
            Place(wallSegment, corridor, prefix + segmentNumber, new Vector3(x, 0f, z), rotation);
        }

        private static void UpdatePrototypePosition(Transform root, string name, Vector3 position)
        {
            var target = FindDescendantByName(root, name);
            if (target == null)
                throw new InvalidOperationException("Expected prototype object is missing: " + name);
            target.localPosition = position;
        }

        private static void UpdatePrototypeTransform(Transform root, string name, Vector3 position, Quaternion rotation)
        {
            var target = FindDescendantByName(root, name);
            if (target == null)
                throw new InvalidOperationException("Expected prototype object is missing: " + name);
            target.localPosition = position;
            target.localRotation = rotation;
        }

        private static Transform FindDescendantByName(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var descendant = FindDescendantByName(child, name);
                if (descendant != null) return descendant;
            }
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? throw new InvalidOperationException("Folder has no parent.");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
