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
    /// Monta a cena da vila do zero: configura a arte (pixel perfect), cria os dados
    /// (itens e construções, só se ainda não existirem) e gera Village.unity.
    /// Menu: Pixel > Montar cena da Vila. ATENÇÃO: recria a cena (edições manuais nela se perdem),
    /// mas NÃO sobrescreve os dados em Assets/Data (pode ajustar preços e tempos à vontade).
    /// </summary>
    public static partial class VillageSceneBuilder
    {
        const string ArtDir = "Assets/Art/Placeholder";
        const string TileDir = "Assets/Art/Tiles";
        const string DataDir = "Assets/Data";
        const string ScenePath = "Assets/Scenes/Village.unity";
        const int PPU = 16;
        const int MapW = 64, MapH = 40;

        [MenuItem("Pixel/Montar cena da Vila")]
        public static void BuildAll()
        {
            ConfigureProjectForPixelArt();
            ImportSprites();
            BuildVillageScene();
            Debug.Log("[Pixel] Cena da vila montada: " + ScenePath);
        }

        // ---------- Configurações do projeto ----------
        static void ConfigureProjectForPixelArt()
        {
            PlayerSettings.productName = "Enseada dos Corsários";
            PlayerSettings.companyName = "Pixel";

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
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                string file = Path.GetFileName(path);
                bool centered = file.StartsWith("tile_") || file.StartsWith("icon_") || file.StartsWith("ship_")
                             || file.StartsWith("fx_") || file.StartsWith("flag_")
                             || file.StartsWith("land_") || file.StartsWith("map_")
                             || (file.StartsWith("sea_") && !file.StartsWith("sea_island"));

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PPU;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)(centered ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        static Sprite LoadSprite(string relPath) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{relPath}.png");

        static Sprite[] Frames(string folder, string prefix, string dir) => new[]
        {
            LoadSprite($"{folder}/{prefix}_{dir}_0"),
            LoadSprite($"{folder}/{prefix}_{dir}_1"),
            LoadSprite($"{folder}/{prefix}_{dir}_2"),
        };

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

        /// <summary>Carrega o asset de dados; se não existir, cria com os valores iniciais.</summary>
        static T GetOrCreate<T>(string relPath, System.Action<T> init) where T : ScriptableObject
        {
            string path = $"{DataDir}/{relPath}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ---------- Dados do jogo ----------
        struct GameData { public ItemDef[] items; public BuildingDef[] buildings; public ItemDef food, rum; public Pixel.Ship.ShipBlockDef[] shipBlocks; public DungeonDef[] dungeons; }

        static GameData CreateData()
        {
            var cane = GetOrCreate<ItemDef>("Itens/Cana", i =>
            {
                i.displayName = "Cana"; i.icon = LoadSprite("Icons/icon_cane"); i.sellPrice = 3;
                i.textColor = new Color(0.6f, 0.95f, 0.45f);
            });
            var rum = GetOrCreate<ItemDef>("Itens/Rum", i =>
            {
                i.displayName = "Rum"; i.icon = LoadSprite("Icons/icon_rum"); i.sellPrice = 15; i.reservePerCrew = 1;
                i.textColor = new Color(1f, 0.7f, 0.4f);
            });
            var fish = GetOrCreate<ItemDef>("Itens/Peixe", i =>
            {
                i.displayName = "Peixe"; i.icon = LoadSprite("Icons/icon_fish"); i.sellPrice = 4; i.reservePerCrew = 2;
                i.textColor = new Color(0.6f, 0.85f, 1f);
            });

            var house = GetOrCreate<BuildingDef>("Construcoes/Casa", b =>
            {
                b.displayName = "Casa"; b.kind = BuildingKind.House; b.sprite = LoadSprite("obj_house");
                b.footprint = new Vector2Int(3, 2); b.cost = 40; b.housing = 2;
                b.description = "Moradia para 2 tripulantes. Sem casa, a moral cai.";
            });
            var tavern = GetOrCreate<BuildingDef>("Construcoes/Taverna", b =>
            {
                b.displayName = "Taverna"; b.kind = BuildingKind.Tavern; b.sprite = LoadSprite("obj_tavern");
                b.footprint = new Vector2Int(3, 2); b.cost = 60;
                b.description = "Onde se contratam piratas. Novos recrutas a cada dia.";
            });
            var farm = GetOrCreate<BuildingDef>("Construcoes/Horta", b =>
            {
                b.displayName = "Horta"; b.kind = BuildingKind.Producer; b.sprite = LoadSprite("obj_farm");
                b.footprint = new Vector2Int(2, 2); b.cost = 30; b.blocksMovement = false;
                b.profession = Profession.Agricultor; b.workerSlots = 1;
                b.outputItem = cane; b.outputAmount = 1; b.cycleSeconds = 6f;
                b.description = "Agricultor planta cana: +1 Cana a cada 6 s.";
            });
            var fishing = GetOrCreate<BuildingDef>("Construcoes/CabanaDePesca", b =>
            {
                b.displayName = "Cabana de Pesca"; b.kind = BuildingKind.Producer; b.sprite = LoadSprite("obj_fishing_hut");
                b.footprint = new Vector2Int(2, 2); b.cost = 40; b.requiresWater = true;
                b.profession = Profession.Pescador; b.workerSlots = 1;
                b.outputItem = fish; b.outputAmount = 1; b.cycleSeconds = 8f;
                b.description = "Pescador traz +1 Peixe a cada 8 s. Precisa ficar na beira d'água.";
            });
            var distillery = GetOrCreate<BuildingDef>("Construcoes/Destilaria", b =>
            {
                b.displayName = "Destilaria"; b.kind = BuildingKind.Producer; b.sprite = LoadSprite("obj_distillery");
                b.footprint = new Vector2Int(3, 2); b.cost = 80;
                b.profession = Profession.Destilador; b.workerSlots = 1;
                b.inputItem = cane; b.inputAmount = 2; b.outputItem = rum; b.outputAmount = 1; b.cycleSeconds = 10f;
                b.description = "Destilador transforma 2 Cana em 1 Rum a cada 10 s.";
            });
            var shipyard = GetOrCreate<BuildingDef>("Construcoes/Estaleiro", b =>
            {
                b.displayName = "Estaleiro"; b.kind = BuildingKind.Shipyard; b.sprite = LoadSprite("obj_shipyard");
                b.footprint = new Vector2Int(3, 2); b.cost = 100;
                b.description = "Libera a edição do navio nas docas.";
            });
            var board = GetOrCreate<BuildingDef>("Construcoes/QuadroDeExpedicoes", b =>
            {
                b.displayName = "Quadro de Expedições"; b.kind = BuildingKind.Expedition; b.sprite = LoadSprite("obj_expedition_board");
                b.footprint = new Vector2Int(2, 1); b.cost = 80;
                b.description = "Mande grupos de até 4 tripulantes para masmorras. Combate automático!";
            });
            var market = GetOrCreate<BuildingDef>("Construcoes/Mercado", b =>
            {
                b.displayName = "Mercado"; b.kind = BuildingKind.Market; b.sprite = LoadSprite("obj_market");
                b.footprint = new Vector2Int(3, 2); b.cost = 50;
                b.description = "Visitantes desembarcam nas docas e compram seu estoque aqui.";
            });
            AssetDatabase.SaveAssets();
            return new GameData
            {
                items = new[] { rum, fish, cane },
                buildings = new[] { house, tavern, farm, fishing, distillery, market, shipyard, board },
                dungeons = CreateDungeons(),
                shipBlocks = CreateShipBlocks(),
                food = fish,
                rum = rum,
            };
        }

        static DungeonDef[] CreateDungeons()
        {
            DungeonStage St(params EnemyType[] e) => new DungeonStage { enemies = new List<EnemyType>(e) };
            var C = EnemyType.Caranguejo; var P = EnemyType.Pirata; var E = EnemyType.Esqueleto; var S = EnemyType.Serpente; var B = EnemyType.CapitaoLendario;
            var d1 = GetOrCreate<DungeonDef>("Masmorras/1_CavernaDosCaranguejos", d =>
            {
                d.displayName = "Caverna dos Caranguejos"; d.recommendedLevel = 1; d.enemyPower = 1f; d.travelSeconds = 10f;
                d.description = "Uma gruta úmida cheia de caranguejos mal-humorados. Boa para começar.";
                d.stages = new List<DungeonStage> { St(C, C, C), St(C, C, C, C), St(C, C, C, C, C) };
                d.goldReward = new Vector2Int(60, 90); d.itemReward = "Peixe"; d.itemAmount = new Vector2Int(5, 10);
                d.background = new Color(0.25f, 0.22f, 0.2f);
            });
            var d2 = GetOrCreate<DungeonDef>("Masmorras/2_NaufragioAssombrado", d =>
            {
                d.displayName = "Naufrágio Assombrado"; d.recommendedLevel = 2; d.enemyPower = 1.15f; d.travelSeconds = 14f;
                d.description = "O casco de um galeão afundado, onde os mortos ainda guardam o rum.";
                d.stages = new List<DungeonStage> { St(E, E), St(E, E, E), St(E, E, P, P) };
                d.goldReward = new Vector2Int(120, 180); d.itemReward = "Rum"; d.itemAmount = new Vector2Int(3, 6);
                d.background = new Color(0.12f, 0.2f, 0.32f);
            });
            var d3 = GetOrCreate<DungeonDef>("Masmorras/3_TemploDaSerpente", d =>
            {
                d.displayName = "Templo da Serpente"; d.recommendedLevel = 3; d.enemyPower = 1.1f; d.travelSeconds = 18f;
                d.description = "Ruínas na selva tomadas por serpentes gigantes. Leve rum!";
                d.stages = new List<DungeonStage> { St(S, S), St(E, E, E, S), St(S, S, S) };
                d.goldReward = new Vector2Int(220, 320); d.itemReward = "Rum"; d.itemAmount = new Vector2Int(5, 9);
                d.background = new Color(0.14f, 0.28f, 0.16f);
            });
            var d4 = GetOrCreate<DungeonDef>("Masmorras/4_CovilDoCapitaoLendario", d =>
            {
                d.displayName = "Covil do Capitão Lendário"; d.recommendedLevel = 4; d.enemyPower = 1.15f; d.travelSeconds = 22f;
                d.description = "A fortaleza do pirata mais temido dos mares. Só os melhores voltam.";
                d.stages = new List<DungeonStage> { St(P, P, P), St(P, P, P, P), St(B, P, P) };
                d.goldReward = new Vector2Int(400, 600); d.itemReward = "Rum"; d.itemAmount = new Vector2Int(8, 14);
                d.background = new Color(0.3f, 0.12f, 0.14f);
            });
            AssetDatabase.SaveAssets();
            return new[] { d1, d2, d3, d4 };
        }

        static CrewManager.DirSprites CrewDir(string dir)
        {
            Sprite[] L(string layer) => new[]
            {
                LoadSprite($"Crew/crew_{layer}_{dir}_0"), LoadSprite($"Crew/crew_{layer}_{dir}_1"), LoadSprite($"Crew/crew_{layer}_{dir}_2"),
            };
            return new CrewManager.DirSprites { skin = L("skin"), outfit = L("outfit"), hair = L("hair"), baseLayer = L("base") };
        }

        // ---------- Cena ----------
        static void BuildVillageScene()
        {
            var data = CreateData();
            BuildSeaScene(data.shipBlocks);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rng = new System.Random(42);

            var grass = new[] { GetTile("tile_grass_0"), GetTile("tile_grass_1"), GetTile("tile_grass_2") };
            var sand = GetTile("tile_sand");
            var water = GetTile("tile_water");
            var path = GetTile("tile_path");
            var dock = GetTile("tile_dock");
            AssetDatabase.SaveAssets();

            var grid = new GameObject("Grid", typeof(Grid));
            Tilemap groundMap = CreateTilemap(grid.transform, "Chao", -20);
            Tilemap waterMap = CreateTilemap(grid.transform, "Agua", -21);
            Tilemap detailMap = CreateTilemap(grid.transform, "Caminhos_Docas", -19);

            var waterCol = waterMap.gameObject.AddComponent<TilemapCollider2D>();
            var waterBody = waterMap.gameObject.AddComponent<Rigidbody2D>();
            waterBody.bodyType = RigidbodyType2D.Static;
            waterMap.gameObject.AddComponent<CompositeCollider2D>();
            waterCol.compositeOperation = Collider2D.CompositeOperation.Merge;

            bool IsWater(int x, int y) => y < 6 || (x < 5 && y < 26) || (x < 9 && y < 10);
            bool IsSand(int x, int y) => !IsWater(x, y) && (IsWater(x, y - 2) || IsWater(x - 2, y) || IsWater(x - 2, y - 2));
            bool IsDock(int x, int y) => x >= 30 && x <= 32 && y >= 0 && y < 9;

            for (int x = 0; x < MapW; x++)
            for (int y = 0; y < MapH; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (IsWater(x, y))
                {
                    if (IsDock(x, y)) { detailMap.SetTile(cell, dock); groundMap.SetTile(cell, water); }
                    else waterMap.SetTile(cell, water);
                }
                else if (IsSand(x, y)) groundMap.SetTile(cell, sand);
                else groundMap.SetTile(cell, grass[rng.Next(100) < 70 ? 0 : rng.Next(1, 3)]);
            }

            var pathCells = new HashSet<Vector2Int>();
            for (int y = 6; y < 32; y++) for (int x = 30; x <= 32; x++) pathCells.Add(new Vector2Int(x, y));
            for (int x = 10; x < 58; x++) for (int y = 19; y <= 20; y++) pathCells.Add(new Vector2Int(x, y));
            foreach (var c in pathCells) detailMap.SetTile((Vector3Int)c, path);

            // Objetos do cenário
            var objects = new GameObject("Cenario").transform;
            foreach (var p in new[] { new Vector2(28.5f, 9.2f), new Vector2(28.5f, 10.4f), new Vector2(34.5f, 9.4f) })
                CreateProp(objects, "Barril", LoadSprite("obj_barrel"), p, new Vector2(0.6f, 0.4f), new Vector2(0, 0.25f));

            // Coqueiros: mais nas bordas, uma clareira no meio para construir.
            var palm = LoadSprite("obj_palm");
            int placed = 0;
            for (int tries = 0; tries < 3000 && placed < 50; tries++)
            {
                int x = rng.Next(1, MapW - 1), y = rng.Next(1, MapH - 1);
                if (IsWater(x, y) || IsDock(x, y)) continue;
                bool nearPath = false;
                for (int dx = -2; dx <= 2 && !nearPath; dx++)
                    for (int dy = -1; dy <= 3 && !nearPath; dy++)
                        if (pathCells.Contains(new Vector2Int(x + dx, y + dy))) nearPath = true;
                if (nearPath) continue;
                bool clearing = x > 11 && x < 55 && y > 8 && y < 31;
                if (clearing && rng.Next(100) < 85) continue;
                CreateProp(objects, "Coqueiro", palm, new Vector2(x + 0.5f, y), new Vector2(0.4f, 0.3f), new Vector2(0, 0.15f));
                placed++;
            }

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
            ctrl.down = Frames("Captain", "captain", "down");
            ctrl.up = Frames("Captain", "captain", "up");
            ctrl.left = Frames("Captain", "captain", "left");
            ctrl.right = Frames("Captain", "captain", "right");

            // Câmera pixel perfect
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

            var lightGo = new GameObject("Luz Global");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            light.color = new Color(1f, 0.97f, 0.9f);

            // Sistemas da vila (Fase 1)
            var village = new GameObject("Vila");
            var vm = village.AddComponent<VillageManager>();
            vm.startingGold = 200;
            vm.items = data.items;
            vm.buildable = data.buildings;
            vm.mapWidth = MapW;
            vm.mapHeight = MapH;
            vm.waterMap = waterMap;
            vm.detailMap = detailMap;
            var buildMode = village.AddComponent<BuildMode>();

            var visitors = new GameObject("Visitantes");
            visitors.transform.SetParent(village.transform, false);
            var spawner = visitors.AddComponent<VisitorSpawner>();
            spawner.spawnPoint = new Vector2(31.5f, 1f);
            spawner.down = Frames("Visitor", "visitor", "down");
            spawner.up = Frames("Visitor", "visitor", "up");
            spawner.left = Frames("Visitor", "visitor", "left");
            spawner.right = Frames("Visitor", "visitor", "right");

            var crewManager = village.AddComponent<CrewManager>();
            crewManager.food = data.food;
            crewManager.rum = data.rum;
            crewManager.dockPoint = new Vector2(31.5f, 1f);
            crewManager.idleArea = new Vector2(31.5f, 14f);
            crewManager.sprites = new CrewManager.LayerSprites
            {
                down = CrewDir("down"), up = CrewDir("up"), left = CrewDir("left"), right = CrewDir("right"),
            };

            var expeditions = village.AddComponent<ExpeditionManager>();
            expeditions.dungeons = data.dungeons;
            expeditions.crab = new[] { LoadSprite("Island/crab_0"), LoadSprite("Island/crab_1") };
            expeditions.skeleton = new[] { LoadSprite("Dungeon/skeleton_0"), LoadSprite("Dungeon/skeleton_1") };
            expeditions.serpent = new[] { LoadSprite("Dungeon/serpent_0"), LoadSprite("Dungeon/serpent_1") };

            var flowGo = new GameObject("Jogo");
            var flow = flowGo.AddComponent<GameFlow>();
            flow.shipBlocks = data.shipBlocks;
            flow.raftHull = data.shipBlocks[0];
            flow.raftMast = data.shipBlocks[2];
            flow.raftSail = data.shipBlocks[3];
            flow.villageCamera = camGo;
            flow.villageLight = lightGo;
            flow.captain = captain;
            flow.buildMode = buildMode;
            flow.boardingArea = new Rect(30f, 0f, 3f, 3.5f);
            flow.returnPoint = new Vector2(31.5f, 4f);

            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<VillageHUD>();
            hud.buildMode = buildMode;
            hud.goldIcon = LoadSprite("Icons/icon_gold");
            hud.crewIcon = LoadSprite("Icons/icon_crew");
            flow.villageHUD = hudGo;

            EditorSceneManager.SaveScene(scene, ScenePath);

            var list = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(SeaScenePath, true),
            };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != ScenePath && s.path != SeaScenePath) list.Add(s);
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
