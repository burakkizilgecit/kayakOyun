using UnityEngine;

namespace SledSurfers
{
    /// Karlı parkurda kameranın önünde yağan kar: dünyada asılı küçük mesh tanecikler (kızak hızla içinden geçer).
    /// Parçacık shader'ı yerine zemin malzemesi kullanılır: yapıda shader eksik kalmaz.
    public static class Snowfall
    {
        static ParticleSystem system;

        public static void Set(Camera cam, bool on)
        {
            if (!on)
            {
                if (system != null) Object.Destroy(system.gameObject);
                system = null;
                return;
            }
            if (system != null) return;

            var go = new GameObject("Snowfall");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 9f, 22f);
            system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.12f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1600;
            main.prewarm = false;
            var em = system.emission;
            em.rateOverTime = 320f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(44f, 6f, 46f);
            // Hafif rüzgâr: tanecikler yana süzülür ve salınır.
            var vel = system.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.6f, 1.2f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.3f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.4f;

            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Mesh;
            pr.mesh = FlakeMesh();
            pr.sharedMaterial = Mats.Solid(new Color(1f, 1f, 1f), 0f);
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            system.Play();
        }

        /// 6 köşeli yassı tanecik (birkaç üçgen: yüzlerce tanecik ucuza çizilir).
        static Mesh FlakeMesh()
        {
            var m = new Mesh { name = "Flake" };
            var v = new Vector3[7];
            v[0] = Vector3.zero;
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                v[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.5f;
            }
            var tris = new int[36];
            for (int i = 0; i < 6; i++)
            {
                int a = i + 1, b = (i + 1) % 6 + 1;
                tris[i * 6 + 0] = 0; tris[i * 6 + 1] = a; tris[i * 6 + 2] = b;   // ön yüz
                tris[i * 6 + 3] = 0; tris[i * 6 + 4] = b; tris[i * 6 + 5] = a;   // arka yüz
            }
            m.vertices = v;
            m.triangles = tris;
            m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            m.RecalculateBounds();
            return m;
        }
    }
}
