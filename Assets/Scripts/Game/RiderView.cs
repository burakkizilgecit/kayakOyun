using UnityEngine;

namespace SledSurfers
{
    /// Kızak, sürücü, elde bardak ve bardağı yakından gösteren küçük kamera.
    /// Bardak ve sıvı, oyunda okunabilsin diye gerçek boyutun GlassScale katı çizilir.
    public class RiderView
    {
        public const float GlassScale = 2.4f;

        public readonly Transform root;
        public readonly Camera glassCam;

        readonly Transform hand, handBall, glassPivot, glassShell, liquidBody, liquidSurface;
        readonly Transform torso, glassArm, ropeArm;
        readonly Transform leftWing, rightWing, rope;
        // Aşama görünümleri
        readonly SledModel.Parts sledParts;
        readonly Material wingMat, wingletMat, rocketBodyMat, rocketTrimMat;
        public static readonly Color[] RunnerColors = { new Color(0.25f, 0.27f, 0.3f), new Color(0.78f, 0.8f, 0.85f), new Color(0.12f, 0.13f, 0.18f), new Color(1f, 0.76f, 0.18f) };
        public static readonly Color[] WingColors = { new Color(0.95f, 0.95f, 0.98f), new Color(0.35f, 0.7f, 1f), new Color(1f, 0.8f, 0.25f) };
        public static readonly Color[] WingletColors = { new Color(1f, 0.5f, 0.15f), Color.white, new Color(0.9f, 0.2f, 0.15f) };
        static readonly Color[] RocketBodies = { new Color(0.95f, 0.95f, 0.97f), new Color(0.95f, 0.95f, 0.97f), new Color(0.12f, 0.12f, 0.15f), new Color(1f, 0.78f, 0.22f) };
        static readonly Color[] RocketTrims = { new Color(0.9f, 0.22f, 0.15f), new Color(0.15f, 0.45f, 0.95f), new Color(1f, 0.5f, 0.1f), new Color(0.85f, 0.15f, 0.12f) };
        readonly Transform lid, glassDeco;
        int builtGlass = -1;
        float builtSide;
        readonly Material glassMat;
        readonly Transform[] rockets = new Transform[2], flames = new Transform[2];
        // Roket efektleri: alev ve duman izi; son yakıt bitince roketler kızaktan kopup düşer.
        readonly ParticleSystem[] fire = new ParticleSystem[2], smoke = new ParticleSystem[2];
        readonly Vector3[] rocketHome = new Vector3[2], rocketVel = new Vector3[2], rocketSpin = new Vector3[2];
        bool rocketsDetached, wasFiring;
        float detachClock;
        readonly Mesh rocketMesh;
        int rocketLook = -1;
        float flameSize = 1f;

        static readonly Color[] GlassTints =
        {
            new Color(0.85f, 0.95f, 1f, 0.28f), new Color(1f, 0.92f, 0.75f, 0.4f), new Color(0.75f, 0.8f, 0.88f, 0.45f),
            new Color(0.75f, 1f, 0.82f, 0.32f), new Color(0.88f, 0.78f, 1f, 0.38f),
        };
        float lidClock = -1f;   // < 0: kapak bardakta; >= 0: fırlayıp düşüyor
        static readonly Vector3 TorsoPivot = new Vector3(0f, 0.42f, -0.2f);   // kalça
        readonly Material steel;
        SledConfig cfg;
        float side = 1f;
        float punch;
        float wingSpan = -1f;

        // Su birikintisinde raylardan saçılan damlalar
        readonly ParticleSystem[] spray = new ParticleSystem[2];
        readonly Material dropMat;

        // Çarpma: karakter kızaktan fırlar, yerde yuvarlanır, oturup gülümser.
        bool crashing;
        int crashPhase;
        float crashClock, phaseClock, spinRate, haTimer;
        Vector3 crashVel, spinAxis;
        Quaternion fromRot;
        readonly ParticleSystem dust;

        public bool Crashing => crashing;
        public Vector3 CrashFocus => avatar != null && crashing ? avatar.Hips.position : root.position;

        // Karakter modeli; yüklenemezse basit (küp/küre) gövde görünür.
        AvatarRig avatar;
        readonly System.Collections.Generic.List<GameObject> simpleBody = new System.Collections.Generic.List<GameObject>();

