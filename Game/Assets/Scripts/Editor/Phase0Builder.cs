using System.Collections.Generic;
using System.IO;
using Pixel.Village;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace Pixel.EditorTools
{
    /// <summary>
    /// Fase 0: configura a arte provisória (pixel perfect) e monta a cena da vila.
    /// Menu: Pixel > Fase 0 > Montar cena da Vila. Pode ser rodado de novo sem problemas.
    /// </summary>
    public static class Phase0Builder
    {
        const string ArtDir = "Assets/Art/Placeholder";
        const string TileDir = "Assets/Art/Tiles";
        const string ScenePath = "Assets/Scenes/Village.unity";
        const int PPU = 16;
        const int MapW = 64, MapH = 40;

        [MenuItem("Pixel/Fase 0/Montar cena da Vila")]
        public static void BuildAll()
        {
            ConfigureProjectForPixelArt();
            ImportSprites();
            BuildVillageScene();
            Debug.Log("[Pixel] Fase 0 montada: " + ScenePath);
        }

        // ---------- Configurações do projeto ----------
        static void ConfigureProjectForPixelArt()
        {
            PlayerSettings.productName = "Enseada dos Corsários";
            PlayerSettings.companyName = "Pixel";

            // Sem anti-aliasing em nenhum nível de qualidade (borra o pixel).
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.antiAliasing = 0;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            }
            QualitySettings.SetQualityLevel(current, false);

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.msaaSampleCount = 1;
                EditorUtility.SetDirty(urp);
            }

            // Ordenação "de cima": quem está mais embaixo na tela aparece na frente.
            var renderer2D = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/Renderer2D.asset");
            if (renderer2D != null)
            {
                var so = new SerializedObject(renderer2D);
                var mode = so.FindProperty("m_TransparencySortMode");
                var axis = so.FindProperty("m_TransparencySortAxis");
                if (mode != null) mode.intValue = (int)TransparencySortMode.CustomAxis;
                if (axis != null) axis.vector3Value = new Vector3(0, 1, 0);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
        }

        // ---------- Importação dos sprites ----------
        static void ImportSprites()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtDir });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool isTile = Path.GetFileName(path).StartsWith("tile_");

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PPU;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)(isTile ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        static Sprite LoadSprite(string relPath) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{relPath}.png");

        static Tile GetTile(string spriteName)
        {
            Directory.CreateDirectory(TileDir);
            string path = $"{TileDir}/{spriteName}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = LoadSprite(spriteName);
            tile.colliderType = Tile.ColliderType.Grid;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        static Sprite[] Frames(string dir) => new[]
        {
            LoadSprite($"Captain/captain_{dir}_0"),
            LoadSprite($"Captain/captain_{dir}_1"),
            LoadSprite($"Captain/captain_{dir}_2"),
        };

        // ---------- Cena ----------
        static void BuildVillageScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rng = new System.Random(42);

            var grass = new[] { GetTile("tile_grass_0"), GetTile("tile_grass_1"), GetTile("tile_grass_2") };
            var sand = GetTile("tile_sand");
            var water = GetTile("tile_water");
            var path = GetTile("tile_path");
            var dock = GetTile("tile_dock");
            AssetDatabase.SaveAssets();

            // Grid + camadas de tilemap
            var grid = new GameObject("Grid", typeof(Grid));
            Tilemap groundMap = CreateTilemap(grid.transform, "Chao", -20);
            Tilemap waterMap = CreateTilemap(grid.transform, "Agua", -21);
            Tilemap detailMap = CreateTilemap(grid.transform, "Caminhos_Docas", -19);

            // Colisão da água: o capitão não entra no mar.
            var waterCol = waterMap.gameObject.AddComponent<TilemapCollider2D>();
            var waterBody = waterMap.gameObject.AddComponent<Rigidbody2D>();
            waterBody.bodyType = RigidbodyType2D.Static;
            waterMap.gameObject.AddComponent<CompositeCollider2D>();
            waterCol.compositeOperation = Collider2D.CompositeOperation.Merge;

            bool IsWater(int x, int y)
            {
                if (y < 6) return true;                       // mar ao sul
                if (x < 5 && y < 26) return true;              // costa oeste
                if (x < 9 && y < 10) return true;              // curva da enseada
                return false;
            }
            bool IsSand(int x, int y) => !IsWater(x, y) && (IsWater(x, y - 2) || IsWater(x - 2, y) || IsWater(x - 2, y - 2));

            // Docas: píer que entra no mar (x 30..32, y 0..8)
            bool IsDock(int x, int y) => x >= 30 && x <= 32 && y >= 0 && y < 9;

            for (int x = 0; x < MapW; x++)
            for (int y = 0; y < MapH; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (IsWater(x, y))
                {
                    if (IsDock(x, y)) detailMap.SetTile(cell, dock);
                    else waterMap.SetTile(cell, water);
                    if (IsDock(x, y)) groundMap.SetTile(cell, water); // água "decorativa" sem colisão sob o píer
                }
                else if (IsSand(x, y)) groundMap.SetTile(cell, sand);
                else groundMap.SetTile(cell, grass[rng.Next(100) < 70 ? 0 : rng.Next(1, 3)]);
            }

            // Caminhos de terra
            var pathCells = new HashSet<Vector2Int>();
            for (int y = 6; y < 32; y++) for (int x = 30; x <= 32; x++) pathCells.Add(new Vector2Int(x, y));
            for (int x = 10; x < 58; x++) for (int y = 19; y <= 20; y++) pathCells.Add(new Vector2Int(x, y));
            foreach (var c in pathCells) detailMap.SetTile((Vector3Int)c, path);

            // Objetos
            var objects = new GameObject("Objetos").transform;
            var housesAt = new[] { new Vector2(23.5f, 22f), new Vector2(39.5f, 22f), new Vector2(23.5f, 13f), new Vector2(40.5f, 13f), new Vector2(50.5f, 22f) };
            foreach (var p in housesAt)
                CreateProp(objects, "Casa", LoadSprite("obj_house"), p, new Vector2(2.6f, 1.2f), new Vector2(0, 0.6f));

            var barrels = new[] { new Vector2(28.5f, 9.2f), new Vector2(28.5f, 10.4f), new Vector2(34.5f, 9.4f) };
            foreach (var p in barrels)
                CreateProp(objects, "Barril", LoadSprite("obj_barrel"), p, new Vector2(0.6f, 0.4f), new Vector2(0, 0.25f));

            // Coqueiros espalhados, evitando caminhos, casas e água
            var palm = LoadSprite("obj_palm");
            int placed = 0;
            for (int tries = 0; tries < 2000 && placed < 55; tries++)
            {
                int x = rng.Next(1, MapW - 1), y = rng.Next(1, MapH - 1);
                if (IsWater(x, y) || IsDock(x, y)) continue;
                bool nearPath = false;
                for (int dx = -2; dx <= 2 && !nearPath; dx++)
                    for (int dy = -1; dy <= 3 && !nearPath; dy++)
                        if (pathCells.Contains(new Vector2Int(x + dx, y + dy))) nearPath = true;
                if (nearPath) continue;
                bool nearHouse = false;
                foreach (var h in housesAt)
                    if (Mathf.Abs(h.x - x) < 4f && y - h.y > -2f && y - h.y < 4f) nearHouse = true;
                if (nearHouse) continue;
                if (Mathf.Abs(x - 31) < 4 && Mathf.Abs(y - 12) < 4) continue; // área do spawn
                CreateProp(objects, "Coqueiro", palm, new Vector2(x + 0.5f, y), new Vector2(0.4f, 0.3f), new Vector2(0, 0.15f));
                placed++;
            }

            // Bordas invisíveis do mapa
            var bounds = new GameObject("LimitesDoMapa").transform;
            AddWall(bounds, new Vector2(MapW / 2f, -0.5f), new Vector2(MapW, 1));
            AddWall(bounds, new Vector2(MapW / 2f, MapH + 0.5f), new Vector2(MapW, 1));
            AddWall(bounds, new Vector2(-0.5f, MapH / 2f), new Vector2(1, MapH));
            AddWall(bounds, new Vector2(MapW + 0.5f, MapH / 2f), new Vector2(1, MapH));

            // Capitão
            var captain = new GameObject("Capitao");
            captain.transform.position = new Vector3(31.5f, 12f, 0);
            var body = captain.AddComponent<Rigidbody2D>();
            body.gravityScale = 0;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var feet = captain.AddComponent<CapsuleCollider2D>();
            feet.direction = CapsuleDirection2D.Horizontal;
            feet.size = new Vector2(0.6f, 0.3f);
            feet.offset = new Vector2(0, 0.15f);
            var sr = captain.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite("Captain/captain_down_0");
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            var ctrl = captain.AddComponent<CaptainController>();
            ctrl.down = Frames("down");
            ctrl.up = Frames("up");
            ctrl.left = Frames("left");
            ctrl.right = Frames("right");

            // Câmera pixel perfect (640x360, 16 px por unidade)
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(31.5f, 12f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 360f / 2f / PPU;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x1d, 0x7a, 0x96, 0xff);
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.allowDynamicResolution = false;
            camGo.AddComponent<UniversalAdditionalCameraData>();
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = PPU;
            ppc.refResolutionX = 640;
            ppc.refResolutionY = 360;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
            ppc.cropFrame = PixelPerfectCamera.CropFrame.None;
            var follow = camGo.AddComponent<CameraFollow>();
            follow.target = captain.transform;
            follow.bounds = new Rect(0, 0, MapW, MapH);

            // Luz global 2D (sem ela os sprites "Lit" ficam pretos)
            var lightGo = new GameObject("Luz Global");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            light.color = new Color(1f, 0.97f, 0.9f);

            EditorSceneManager.SaveScene(scene, ScenePath);

            // Coloca a vila como primeira cena do build
            var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != ScenePath) list.Add(s);
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();
        }

        static Tilemap CreateTilemap(Transform parent, string name, int order)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<TilemapRenderer>().sortingOrder = order;
            return go.GetComponent<Tilemap>();
        }

        static void CreateProp(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 colSize, Vector2 colOffset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = colSize;
            col.offset = colOffset;
        }

        static void AddWall(Transform parent, Vector2 center, Vector2 size)
        {
            var go = new GameObject("Parede");
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.AddComponent<BoxCollider2D>().size = size;
        }
    }
}
