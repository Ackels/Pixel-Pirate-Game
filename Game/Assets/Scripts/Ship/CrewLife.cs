using System.Collections.Generic;
using Pixel.Combat;
using Pixel.Village;
using UnityEngine;

namespace Pixel.Ship
{
    /// <summary>
    /// Vida a bordo de um tripulante (fora de combate): passeia pelo navio (usa escadas), conversa em balõezinhos,
    /// sente fome e come na caixa de comida, bebe no barril de rum, dorme na rede à noite, o Marujo limpa a sujeira
    /// e o Pescador pesca na beirada. Quando aparece briga, tudo para e a CombatAI assume.
    /// </summary>
    [RequireComponent(typeof(CombatAI))]
    public class CrewLife : MonoBehaviour
    {
        public CrewMember member;

        enum Act { Idle, Wander, Eat, Drink, Sleep, Clean, Fish }

        CombatAI ai;
        Combatant me;
        Rigidbody2D body;
        Collider2D col;
        IPose pose;
        float gravity;

        Act act = Act.Idle;
        bool traveling, doing, climbing;
        float decide = 1f, actTimer, travelTime, chatTimer, complainTimer, drunk, zzzTimer;
        Vector2 spot;               // x do destino e y dos pés no destino
        int spotRow;                // linha dos pés no destino
        float ladderX;
        bool hasLadder;
        int climbDir;
        Vector2Int bedCell;
        GameObject dirtTarget;
        readonly List<Collider2D> ignored = new List<Collider2D>();

        /// <summary>Fome de 0 a 100 (sobe com o tempo no mar).</summary>
        public float Hunger { get; private set; }

        void Awake()
        {
            ai = GetComponent<CombatAI>();
            me = GetComponent<Combatant>();
            body = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            pose = GetComponent<IPose>();
            gravity = body.gravityScale;
            Hunger = Random.Range(0f, 40f);
            chatTimer = Random.Range(4f, 14f);
        }

        void OnDisable() => Stop();

        SeaController Sea => SeaController.Instance;
        ShipLife Life => ShipLife.Instance;
        ShipView View => Sea != null ? Sea.shipView : null;
        int FeetRow => Mathf.RoundToInt(transform.position.y - View.origin.y);

        public void Say(string text, float seconds = 2.8f) => SpeechBubble.Say(transform, text, seconds, 1.45f);

        void Update()
        {
            if (Sea == null || Life == null || View == null || member == null) return;
            bool active = Life.Calm && !me.IsDown && !ai.HasTarget && !ai.HasOrder && Sea.Mode != SeaMode.Landed;
            if (!active) { if (act != Act.Idle || climbing) Stop(); return; }

            float dt = Time.deltaTime;
            Hunger = Mathf.Min(100f, Hunger + dt * (Sea.Mode == SeaMode.Sailing ? 100f / 140f : 100f / 400f));
            complainTimer -= dt;
            if (drunk > 0f) drunk -= dt;

            Chat(dt);

            if (climbing) { Climb(); return; }
            if (traveling) { Travel(dt); return; }
            if (doing) { Do(dt); return; }

            decide -= dt;
            if (decide <= 0f) Decide();
        }

        // ---------- Escolher o que fazer ----------
        void Decide()
        {
            decide = Random.Range(1.5f, 3.5f);
            var cm = CrewManager.Instance;
            var vm = VillageManager.Instance;
            bool night = cm != null && cm.DayProgress >= 0.8f;

            if (night && GoUse(BlockUse.Bed, Act.Sleep)) return;
            if (Hunger > 60f && GoUse(BlockUse.Food, Act.Eat)) return;
            if (Hunger >= 99f && complainTimer <= 0f && Life.Uses(BlockUse.Food).Count == 0)
            {
                complainTimer = 25f;
                member.morale = Mathf.Max(0, member.morale - 1);
                Say(Pick("Tô com fome... cadê a comida?", "Ninguém pensou numa caixa de comida?!"));
            }
            if (Life.HasFreeDirt && (member.profession == Profession.Marujo || !SailorAboard()) && GoClean()) return;
            if (member.profession == Profession.Pescador && Sea.Mode == SeaMode.Sailing && Random.value < 0.35f && GoFish()) return;
            int rum = cm != null && cm.rum != null && vm != null ? vm.GetStock(cm.rum) : 0;
            float rumChance = member.Has(Trait.Beberrao) ? 0.3f : member.morale < 50 ? 0.15f : 0.05f;
            if (rum > 0 && Random.value < rumChance && GoUse(BlockUse.Rum, Act.Drink)) return;
            Wander();
        }

