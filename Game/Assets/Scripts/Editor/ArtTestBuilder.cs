using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Pixel.EditorTools
{
    /// <summary>
    /// Cenas de TESTE da arte nova (Ninja Adventure na vila, Treasure Hunters + Pirate Bomb no mar).
    /// Só para aprovar o visual: não têm lógica de jogo. Menu: Pixel > Montar cenas de teste de arte.
    /// </summary>
    public static class ArtTestBuilder
    {
        const string Dir = "Assets/Art/ArtTest";
        const string VillageScene = "Assets/Scenes/ArtTest_Village.unity";
        const string SeaScene = "Assets/Scenes/ArtTest_Sea.unity";

        [MenuItem("Pixel/Montar cenas de teste de arte")]
        public static void BuildAll()
        {
            Import("Village", 16);
            Import("Sea", 32);
            Import("SeaChars", 16); // bonecos da vila vistos de lado (16 px, aparecem 2x maiores no mar)
            BuildVillage();
            BuildSea();
            Debug.Log("[Pixel] Cenas de teste de arte montadas.");
        }

        static void Import(string sub, int ppu)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { $"{Dir}/{sub}" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                string f = Path.GetFileName(path);
                bool centered = f.StartsWith("tile_") || f.StartsWith("sea_") || f.StartsWith("ship_") || f.StartsWith("island_");
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = ppu;
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
                imp.SaveAndReimport();
            }
        }

        static Sprite S(string sub, string name)
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>($"{Dir}/{sub}/{name}.png");
            if (sp == null) Debug.LogWarning("[ArtTest] sprite não encontrado: " + name);
            return sp;
        }

        static SpriteRenderer Put(Transform parent, Sprite sp, float x, float y, int order = 0, bool flip = false)
        {
            var go = new GameObject(sp != null ? sp.name : "?");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingOrder = order;
            sr.flipX = flip;
            return sr;
        }

        static Camera MakeCamera(int ppu, Vector3 pos, Color bg)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = pos;
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 360f / 2f / ppu;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.allowHDR = false; cam.allowMSAA = false; cam.allowDynamicResolution = false;
            camGo.AddComponent<UniversalAdditionalCameraData>();
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = ppu;
            ppc.refResolutionX = 640; ppc.refResolutionY = 360;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
            ppc.cropFrame = PixelPerfectCamera.CropFrame.None;
            var light = new GameObject("Luz Global").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            return cam;
        }

        // ---------------- Vila (vista de cima, 16 px) ----------------
        static void BuildVillage()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeCamera(16, new Vector3(20f, 11.5f, -10f), new Color32(0x3e, 0x9f, 0xc8, 0xff));
            var ground = new GameObject("Chao").transform;
            var objs = new GameObject("Objetos").transform;
            const string V = "Village";
            var rng = new System.Random(7);

            int vx0 = 18, vx1 = 20, vyBottom = 7;      // caminho vertical
            int hy0 = 13, hy1 = 15, hx0 = 4, hx1 = 36; // caminho horizontal
            for (int x = -1; x <= 41; x++)
                for (int y = -1; y <= 24; y++)
                {
                    string t;
                    bool inV = x >= vx0 && x <= vx1 && y >= vyBottom;
                    bool inH = y >= hy0 && y <= hy1 && x >= hx0 && x <= hx1;
                    if (y <= 2) t = "tile_water";
                    else if (y == 3) t = "tile_shore";
                    else if (y <= 6) t = rng.Next(5) == 0 ? "tile_sand_" + (1 + rng.Next(2)) : "tile_sand_0";
                    else if (inV && inH) t = "tile_dirt_c";
                    else if (inV)
                    {
                        string row = y == vyBottom ? "b" : "";
                        t = x == vx0 ? (row == "b" ? "tile_dirt_bl" : "tile_dirt_l")
                          : x == vx1 ? (row == "b" ? "tile_dirt_br" : "tile_dirt_r")
                          : (row == "b" ? "tile_dirt_b" : "tile_dirt_c");
                    }
                    else if (inH)
                    {
                        bool left = x == hx0, right = x == hx1;
                        t = y == hy1 ? (left ? "tile_dirt_tl" : right ? "tile_dirt_tr" : "tile_dirt_t")
                          : y == hy0 ? (left ? "tile_dirt_bl" : right ? "tile_dirt_br" : "tile_dirt_b")
                          : (left ? "tile_dirt_l" : right ? "tile_dirt_r" : "tile_dirt_c");
                    }
                    else t = rng.Next(6) == 0 ? "tile_grass_" + (1 + rng.Next(4)) : "tile_grass_0";
                    Put(ground, S(V, t), x + 0.5f, y + 0.5f, -100);
                }
            // Píer
            for (int y = -1; y <= 4; y++)
                for (int x = 19; x <= 20; x++)
                    Put(ground, S(V, "tile_dock"), x + 0.5f, y + 0.5f, -99);
            Put(objs, S(V, "obj_boat"), 25f, 0.6f);

            // Prédios
            Put(objs, S(V, "obj_house_a"), 7f, 16.2f);
            Put(objs, S(V, "obj_house_b"), 12f, 16.2f);
            Put(objs, S(V, "obj_tavern"), 27f, 16.2f);
            Put(objs, S(V, "obj_shop"), 32.5f, 16.2f);
            Put(objs, S(V, "obj_market"), 10f, 9f);
            Put(objs, S(V, "obj_farm"), 27f, 8f);
            Put(objs, S(V, "obj_house_a"), 6f, 8f);
            // Barris e caixas nas docas
            Put(objs, S(V, "obj_barrel"), 17.5f, 4.2f);
            Put(objs, S(V, "obj_barrel"), 22.5f, 4.2f);
            Put(objs, S(V, "obj_crate"), 23.5f, 4.2f);
            Put(objs, S(V, "obj_crate"), 16.5f, 4.6f);
            // Árvores e moitas
            float[,] trees = { { 2, 19 }, { 4.5f, 21 }, { 1.5f, 10 }, { 36.5f, 19.5f }, { 38.5f, 9 }, { 35, 8 }, { 15.5f, 20.5f }, { 23.5f, 20 }, { 39, 21.5f }, { 15, 8 } };
            string[] treeKinds = { "obj_tree_a", "obj_tree_b", "obj_tree_c" };
            for (int i = 0; i < trees.GetLength(0); i++)
                Put(objs, S(V, treeKinds[i % 3]), trees[i, 0], trees[i, 1]);
            float[,] bushes = { { 3, 12 }, { 13, 11 }, { 31, 11 }, { 37, 13 }, { 9.5f, 20 }, { 30, 21 } };
            for (int i = 0; i < bushes.GetLength(0); i++)
                Put(objs, S(V, i % 2 == 0 ? "obj_bush" : "obj_flower"), bushes[i, 0], bushes[i, 1]);

            // Personagens: capitão no cruzamento, tripulação em volta
            Put(objs, S(V, "char_0_Noble"), 19.5f, 11f);
            Put(objs, S(V, "char_parrot"), 20.7f, 11.1f);
            Put(objs, S(V, "char_1_Boy"), 7f, 10.5f);
            Put(objs, S(V, "char_2_Hunter"), 26f, 6f);
            Put(objs, S(V, "char_3_Child"), 23.5f, 14f);
            Put(objs, S(V, "char_4_Villager"), 11f, 7.5f);
            Put(objs, S(V, "char_5_Villager4"), 28.5f, 14f);
            Put(objs, S(V, "char_6_OldMan"), 21.5f, 5f);
            Put(objs, S(V, "char_7_Shaman"), 14f, 14f);
            Put(objs, S(V, "char_8_Skeleton"), 33.5f, 13.6f);

            EditorSceneManager.SaveScene(scene, VillageScene);
        }

        static Color SkyColor = new Color32(0xdd, 0xc6, 0xa1, 0xff);

        // ---------------- Mar (vista de lado, 32 px) ----------------
        static void BuildSea()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            const string A = "Sea";
            Color sky = SkyColor;
            MakeCamera(32, new Vector3(10f, 5.6f, -10f), sky);
            var bg = new GameObject("Fundo").transform;
            var world = new GameObject("Mundo").transform;
            const float water = 3f; // linha d'água

            // Horizonte (BG Image tem 12x4 unidades; a parte de baixo é o mar distante)
            for (float x = -6; x < 30; x += 12) Put(bg, S(A, "sea_horizon"), x, water + 1.5f, -95);
            Put(bg, S(A, "sea_clouds"), 7f, 7.2f, -90);
            Put(bg, S(A, "sea_cloud_1"), 16f, 9.5f, -90);
            Put(bg, S(A, "sea_cloud_2"), 1f, 9.8f, -90);
            // Navio mercante ao longe
            Put(bg, S(A, "merchant"), 1.0f, water + 0.15f, -85);
            Put(bg, S(A, "sail"), 0.9f, water + 0.85f, -86);

            // Água: fundo opaco atrás de tudo + camada translúcida na frente (tinge o que está submerso)
            for (float x = -1.5f; x < 23; x += 3)
            {
                Put(world, S(A, "sea_water_top"), x, water - 0.5f, 20);
                for (float y = water - 1.5f; y > -2; y -= 1) Put(world, S(A, "sea_water_bottom"), x, y, 20);
            }
            for (float x = -1.5f; x < 23; x += 1)
                for (float y = water - 0.5f; y > -2; y -= 1) Put(bg, S(A, "sea_water_deep"), x, y, -80);

            // Navio do jogador (blocos de 1 unidade = 32 px)
            for (int x = 3; x <= 7; x++) Put(world, S(A, "ship_keel"), x + 0.5f, 2.5f, -20);
            for (int x = 2; x <= 8; x++) Put(world, S(A, "ship_hull"), x + 0.5f, 3.5f, -20);
            for (int x = 1; x <= 9; x++) Put(world, S(A, "ship_deck"), x + 0.5f, 4.5f, -20);
            for (int x = 7; x <= 8; x++)
                for (int y = 5; y <= 6; y++) Put(world, S(A, y == 6 ? "ship_deck" : "ship_hull"), x + 0.5f, y + 0.5f, -22);
            Put(world, S(A, "window"), 8f, 5.5f - 0.5f, -21);
            Put(world, S(A, "sail"), 4.5f, 5f, -18);
            Put(world, S(A, "sail"), 6f, 5f, -19);
            Put(world, S(A, "flag"), 5f, 5f, -23);
            Put(world, S(A, "helm"), 2.2f, 5f, -17);
            Put(world, S(A, "cannon"), 9.2f, 5f, -16, true);
            Put(world, S(A, "barrels"), 7f, 5f, -15);

            // Tripulação
            const string C = "SeaChars";
            Put(world, S(C, "side_Noble_right"), 3.4f, 5f, 0);
            Put(world, S(C, "side_Boy_right"), 5.4f, 5f, 1);
            Put(world, S(C, "side_Hunter_left"), 6.5f, 5f, 2);
            Put(world, S(C, "side_Child_right"), 1.6f, 5f, 3);
            Put(world, S(C, "side_Shaman_right"), 8.2f, 5f, 3);
            Put(world, S(C, "side_Skeleton_left"), 18f, 4.5f, 0);

            // Ilha à direita
            for (int x = 12; x <= 21; x++)
            {
                string top = x == 12 ? "island_tl" : "island_t";
                string mid = x == 12 ? "island_l" : "island_c";
                Put(world, S(A, top), x + 0.5f, 3.5f + 0.5f, -45);
                for (int y = 3; y >= -2; y--) Put(world, S(A, mid), x + 0.5f, y, -46);
            }
            Put(world, S(A, "palm_back"), 17f, 4.5f, -50);
            Put(world, S(A, "palm_back"), 20f, 4.5f, -50);
            Put(world, S(A, "palm_base"), 15f, 4.5f, -44);
            Put(world, S(A, "palm_trunk"), 15f, 5.5f, -44);
            Put(world, S(A, "palm_trunk"), 15f, 6.5f, -44);
            Put(world, S(A, "palm_top"), 15f, 7.2f, -43);
            Put(world, S(A, "island_grass"), 17.5f, 4.5f, -9);
            Put(world, S(A, "chest"), 19.2f, 4.5f, -10);
            Put(world, S(A, "crabby"), 13.8f, 4.5f, 0, true);
            Put(world, S(A, "pinkstar"), 16.6f, 4.5f, 0, true);
            Put(world, S(A, "fierce"), 20.2f, 4.5f, 0, true);

            EditorSceneManager.SaveScene(scene, SeaScene);
        }
    }
}
