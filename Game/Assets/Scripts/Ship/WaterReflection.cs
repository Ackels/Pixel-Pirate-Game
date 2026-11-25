using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Pixel.Ship
{
    /// <summary>
    /// Reflexo na água do mar (como no Pixel Piracy): uma segunda câmera fotografa a cena acima da linha d'água e
    /// um retângulo embaixo dela mostra essa imagem espelhada, ondulando e sumindo com a profundidade
    /// (shader Pixel/WaterReflection). O retângulo fica na camada "Water", que a câmera de reflexo não enxerga.
    /// </summary>
    [DefaultExecutionOrder(100)]   // depois da SeaCamera (que move a câmera principal)
    public class WaterReflection : MonoBehaviour
    {
        public Camera mainCam;
        public float waterline;
        public Material material;
        [Range(0, 1)] public float strength = 0.42f;
        public float fade = 7f;
        [Tooltip("Imagem com metade da resolução da tela (fica macia, mais bonito e mais leve).")]
        public int downscale = 2;

        const int WaterLayer = 4, UILayer = 5;
        Camera reflCam;
        RenderTexture rt;
        Material mat;
        Transform quad;

        void Start()
        {
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null || material == null) { enabled = false; return; }
            var go = new GameObject("CameraDoReflexo");
            go.transform.SetParent(transform, false);
            reflCam = go.AddComponent<Camera>();
            reflCam.orthographic = true;
            reflCam.cullingMask = ~((1 << WaterLayer) | (1 << UILayer));
            reflCam.clearFlags = CameraClearFlags.SolidColor;
            reflCam.depth = mainCam.depth - 1;
            reflCam.allowHDR = false;
            reflCam.allowMSAA = false;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;

            mat = new Material(material);
            var q = new GameObject("ReflexoNaAgua", typeof(MeshFilter), typeof(MeshRenderer));
            q.layer = WaterLayer;
            q.transform.SetParent(transform, false);
            var mesh = new Mesh { name = "Retangulo" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            q.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.sortingOrder = 22;   // por cima da água (20 e 21)
            quad = q.transform;
        }

        void OnDestroy()
        {
            if (rt != null) { rt.Release(); Destroy(rt); }
            if (mat != null) Destroy(mat);
        }

        void LateUpdate()
        {
            if (reflCam == null || mainCam == null) return;
            int w = Mathf.Max(64, Screen.width / Mathf.Max(1, downscale)), h = Mathf.Max(64, Screen.height / Mathf.Max(1, downscale));
            if (rt == null || rt.width != w || rt.height != h)
            {
                if (rt != null) { reflCam.targetTexture = null; rt.Release(); Destroy(rt); }
                rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { name = "Reflexo", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                reflCam.targetTexture = rt;
                mat.mainTexture = rt;
            }
            reflCam.transform.position = mainCam.transform.position;
            reflCam.orthographicSize = mainCam.orthographicSize;
            reflCam.aspect = mainCam.aspect;
            reflCam.backgroundColor = mainCam.backgroundColor;

            float hh = mainCam.orthographicSize, hw = hh * mainCam.aspect;
            Vector2 c = mainCam.transform.position;
            float bottom = c.y - hh - 1f;
            float top = waterline;
            if (top <= bottom) { quad.gameObject.SetActive(false); return; }
            quad.gameObject.SetActive(true);
            quad.position = new Vector3(c.x, (top + bottom) / 2f, 0f);
            quad.localScale = new Vector3(2f * hw + 2f, top - bottom, 1f);
            mat.SetVector("_Cam", new Vector4(c.x, c.y, hw, hh));
            mat.SetFloat("_Waterline", waterline);
            mat.SetFloat("_Strength", strength);
            mat.SetFloat("_Fade", fade);
        }
    }
}
