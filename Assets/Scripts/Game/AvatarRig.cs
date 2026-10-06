using System.Collections.Generic;
using UnityEngine;

namespace SledSurfers
{
    /// Kızaktaki karakter (Quaternius modeli). Oturma pozu animasyondan bir kez örneklenir;
    /// her karede o poza dönülür, üstüne gövde eğilmesi ve iki kemikli IK ile kollar eklenir:
    /// bir el bardağı tutar, öbürü kızağın ipini.
    public class AvatarRig
    {
        const float SeatedHeight = 0.98f;   // oturur karakterin kızak üstü yüksekliği (m)
        const float DeckTop = 0.27f;

        readonly Transform holder;
        readonly GameObject model;
        readonly Transform hips, abdomen, torso;
        readonly Transform[] upper = new Transform[2], lower = new Transform[2], fist = new Transform[2];   // 0 = sol, 1 = sağ
        readonly Dictionary<Transform, Quaternion> basePose = new Dictionary<Transform, Quaternion>();
        Transform head, mouth, laughPivot;
        Vector3 homePos, homeScale, laughScale;
        Quaternion homeRot;
        // Bacaklar: ayak bir IK hedefi (kök kemiğe bağlı), baldırı izlemez; baldıra göre yeri saklanır.
        readonly Transform[] thigh = new Transform[2], shin = new Transform[2], foot = new Transform[2];
        readonly Vector3[] footInShin = new Vector3[2], footHome = new Vector3[2];
        readonly Quaternion[] footRotInShin = new Quaternion[2];
        Transform[] contact, allBones;
        float seatGap;   // oturma kemiklerinin en alçağı ile gerçek oturma yüzeyi arası (m)

        /// Oturur modelin en alt noktası, tutucunun (holder) orijinine göre (dik dururken, aşağı eksi).
        public float BottomOffset { get; private set; }
        public Transform Holder => holder;

        public bool Valid => model != null && torso != null && upper[0] != null && upper[1] != null;

        public AvatarRig(Transform parent, string modelName)
        {
            holder = new GameObject("Avatar " + modelName).transform;
            holder.SetParent(parent, false);
            var prefab = Resources.Load<GameObject>("Avatars/" + modelName);
            if (prefab == null)
            {
                Debug.LogWarning("[AVATAR] Model bulunamadı: " + modelName);
                return;
            }
            model = Object.Instantiate(prefab, holder);
            var animator = model.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;
            var legacy = model.GetComponent<Animation>();
            if (legacy != null) { legacy.playAutomatically = false; legacy.enabled = false; }

            var bones = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
            Transform B(string n) => bones.TryGetValue(n, out var t) ? t : null;
            hips = B("Hips");
            abdomen = B("Abdomen");
            torso = B("Torso");
            upper[0] = B("UpperArm.L"); lower[0] = B("LowerArm.L"); fist[0] = B("Fist.L");
            upper[1] = B("UpperArm.R"); lower[1] = B("LowerArm.R"); fist[1] = B("Fist.R");

            // Ayakta durma (Idle ilk karesi) ve sevinç (Victory ortası) pozları: düşüp kalkma sahnesi için saklanır.
            AnimationClip sit = null, idle = null, cheer = null;
            foreach (var clip in Resources.LoadAll<AnimationClip>("Avatars/" + modelName))
            {
                if (clip.name.Contains("SitDown")) sit = clip;
                else if (clip.name.Contains("Idle")) idle = clip;
                else if (clip.name.Contains("Victory")) cheer = clip;
            }
            if (idle != null)
            {
                idle.SampleAnimation(model, 0f);
                foreach (var t in model.GetComponentsInChildren<Transform>(true)) standPose[t] = (t.localRotation, t.localPosition);
            }
            if (cheer != null)
            {
                cheer.SampleAnimation(model, cheer.length * 0.45f);
                foreach (var n in new[] { "UpperArm.L", "LowerArm.L", "Fist.L", "UpperArm.R", "LowerArm.R", "Fist.R" })
                    if (B(n) != null) cheerArms[B(n)] = B(n).localRotation;
            }
            if (sit != null) sit.SampleAnimation(model, sit.length);
            else Debug.LogWarning("[AVATAR] SitDown animasyonu yok: " + modelName);

            // Sandalye oturuşu → kızak oturuşu: bacaklar öne uzanır, dizler hafif kalkık.
            Vector3 fw = parent.forward, up = parent.up, rt = parent.right;
            for (int s = 0; s < 2; s++)
            {
                float sign = s == 0 ? -1f : 1f;
                var thigh = B(s == 0 ? "UpperLeg.L" : "UpperLeg.R");
                var shin = B(s == 0 ? "LowerLeg.L" : "LowerLeg.R");
                var foot = B(s == 0 ? "Foot.L" : "Foot.R");
                if (thigh == null || shin == null || foot == null) continue;
                // Ayak bir IK hedefi (kök kemiğe bağlı), baldırı izlemez: baldıra göre yeri saklanıp sonra taşınır.
                Vector3 footPos = shin.InverseTransformPoint(foot.position);
                Quaternion footRot = Quaternion.Inverse(shin.rotation) * foot.rotation;
                Aim(thigh, shin.position, fw + up * 0.28f + rt * sign * 0.14f);
                Aim(shin, shin.TransformPoint(footPos), fw - up * 0.3f + rt * sign * 0.04f);
                foot.position = shin.TransformPoint(footPos);
                foot.rotation = Quaternion.AngleAxis(-25f, rt) * shin.rotation * footRot;   // ayak uçları yukarı
            }

            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                basePose[t] = t.localRotation;
                basePos[t] = t.localPosition;
            }
            for (int s = 0; s < 2; s++)
            {
                thigh[s] = B(s == 0 ? "UpperLeg.L" : "UpperLeg.R");
                shin[s] = B(s == 0 ? "LowerLeg.L" : "LowerLeg.R");
                foot[s] = B(s == 0 ? "Foot.L" : "Foot.R");
                if (shin[s] == null || foot[s] == null) continue;
                footInShin[s] = shin[s].InverseTransformPoint(foot[s].position);
                footRotInShin[s] = Quaternion.Inverse(shin[s].rotation) * foot[s].rotation;
                footHome[s] = foot[s].localPosition;
            }
            contact = System.Array.FindAll(new[] { hips, thigh[0], thigh[1], shin[0], shin[1], foot[0], foot[1] }, t => t != null);
            allBones = System.Array.FindAll(new[] { hips, abdomen, torso, B("Head"), upper[0], upper[1], lower[0], lower[1],
                                                    fist[0], fist[1], thigh[0], thigh[1], shin[0], shin[1], foot[0], foot[1] }, t => t != null);