        public RiderView()
        {
            root = new GameObject("Rider").transform;

            steel = Mats.Solid(new Color(0.25f, 0.27f, 0.3f), 0.6f);
            var jacket = Mats.Solid(new Color(0.18f, 0.45f, 0.85f));
            var pants = Mats.Solid(new Color(0.2f, 0.22f, 0.32f));
            var skin = Mats.Solid(new Color(1f, 0.8f, 0.65f));
            var hat = Mats.Solid(new Color(1f, 0.72f, 0.1f));
            wingMat = Mats.Solid(new Color(0.95f, 0.95f, 0.98f), 0.3f);
            var wing = wingMat;

            // Kızak: sörf tahtası + raylar; burundan sürücünün eline ip
            sledParts = SledModel.Build(root, steel);
            rope = Mats.Prim(PrimitiveType.Cylinder, "Rope", root, Vector3.zero, Vector3.one, Mats.Solid(new Color(1f, 0.76f, 0.2f), 0.2f));

            // Kanatlar: mesh Apply'da kanat boyuna göre üretilir; uçta turuncu dikey kanatçık.
            wingletMat = Mats.Solid(new Color(1f, 0.5f, 0.15f), 0.35f);
            var winglet = wingletMat;
            leftWing = NewWing("Wing L", wing, winglet);
            rightWing = NewWing("Wing R", wing, winglet);

            // Sürücü (oturur, bacaklar önde). Gövde kalçadan döner: ivmeyle öne-arkaya savrulur.
            simpleBody.Add(Mats.Prim(PrimitiveType.Cube, "Legs", root, new Vector3(0f, 0.38f, 0.25f), new Vector3(0.38f, 0.16f, 0.62f), pants).gameObject);
            torso = new GameObject("Torso").transform;
            torso.SetParent(root, false);
            torso.localPosition = TorsoPivot;
            simpleBody.Add(torso.gameObject);
            Mats.Prim(PrimitiveType.Capsule, "Body", torso, new Vector3(0f, 0.26f, 0f), new Vector3(0.48f, 0.36f, 0.38f), jacket);
            Mats.Prim(PrimitiveType.Sphere, "Head", torso, new Vector3(0f, 0.8f, 0.02f), Vector3.one * 0.34f, skin);
            Mats.Prim(PrimitiveType.Sphere, "Hat", torso, new Vector3(0f, 0.91f, 0f), new Vector3(0.37f, 0.2f, 0.37f), hat);

            glassArm = Mats.Prim(PrimitiveType.Cylinder, "Glass Arm", root, Vector3.zero, Vector3.one, jacket);
            ropeArm = Mats.Prim(PrimitiveType.Cylinder, "Rope Arm", root, Vector3.zero, Vector3.one, jacket);
            simpleBody.Add(glassArm.gameObject);
            simpleBody.Add(ropeArm.gameObject);

            hand = new GameObject("Hand").transform;
            hand.SetParent(root, false);

            glassPivot = new GameObject("Glass").transform;
            glassPivot.SetParent(root, false);
            handBall = Mats.Prim(PrimitiveType.Sphere, "Fist", glassPivot, Vector3.zero, Vector3.one * 0.1f, skin, null, Mats.GlassLayer);
            glassMat = Mats.Glass();
            glassShell = Mats.Prim(PrimitiveType.Cylinder, "Shell", glassPivot, Vector3.zero, Vector3.one, glassMat, null, Mats.GlassLayer);
            // Bardak modeline göre kulp, bant, pipet vb. (Apply'da kurulur)
            glassDeco = new GameObject("Glass Deco").transform;
            glassDeco.SetParent(glassPivot, false);

            // Roketler: kızağın iki yanında, arkaya bakan alevlerle
            var rocketBody = rocketBodyMat = Mats.Solid(new Color(0.95f, 0.95f, 0.97f), 0.5f);
            var rocketRed = rocketTrimMat = Mats.Solid(new Color(0.9f, 0.22f, 0.15f), 0.4f);
            var flameMat = Mats.Solid(new Color(1f, 0.6f, 0.1f));
            flameMat.EnableKeyword("_EMISSION");
            flameMat.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.05f) * 1.6f);
            rocketMesh = SledModel.RocketMesh();
            for (int s = 0; s < 2; s++)
            {
                float x = s == 0 ? -0.46f : 0.46f;
                var r = new GameObject("Rocket").transform;
                r.SetParent(root, false);
                r.localPosition = new Vector3(x, 0.33f, -0.42f);
                var look = new GameObject("Look").transform;   // seviyeye göre yeniden kurulur (PartVisuals.BuildRocket)
                look.SetParent(r, false);
                flames[s] = Mats.Prim(PrimitiveType.Sphere, "Flame", r, new Vector3(0f, 0f, -0.36f), new Vector3(0.1f, 0.1f, 0.3f), flameMat);
                rockets[s] = r;
                rocketHome[s] = r.localPosition;
                fire[s] = Trail(r, new Color(1f, 0.62f, 0.12f), true, 0.09f, 0.22f, 0.25f, 150);
                smoke[s] = Trail(r, new Color(0.55f, 0.55f, 0.58f), false, 0.12f, 0.35f, 0.9f, 220);
            }
            // Su sıçraması: her rayın arkasından yana-yukarı damla yelpazesi
            // Köpüklü su: neredeyse opak açık mavi-beyaz damlalar (şeffaf küçük damlalar uzaktan görünmüyordu).
            var drop = dropMat = Mats.Solid(new Color(0.84f, 0.92f, 1f), 0.92f);
            // Uzun damla: 6 köşeli, uçuş yönünde uzatılmış elmas (parçacık hızına hizalanır; yüzlerce damla telefonu yormaz).
            var dropMesh = new Mesh { name = "Drop" };
            dropMesh.vertices = new[]
            {
                new Vector3(0f, 0f, 1.3f), new Vector3(0.5f, 0f, 0f), new Vector3(0f, 0.5f, 0f),
                new Vector3(-0.5f, 0f, 0f), new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0f, -1.3f),
            };
            dropMesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1, 5, 2, 1, 5, 3, 2, 5, 4, 3, 5, 1, 4 };
            dropMesh.RecalculateNormals();
            dropMesh.RecalculateBounds();
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1f : 1f;
                var go = new GameObject("Spray");
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(0.26f * side, 0.08f, -0.55f);
                go.transform.localRotation = Quaternion.Euler(-50f, 180f - 40f * side, 0f);
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.playOnAwake = false;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
                main.gravityModifier = 1.3f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 900;
                var em = ps.emission;
                em.rateOverTime = 0f;
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 28f;
                shape.radius = 0.06f;
                // Damlalar kızağın hızının bir kısmını alır: yanında yelpaze gibi açılır, hemen geride kalmaz.
                var inherit = ps.inheritVelocity;
                inherit.enabled = true;
                inherit.mode = ParticleSystemInheritVelocityMode.Initial;
                inherit.curve = new ParticleSystem.MinMaxCurve(0.65f);
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
                var pr = go.GetComponent<ParticleSystemRenderer>();
                pr.renderMode = ParticleSystemRenderMode.Mesh;
                pr.mesh = dropMesh;
                pr.alignment = ParticleSystemRenderSpace.Velocity;
                pr.sharedMaterial = drop;
                pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ps.Play();
                spray[s] = ps;
            }

            // Toz bulutu (çarpmada yere değişte)
            {
                var go = new GameObject("Dust");
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.playOnAwake = false;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
                main.startRotation3D = true;
                main.gravityModifier = -0.05f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 200;
                var em = ps.emission;
                em.rateOverTime = 0f;
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = 0.25f;
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));
                var pr = go.GetComponent<ParticleSystemRenderer>();
                pr.renderMode = ParticleSystemRenderMode.Mesh;
                pr.mesh = Mats.PrimitiveMesh(PrimitiveType.Sphere);
                pr.sharedMaterial = Mats.Solid(new Color(0.86f, 0.78f, 0.62f), 0.0f);
                pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ps.Play();
                dust = ps;
            }

            var milk = Mats.Solid(new Color(0.98f, 0.97f, 0.92f), 0.5f);
            liquidBody = Mats.Prim(PrimitiveType.Cylinder, "Milk", glassPivot, Vector3.zero, Vector3.one, milk, null, Mats.GlassLayer);
            liquidSurface = Mats.Prim(PrimitiveType.Cylinder, "Milk Surface", glassPivot, Vector3.zero, Vector3.one, milk, null, Mats.GlassLayer);
            lid = Mats.Prim(PrimitiveType.Cylinder, "Lid", glassPivot, Vector3.zero, Vector3.one,
                            Mats.Solid(new Color(1f, 0.55f, 0.12f), 0.5f), null, Mats.GlassLayer);

            // Bardağı yakından gösteren köşe kamerası
            var camGo = new GameObject("Glass Camera");
            camGo.transform.SetParent(root, false);
            glassCam = camGo.AddComponent<Camera>();
            glassCam.cullingMask = 1 << Mats.GlassLayer;
            glassCam.clearFlags = CameraClearFlags.SolidColor;
            glassCam.backgroundColor = new Color(1f, 0.95f, 0.86f);
            glassCam.fieldOfView = 32f;
            glassCam.nearClipPlane = 0.05f;
            glassCam.farClipPlane = 5f;
            glassCam.depth = 1f;
        }

        /// 6 aşamayı (0..5) 4 renk takımına eşler.
        public static int StageColor(int stage) => stage <= 1 ? 0 : stage == 2 ? 1 : stage == 3 ? 2 : 3;

        Transform NewWing(string name, Material body, Material tip)
        {
            var w = new GameObject(name).transform;
            w.SetParent(root, false);
            w.gameObject.AddComponent<MeshFilter>();
            w.gameObject.AddComponent<MeshRenderer>().sharedMaterial = body;
            Mats.Prim(PrimitiveType.Cube, "Winglet", w, Vector3.zero, Vector3.one, tip);
            return w;
        }

        static void FitWing(Transform w, float span, float chord, float s)
        {
            var mf = w.GetComponent<MeshFilter>();
            if (mf.sharedMesh != null) Object.Destroy(mf.sharedMesh);
            mf.sharedMesh = SledModel.WingMesh(span, chord, s);
            w.localPosition = new Vector3(0.3f * s, 0.29f, -0.08f);
            var tip = w.GetChild(0);
            tip.localPosition = new Vector3(span * s, 0.08f * span + 0.06f, -0.04f * chord);
            tip.localScale = new Vector3(0.025f, 0.14f, 0.42f * chord);
        }

        /// Kızaktaki karakteri değiştirir (AvatarLibrary model adı).
        public void SetAvatar(string modelName)
        {
            avatar?.Destroy();
            var saved = root.localScale;
            root.localScale = Vector3.one;
            avatar = new AvatarRig(root, modelName);
            root.localScale = saved;
            foreach (var go in simpleBody) go.SetActive(!avatar.Valid);
            if (cfg != null) Apply(cfg, side >= 0f ? 1 : -1);
        }

        public void DebugGlassFull(bool full)
        {
            glassCam.rect = full ? new Rect(0f, 0f, 1f, 1f) : side > 0f ? new Rect(0.68f, 0.03f, 0.29f, 0.2f) : new Rect(0.03f, 0.03f, 0.29f, 0.2f);
        }

        /// Su birikintisinde: intensity 0..1 (hız), enter: suya ilk giriş anı (büyük sıçrama).
        public void Splash(float intensity, bool enter, bool mud = false)
        {
            if (intensity > 0f) dropMat.color = mud ? new Color(0.42f, 0.29f, 0.17f) : new Color(0.84f, 0.92f, 1f);
            foreach (var ps in spray)
            {
                var em = ps.emission;
                em.rateOverTime = 600f * intensity;
                if (enter) ps.Emit(Mathf.RoundToInt(120 + 180 * intensity));
            }
        }

        /// Çarpma anı: karakter kızağın hızının bir kısmıyla ileri-yukarı fırlar; kollar ve bacaklar savrulur,
        /// gövdesinin ortası etrafında takla atar.
        public void Crash(Vector3 impactVelocity)
        {
            if (avatar == null || !avatar.Valid || crashing) return;
            crashing = true;
            crashPhase = 0;
            crashClock = 0f;
            haTimer = 0.2f;
            var h = avatar.Holder;
            h.SetParent(null, true);
            avatar.ResetPose();
            var v = Vector3.ClampMagnitude(impactVelocity, 22f);
            crashVel = v * 0.5f + Vector3.up * (3.2f + 0.12f * v.magnitude);
            var flat = new Vector3(v.x, 0f, v.z);
            spinAxis = Vector3.Cross(Vector3.up, flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.forward);
            spinAxis = (spinAxis + Random.insideUnitSphere * 0.25f).normalized;
            spinRate = 480f + 10f * v.magnitude;
            glassPivot.gameObject.SetActive(false);   // bardak elden uçar
        }

        /// Yeni atıştan önce: karakter kızağa geri oturur.
        public void ResetCrash()
        {
            foreach (var t in haTexts) if (t.tm != null) Object.Destroy(t.tm.transform.parent.gameObject);
            haTexts.Clear();
            if (!crashing) return;
            crashing = false;
            avatar?.Reattach(root);
            glassPivot.gameObject.SetActive(true);
        }

        /// groundHeight(x, z): zemin yüksekliği. 0) savrularak uçar, seker, yuvarlanır; 1) kameraya dönüp doğrulur
        /// ve oturur; 2) kahkaha atar ("HA HA!" yazıları yükselir).
        public void TickCrash(float dt, System.Func<float, float, float> groundHeight, Vector3 cameraPos)
        {
            TickHaTexts(dt, cameraPos);
            if (!crashing || avatar == null) return;
            crashClock += dt;
            var h = avatar.Holder;
            Vector3 c = avatar.Hips.position;
            float ground = groundHeight(c.x, c.z);
            Vector3 toCam = cameraPos - c;
            toCam.y = 0f;
            var face = toCam.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toCam.normalized, Vector3.up) : h.rotation;

            if (crashPhase == 0)
            {
                crashVel += Vector3.down * 9.81f * dt;
                h.position += crashVel * dt;
                h.RotateAround(c, spinAxis, spinRate * dt);
                avatar.FlailPose(crashClock, Mathf.Clamp01(1.2f - crashClock * 0.35f));
                float low = avatar.LowestPoint();
                if (low < ground)
                {
                    h.position += Vector3.up * (ground - low);
                    if (crashVel.y < -1.2f) Dust(c, Mathf.Clamp01(-crashVel.y / 8f));
                    if (crashVel.y < 0f) crashVel.y = -crashVel.y * 0.38f;
                    crashVel.x *= 0.72f;
                    crashVel.z *= 0.72f;
                    // Yerde yuvarlanma: dönüş ekseni hızın yönüne dik, hızı yuvarlanma hızına uyar.
                    var hv = new Vector3(crashVel.x, 0f, crashVel.z);
                    if (hv.sqrMagnitude > 0.2f) spinAxis = Vector3.Cross(Vector3.up, hv.normalized);
                    spinRate = Mathf.Min(spinRate * 0.8f, hv.magnitude / 0.35f * Mathf.Rad2Deg);
                }
                bool slow = crashVel.magnitude < 1.6f && low <= ground + 0.08f;
                if ((crashClock > 1.0f && slow) || crashClock > 2.2f)
                {
                    crashPhase = 1;
                    phaseClock = 0f;
                    fromRot = h.rotation;
                }
            }
            else if (crashPhase == 1)
            {
                // Doğrulma: yattığı yerden kameraya dönerek oturur (pozdaki savrulma söner).
                phaseClock += dt;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phaseClock / 0.5f));
                h.rotation = Quaternion.Slerp(fromRot, face, k);
                avatar.FlailPose(crashClock, 0.25f * (1f - k));
                float bottom = Mathf.Lerp(avatar.LowestPoint(), avatar.SeatBottom(), k);
                h.position += Vector3.up * (ground - bottom);
                if (k >= 1f)
                {
                    crashPhase = 2;
                    phaseClock = 0f;
                    Dust(c, 0.4f);
                }
            }
            else
            {
                phaseClock += dt;
                h.rotation = Quaternion.Slerp(h.rotation, face, 3f * dt);
                avatar.LaughPose(phaseClock);
                avatar.Laugh(Mathf.Abs(Mathf.Sin(phaseClock * 15f)));
                // Oturur: kahkahayla gövde hafifçe zıplar.
                float hop = 0.025f * Mathf.Abs(Mathf.Sin(phaseClock * 7.5f));
                h.position += Vector3.up * (ground + hop - avatar.SeatBottom());
                haTimer -= dt;
                if (haTimer <= 0f)
                {
                    haTimer = 0.5f;
                    SpawnHa(avatar.HeadTop, cameraPos);
                }
            }
        }

        struct HaText
        {
            public TextMesh tm;
            public float age;
            public Vector3 drift;
        }

        readonly System.Collections.Generic.List<HaText> haTexts = new System.Collections.Generic.List<HaText>();
        static readonly Color[] HaColors = { new Color(1f, 0.82f, 0.2f), new Color(1f, 0.5f, 0.2f), new Color(1f, 0.35f, 0.55f) };

        void SpawnHa(Vector3 at, Vector3 cameraPos)
        {
            var go = new GameObject("HA");
            go.transform.position = at + new Vector3(Random.Range(-0.25f, 0.25f), 0f, 0f);
            var tm = WorldView.MakeText(go.transform, Vector3.zero, haTexts.Count % 2 == 0 ? "HA HA!" : "HA!", 0.035f,
                                        HaColors[haTexts.Count % HaColors.Length]);
            tm.fontStyle = FontStyle.Bold;
            haTexts.Add(new HaText { tm = tm, age = 0f, drift = new Vector3(Random.Range(-0.3f, 0.3f), 0.9f, 0f) });
        }

        void TickHaTexts(float dt, Vector3 cameraPos)
        {
            for (int i = haTexts.Count - 1; i >= 0; i--)
            {
                var t = haTexts[i];
                t.age += dt;
                if (t.tm == null || t.age > 1.3f)
                {
                    if (t.tm != null) Object.Destroy(t.tm.transform.parent.gameObject);
                    haTexts.RemoveAt(i);
                    continue;
                }
                var tr = t.tm.transform.parent;
                tr.position += t.drift * dt;
                tr.rotation = Quaternion.LookRotation(tr.position - cameraPos);
                float pop = t.age < 0.15f ? Mathf.Lerp(0.3f, 1.2f, t.age / 0.15f) : Mathf.Lerp(1.2f, 1f, (t.age - 0.15f) / 0.2f);
                tr.localScale = Vector3.one * pop;
                var col = t.tm.color;
                col.a = Mathf.Clamp01((1.3f - t.age) / 0.4f);
                t.tm.color = col;
                haTexts[i] = t;
            }
        }

        /// Yere değişte toz bulutu.
        void Dust(Vector3 at, float amount)
        {
            if (dust == null) return;
            dust.transform.position = at;
            dust.Emit(Mathf.RoundToInt(6 + 22 * amount));
        }

        /// Roket arkasında iz: dünya uzayında kalan, sönen/büyüyen küçük küreler (ateş parlak, duman gri).
        static ParticleSystem Trail(Transform rocket, Color color, bool glow, float size0, float size1, float life, int max)
        {
            var go = new GameObject(glow ? "Fire Trail" : "Smoke Trail");
            go.transform.SetParent(rocket, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.35f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // geriye doğru
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(glow ? 3f : 1f, glow ? 6f : 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(size0 * 0.7f, size0 * 1.3f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = glow ? 0f : -0.08f;
            main.maxParticles = max;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = glow ? 10f : 18f;
            shape.radius = 0.03f;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, size1 / size0));
            if (glow)
            {
                // Ateş: sarıdan kırmızıya döner
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var grad = new Gradient();
                grad.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.05f), 1f) },
                             new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                col.color = grad;
            }
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Mesh;
            pr.mesh = Mats.PrimitiveMesh(PrimitiveType.Sphere);
            var mat = Mats.Solid(color, 0.1f);
            if (glow)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 2f);
            }
            pr.sharedMaterial = mat;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        /// Yeni atıştan önce roketler kızağa geri takılır.
        public void ResetRockets()
        {
            rocketsDetached = false;
            wasFiring = false;
            for (int s = 0; s < 2; s++)
            {
                rockets[s].SetParent(root, false);
                rockets[s].localPosition = rocketHome[s];
                rockets[s].localRotation = Quaternion.identity;
                var e1 = fire[s].emission; e1.rateOverTime = 0f;
                var e2 = smoke[s].emission; e2.rateOverTime = 0f;
                fire[s].Clear();
                smoke[s].Clear();
            }
        }

        /// Roket ateşi ve son yakıt bittiğinde roketlerin kızaktan kopup dönerek düşmesi.
        void UpdateRockets(SledPhysics sled, bool firing, float dt)
        {
            for (int s = 0; s < 2; s++)
            {
                var e1 = fire[s].emission;
                e1.rateOverTime = firing ? 260f : 0f;
                var e2 = smoke[s].emission;
                e2.rateOverTime = firing ? 70f : 0f;
            }
            if (wasFiring && !firing && sled.rocketCharges == 0 && !rocketsDetached && rockets[0].gameObject.activeSelf)
            {
                rocketsDetached = true;
                detachClock = 0f;
                for (int s = 0; s < 2; s++)
                {
                    float side = s == 0 ? -1f : 1f;
                    rockets[s].SetParent(null, true);
                    rocketVel[s] = sled.velocity * 0.75f + root.right * side * 2.5f + Vector3.up * 2.5f;
                    rocketSpin[s] = new Vector3(Random.Range(-400f, 400f), Random.Range(-200f, 200f), side * 300f);
                }
            }
            wasFiring = firing;
            if (!rocketsDetached) return;
            detachClock += dt;
            for (int s = 0; s < 2; s++)
            {
                rocketVel[s] += Vector3.down * 9.81f * dt;
                rockets[s].position += rocketVel[s] * dt;
                rockets[s].Rotate(rocketSpin[s] * dt, Space.Self);
                if (detachClock > 2.5f) rockets[s].gameObject.SetActive(false);
            }
        }

        /// Kapağı bardağa geri takar.
        public void CloseLid()
        {
            lidClock = -1f;
            lid.gameObject.SetActive(true);
        }

        /// Kapak açılır: dönerek yukarı fırlar ve düşer.
        public void PopLid() => lidClock = 0f;

        /// Garajda yükseltme alınınca model kısa bir an zıplar.
        public void Punch() => punch = 1f;

        public void Apply(SledConfig config, int handSide)
        {
            cfg = config;
            side = handSide >= 0 ? 1f : -1f;

            bool wings = cfg.wingArea > 0f;
            leftWing.gameObject.SetActive(wings);
            rightWing.gameObject.SetActive(wings);
            if (wings)
            {
                const float chord = 0.55f;
                float span = cfg.wingArea / (2f * chord);
                if (!Mathf.Approximately(span, wingSpan))
                {
                    wingSpan = span;
                    FitWing(leftWing, span, chord, -1f);
                    FitWing(rightWing, span, chord, 1f);
                }
            }

            // Kızak altı yükseldikçe raylar çelikten parlak altına döner.
            // Aşama görünümü: her 4 seviyede kızak boyanır (2. aşamada spoiler), raylar çelik → gümüş → siyah krom → altın.
            // Kızak seviyeleri: 1 tutma ipi, 2 gümüş kayaklar, 3 arka tampon kanadı (spoiler), 4 küçük kanat, 5 büyük kanat.
            int rs = Mathf.Clamp(cfg.runnerStage, 0, 5);
            sledParts.SetStage(rs);
            rope.gameObject.SetActive(rs >= 1);
            int metal = rs >= 5 ? 3 : rs >= 4 ? 2 : rs >= 2 ? 1 : 0;
            steel.color = RunnerColors[metal];
            steel.SetFloat("_Glossiness", metal == 0 ? 0.6f : 0.9f);
            int ws = rs >= 5 ? 2 : rs >= 4 ? 1 : 0;
            wingMat.color = WingColors[ws];
            wingletMat.color = WingletColors[ws];
            // Roket: her tamamlanan seviyede yeni gövde (PartVisuals.BuildRocket); seviye içinde hafifçe büyür.
            int ks = Mathf.Clamp(cfg.rocketStage, 0, 5);
            if (ks != rocketLook)
            {
                rocketLook = ks;
                foreach (var r in rockets)
                {
                    var look = r.Find("Look");
                    for (int i = look.childCount - 1; i >= 0; i--) Object.Destroy(look.GetChild(i).gameObject);
                    var tail = PartVisuals.BuildRocket(look, ks, rocketMesh);
                    r.Find("Flame").localPosition = tail - new Vector3(0f, 0f, 0.04f);
                }
                flameSize = 1f + 0.18f * ks;
            }

            bool hasRockets = cfg.rocketCharges > 0;
            float rocketSize = 0.9f + 0.025f * Mathf.Clamp(cfg.rocketCharges > 0 ? (cfg.rocketThrust - 80f) / 12f % 4f : 0f, 0f, 4f);
            foreach (var r in rockets)
            {
                if (!rocketsDetached) r.gameObject.SetActive(hasRockets);
                r.localScale = Vector3.one * rocketSize;
            }

            // Bardağı tutan el: karakterin omzunun önünde, kolunun rahatça uzandığı yerde.
            if (avatar != null && avatar.Valid)
            {
                var shoulder = avatar.ShoulderLocal(side, root, out float armLength);
                hand.localPosition = shoulder + new Vector3(0.1f * side, -0.02f, armLength * 0.78f);
            }
            else hand.localPosition = new Vector3(0.45f * side, 0.86f, 0.32f);
            float d = cfg.glassRadius * 2f * GlassScale;
            float h = cfg.glassHeight * GlassScale;
            glassMat.color = GlassTints[Mathf.Clamp(cfg.glassModel, 0, GlassTints.Length - 1)];
            if (cfg.glassModel != builtGlass || side != builtSide)
            {
                builtGlass = cfg.glassModel;
                builtSide = side;
                for (int i = glassDeco.childCount - 1; i >= 0; i--) Object.Destroy(glassDeco.GetChild(i).gameObject);
                var mf = glassShell.GetComponent<MeshFilter>();
                if (mf.sharedMesh != null && mf.sharedMesh.name == "Lathe") Object.Destroy(mf.sharedMesh);
                mf.sharedMesh = GlassModels.Build(cfg.glassModel, d, h, side, glassDeco, glassMat, Mats.GlassLayer);
                glassShell.localScale = Vector3.one;
                glassShell.localPosition = Vector3.zero;
            }

            // Yumruk bardağı gövdeye bakan yandan kavrar.
            handBall.localPosition = new Vector3(-side * (d * 0.5f + 0.04f), h * 0.4f, 0f);

            float camSide = side;
            // Uzun bardaklarda kamera geri çekilir, bardak köşe görüntüsüne sığar.
            float camDist = Mathf.Max(1f, h / 0.33f);
            glassCam.transform.localPosition = hand.localPosition + new Vector3(0.32f * camSide, 0.3f, -0.62f) * camDist;
            glassCam.rect = side > 0f ? new Rect(0.68f, 0.03f, 0.29f, 0.2f) : new Rect(0.03f, 0.03f, 0.29f, 0.2f);
        }

        /// torsoPitch: + = öne eğilme (derece), torsoRoll: + = sağa yatma, shake: gerilim titremesi (m)
        public void Sync(SledPhysics sled, LiquidSim liquid, float glassPitch, float glassRoll,
                         float torsoPitch, float torsoRoll, Vector3 shake)
        {
            root.SetPositionAndRotation(sled.position + shake, sled.Orientation);
            bool firing = sled.RocketFiring;
            UpdateRockets(sled, firing, Time.deltaTime);
            foreach (var flame in flames)
            {
                flame.gameObject.SetActive(firing);
                if (firing) flame.localScale = new Vector3(0.1f, 0.1f, 0.26f + 0.12f * Random.value) * flameSize;
            }
            punch = Mathf.MoveTowards(punch, 0f, Time.deltaTime * 2.5f);
            root.localScale = Vector3.one * (1f + 0.14f * Mathf.Sin(punch * Mathf.PI * 2f) * punch);
            torso.localRotation = Quaternion.Euler(torsoPitch, 0f, -torsoRoll);
            bool rigged = avatar != null && avatar.Valid;

            // Kollar: omuz gövdeyle hareket eder, el bardakta / kızağın ipinde kalır.
            Vector3 glassShoulder = root.InverseTransformPoint(torso.TransformPoint(new Vector3(0.22f * side, 0.5f, 0.05f)));
            Vector3 ropeShoulder = root.InverseTransformPoint(torso.TransformPoint(new Vector3(-0.22f * side, 0.5f, 0.05f)));
            if (!rigged)
            {
                Mats.PlaceBetween(glassArm, glassShoulder, hand.localPosition, 0.11f);
                Mats.PlaceBetween(ropeArm, ropeShoulder, new Vector3(-0.18f * side, 0.36f, 0.6f), 0.1f);
            }

            // Kolun esnemesi bardağın konumunu biraz oynatır; yönelimi el (telefon) belirler.
            Vector3 armLocal = Quaternion.Inverse(root.rotation) * liquid.ArmOffset;
            glassPivot.localPosition = hand.localPosition + armLocal + new Vector3(0f, -0.06f, 0f);
            glassPivot.localRotation = Quaternion.Euler(glassPitch, 0f, -glassRoll);

            float top = cfg.glassHeight * GlassScale;
            float rimD = cfg.glassRadius * 2f * GlassScale;
            lid.localScale = new Vector3(rimD * 1.12f, 0.012f, rimD * 1.12f);
            if (lidClock < 0f)
            {
                lid.localPosition = new Vector3(0f, top + 0.012f, 0f);
                lid.localRotation = Quaternion.identity;
            }
            else
            {
                lidClock += Time.deltaTime;
                float t = lidClock;
                lid.localPosition = new Vector3(0.5f * t * side, top + 1.6f * t - 3f * t * t, -0.3f * t);
                lid.localRotation = Quaternion.Euler(720f * t, 0f, 260f * t * side);
                if (t > 1.2f) lid.gameObject.SetActive(false);
            }

            float d = cfg.glassRadius * 2f * GlassScale * 0.92f;
            float f = Mathf.Max(liquid.fill, 0f) * GlassScale;
            bool any = f > 0.002f;
            liquidBody.gameObject.SetActive(any);
            liquidSurface.gameObject.SetActive(any);
            if (any)
            {
                float rise = Mathf.Min(liquid.slope.magnitude * cfg.glassRadius * GlassScale, f);
                float body = Mathf.Max(f - rise, 0.002f);
                liquidBody.localScale = new Vector3(d, body * 0.5f, d);
                liquidBody.localPosition = new Vector3(0f, body * 0.5f, 0f);

                var normal = new Vector3(-liquid.slope.x, 1f, -liquid.slope.y).normalized;
                liquidSurface.localPosition = new Vector3(0f, f, 0f);
                liquidSurface.localRotation = Quaternion.FromToRotation(Vector3.up, normal);
                liquidSurface.localScale = new Vector3(d, 0.003f, d);
            }

            Vector3 ropeHand = new Vector3(-0.2f * side, 0.36f, 0.62f);
            if (rigged && !crashing)
            {
                avatar.Pose(torsoPitch, torsoRoll, side, glassPivot.TransformPoint(handBall.localPosition), root.TransformPoint(ropeHand), root);
                ropeHand = root.InverseTransformPoint(avatar.RopeFist(side));
            }
            Mats.PlaceBetween(rope, SledModel.NoseEye, ropeHand, 0.022f);

            glassCam.transform.LookAt(glassPivot.position + glassPivot.up * cfg.glassHeight * GlassScale * 0.5f, root.up);
        }
    }
}
