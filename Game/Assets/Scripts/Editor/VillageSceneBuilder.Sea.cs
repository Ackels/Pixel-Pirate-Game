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

        /// <summary>
        /// Peças do estaleiro. Custo e atributos só são definidos quando o asset é criado (ajustes no Inspector
        /// ficam); forma, aba, família, desenho e ordem de desenho são aplicados a cada montagem (ConfigureShipBlocks).
        /// Os 6 primeiros mantêm os nomes antigos (saves guardam o nome do asset).
        /// </summary>
        static ShipBlockDef[] CreateShipBlocks()
        {
            var list = new System.Collections.Generic.List<ShipBlockDef>();
            ShipBlockDef Block(string file, string name, BlockRole role, int cost, bool solid, int hp,
                int sail, int bunks, int cannons, string desc)
            {
                var b = GetOrCreate<ShipBlockDef>($"Navio/{file}", x =>
                {
                    x.displayName = name; x.role = role; x.cost = cost; x.solid = solid;
                    x.hp = hp; x.sailPower = sail; x.bunks = bunks; x.cannons = cannons; x.description = desc;
                });
                list.Add(b);
                return b;
            }

            // Os 6 originais (a ordem importa: GameFlow usa [0] casco, [2] mastro, [3] vela)
            Block("Casco", "Casco", BlockRole.Hull, 15, true, 10, 0, 0, 0, "Estrutura do navio: +10 de vida. Pode ir abaixo d'água.");
            Block("Conves", "Convés", BlockRole.Deck, 10, true, 6, 0, 0, 0, "Piso mais leve e barato: +6 de vida.");
            Block("Mastro", "Mastro", BlockRole.Mast, 20, false, 2, 0, 0, 0, "Segura as velas e a bandeira. Precisa de apoio embaixo.");
            Block("Vela", "Vela branca", BlockRole.Sail, 25, false, 1, 1, 0, 0, "Mais velas = mais velocidade (o peso do navio atrapalha). Velas vizinhas se emendam.");
            Block("Canhao", "Canhão", BlockRole.Cannon, 60, false, 2, 0, 0, 1, "Atira sozinho no combate. Precisa de chão embaixo.");
            Block("Cabine", "Rede (beliche)", BlockRole.Cabin, 30, false, 2, 0, 1, 0, "Parede com rede de dormir: +1 tripulante no navio.");

            // Casco em 4 formas e 3 cores
            Block("CascoRampa", "Casco em rampa", BlockRole.Hull, 12, true, 6, 0, 0, 0, "Ponta inclinada (proa, popa). R gira.");
            Block("CascoCurvo", "Casco curvo", BlockRole.Hull, 12, true, 6, 0, 0, 0, "Canto arredondado. R gira.");
            Block("CascoMeio", "Meio casco", BlockRole.Hull, 8, true, 5, 0, 0, 0, "Meio bloco (degrau, beirada). R vira para cima.");
            foreach (var cor in new[] { "Escuro", "Vermelho" })
            {
                Block($"Casco{cor}", $"Casco {cor.ToLower()}", BlockRole.Hull, 15, true, 10, 0, 0, 0, "Estrutura do navio: +10 de vida.");
                Block($"Casco{cor}Rampa", $"Rampa {cor.ToLower()}", BlockRole.Hull, 12, true, 6, 0, 0, 0, "Ponta inclinada. R gira.");
                Block($"Casco{cor}Curvo", $"Curvo {cor.ToLower()}", BlockRole.Hull, 12, true, 6, 0, 0, 0, "Canto arredondado. R gira.");
                Block($"Casco{cor}Meio", $"Meio {cor.ToLower()}", BlockRole.Hull, 8, true, 5, 0, 0, 0, "Meio bloco. R vira para cima.");
            }

            // Estrutura (andares)
            Block("Piso", "Piso vazado", BlockRole.Platform, 6, false, 2, 0, 0, 0, "Chão de um andar: dá pra pular de baixo e descer com S.");
            Block("Escada", "Escada", BlockRole.Ladder, 8, false, 1, 0, 0, 0, "Sobe e desce entre os andares (W/S).");
            Block("Parede", "Parede", BlockRole.Wall, 5, false, 1, 0, 0, 0, "Parede de fundo para cabines e porões.");
            Block("ParedeJanela", "Parede com janela", BlockRole.Wall, 8, false, 1, 0, 0, 0, "Parede de fundo com escotilha redonda.");

            // Velas
            Block("VelaListrada", "Vela listrada", BlockRole.Sail, 30, false, 1, 1, 0, 0, "Vela vermelha e branca. Velas vizinhas se emendam.");
            Block("VelaPreta", "Vela preta", BlockRole.Sail, 30, false, 1, 1, 0, 0, "Vela de pirata de respeito. Velas vizinhas se emendam.");
            Block("Bandeira", "Bandeira pirata", BlockRole.Flag, 15, false, 1, 0, 0, 0, "Vai no topo de um mastro.");

            // Enfeites
            Block("Amurada", "Amurada", BlockRole.Decor, 6, false, 1, 0, 0, 0, "Corrimão na beira do convés.");
            Block("Lanterna", "Lanterna", BlockRole.Decor, 10, false, 1, 0, 0, 0, "Vela acesa num pires.");
            Block("Barril", "Barril de rum", BlockRole.Decor, 8, false, 1, 0, 0, 0, "A tripulação bebe aqui de vez em quando: gasta 1 rum do estoque e anima o bando.");
            Block("Caixote", "Caixote", BlockRole.Decor, 8, false, 1, 0, 0, 0, "Carga do navio.");
            Block("Ancora", "Âncora", BlockRole.Decor, 12, false, 1, 0, 0, 0, "Toda embarcação que se preza tem uma.");
            Block("Timao", "Timão", BlockRole.Decor, 20, false, 1, 0, 0, 0, "O leme do capitão.");
            Block("Comida", "Caixa de comida", BlockRole.Decor, 15, false, 1, 0, 0, 0, "A tripulação come aqui quando bate a fome. Sem ela, o bando passa fome no mar (perde moral).");

            var all = list.ToArray();
            ConfigureShipBlocks(all);
            return all;
        }

        /// <summary>Forma, aba, família, cor da amostra, emendas, desenho e ordem de desenho (aplicado a cada montagem).</summary>
        static void ConfigureShipBlocks(ShipBlockDef[] blocks)
        {
            ImportMarArt();
            var hullColors = new System.Collections.Generic.Dictionary<string, (string art, Color swatch)>
            {
                { "", ("natural", new Color32(0xbc, 0x99, 0x7f, 255)) },
                { "Escuro", ("escuro", new Color32(0x76, 0x58, 0x4c, 255)) },
                { "Vermelho", ("vermelho", new Color32(0xb4, 0x4c, 0x42, 255)) },
            };
            foreach (var b in blocks)
            {
                if (b == null) continue;
                b.connect = ConnectMode.None;
                b.spriteTop = b.spriteBottom = b.spriteSingle = null;
                b.connectSprites = null;
                b.tabSprites = null;
                b.animFrames = null;
                b.shape = BlockShape.Full;
                string n = b.name;
                b.use = n == "Comida" ? BlockUse.Food : n == "Barril" ? BlockUse.Rum : n == "Cabine" ? BlockUse.Bed : BlockUse.None;
                if (n == "Barril")   // antes era só enfeite: o texto muda junto com a função
                {
                    b.displayName = "Barril de rum";
                    b.description = "A tripulação bebe aqui de vez em quando: gasta 1 rum do estoque e anima o bando.";
                }
                if (n == "Cabine") b.description = "Parede com rede de dormir: +1 tripulante no navio. À noite a tripulação dorme aqui.";

                if (n.StartsWith("Casco"))
                {
                    string rest = n.Substring(5);
                    string cor = rest.StartsWith("Escuro") ? "Escuro" : rest.StartsWith("Vermelho") ? "Vermelho" : "";
                    string forma = rest.Substring(cor.Length);
                    var (art, sw) = hullColors[cor];
                    b.category = BlockCategory.Casco; b.swatch = sw; b.sortingOrder = 0; b.solid = true; b.role = BlockRole.Hull;
                    b.shape = forma == "Rampa" ? BlockShape.Slope : forma == "Curvo" ? BlockShape.Round : forma == "Meio" ? BlockShape.Half : BlockShape.Full;
                    string f = forma == "Rampa" ? "rampa" : forma == "Curvo" ? "curvo" : forma == "Meio" ? "meio" : "cheio";
                    b.family = "casco_" + f;
                    b.sprite = Mar($"Navio/casco_{art}_{f}");
                    if (f == "cheio") { b.spriteTop = Mar($"Navio/casco_{art}_cheio_topo"); b.connect = ConnectMode.ExposedTop; }
                }
                else switch (n)
                {
                    case "Conves":
                        b.category = BlockCategory.Casco; b.family = "conves"; b.sortingOrder = 0; b.sprite = Mar("Navio/conves"); break;
                    case "Mastro":
                        b.category = BlockCategory.Velas; b.family = "mastro"; b.sortingOrder = -11; b.sprite = Mar("Navio/mastro"); break;
                    case "Vela": case "VelaListrada": case "VelaPreta":
                    {
                        string estilo = n == "Vela" ? "branca" : n == "VelaListrada" ? "listrada" : "preta";
                        b.category = BlockCategory.Velas; b.family = "vela"; b.sortingOrder = -10; b.connect = ConnectMode.Sail16;
                        b.connectSprites = new Sprite[48];
                        for (int f = 0; f < 3; f++)
                            for (int m = 0; m < 16; m++) b.connectSprites[m + 16 * f] = Mar($"Navio/vela_{estilo}_{m}" + (f > 0 ? $"_b{f}" : ""));
                        b.tabSprites = new Sprite[4];
                        for (int m = 0; m < 4; m++) b.tabSprites[m] = Mar($"Navio/vela_{estilo}_aba_{m}");
                        b.sprite = b.connectSprites[15];
                        b.swatch = estilo == "branca" ? new Color32(0xf0, 0xe8, 0xd6, 255) : estilo == "listrada" ? new Color32(0xc8, 0x46, 0x3e, 255) : new Color32(0x4a, 0x44, 0x52, 255);
                        break;
                    }
                    case "Bandeira":
                        b.category = BlockCategory.Velas; b.family = "bandeira"; b.sortingOrder = -9; b.sprite = Mar("Navio/bandeira");
                        b.animFrames = new Sprite[9];
                        for (int f = 0; f < 9; f++) b.animFrames[f] = Mar($"Navio/flamula_{f + 1}");
                        b.animFps = 10f;
                        break;
                    case "Canhao":
                        b.category = BlockCategory.Armas; b.family = "canhao"; b.sortingOrder = 5; b.sprite = Mar("Navio/canhao"); break;
                    case "Cabine":
                        b.category = BlockCategory.Estrutura; b.family = "rede"; b.sortingOrder = -14; b.sprite = Mar("Navio/parede_rede"); break;
                    case "Piso":
                        b.category = BlockCategory.Estrutura; b.family = "piso"; b.sortingOrder = 1; b.sprite = Mar("Navio/piso"); break;
                    case "Escada":
                        b.category = BlockCategory.Estrutura; b.family = "escada"; b.sortingOrder = -6; b.sprite = Mar("Navio/escada"); break;
                    case "Parede":
                        b.category = BlockCategory.Estrutura; b.family = "parede"; b.sortingOrder = -14; b.sprite = Mar("Navio/parede"); break;
                    case "ParedeJanela":
                        b.category = BlockCategory.Estrutura; b.family = "janela"; b.sortingOrder = -14; b.sprite = Mar("Navio/parede_janela"); break;
                    case "Amurada":
                        b.category = BlockCategory.Enfeites; b.family = "amurada"; b.sortingOrder = 12; b.sprite = Mar("Navio/amurada"); break;
                    default:
                    {
                        string art = n == "Lanterna" ? "lanterna" : n == "Barril" ? "barril" : n == "Caixote" ? "caixote"
                                   : n == "Ancora" ? "ancora" : n == "Timao" ? "timao" : n == "Comida" ? "comida" : null;
                        if (art == null) break;
                        b.category = BlockCategory.Enfeites; b.family = art; b.sortingOrder = 3; b.sprite = Mar($"Navio/{art}");
                        break;
                    }
                }
                EditorUtility.SetDirty(b);
            }
            AssetDatabase.SaveAssets();
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
            // Visão mais aberta que antes (≈ 34 blocos de largura em 16:9): personagens menores, cena mais "cheia",
            // como no Pixel Piracy. Sem Pixel Perfect no mar: ele travava o zoom e deixava tudo com cara de "pixelão".
            cam.orthographicSize = SeaCameraSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0xdd, 0xc6, 0xa1, 255);   // céu do Treasure Hunters
            cam.allowHDR = false;
            cam.allowMSAA = false;
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;   // brilho, vinheta e cores
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

            var cloudSprites = new[] { Mar("Ceu/nuvem_0_borrada"), Mar("Ceu/nuvem_1_borrada"), Mar("Ceu/nuvem_2_borrada") };
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
                c.sprite = Mar("Ceu/nuvem_grande_borrada");
                c.sortingOrder = -92;
                c.transform.position = new Vector3(-40f + i * 14f, waterline + 3.6f, 0);
                bigClouds[i] = c.transform;
            }

            // Céu que muda com a hora (degradê, sol, lua, estrelas) e o clima (chuva, tempestade)
            var sky = new GameObject("Ceu e Clima").AddComponent<SeaSky>();
            sky.cam = cam;
            sky.horizonY = waterline + 3.5f;   // topo da faixa do horizonte (bege opaco)
            sky.sun = Mar("Ceu/sol");
            sky.moon = Mar("Ceu/lua");
            sky.star = Mar("Ceu/estrela");
            sky.drop = Mar("Ceu/gota");
            var cloudRenderers = new System.Collections.Generic.List<SpriteRenderer>();
            foreach (var t in clouds) cloudRenderers.Add(t.GetComponent<SpriteRenderer>());
            foreach (var t in bigClouds) cloudRenderers.Add(t.GetComponent<SpriteRenderer>());
            sky.clouds = cloudRenderers.ToArray();

            // Reflexo na água (câmera de reflexo + shader Pixel/WaterReflection)
            var refl = new GameObject("Reflexo da Agua").AddComponent<WaterReflection>();
            refl.mainCam = cam;
            refl.waterline = waterline + 0.15f;
            refl.material = WaterReflectionMaterial();

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
            Sprite[] Frames(string prefix, int n) { var a = new Sprite[n]; for (int i = 0; i < n; i++) a[i] = Mar($"{prefix}_{i + 1}"); return a; }
            director.fxExplosion = Frames("Fx/explosao", 7);
            director.fxFlash = Frames("Fx/clarao", 6);
            director.fxSplashAnim = Frames("Fx/respingo", 5);
            director.fxSmoke = Frames("Fx/fumaca", 6);
            director.fxDebris = Frames("Fx/lasca", 6);
            director.fxCracks = Frames("Fx/dano", 3);
            director.cannonFireFrames = Frames("Navio/canhao_tiro", 6);
            director.flagPirateFrames = Frames("Navio/bandeira_pirata", 9);
            director.flagMerchantFrames = Frames("Navio/bandeira_mercante", 9);
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
            islands.backPalms = new[] { Mar("Ilha/coqueiro_fundo_0_borrado"), Mar("Ilha/coqueiro_fundo_1_borrado"), Mar("Ilha/coqueiro_fundo_2_borrado") };
            islands.farPalms = new[] { Mar("Ilha/coqueiro_longe_0"), Mar("Ilha/coqueiro_longe_1"), Mar("Ilha/coqueiro_longe_2") };
            islands.grassDecor = Mar("Ilha/grama");
            islands.market = Ninja("Props/barraca_lado");   // barraca vista de lado
            islands.plank = Mar("Navio/prancha");
            Sprite C(string n) => Mar("Casa/" + n);
            islands.house = new IslandBuilder.HouseArt
            {
                back = C("parede_fundo"), pillar = C("pilar"), pillarTop = C("pilar_topo"), pillarBase = C("pilar_base"),
                beam = C("viga"), beamLeft = C("viga_esq"), beamRight = C("viga_dir"),
                roof = C("telhado"), roofLeft = C("telhado_esq"), roofRight = C("telhado_dir"),
                table = C("mesa"), chair = C("cadeira"), window = C("janela"), barrel = C("barril"),
                barrelSide = C("barril_deitado"), barrelFront = C("barril_frente"),
                bottleBlue = C("garrafa_azul"), bottleGreen = C("garrafa_verde"), bottleRed = C("garrafa_vermelha"),
                skull = C("caveira"), shelf = C("prateleira"),
                fish = C("comida_peixe"), meat = C("comida_carne"), steak = C("comida_bife"), shrimp = C("comida_camarao"),
                sword = C("arma_espada"), axe = C("arma_machado"), lance = C("arma_lanca"),
                candle = new[] { C("vela_1"), C("vela_2"), C("vela_3"), C("vela_4"), C("vela_5"), C("vela_6") },
            };
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
                Ninja("Icons/mapa_papel"), Ninja("Icons/mapa_casa"), Ninja("Icons/mapa_porto"), Ninja("Icons/mapa_selvagem"),
                Ninja("Icons/mapa_acampamento"), Ninja("Icons/mapa_desconhecida"), Ninja("Icons/mapa_navio"),
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
