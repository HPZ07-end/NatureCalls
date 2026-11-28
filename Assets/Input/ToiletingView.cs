using NatureCalls.Core;
using NatureCalls.Facility;
using NatureCalls.Network;
using NatureCalls.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NatureCalls.Input
{
    /// <summary>Adds the local toileting presentation (GLOSSARY Squatting/统一如厕过程) to a player.</summary>
    public static class ToiletingView
    {
        public static void Attach(GameObject player)
        {
            if (RuntimeMode.Dedicated || player == null) return;
            if (player.GetComponent<ToiletGaze>() == null) player.AddComponent<ToiletGaze>();
            if (player.GetComponent<ToiletingFx>() == null) player.AddComponent<ToiletingFx>();
            if (player.GetComponent<ComfortSparkle>() == null) player.AddComponent<ComfortSparkle>();
        }
    }

    /// <summary>
    /// Timed look-down while defecating on a toilet: moving the mouse down bows the head (Movement.Pitch, which
    /// PlayerNeeds already rewards with Comfort) and shows where the stool will land. About 3 seconds of looking per
    /// sitting; when it runs out the view is pulled back up. Looking steadies the aim (drift ×0.6).
    /// </summary>
    public sealed class ToiletGaze : MonoBehaviour
    {
        public const float Budget = 3f, LookThreshold = .6f, LookPitch = 70f, DriftWhileLooking = .6f;
        /// <summary>Head pitch (degrees down) at which a squat becomes the aim; looking back up leaves it.</summary>
        public const float AimPitch = 35f;
        /// <summary>0 = head up, 1 = fully bowed.</summary>
        public float Amount { get; private set; }
        public float Remaining { get; private set; } = Budget;
        public bool Looking => Amount >= LookThreshold;
        public bool Exhausted => Remaining <= 0;
        /// <summary>Multiplier PlayerInput applies to the landing drift.</summary>
        public float DriftScale => Looking ? DriftWhileLooking : 1f;
        public GameObject Marker { get; private set; }

        private PlayerInput input;
        private Movement movement;
        private float push;
        private bool wasActive;
        private Renderer markerRenderer;
        private MaterialPropertyBlock block;

        private void Awake() { input = GetComponent<PlayerInput>(); movement = GetComponent<Movement>(); }

        /// <summary>Mouse-down amount for this frame (tests, or other input devices).</summary>
        public void Push(float amount) => push += amount;

        private bool Active => input != null && (input.Phase == PlayerInput.Stage.Squatting || input.Phase == PlayerInput.Stage.Aiming) && movement.Controlled;

        private void Update()
        {
            bool active = Active;
            if (active && !wasActive) { Remaining = Budget; Amount = 0; }
            if (!active)
            {
                if (wasActive) { Amount = 0; SetPitch(0); Camera(0); }
                wasActive = false; ShowMarker(false); push = 0;
                return;
            }
            wasActive = true;
            // The mouse owns the view (PlayerInput); the gaze only reads how far the head is bowed. Push() still lets
            // tests and other devices bow the head. No time budget: the player may look down as long as they like.
            if (push != 0) { SetPitch(movement.Pitch + push * LookPitch); push = 0; }
            Amount = Mathf.Clamp01(movement.Pitch / LookPitch);
            input.RefreshAimPhase();
            input.TickRelease(Time.deltaTime);
            if (Looking) Remaining = Mathf.Max(0, Remaining - Time.deltaTime);
            // A bowed head turns the view gently toward the spot between the legs (never straight down).
            Camera(.8f * Mathf.InverseLerp(.3f, .9f, Amount));
            ShowMarker(false); // blind aim: no reticle or landing marker
        }

        private void SetPitch(float pitch) => movement.SetPitch(pitch);

        private void Camera(float amount)
        {
            var world = FindFirstObjectByType<Map.Playground>();
            var rig = world != null ? world.Camera : null;
            if (rig == null || rig.Target != movement) return;
            rig.Gaze = amount;
            // Between the legs, just under the buttocks: where the stool comes out (the landing itself stays unmarked).
            var under = input.Releasing ? input.LastDropPoint : input.CurrentLanding;
            rig.GazePoint = under + movement.transform.forward * .1f;
        }

        private void ShowMarker(bool visible)
        {
            var art = MessArt.Current;
            if (Marker == null)
            {
                if (!visible || art == null || art.Splash == null) return;
                Marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Marker.name = "Landing marker";
                Destroy(Marker.GetComponent<Collider>());
                markerRenderer = Marker.GetComponent<Renderer>(); markerRenderer.sharedMaterial = art.Splash;
                markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                block = new MaterialPropertyBlock();
            }
            Marker.SetActive(visible);
            if (!visible) return;
            bool inside = Vector3.Distance(input.CurrentLanding, input.TargetCenter) <= input.TargetRadius;
            Marker.transform.SetPositionAndRotation(input.CurrentLanding + Vector3.down * .12f, Quaternion.Euler(90, 0, 0));
            Marker.transform.localScale = Vector3.one * (.09f + .02f * Mathf.Sin(Time.time * 10));
            block.SetColor("_BaseColor", inside ? new Color(.3f, 1f, .4f, .95f) : new Color(1f, .3f, .25f, .95f));
            markerRenderer.SetPropertyBlock(block);
        }

        private void OnDestroy() { if (Marker != null) Destroy(Marker); }
    }

    /// <summary>
    /// Local toileting effects: the stool squeezes out and falls from the buttocks (into the water with a splash, maybe
    /// splashing the buttocks), and the urine stream is a ballistic arc aimed with the mouse that rebounds off the
    /// urinal wall by its angle of incidence; a rebound coming straight back splashes the player (BodyEvents.Splash).
    /// </summary>
    public sealed class ToiletingFx : MonoBehaviour
    {
        public const float WaterDepth = .2f, StreamWidth = .018f, Restitution = .45f;
        /// <summary>Cosine of incidence above which an off-target rebound comes back at the player.</summary>
        public const float SelfSplashCosine = .8f, SelfSplashSeconds = .35f;
        public static float ButtSplashChance = .5f;

        public event System.Action<bool, bool> Dropped; // inBowl, buttSplash
        public event System.Action<Vector3, bool> StreamHit; // point, inside target
        public event System.Action SelfSplashed;
        public bool Dropping => pieces.Count > 0;
        public LineRenderer Stream { get; private set; }
        public Vector3 StreamOrigin { get; private set; }
        public Vector3 StreamVelocity { get; private set; }

        private PlayerInput input;
        private Movement movement;
        private PlayerInput.Stage lastPhase;
        private bool dropped, selfSplashed;
        private float offTargetBack, nextSplash;
        private float dropWater, dropFloor, dropAge;
        private bool dropInBowl;
        private int seenDrop;
        private Vector3 dropTarget, bowlCenter;

        private void Awake() { input = GetComponent<PlayerInput>(); movement = GetComponent<Movement>(); }

        private void Update()
        {
            if (input == null || !movement.Controlled) { HideStream(); return; }
            var phase = input.Phase;
            if (phase != lastPhase)
            {
                // C confirmed the squat aim: the stool leaves now, so show it falling into (or past) the bowl.
                // (The squat release is detected through PlayerInput.DropSerial below.)
                if (phase == PlayerInput.Stage.Squatting || phase == PlayerInput.Stage.Aiming) { dropped = false; }
                if (phase == PlayerInput.Stage.Urinating) { selfSplashed = false; offTargetBack = 0; FaceUrinal(); }
                lastPhase = phase;
            }
            if ((phase == PlayerInput.Stage.Squatting || phase == PlayerInput.Stage.Aiming) && !dropped && input.Duration > 0 && input.Elapsed >= input.Duration - 1.2f) BeginDrop();
            if (input.DropSerial != seenDrop) { seenDrop = input.DropSerial; BeginDrop(); }
            if (phase == PlayerInput.Stage.Urinating && input.UrineFlowing) TickStream(Time.deltaTime); else HideStream();
            TickDrop(Time.deltaTime);
            FadeBlockers(phase == PlayerInput.Stage.Urinating && movement.Seat == null ? input.CurrentToilet : null);
        }

        private readonly System.Collections.Generic.List<Renderer> faded = new();
        /// <summary>
        /// Standing close to a toilet, its tank or lid can sit between the eye and the bowl. While aiming the stream,
        /// parts of that toilet which cover the line of sight to the opening (and do not contain it) are hidden.
        /// </summary>
        private void FadeBlockers(Toilet target)
        {
            foreach (var renderer in faded) if (renderer != null) renderer.forceRenderingOff = false;
            faded.Clear();
            var eye = UnityEngine.Camera.main;
            if (target == null || eye == null) return;
            var from = eye.transform.position; var to = input.TargetCenter;
            var ray = new Ray(from, (to - from).normalized); float length = Vector3.Distance(from, to);
            var root = target.transform.parent != null && target.transform.parent.GetComponent<StallDoor>() == null ? target.transform.parent : target.transform;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (renderer is LineRenderer || renderer.forceRenderingOff) continue;
                var bounds = renderer.bounds;
                if (bounds.Contains(to) || Vector3.Distance(bounds.center, to) > 1.2f) continue; // keep the bowl itself; only nearby parts
                if (bounds.IntersectRay(ray, out var distance) && distance < length - .2f) { renderer.forceRenderingOff = true; faded.Add(renderer); }
            }
        }

        // ---------------------------------------------------------------- defecation
        /// <summary>Starts the stool falling from the buttocks: soft is one log, hard drops pellet by pellet, loose pours as drops.</summary>
        public void BeginDrop()
        {
            dropped = true;
            ClearPieces();
            var art = MessArt.Current;
            if (art == null || art.SoftSwirl == null) return;
            dropTarget = input.DropSerial > 0 ? input.LastDropPoint : input.CurrentLanding;
            dropInBowl = input.DropSerial > 0 ? input.LastDropInBowl : movement.Seat != null && Vector3.Distance(dropTarget, input.TargetCenter) <= input.TargetRadius;
            bowlCenter = dropInBowl && input.DropSerial > 0 ? input.LastDropPoint : input.TargetCenter;
            dropWater = bowlCenter.y - WaterDepth;
            dropFloor = movement.transform.position.y;
            // Out of the buttocks: seat height above the landing spot, so a bowed head sees it leave and fall.
            var butt = new Vector3(dropTarget.x, movement.transform.position.y + .48f, dropTarget.z);
            var kind = input.DropSerial > 0 ? input.LastDropKind : StoolKind.Soft;
            dropKind = kind; dropAge = 0; landedAny = false;
            // Pace to the release time (harder is slower). Stage 1: the stool emerges from the buttocks (animated);
            // stage 2: it detaches and the physics engine drops it.
            float span = Mathf.Max(.4f, (input.ReleaseDuration > 0 ? input.ReleaseDuration : StoolRules.ReleaseSeconds(kind)) - .4f);
            float yaw = movement.transform.eulerAngles.y;
            switch (kind)
            {
                case StoolKind.Hard: // pellets pushed out one at a time, each dropping before the next
                    for (int i = 0; i < 5; i++)
                        AddPiece(art.HardLog, art.StoolDry, butt, .16f, i * span / 5f, Mathf.Min(.45f, span / 5f * .8f), .03f, yaw,
                                 new Vector3(Random.Range(-.015f, .015f), 0, Random.Range(-.015f, .015f)));
                    break;
                case StoolKind.Loose: // a runny pour: short drips that stretch and let go
                    for (int i = 0; i < 22; i++)
                    {
                        var drip = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(drip.GetComponent<Collider>());
                        drip.GetComponent<Renderer>().sharedMaterial = art.Loose; drip.name = "Loose drop"; drip.SetActive(false);
                        pieces.Add(new Piece { T = drip.transform, Delay = i * span / 22f, Emerge = .08f, Hang = .02f, Scale = Random.Range(.035f, .06f),
                            Origin = butt + new Vector3(Random.Range(-.015f, .015f), 0, Random.Range(-.015f, .015f)), Liquid = true });
                    }
                    break;
                default: // one soft log that slowly hangs down before it lets go
                    AddPiece(art.HardLog != null ? art.HardLog : art.SoftSwirl, art.Stool, butt, .5f, 0, span * .85f, .12f, yaw, Vector3.zero);
                    break;
            }
        }

        private struct Piece
        {
            public Transform T; public Rigidbody Body; public Vector3 Origin;
            public float Delay, Emerge, Hang, Scale, Yaw; public bool Started, Falling, Landed, Liquid;
        }
        private readonly System.Collections.Generic.List<Piece> pieces = new();
        private StoolKind dropKind;
        private bool landedAny;
        /// <summary>Pieces still coming out of the buttocks (stage 1).</summary>
        public int EmergingPieces { get { int n = 0; foreach (var p in pieces) if (p.Started && !p.Falling && !p.Landed) n++; return n; } }
        /// <summary>Pieces the physics engine is dropping (stage 2).</summary>
        public int FallingPieces { get { int n = 0; foreach (var p in pieces) if (p.Falling && !p.Landed) n++; return n; } }

        private void AddPiece(Mesh mesh, Material material, Vector3 origin, float scale, float delay, float emerge, float hang, float yaw, Vector3 offset)
        {
            var go = new GameObject("Falling stool", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.SetActive(false);
            pieces.Add(new Piece { T = go.transform, Delay = delay, Emerge = emerge, Hang = hang, Scale = scale, Yaw = yaw, Origin = origin + offset });
        }

        private void ClearPieces() { foreach (var piece in pieces) if (piece.T != null) Destroy(piece.T.gameObject); pieces.Clear(); }

        private void TickDrop(float dt)
        {
            if (pieces.Count == 0) return;
            dropAge += dt;
            float floor = dropInBowl ? dropWater - .04f : dropFloor;
            bool alive = false;
            for (int i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                if (piece.T == null) continue;
                float age = dropAge - piece.Delay;
                if (age < 0) { alive = true; continue; }
                if (!piece.Started)
                {
                    piece.Started = true; piece.T.gameObject.SetActive(true);
                    piece.T.SetPositionAndRotation(piece.Origin, piece.Liquid ? Quaternion.identity : Quaternion.Euler(90, piece.Yaw, 0));
                }
                if (!piece.Falling && !piece.Landed)
                {
                    // Stage 1: coming out. It grows and sinks below the buttocks (a soft log hangs and sways).
                    float t = Mathf.Clamp01(age / Mathf.Max(.01f, piece.Emerge));
                    float eased = Mathf.SmoothStep(0, 1, t);
                    var scale = Vector3.one * Mathf.Lerp(piece.Scale * .15f, piece.Scale, eased);
                    if (piece.Liquid) scale.y *= Mathf.Lerp(1f, 2.2f, eased); // a drip stretching before it lets go
                    piece.T.localScale = scale;
                    var sway = dropKind == StoolKind.Soft ? new Vector3(Mathf.Sin(age * 2.3f), 0, Mathf.Cos(age * 1.7f)) * .006f * eased : Vector3.zero;
                    piece.T.position = piece.Origin + Vector3.down * piece.Hang * eased + sway;
                    if (t >= 1f)
                    {
                        // Stage 2: let go; gravity and momentum are the physics engine's.
                        piece.Falling = true;
                        piece.Body = piece.T.gameObject.AddComponent<Rigidbody>();
                        piece.Body.useGravity = true; piece.Body.interpolation = RigidbodyInterpolation.Interpolate;
                        piece.Body.linearVelocity = Vector3.down * (piece.Liquid ? .6f : .15f);
                        if (!piece.Liquid) piece.Body.angularVelocity = new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f)) * (dropKind == StoolKind.Soft ? .6f : 2f);
                    }
                    alive = true;
                }
                else if (piece.Falling && !piece.Landed)
                {
                    if (piece.T.position.y <= floor) { piece.Landed = true; if (piece.Body != null) Destroy(piece.Body); piece.Body = null; Land(piece, floor); }
                    else if (age > piece.Emerge + 4f) { piece.Landed = true; }
                    alive = true;
                }
                else if (age < piece.Emerge + 3.5f) alive = true;
                else { Destroy(piece.T.gameObject); piece.T = null; }
                pieces[i] = piece;
            }
            if (!alive) ClearPieces();
        }

        private void Land(Piece piece, float floor)
        {
            piece.T.position = new Vector3(piece.T.position.x, floor, piece.T.position.z);
            if (piece.Liquid) { piece.T.localScale = new Vector3(piece.Scale * 1.8f, piece.Scale * .25f, piece.Scale * 1.8f); }
            else piece.T.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            bool first = !landedAny; landedAny = true;
            bool butt = false;
            if (dropInBowl)
            {
                var water = new Vector3(piece.T.position.x, dropWater, piece.T.position.z);
                MessFx.Splash(water, new Color(.7f, .88f, 1f, .95f), piece.Liquid ? 6 : dropKind == StoolKind.Soft ? 26 : 12);
                if (first)
                {
                    butt = Random.value < ButtSplashChance;
                    if (butt)
                    {
                        MessFx.Splash(water, new Color(.75f, .9f, 1f, 1f), 30)?.Aim(Vector3.up, 3.2f);
                        SplashOverlay.Show(new Color(.7f, .9f, 1f, .85f), "噗通！屁股一凉", 1.4f);
                    }
                }
            }
            else MessFx.Splash(piece.T.position + Vector3.up * .02f, new Color(.5f, .27f, .08f, 1f), piece.Liquid ? 4 : 14);
            if (first) Dropped?.Invoke(dropInBowl, butt);
        }

        // ---------------------------------------------------------------- urination
        private void FaceUrinal()
        {
            var target = input.TargetCenter;
            movement.LookAt(target); // the view is the aim: start looking straight at the target
        }

        /// <summary>Crotch height and lead, in front of the standing body (never shown: the view faces the urinal).</summary>
        public Vector3 Origin => movement.transform.position + Vector3.up * .92f + movement.transform.forward * .2f;

        /// <summary>Launch velocity that reaches <paramref name="target"/> after a flight time set by distance.</summary>
        public static Vector3 Launch(Vector3 origin, Vector3 target, out float time)
        {
            time = Mathf.Clamp(Vector3.Distance(origin, target) / 2.4f, .2f, .6f);
            return (target - origin - .5f * Physics.gravity * time * time) / time;
        }

        /// <summary>Velocity after bouncing off a surface with normal <paramref name="normal"/>.</summary>
        public static Vector3 Rebound(Vector3 incident, Vector3 normal) => Vector3.Reflect(incident, normal.normalized) * Restitution;

        /// <summary>Cosine between the reversed incident ray and the wall normal: 1 is head-on (bounces straight back).</summary>
        public static float Incidence(Vector3 incident, Vector3 normal) => Vector3.Dot(-incident.normalized, normal.normalized);

        public void TickStream(float dt)
        {
            var art = MessArt.Current;
            if (Stream == null)
            {
                var go = new GameObject("Urine stream");
                Stream = go.AddComponent<LineRenderer>();
                Stream.positionCount = 18; Stream.useWorldSpace = true; Stream.numCapVertices = 2;
                Stream.widthCurve = AnimationCurve.Linear(0, StreamWidth, 1, StreamWidth * .6f);
                Stream.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (art != null) Stream.sharedMaterial = art.Urine;
            }
            Stream.enabled = true;
            var origin = Origin; var target = input.CurrentLanding;
            var velocity = Launch(origin, target, out float time);
            StreamOrigin = origin; StreamVelocity = velocity;
            for (int i = 0; i < Stream.positionCount; i++)
            {
                float t = time * i / (Stream.positionCount - 1);
                Stream.SetPosition(i, origin + velocity * t + .5f * Physics.gravity * t * t + Random.insideUnitSphere * .002f);
            }
            var incident = velocity + Physics.gravity * time;
            var wallNormal = WallNormal();
            bool inside = Vector3.Distance(target, input.TargetCenter) <= input.TargetRadius;
            StreamHit?.Invoke(target, inside);
            if (Time.time >= nextSplash)
            {
                nextSplash = Time.time + .12f;
                var tint = new Color(1f, .9f, .3f, .9f);
                if (inside) MessFx.Splash(target, tint, 3);
                else MessFx.Splash(target + wallNormal * .01f, tint, 5)?.Aim(Rebound(incident, wallNormal).normalized, Rebound(incident, wallNormal).magnitude);
            }
            // Off target and nearly head-on: the rebound comes back at the player.
            bool back = !inside && Incidence(incident, wallNormal) >= SelfSplashCosine;
            offTargetBack = back ? offTargetBack + dt : 0;
            if (!selfSplashed && offTargetBack >= SelfSplashSeconds) SplashSelf();
        }

        private Vector3 WallNormal()
        {
            var toPlayer = Vector3.ProjectOnPlane(movement.transform.position - input.TargetCenter, Vector3.up);
            return toPlayer.sqrMagnitude > .0001f ? toPlayer.normalized : -movement.transform.forward;
        }

        public void SplashSelf()
        {
            selfSplashed = true;
            var sync = GetComponent<FacilitySync>();
            if (sync != null && sync.IsSpawned) sync.ReportSplash();
            else BodyEvents.Splash(movement, BodyEvents.SplashKind.OwnRebound);
            SplashOverlay.Show(new Color(1f, .9f, .25f, .8f), "溅到自己了！", 1.2f);
            SelfSplashed?.Invoke();
        }

        private void HideStream() { if (Stream != null) Stream.enabled = false; }
        private void OnDestroy() { if (Stream != null) Destroy(Stream.gameObject); ClearPieces(); FadeBlockers(null); }
    }

    /// <summary>Gold sparkles while the explicit comfort boost runs (GLOSSARY Comfort); shown on every peer.</summary>
    public sealed class ComfortSparkle : MonoBehaviour
    {
        private PlayerNeeds needs;
        private float last, next;
        public int Bursts { get; private set; }
        private void Awake() => needs = GetComponent<PlayerNeeds>();
        private void Update()
        {
            if (needs == null) return;
            float boost = needs.ComfortBoostRemaining;
            var head = transform.position + Vector3.up * 1.9f;
            if (boost > last + 5f) { MessFx.Sparkle(head); MessFx.Sparkle(head + Vector3.down * .4f); Bursts++; next = Time.time + .7f; }
            else if (boost > 0 && Time.time >= next) { MessFx.Burst(head + Random.insideUnitSphere * .3f, MessArt.Current != null ? MessArt.Current.Sparkle : null, new Color(1, .9f, .5f), 3, .5f, .14f, -.05f, 1f); next = Time.time + .7f; }
            last = boost;
        }
    }

    /// <summary>A brief full-screen splash: droplets on the "lens" and a caption. Local only.</summary>
    public sealed class SplashOverlay : MonoBehaviour
    {
        public static SplashOverlay Current { get; private set; }
        private Image[] drops;
        private Text caption;
        private float age, life;
        private Color tint;

        public static void Show(Color tint, string text, float seconds)
        {
            if (RuntimeMode.Dedicated) return;
            if (Current == null)
            {
                var go = new GameObject("Splash overlay");
                var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 60;
                Current = go.AddComponent<SplashOverlay>();
                Current.Build(canvas);
            }
            Current.tint = tint; Current.age = 0; Current.life = seconds; Current.caption.text = text;
            Current.gameObject.SetActive(true);
            foreach (var d in Current.drops)
            {
                var rect = d.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(Random.Range(.05f, .95f), Random.Range(.05f, .95f));
                float size = Random.Range(60f, 190f); rect.sizeDelta = new Vector2(size, size * Random.Range(.8f, 1.3f));
            }
        }

        private void Build(Canvas canvas)
        {
            var art = MessArt.Current;
            Sprite sprite = null;
            if (art != null && art.Splash != null && art.Splash.mainTexture is Texture2D texture)
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
            drops = new Image[10];
            for (int i = 0; i < drops.Length; i++)
            {
                var d = new GameObject("Drop", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                d.transform.SetParent(canvas.transform, false); d.sprite = sprite; d.raycastTarget = false;
                drops[i] = d;
            }
            caption = new GameObject("Caption", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            caption.transform.SetParent(canvas.transform, false);
            caption.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            caption.fontSize = 46; caption.fontStyle = FontStyle.Bold; caption.alignment = TextAnchor.MiddleCenter; caption.raycastTarget = false;
            var rect = caption.rectTransform; rect.anchorMin = new Vector2(.5f, .62f); rect.anchorMax = new Vector2(.5f, .62f); rect.sizeDelta = new Vector2(900, 120);
            caption.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, .7f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            float a = Mathf.Clamp01(1 - age / life);
            foreach (var d in drops) { var c = tint; c.a *= a; d.color = c; d.rectTransform.anchoredPosition += Vector2.down * 30 * Time.deltaTime; }
            caption.color = new Color(1, 1, 1, a);
            caption.rectTransform.localScale = Vector3.one * (1 + .25f * Mathf.Exp(-age * 8));
            if (age >= life) gameObject.SetActive(false);
        }
    }
}
