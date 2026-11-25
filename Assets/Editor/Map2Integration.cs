using System;
using System.Linq;
using NatureCalls.Camera;
using NatureCalls.Facility;
using NatureCalls.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NatureCalls.Editor
{
    public static class Map2Integration
    {
        public const string ScenePath = "Assets/Scenes/Map2.unity";

        [MenuItem("NatureCalls/Connect Map2 To Lobby")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var world = Find<Map.Playground>(scene);
            if (world == null)
            {
                var templateScene = EditorSceneManager.OpenScene(Playground.ScenePath, OpenSceneMode.Additive);
                var template = Find<Map.Playground>(templateScene);
                var source = new GameObject("Local camera rig");
                SceneManager.MoveGameObjectToScene(source, templateScene);
                var camera = template.Camera;
                foreach (var root in new[] { camera.transform.root, camera.Eye.root, camera.Shoulder.root,
                    camera.FirstPerson.transform.root, camera.ThirdPerson.transform.root }.Distinct())
                    root.SetParent(source.transform, true);
                SceneManager.SetActiveScene(scene);
                var rig = Object.Instantiate(source);
                rig.name = source.name;
                EditorSceneManager.CloseScene(templateScene, true);

                world = new GameObject("Playground").AddComponent<Map.Playground>();
                world.PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerAppearanceSetup.PlayerPath);
                world.Camera = rig.GetComponentInChildren<CameraController>(true);
            }
            world.StartOffline = true;
            using (var serializedWorld = new SerializedObject(world))
            {
                serializedWorld.FindProperty("startOfflineRoundOnDirectPlay").boolValue = true;
                serializedWorld.ApplyModifiedPropertiesWithoutUndo();
            }
            world.Spawns = FindRoot(scene, "Street Restroom").Find("Four Spawn Markers").Cast<Transform>()
                .OrderBy(spawn => spawn.name, StringComparer.Ordinal).ToArray();
            if (world.Spawns.Length != 4) throw new InvalidOperationException("Map2 expects four spawn markers.");
            foreach (var camera in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UnityEngine.Camera>(true)))
                if (camera != world.Camera.Output) camera.enabled = false;

            foreach (Transform stall in FindRoot(scene, "Street Restroom").Find("Three Offset Stalls")) ConfigureStall(stall);
            var toolbox = Descendant(FindRoot(scene, "Street Restroom"), "Toolbox");
            var mop = Descendant(toolbox.parent, "Mop");
            var box = GetOrAdd<Toolbox>(toolbox.gameObject);
            box.Mop = ConfigureItem<Mop>(mop, .5f);
            if (toolbox.GetComponent<Collider>() == null) FitBox(toolbox);
            var emergency = ConfigureItem<PaperRoll>(Descendant(toolbox.parent, "Emergency roll"), .05f);
            emergency.PickupHeightOffset = .65f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(emergency);
            var cabinet = Descendant(toolbox.parent, "Backup paper cabinet");
            cabinet.SetPositionAndRotation(new Vector3(6, 0, .46f), Quaternion.Euler(0, 180, 0));
            var counter = BoundsIn(cabinet);
            var top = cabinet.TransformPoint(new Vector3(counter.center.x, counter.max.y, counter.center.z));
            var rollBounds = BoundsIn(emergency.transform);
            emergency.transform.position = top + Vector3.up * (.025f - rollBounds.min.y * emergency.transform.lossyScale.y);
            PrefabUtility.RecordPrefabInstancePropertyModifications(cabinet);
            PrefabUtility.RecordPrefabInstancePropertyModifications(emergency.transform);
            var shafts = FindRoot(scene, "Shafts A and B");
            ConfigureShaft(world.transform, shafts.Find("Shaft A"), false, Vector3.forward);
            ConfigureShaft(world.transform, shafts.Find("Shaft B"), true, Vector3.back);
            ConfigureSewerStairs(FindRoot(scene, "Sewer and Maintenance").Find("Platforms and Main Channel"));
            ConfigureActivities(scene, world, toolbox.parent, emergency);
            LobbyFlowBuilder.AddMatchHud();

            var catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>(LobbyFlowBuilder.MapCatalogPath);
            var entries = Enumerable.Range(0, catalog.Count).Select(i => catalog[i]).ToList();
            if (entries.All(e => e.ScenePath != ScenePath))
            {
                entries.Add(new MapCatalog.Entry { DisplayName = "Map2 · 下水道", ScenePath = ScenePath });
                catalog.SetMaps(entries.ToArray());
                EditorUtility.SetDirty(catalog);
            }
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath)
                .Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Map2.");
            AssetDatabase.SaveAssets();
            Debug.Log("MAP2_LOBBY_INTEGRATION_OK");
        }

        private static void ConfigureStall(Transform stall)
        {
            var door = stall.Find("Door shown open");
            var existing = stall.GetComponent<StallDoor>();
            if (existing != null) return;
            door.localRotation = Quaternion.identity;
            var blocker = FitBox(door);
            var visual = BoundsIn(door);
            blocker.size = new Vector3(visual.size.x, visual.size.y, .09f);
            blocker.center = new Vector3(visual.center.x, visual.center.y, 0);
            var interactive = stall.gameObject.AddComponent<StallDoor>();
            interactive.DoorLeaf = interactive.DoorColliderPivot = door;
            interactive.OpenAngle = -95;

            var bowl = stall.Find("Bowl");
            var toilet = GetOrAdd<Toilet>(bowl.gameObject);
            toilet.Interior = Descendant(bowl, "Bowl Center");
            toilet.Anchor = new GameObject("Posture anchor").transform;
            toilet.Anchor.SetParent(bowl, false);
            toilet.Anchor.SetPositionAndRotation(toilet.TargetCenter - stall.forward * .65f
                + Vector3.up * (stall.position.y + .15f - toilet.TargetCenter.y), stall.rotation);
        }

        private static void ConfigureShaft(Transform world, Transform shaft, bool ascending, Vector3 direction)
        {
            var node = world.Find(shaft.name + " access");
            if (node == null)
            {
                node = new GameObject(shaft.name + " access").transform;
                node.SetParent(world, false);
            }
            var access = GetOrAdd<SewerAccess>(node.gameObject);
            access.Ascending = ascending;
            access.Top = shaft.Find("Shaft top marker");
            access.Landing = shaft.Find("Shaft landing marker");
            access.Lid = Descendant(shaft, "Cover Hinge");
            var surface = Descendant(access.Lid, "Cover");
            access.ClosedCover = FitBox(surface);
            access.ApproachDirection = direction;
            access.ApproachDistance = 1.18f;
        }

        private static void ConfigureSewerStairs(Transform platforms)
        {
            var stairs = platforms.Find("Stairs down to inspection");
            foreach (var tread in stairs.GetComponentsInChildren<MeshCollider>(true))
            {
                tread.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(tread);
            }
            // The decorative tread noses leave too little continuous floor for the shared capsule.
            // A walkable ramp follows the top line of the stairs and overlaps both landings.
            var name = "Inspection stairs walkable slope";
            var slope = platforms.Find(name);
            if (slope == null)
            {
                slope = new GameObject(name).transform;
                slope.SetParent(platforms, false);
            }
            var high = new Vector3(7.12f, -3.20f, 7.51f);
            var low = new Vector3(8.48f, -3.80f, 7.51f);
            var tangent = low - high;
            slope.rotation = Quaternion.FromToRotation(Vector3.right, tangent.normalized);
            slope.position = (high + low) * .5f - slope.up * .04f;
            GetOrAdd<BoxCollider>(slope.gameObject).size = new Vector3(tangent.magnitude, .08f, 1.43f);
        }

        private static void ConfigureActivities(Scene scene, Map.Playground world, Transform supplies, PaperRoll paper)
        {
            var activities = GetOrAdd<Map2Interactions>(world.gameObject);
            activities.EmergencyPaper = paper;
            var room = FindRoot(scene, "Street Restroom");
            var sewer = FindRoot(scene, "Sewer and Maintenance");
            var machine = Descendant(supplies, "Paper vending machine");
            activities.VendingMachine = Descendant(machine, "Paper Output");
            activities.Coins = room.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("Coin at ", StringComparison.Ordinal))
                .OrderBy(t => t.name, StringComparer.Ordinal).ToArray();
            activities.VendingStock = activities.Coins.Select((_, index) =>
            {
                var name = "Vending paper " + (index + 1);
                var stock = supplies.Find(name);
                if (stock != null) return stock.GetComponent<PaperRoll>();
                var copy = Object.Instantiate(paper, supplies);
                copy.name = name;
                copy.transform.position = activities.VendingMachine.position;
                return copy;
            }).ToArray();
            activities.UpstreamWindow = Descendant(sewer, "A upstream window");
            activities.Valve = Descendant(sewer, "A isolating valve");
            activities.InspectionPort = Descendant(sewer, "A inspection cover");
            activities.DownstreamWindow = Descendant(sewer, "A downstream window");
            activities.UpstreamWater = Descendant(activities.UpstreamWindow, "Water Height Pivot");
            activities.DownstreamWater = Descendant(activities.DownstreamWindow, "Water Height Pivot");
            activities.Handwheel = Descendant(activities.Valve, "Handwheel Pivot");
            activities.InspectionLid = Descendant(activities.InspectionPort, "Inspection Cover Hinge");
            activities.AugerCrank = Descendant(Descendant(sewer, "Drain auger"), "Crank Pivot");
            activities.WetZones = room.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "Wet shortcut patch").Select(patch =>
                {
                    var bounds = patch.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) =>
                    {
                        a.Encapsulate(b);
                        return a;
                    });
                    var name = "Wet zone " + patch.GetSiblingIndex();
                    var zone = world.transform.Find(name);
                    if (zone == null)
                    {
                        zone = new GameObject(name).transform;
                        zone.SetParent(world.transform, false);
                    }
                    zone.position = bounds.center + Vector3.up * .08f;
                    var collider = GetOrAdd<BoxCollider>(zone.gameObject);
                    collider.size = new Vector3(bounds.size.x, .24f, bounds.size.z);
                    collider.isTrigger = true;
                    return collider;
                }).ToArray();
        }

        private static T ConfigureItem<T>(Transform root, float mass) where T : Item
        {
            var item = GetOrAdd<T>(root.gameObject);
            foreach (var collider in root.GetComponentsInChildren<MeshCollider>(true))
                if (!collider.convex) collider.convex = true;
            if (root.GetComponentInChildren<Collider>(true) == null) FitBox(root);
            var rigidbody = GetOrAdd<Rigidbody>(root.gameObject);
            rigidbody.mass = mass;
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            return item;
        }

        private static BoxCollider FitBox(Transform root)
        {
            var bounds = BoundsIn(root);
            var box = GetOrAdd<BoxCollider>(root.gameObject);
            box.center = bounds.center;
            box.size = bounds.size;
            return box;
        }

        private static Bounds BoundsIn(Transform root)
        {
            var corners = root.GetComponentsInChildren<Renderer>(true).SelectMany(renderer =>
            {
                var bounds = renderer.localBounds;
                return Enumerable.Range(0, 8).Select(i => root.InverseTransformPoint(renderer.transform.TransformPoint(new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z))));
            }).ToArray();
            var bounds = new Bounds(corners[0], Vector3.zero);
            foreach (var corner in corners) bounds.Encapsulate(corner);
            return bounds;
        }

        private static Transform FindRoot(Scene scene, string name) =>
            scene.GetRootGameObjects().Single(r => r.name == name).transform;

        private static Transform Descendant(Transform parent, string name) =>
            parent.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

        private static T Find<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).SingleOrDefault();

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var existing = target.GetComponent<T>();
            return existing != null ? existing : target.AddComponent<T>();
        }
    }
}
