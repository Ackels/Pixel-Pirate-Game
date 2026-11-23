using System.Collections.Generic;
using UnityEngine;

namespace Pixel.Village
{
    public enum ExpeditionState { Going, Fighting, Advancing, Returning, Done }

    /// <summary>Um lutador dentro da simulação de expedição (tripulante ou inimigo).</summary>
    public class ExpeditionFighter
    {
        public string name;
        public CrewMember member;      // null para inimigos
        public EnemyType enemyType;
        public int hp, maxHp, dmgMin, dmgMax;
        public float interval, timer;
        public bool Alive => hp > 0;
        /// <summary>Último instante em que atacou / levou dano (para a animação da faixa).</summary>
        public float lastAttack = -10f, lastHit = -10f;
        public int lastDamageTaken;
    }

    /// <summary>
    /// Uma expedição em andamento: viagem → andares de combate automático → volta.
    /// Toda a luta é calculada aqui (sem física), para rodar mesmo com a vila fora da tela.
    /// </summary>
    public class Expedition
    {
        public const int RumHeal = 25;
        public DungeonDef dungeon;
        public readonly List<ExpeditionFighter> party = new List<ExpeditionFighter>();
        public readonly List<ExpeditionFighter> enemies = new List<ExpeditionFighter>();
        public int rum;
        public int stage;
        public ExpeditionState state = ExpeditionState.Going;
        public float stateTimer;
        public bool victory;
        public int stagesCleared;
        public readonly List<string> log = new List<string>();
        public float Clock { get; private set; }

        public float Progress => state switch
        {
            ExpeditionState.Going => Mathf.Clamp01(stateTimer / dungeon.travelSeconds),
            ExpeditionState.Returning => Mathf.Clamp01(stateTimer / dungeon.travelSeconds),
            _ => 0f,
        };

        public string StatusText => state switch
        {
            ExpeditionState.Going => $"A caminho de {dungeon.displayName}... {Progress * 100f:0}%",
            ExpeditionState.Fighting => $"{dungeon.displayName} — Andar {stage + 1}/{dungeon.stages.Count}",
            ExpeditionState.Advancing => $"{dungeon.displayName} — descendo para o andar {stage + 2}...",
            ExpeditionState.Returning => (victory ? "Vitória! Voltando com o saque... " : "Recuando, feridos... ") + $"{Progress * 100f:0}%",
            _ => "Concluída",
        };

        void Log(string s)
        {
            log.Add(s);
            if (log.Count > 6) log.RemoveAt(0);
        }

        public void SpawnStage()
        {
            enemies.Clear();
            foreach (var t in dungeon.stages[stage].enemies)
            {
                var (hp, mn, mx, iv) = EnemyStats.Get(t);
                float pw = dungeon.enemyPower;
                hp = Mathf.RoundToInt(hp * pw); mn = Mathf.RoundToInt(mn * pw); mx = Mathf.RoundToInt(mx * pw);
                enemies.Add(new ExpeditionFighter
                {
                    name = EnemyStats.Name(t), enemyType = t, hp = hp, maxHp = hp, dmgMin = mn, dmgMax = mx,
                    interval = iv, timer = Random.Range(0.3f, iv),
                });
            }
            Log($"Andar {stage + 1}: {enemies.Count} inimigo(s)!");
        }

        /// <summary>Avança a simulação. Retorna true quando a expedição terminou (voltou pra vila).</summary>
        public bool Tick(float dt)
        {
            Clock += dt;
            stateTimer += dt;
            switch (state)
            {
                case ExpeditionState.Going:
                    if (stateTimer >= dungeon.travelSeconds) { state = ExpeditionState.Fighting; stateTimer = 0; SpawnStage(); }
                    break;

                case ExpeditionState.Fighting:
                    Fight(dt);
                    if (!party.Exists(p => p.Alive)) { Finish(false); break; }
                    if (!enemies.Exists(e => e.Alive))
                    {
                        stagesCleared++;
                        Log($"Andar {stage + 1} limpo!");
                        if (stage + 1 >= dungeon.stages.Count) Finish(true);
                        else { state = ExpeditionState.Advancing; stateTimer = 0; }
                    }
                    break;

                case ExpeditionState.Advancing:
                    if (stateTimer >= 2.5f) { stage++; state = ExpeditionState.Fighting; stateTimer = 0; SpawnStage(); }
                    break;

                case ExpeditionState.Returning:
                    if (stateTimer >= dungeon.travelSeconds) { state = ExpeditionState.Done; return true; }
                    break;
            }
            return false;
        }

        void Finish(bool won)
        {
            victory = won;
            state = ExpeditionState.Returning;
            stateTimer = 0;
            Log(won ? "Masmorra concluída!" : "O grupo caiu... recuando.");
        }

        void Fight(float dt)
        {
            foreach (var p in party) Act(p, enemies, dt);
            foreach (var e in enemies) Act(e, party, dt);

            // Rum como curativo: quem está fraco toma um gole
            if (rum > 0)
                foreach (var p in party)
                    if (p.Alive && p.hp < p.maxHp * 0.35f && rum > 0)
                    {
                        rum--;
                        int heal = Mathf.Min(RumHeal, p.maxHp - p.hp);
                        p.hp += heal;
                        Log($"{FirstName(p.name)} tomou um gole de rum (+{heal}).");
                    }
        }

        void Act(ExpeditionFighter f, List<ExpeditionFighter> foes, float dt)
        {
            if (!f.Alive) return;
            f.timer -= dt;
            if (f.timer > 0) return;
            f.timer = f.interval;
            var alive = foes.FindAll(x => x.Alive);
            if (alive.Count == 0) return;
            var target = alive[Random.Range(0, alive.Count)];
            int dmg = Random.Range(f.dmgMin, f.dmgMax + 1);
            target.hp = Mathf.Max(0, target.hp - dmg);
            target.lastHit = Clock;
            target.lastDamageTaken = dmg;
            f.lastAttack = Clock;
            if (!target.Alive) Log(target.member != null ? $"{FirstName(target.name)} caiu!" : $"{target.name} derrotado.");
        }

        public static string FirstName(string n) => n.Split(' ')[0];
    }
}
