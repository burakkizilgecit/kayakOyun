using UnityEngine;

namespace SledSurfers
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

        readonly bool touchMode = Application.isMobilePlatform;
        readonly bool tiltMode = Application.isMobilePlatform && SystemInfo.supportsAccelerometer;
        Vector3 gravity = new Vector3(0f, -0.7f, -0.7f);
        Vector3 neutral = new Vector3(0f, -0.7f, -0.7f);
        bool gravityReady;

        public bool TiltMode => tiltMode;

        /// Telefonun o anki duruşunu "düz bardak" kabul eder.
        public void Calibrate()
        {
            if (tiltMode) neutral = gravity;
            glassPitch = glassRoll = 0f;
        }

        public void Update(float dt, bool driving)
        {
            ReadPointer();
            ReadSteer(driving);
            ReadGlass(dt);
            spaceHeld = Input.GetKey(KeyCode.Space);
            spaceUp = Input.GetKeyUp(KeyCode.Space);
        }

        void ReadPointer()
        {
            pointerDown = pointerUp = false;
            if (touchMode)
            {
                if (Input.touchCount > 0)
                {
                    var t = Input.GetTouch(0);
                    pointerPos = t.position;
                    if (t.phase == TouchPhase.Began) { pointerDown = true; pointerStart = t.position; }
                    pointerUp = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                    pointerHeld = !pointerUp;
                }
                else pointerHeld = false;
                return;
            }

            pointerPos = Input.mousePosition;
            if (Input.GetMouseButtonDown(0)) { pointerDown = true; pointerStart = pointerPos; }
            pointerUp = Input.GetMouseButtonUp(0);
            pointerHeld = Input.GetMouseButton(0);
        }

        void ReadSteer(bool driving)
        {
            float kx = 0f, ky = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) kx -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) kx += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) ky -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) ky += 1f;

            float px = 0f, py = 0f;
            if (driving && pointerHeld)
            {
                Vector2 d = pointerPos - pointerStart;
                px = Mathf.Clamp(d.x / (0.15f * Screen.width), -1f, 1f);
                py = Mathf.Clamp(d.y / (0.12f * Screen.height), -1f, 1f);
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
                gravity = Vector3.Lerp(gravity, a, 1f - Mathf.Exp(-25f * dt));
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
