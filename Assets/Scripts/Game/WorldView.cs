using System.Collections.Generic;
using UnityEngine;

namespace SledSurfers
{
    /// Parkur profilinden zemin, korkuluk, çimen, ağaçlar, engeller, tabelalar ve sapanı üretir.
    public class WorldView
    {
        // 2026-10-19 (kullanıcı: sapan oyuncudan çok büyük): ±1.7 m / 1.75 m -> ±1.15 m / 1.2 m, direkler orantılı ince.
        const float SlingPostX = 1.15f;
        const float SlingPostZ = 0.3f;
        const float SlingTop = 1.2f;

        const int GuideDots = 14;

        readonly Transform root;
        readonly RuntimeResources resources;
        readonly SlingshotView sling;
        readonly Transform[] guide = new Transform[GuideDots];
        Vector3 pouchPos, pouchVel;
        Color bandBase = new Color(0.3f, 0.3f, 0.32f);
        float bandThickness = 1f;
        bool pouchAttached = true;
        readonly Transform bestFlag;
        readonly TextMesh bestText;
        readonly Transform stopFlag;
        readonly TextMesh stopText;
        float stopClock = -1f;
        readonly TrackProfile track;
        readonly List<Transform> flockRoots = new List<Transform>();
        readonly List<Transform[]> wings = new List<Transform[]>();
        readonly TrackTheme theme;

        public WorldView(TrackProfile track, TrackTheme theme)
        {
            this.track = track;
            this.theme = theme;
            root = new GameObject("World").transform;
            resources = RuntimeResources.For(root);

            Scenery.Build(root, track, theme);
            if (track.fragileBridges)
                foreach (var t in root.GetComponentsInChildren<Transform>())
                    if (t.parent != null && t.parent.name == "Stone Ledge")
                        ledgePieces.Add(new LedgePiece { t = t, pos = t.position, rot = t.rotation });

            // Sapan
            sling = new SlingshotView(root, SlingPostX, SlingPostZ, track.Height(SlingPostZ), SlingTop);
            pouchPos = sling.Rest;

            var dotMat = resources.Solid(Color.white, 0.6f);
            dotMat.EnableKeyword("_EMISSION");
            dotMat.SetColor("_EmissionColor", new Color(0.6f, 0.6f, 0.6f));
            for (int i = 0; i < GuideDots; i++)
                guide[i] = Mats.Prim(PrimitiveType.Cylinder, "Guide", root, Vector3.zero, new Vector3(0.28f, 0.02f, 0.28f), dotMat);
            ShowGuide(0f, 0f);

            // Kuş sürüleri: her biri ~10 kuş (gövde + iki kanat), sürünün merkezi fizikle aynı formülle salınır.
            var birdMat = resources.Solid(new Color(0.18f, 0.18f, 0.22f), 0.2f);
            var rnd = new System.Random(5);
            foreach (var f in track.flocks)
            {
                var flock = new GameObject("Flock").transform;
                flock.SetParent(root, false);
                var flaps = new List<Transform>();
                for (int b = 0; b < 10; b++)
                {
                    var bird = new GameObject("Bird").transform;
                    bird.SetParent(flock, false);
                    float a = (float)rnd.NextDouble() * 6.28f, r = (float)rnd.NextDouble() * f.radius * 0.75f;
                    bird.localPosition = new Vector3(Mathf.Cos(a) * r, ((float)rnd.NextDouble() - 0.5f) * 1.4f, Mathf.Sin(a) * r * 0.6f);
                    bird.localRotation = Quaternion.Euler(0f, 180f + ((float)rnd.NextDouble() - 0.5f) * 40f, 0f);
                    Mats.Prim(PrimitiveType.Sphere, "Body", bird, Vector3.zero, new Vector3(0.18f, 0.16f, 0.42f), birdMat);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var pivot = new GameObject("Wing").transform;
                        pivot.SetParent(bird, false);
                        Mats.Prim(PrimitiveType.Cube, "Feathers", pivot, new Vector3(0.28f * s, 0f, 0f), new Vector3(0.52f, 0.03f, 0.22f), birdMat);
                        flaps.Add(pivot);
                    }
                }
                flockRoots.Add(flock);
                wings.Add(flaps.ToArray());
            }

            BuildBoosts();

