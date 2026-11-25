using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pixel.Village;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Pixel.EditorTools
{
    /// <summary>
    /// Arte do Ninja Adventure na vila: importação, peças de borda automáticas,
    /// visuais de personagem (CharacterLook) e sprites dos prédios.
    /// Os PNGs em Assets/Art/Ninja são gerados a partir dos pacotes baixados (pasta ArteBaixada).
    /// </summary>
    public static partial class VillageSceneBuilder
    {
        const string NinjaDir = "Assets/Art/Ninja";
        const string LookDir = "Assets/Data/Visuais";

        // ---------- Importação ----------
        static void ImportNinjaArt()
        {
            // Fonte da interface (Pixelify Sans Bold). Suavizada: a interface é redimensionada pela tela,
            // e a renderização "sem suavização" deforma as letras em tamanhos quebrados.
            const string fontPath = "Assets/Resources/Fonts/PixelifySans-Bold.ttf";
            if (AssetImporter.GetAtPath(fontPath) is TrueTypeFontImporter fi)
            {
                fi.fontRenderingMode = FontRenderingMode.Smooth;
                fi.fontTextureCase = FontTextureCase.Dynamic;
                fi.SaveAndReimport();
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { NinjaDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileName(path);
                bool sheet = file == "SpriteSheet.png";
                bool centered = path.Contains("/Auto/") || path.Contains("/Tiles/") || path.Contains("/Icons/") || file == "Faceset.png";
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
                imp.spritePixelsPerUnit = PPU;
                imp.filterMode = FilterMode.Point;
                imp.mipmapEnabled = false;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.alphaIsTransparency = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                var s = new TextureImporterSettings();
                imp.ReadTextureSettings(s);
                s.spriteAlignment = (int)(centered ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
                s.spriteMeshType = SpriteMeshType.FullRect;
                imp.SetTextureSettings(s);
                // Pergaminho do mapa: cantos de 32 px fixos, bordas e meio repetidos
                imp.spriteBorder = file == "mapa_papel.png" ? new Vector4(32, 32, 32, 32) : Vector4.zero;
                imp.SaveAndReimport();
                if (sheet) SliceGrid(imp, 16, Path.GetFileName(Path.GetDirectoryName(path)));
            }
        }

        /// <summary>Fatia uma folha em quadros de cell x cell chamados Nome_coluna_linha (linha 0 = topo).</summary>
        static void SliceGrid(TextureImporter imp, int cell, string prefix)
        {
            imp.GetSourceTextureWidthAndHeight(out int w, out int h);
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(imp);
            dp.InitSpriteEditorDataProvider();
            var old = dp.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            var rects = new List<SpriteRect>();
            for (int r = 0; r < h / cell; r++)
                for (int c = 0; c < w / cell; c++)
                {
                    string n = $"{prefix}_{c}_{r}";
                    rects.Add(new SpriteRect
                    {
                        name = n,
                        rect = new Rect(c * cell, h - (r + 1) * cell, cell, cell),
                        alignment = SpriteAlignment.BottomCenter,
                        pivot = new Vector2(0.5f, 0f),
                        spriteID = old.TryGetValue(n, out var id) ? id : GUID.Generate(),
                    });
                }
            dp.SetSpriteRects(rects.ToArray());
            var names = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            dp.Apply();
            imp.SaveAndReimport();
        }

        static Sprite Ninja(string relPath)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{NinjaDir}/{relPath}.png");
            if (s == null) Debug.LogWarning("[Pixel] Sprite não encontrado: " + relPath);
            return s;
        }

        // ---------- Visuais de personagem ----------
        static readonly Dictionary<string, CharacterLook> lookCache = new Dictionary<string, CharacterLook>();

        static CharacterLook Look(string name)
        {
            if (lookCache.TryGetValue(name, out var cached) && cached != null) return cached;
            string sheet = $"{NinjaDir}/Characters/{name}/SpriteSheet.png";
            var frames = AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>().ToDictionary(s => s.name);
            Sprite F(int c, int r) => frames.TryGetValue($"{name}_{c}_{r}", out var s) ? s : null;
            Sprite[] Walk(int c) => new[] { F(c, 0), F(c, 1), F(c, 2), F(c, 3) };

            Directory.CreateDirectory(LookDir);
            string path = $"{LookDir}/{name}.asset";
            var look = AssetDatabase.LoadAssetAtPath<CharacterLook>(path);
            if (look == null)
            {
                look = ScriptableObject.CreateInstance<CharacterLook>();
                AssetDatabase.CreateAsset(look, path);
            }
            // Folha do Ninja Adventure: colunas = baixo, cima, esquerda, direita; linhas 0-3 = caminhada; 4 = ataque.
            look.down = Walk(0); look.up = Walk(1); look.left = Walk(2); look.right = Walk(3);
            look.attack = new[] { F(0, 4), F(1, 4), F(2, 4), F(3, 4) };
            look.portrait = AssetDatabase.LoadAssetAtPath<Sprite>($"{NinjaDir}/Characters/{name}/Faceset.png");
            if (look.down[0] == null) Debug.LogWarning("[Pixel] Folha de personagem sem quadros: " + name);
            EditorUtility.SetDirty(look);
            lookCache[name] = look;
            return look;
        }

        static CharacterLibrary CreateLibrary()
        {
            string path = $"{LookDir}/Catalogo.asset";
            Directory.CreateDirectory(LookDir);
            var lib = AssetDatabase.LoadAssetAtPath<CharacterLibrary>(path);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<CharacterLibrary>();
                AssetDatabase.CreateAsset(lib, path);
            }
            lib.captain = Look("CapitaoPirata");   // Villager + tricórnio, barba e casaco (tools/arte/pirata_export.py)
            lib.crew = new[]
            {
                // Bonecos do Ninja Adventure "piratizados" (bandana, tapa-olho, listras): tools/arte/pirata_export.py
                "PirataBoy", "PirataOldMan", "PirataOldMan2", "PirataCavegirl", "PirataCaveman", "PirataCaveman2", "PirataVillager2",
                "PirataVillager3", "PirataVillager4", "PirataVillager5", "PirataWoman", "PirataHunter", "PirataPrincess", "PirataOldWoman",
            }.Select(Look).ToArray();
            lib.visitors = new[] { "Villager", "Villager2", "Villager3", "Villager5", "OldWoman", "Inspector", "Princess" }
                .Select(Look).ToArray();
            lib.skeleton = Look("Skeleton");
            lib.enemyPirate = Look("PirataInimigo");
            lib.legendCaptain = Look("CapitaoLendario");
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return lib;
        }

        // ---------- Prédios ----------
        static void ApplyBuildingArt(BuildingDef[] defs)
        {
            var map = new Dictionary<string, string>
            {
                { "Casa", "casa" }, { "Taverna", "taverna" }, { "Horta", "horta" }, { "CabanaDePesca", "cabana_pesca" },
                { "Destilaria", "destilaria" }, { "Mercado", "mercado" }, { "Estaleiro", "estaleiro" },
                { "QuadroDeExpedicoes", "quadro" }, { "Forja", "forja" },
                { "Lenhador", "lenhador" }, { "Pedreira", "pedreira" }, { "Fundicao", "fundicao" },
            };
            foreach (var d in defs)
            {
                if (d == null || !map.TryGetValue(d.name, out var file)) continue;
                var s = Ninja("Buildings/" + file);
                if (s == null) continue;
                d.sprite = s;
                d.icon = null; // a barra de construção usa o próprio desenho
                EditorUtility.SetDirty(d);
            }
            AssetDatabase.SaveAssets();
        }

        // ---------- Ícones dos itens ----------
        static void ApplyItemArt(ItemDef[] items)
        {
            var map = new Dictionary<string, string> { { "Cana", "cana" }, { "Rum", "rum" }, { "Peixe", "peixe" } };
            foreach (var i in items)
            {
                if (i == null || !map.TryGetValue(i.name, out var file)) continue;
                var s = Ninja("Icons/" + file);
                if (s == null) continue;
                i.icon = s;
                EditorUtility.SetDirty(i);
            }
            AssetDatabase.SaveAssets();
        }

        // ---------- Peças de borda automáticas ----------
        static readonly Dictionary<string, List<int>> autoMasks = new Dictionary<string, List<int>>();

        /// <summary>Bits: 1=N 2=NE 4=L 8=SE 16=S 32=SO 64=O 128=NO. Canto só conta com os dois lados.</summary>
        static int Mask(System.Func<int, int, bool> inRegion, int x, int y)
        {
            bool N = inRegion(x, y + 1), E = inRegion(x + 1, y), S = inRegion(x, y - 1), W = inRegion(x - 1, y);
            int m = 0;
            if (N) m |= 1;
            if (N && E && inRegion(x + 1, y + 1)) m |= 2;
            if (E) m |= 4;
            if (S && E && inRegion(x + 1, y - 1)) m |= 8;
            if (S) m |= 16;
            if (S && W && inRegion(x - 1, y - 1)) m |= 32;
            if (W) m |= 64;
            if (N && W && inRegion(x - 1, y + 1)) m |= 128;
            return m;
        }

        static Tile AutoTile(string set, int mask)
        {
            if (!autoMasks.TryGetValue(set, out var masks))
            {
                masks = new List<int>();
                foreach (var f in Directory.GetFiles($"{NinjaDir}/Auto", $"{set}_*.png"))
                    if (int.TryParse(Path.GetFileNameWithoutExtension(f).Substring(set.Length + 1), out int m)) masks.Add(m);
                autoMasks[set] = masks;
            }
            int best = 255, bestScore = int.MaxValue;
            foreach (int m in masks)
            {
                int edgesDiff = BitCount((m ^ mask) & 0b01010101);   // lados errados pesam muito
                int cornersDiff = BitCount((m ^ mask) & 0b10101010);
                int score = edgesDiff * 10 + cornersDiff;
                if (score < bestScore) { bestScore = score; best = m; }
            }
            return NinjaTileAsset($"Auto/{set}_{best:000}");
        }

        static int BitCount(int v) { int c = 0; while (v != 0) { c += v & 1; v >>= 1; } return c; }

        static readonly Dictionary<string, Tile> tileCache = new Dictionary<string, Tile>();

        static Tile NinjaTileAsset(string rel)
        {
            if (tileCache.TryGetValue(rel, out var t) && t != null) return t;
            string dir = $"{TileDir}/Ninja";
            Directory.CreateDirectory(dir);
            string path = $"{dir}/{rel.Replace('/', '_')}.asset";
            t = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (t == null)
            {
                t = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(t, path);
            }
            t.sprite = Ninja(rel);
            t.colliderType = Tile.ColliderType.Grid;
            EditorUtility.SetDirty(t);
            tileCache[rel] = t;
            return t;
        }
        // ---------- Mar (Treasure Hunters, 32 px por bloco) ----------
        const string MarDir = "Assets/Art/Mar";
        const int SeaPPU = 32;

        static void ImportMarArt()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { MarDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                // Objetos que ficam "em pé" no chão têm o pivô embaixo; peças de bloco, céu e efeitos, no centro.
                bool bottom = path.Contains("/Bichos/") || file.StartsWith("coqueiro") || file.StartsWith("bau_")
                              || file == "grama" || file.StartsWith("bandeira_");
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = SeaPPU;
                imp.filterMode = FilterMode.Point;
                imp.mipmapEnabled = false;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.alphaIsTransparency = true;
                imp.wrapMode = TextureWrapMode.Clamp;   // Repeat faz a borda de cima "puxar" a linha de baixo
                var s = new TextureImporterSettings();
                imp.ReadTextureSettings(s);
                s.spriteAlignment = (int)(bottom ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
                if (file == "bandeira" || file.StartsWith("flamula"))   // bloco do estaleiro: 48 px de largura, o mastro (colunas 12-19) fica no centro da célula
                {
                    s.spriteAlignment = (int)SpriteAlignment.Custom;
                    s.spritePivot = new Vector2(16f / 48f, 0.5f);
                }
                s.spriteMeshType = SpriteMeshType.FullRect;
                imp.SetTextureSettings(s);
                imp.SaveAndReimport();
            }
        }

        static Sprite Mar(string relPath)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{MarDir}/{relPath}.png");
            if (s == null) Debug.LogWarning("[Pixel] Sprite do mar não encontrado: " + relPath);
            return s;
        }

        static Sprite[] MarFrames(string relPrefix)
        {
            var list = new List<Sprite>();
            for (int i = 0; ; i++)
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{MarDir}/{relPrefix}_{i}.png");
                if (s == null) break;
                list.Add(s);
            }
            return list.ToArray();
        }

        static Pixel.Ship.IslandBuilder.TerrainSet TerrainSet(string prefix)
        {
            var masks = new List<int>();
            var sprites = new List<Sprite>();
            foreach (var f in Directory.GetFiles($"{MarDir}/Ilha", $"{prefix}_*.png"))
            {
                if (!int.TryParse(Path.GetFileNameWithoutExtension(f).Substring(prefix.Length + 1), out int m)) continue;
                masks.Add(m);
                sprites.Add(Mar($"Ilha/{prefix}_{m:000}"));
            }
            return new Pixel.Ship.IslandBuilder.TerrainSet { masks = masks.ToArray(), sprites = sprites.ToArray() };
        }

        /// <summary>Desenho das peças do navio: tudo fica em ConfigureShipBlocks (VillageSceneBuilder.Sea.cs).</summary>
        static void ApplyShipArt(Pixel.Ship.ShipBlockDef[] blocks) => ConfigureShipBlocks(blocks);
    }
}
