using System.Collections.Generic;
using NatureCalls.Core;
using NatureCalls.Map;
using NatureCalls.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NatureCalls.UI
{
    /// <summary>
    /// Versus-mode HUD (GLOSSARY: GameFlow, Tidiness, Role, Urgency, Composure, Comfort). Builds itself in code on
    /// every map that has a <see cref="Playground"/>, so no scene or prefab has to change. Reads only
    /// <see cref="MatchContext"/>, <see cref="RoleRegistry"/> and <see cref="VersusHudFeed"/>.
    /// Shows: tidiness bar, round timer and urge-wave pips, own role badge, role reveal card, random-event toast,
    /// end-of-round card, saboteur head markers, own urge stages (no numbers), composure bar, and full-screen body
    /// signals (endurance pulse, high-urge tunnel, vomit nausea, comfort glow, blackout).
    /// </summary>
    public sealed class VersusHud : MonoBehaviour
    {
        // Palette: playful but readable on bright tiled bathrooms.
        private static readonly Color Ink = new(0.13f, 0.11f, 0.10f, 0.86f);
        private static readonly Color Paper = new(1f, 0.98f, 0.93f, 1f);
        private static readonly Color Clean = new(0.36f, 0.80f, 0.72f);
        private static readonly Color Grimy = new(0.84f, 0.67f, 0.25f);
        private static readonly Color Filthy = new(0.45f, 0.29f, 0.14f);
        private static readonly Color KeeperBlue = new(0.25f, 0.55f, 0.95f);
        private static readonly Color SaboteurPurple = new(0.62f, 0.30f, 0.85f);
        private static readonly Color[] StageColors = { new(0.55f, 0.55f, 0.55f, 0.75f), new(0.95f, 0.80f, 0.25f), new(1f, 0.52f, 0.15f), new(0.92f, 0.18f, 0.15f) };

        private static VersusHud instance;
        private Playground world;
        private Font font;
        private RectTransform root;
        private Image tidinessFill, tidinessBack;
        private Text tidinessLabel, timerLabel, wavesLabel, roleLabel, cooldownLabel, toastLabel;
        private Image roleBadge;
        private GameObject revealCard, endCard;
        private Text revealTitle, revealBody, endTitle, endBody;
        private Image bladderDot, bowelDot, composureFill;
        private Text bladderLabel, bowelLabel, composureLabel;
        private Image pulse, tunnel, nausea, glow, blackout;
        private CanvasGroup toastGroup;
        private int lastEventSerial;
        private float toastUntil;
        private MatchPhase lastPhase = MatchPhase.None;
        private float revealShownAt;
        private readonly Dictionary<Movement, Text> markers = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isEditor && Application.isBatchMode) return; // dedicated server: no HUD
            if (instance != null || FindFirstObjectByType<Playground>() == null) return;
            var go = new GameObject("Versus HUD");
            SceneManager.MoveGameObjectToScene(go, scene);
            instance = go.AddComponent<VersusHud>();
        }

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root = (RectTransform)transform;
            BuildSignals();
            BuildTop();
            BuildBody();
            BuildCards();
        }

        private void OnDestroy() { if (instance == this) instance = null; }

        // ---------- building ----------

        private RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }

        private RectTransform Stretch(string name, Transform parent)
        {
            var rect = Rect(name, parent, Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private Image Panel(RectTransform rect, Color color, Sprite sprite = null)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color; image.sprite = sprite; image.raycastTarget = false;
            return image;
        }

        private Text Label(RectTransform rect, int size, Color color, TextAnchor anchor, FontStyle style = FontStyle.Bold)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.color = color; text.alignment = anchor; text.fontStyle = style;
            text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = rect.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .55f); shadow.effectDistance = new Vector2(2, -2);
            return text;
        }

        private static Sprite radial, round;

        private static Sprite RadialSprite()
        {
            if (radial != null) return radial;
            const int n = 256; var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var d = new Vector2(x - n / 2f + .5f, y - n / 2f + .5f).magnitude / (n / 2f);
                    var a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.45f, 1.05f, d));
                    texture.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            texture.Apply();
            return radial = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(.5f, .5f));
        }

        private static Sprite RoundSprite()
        {
            if (round != null) return round;
            const int n = 64; var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var d = new Vector2(x - n / 2f + .5f, y - n / 2f + .5f).magnitude;
                    texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(n / 2f - d)));
                }
            texture.Apply();
            return round = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
        }

        private Image Overlay(string name, Color color, bool vignette)
        {
            var image = Panel(Stretch(name, root), color, vignette ? RadialSprite() : null);
            image.enabled = false;
            return image;
        }

        private void BuildSignals()
        {
            blackout = Overlay("Blackout", new Color(0, 0, 0, .78f), false);
            nausea = Overlay("Nausea", new Color(.35f, .75f, .20f, 0), false);
            glow = Overlay("Comfort glow", new Color(1f, .78f, .35f, 0), true);
            tunnel = Overlay("Urge tunnel", new Color(0, 0, 0, 0), true);
            pulse = Overlay("Endurance pulse", new Color(.85f, .05f, .05f, 0), true);
        }

        private void BuildTop()
        {
            var bar = Rect("Tidiness", root, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -22), new Vector2(640, 46));
            tidinessBack = Panel(bar, Ink, RoundSprite()); tidinessBack.type = Image.Type.Sliced;
            var fillRect = Stretch("Fill", bar); fillRect.offsetMin = new Vector2(6, 6); fillRect.offsetMax = new Vector2(-6, -6);
            tidinessFill = Panel(fillRect, Clean, RoundSprite());
            tidinessFill.type = Image.Type.Filled; tidinessFill.fillMethod = Image.FillMethod.Horizontal;
            tidinessLabel = Label(Stretch("Label", bar), 24, Paper, TextAnchor.MiddleCenter);

            timerLabel = Label(Rect("Timer", root, new Vector2(.5f, 1), new Vector2(0, 1), new Vector2(336, -26), new Vector2(220, 40)), 28, Paper, TextAnchor.MiddleLeft);
            wavesLabel = Label(Rect("Waves", root, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -74), new Vector2(640, 30)), 20, Paper, TextAnchor.MiddleCenter, FontStyle.Normal);

            var badge = Rect("Role", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -22), new Vector2(210, 52));
            roleBadge = Panel(badge, KeeperBlue, RoundSprite()); roleBadge.type = Image.Type.Sliced;
            roleLabel = Label(Stretch("Label", badge), 26, Color.white, TextAnchor.MiddleCenter);
            cooldownLabel = Label(Rect("Cooldowns", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -84), new Vector2(360, 120)), 19, Paper, TextAnchor.UpperLeft, FontStyle.Normal);

            var toast = Rect("Toast", root, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -118), new Vector2(760, 58));
            var toastBack = Panel(toast, new Color(.98f, .86f, .30f, .95f), RoundSprite()); toastBack.type = Image.Type.Sliced;
            toastLabel = Label(Stretch("Label", toast), 26, Ink, TextAnchor.MiddleCenter);
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>(); toastGroup.alpha = 0;
        }

        private void BuildBody()
        {
            var panel = Rect("Body", root, Vector2.zero, Vector2.zero, new Vector2(24, 24), new Vector2(330, 120));
            var back = Panel(panel, new Color(Ink.r, Ink.g, Ink.b, .55f), RoundSprite()); back.type = Image.Type.Sliced;
            bladderDot = Dot(panel, new Vector2(20, -18)); bladderLabel = Label(Rect("Bladder", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(78, -18), new Vector2(90, 46)), 24, Paper, TextAnchor.MiddleLeft);
            bowelDot = Dot(panel, new Vector2(170, -18)); bowelLabel = Label(Rect("Bowel", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(228, -18), new Vector2(90, 46)), 24, Paper, TextAnchor.MiddleLeft);
            bladderLabel.text = "尿意"; bowelLabel.text = "便意";
            var compBack = Rect("Composure", panel, new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 14), new Vector2(298, 34));
            var cb = Panel(compBack, new Color(0, 0, 0, .55f), RoundSprite()); cb.type = Image.Type.Sliced;
            var compFill = Stretch("Fill", compBack); compFill.offsetMin = new Vector2(4, 4); compFill.offsetMax = new Vector2(-4, -4);
            composureFill = Panel(compFill, new Color(.40f, .85f, .45f), RoundSprite());
            composureFill.type = Image.Type.Filled; composureFill.fillMethod = Image.FillMethod.Horizontal;
            composureLabel = Label(Stretch("Label", compBack), 19, Paper, TextAnchor.MiddleCenter);
        }

        private Image Dot(RectTransform parent, Vector2 position)
        {
            var image = Panel(Rect("Dot", parent, new Vector2(0, 1), new Vector2(0, 1), position, new Vector2(46, 46)), StageColors[0], RoundSprite());
            image.type = Image.Type.Sliced;
            return image;
        }

        private GameObject Card(string name, out Text title, out Text body, Vector2 size)
        {
            var card = Rect(name, root, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 40), size);
            var back = Panel(card, new Color(Ink.r, Ink.g, Ink.b, .93f), RoundSprite()); back.type = Image.Type.Sliced;
            title = Label(Rect("Title", card, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -26), new Vector2(size.x - 40, 90)), 60, Paper, TextAnchor.MiddleCenter);
            body = Label(Rect("Body", card, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -130), new Vector2(size.x - 80, size.y - 150)), 26, Paper, TextAnchor.UpperCenter, FontStyle.Normal);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            card.gameObject.SetActive(false);
            return card.gameObject;
        }

        private void BuildCards()
        {
            revealCard = Card("Reveal", out revealTitle, out revealBody, new Vector2(820, 330));
            endCard = Card("End", out endTitle, out endBody, new Vector2(860, 560));
        }

        // ---------- per frame ----------

        private Movement LocalPlayer()
        {
            if (world == null) world = FindFirstObjectByType<Playground>();
            return world != null && world.Camera != null ? world.Camera.Target : null;
        }

        private void Update()
        {
            var phase = MatchContext.Phase;
            var versus = phase != MatchPhase.None;
            root.gameObject.GetComponent<Canvas>().enabled = versus;
            HideLegacyHud(versus);
            if (!versus) { lastPhase = phase; return; }
            var me = LocalPlayer();
            UpdateTop(me);
            UpdateBody(me);
            UpdateSignals(me);
            UpdateCards(phase, me);
            UpdateMarkers(me);
            lastPhase = phase;
        }

        private void HideLegacyHud(bool versus)
        {
            // The old numeric urgency/disgust bars and the countdown clock are replaced in versus rounds.
            foreach (var view in FindObjectsByType<NeedsView>(FindObjectsSortMode.None)) Fade(view.gameObject, versus);
            foreach (var clock in FindObjectsByType<MatchClock>(FindObjectsSortMode.None)) Fade(clock.gameObject, versus);
        }

        private static void Fade(GameObject target, bool hidden)
        {
            if (!target.TryGetComponent<CanvasGroup>(out var group)) group = target.AddComponent<CanvasGroup>();
            group.alpha = hidden ? 0f : 1f;
        }

        private void UpdateTop(Movement me)
        {
            var tidy = Mathf.Clamp(MatchContext.Tidiness, 0, 100);
            var t = tidy / 100f;
            tidinessFill.fillAmount = Mathf.Lerp(tidinessFill.fillAmount, t, Time.unscaledDeltaTime * 6f);
            tidinessFill.color = t > .5f ? Color.Lerp(Grimy, Clean, (t - .5f) * 2f) : Color.Lerp(Filthy, Grimy, t * 2f);
            var danger = tidy < 25 ? .5f + .5f * Mathf.Sin(Time.unscaledTime * 8f) : 0f;
            tidinessBack.color = Color.Lerp(Ink, new Color(.75f, .12f, .10f, .9f), danger);
            tidinessLabel.text = $"干净值  {Mathf.CeilToInt(tidy)}";

            var remaining = VersusHudFeed.TimeRemaining();
            timerLabel.text = remaining >= 0 ? RoundClock.Format(remaining) : "";
            wavesLabel.text = WaveText();

            var saboteur = RoleRegistry.IsSaboteur(me);
            roleBadge.color = saboteur ? SaboteurPurple : KeeperBlue;
            roleLabel.text = saboteur ? "捣蛋者" : "保洁队";
            cooldownLabel.text = saboteur ? VersusHudFeed.Cooldowns(me) : "";

            var (text, serial) = VersusHudFeed.LatestEvent();
            if (serial != lastEventSerial && !string.IsNullOrEmpty(text)) { lastEventSerial = serial; toastLabel.text = text; toastUntil = Time.unscaledTime + 4.5f; }
            toastGroup.alpha = Mathf.Clamp01((toastUntil - Time.unscaledTime) / .5f);
        }

        private static string WaveText()
        {
            var schedule = MatchContext.Schedule;
            if (schedule.Centers == null || schedule.Centers.Length == 0) return "";
            var now = MatchContext.Elapsed;
            var parts = new List<string>();
            for (int i = 0; i < schedule.Centers.Length; i++)
            {
                var c = schedule.Centers[i];
                var mark = now > c + schedule.Width * 1.5 ? "●" : now > c - schedule.Width * 2 ? "◉" : "○";
                parts.Add(mark);
            }
            int next = -1;
            for (int i = 0; i < schedule.Centers.Length; i++) if (schedule.Centers[i] - schedule.Width * 2 > now) { next = i; break; }
            var hint = next >= 0 ? $"   下一波高峰约 {RoundClock.Format(schedule.Centers[next] - schedule.Width * 2 - now)} 后" : "   最后关头！";
            for (int i = 0; i < schedule.Centers.Length; i++)
                if (now > schedule.Centers[i] - schedule.Width * 2 && now < schedule.Centers[i] + schedule.Width * 1.5) hint = $"   第 {i + 1} 波便意高峰！";
            return "便意高峰  " + string.Join(" ", parts) + hint;
        }

        private static int Stage(float value01) => value01 >= .9f ? 3 : value01 >= .7f ? 2 : value01 >= .4f ? 1 : 0;

        private void UpdateBody(Movement me)
        {
            SetDot(bladderDot, VersusHudFeed.Bladder(me));
            SetDot(bowelDot, VersusHudFeed.Bowel(me));
            var composure = Mathf.Clamp01(VersusHudFeed.Composure(me));
            composureFill.fillAmount = composure;
            composureFill.color = Color.Lerp(new Color(.55f, .75f, .20f), new Color(.40f, .85f, .45f), composure);
            var vomit = VersusHudFeed.Vomit(me);
            composureLabel.text = vomit > 0 ? "要吐了……" : $"镇定值  {Mathf.CeilToInt(composure * 100)}";
        }

        private void SetDot(Image dot, float value01)
        {
            var stage = Stage(value01);
            var color = StageColors[stage];
            if (stage == 3) color = Color.Lerp(color, Color.white, .35f + .35f * Mathf.Sin(Time.unscaledTime * 10f));
            dot.color = color;
            var scale = 1f + (stage == 3 ? .12f * Mathf.Sin(Time.unscaledTime * 10f) : 0f);
            dot.rectTransform.localScale = new Vector3(scale, scale, 1);
        }

        private void UpdateSignals(Movement me)
        {
            // Heartbeat: two quick beats per cycle, faster as endurance deepens.
            var endurance = Mathf.Clamp01(VersusHudFeed.Endurance(me));
            var bpm = Mathf.Lerp(80f, 150f, endurance);
            var beat = Mathf.Repeat(Time.unscaledTime * bpm / 60f, 1f);
            var thump = Mathf.Max(Mathf.Exp(-beat * 18f), .7f * Mathf.Exp(-Mathf.Max(0, beat - .22f) * 18f) * (beat > .22f ? 1 : 0));
            SetOverlay(pulse, endurance > 0 || VersusHudFeed.LostControl(me), new Color(.85f, .05f, .05f, Mathf.Lerp(.25f, .55f, endurance) + .3f * thump), 1f + .05f * thump);

            var narrow = Mathf.Clamp01(Mathf.Max(VersusHudFeed.Narrow(me), endurance * .6f));
            SetOverlay(tunnel, narrow > .01f, new Color(0, 0, 0, .85f * narrow), Mathf.Lerp(1.25f, .8f, narrow));

            var vomit = Mathf.Clamp01(VersusHudFeed.Vomit(me));
            var wobble = vomit > 0 ? .5f + .5f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(2f, 7f, vomit)) : 0;
            SetOverlay(nausea, vomit > 0, new Color(.35f, .75f, .20f, Mathf.Lerp(.10f, .38f, vomit) * (.6f + .4f * wobble)), 1f);

            var comfort = Mathf.Clamp01(VersusHudFeed.Comfort(me));
            SetOverlay(glow, comfort > .01f, new Color(1f, .78f, .35f, .45f * comfort), 1.1f - .05f * Mathf.Sin(Time.unscaledTime * 1.5f));

            var dark = VersusHudFeed.Blackout();
            SetOverlay(blackout, dark > 0, new Color(0, 0, 0, Mathf.Clamp01(dark) * .78f * (Random.value > .03f ? 1f : .4f)), 1f);
        }

        private static void SetOverlay(Image image, bool on, Color color, float scale)
        {
            image.enabled = on;
            if (!on) return;
            image.color = color;
            image.rectTransform.localScale = new Vector3(scale, scale, 1);
        }

        private void UpdateCards(MatchPhase phase, Movement me)
        {
            if (phase == MatchPhase.Reveal && lastPhase != MatchPhase.Reveal) revealShownAt = Time.unscaledTime;
            var revealing = phase == MatchPhase.Reveal || (phase == MatchPhase.Playing && Time.unscaledTime - revealShownAt < 4.5f && revealShownAt > 0);
            revealCard.SetActive(revealing);
            if (revealing)
            {
                var mine = RoleRegistry.IsSaboteur(me);
                string saboteurName = "";
                foreach (var player in FindObjectsByType<Movement>(FindObjectsSortMode.None))
                    if (RoleRegistry.IsSaboteur(player)) { saboteurName = VersusHudFeed.NameOf(player); break; }
                revealTitle.text = mine ? "你是捣蛋者" : "你是保洁队";
                revealTitle.color = mine ? new Color(.82f, .58f, 1f) : new Color(.55f, .80f, 1f);
                revealBody.text = mine
                    ? "把厕所搞脏，让他们拉不成！\n塞纸、弄坏、泼水、敲门……\n你便秘：急得慢、用纸少，但随地拉不划算。"
                    : (string.IsNullOrEmpty(saboteurName) ? "捣蛋者头顶有紫色标记。" : $"捣蛋者是 {saboteurName}（头顶有紫色标记）") + "\n守住干净值，撑过所有便意高峰！\n你们看不到彼此有多急——记得喊话。";
                var alpha = phase == MatchPhase.Reveal ? 1f : Mathf.Clamp01((4.5f - (Time.unscaledTime - revealShownAt)) / .6f);
                foreach (var g in revealCard.GetComponentsInChildren<Graphic>()) { var c = g.color; c.a = g == revealTitle || g == revealBody ? alpha : .93f * alpha; g.color = c; }
            }

            endCard.SetActive(phase == MatchPhase.Ended);
            if (phase == MatchPhase.Ended)
            {
                var (keepersWon, reason, stats) = VersusHudFeed.Outcome();
                var iWon = keepersWon != RoleRegistry.IsSaboteur(me);
                endTitle.text = keepersWon ? "保洁队胜利" : "捣蛋者胜利";
                endTitle.color = keepersWon ? new Color(.55f, .85f, 1f) : new Color(.82f, .58f, 1f);
                endBody.text = (iWon ? "你赢了！" : "你输了……") + "\n" + reason + (string.IsNullOrEmpty(stats) ? "" : "\n\n" + stats);
            }
        }

        private void UpdateMarkers(Movement me)
        {
            var camera = UnityEngine.Camera.main;
            var seen = new HashSet<Movement>();
            foreach (var player in FindObjectsByType<Movement>(FindObjectsSortMode.None))
            {
                if (player == me || !RoleRegistry.IsSaboteur(player)) continue;
                seen.Add(player);
                if (!markers.TryGetValue(player, out var label) || label == null)
                {
                    label = Label(Rect("Saboteur marker", root, Vector2.zero, new Vector2(.5f, 0), Vector2.zero, new Vector2(160, 40)), 26, new Color(.85f, .62f, 1f), TextAnchor.LowerCenter);
                    label.text = "▼ 捣蛋者";
                    markers[player] = label;
                }
                var visible = camera != null;
                var screen = visible ? camera.WorldToScreenPoint(player.transform.position + Vector3.up * 2.05f) : Vector3.zero;
                visible &= screen.z > 0;
                label.enabled = visible;
                if (visible)
                {
                    var scale = root.lossyScale.x > 0 ? root.lossyScale.x : 1f;
                    label.rectTransform.anchoredPosition = new Vector2(screen.x / scale, screen.y / scale + 4f * Mathf.Sin(Time.unscaledTime * 3f));
                }
            }
            var stale = new List<Movement>();
            foreach (var pair in markers) if (!seen.Contains(pair.Key)) stale.Add(pair.Key);
            foreach (var key in stale) { if (markers[key] != null) Destroy(markers[key].gameObject); markers.Remove(key); }
        }
    }
}