        bool SailorAboard()
        {
            foreach (var c in Sea.CrewFighters)
                if (c != null && c.link is CrewMember cmb && cmb.profession == Profession.Marujo && !c.IsDown) return true;
            return false;
        }

        bool GoUse(BlockUse use, Act what)
        {
            int row = FeetRow;
            Vector2Int? best = null;
            float bestD = float.MaxValue;
            foreach (var c in Life.Uses(use))
            {
                if (c.y != row && Life.LadderBetween(row, c.y, transform.position.x) == null) continue;   // não dá pra chegar
                if (use == BlockUse.Bed && !Life.IsBedFree(c, this)) continue;    // rede de outro
                float d = Mathf.Abs(View.CellCenter(c).x - transform.position.x) + Mathf.Abs(c.y - row) * 3f;
                if (d < bestD) { bestD = d; best = c; }
            }
            if (best == null) return false;
            if (use == BlockUse.Bed) Life.ReserveBed(best.Value, this);
            bedCell = best.Value;
            GoTo(View.CellCenter(best.Value).x, best.Value.y, what);
            return true;
        }

        bool GoClean()
        {
            var d = Life.ClaimDirt(this, transform.position.x);
            if (d == null) return false;
            int row = Mathf.RoundToInt(d.transform.position.y - 0.06f - View.origin.y);
            if (row != FeetRow && Life.LadderBetween(FeetRow, row, transform.position.x) == null) { Life.ReleaseDirt(this); return false; }
            dirtTarget = d;
            GoTo(d.transform.position.x, row, Act.Clean);
            return true;
        }

        bool GoFish()
        {
            var deck = Life.FloorCells(true);
            if (deck.Count == 0) return false;
            int row = FeetRow;
            // A beirada do convés (a ponta mais perto, no andar onde dá pra chegar)
            Vector2Int? edge = null;
            foreach (var c in deck)
            {
                if (c.y + 1 != row && Life.LadderBetween(row, c.y + 1, transform.position.x) == null) continue;
                if (edge == null || c.x < edge.Value.x || c.x > edge.Value.x && Random.value < 0.5f) edge = c;
            }
            if (edge == null) return false;
            GoTo(View.CellCenter(edge.Value).x, edge.Value.y + 1, Act.Fish);
            return true;
        }

        void Wander()
        {
            int row = FeetRow;
            var options = new List<Vector2Int>();
            foreach (var c in Life.FloorCells(false))
            {
                if (c.y + 1 == row) options.Add(c);
                else if (Random.value < 0.15f && Life.LadderBetween(row, c.y + 1, transform.position.x) != null) options.Add(c);
            }
            if (options.Count == 0) { decide = 3f; return; }
            var t = options[Random.Range(0, options.Count)];
            GoTo(View.CellCenter(t).x + Random.Range(-0.3f, 0.3f), t.y + 1, Act.Wander);
        }

        // ---------- Andar até lá (com escada, se for outro andar) ----------
        void GoTo(float x, int row, Act what)
        {
            act = what;
            spot = new Vector2(x, View.origin.y + row);
            spotRow = row;
            traveling = true;
            doing = false;
            travelTime = 0f;
            hasLadder = false;
        }

        void Travel(float dt)
        {
            travelTime += dt;
            if (travelTime > 16f) { Stop(); return; }   // travou em algum lugar: desiste
            int row = FeetRow;
            if (row != spotRow)
            {
                if (!hasLadder)
                {
                    var lx = Life.LadderBetween(row, spotRow, transform.position.x);
                    if (lx == null) { Stop(); return; }
                    ladderX = lx.Value;
                    hasLadder = true;
                }
                ai.homeX = ladderX;
                if (Mathf.Abs(transform.position.x - ladderX) < 0.35f && Mathf.Abs(body.linearVelocity.y) < 0.1f) StartClimb();   // a IA para a 0,25 do destino
                return;
            }
            hasLadder = false;
            ai.homeX = spot.x;
            if (Mathf.Abs(transform.position.x - spot.x) < 0.3f) Arrive();
        }

