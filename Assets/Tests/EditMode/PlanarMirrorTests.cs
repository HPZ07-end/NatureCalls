using System.Linq;
using NatureCalls.Facility;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NatureCalls.Tests
{
    public sealed class PlanarMirrorTests
    {
        [TestCase(-.2f, 0f, .9f)]
        [TestCase(.2f, .36f, 1.4f)]
        public void ReflectedEyeAndWindowCornersFollowTheViewer(float lateral, float raised, float distance)
        {
            var root = new GameObject("Mirror Projection Test");
            try
            {
                var mirror = root.AddComponent<PlanarMirror>();
                mirror.Surface = root.AddComponent<MeshRenderer>();
                root.transform.position = new Vector3(0, 1.5f, 0);
                mirror.Size = new Vector2(.8f, 1);
                var viewer = new GameObject("Viewer"); viewer.transform.SetParent(root.transform);
                mirror.ViewerCamera = viewer.AddComponent<UnityEngine.Camera>();
                viewer.transform.position = new Vector3(lateral, 1.65f + raised, -distance);
                var reflection = new GameObject("Reflection"); reflection.transform.SetParent(root.transform);
                mirror.ReflectionCamera = reflection.AddComponent<UnityEngine.Camera>();
                Assert.That(mirror.UpdateView(), Is.True);
                Assert.That(Vector3.Distance(reflection.transform.position, new Vector3(lateral, 1.65f + raised, distance)), Is.LessThan(.0001f));
                var matrix = mirror.ReflectionCamera.projectionMatrix * mirror.ReflectionCamera.worldToCameraMatrix;
                var corners = new[] { new Vector3(-.4f, -.5f), new Vector3(.4f, -.5f), new Vector3(-.4f, .5f), new Vector3(.4f, .5f) };
                var uv = new[] { new Vector2(1, 0), new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 1) };
                for (int index = 0; index < corners.Length; index++)
                {
                    var world = root.transform.TransformPoint(corners[index]);
                    var clip = matrix * new Vector4(world.x, world.y, world.z, 1);
                    Assert.That(clip.w, Is.GreaterThan(0));
                    Assert.That(clip.x / clip.w * .5f + .5f, Is.EqualTo(uv[index].x).Within(.0001f));
                    Assert.That(clip.y / clip.w * .5f + .5f, Is.EqualTo(uv[index].y).Within(.0001f));
                }
                viewer.transform.position = new Vector3(0, 1.65f, .1f);
                Assert.That(mirror.UpdateView(), Is.False, "A viewer behind the mirror is not rendered.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void RoomUsesFullRangeUvAndFrontFacingDoorSigns()
        {
            const string path = "Assets/Scenes/DressingRoom.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool loaded = scene.IsValid() && scene.isLoaded;
            if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var mirrors = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlanarMirror>()).ToArray();
                Assert.That(mirrors, Has.Length.EqualTo(2));
                foreach (var mirror in mirrors)
                {
                    Assert.That(mirror.ViewerCamera, Is.Not.Null);
                    Assert.That(mirror.ReflectionCamera.enabled, Is.False, "Reflection rendering is driven after the viewer's LateUpdate.");
                    var mesh = mirror.Surface.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(mesh.vertexCount, Is.EqualTo(4));
                    Assert.That(mesh.uv, Is.EquivalentTo(new[] { Vector2.zero, Vector2.one, Vector2.right, Vector2.up }));
                    Assert.That(mesh.normals.All(normal => Vector3.Dot(normal, Vector3.back) > .99f), Is.True);
                }
                var room = scene.GetRootGameObjects().Single(root => root.name == "DressingRoom");
                foreach (var label in room.GetComponentsInChildren<TextMesh>())
                {
                    var inside = label.text == "大厅" ? new Vector3(1.25f, 1.65f, 1.48f) : new Vector3(-1.15f, 1.65f, -1.25f);
                    Assert.That(Vector3.Dot(-label.transform.forward, inside - label.transform.position), Is.GreaterThan(0), label.text);
                }
            }
            finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
