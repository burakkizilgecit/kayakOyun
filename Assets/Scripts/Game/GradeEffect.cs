using UnityEngine;

namespace SledSurfers
{
    /// Ana kameraya tek geçişli renk ayarı uygular (bkz. Resources/Shaders/Grade.shader).
    /// Değerleri parkur teması belirler; shader bulunamazsa sessizce devre dışı kalır.
    [RequireComponent(typeof(Camera))]
    public class GradeEffect : MonoBehaviour
    {
        public float contrast = 1.08f, saturation = 1.15f, lift, vignette = 0.25f;
        public Color tint = Color.white;

        Material mat;

        void Awake()
        {
            var shader = Resources.Load<Shader>("Shaders/Grade");
            if (shader != null && shader.isSupported) mat = new Material(shader);
            else enabled = false;
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            mat.SetFloat("_Contrast", contrast);
            mat.SetFloat("_Saturation", saturation);
            mat.SetFloat("_Lift", lift);
            mat.SetFloat("_Vignette", vignette);
            mat.SetColor("_Tint", tint);
            Graphics.Blit(src, dst, mat);
        }
    }
}