            // Yükselen hava (kullanıcı 2026-10-19: düz çizgiler çok basitti, dalgalı hava figürü olmalı): bölge boyunca
            // kıvrılarak yükselen yarı saydam hava şeritleri (dokusu yukarı akar, uçlarda incelir) ve aralarında girdap
            // gibi dönerek yükselen hava zerrecikleri. Renk temadan (lavda sıcak turuncu, çölde kum, Mars'ta pas).
            Color wind = theme.lava ? new Color(1f, 0.62f, 0.3f)
                       : theme.landscape == TrackTheme.Landscape.Dunes ? new Color(1f, 0.92f, 0.7f)
                       : theme.landscape == TrackTheme.Landscape.Craters ? new Color(1f, 0.76f, 0.6f)
                       : new Color(0.9f, 0.97f, 1f);
            if (track.updrafts.Count > 0)
            {
                windRibbonMat = Mats.Glass();
                windRibbonMat.color = new Color(wind.r, wind.g, wind.b, 0.55f);
                windRibbonMat.mainTexture = WindTexture();
                windRibbonMat.SetFloat("_Glossiness", 0.2f);
            }
            var moteMat = Mats.Glow(wind, wind * 0.7f);
            var wrnd = new System.Random(77);
            foreach (var u in track.updrafts)
            {
                float zc = (u.x + u.y) * 0.5f, len = u.y - u.x;
                var zone = new GameObject("Updraft").transform;
                zone.SetParent(root, false);
                zone.position = new Vector3(track.CenterX(zc), track.Height(zc) - 0.3f, zc);
                const int Ribbons = 6;
                for (int k = 0; k < Ribbons; k++)
                {
                    var rib = new GameObject("Wind Ribbon");
                    rib.transform.SetParent(zone, false);
                    float lz = -len * 0.5f + (k + 0.5f) * len / Ribbons + (float)(wrnd.NextDouble() - 0.5) * 4f;
                    float lx = (float)(wrnd.NextDouble() * 2 - 1) * track.halfWidth * 0.8f;
                    rib.transform.localPosition = new Vector3(lx, 0f, lz);
                    rib.AddComponent<MeshFilter>().sharedMesh = resources.Own(WindRibbon((float)wrnd.NextDouble() * 6.28f, 16f + (float)wrnd.NextDouble() * 8f));
                    var mr = rib.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = windRibbonMat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }
                // Girdaplı hava zerrecikleri
                var go = new GameObject("Updraft Motes");
                go.transform.SetParent(zone, false);
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.maxParticles = 300;
                var em = ps.emission;
                em.rateOverTime = 60f;
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(track.halfWidth * 2f, 1f, len);
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                // Bütün eğriler aynı modda (iki sabit arası) olmalı: karışık mod konsola hata yağdırıyordu.
                vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                vel.y = new ParticleSystem.MinMaxCurve(4f, 7f);
                vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
                vel.orbitalY = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);   // dönerek yükselir
                vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
                vel.radial = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 1.2f;
                noise.frequency = 0.35f;
                noise.scrollSpeed = 0.6f;
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var grad = new Gradient();
                grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                             new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
                col.color = grad;
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f));
                var pr = go.GetComponent<ParticleSystemRenderer>();
                pr.renderMode = ParticleSystemRenderMode.Billboard;
                pr.sharedMaterial = moteMat;
                pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ps.Play();
            }

            // Rekor bayrağı
            bestFlag = new GameObject("Best Flag").transform;
            bestFlag.SetParent(root, false);
            var gold = resources.Solid(new Color(1f, 0.78f, 0.1f), 0.5f);
            // Rekor işareti de küçük ve rakamsız (mesafe bayrağıyla aynı karar): altın bayrak; rekor ilerleme çubuğunda yazar.
            Mats.Prim(PrimitiveType.Cylinder, "Pole", bestFlag, new Vector3(0f, 0.85f, 0f), new Vector3(0.06f, 0.85f, 0.06f), resources.Solid(Color.white));
            Mats.Prim(PrimitiveType.Cube, "Flag", bestFlag, new Vector3(0.28f, 1.5f, 0f), new Vector3(0.5f, 0.32f, 0.04f), gold);
            bestText = MakeText(bestFlag, new Vector3(0f, 1.9f, 0f), "BEST", 0.06f, new Color(1f, 0.85f, 0.2f));
            bestText.gameObject.SetActive(false);
            bestFlag.gameObject.SetActive(false);

            // Atışın bittiği noktaya dikilen mesafe bayrağı
            stopFlag = new GameObject("Stop Flag").transform;
            stopFlag.SetParent(root, false);
            var red = resources.Solid(new Color(0.95f, 0.3f, 0.2f), 0.4f);
            // Küçük işaret bayrağı (kullanıcı 2026-10-10): üstünde rakam yok (bazen tam okunmuyordu); mesafe ekranda yazar.
            Mats.Prim(PrimitiveType.Cylinder, "Pole", stopFlag, new Vector3(0f, 0.75f, 0f), new Vector3(0.06f, 0.75f, 0.06f), resources.Solid(Color.white));
            Mats.Prim(PrimitiveType.Cube, "Flag", stopFlag, new Vector3(0.28f, 1.3f, 0f), new Vector3(0.5f, 0.32f, 0.04f), red);
            Mats.Prim(PrimitiveType.Sphere, "Knob", stopFlag, new Vector3(0f, 1.54f, 0f), Vector3.one * 0.12f, resources.Solid(new Color(1f, 0.8f, 0.2f), 0.6f));
            stopText = MakeText(stopFlag, new Vector3(0.28f, 1.3f, -0.04f), "", 0.05f, Color.white);
            stopText.gameObject.SetActive(false);
            stopFlag.gameObject.SetActive(false);
        }

        /// Atış bitince en uzak noktaya bayrak dikilir ve mesafe yazılır.
        public void PlantFlag(Vector3 groundPos, int meters)
        {
            stopFlag.position = groundPos;
            stopText.text = TrackLibrary.Show(meters) + " m";
            stopFlag.localScale = Vector3.zero;
            stopFlag.gameObject.SetActive(true);
            stopClock = 0f;
        }

        public void ClearFlag()
        {
            stopFlag.gameObject.SetActive(false);
            stopClock = -1f;
        }

        /// Bayrak yere saplanıyormuş gibi yukarıdan iner ve hafifçe sallanır.
        public void UpdateFlag(float dt, Vector3 cameraPos)
        {
            if (stopClock < 0f) return;
            stopClock += dt;
            float t = Mathf.Clamp01(stopClock / 0.35f);
            float s = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);
            stopFlag.localScale = Vector3.one * Mathf.Max(s, 0f);
            float wobble = 6f * Mathf.Exp(-stopClock * 3f) * Mathf.Sin(stopClock * 18f);
            Vector3 toCam = cameraPos - stopFlag.position;
            toCam.y = 0f;
            float yaw = toCam.sqrMagnitude > 0.01f ? Quaternion.LookRotation(-toCam).eulerAngles.y : 0f;
            stopFlag.rotation = Quaternion.Euler(0f, yaw, wobble);
        }

        public Transform Root => root;

        // ---------------------------------------------------------------- sürpriz engeller
        /// Görünüşler: kartopu (yuvarlanır), düşüş gölgesi / meteor halkası (büyür, nabız), düşen kaya / alevli meteor.
        class HazardView { public Transform body, marker, trail; public Material markerMat; }
        HazardView[] hazardViews = new HazardView[0];
        HazardState[] shownHazards;
        Material snowMat, rockMat, meteorMat, flameMat;

        public void UpdateHazards(SledPhysics sled)
        {
            if (sled.hazards.Length == 0) return;
            if (!ReferenceEquals(shownHazards, sled.hazards))
            {
                shownHazards = sled.hazards;
                if (hazardViews.Length != sled.hazards.Length) BuildHazardViews(sled.hazards);
                foreach (var v in hazardViews) { v.body.gameObject.SetActive(false); v.marker.gameObject.SetActive(false); if (v.trail) v.trail.gameObject.SetActive(false); }
            }
            for (int i = 0; i < sled.hazards.Length; i++)
            {
                var h = sled.hazards[i];
                var v = hazardViews[i];
                bool live = h.phase == 1 || h.phase == 2 || (h.phase == 3 && h.kind != HazardKind.Snowball && h.pos.z > sled.position.z - 30f);
                if (h.kind == HazardKind.Snowball)
                {
                    v.body.gameObject.SetActive(h.phase == 1 || h.hit);
                    if (!v.body.gameObject.activeSelf) continue;
                    v.body.position = h.pos;
                    v.body.localScale = Vector3.one * h.radius * 2f;
                    v.body.rotation = Quaternion.Euler(-(h.t * HazardState.SnowballSpeed) / h.radius * Mathf.Rad2Deg, 0f, 0f);
                    continue;
                }
                v.body.gameObject.SetActive(live);
                bool falling = h.phase == 1;
                v.marker.gameObject.SetActive(falling);
                if (v.trail) v.trail.gameObject.SetActive(falling && h.kind == HazardKind.Meteor);
                if (!live) continue;
                float u = Mathf.Clamp01(h.t / h.FallDuration);
                if (falling)
                {
                    // Gölge / halka: büyür, meteorda kırmızı nabız
                    float k = Mathf.Lerp(0.35f, 1.15f, u);
                    v.marker.position = h.pos + Vector3.up * 0.06f;
                    v.marker.localScale = new Vector3(h.radius * 2.2f * k, 0.01f, h.radius * 2.2f * k);
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
                    v.markerMat.color = h.kind == HazardKind.Meteor ? new Color(1f, 0.12f, 0.08f, 0.35f + 0.4f * pulse * u)
                                                                    : new Color(0f, 0f, 0f, 0.15f + 0.4f * u);
                }
                // Gövde: gökten iner (ivmelenerek); meteor çaprazdan, arkasında alev izi
                float drop = 28f * (1f - u * u);
                // Meteor yandan çapraz iner: arkadan bakan kamerada alev izi ekranda eğik bir çizgi olur (önden gelince iz kameraya
                // doğru uzanıp turuncu bir leke gibi görünüyordu).
                Vector3 from = h.kind == HazardKind.Meteor ? new Vector3(-1f, 0f, 0.25f).normalized * drop : Vector3.up * drop;
                v.body.position = h.pos + Vector3.up * (h.radius * 0.7f) + (falling ? from + Vector3.up * drop * (h.kind == HazardKind.Meteor ? 0.9f : 0f) : Vector3.zero);
                v.body.localScale = h.kind == HazardKind.Meteor ? Vector3.one * h.radius * 0.9f
                                                                : new Vector3(h.radius * 2f, h.radius * 1.6f, h.radius * 1.8f);
                if (v.trail && falling)
                {
                    // İnce alev izi: gövdenin arkasında (geldiği yöne doğru) uzanır
                    var dir = (v.body.position - (h.pos + Vector3.up * h.radius * 0.7f)).normalized;
                    v.trail.position = v.body.position + dir * 2f;
                    v.trail.rotation = Quaternion.LookRotation(dir);
                    v.trail.localScale = new Vector3(h.radius * 0.35f, h.radius * 0.35f, 4f);
                }
            }
        }

        void BuildHazardViews(HazardState[] hs)
        {
            snowMat ??= resources.Solid(new Color(0.95f, 0.97f, 1f), 0.35f);
            rockMat ??= resources.Solid(Color.Lerp(theme.rock, Color.black, 0.15f), 0.15f);
            meteorMat ??= resources.Glow(new Color(0.22f, 0.1f, 0.08f), new Color(0.9f, 0.25f, 0.05f), 0.2f);   // kor gibi koyu kaya
            flameMat ??= resources.Own(new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.6f, 0.2f, 0.45f) });
            hazardViews = new HazardView[hs.Length];
            for (int i = 0; i < hs.Length; i++)
            {
                var v = new HazardView();
                var mat = hs[i].kind == HazardKind.Snowball ? snowMat : hs[i].kind == HazardKind.Meteor ? meteorMat : rockMat;
                v.body = Mats.Prim(PrimitiveType.Sphere, "Hazard", root, Vector3.zero, Vector3.one, mat);
                v.markerMat = resources.Own(new Material(Shader.Find("Sprites/Default")) { color = new Color(0f, 0f, 0f, 0.3f) });
                v.marker = Mats.Prim(PrimitiveType.Cylinder, "Hazard Marker", root, Vector3.zero, Vector3.one, v.markerMat);
                if (hs[i].kind == HazardKind.Meteor)
                    v.trail = Mats.Prim(PrimitiveType.Sphere, "Meteor Trail", root, Vector3.zero, Vector3.one, flameMat);
                v.body.gameObject.SetActive(false);
                v.marker.gameObject.SetActive(false);
                if (v.trail) v.trail.gameObject.SetActive(false);
                hazardViews[i] = v;
            }
        }

        /// Çöken kaya köprünün parçaları: çöküş cephesinin gerisinde kalanlar düşer, önündeki 3 m titrer (çatlama).
        class LedgePiece { public Transform t; public Vector3 pos, vel; public Quaternion rot; public Vector3 spin; public bool falling; }
        readonly List<LedgePiece> ledgePieces = new List<LedgePiece>();
        float lastLedgeClock = -1f;

        public void UpdateLedges(SledPhysics sled, float dt)
        {
            if (ledgePieces.Count == 0) return;
            // Yeni atış: hepsi yerine
            if (sled.clock < lastLedgeClock || sled.clock <= 0f)
                foreach (var p in ledgePieces)
                {
                    p.falling = false;
                    p.vel = Vector3.zero;
                    p.t.gameObject.SetActive(true);
                    p.t.SetPositionAndRotation(p.pos, p.rot);
                }
            lastLedgeClock = sled.clock;
            float front = sled.CollapseFront;
            foreach (var p in ledgePieces)
            {
                bool inSpan = p.pos.z > sled.ledgeR0 - 0.5f && p.pos.z < sled.ledgeR1 + 0.5f;
                if (!p.falling && inSpan && p.pos.z < front)
                {
                    p.falling = true;
                    var h = (uint)(p.pos.GetHashCode());
                    p.vel = new Vector3(((h & 255) / 255f - 0.5f) * 1.5f, -1f, 0f);
                    p.spin = new Vector3(((h >> 8) & 255) / 255f - 0.5f, 0f, ((h >> 16) & 255) / 255f - 0.5f) * 220f;
                }
                if (p.falling)
                {
                    if (!p.t.gameObject.activeSelf) continue;
                    p.vel.y -= track.gravity * dt;
                    p.t.position += p.vel * dt;
                    p.t.rotation = Quaternion.Euler(p.spin * dt) * p.t.rotation;
                    if (p.t.position.y < p.pos.y - 25f) p.t.gameObject.SetActive(false);
                }
                else if (inSpan && !float.IsInfinity(front) && p.pos.z < front + 3f)
                {
                    // Çatlama titremesi
                    float k = 1f - (p.pos.z - front) / 3f;
                    p.t.position = p.pos + new Vector3(Mathf.Sin(Time.time * 53f + p.pos.z) , Mathf.Sin(Time.time * 61f + p.pos.z * 3f), 0f) * 0.035f * k;
                }
            }
        }

        const int BoostRows = 4;
        readonly Material[] boostArrowMats = new Material[BoostRows], boostGlowMats = new Material[BoostRows];

        /// Hızlandırıcılar (kullanıcı 2026-10-23: "kırmızılar yanıp sönen ışık gibi, yüzeye yayılımı doğal ve akıcı"):
        /// araziye (rampada rampaya) oturan parlak gümüş levha, koyu çerçeve ve beyaz kenar ışıkları; üstünde dört sıra
        /// kendinden ışıklı kırmızı ok başı. Her okun altında levhaya ve zemine taşan yumuşak kırmızı ışık lekesi var.
        /// Oklar arkadan öne sırayla yanıp söner, ışık lekesi okla birlikte parlar (AnimateBirds).
        void BuildBoosts()
        {
            if (track.boosts.Count == 0) return;
            var frameMat = resources.Solid(new Color(0.28f, 0.3f, 0.36f), 0.5f);
            // Metalik malzeme sahnede yansıtacak bir şey bulamayıp koyu görünüyordu: açık gümüş, hafif kendinden ışıklı ve parlak.
            var padMat = resources.Glow(new Color(0.7f, 0.72f, 0.77f), new Color(0.2f, 0.21f, 0.24f), 0.95f);
            var shineMat = resources.Glow(Color.white, new Color(0.9f, 0.9f, 0.95f), 0.95f);
            var glowTex = resources.Own(GlowSpotTexture());
            for (int k = 0; k < BoostRows; k++)
            {
                boostArrowMats[k] = resources.Glow(new Color(1f, 0.12f, 0.1f), new Color(1.6f, 0.1f, 0.08f), 0.8f);
                // Işıksız, renk tonlu saydam malzeme: Standard saydam cam gökyüzü yansımasını beyaz ekliyor, leke kırmızı
                // yerine gri-beyaz görünüyordu (2026-10-23).
                boostGlowMats[k] = resources.Own(new Material(Shader.Find("Sprites/Default")) { mainTexture = glowTex, renderQueue = 3000 });
                boostGlowMats[k].color = new Color(1f, 0.15f, 0.1f, 0.6f);
            }
            foreach (var b in track.boosts)
            {
                var pad = new GameObject(b.ramp ? "Boost Ramp" : "Boost").transform;
                pad.SetParent(root, false);
                Strip(pad, b, TrackProfile.BoostHalfWidth + 0.16f, b.z0 - 0.2f, b.z1 + 0.2f, 0.03f, frameMat);
                Strip(pad, b, TrackProfile.BoostHalfWidth, b.z0, b.z1, 0.06f, padMat);
                for (int side = -1; side <= 1; side += 2)
                {
                    var e = b;
                    e.x = b.x + side * (TrackProfile.BoostHalfWidth - 0.14f);
                    Strip(pad, e, 0.06f, b.z0 + 0.2f, b.z1 - 0.2f, 0.075f, shineMat);
                }
                // Rampanın yan yüzleri (dudakta altı boş görünmesin): levhadan zemine inen koyu etek
                if (b.ramp)
                    for (int side = -1; side <= 1; side += 2)
                        RampSkirt(pad, b, side, frameMat);
                for (int r = 0; r < BoostRows; r++)
                {
                    float zt = Mathf.Lerp(b.z0 + 1.7f, b.z1 - 0.5f, r / (float)(BoostRows - 1));
                    Chevron(pad, b.x, zt, boostArrowMats[r]);
                    Quad(pad, b.x, zt - 0.3f, 1.25f, 0.95f, 0.09f, boostGlowMats[r]);
                }
                // Levhanın önüne ve yanlarına taşan geniş, sönük ışık (zemine yayılım)
                Quad(pad, b.x, (b.z0 + b.z1) * 0.5f, TrackProfile.BoostHalfWidth + 1.1f, (b.z1 - b.z0) * 0.5f + 1.4f, 0.02f, boostGlowMats[0]);
            }
        }

        /// Yumuşak ışık lekesi dokusu: ortada parlak, kenarlara doğru üstel sönen (ışığın yüzeye yayılması).
        static Texture2D GlowSpotTexture()
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Glow Spot" };
            var px = new Color[N * N];
            float edge = Mathf.Exp(-3.2f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                    float d2 = u * u + v * v;
                    float a = Mathf.Clamp01((Mathf.Exp(-d2 * 3.2f) - edge) / (1f - edge));
                    float core = Mathf.Exp(-d2 * 14f);
                    px[y * N + x] = new Color(1f, 0.55f + 0.45f * core, 0.5f + 0.5f * core, a);
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// Araziye oturan dikdörtgen (yarı genişlik hx, yarı uzunluk hz): ışık lekesi.
        void Quad(Transform parent, float cx, float cz, float hx, float hz, float lift, Material mat)
        {
            const int NX = 6, NZ = 8;
            var verts = new Vector3[(NX + 1) * (NZ + 1)];
            var uv = new Vector2[verts.Length];
            var tris = new int[NX * NZ * 6];
            for (int j = 0; j <= NZ; j++)
                for (int i = 0; i <= NX; i++)
                {
                    float x = cx + Mathf.Lerp(-hx, hx, i / (float)NX), z = cz + Mathf.Lerp(-hz, hz, j / (float)NZ);
                    verts[j * (NX + 1) + i] = new Vector3(x, track.TerrainHeight(x, z) + lift, z);
                    uv[j * (NX + 1) + i] = new Vector2(i / (float)NX, j / (float)NZ);
                }
            int t = 0;
            for (int j = 0; j < NZ; j++)
                for (int i = 0; i < NX; i++)
                {
                    int a = j * (NX + 1) + i;
                    tris[t++] = a; tris[t++] = a + NX + 1; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = a + NX + 1; tris[t++] = a + NX + 2;
                }
            var mesh = new Mesh { vertices = verts, uv = uv, triangles = tris };
            mesh.RecalculateNormals();
            var go = new GameObject("Boost Glow");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = resources.Own(mesh);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// İleri (+z) bakan kalın V ok başı, yüzeye oturur.
        void Chevron(Transform parent, float cx, float zt, Material mat)
        {
            var outer = new[] { new Vector2(-0.82f, -0.36f), new Vector2(0f, 0.42f), new Vector2(0.82f, -0.36f) };
            var inner = new[] { new Vector2(-0.6f, -0.56f), new Vector2(0f, 0.1f), new Vector2(0.6f, -0.56f) };
            var verts = new Vector3[6];
            for (int k = 0; k < 3; k++)
            {
                float xo = cx + outer[k].x, zo = zt + outer[k].y, xi = cx + inner[k].x, zi = zt + inner[k].y;
                verts[k] = new Vector3(xo, track.TerrainHeight(xo, zo) + 0.1f, zo);
                verts[k + 3] = new Vector3(xi, track.TerrainHeight(xi, zi) + 0.1f, zi);
            }
            var tris = new[] { 0, 1, 3, 3, 1, 4, 1, 2, 4, 4, 2, 5 };
            var mesh = new Mesh { vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            var go = new GameObject("Boost Arrow");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = resources.Own(mesh);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// Rampanın yan eteği: levhanın kenarından zemine inen dikey yüz.
        void RampSkirt(Transform parent, TrackProfile.Boost b, int side, Material mat)
        {
            const int N = 20;
            var verts = new Vector3[(N + 1) * 2];
            var tris = new int[N * 6];
            float x = b.x + side * (TrackProfile.BoostHalfWidth + 0.16f);
            for (int i = 0; i <= N; i++)
            {
                float z = Mathf.Lerp(b.z0, b.z1, i / (float)N);
                float top = track.TerrainHeight(x, z) + 0.06f;
                float ground = track.Height(z) + track.Bank(x - track.CenterX(z)) - 0.1f;
                verts[i * 2] = new Vector3(x, top, z);
                verts[i * 2 + 1] = new Vector3(x, Mathf.Min(ground, top), z);
                if (i < N)
                {
                    int t = i * 6, v = i * 2;
                    if (side > 0) { tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1; tris[t + 3] = v + 1; tris[t + 4] = v + 2; tris[t + 5] = v + 3; }
                    else { tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2; tris[t + 3] = v + 1; tris[t + 4] = v + 3; tris[t + 5] = v + 2; }
                }
            }
            var mesh = new Mesh { vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            var go = new GameObject("Ramp Skirt");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = resources.Own(mesh);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// Hızlandırıcı levhası: z boyunca araziyi izleyen ince şerit (üstü lift kadar yukarıda).
        void Strip(Transform parent, TrackProfile.Boost b, float half, float z0, float z1, float lift, Material mat)
        {
            const int N = 20;
            var verts = new Vector3[(N + 1) * 2];
            var uv = new Vector2[verts.Length];
            var tris = new int[N * 6];
            for (int i = 0; i <= N; i++)
            {
                float z = Mathf.Lerp(z0, z1, i / (float)N);
                for (int s = 0; s < 2; s++)
                {
                    float x = b.x + (s == 0 ? -half : half);
                    verts[i * 2 + s] = new Vector3(x, track.TerrainHeight(x, z) + lift, z);
                    uv[i * 2 + s] = new Vector2(s, i / (float)N);
                }
                if (i < N)
                {
                    int t = i * 6, v = i * 2;
                    tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
                    tris[t + 3] = v + 1; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
                }
            }
            var mesh = new Mesh { vertices = verts, uv = uv, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Boost Plate");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = resources.Own(mesh);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        Material windRibbonMat;

        /// Dalgalı hava şeridi: yükseldikçe sağa-sola salınan ve kendi ekseninde kıvrılan, uçlarda incelen dikey şerit
        /// (iki yüzlü). v boyunca doku yukarı akar (AnimateBirds).
        static Mesh WindRibbon(float phase, float height)
        {
            const int Seg = 36, Front = (Seg + 1) * 2;
            // Ön ve arka yüz ayrı noktalarla (aynı noktaları paylaşınca yüzey yönleri birbirini sıfırlıyor, şerit siyah görünüyordu).
            var verts = new Vector3[Front * 2];
            var uvs = new Vector2[verts.Length];
            for (int i = 0; i <= Seg; i++)
            {
                float t = i / (float)Seg, y = t * height;
                var c = new Vector3(1.1f * Mathf.Sin(y * 0.32f + phase), y, 0.8f * Mathf.Cos(y * 0.27f + phase * 1.3f));
                float twist = y * 0.22f + phase;
                float w = 1.6f * Mathf.Sin(Mathf.PI * t) * (0.75f + 0.25f * Mathf.Sin(y * 0.6f + phase));
                var side = new Vector3(Mathf.Cos(twist), 0f, Mathf.Sin(twist)) * (w * 0.5f);
                verts[i * 2] = c - side;
                verts[i * 2 + 1] = c + side;
                uvs[i * 2] = new Vector2(0f, y / 6f);
                uvs[i * 2 + 1] = new Vector2(1f, y / 6f);
                verts[Front + i * 2] = verts[i * 2];
                verts[Front + i * 2 + 1] = verts[i * 2 + 1];
                uvs[Front + i * 2] = uvs[i * 2];
                uvs[Front + i * 2 + 1] = uvs[i * 2 + 1];
            }
            var tris = new int[Seg * 12];
            for (int i = 0, n = 0; i < Seg; i++)
            {
                int a = i * 2, b = a + 1, c2 = a + 2, d = a + 3;
                tris[n++] = a; tris[n++] = c2; tris[n++] = b; tris[n++] = b; tris[n++] = c2; tris[n++] = d;
                a += Front; b += Front; c2 += Front; d += Front;
                tris[n++] = a; tris[n++] = b; tris[n++] = c2; tris[n++] = b; tris[n++] = d; tris[n++] = c2;   // arka yüz
            }
            var mesh = new Mesh { name = "Wind Ribbon" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// Şerit dokusu: kenarlara doğru sönen, akış yönünde dalgalı ince bantlar (alfa).
        static Texture2D WindTexture()
        {
            const int W = 32, H = 128;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Wind" };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                    float edge = Mathf.Sin(Mathf.PI * u);
                    float band = 0.5f + 0.5f * Mathf.Sin((v * 3f + 0.15f * Mathf.Sin(u * 6.28f * 2f)) * 6.2832f);
                    float a = edge * edge * Mathf.Lerp(0.15f, 1f, band * band);
                    px[y * W + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// Kuş sürülerini atışın saatine göre konumlar, kanatları çırptırır; hava şeritlerinin dokusunu yukarı akıtır.
        public void AnimateBirds(float clock, float time)
        {
            if (windRibbonMat != null) windRibbonMat.mainTextureOffset = new Vector2(0f, -time * 0.45f);
            // Hızlandırıcı okları arkadan öne sırayla yanıp söner; altındaki ışık lekesi okla birlikte parlar.
            if (boostArrowMats[0] != null)
                for (int k = 0; k < BoostRows; k++)
                {
                    float ph = Mathf.Repeat(time * 1.8f - k / (float)BoostRows, 1f);
                    float on = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(ph * Mathf.PI * 2f)), 3f);   // yumuşak yanıp sönme
                    boostArrowMats[k].SetColor("_EmissionColor", new Color(2.2f, 0.16f, 0.1f) * (0.15f + 0.85f * on));
                    boostArrowMats[k].color = Color.Lerp(new Color(0.5f, 0.06f, 0.05f), new Color(1f, 0.2f, 0.15f), on);
                    boostGlowMats[k].color = new Color(1f, 0.15f, 0.1f, 0.08f + 0.62f * on);
                }
            for (int i = 0; i < flockRoots.Count; i++)
            {
                flockRoots[i].position = track.FlockCenter(track.flocks[i], clock);
                var w = wings[i];
                for (int k = 0; k < w.Length; k++)
                {
                    float side = k % 2 == 0 ? -1f : 1f;
                    w[k].localRotation = Quaternion.Euler(0f, 0f, side * 35f * Mathf.Sin(time * 14f + k * 0.7f));
                }
            }
        }

        /// Parkur değişince eski dünya silinir.
        public void Destroy() => Object.Destroy(root.gameObject);

        public void SetBest(int meters)
        {
            bestFlag.gameObject.SetActive(meters > 2);
            float z = meters;
            float x = track.CenterX(z) - track.halfWidth - 0.6f;
            bestFlag.position = new Vector3(x, track.Height(x, z), z);
            bestText.text = "BEST " + TrackLibrary.Show(meters) + " m";
        }

        /// attachPoint: lastiğin kızağa bağlandığı nokta. Kızak bırakılınca lastik (kütleli bir yay gibi)
        /// ileri şaklar, sapanın önüne taşar ve titreşerek durulur.
        public void UpdateSlingshot(Vector3 attachPoint, Vector3 sledVelocity, bool attached, float dt)
        {
            Vector3 rest = sling.Rest;
            if (attached)
            {
                pouchPos = attachPoint;
                pouchVel = sledVelocity;
            }
            else
            {
                if (pouchAttached) pouchVel = sledVelocity;   // bırakıldığı an
                // Küçük adımlarla yarı örtük Euler: tek adımda w·dt > 2 olunca (kare 0.07 s'den uzun) yay patlıyor, lastik
                // sonsuza gidip "abnormal mesh bounds" hatası yağdırıyordu (2026-10-18).
                const float w = 28f, zeta = 0.1f;
                int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.008f), 1, 20);
                float h = dt / steps;
                for (int i = 0; i < steps; i++)
                {
                    Vector3 acc = -w * w * (pouchPos - rest) - 2f * zeta * w * pouchVel;
                    pouchVel += acc * h;
                    pouchPos += pouchVel * h;
                }
            }
            pouchAttached = attached;

            // Gerildikçe lastik incelir ve kızarır.
            float stretch = Mathf.Clamp01((Vector3.Distance(pouchPos, rest) - 0.3f) / SledConfig.SlingStretch);
            sling.Update(pouchPos, stretch, Color.Lerp(bandBase, new Color(0.95f, 0.2f, 0.12f), stretch), bandThickness);
        }

        /// Sapan yükseltmesi: lastik kalınlaşır ve rengi koyu griden turuncuya döner (0..1).
        /// Sapan seviyesi: her aşamada direkler boyanır ve halat rengi değişir, seviyeyle halat kalınlaşır.
        public void SetSlingLevel(int level)
        {
            int stage = Mathf.Clamp(Upgrades.Stage(level), 0, 5);
            sling.SetStage(stage);
            bandBase = PartVisuals.SlingRope[stage];
            bandThickness = 1f + 0.5f * level / Upgrades.MaxLevel((int)UpgradeType.Slingshot);
        }

        /// Zeminde fırlatma yönü ve gücünü gösteren noktalar.
        public void ShowGuide(float aim, float pull)
        {
            bool visible = pull > 0.03f;
            var dir = new Vector3(Mathf.Sin(aim), 0f, Mathf.Cos(aim));
            float length = 4f + 22f * pull;
            for (int i = 0; i < GuideDots; i++)
            {
                guide[i].gameObject.SetActive(visible);
                if (!visible) continue;
                float t = (i + 1f) / GuideDots;
                Vector3 p = new Vector3(0f, 0f, 1.2f) + dir * (length * t);
                p.y = track.Height(p.x, p.z) + 0.04f;
                guide[i].position = p;
                float s = Mathf.Lerp(0.6f, 0.3f, t);
                guide[i].localScale = new Vector3(s, 0.02f, s);
            }
        }

        public static TextMesh MakeText(Transform parent, Vector3 localPos, string text, float size, Color color)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            return tm;
        }
    }
}
