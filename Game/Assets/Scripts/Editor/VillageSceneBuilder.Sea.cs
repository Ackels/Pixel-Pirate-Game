using Pixel.Combat;
using Pixel.Ship;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Pixel.EditorTools
{
    /// <summary>Parte do montador que cria a cena do Mar (visão lateral).</summary>
    public static partial class VillageSceneBuilder
    {
        const string SeaScenePath = "Assets/Scenes/Sea.unity";
        static readonly Vector2 SeaCenter = new Vector2(0f, 1000f);   // o mar fica longe da vila no mundo

        static ShipBlockDef[] CreateShipBlocks()
        {
            ShipBlockDef Block(string file, string name, BlockRole role, int cost, bool solid, int order, int hp,
                int sail, int bunks, int cannons, string desc, string sprite) =>
                GetOrCreate<ShipBlockDef>($"Navio/{file}", b =>
                {
                    b.displayName = name; b.role = role; b.cost = cost; b.solid = solid; b.sortingOrder = order;
                    b.hp = hp; b.sailPower = sail; b.bunks = bunks; b.cannons = cannons; b.description = desc;
                    b.sprite = LoadSprite($"Ship/{sprite}");
                });

            return new[]
            {
                Block("Casco", "Casco", BlockRole.Hull, 15, true, 0, 10, 0, 0, 0, "Estrutura do navio: +10 de vida. Pode ir abaixo d'água.", "ship_hull"),
                Block("Conves", "Convés", BlockRole.Deck, 10, true, 0, 6, 0, 0, 0, "Piso mais leve e barato: +6 de vida.", "ship_deck"),
                Block("Mastro", "Mastro", BlockRole.Mast, 20, false, -11, 2, 0, 0, 0, "Segura as velas. Precisa de apoio embaixo.", "ship_mast"),
                Block("Vela", "Vela", BlockRole.Sail, 25, false, -10, 1, 1, 0, 0, "Mais velas = mais velocidade (o peso do navio atrapalha).", "ship_sail"),
                Block("Canhao", "Canhão", BlockRole.Cannon, 60, false, 5, 2, 0, 0, 1, "Poder de fogo para a Fase 4. Precisa de piso embaixo.", "ship_cannon"),
                Block("Cabine", "Cabine", BlockRole.Cabin, 30, false, -12, 2, 0, 1, 0, "Alojamento: +1 tripulante no navio.", "ship_cabin"),
            };
        }

        static void BuildSeaScene(ShipBlockDef[] blocks)
        {
            ImportMarArt();
            ApplyShipArt(blocks);
            var looks = CreateLibrary();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            float waterline = SeaCenter.y - 6f + ShipLayout.WaterRow + 0.5f;   // 996.5

            // Câmera: blocos de 32 px; 960x540 de referência = 30 blocos de largura
            var camGo = new GameObject("Camera do Mar");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(SeaCenter.x, SeaCenter.y, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 540f / 2f / SeaPPU;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0xdd, 0xc6, 0xa1, 255);   // céu do Treasure Hunters
            cam.allowHDR = false;
            cam.allowMSAA = false;
            camGo.AddComponent<UniversalAdditionalCameraData>();
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = SeaPPU;
            ppc.refResolutionX = 960;
            ppc.refResolutionY = 540;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
            var seaCam = camGo.AddComponent<SeaCamera>();

            var light = new GameObject("Luz Global do Mar").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            var dayNight = light.gameObject.AddComponent<Pixel.Village.DayNightLight>();
            dayNight.tintCamera = cam;
            dayNight.skyColor = cam.backgroundColor;

            // Cenário: horizonte, nuvens e água
            var scenery = new GameObject("Cenario").transform;
            // Horizonte em cópias soltas (no modo repetido, a borda de cima "puxa" a linha azul de baixo)
            var horizon = Mar("Ceu/horizonte");
            for (float hx = -60f; hx <= 60f; hx += 12f)
            {
                var h = new GameObject("Horizonte").AddComponent<SpriteRenderer>();
                h.transform.SetParent(scenery, false);
                h.sprite = horizon;
                h.sortingOrder = -95;
                h.transform.position = new Vector3(hx, waterline + 1.5f, 0);
            }

            float deepBottom = SeaCenter.y - 30f;
            float deepTop = waterline + 0.5f;
            float deepHeight = deepTop - deepBottom;
            // Fundo opaco atrás de tudo; na frente, a superfície (ondas) e uma camada translúcida que tinge o que está submerso
            var backDeep = Tiled(scenery, "AguaFundo", Mar("Ceu/agua_fundo"), new Vector2(0, deepBottom + deepHeight / 2f), new Vector2(90, deepHeight), -50, Color.white);
            var frontDeep = Tiled(scenery, "AguaFrente", Mar("Ceu/agua_baixo"), new Vector2(0, deepBottom + (deepHeight - 1f) / 2f), new Vector2(90, deepHeight - 1f), 20, Color.white);
            var frontTop = Tiled(scenery, "AguaSuperficie", Mar("Ceu/agua_topo"), new Vector2(0, waterline), new Vector2(90, 1), 21, Color.white);

            var cloudSprites = new[] { Mar("Ceu/nuvem_0"), Mar("Ceu/nuvem_1"), Mar("Ceu/nuvem_2") };
            var clouds = new Transform[6];
            var rng = new System.Random(3);
            for (int i = 0; i < clouds.Length; i++)
            {
                var c = new GameObject("Nuvem").AddComponent<SpriteRenderer>();
                c.transform.SetParent(scenery, false);
                c.sprite = cloudSprites[i % cloudSprites.Length];
                c.sortingOrder = -90;
                c.transform.position = new Vector3(-30f + i * 11f + rng.Next(-3, 3), SeaCenter.y + 3f + rng.Next(0, 5), 0);
                clouds[i] = c.transform;
            }
            var bigClouds = new Transform[6];
            for (int i = 0; i < bigClouds.Length; i++)
            {
                var c = new GameObject("NuvemGrande").AddComponent<SpriteRenderer>();
                c.transform.SetParent(scenery, false);
                c.sprite = Mar("Ceu/nuvem_grande");
                c.sortingOrder = -92;
                c.transform.position = new Vector3(-40f + i * 14f, waterline + 3.6f, 0);
                bigClouds[i] = c.transform;
            }

            // Docas da Enseada (à esquerda do navio, ficam para trás ao zarpar)
            var dock = new GameObject("Docas").transform;
            dock.SetParent(scenery, false);
            var plank = Mar("Navio/tabua");
            var post = Mar("Navio/mastro");
            for (int x = -26; x <= -5; x++)
            {
                var p = new GameObject("Tabua").AddComponent<SpriteRenderer>();
                p.transform.SetParent(dock, false);
                p.sprite = plank;
                p.sortingOrder = -40;
                p.transform.position = new Vector3(x + 0.5f, waterline + 1f, 0);
            }
            for (int x = -24; x <= -5; x += 4)
                for (float y = waterline + 0.5f; y > waterline - 6f; y -= 1f)
                {
                    var p = new GameObject("Poste").AddComponent<SpriteRenderer>();
                    p.transform.SetParent(dock, false);
                    p.sprite = post;
                    p.sortingOrder = -41;
                    p.transform.position = new Vector3(x + 0.5f, y, 0);
                }

            // Navio
            var shipGo = new GameObject("Navio", typeof(Rigidbody2D), typeof(CompositeCollider2D));
            shipGo.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            shipGo.transform.position = new Vector3(SeaCenter.x - ShipLayout.Width / 2f, SeaCenter.y - 6f, 0);
            var view = shipGo.AddComponent<ShipView>();

            var editorGo = new GameObject("Estaleiro");
            var editor = editorGo.AddComponent<ShipEditor>();
            editor.view = view;
            editor.gridSprite = LoadSprite("Ship/ship_grid");

            // Capitão no convés: o mesmo personagem da vila, de perfil
            const string matPath = DataDir + "/Fisica/SemAtrito.physicsMaterial2D";
            var noFriction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(matPath);
            if (noFriction == null)
            {
                System.IO.Directory.CreateDirectory(DataDir + "/Fisica");
                noFriction = new PhysicsMaterial2D("SemAtrito") { friction = 0f, bounciness = 0f };
                AssetDatabase.CreateAsset(noFriction, matPath);
            }
            var capGo = new GameObject("Capitao (lateral)");
            capGo.transform.position = new Vector3(0, waterline + 2f, 0);
            var body = capGo.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var capsule = capGo.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.55f, 0.9f);
            capsule.offset = new Vector2(0, 0.45f);
            capsule.sharedMaterial = noFriction;
            var sr = capGo.AddComponent<SpriteRenderer>();
            sr.sprite = looks.captain.right[0];
            sr.sortingOrder = 10;
            var side = capGo.AddComponent<SideCaptainController>();
            side.left = looks.captain.left;
            side.right = looks.captain.right;
            side.attackLeft = looks.captain.attack[2];
            side.attackRight = looks.captain.attack[3];
            var capFighter = capGo.AddComponent<Combatant>();
            capFighter.team = Team.Player;
            capFighter.displayName = "Capitão";
            capFighter.maxHp = capFighter.hp = 100;
            capFighter.damage = new Vector2Int(16, 24);
            capFighter.attackCooldown = 0.45f;
            capFighter.attackRange = 1.25f;
            capFighter.knockOutInsteadOfDeath = true;
            capFighter.figureHeight = 1f;

            // Controle e HUD
            var seaGo = new GameObject("Mar");
            var scroller = seaGo.AddComponent<SeaScroller>();
            scroller.waterLayers = new[] { backDeep, frontDeep, frontTop };
            scroller.waterPeriod = 3f;   // a onda do Treasure Hunters tem 3 blocos de largura
            scroller.clouds = clouds;
            scroller.farClouds = bigClouds;
            scroller.farCloudWrapLeft = -44f;
            scroller.farCloudWrapWidth = bigClouds.Length * 14f;
            scroller.nearScenery = dock;
            var sea = seaGo.AddComponent<SeaController>();
            sea.shipView = view;
            sea.editor = editor;
            sea.captain = side;
            sea.scroller = scroller;
            sea.captainCombatant = capFighter;
            sea.fallbackBlocks = blocks;
            sea.fallbackHull = blocks[0];
            sea.fallbackMast = blocks[2];
            sea.fallbackSail = blocks[3];

            var director = seaGo.AddComponent<EncounterDirector>();
            director.sea = sea;
            director.seaCamera = seaCam;
            director.flagPirate = Mar("Navio/bandeira_pirata");
            director.flagMerchant = Mar("Navio/bandeira_mercante");
            director.plankSprite = Mar("Navio/prancha");
            director.fxPuff = Mar("Fx/poeira");
            director.fxBoom = Mar("Fx/explosao");
            director.fxSplash = Mar("Fx/respingo");
            director.fxSlash = Mar("Fx/golpe");
            director.fxCannonball = Mar("Fx/bala");
            director.crewSprites = new Pixel.Village.CrewManager.LayerSprites
            {
                down = CrewDir("down"), up = CrewDir("up"), left = CrewDir("left"), right = CrewDir("right"),
            };
            director.looks = looks;

            var islandGo = new GameObject("Ilhas");
            var islands = islandGo.AddComponent<IslandBuilder>();
            islands.sea = sea;
            islands.director = director;
            islands.grass = TerrainSet("terra");
            islands.sand = TerrainSet("areia");
            islands.chestClosed = Mar("Ilha/bau_fechado");
            islands.chestOpen = Mar("Ilha/bau_aberto");
            islands.tent = Ninja("Props/tenda_lado");   // a tenda do pacote é vista de cima
            islands.campfire = Ninja("Props/fogueira");
            islands.palm = Mar("Ilha/coqueiro");
            islands.backPalms = new[] { Mar("Ilha/coqueiro_fundo_0"), Mar("Ilha/coqueiro_fundo_1"), Mar("Ilha/coqueiro_fundo_2") };
            islands.grassDecor = Mar("Ilha/grama");
            islands.market = Ninja("Props/barraca_lado");   // barraca vista de lado
            islands.plank = Mar("Navio/prancha");
            islands.creatures = new[]
            {
                new IslandBuilder.Creature { name = "Caranguejo", frames = MarFrames("Bichos/caranguejo") },
                new IslandBuilder.Creature { name = "Estrela-do-mar", frames = MarFrames("Bichos/estrela") },
                new IslandBuilder.Creature { name = "Baleote", frames = MarFrames("Bichos/baleote") },
            };
            sea.islands = islands;

            var hud = new GameObject("HUD do Mar").AddComponent<SeaHUD>();
            hud.mapIcons = new[]
            {
                LoadSprite("Map/map_paper"), LoadSprite("Map/map_home"), LoadSprite("Map/map_port"), LoadSprite("Map/map_wild"),
                LoadSprite("Map/map_camp"), LoadSprite("Map/map_unknown"), LoadSprite("Map/map_ship"),
            };
            hud.sea = sea;

            EditorSceneManager.SaveScene(scene, SeaScenePath);
        }

        static Transform Tiled(Transform parent, string name, Sprite sprite, Vector2 center, Vector2 size, int order, Color color)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = size;
            sr.sortingOrder = order;
            sr.color = color;
            sr.transform.position = center;
            return sr.transform;
        }
    }
}
