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
        const int MapW = 72, MapH = 48;   // a ilha com mar em volta (era 64x40, com mar só embaixo e à esquerda)

        [MenuItem("Pixel/Montar cena da Vila")]
        public static void BuildAll()
        {
            ConfigureProjectForPixelArt();
            ImportSprites();
            ImportNinjaArt();
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
            asset.name = Path.GetFileNameWithoutExtension(path);   // o nome já vale nesta montagem (saves e buscas usam o nome)
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ---------- Dados do jogo ----------
        struct GameData { public ItemDef[] items; public BuildingDef[] buildings; public ItemDef food, rum; public Pixel.Ship.ShipBlockDef[] shipBlocks; public DungeonDef[] dungeons; public GearDef[] gear; }

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
                b.footprint = new Vector2Int(2, 2); b.cost = 30;
                b.profession = Profession.Agricultor; b.workerSlots = 1;
                b.outputItem = cane; b.outputAmount = 1; b.cycleSeconds = 6f;
                b.description = "Agricultor planta cana: +1 Cana a cada 6 s.";
            });
            farm.blocksMovement = true;
            farm.flat = true;   // ninguém atravessa a horta (antes o capitão passava por cima dela)
            EditorUtility.SetDirty(farm);
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
            var forge = GetOrCreate<BuildingDef>("Construcoes/Forja", b =>
            {
                b.displayName = "Forja"; b.kind = BuildingKind.Producer;
                b.footprint = new Vector2Int(4, 2); b.cost = 90;
                b.profession = Profession.Marujo; b.workerSlots = 1;
                b.forgesGear = true; b.goldPerPiece = 25; b.cycleSeconds = 45f;
                b.description = "Um ferreiro forja armas e chapéus: 1 peça a cada 45 s por 25 ouro. Quanto maior o nível dele, melhores as peças. Vão para o Arsenal (tecla I).";
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
                buildings = new[] { house, tavern, farm, fishing, distillery, forge, market, shipyard, board },
                gear = CreateGear(),
                dungeons = CreateDungeons(),
                shipBlocks = CreateShipBlocks(),
                food = fish,
                rum = rum,
            };
        }

        /// <summary>As peças de equipamento (Assets/Data/Equipamento). Só são criadas se ainda não existirem.</summary>
        static GearDef[] CreateGear()
        {
            GearDef G(string asset, string name, string icon, GearSlot slot, GearRarity rarity, int price, System.Action<GearDef> stats, string flavor = null) =>
                GetOrCreate<GearDef>("Equipamento/" + asset, g =>
                {
                    g.displayName = name; g.icon = Ninja("Equip/" + icon); g.slot = slot; g.rarity = rarity; g.price = price; g.flavor = flavor;
                    stats(g);
                });
            var W = GearSlot.Weapon; var H = GearSlot.Hat; var C = GearSlot.Charm;
            var c = GearRarity.Comum; var b = GearRarity.Bom; var r = GearRarity.Raro; var l = GearRarity.Lendario;
            var list = new[]
            {
                // Armas
                G("Pa", "Pá", "pa", W, c, 15, g => { g.damage = new Vector2Int(0, 1); g.digs = true; }, "Cava o X do tesouro. Como arma, quebra um galho."),
                G("Porrete", "Porrete", "arma_porrete", W, c, 20, g => { g.damage = new Vector2Int(2, 3); g.speed = -0.05f; }),
                G("Adaga", "Adaga", "arma_adaga", W, c, 25, g => { g.damage = new Vector2Int(1, 2); g.speed = 0.2f; }, "Pequena e rápida."),
                G("Sabre", "Sabre", "arma_sabre", W, b, 60, g => { g.damage = new Vector2Int(4, 6); }, "A arma de todo pirata."),
                G("Florete", "Florete", "arma_florete", W, b, 65, g => { g.damage = new Vector2Int(3, 4); g.speed = 0.25f; }, "Fino e muito rápido."),
                G("Machado", "Machado", "arma_machado", W, b, 70, g => { g.damage = new Vector2Int(6, 9); g.speed = -0.15f; }),
                G("Marreta", "Marreta", "arma_marreta", W, b, 70, g => { g.damage = new Vector2Int(7, 11); g.speed = -0.25f; }, "Pesada: bate forte, mas devagar."),
                G("EspadaLarga", "Espada Larga", "arma_espada_larga", W, r, 140, g => { g.damage = new Vector2Int(8, 12); g.speed = -0.05f; }),
                G("Arpao", "Arpão", "arma_arpao", W, r, 130, g => { g.damage = new Vector2Int(6, 9); g.range = 0.6f; }, "Acerta de mais longe."),
                G("Tridente", "Tridente", "arma_tridente", W, r, 150, g => { g.damage = new Vector2Int(7, 10); g.range = 0.4f; g.speed = 0.05f; }, "Dizem que era de um deus do mar."),
                G("SabreDourado", "Sabre Dourado", "arma_sabre_dourado", W, l, 400, g => { g.damage = new Vector2Int(10, 14); g.speed = 0.15f; g.loot = 0.1f; }, "Lâmina de ouro de um almirante afundado."),
                // Chapéus
                G("Bandana", "Bandana", "chapeu_bandana", H, c, 15, g => { g.hp = 10; }),
                G("ChapeuDeCouro", "Chapéu de Couro", "chapeu_chapeu_couro", H, b, 50, g => { g.hp = 22; }),
                G("Tricornio", "Tricórnio", "chapeu_tricornio", H, r, 120, g => { g.hp = 35; g.damage = new Vector2Int(1, 1); }, "Chapéu de capitão de verdade."),
                G("TricornioDoAlmirante", "Tricórnio do Almirante", "chapeu_tricornio_almirante", H, l, 350, g => { g.hp = 50; g.speed = 0.1f; }),
                // Amuletos
                G("MoedaDaSorte", "Moeda da Sorte", "amuleto_moeda_sorte", C, c, 30, g => { g.loot = 0.1f; }, "Mais ouro nos baús e saques."),
                G("Rubi", "Rubi", "amuleto_rubi", C, b, 70, g => { g.damage = new Vector2Int(2, 3); }),
                G("Safira", "Safira", "amuleto_safira", C, b, 70, g => { g.hp = 18; }),
                G("Esmeralda", "Esmeralda", "amuleto_esmeralda", C, r, 120, g => { g.speed = 0.18f; }),
                G("CaveiraDourada", "Caveira Dourada", "amuleto_caveira_dourada", C, l, 380, g => { g.damage = new Vector2Int(3, 4); g.hp = 10; g.loot = 0.2f; }, "Amaldiçoada? Talvez. Lucrativa? Com certeza."),
            };
            AssetDatabase.SaveAssets();
            return list;
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
            ApplyBuildingArt(data.buildings);
            ApplyItemArt(data.items);
            var looks = CreateLibrary();
            BuildSeaScene(data.shipBlocks);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Ilha de verdade: costa, praia, grama com variação, mata, campo e natureza (VillageSceneBuilder.Island.cs)
            var island = BuildIsland();
            var waterMap = island.water;
            var detailMap = island.detail;

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
            sr.sprite = looks.captain.down[0];
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            var ctrl = captain.AddComponent<CaptainController>();
            ctrl.down = looks.captain.down;
            ctrl.up = looks.captain.up;
            ctrl.left = looks.captain.left;
            ctrl.right = looks.captain.right;

            // Câmera pixel perfect
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(31.5f, 12f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 360f / 2f / PPU;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x3a, 0x8c, 0xc4, 0xff);   // mesma cor do mar fundo
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.allowDynamicResolution = false;
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;   // brilho, vinheta e cores
            AddPostProcessing();
            // Sem Pixel Perfect Camera: o CameraFollow escolhe um zoom inteiro (cada pixel da arte = 2, 3, 4... da tela)
            // que mostre ~16 blocos de altura em qualquer tamanho de janela, e a rodinha aproxima/afasta.
            var follow = camGo.AddComponent<CameraFollow>();
            follow.pixelsPerUnit = PPU;
            follow.target = captain.transform;
            follow.bounds = new Rect(0, 0, MapW, MapH);

            var lightGo = new GameObject("Luz Global");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            light.color = new Color(1f, 0.97f, 0.9f);
            lightGo.AddComponent<DayNightLight>();

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
            vm.shadowSprite = Ninja("Natureza/sombra_g");
            var buildMode = village.AddComponent<BuildMode>();
            village.AddComponent<Armory>().catalog = data.gear;   // equipamento guardado + o do capitão

            var visitors = new GameObject("Visitantes");
            visitors.transform.SetParent(village.transform, false);
            var spawner = visitors.AddComponent<VisitorSpawner>();
            spawner.spawnPoint = new Vector2(31.5f, 1f);
            spawner.down = Frames("Visitor", "visitor", "down");
            spawner.up = Frames("Visitor", "visitor", "up");
            spawner.left = Frames("Visitor", "visitor", "left");
            spawner.right = Frames("Visitor", "visitor", "right");
            spawner.looks = looks.visitors;

            var crewManager = village.AddComponent<CrewManager>();
            crewManager.food = data.food;
            crewManager.rum = data.rum;
            crewManager.dockPoint = new Vector2(31.5f, 1f);
            crewManager.idleArea = new Vector2(31.5f, 14f);
            crewManager.sprites = new CrewManager.LayerSprites
            {
                down = CrewDir("down"), up = CrewDir("up"), left = CrewDir("left"), right = CrewDir("right"),
            };
            crewManager.looks = looks;

            village.AddComponent<ChronicleManager>();

            var audioGo = new GameObject("Audio");
            var audio = audioGo.AddComponent<AudioManager>();
            AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/{n}.wav");
            AudioManager.SfxClip S(Sfx id, string file, float vol) => new AudioManager.SfxClip { id = id, clip = Clip("Sfx/" + file), volume = vol };
            audio.effects = new[]
            {
                S(Sfx.Coin, "sfx_coin", 0.5f), S(Sfx.Build, "sfx_build", 0.7f), S(Sfx.Cannon, "mar_tiro", 0.6f),
                S(Sfx.Boom, "mar_explosao", 0.6f), S(Sfx.Splash, "mar_respingo", 0.45f), S(Sfx.Swing, "sfx_swing", 0.4f),
                S(Sfx.Hit, "sfx_hit", 0.5f), S(Sfx.Chest, "sfx_chest", 0.7f), S(Sfx.Quest, "sfx_quest", 0.8f),
                S(Sfx.Click, "sfx_click", 0.4f),
                // Sons do mar (Ninja Adventure, CC0): substituem tiro, explosão e respingo antigos
                S(Sfx.Wood, "mar_madeira", 0.55f), S(Sfx.Hurt, "mar_ferido", 0.5f), S(Sfx.Gull, "mar_gaivota", 0.35f), S(Sfx.Thunder, "mar_trovao", 0.8f),
            };
            // Músicas (OpenGameArt; créditos em Assets/Audio/Music/CREDITOS.txt). Longas: tocam por streaming.
            AudioClip Music(string file)
            {
                string path = $"Assets/Audio/Music/{file}";
                if (AssetImporter.GetAtPath(path) is AudioImporter ai)
                {
                    var st = ai.defaultSampleSettings;
                    if (st.loadType != AudioClipLoadType.Streaming)
                    {
                        st.loadType = AudioClipLoadType.Streaming;
                        st.compressionFormat = AudioCompressionFormat.Vorbis;
                        st.quality = 0.6f;
                        ai.defaultSampleSettings = st;
                        ai.SaveAndReimport();
                    }
                }
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            audio.villageMusic = Music("musica_vila.mp3");
            audio.seaMusic = Music("musica_mar.ogg");
            audio.seaMusic2 = Music("musica_mar2.ogg");
            audio.battleMusic = Music("musica_combate.mp3");
            audio.seaAmbience = Clip("Ambiente/mar_ondas");
            audio.rainAmbience = Clip("Ambiente/chuva");
            audio.stormAmbience = Clip("Ambiente/tempestade");

            var expeditions = village.AddComponent<ExpeditionManager>();
            expeditions.dungeons = data.dungeons;
            expeditions.crab = new[] { Ninja("Monsters/caranguejo_0"), Ninja("Monsters/caranguejo_1") };
            expeditions.skeleton = new[] { Ninja("Monsters/esqueleto_0"), Ninja("Monsters/esqueleto_1") };
            expeditions.serpent = new[] { Ninja("Monsters/serpente_0"), Ninja("Monsters/serpente_1") };

            var flowGo = new GameObject("Jogo");
            var flow = flowGo.AddComponent<GameFlow>();
            flow.shipBlocks = data.shipBlocks;
            flow.raftHull = data.shipBlocks[0];
            flow.raftMast = data.shipBlocks[2];
            flow.raftSail = data.shipBlocks[3];
            // Pelo arquivo: ao montar a cena do Mar o Unity descarrega os blocos da memória (a busca por nome falharia)
            flow.raftSlope = AssetDatabase.LoadAssetAtPath<Pixel.Ship.ShipBlockDef>($"{DataDir}/Navio/CascoRampa.asset");
            flow.villageCamera = camGo;
            flow.villageLight = lightGo;
            flow.captain = captain;
            flow.buildMode = buildMode;
            flow.boardingArea = new Rect(30f, 0f, 3f, 3.5f);
            flow.returnPoint = new Vector2(31.5f, 4f);

            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<VillageHUD>();
            hud.buildMode = buildMode;
            hud.goldIcon = Ninja("Icons/ouro");
            hud.crewIcon = Ninja("Icons/tripulacao");
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
