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
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            float waterline = SeaCenter.y - 6f + ShipLayout.WaterRow + 0.5f;   // 996.5

            // Câmera
            var camGo = new GameObject("Camera do Mar");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(SeaCenter.x, SeaCenter.y, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 360f / 2f / PPU;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(135, 200, 232, 255);
            cam.allowHDR = false;
            cam.allowMSAA = false;
            camGo.AddComponent<UniversalAdditionalCameraData>();
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = PPU;
            ppc.refResolutionX = 640;
            ppc.refResolutionY = 360;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
            var seaCam = camGo.AddComponent<SeaCamera>();

            var light = new GameObject("Luz Global do Mar").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            // Cenário
            var scenery = new GameObject("Cenario").transform;
            var deepSprite = LoadSprite("Sea/sea_water_deep");
            var topSprite = LoadSprite("Sea/sea_water_top");
            float deepBottom = SeaCenter.y - 30f;
            float deepHeight = waterline - 0.5f - deepBottom;
            var backDeep = Tiled(scenery, "AguaFundo", deepSprite, new Vector2(0, deepBottom + deepHeight / 2f), new Vector2(90, deepHeight), -50, Color.white);
            var frontDeep = Tiled(scenery, "AguaFrenteFundo", deepSprite, new Vector2(0, deepBottom + deepHeight / 2f), new Vector2(90, deepHeight), 20, new Color(1, 1, 1, 0.55f));
            var backTop = Tiled(scenery, "AguaSuperficie", topSprite, new Vector2(0, waterline), new Vector2(90, 1), -49, Color.white);
            var frontTop = Tiled(scenery, "AguaFrenteSuperficie", topSprite, new Vector2(0, waterline), new Vector2(90, 1), 21, new Color(1, 1, 1, 0.55f));

            var cloudSprite = LoadSprite("Sea/sea_cloud");
            var clouds = new Transform[6];
            var rng = new System.Random(3);
            for (int i = 0; i < clouds.Length; i++)
            {
                var c = new GameObject("Nuvem").AddComponent<SpriteRenderer>();
                c.transform.SetParent(scenery, false);
                c.sprite = cloudSprite;
                c.sortingOrder = -90;
                c.transform.position = new Vector3(-30f + i * 11f + rng.Next(-3, 3), SeaCenter.y + 4f + rng.Next(0, 6), 0);
                clouds[i] = c.transform;
            }

            var island = new GameObject("IlhaDistante").AddComponent<SpriteRenderer>();
            island.transform.SetParent(scenery, false);
            island.sprite = LoadSprite("Sea/sea_island");
            island.sortingOrder = -80;
            island.transform.position = new Vector3(-15f, waterline - 0.3f, 0);
            island.transform.localScale = new Vector3(1.6f, 1.6f, 1);

            var dock = new GameObject("Docas").transform;
            dock.SetParent(scenery, false);
            var plank = LoadSprite("Sea/sea_dock_plank");
            var post = LoadSprite("Sea/sea_dock_post");
            for (int x = -26; x <= -5; x++)
            {
                var p = new GameObject("Tabua").AddComponent<SpriteRenderer>();
                p.transform.SetParent(dock, false);
                p.sprite = plank;
                p.sortingOrder = -40;
                p.transform.position = new Vector3(x + 0.5f, waterline + 1f, 0);
            }
            for (int x = -24; x <= -5; x += 5)
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

            // Capitão no convés
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
            capsule.size = new Vector2(0.6f, 1.35f);
            capsule.offset = new Vector2(0, 0.68f);
            capsule.sharedMaterial = noFriction;
            var sr = capGo.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite("Captain/captain_right_0");
            sr.sortingOrder = 10;
            var side = capGo.AddComponent<SideCaptainController>();
            side.left = Frames("Captain", "captain", "left");
            side.right = Frames("Captain", "captain", "right");
            var capFighter = capGo.AddComponent<Combatant>();
            capFighter.team = Team.Player;
            capFighter.displayName = "Capitão";
            capFighter.maxHp = capFighter.hp = 100;
            capFighter.damage = new Vector2Int(16, 24);
            capFighter.attackCooldown = 0.45f;
            capFighter.attackRange = 1.25f;
            capFighter.knockOutInsteadOfDeath = true;

            // Controle e HUD
            var seaGo = new GameObject("Mar");
            var scroller = seaGo.AddComponent<SeaScroller>();
            scroller.waterLayers = new[] { backDeep, frontDeep, backTop, frontTop };
            scroller.clouds = clouds;
            scroller.farScenery = island.transform;
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
            director.flagPirate = LoadSprite("Fx/flag_pirate");
            director.flagMerchant = LoadSprite("Fx/flag_merchant");
            director.plankSprite = LoadSprite("Sea/sea_dock_plank");
            director.fxPuff = LoadSprite("Fx/fx_puff");
            director.fxBoom = LoadSprite("Fx/fx_boom");
            director.fxSplash = LoadSprite("Fx/fx_splash");
            director.fxSlash = LoadSprite("Fx/fx_slash");
            director.fxCannonball = LoadSprite("Fx/fx_cannonball");
            director.crewSprites = new Pixel.Village.CrewManager.LayerSprites
            {
                down = CrewDir("down"), up = CrewDir("up"), left = CrewDir("left"), right = CrewDir("right"),
            };

            var islandGo = new GameObject("Ilhas");
            var islands = islandGo.AddComponent<IslandBuilder>();
            islands.sea = sea;
            islands.director = director;
            islands.grassTop = LoadSprite("Island/land_grass_top");
            islands.dirt = LoadSprite("Island/land_dirt");
            islands.sandTop = LoadSprite("Island/land_sand_top");
            islands.sand = LoadSprite("Island/land_sand");
            islands.chestClosed = LoadSprite("Island/chest_closed");
            islands.chestOpen = LoadSprite("Island/chest_open");
            islands.tent = LoadSprite("Island/tent");
            islands.campfire = LoadSprite("Island/campfire");
            islands.palm = LoadSprite("obj_palm");
            islands.market = LoadSprite("obj_market");
            islands.plank = LoadSprite("Sea/sea_dock_plank");
            islands.crabFrames = new[] { LoadSprite("Island/crab_0"), LoadSprite("Island/crab_1") };
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
