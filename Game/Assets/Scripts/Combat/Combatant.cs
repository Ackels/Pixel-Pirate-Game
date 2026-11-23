using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pixel.Combat
{
    public enum Team { Player, Enemy }

    /// <summary>
    /// Qualquer um que luta corpo a corpo: capitão, tripulantes e inimigos.
    /// Tem vida, dano, tempo entre golpes, barra de vida e "piscar" ao levar dano.
    /// Do lado do jogador, quem cai fica nocauteado (não morre no modo Normal).
    /// </summary>
    public class Combatant : MonoBehaviour
    {
        public static readonly List<Combatant> All = new List<Combatant>();

        public Team team;
        public string displayName = "Pirata";
        public int maxHp = 50;
        public int hp = 50;
        public Vector2Int damage = new Vector2Int(8, 12);
        public float attackCooldown = 0.8f;
        public float attackRange = 1.1f;
        [Tooltip("Jogador: nocauteia em vez de morrer. Inimigo: morre e some.")]
        public bool knockOutInsteadOfDeath;

        public bool IsDown => hp <= 0;
        public bool FacingRight { get; set; } = true;
        public event Action<Combatant> Downed;
        /// <summary>Ligado ao tripulante da vila (para ferimentos e moral).</summary>
        [NonSerialized] public object link;

        float cooldown, flash;
        Transform bar, barFill;
        SpriteRenderer[] renderers;
        Color[] baseColors;
        Rigidbody2D body;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            body = GetComponent<Rigidbody2D>();
            // Lutadores não se empurram (atravessam uns aos outros)
            var myCol = GetComponent<Collider2D>();
            if (myCol != null)
                foreach (var other in All)
                {
                    var oc = other != null && other != this ? other.GetComponent<Collider2D>() : null;
                    if (oc != null) Physics2D.IgnoreCollision(myCol, oc);
                }
            CacheRenderers();
            BuildBar();
        }

        public void CacheRenderers()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>();
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;
        }

        void BuildBar()
        {
            bar = new GameObject("BarraDeVida").transform;
            bar.SetParent(transform, false);
            bar.localPosition = new Vector3(0, 1.75f, 0);
            var bg = bar.gameObject.AddComponent<SpriteRenderer>();
            bg.sprite = Fx.White;
            bg.color = new Color(0, 0, 0, 0.7f);
            bg.sortingOrder = 50;
            bar.localScale = new Vector3(1f, 0.14f, 1f);
            barFill = new GameObject("Preenchimento").transform;
            barFill.SetParent(bar, false);
            var fill = barFill.gameObject.AddComponent<SpriteRenderer>();
            fill.sprite = Fx.White;
            fill.color = team == Team.Player ? new Color(0.4f, 0.95f, 0.4f) : new Color(1f, 0.35f, 0.3f);
            fill.sortingOrder = 51;
            UpdateBar();
        }

        void UpdateBar()
        {
            if (bar == null) return;
            float f = Mathf.Clamp01((float)hp / maxHp);
            bar.gameObject.SetActive(hp < maxHp && hp > 0);
            barFill.localScale = new Vector3(f, 1f, 1f);
            barFill.localPosition = new Vector3(-(1f - f) / 2f, 0, 0);
        }

        void Update()
        {
            if (cooldown > 0) cooldown -= Time.deltaTime;
            if (flash > 0)
            {
                flash -= Time.deltaTime;
                if (flash <= 0) SetTint(null);
            }
        }

        void SetTint(Color? c)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null && renderers[i].transform.parent != bar && renderers[i].transform != bar)
                    renderers[i].color = c ?? baseColors[i];
        }

        public bool CanAttack => !IsDown && cooldown <= 0;

        /// <summary>Golpeia à frente. Acerta todos os adversários no alcance.</summary>
        public bool TryAttack()
        {
            if (!CanAttack) return false;
            cooldown = attackCooldown;
            float dir = FacingRight ? 1f : -1f;
            Vector2 pos = transform.position;
            Fx.Spawn(Fx.Slash, pos + new Vector2(dir * 0.7f, 0.8f), 0.18f, 1.1f, !FacingRight, 40);
            foreach (var other in All.ToArray())
            {
                if (other == this || other.team == team || other.IsDown) continue;
                Vector2 d = (Vector2)other.transform.position - pos;
                if (d.x * dir < -0.2f || Mathf.Abs(d.x) > attackRange || Mathf.Abs(d.y) > 1.2f) continue;
                other.TakeDamage(UnityEngine.Random.Range(damage.x, damage.y + 1), this);
            }
            return true;
        }

        public void TakeDamage(int amount, Combatant from)
        {
            if (IsDown) return;
            hp = Mathf.Max(0, hp - amount);
            flash = 0.12f;
            SetTint(new Color(1f, 0.45f, 0.45f));
            if (body != null && from != null)
            {
                float push = from.transform.position.x < transform.position.x ? 1f : -1f;
                body.linearVelocity = new Vector2(push * 3f, 2.5f);
            }
            UpdateBar();
            if (hp <= 0) GoDown();
        }

        void GoDown()
        {
            SetTint(null);
            transform.rotation = Quaternion.Euler(0, 0, FacingRight ? 90f : -90f);
            if (body != null) { body.linearVelocity = Vector2.zero; body.simulated = false; }
            foreach (var mb in GetComponents<MonoBehaviour>())
                if (mb != this && mb.GetType().Name != "SortingGroup") mb.enabled = false;
            Downed?.Invoke(this);
            if (!knockOutInsteadOfDeath) Destroy(gameObject, 1.2f);
        }

        /// <summary>Levanta um nocauteado com parte da vida.</summary>
        public void Revive(float fraction)
        {
            hp = Mathf.Max(1, Mathf.RoundToInt(maxHp * fraction));
            transform.rotation = Quaternion.identity;
            if (body != null) body.simulated = true;
            foreach (var mb in GetComponents<MonoBehaviour>()) mb.enabled = true;
            UpdateBar();
        }

        public void HealFull()
        {
            hp = maxHp;
            UpdateBar();
        }
    }
}
