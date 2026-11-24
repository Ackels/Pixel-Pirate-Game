using UnityEditor;
using UnityEngine;

namespace Pixel.EditorTools
{
    /// <summary>
    /// Importa as peças da interface (Assets/Resources/UI, geradas por tools/arte/ui_export.py) como pixel art:
    /// filtro Point, sem compressão e com as bordas de 9 fatias de cada peça.
    /// </summary>
    public class UISkinImporter : AssetPostprocessor
    {
        const string Folder = "Assets/Resources/UI/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var imp = (TextureImporter)assetImporter;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 100;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(settings);

            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            // Borda (esquerda, baixo, direita, cima) em pixels da imagem
            imp.spriteBorder = file switch
            {
                "moldura" => new Vector4(6, 8, 6, 8),
                "botao" or "botao_verde" => new Vector4(3, 5, 3, 3),
                "papel" => new Vector4(4, 4, 4, 4),
                _ => Vector4.zero,
            };
            imp.wrapMode = file == "tabuas" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        }
    }
}
