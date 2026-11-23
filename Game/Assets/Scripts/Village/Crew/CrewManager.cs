using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Pixel.Village
{
    /// <summary>
    /// Tripulação da vila: contratação na Taverna, moradia, trabalho nos prédios
    /// e o fechamento de cada dia (salário, comida, rum, moral, deserção).
    /// </summary>
    public class CrewManager : MonoBehaviour
    {
        public static CrewManager Instance { get; private set; }

        [Serializable] public class DirSprites { public Sprite[] skin, outfit, hair, baseLayer; }
        [Serializable] public class LayerSprites { public DirSprites down, up, left, right; }

        [Header("Tempo")]
        [Tooltip("Duração de um dia, em segundos.")]
        public float dayLength = 90f;

        [Header("Itens consumidos")]
        public ItemDef food;
        public ItemDef rum;

        [Header("Começo do jogo")]
        public int startingCrew = 2;
        public int startingFood = 10;
        public Vector2 dockPoint = new Vector2(31.5f, 1f);
        public Vector2 idleArea = new Vector2(31.5f, 14f);

        [Header("Taverna")]
        public int recruitsPerDay = 3;

        [Header("Visual")]
        public LayerSprites sprites;

        public int Day { get; private set; } = 1;
        public float DayProgress => dayTimer / dayLength;
        public IReadOnlyList<CrewMember> Crew => crew;
        public IReadOnlyList<CrewMember> Recruits => recruits;
        public event Action Changed;
        public event Action<string, Color> Logged;

        readonly List<CrewMember> crew = new List<CrewMember>();
        readonly List<CrewMember> recruits = new List<CrewMember>();
        float dayTimer;
        VillageManager vm;

        static readonly string[] FirstNames =
        {
            "Zé", "Bento", "Tonho", "Chico", "Joana", "Rosa", "Dandara", "Iracema", "Pedro", "Baltazar",
            "Severino", "Luzia", "Benedita", "Caetano", "Inácio", "Tereza", "Anita", "Quitéria", "Raimundo", "Jacira",
        };
        static readonly string[] Nicknames =
        {
            "Barba-Ruiva", "Perna-Torta", "Olho-de-Vidro", "Mão-Leve", "Dente-de-Ouro", "Sete-Mares",
            "Maré-Alta", "Cabeça-de-Coco", "Pé-de-Pato", "Bico-Fino", "Casca-Grossa", "Vento-Sul",
        };
        static readonly Color[] Skins =
        {
            new Color32(250, 214, 175, 255), new Color32(232, 178, 128, 255), new Color32(190, 130, 85, 255),
            new Color32(150, 98, 62, 255), new Color32(105, 68, 45, 255),
        };
        static readonly Color[] Outfits =
        {
            new Color32(70, 100, 190, 255), new Color32(185, 60, 60, 255), new Color32(70, 150, 80, 255),
            new Color32(205, 185, 125, 255), new Color32(130, 80, 160, 255), new Color32(60, 60, 70, 255),
            new Color32(220, 220, 210, 255), new Color32(200, 120, 50, 255),
        };
        static readonly Color[] Hairs =
        {
            new Color32(40, 28, 20, 255), new Color32(90, 55, 30, 255), new Color32(200, 160, 70, 255),
            new Color32(150, 50, 40, 255), new Color32(170, 170, 170, 255), new Color32(25, 25, 30, 255),
        };

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            vm = VillageManager.Instance;
            vm.BuildingsChanged += OnBuildingsChanged;
            if (food != null) vm.AddStock(food, startingFood);

            for (int i = 0; i < startingCrew; i++)
            {
                var p = i == 0 ? Profession.Agricultor : i == 1 ? Profession.Pescador : Profession.Marujo;
                var m = Generate(p, 1);
                m.traits.Remove(Trait.Preguicoso);
                m.traits.Remove(Trait.Rabugento);
                AddToCrew(m, idleArea + new Vector2(i * 1.2f - 0.6f, -1f));
            }
            RefreshRecruits();
            Log($"Dia 1. {crew.Count} marujos dormem ao relento: construa uma Casa!", new Color(1f, 0.85f, 0.4f));
        }

        void Update()
        {
            dayTimer += Time.deltaTime;
            if (dayTimer >= dayLength)
            {
                dayTimer -= dayLength;
                EndOfDay();
            }
        }

        // ---------- Contratação ----------
        public int FreeBeds => vm.HousingCapacity - CountHoused();

        int CountHoused()
        {
            int n = 0;
            foreach (var c in crew) if (c.home != null) n++;
            return n;
        }

        public bool CanHire(CrewMember r, out string reason)
        {
            if (FindTavern() == null) { reason = "Construa uma Taverna para contratar."; return false; }
            if (FreeBeds <= 0) { reason = "Sem moradia livre: construa uma Casa."; return false; }
            if (vm.Gold < r.HireCost) { reason = $"Ouro insuficiente ({r.HireCost})."; return false; }
            reason = null;
            return true;
        }

        public bool Hire(CrewMember r)
        {
            if (!recruits.Contains(r) || !CanHire(r, out _)) return false;
            vm.TrySpendGold(r.HireCost);
            recruits.Remove(r);
            var tavern = FindTavern();
            AddToCrew(r, tavern != null ? tavern.Door : idleArea);
            Log($"{r.name} entrou para o bando! ({CrewText.Name(r.profession)})", new Color(0.6f, 0.9f, 1f));
            return true;
        }

        public void Fire(CrewMember m)
        {
            if (!crew.Contains(m)) return;
            RemoveFromCrew(m);
            Log($"{m.name} foi dispensado(a) e foi embora.", new Color(0.85f, 0.85f, 0.85f));
        }

        Building FindTavern() => vm.FindNearest(BuildingKind.Tavern, idleArea);

        void AddToCrew(CrewMember m, Vector2 spawn)
        {
            crew.Add(m);
            var go = new GameObject("Tripulante_" + m.name);
            go.transform.position = spawn;
            go.transform.SetParent(transform, true);
            go.AddComponent<CrewAgent>().Init(m, sprites);
            AssignAll();
        }

        void RemoveFromCrew(CrewMember m)
        {
            crew.Remove(m);
            m.workplace?.Workers.Remove(m);
            m.workplace = null;
            m.home = null;
            if (m.agent != null) m.agent.Leave(dockPoint);
            AssignAll();
        }

        void RefreshRecruits()
        {
            recruits.Clear();
            for (int i = 0; i < recruitsPerDay; i++)
            {
                var p = (Profession)Random.Range(0, 4);
                int lvl = Random.value < 0.15f ? 3 : Random.value < 0.4f ? 2 : 1;
                recruits.Add(Generate(p, lvl));
            }
            Changed?.Invoke();
        }

        CrewMember Generate(Profession p, int level)
        {
            var m = new CrewMember
            {
                name = $"{FirstNames[Random.Range(0, FirstNames.Length)]} {Nicknames[Random.Range(0, Nicknames.Length)]}",
                profession = p,
                level = level,
                morale = Random.Range(60, 81),
                skin = Skins[Random.Range(0, Skins.Length)],
                outfit = Outfits[Random.Range(0, Outfits.Length)],
                hair = Hairs[Random.Range(0, Hairs.Length)],
            };
            int traitCount = Random.value < 0.35f ? 2 : 1;
            var all = (Trait[])Enum.GetValues(typeof(Trait));
            while (m.traits.Count < traitCount)
            {
                var t = all[Random.Range(0, all.Length)];
                bool conflict = (t == Trait.Trabalhador && m.Has(Trait.Preguicoso)) || (t == Trait.Preguicoso && m.Has(Trait.Trabalhador))
                             || (t == Trait.Alegre && m.Has(Trait.Rabugento)) || (t == Trait.Rabugento && m.Has(Trait.Alegre));
                if (!m.Has(t) && !conflict) m.traits.Add(t);
            }
            return m;
        }

        // ---------- Moradia e trabalho ----------
        void OnBuildingsChanged() => AssignAll();

        /// <summary>Distribui casas e postos de trabalho (quem tem o ofício certo tem prioridade).</summary>
        public void AssignAll()
        {
            if (vm == null) return;
            // Moradia: mantém quem já tem casa válida; distribui os sem-teto.
            var beds = new Dictionary<Building, int>();
            foreach (var b in vm.Buildings) if (b.def.kind == BuildingKind.House) beds[b] = b.def.housing;
            foreach (var c in crew)
                if (c.home != null && beds.TryGetValue(c.home, out int left) && left > 0) beds[c.home] = left - 1;
                else c.home = null;
            foreach (var c in crew)
            {
                if (c.home != null) continue;
                foreach (var kv in beds)
                    if (kv.Value > 0) { c.home = kv.Key; beds[kv.Key] = kv.Value - 1; break; }
            }

            // Trabalho: refaz a distribuição. Primeiro quem tem o ofício certo, depois o resto.
            foreach (var b in vm.Buildings) b.Workers.Clear();
            var free = new List<CrewMember>();
            foreach (var c in crew) { c.workplace = null; if (!c.atSea) free.Add(c); }
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var b in vm.Buildings)
                {
                    if (b.def.kind != BuildingKind.Producer) continue;
                    while (b.Workers.Count < b.def.workerSlots)
                    {
                        CrewMember pick = null;
                        foreach (var c in free)
                        {
                            bool match = c.profession == b.def.profession;
                            if (pass == 0 && !match) continue;
                            if (pick == null || c.EfficiencyAt(b.def) > pick.EfficiencyAt(b.def)) pick = c;
                        }
                        if (pick == null) break;
                        free.Remove(pick);
                        pick.workplace = b;
                        b.Workers.Add(pick);
                    }
                }
            }
            Changed?.Invoke();
        }

        /// <summary>Chamado pelo prédio a cada ciclo de produção concluído.</summary>
        public void GiveWorkXp(CrewMember m, Building b)
        {
            if (m.profession != b.def.profession || m.level >= 5) return;
            m.xp += m.Has(Trait.Talentoso) ? 2 : 1;
            if (m.xp >= m.XpToNextLevel)
            {
                m.xp = 0;
                m.level++;
                FloatingText.Spawn(m.agent != null ? m.agent.transform.position + Vector3.up * 1.8f : b.transform.position,
                    $"{m.name}: nível {m.level}!", new Color(0.6f, 0.9f, 1f));
                Log($"{m.name} subiu para {CrewText.Name(m.profession)} nível {m.level} (salário agora {m.Wage}).", new Color(0.6f, 0.9f, 1f));
                Changed?.Invoke();
            }
        }

        // ---------- Fechamento do dia ----------
        void EndOfDay()
        {
            int wages = 0, eaten = 0, drunk = 0, hungry = 0, unpaid = 0;
            foreach (var c in new List<CrewMember>(crew))
            {
                int delta = 0;

                // Salário
                c.paidToday = vm.TrySpendGold(c.Wage);
                if (c.paidToday) wages += c.Wage; else { delta -= 25; unpaid++; }

                // Comida
                int foodNeed = c.Has(Trait.Guloso) ? 2 : 1;
                c.fedToday = food != null && vm.TryTakeStock(food, foodNeed);
                if (c.fedToday) { eaten += foodNeed; delta += 5; } else { delta -= 20; hungry++; }

                // Rum
                int rumNeed = c.Has(Trait.Beberrao) ? 2 : 1;
                if (rum != null && vm.TryTakeStock(rum, rumNeed)) { drunk += rumNeed; delta += 10; }
                else if (c.Has(Trait.Beberrao)) delta -= 15;

                // Moradia e temperamento
                delta += c.home != null ? 5 : -15;
                if (c.Has(Trait.Alegre)) delta += 10;
                if (c.Has(Trait.Rabugento)) delta -= 5;

                c.morale = Mathf.Clamp(c.morale + delta, 0, 100);
            }

            Day++;
            Log($"Dia {Day}. Salários: -{wages} ouro · Comida: -{eaten} · Rum: -{drunk}", new Color(1f, 0.85f, 0.4f));
            if (hungry > 0) Log($"{hungry} tripulante(s) passaram fome! Construa uma Cabana de Pesca.", new Color(1f, 0.5f, 0.45f));
            if (unpaid > 0) Log($"{unpaid} tripulante(s) ficaram sem salário!", new Color(1f, 0.5f, 0.45f));

            // Deserção
            foreach (var c in new List<CrewMember>(crew))
            {
                if (c.morale < 15 && !c.Has(Trait.Leal))
                {
                    RemoveFromCrew(c);
                    Log($"{c.name} abandonou o bando! (moral muito baixa)", new Color(1f, 0.4f, 0.35f));
                }
                else if (c.morale < 35)
                    Log($"{c.name} está revoltado(a). Dê comida, rum e casa!", new Color(1f, 0.65f, 0.4f));
            }

            RefreshRecruits();
            Changed?.Invoke();
        }

        // ---------- Viagens ----------
        /// <summary>
        /// Embarca até "max" tripulantes SEM posto de trabalho (marujos ociosos).
        /// Quem está trabalhando fica na vila, para a produção não parar.
        /// </summary>
        public List<CrewMember> Embark(int max)
        {
            var list = new List<CrewMember>();
            foreach (var c in crew)
            {
                if (list.Count >= max) break;
                if (c.workplace != null || c.atSea) continue;
                c.atSea = true;
                c.injured = false;
                if (c.agent != null) c.agent.gameObject.SetActive(false);
                list.Add(c);
            }
            if (list.Count > 0) AssignAll();
            return list;
        }

        public void Disembark(List<CrewMember> list)
        {
            int hurt = 0;
            foreach (var c in list)
            {
                if (!crew.Contains(c)) continue;
                c.atSea = false;
                if (c.injured) { c.morale = Mathf.Max(0, c.morale - 15); c.injured = false; hurt++; }
                if (c.agent != null)
                {
                    c.agent.transform.position = dockPoint + new Vector2(0, 3f) + Random.insideUnitCircle;
                    c.agent.gameObject.SetActive(true);
                }
            }
            if (hurt > 0) Log($"{hurt} tripulante(s) voltaram feridos (-15 de moral).", new Color(1f, 0.65f, 0.4f));
            AssignAll();
        }

        /// <summary>Escreve uma notícia no registro da vila.</summary>
        public void Report(string msg, Color color) => Log(msg, color);

        /// <summary>Últimas mensagens (para o registro mostrar o que aconteceu antes de abrir).</summary>
        public readonly List<(string msg, Color color)> History = new List<(string, Color)>();

        void Log(string msg, Color color)
        {
            History.Add((msg, color));
            if (History.Count > 30) History.RemoveAt(0);
            Logged?.Invoke(msg, color);
        }
    }
}
