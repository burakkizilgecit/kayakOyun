using UnityEngine;

namespace SlingRiders
{
    /// Mobil: tek parmak (sapan / kızak yönü) + telefonu eğme (bardak).
    /// PC testi: fare ile sürükleme, Boşluk = sapan, A/D + W/S = kızak, I/J/K/L = bardak.
    public class InputReader
    {
        public const float MaxGlassTilt = 40f;

        public bool pointerDown, pointerHeld, pointerUp;
        public Vector2 pointerPos, pointerStart;

        public float steer;        // -1..1
        public float pitch;        // -1..1 (havada burun)
        public float glassPitch;   // derece, + = bardağın ağzı ileri
        public float glassRoll;    // derece, + = bardağın ağzı sağa
        public bool spaceHeld, spaceUp;
        /// Direksiyon hassasiyeti (ayarlar): tam dönüş için gereken parmak yolu ekran genişliğinin bu oranı.
        public int sensitivity = 1;
        static readonly float[] SteerSpan = { 0.22f, 0.15f, 0.1f };
        const float DeadZone = 0.015f;   // ekran genişliği oranı: titreyen parmak kızağı oynatmasın

        readonly bool touchMode = Application.isMobilePlatform;
        readonly bool tiltMode = Application.isMobilePlatform && SystemInfo.supportsAccelerometer;
        Vector3 gravity = new Vector3(0f, -0.7f, -0.7f);
        Vector3 neutral = new Vector3(0f, -0.7f, -0.7f);
        bool gravityReady;

        public bool TiltMode => tiltMode;
        public bool TouchMode => touchMode;

        // Require a fresh touch after a pause; recalibrate to the resumed phone pose.
        public void ResetInput()
        {
            finger = -1;
            mouseActive = false;
            pointerDown = pointerHeld = pointerUp = spaceHeld = spaceUp = false;
            steer = pitch = glassPitch = glassRoll = 0f;
            gravityReady = false;
        }

        /// Telefonun o anki duruşunu "düz bardak" kabul eder.
        public void Calibrate()
        {
            if (tiltMode) neutral = gravity;
            glassPitch = glassRoll = 0f;
        }

        public void Update(float dt, bool driving)
        {
            ReadPointer(driving);
            ReadSteer(driving);
            ReadGlass(dt);
            spaceHeld = Input.GetKey(KeyCode.Space);
            spaceUp = Input.GetKeyUp(KeyCode.Space);
        }

        /// Ekran noktası arayüz düğmesinin üstünde mi (GameUI.BlocksPointer)? Koşuda düğmeye başlayan dokunuş
        /// direksiyon sayılmaz: bir parmak yön verirken öbürüyle rokete basılabilir.
        public System.Func<Vector2, bool> blocksPointer;
        int finger = -1;          // direksiyonu tutan parmak (fingerId); -1: yok
        bool mouseActive;

        bool OnButton(bool driving, Vector2 pos) => driving && blocksPointer != null && blocksPointer(pos);

        void ReadPointer(bool driving)
        {
            pointerDown = pointerUp = false;
            if (touchMode)
            {
                // Parmak fingerId ile izlenir: ilk parmak kalkıp ikincisi kalınca dokunuş sırası değişse de
                // direksiyon başka parmağa atlamaz (eskiden GetTouch(0) ile başlangıç noktası eskide kalıyordu).
                bool found = false;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (finger < 0 && t.phase == TouchPhase.Began && !OnButton(driving, t.position))
                    {
                        finger = t.fingerId;
                        pointerDown = true;
                        pointerStart = t.position;
                    }
                    if (t.fingerId != finger) continue;
                    found = true;
                    pointerPos = t.position;
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { pointerUp = true; finger = -1; }
                }
                if (!found && finger >= 0) { pointerUp = true; finger = -1; }
                pointerHeld = finger >= 0;
                return;
            }

            pointerPos = Input.mousePosition;
            if (Input.GetMouseButtonDown(0) && !OnButton(driving, pointerPos)) { pointerDown = true; pointerStart = pointerPos; mouseActive = true; }
            pointerUp = mouseActive && Input.GetMouseButtonUp(0);
            if (pointerUp) mouseActive = false;
            pointerHeld = mouseActive && Input.GetMouseButton(0);
        }

        void ReadSteer(bool driving)
        {
            float kx = 0f, ky = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) kx -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) kx += 1f;
            // Uçak kumandası gibi (kullanıcı 2026-10-24: "parmağını öne doğru itip dengeye getirmeli"): öne/yukarı = burun
            // aşağı, geri/aşağı = burun yukarı.
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) ky += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) ky -= 1f;

            float px = 0f, py = 0f;
            if (driving && pointerHeld)
            {
                Vector2 d = pointerPos - pointerStart;
                float dead = DeadZone * Screen.width;
                float dx = Mathf.Sign(d.x) * Mathf.Max(0f, Mathf.Abs(d.x) - dead);
                px = Mathf.Clamp(dx / (SteerSpan[Mathf.Clamp(sensitivity, 0, 2)] * Screen.width), -1f, 1f);
                // Dikeyde de ölü bölge (2026-10-30): direksiyon çevirirken parmağın hafif kayması burnu oynatıp süzülmeyi
                // titretiyordu.
                float dy = Mathf.Sign(d.y) * Mathf.Max(0f, Mathf.Abs(d.y) - dead);
                py = Mathf.Clamp(-dy / (0.12f * Screen.height), -1f, 1f);
            }
            steer = Mathf.Abs(kx) > Mathf.Abs(px) ? kx : px;
            pitch = Mathf.Abs(ky) > Mathf.Abs(py) ? ky : py;
        }

        void ReadGlass(float dt)
        {
            if (tiltMode)
            {
                Vector3 a = Input.acceleration;
                if (!gravityReady) { gravity = neutral = a; gravityReady = true; }
                // 25/s ham ivmeölçer gürültüsünü bardağa ve kola taşıyordu (kullanıcı 2026-10-26: karakter titriyor); 15/s ~65 ms
                gravity = Vector3.Lerp(gravity, a, 1f - Mathf.Exp(-15f * dt));
                glassPitch = Mathf.Clamp(Mathf.DeltaAngle(ForwardTilt(neutral), ForwardTilt(gravity)), -MaxGlassTilt, MaxGlassTilt);
                glassRoll = Mathf.Clamp(Mathf.DeltaAngle(SideTilt(neutral), SideTilt(gravity)), -MaxGlassTilt, MaxGlassTilt);
                return;
            }

            float tp = 0f, tr = 0f;
            if (Input.GetKey(KeyCode.I)) tp += MaxGlassTilt;
            if (Input.GetKey(KeyCode.K)) tp -= MaxGlassTilt;
            if (Input.GetKey(KeyCode.L)) tr += MaxGlassTilt;
            if (Input.GetKey(KeyCode.J)) tr -= MaxGlassTilt;
            glassPitch = Mathf.MoveTowards(glassPitch, tp, 110f * dt);
            glassRoll = Mathf.MoveTowards(glassRoll, tr, 110f * dt);
        }

        // Dikey tutulan telefonun üst kenarı sizden uzaklaştıkça artar.
        static float ForwardTilt(Vector3 g) => Mathf.Atan2(-g.z, -g.y) * Mathf.Rad2Deg;

        // Sağ kenar aşağı indikçe artar.
        static float SideTilt(Vector3 g) =>
            Mathf.Atan2(g.x, Mathf.Sqrt(g.y * g.y + g.z * g.z)) * Mathf.Rad2Deg;
    }
}