            FitToSled();
            // Kızakta (doğru oturmuş) iken oturma kemiklerinin güverteye uzaklığı: yerde de aynı payla oturur.
            seatGap = Lowest(contact) - parent.TransformPoint(new Vector3(0f, DeckTop, 0f)).y;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            head = B("Head");
            BuildSmile();
            homePos = holder.localPosition;
            homeRot = holder.localRotation;
            homeScale = holder.localScale;
        }

        /// Pozlanmış modeli ölçer: kızağın üstüne oturacak boya ölçekler, kalçası güvertenin ortasına gelir.
        void FitToSled()
        {
            holder.localPosition = Vector3.zero;
            holder.localRotation = Quaternion.identity;
            holder.localScale = Vector3.one;
            var b = PosedBounds();
            float s = SeatedHeight / Mathf.Max(b.size.y, 0.01f);
            holder.localScale = Vector3.one * s;
            b = PosedBounds();
            // Yatayda kalça güvertenin biraz arkasında; dikeyde en alt nokta güvertenin üstünde.
            Vector3 hip = hips != null ? holder.parent.InverseTransformPoint(hips.position) : b.center;
            holder.localPosition = new Vector3(-hip.x, DeckTop - b.min.y, -0.3f - hip.z);
            BottomOffset = b.min.y;
        }