        void StartClimb()
        {
            climbing = true;
            climbDir = spotRow > FeetRow ? 1 : -1;
            ai.externalControl = true;
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            var p = body.position; p.x = ladderX; body.position = p;
            // Atravessa os pisos vazados da escada enquanto sobe/desce
            foreach (var plat in View.GetComponentsInChildren<ShipPlatform>())
            {
                var pc = plat.GetComponent<Collider2D>();
                if (pc == null) continue;
                Physics2D.IgnoreCollision(col, pc, true);
                ignored.Add(pc);
            }
        }

        void Climb()
        {
            body.linearVelocity = new Vector2(0f, climbDir * 3f);
            pose?.Pose(true, true, me.FacingRight);
            float targetY = View.origin.y + spotRow;
            bool done = climbDir > 0 ? transform.position.y >= targetY + 0.05f : transform.position.y <= targetY + 0.02f;
            if (!done) return;
            EndClimb(targetY + 0.03f);
        }

        void EndClimb(float y)
        {
            climbing = false;
            body.gravityScale = gravity;
            body.linearVelocity = Vector2.zero;
            var p = body.position; p.y = y; body.position = p;
            foreach (var c in ignored) if (c != null) Physics2D.IgnoreCollision(col, c, false);
            ignored.Clear();
            ai.externalControl = false;
            ai.homeX = transform.position.x;
        }

        // ---------- Fazer a coisa ----------
        void Arrive()
        {
            traveling = false;
            doing = true;
            ai.homeX = transform.position.x;
            var cm = CrewManager.Instance;
            var vm = VillageManager.Instance;
            switch (act)
            {
                case Act.Wander:
                    actTimer = Random.Range(1.5f, 4.5f);
                    break;
                case Act.Eat:
                    actTimer = 3f;
                    Say(Pick("Nham!", "Peixe de novo...", "Delícia!", "Agora sim!"));
                    break;
                case Act.Drink:
                    if (cm == null || cm.rum == null || vm == null || !vm.TryTakeStock(cm.rum, 1)) { Stop(); return; }
                    actTimer = 3f;
                    Say(Pick("Saúde!", "Iô-hô-hô e uma garrafa de rum!", "Só um golinho..."));
                    break;
                case Act.Sleep:
                    actTimer = 999f;
                    zzzTimer = 0f;
                    ai.externalControl = true;
                    body.linearVelocity = Vector2.zero;
                    body.bodyType = RigidbodyType2D.Kinematic;
                    var c = View.CellCenter(bedCell);
                    // deitado em cima da rede: gira pelo pé com a cabeça para a direita, então o pé fica à esquerda do centro
                    body.position = new Vector2(c.x - 0.6f, c.y + 0.15f); transform.position = body.position;
                    body.rotation = -90f;   // deitado (o corpo físico manda na rotação)
                    break;
                case Act.Clean:
                    if (dirtTarget == null) { Stop(); return; }
                    actTimer = 3f;
                    Say(Pick("Gaivota maldita!", "Esfregando o convés...", "Que sujeira!"));
                    break;
                case Act.Fish:
                    actTimer = 6f;
                    me.FacingRight = transform.position.x > View.CenterX;
                    Say(Pick("Vamos ver se morde...", "Peixinho, peixinho..."));
                    break;
            }
        }

        void Do(float dt)
        {
            pose?.Pose(false, true, me.FacingRight);
            if (act == Act.Sleep)
            {
                var cm = CrewManager.Instance;
                zzzTimer -= dt;
                if (zzzTimer <= 0f) { zzzTimer = 3f; SpeechBubble.Say(transform, "Zzz...", 2f, 0.9f); }
                if (cm == null || cm.DayProgress < 0.8f) Finish();
                return;
            }
            if (act == Act.Clean && dirtTarget == null) { Finish(); return; }
            actTimer -= dt;
            if (actTimer > 0f) return;

            var vm = VillageManager.Instance;
            var crew = CrewManager.Instance;
            switch (act)
            {
                case Act.Eat:
                    Hunger = 0f;
                    member.morale = Mathf.Min(100, member.morale + 1);
                    break;
                case Act.Drink:
                    member.morale = Mathf.Min(100, member.morale + 4);
                    drunk = 25f;
                    break;
                case Act.Clean:
                    Life.Clean(dirtTarget);
                    member.morale = Mathf.Min(100, member.morale + 1);
                    Say(Pick("Brilhando!", "Pronto, limpinho."));
                    break;
                case Act.Fish:
                    if (Random.value < 0.5f && vm != null && crew != null && crew.food != null)
                    {
                        vm.AddStock(crew.food, 1);
                        Say(Pick("Fisguei um!", "Peixe pro jantar!"));
                    }
                    else Say(Pick("Escapou...", "Nada hoje."));
                    break;
            }
            Finish();
        }

