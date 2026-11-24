using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>
    /// Faixa no topo da tela que mostra a expedição em andamento: tripulantes à esquerda,
    /// inimigos à direita, barras de vida, golpes e o registro da luta. Clique para trocar de expedição.
    /// </summary>
    public class ExpeditionStrip : MonoBehaviour
    {
        const float Width = 560f, Height = 132f;

        RectTransform root, stage;
        Image background;
        Text header, logText;
        int shown;
        Expedition built;
        int builtStage = -1;
        ExpeditionState builtState;
        readonly List<(ExpeditionFighter f, RectTransform body, RectTransform fill, Text dmg)> slots = new List<(ExpeditionFighter, RectTransform, RectTransform, Text)>();

        public static ExpeditionStrip Create(Transform canvas)
        {
            // O script fica num "suporte" sempre ligado; só o painel some quando não há expedição
            var holder = UIKit.Rect("FaixaDaExpedicao", canvas);
            UIKit.Stretch(holder);
            var strip = holder.gameObject.AddComponent<ExpeditionStrip>();
            var rt = UIKit.Panel("Painel", holder, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -76), new Vector2(Width, Height));
            strip.root = rt;
            strip.background = rt.GetComponent<Image>();
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = strip.background;
            btn.onClick.AddListener(() => strip.shown++);

            strip.header = UIKit.Text(rt, "", 15, TextAnchor.UpperLeft, UIKit.GoldColor);
            strip.header.rectTransform.anchorMin = new Vector2(0, 1);
            strip.header.rectTransform.anchorMax = new Vector2(1, 1);
            strip.header.rectTransform.pivot = new Vector2(0.5f, 1);
            strip.header.rectTransform.sizeDelta = new Vector2(-16, 22);
            strip.header.rectTransform.anchoredPosition = new Vector2(0, -4);

            strip.stage = UIKit.Rect("Palco", rt);
            strip.stage.anchorMin = new Vector2(0, 0);
            strip.stage.anchorMax = new Vector2(0.68f, 1);
            strip.stage.offsetMin = new Vector2(8, 6);
            strip.stage.offsetMax = new Vector2(0, -26);

            strip.logText = UIKit.Text(rt, "", 11, TextAnchor.LowerLeft, UIKit.MutedText);
            strip.logText.rectTransform.anchorMin = new Vector2(0.68f, 0);
            strip.logText.rectTransform.anchorMax = new Vector2(1, 1);
            strip.logText.rectTransform.offsetMin = new Vector2(6, 6);
            strip.logText.rectTransform.offsetMax = new Vector2(-8, -26);
            rt.gameObject.SetActive(false);
            return strip;
        }

        void Update()
        {
            var em = ExpeditionManager.Instance;
            bool any = em != null && em.Active.Count > 0;
            if (!any) { root.gameObject.SetActive(false); return; }
            root.gameObject.SetActive(true);
            var e = em.Active[shown % em.Active.Count];

            if (e != built || e.stage != builtStage || e.state != builtState) BuildFighters(e);
            string more = em.Active.Count > 1 ? $"   ({shown % em.Active.Count + 1}/{em.Active.Count} — clique para trocar)" : "";
            string rum = e.rum > 0 ? $" · rum: {e.rum}" : "";
            header.text = e.StatusText + rum + more;
            var c = e.dungeon.background;
            background.color = new Color(c.r, c.g, c.b, 0.92f);

            var sb = new StringBuilder();
            foreach (var l in e.log) sb.AppendLine(l);
            logText.text = sb.ToString();

            // Animação: golpe (avança um pouco), dano (pisca e mostra o número), caído (fica cinza)
            foreach (var (f, body, fill, dmg) in slots)
            {
                bool enemy = f.member == null;
                float since = e.Clock - f.lastAttack;
                float lunge = since < 0.18f ? (enemy ? -8f : 8f) : 0f;
                body.anchoredPosition = new Vector2(lunge, body.anchoredPosition.y);
                float hit = e.Clock - f.lastHit;
                foreach (var img in body.GetComponentsInChildren<Image>())
                    img.color = WithAlpha(BaseColor(img), f.Alive ? 1f : 0.35f) * (hit < 0.12f ? new Color(1f, 0.5f, 0.5f) : Color.white);
                fill.anchorMax = new Vector2(Mathf.Clamp01((float)f.hp / f.maxHp), 1);
                dmg.text = hit < 0.6f ? $"-{f.lastDamageTaken}" : "";
                dmg.rectTransform.anchoredPosition = new Vector2(0, 34 + hit * 20f);
            }
        }

        static readonly Dictionary<Image, Color> baseColors = new Dictionary<Image, Color>();
        static Color BaseColor(Image i) => baseColors.TryGetValue(i, out var c) ? c : Color.white;
        static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        void BuildFighters(Expedition e)
        {
            built = e;
            builtStage = e.stage;
            builtState = e.state;
            foreach (var s in slots) foreach (var img in s.body.GetComponentsInChildren<Image>()) baseColors.Remove(img);
            slots.Clear();
            UIKit.Clear(stage);

            bool traveling = e.state == ExpeditionState.Going || e.state == ExpeditionState.Returning;
            for (int i = 0; i < e.party.Count; i++) AddSlot(e.party[i], 10 + i * 42f);
            if (!traveling)
                for (int i = 0; i < e.enemies.Count; i++) AddSlot(e.enemies[i], 360 - 10 - i * 42f - 32);
        }

        void AddSlot(ExpeditionFighter f, float x)
        {
            var slot = UIKit.Rect("Lutador", stage);
            slot.anchorMin = slot.anchorMax = new Vector2(0, 0);
            slot.pivot = new Vector2(0, 0);
            slot.sizeDelta = new Vector2(32, 60);
            slot.anchoredPosition = new Vector2(x, 0);

            var body = UIKit.Rect("Corpo", slot);
            body.anchorMin = body.anchorMax = new Vector2(0, 0);
            body.pivot = new Vector2(0, 0);
            body.sizeDelta = new Vector2(32, 48);
            body.anchoredPosition = new Vector2(0, 4);
            DrawFigure(f, body);

            var barBg = UIKit.Rect("Barra", slot);
            barBg.anchorMin = barBg.anchorMax = new Vector2(0, 0);
            barBg.pivot = new Vector2(0, 0);
            barBg.sizeDelta = new Vector2(32, 4);
            barBg.anchoredPosition = new Vector2(0, 56);
            barBg.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);
            var fill = UIKit.Rect("Vida", barBg);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            fill.gameObject.AddComponent<Image>().color = f.member != null ? new Color(0.4f, 0.95f, 0.4f) : new Color(1f, 0.35f, 0.3f);

            var dmg = UIKit.Text(slot, "", 13, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.4f));
            dmg.rectTransform.anchorMin = dmg.rectTransform.anchorMax = new Vector2(0.5f, 0);
            dmg.rectTransform.sizeDelta = new Vector2(40, 18);
            dmg.gameObject.AddComponent<Outline>().effectColor = Color.black;
            slots.Add((f, body, fill, dmg));
        }

        void DrawFigure(ExpeditionFighter f, RectTransform body)
        {
            var em = ExpeditionManager.Instance;
            var cm = CrewManager.Instance;
            var lib = cm.looks;
            if (f.member != null)
            {
                var look = cm.LookOf(f.member);
                if (look != null) Look(body, look.right[0]);
                else Layered(body, cm.sprites.right, f.member.skin, f.member.outfit, f.member.hair, false);
                return;
            }
            switch (f.enemyType)
            {
                case EnemyType.Pirata when lib != null && lib.enemyPirate != null:
                    Look(body, lib.enemyPirate.left[0]);
                    break;
                case EnemyType.CapitaoLendario when lib != null && lib.legendCaptain != null:
                    Look(body, lib.legendCaptain.left[0]);
                    body.localScale = new Vector3(1.35f, 1.35f, 1);
                    break;
                case EnemyType.Esqueleto when lib != null && lib.skeleton != null:
                    Look(body, lib.skeleton.left[0]);
                    break;
                case EnemyType.Pirata:
                    Layered(body, cm.sprites.left, new Color32(232, 178, 128, 255), new Color32(40, 40, 45, 255), new Color32(160, 30, 30, 255), false);
                    break;
                case EnemyType.CapitaoLendario:
                    Layered(body, cm.sprites.left, new Color32(190, 130, 85, 255), new Color32(120, 30, 120, 255), new Color32(240, 200, 60, 255), false);
                    body.localScale = new Vector3(1.35f, 1.35f, 1);
                    break;
                default:
                    var frames = f.enemyType == EnemyType.Caranguejo ? em.crab : f.enemyType == EnemyType.Esqueleto ? em.skeleton : em.serpent;
                    var img = Img(body, frames != null && frames.Length > 0 ? frames[0] : null, Color.white);
                    img.rectTransform.localScale = new Vector3(-1, 1, 1); // inimigos olham para a esquerda
                    if (f.enemyType == EnemyType.Caranguejo) { img.rectTransform.sizeDelta = new Vector2(32, 32); }
                    if (f.enemyType == EnemyType.Serpente) { img.rectTransform.sizeDelta = new Vector2(48, 32); img.rectTransform.anchoredPosition = new Vector2(-8, 0); }
                    break;
            }
        }

        /// <summary>Boneco do Ninja Adventure (16x16) desenhado em dobro.</summary>
        void Look(RectTransform body, Sprite s) => Img(body, s, Color.white).rectTransform.sizeDelta = new Vector2(32, 32);

        void Layered(RectTransform body, CrewManager.DirSprites set, Color skin, Color outfit, Color hair, bool flip)
        {
            Img(body, set.skin[0], skin);
            Img(body, set.outfit[0], outfit);
            Img(body, set.hair[0], hair);
            Img(body, set.baseLayer[0], Color.white);
            if (flip) body.localScale = new Vector3(-1, 1, 1);
        }

        Image Img(RectTransform parent, Sprite s, Color c)
        {
            var rt = UIKit.Rect("Camada", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(32, 48);
            rt.anchoredPosition = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s;
            img.color = c;
            img.raycastTarget = false;
            img.preserveAspect = true;
            baseColors[img] = c;
            return img;
        }
    }
}