        Bounds PosedBounds()
        {
            var parent = holder.parent;
            bool first = true;
            var bounds = new Bounds();
            var baked = new Mesh();
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.BakeMesh(baked, true);
                var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                foreach (var v in baked.vertices)
                {
                    var p = parent.InverseTransformPoint(m.MultiplyPoint3x4(v));
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                }
            }
            Object.Destroy(baked);
            return bounds;
        }

        /// Kemiği, çocuğu (child) verilen yöne bakacak şekilde döndürür.
        static void Aim(Transform bone, Vector3 child, Vector3 dir)
        {
            bone.rotation = Quaternion.FromToRotation(child - bone.position, dir.normalized) * bone.rotation;
        }

        /// Gülümseme: modellerde ağız yok, yalnızca gözler var. Gözlerin ("Face" malzemesi) yeri ve boyu
        /// modelden ölçülür, hemen altına uçları yukarı kıvrık koyu bir yay eklenir. Başa bağlıdır, başla döner.
        void BuildSmile()
        {
            if (head == null) return;
            var parent = holder.parent;
            bool any = false;
            var eyes = new Bounds();
            // Pişirilmiş mesh'in ölçeği hiyerarşideki ölçeklere göre tutarsız çıkıyor (tutucu küçültmesini
            // içermiyor). Bu yüzden gözler önce pişirilmiş mesh içinde ölçülür, sonra mesh'in pişirilmiş sınırları
            // rendererın dünya sınırlarına (Unity'nin doğru hesapladığı) eksen eksen eşlenir.
            var baked = new Mesh();
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mats = smr.sharedMaterials;
                smr.BakeMesh(baked, true);
                var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                var verts = baked.vertices;
                var all = new Bounds(m.MultiplyPoint3x4(verts[0]), Vector3.zero);
                foreach (var v in verts) all.Encapsulate(m.MultiplyPoint3x4(v));
                var world = smr.bounds;
                Vector3 Map(Vector3 p) => new Vector3(
                    world.min.x + (p.x - all.min.x) / Mathf.Max(all.size.x, 1e-5f) * world.size.x,
                    world.min.y + (p.y - all.min.y) / Mathf.Max(all.size.y, 1e-5f) * world.size.y,
                    world.min.z + (p.z - all.min.z) / Mathf.Max(all.size.z, 1e-5f) * world.size.z);
                for (int sub = 0; sub < mats.Length && sub < baked.subMeshCount; sub++)
                {
                    if (mats[sub] == null || !mats[sub].name.Contains("Face")) continue;
                    foreach (int i in baked.GetTriangles(sub))
                    {
                        var p = parent.InverseTransformPoint(Map(m.MultiplyPoint3x4(verts[i])));
                        if (!any) { eyes = new Bounds(p, Vector3.zero); any = true; }
                        else eyes.Encapsulate(p);
                    }
                }
            }
            Object.Destroy(baked);
            if (!any) return;

            // Yay: göz grubunun genişliğinin yarısı kadar, gözlerin altında; uçlar yukarı ve biraz geride (yüz yuvarlak).
            // Ağız gözler arası mesafenin ~yarısı genişliğinde, gözlerin biraz altında.
            float w = eyes.size.x * 0.16f, drop = eyes.size.y * 0.85f + w * 0.5f;
            var points = new List<Vector3>();
            for (int k = 0; k <= 10; k++)
            {
                float u = k / 5f - 1f;   // -1..1
                // Yüzün önüne biraz taşar (gözlerin altında yüz çoğu modelde daha öndedir): içine gömülmesin.
                var local = new Vector3(eyes.center.x + u * w, eyes.center.y - drop + w * 0.4f * u * u, eyes.max.z + w * 0.18f - w * 0.3f * u * u);
                points.Add(head.InverseTransformPoint(parent.TransformPoint(local)));
            }
            mouth = new GameObject("Smile").transform;
            mouth.SetParent(head, false);
            float radius = w * 0.16f / Mathf.Max(head.lossyScale.x, 1e-4f);
            mouth.gameObject.AddComponent<MeshFilter>().sharedMesh = SledModel.Tube(points, radius, 6);
            mouth.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.Solid(new Color(0.22f, 0.07f, 0.08f), 0.3f);
            mouth.gameObject.SetActive(false);

            // Kahkaha ağzı: üst kenarı düz, altı yuvarlak koyu ağız, içinde pembe dil. Kafaya bağlı bir pivot
            // altında metre ölçeğinde kurulur (pivotun y ölçeği ağzı açıp kapatır).
            laughPivot = new GameObject("Laugh").transform;
            laughPivot.SetParent(head, false);
            var topCenter = new Vector3(eyes.center.x, eyes.center.y - drop + w * 0.25f, eyes.max.z + w * 0.2f);
            laughPivot.position = parent.TransformPoint(topCenter);
            laughPivot.rotation = parent.rotation;
            float hs = Mathf.Max(head.lossyScale.x, 1e-4f);
            laughScale = Vector3.one / hs;
            laughPivot.localScale = laughScale;
            float mw = w * 1.15f, mh = w * 1.1f;
            Mesh HalfDisc(float width, float depth, float z)
            {
                var v = new List<Vector3> { new Vector3(0f, 0f, z) };
                for (int k = 0; k <= 14; k++)
                {
                    float a = Mathf.PI * k / 14f;   // 0..π: sağdan sola, alttan
                    v.Add(new Vector3(Mathf.Cos(a) * width, -Mathf.Sin(a) * depth, z));
                }
                var tr = new List<int>();
                for (int k = 1; k <= 14; k++) { tr.Add(0); tr.Add(k + 1); tr.Add(k); tr.Add(0); tr.Add(k); tr.Add(k + 1); }   // iki yüzlü
                var mesh = new Mesh();
                mesh.SetVertices(v);
                mesh.SetTriangles(tr, 0);
                mesh.RecalculateNormals();
                return mesh;
            }
            var mouthGo = new GameObject("Mouth");
            mouthGo.transform.SetParent(laughPivot, false);
            mouthGo.AddComponent<MeshFilter>().sharedMesh = HalfDisc(mw, mh, 0f);
            mouthGo.AddComponent<MeshRenderer>().sharedMaterial = Mats.Solid(new Color(0.3f, 0.05f, 0.07f), 0.2f);
            var tongue = new GameObject("Tongue");
            tongue.transform.SetParent(laughPivot, false);
            tongue.transform.localPosition = new Vector3(0f, -mh * 0.45f, 0.002f);
            tongue.AddComponent<MeshFilter>().sharedMesh = HalfDisc(mw * 0.55f, mh * 0.5f, 0f);
            tongue.AddComponent<MeshRenderer>().sharedMaterial = Mats.Solid(new Color(0.95f, 0.42f, 0.5f), 0.3f);
            laughPivot.gameObject.SetActive(false);
        }

        public void Smile(bool on)
        {
            if (mouth != null) mouth.gameObject.SetActive(on);
        }

        /// Kahkaha ağzı: open 0..1 (açılıp kapanır). Negatif: gizle.
        public void Laugh(float open)
        {
            if (laughPivot == null) return;
            laughPivot.gameObject.SetActive(open >= 0f);
            if (open >= 0f) laughPivot.localScale = new Vector3(laughScale.x, laughScale.y * (0.35f + 0.65f * open), laughScale.z);
            if (open >= 0f) Smile(false);
        }

        public Transform Hips => hips;
        public Vector3 HeadTop => head != null ? head.position + holder.up * 0.45f * holder.lossyScale.y / 0.37f : holder.position;

        static float Lowest(Transform[] bones)
        {
            float y = float.MaxValue;
            foreach (var b in bones) y = Mathf.Min(y, b.position.y);
            return y;
        }

        /// Gövdenin en alçak noktası (kemiklerin en alçağı eksi kalınlık payı): yuvarlanırken yere değme.
        public float LowestPoint() => Lowest(allBones) - 0.09f;

        /// Oturur dururken gerçek oturma yüzeyi (kızaktaki oturuşla aynı payla).
        public float SeatBottom() => Lowest(contact) - seatGap;

        readonly Dictionary<Transform, (Quaternion rot, Vector3 pos)> standPose = new Dictionary<Transform, (Quaternion, Vector3)>();
        readonly Dictionary<Transform, Quaternion> cheerArms = new Dictionary<Transform, Quaternion>();
        readonly Dictionary<Transform, Vector3> basePos = new Dictionary<Transform, Vector3>();
        bool posed;   // StandPose kemik konumlarını değiştirdi: ResetPose geri alır

        /// Oturma pozundan ayakta durma pozuna geçiş (k 0..1). Ayakta durma pozu yoksa oturma pozunda kalır.
        public void StandPose(float k)
        {
            ResetPose();
            if (standPose.Count == 0) return;
            foreach (var kv in standPose)
            {
                var t = kv.Key;
                if (!basePose.TryGetValue(t, out var r0)) continue;
                t.localRotation = Quaternion.Slerp(r0, kv.Value.rot, k);
                t.localPosition = Vector3.Lerp(basePos[t], kv.Value.pos, k);
            }
            posed = true;
        }

        /// Ayaktayken kolları sevinçle kaldırma (Victory pozuna doğru, k 0..1).
        public void CheerArms(float k)
        {
            foreach (var kv in cheerArms)
                if (standPose.TryGetValue(kv.Key, out var s)) kv.Key.localRotation = Quaternion.Slerp(s.rot, kv.Value, k);
        }

        /// Ayaktayken hafif nefes alma ve kafa sallama (canlı dursun).
        public void Breathe(float t)
        {
            Vector3 rt = holder.right, fw = holder.forward;
            if (torso != null) torso.rotation = Quaternion.AngleAxis(2.5f * Mathf.Sin(t * 2.4f), rt) * torso.rotation;
            if (head != null) head.rotation = Quaternion.AngleAxis(6f * Mathf.Sin(t * 1.7f), fw) * Quaternion.AngleAxis(4f * Mathf.Sin(t * 2.4f + 1f), rt) * head.rotation;
        }

        /// Temel oturma pozuna döner (kollar, bacaklar ve ayaklar dahil).
        public void ResetPose()
        {
            foreach (var kv in basePose) kv.Key.localRotation = kv.Value;
            if (posed)
            {
                foreach (var kv in basePos) kv.Key.localPosition = kv.Value;
                posed = false;
            }
            for (int s = 0; s < 2; s++) if (foot[s] != null) foot[s].localPosition = footHome[s];
        }

        void FeetFollowShins()
        {
            for (int s = 0; s < 2; s++)
            {
                if (shin[s] == null || foot[s] == null) continue;
                foot[s].position = shin[s].TransformPoint(footInShin[s]);
                foot[s].rotation = shin[s].rotation * footRotInShin[s];
            }
        }

        /// Havada çırpınma: kollar ve bacaklar savrulur, kafa sallanır. amount 0..1.
        public void FlailPose(float t, float amount)
        {
            ResetPose();
            Vector3 fw = holder.forward, rt = holder.right, up = holder.up;
            for (int s = 0; s < 2; s++)
            {
                float sign = s == 0 ? -1f : 1f;
                if (upper[s] != null)
                    upper[s].rotation = Quaternion.AngleAxis(amount * (70f + 45f * Mathf.Sin(t * 13f + s * 2.1f)) * -sign, fw)
                                        * Quaternion.AngleAxis(amount * 50f * Mathf.Cos(t * 11f + s), rt) * upper[s].rotation;
                if (lower[s] != null)
                    lower[s].rotation = Quaternion.AngleAxis(amount * 35f * Mathf.Sin(t * 17f + s), rt) * lower[s].rotation;
                if (thigh[s] != null)
                    thigh[s].rotation = Quaternion.AngleAxis(amount * 40f * Mathf.Sin(t * 12f + s * 3f), rt)
                                        * Quaternion.AngleAxis(amount * 18f * sign, fw) * thigh[s].rotation;
                if (shin[s] != null)
                    shin[s].rotation = Quaternion.AngleAxis(amount * 30f * (0.5f + 0.5f * Mathf.Sin(t * 15f + s * 2f)), rt) * shin[s].rotation;
            }
            FeetFollowShins();
            if (head != null)
                head.rotation = Quaternion.AngleAxis(amount * 18f * Mathf.Sin(t * 9f), fw) * Quaternion.AngleAxis(amount * 15f * Mathf.Sin(t * 7f), rt) * head.rotation;
        }

        /// Kahkaha: gövde ve kafa geriye atılıp sarsılır, eller karnı tutar.
        public void LaughPose(float t)
        {
            ResetPose();
            Vector3 fw = holder.forward, rt = holder.right, up = holder.up;
            float shake = Mathf.Sin(t * 15f), slow = Mathf.Sin(t * 2.2f);
            if (abdomen != null) abdomen.rotation = Quaternion.AngleAxis(-14f + 6f * shake, rt) * Quaternion.AngleAxis(5f * slow, fw) * abdomen.rotation;
            torso.rotation = Quaternion.AngleAxis(-6f + 4f * shake, rt) * torso.rotation;
            if (head != null) head.rotation = Quaternion.AngleAxis(-16f + 9f * Mathf.Sin(t * 15f + 0.7f), rt) * Quaternion.AngleAxis(8f * slow, fw) * head.rotation;
            float size = holder.lossyScale.y / 0.37f;   // modeller arası boy farkı
            Vector3 belly = (abdomen != null ? abdomen.position : torso.position) + fw * 0.16f * size - up * 0.03f * size;
            for (int s = 0; s < 2; s++)
            {
                float sign = s == 0 ? -1f : 1f;
                var target = belly + rt * sign * 0.07f * size + up * 0.02f * size * Mathf.Sin(t * 15f + s);
                TwoBoneIK(upper[s], lower[s], fist[s], target, upper[s].position + rt * sign * 0.35f - up * 0.15f);
            }
            // Ayaklar sevinçle hafifçe kalkar
            for (int s = 0; s < 2; s++)
                if (thigh[s] != null) thigh[s].rotation = Quaternion.AngleAxis(-10f * (0.5f + 0.5f * Mathf.Sin(t * 7.5f + s * 1.6f)), rt) * thigh[s].rotation;
            FeetFollowShins();
        }

        /// Kızaktan ayrılan karakteri yerine geri takar.
        public void Reattach(Transform parent)
        {
            holder.SetParent(parent, false);
            holder.localPosition = homePos;
            holder.localRotation = homeRot;
            holder.localScale = homeScale;
            ResetPose();
            Smile(false);
            Laugh(-1f);
        }

        public void Destroy() => Object.Destroy(holder.gameObject);

        /// Bardağı tutacak omzun kızak koordinatındaki yeri ve kolun toplam boyu (temel oturma pozunda).
        public Vector3 ShoulderLocal(float side, Transform frame, out float armLength)
        {
            int g = side > 0f ? 1 : 0;
            foreach (var kv in basePose) kv.Key.localRotation = kv.Value;
            armLength = Vector3.Distance(upper[g].position, lower[g].position) + Vector3.Distance(lower[g].position, fist[g].position);
            return frame.InverseTransformPoint(upper[g].position);
        }

        /// İpi tutan yumruğun dünya konumu (Pose'dan sonra).
        public Vector3 RopeFist(float side) => fist[side > 0f ? 0 : 1].position;

        /// side: bardağın olduğu taraf (+1 sağ, -1 sol). Hedefler dünya koordinatında.
        public void Pose(float torsoPitch, float torsoRoll, float side, Vector3 glassHand, Vector3 ropeHand, Transform frame)
        {
            if (!Valid) return;
            foreach (var kv in basePose) kv.Key.localRotation = kv.Value;

            // Gövde: kalçadan ileri-geri ve yana savrulur (kızağın eksenlerine göre).
            var lean = Quaternion.AngleAxis(torsoPitch * 0.6f, frame.right) * Quaternion.AngleAxis(-torsoRoll * 0.6f, frame.forward);
            if (abdomen != null) abdomen.rotation = lean * abdomen.rotation;
            torso.rotation = Quaternion.AngleAxis(torsoPitch * 0.4f, frame.right) * torso.rotation;

            int g = side > 0f ? 1 : 0;
            Vector3 up = frame.up;
            TwoBoneIK(upper[g], lower[g], fist[g], glassHand, upper[g].position - up * 0.3f + frame.right * side * 0.3f);
            TwoBoneIK(upper[1 - g], lower[1 - g], fist[1 - g], ropeHand, upper[1 - g].position - up * 0.3f - frame.right * side * 0.3f);
        }

        /// Analitik iki kemikli IK: dirsek, hint noktasına doğru bükülür.
        static void TwoBoneIK(Transform a, Transform b, Transform c, Vector3 target, Vector3 hint)
        {
            if (a == null || b == null || c == null) return;
            float la = Vector3.Distance(a.position, b.position);
            float lb = Vector3.Distance(b.position, c.position);
            Vector3 toTarget = target - a.position;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(la - lb) + 0.001f, la + lb - 0.001f);
            Vector3 dir = toTarget.normalized;

            // Dirseğin büküleceği düzlem: hedef yönü ve hint.
            Vector3 toHint = hint - a.position;
            Vector3 bendAxis = Vector3.Cross(dir, toHint);
            if (bendAxis.sqrMagnitude < 1e-6f) bendAxis = Vector3.Cross(dir, Vector3.up);
            bendAxis.Normalize();
            float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
            float angleA = Mathf.Acos(cosA) * Mathf.Rad2Deg;
            Vector3 elbow = a.position + Quaternion.AngleAxis(-angleA, bendAxis) * dir * la;

            a.rotation = Quaternion.FromToRotation(b.position - a.position, elbow - a.position) * a.rotation;
            b.rotation = Quaternion.FromToRotation(c.position - b.position, a.position + dir * d - b.position) * b.rotation;
        }
    }
}