        void Finish()
        {
            Stop();
            decide = Random.Range(1f, 3f);
        }

        /// <summary>Para tudo e devolve o boneco para a IA (começou uma briga, saiu do navio, etc.).</summary>
        void Stop()
        {
            if (climbing) EndClimb(transform.position.y);
            if (act == Act.Sleep && doing)
            {
                body.bodyType = RigidbodyType2D.Dynamic;
                body.rotation = 0f;
                if (View != null)
                {
                    var c = View.CellCenter(bedCell);
                    body.position = new Vector2(c.x, c.y - 0.45f); transform.position = body.position;
                }
            }
            if (Life != null) { Life.ReleaseBeds(this); Life.ReleaseDirt(this); }
            if (ai != null) ai.externalControl = false;
            if (body != null && body.bodyType == RigidbodyType2D.Kinematic) body.bodyType = RigidbodyType2D.Dynamic;
            if (body != null) body.rotation = 0f;
            act = Act.Idle;
            traveling = doing = false;
            dirtTarget = null;
        }

        // ---------- Conversa ----------
        void Chat(float dt)
        {
            if (act == Act.Sleep && doing) return;
            chatTimer -= dt;
            if (chatTimer > 0f) return;
            chatTimer = Random.Range(14f, 32f);
            if (SpeechBubble.IsTalking(transform) || Random.value > 0.6f) return;
            Say(Line());
        }

        string Line()
        {
            var cm = CrewManager.Instance;
            if (drunk > 0f && Random.value < 0.7f) return Pick("Hic!", "O mar tá balançando... ou sou eu?", "Hic! Iô-hô-hô!");
            if (Hunger > 70f && Random.value < 0.5f) return Pick("Tô com fome...", "Minha barriga tá roncando!");
            if (member.morale < 40 && Random.value < 0.5f) return Pick("Ninguém merece...", "Quero meu salário!", "Esse navio tá horrível.");
            if (member.traits.Count > 0 && Random.value < 0.35f)
            {
                var t = member.traits[Random.Range(0, member.traits.Count)];
                string l = t switch
                {
                    Trait.Beberrao => Pick("Cadê o rum?", "Um golinho não faz mal..."),
                    Trait.Guloso => Pick("Que horas sai a comida?", "Comia um peixe agora..."),
                    Trait.Rabugento => Pick("Hmpf.", "Que barco lento...", "Barulho demais aqui."),
                    Trait.Alegre => Pick("Lá-lá-lá!", "Que dia lindo!"),
                    Trait.Preguicoso => Pick("Bem que dava pra cochilar...", "*bocejo*"),
                    Trait.Leal => Pick("Pelo capitão!", "Sigo o capitão até o fim do mundo!"),
                    Trait.Trabalhador => Pick("Bora trabalhar!", "Mãos à obra, marujos!"),
                    Trait.Talentoso => Pick("Faço isso de olhos fechados.", "Ninguém dá um nó como eu."),
                    _ => null,
                };
                if (l != null) return l;
            }
            if (Sea.Mode == SeaMode.Docked && Random.value < 0.5f) return Pick("Bom estar em casa.", "Bora zarpar, capitão?", "A Enseada tá crescendo!");
            if (cm != null && cm.DayProgress >= 0.8f && Random.value < 0.4f) return Pick("Que noite estrelada...", "*bocejo*");
            return Pick("Iô-hô-hô!", "Que vento bom!", "Terra à vista? Ainda não...", "Cheiro de maresia!",
                        "Alguém viu meu papagaio?", "Saudade da taverna...", "O mar tá bonito hoje.", "Ouvi falar de um tesouro...");
        }

        static string Pick(params string[] options) => options[Random.Range(0, options.Length)];

        void LateUpdate()
        {
            // Bêbado: o boneco balança (só enquanto está de pé)
            if (drunk > 0f && !(act == Act.Sleep && doing) && !climbing)
                body.rotation = Mathf.Sin(Time.time * 3f) * 7f;
            else if (!(act == Act.Sleep && doing) && body.rotation != 0f)
                body.rotation = 0f;
        }
    }
}
