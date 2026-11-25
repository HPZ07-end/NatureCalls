using System;
using NatureCalls.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NatureCalls.Editor
{
    /// <summary>Restyles the existing outfit references, preserving the Lobby prefab and its GUID.</summary>
    public static class FloatingOutfitSetup
    {
        private static readonly Color Ink = new(.95f, .93f, .85f);
        private static readonly Color Gold = new(1, .84f, .48f);

        [MenuItem("NatureCalls/Restyle Dressing Room UI")]
        public static void Configure()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before restyling the UI.");
            var root = PrefabUtility.LoadPrefabContents(LobbyFlowBuilder.CanvasPath);
            try
            {
                var view = root.GetComponent<LobbyView>();
                var panel = (RectTransform)view.OutfitPanel.transform;
                if (panel.TryGetComponent<Image>(out var background)) Object.DestroyImmediate(background);
                panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
                panel.pivot = new Vector2(0, .5f); panel.anchoredPosition = Vector2.zero; panel.sizeDelta = new Vector2(360, 820);
                var canvas = Get<Canvas>(panel.gameObject); canvas.renderMode = RenderMode.WorldSpace;
                canvas.overrideSorting = true; canvas.sortingOrder = 20;
                Get<GraphicRaycaster>(panel.gameObject);
                Get<CanvasGroup>(panel.gameObject);
                Get<FloatingOutfitMenu>(panel.gameObject);
                Label(panel.Find("Title").GetComponent<Text>(), panel, "镜子里的我", 46, 0, 0, 360, 64);
                Label(GetText(panel, "BodyHeading"), panel, "体型", 24, 0, -84, 360, 38);
                Choice(view.OutfitSwift, panel, "矮快型", 0, -130, false);
                Choice(view.OutfitBalanced, panel, "均衡型", 0, -192, false);
                Choice(view.OutfitTall, panel, "高伸型", 0, -254, false);
                Label(GetText(panel, "ColorHeading"), panel, "衣服的颜色", 24, 0, -332, 360, 38);
                Place(view.OutfitOptions, panel, 0, -380, 360, 244);
                var layout = Get<VerticalLayoutGroup>(view.OutfitOptions.gameObject);
                layout.spacing = 4; layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
                Choice(view.OutfitOptionTemplate, view.OutfitOptions, "颜色", 0, 0, true);
                view.OutfitOptionTemplate.gameObject.SetActive(false);
                Label(view.OutfitCurrent, panel, "", 22, 0, -656, 360, 36);
                view.OutfitCurrent.color = new Color(Ink.r, Ink.g, Ink.b, .65f);
                Action(view.OutfitConfirm, panel, "确认", 0, -734);
                Action(view.OutfitCancel, panel, "取消", 180, -734);
                foreach (var item in panel.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 5;
                view.OutfitPanel.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, LobbyFlowBuilder.CanvasPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("DRESSING_ROOM_UI_RESTYLED|worldText=1|bodyChoices=3|colorChoices=catalog|backdrop=0");
        }

        private static void Choice(Button button, Transform parent, string text, float x, float y, bool swatch)
        {
            Place((RectTransform)button.transform, parent, x, y, 360, 58);
            ClearButton(button);
            var height = Get<LayoutElement>(button.gameObject); height.minHeight = height.preferredHeight = 58;
            height.flexibleHeight = 0;
            Get<CanvasGroup>(button.gameObject);
            var label = button.GetComponentInChildren<Text>(true);
            Label(label, button.transform, text, 38, swatch ? 76 : 48, 0, swatch ? 284 : 312, 58);
            // The row fades as one line; avoid multiplying a second fade on its label.
            if (label.TryGetComponent<CanvasGroup>(out var labelGroup)) Object.DestroyImmediate(labelGroup);
            var box = Rect(button.transform, "SelectionBox"); Place(box, button.transform, 0, -16, 26, 26);
            Stroke(box, "Top", new Vector2(0, 0), new Vector2(26, 0), 1.6f, Ink);
            Stroke(box, "Bottom", new Vector2(0, -26), new Vector2(26, -26), 1.6f, Ink);
            Stroke(box, "Left", new Vector2(0, 0), new Vector2(0, -26), 1.6f, Ink);
            Stroke(box, "Right", new Vector2(26, 0), new Vector2(26, -26), 1.6f, Ink);
            var check = Rect(box, "Check"); Place(check, box, 0, 0, 26, 26);
            Stroke(check, "Short", new Vector2(4, -13), new Vector2(10, -19), 3, Gold);
            Stroke(check, "Long", new Vector2(10, -19), new Vector2(23, -5), 3, Gold);
            check.gameObject.SetActive(false);
            var choice = Get<OutfitChoice>(button.gameObject); choice.Label = label; choice.Check = check.gameObject;
            if (swatch)
            {
                var sample = Rect(button.transform, "ColorSwatch"); Place(sample, button.transform, 46, -22, 14, 14);
                choice.Swatch = Get<Image>(sample.gameObject); choice.Swatch.raycastTarget = false;
            }
        }

        private static void Action(Button button, Transform parent, string value, float x, float y)
        {
            Place((RectTransform)button.transform, parent, x, y, 150, 58);
            ClearButton(button); Get<CanvasGroup>(button.gameObject);
            var label = button.GetComponentInChildren<Text>(true);
            Label(label, button.transform, value, 34, 0, 0, 150, 58);
            if (label.TryGetComponent<CanvasGroup>(out var group)) Object.DestroyImmediate(group);
        }

        private static void ClearButton(Button button)
        {
            var image = Get<Image>(button.gameObject); image.color = Color.clear; image.sprite = null; image.type = Image.Type.Simple;
            image.raycastTarget = true; button.targetGraphic = image; button.transition = Selectable.Transition.None;
            button.interactable = true;
        }

        private static void Label(Text label, Transform parent, string value, int size, float x, float y, float width, float height)
        {
            Place(label.rectTransform, parent, x, y, width, height);
            label.text = value; label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = size;
            label.alignment = TextAnchor.MiddleLeft; label.color = Ink; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = Get<Shadow>(label.gameObject); shadow.effectColor = new Color(.04f, .055f, .05f, .75f); shadow.effectDistance = new Vector2(1, -2);
            Get<CanvasGroup>(label.gameObject);
        }

        private static void Stroke(Transform parent, string name, Vector2 a, Vector2 b, float thickness, Color color)
        {
            var rect = Rect(parent, name); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = (a + b) / 2; rect.sizeDelta = new Vector2(Vector2.Distance(a, b), thickness);
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            var image = Get<Image>(rect.gameObject); image.color = color; image.raycastTarget = false;
        }

        private static void Place(RectTransform rect, Transform parent, float x, float y, float width, float height)
        {
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height); rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        }
        private static Text GetText(Transform parent, string name) => Get<Text>(Rect(parent, name).gameObject);
        private static RectTransform Rect(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null) { child = new GameObject(name, typeof(RectTransform)).transform; child.SetParent(parent, false); }
            return (RectTransform)child;
        }
        private static T Get<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
