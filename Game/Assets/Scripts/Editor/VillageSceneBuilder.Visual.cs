using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Pixel.EditorTools
{
    /// <summary>
    /// Visual "mais bonito" (inspirado no Pixel Piracy): pós-processamento (brilho suave nas luzes, vinheta, cores
    /// mais vivas), material do reflexo na água e o tamanho da câmera do mar. Os valores ficam aqui no código
    /// e são reaplicados a cada montagem.
    /// </summary>
    public static partial class VillageSceneBuilder
    {
        /// <summary>Meia altura da visão do mar, em blocos (9,6 ≈ 34 blocos de largura em 16:9).</summary>
        const float SeaCameraSize = 9.6f;
        const string VisualDir = "Assets/Data/Visual";

        static Material WaterReflectionMaterial()
        {
            Directory.CreateDirectory(VisualDir);
            string path = VisualDir + "/ReflexoAgua.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Pixel/WaterReflection");
            if (shader == null) { Debug.LogWarning("[Pixel] Shader do reflexo não encontrado."); return null; }
            if (mat == null)
            {
                mat = new Material(shader) { name = "ReflexoAgua" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_Tint", new Color(0.55f, 0.75f, 0.95f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static T Effect<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var c)) return c;
            c = profile.Add<T>(true);
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }

        /// <summary>Perfil de pós-processamento do jogo inteiro (vila e mar).</summary>
        static VolumeProfile LookProfile()
        {
            Directory.CreateDirectory(VisualDir);
            string path = VisualDir + "/Visual.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            var bloom = Effect<Bloom>(profile);
            bloom.active = true;
            bloom.threshold.Override(0.82f);
            bloom.intensity.Override(0.75f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.93f, 0.82f));
            var vignette = Effect<Vignette>(profile);
            vignette.active = true;
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.45f);
            var color = Effect<ColorAdjustments>(profile);
            color.active = true;
            color.saturation.Override(7f);
            color.contrast.Override(10f);
            color.postExposure.Override(0.05f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>Volume global (fica na cena da vila, que nunca descarrega: vale para a vila e para o mar).</summary>
        static void AddPostProcessing()
        {
            var go = new GameObject("Pos-processamento");
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 1f;
            v.sharedProfile = LookProfile();
        }
    }
}
