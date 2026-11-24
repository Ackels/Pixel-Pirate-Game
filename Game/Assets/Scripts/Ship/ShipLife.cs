using System.Collections.Generic;
using Pixel.Combat;
using Pixel.Village;
using UnityEngine;

namespace Pixel.Ship
{
    /// <summary>
    /// Vida a bordo (fora de combate): sujeira de gaivota no convés durante a viagem, reserva das redes de dormir
    /// e consultas de lugares do navio (chão, peças de comida/rum/rede, escadas). Cada tripulante tem um CrewLife.
    /// </summary>
    public class ShipLife : MonoBehaviour
    {
        public static ShipLife Instance { get; private set; }
        public const int MaxDirt = 4;

        SeaController sea;
        Sprite dirtSprite;
        float dirtTimer = 15f, dirtPenalty = 40f, gullTimer = 12f;
        readonly List<GameObject> dirt = new List<GameObject>();
        readonly Dictionary<GameObject, CrewLife> dirtClaims = new Dictionary<GameObject, CrewLife>();
        readonly Dictionary<Vector2Int, CrewLife> beds = new Dictionary<Vector2Int, CrewLife>();

        void Awake()
        {
            Instance = this;
            sea = GetComponent<SeaController>();
            dirtSprite = MakeDirtSprite();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public bool Calm => sea != null && (sea.Mode == SeaMode.Sailing || sea.Mode == SeaMode.Docked)
                            && (EncounterDirector.Instance == null || !EncounterDirector.Instance.Busy);

        void Update()
        {
            if (sea == null) return;
            dirt.RemoveAll(d => d == null);
            if (sea.Mode == SeaMode.Editing) ClearDirt();

            // Gaivotas sujando o convés durante a viagem (e gritando de vez em quando)
            if (sea.Mode == SeaMode.Sailing && Calm)
            {
                gullTimer -= Time.deltaTime;
                if (gullTimer <= 0f) { gullTimer = Random.Range(14f, 30f); AudioManager.PlayVaried(Sfx.Gull, 0.6f, 0.2f); }
                dirtTimer -= Time.deltaTime;
                if (dirtTimer <= 0f)
                {
                    dirtTimer = Random.Range(18f, 32f);
                    if (dirt.Count < MaxDirt) SpawnDirt();
                }
            }

            // Convés imundo deixa todo mundo de mau humor
            if (dirt.Count >= 2 && Calm)
            {
                dirtPenalty -= Time.deltaTime;
                if (dirtPenalty <= 0f)
                {
                    dirtPenalty = 40f;
                    CrewLife speaker = null;
                    foreach (var c in sea.CrewFighters)
                    {
                        if (c == null || !(c.link is CrewMember cmb)) continue;
                        cmb.morale = Mathf.Max(0, cmb.morale - 1);
                        if (speaker == null) speaker = c.GetComponent<CrewLife>();
                    }
                    if (speaker != null) speaker.Say("Que imundície no convés!");
                }
            }
            else dirtPenalty = 40f;
        }

        // ---------- Lugares do navio ----------
        public ShipView View => sea != null ? sea.shipView : null;

        /// <summary>Chão onde dá pra ficar em pé (casco cheio ou piso) sem nada sólido em cima.</summary>
        public bool IsFloor(Vector2Int c)
        {
            var l = View.Layout;
            var d = l.Get(c);
            if (d == null || !((d.solid && d.shape == BlockShape.Full) || d.role == BlockRole.Platform)) return false;
            var above = l.Get(c + Vector2Int.up);
            return above == null || !(above.solid || above.role == BlockRole.Platform);
        }

        /// <summary>Chão aberto para o céu (convés).</summary>
        public bool IsOpenDeck(Vector2Int c)
        {
            if (!IsFloor(c)) return false;
            var above = View.Layout.Get(c + Vector2Int.up);
            return above == null || above.role == BlockRole.Decor || above.role == BlockRole.Cannon || above.role == BlockRole.Mast
                   || above.role == BlockRole.Sail || above.role == BlockRole.Flag;
        }

        public List<Vector2Int> FloorCells(bool openOnly)
        {
            var list = new List<Vector2Int>();
            foreach (var c in View.Layout.Blocks.Keys) if (openOnly ? IsOpenDeck(c) : IsFloor(c)) list.Add(c);
            return list;
        }

        public List<Vector2Int> Uses(BlockUse use)
        {
            var list = new List<Vector2Int>();
            foreach (var kv in View.Layout.Blocks) if (kv.Value.use == use) list.Add(kv.Key);
            return list;
        }

        /// <summary>Coluna de escada que liga dois andares (linhas dos pés). null se não houver.</summary>
        public float? LadderBetween(int rowA, int rowB, float nearX)
        {
            int lo = Mathf.Min(rowA, rowB), hi = Mathf.Max(rowA, rowB);
            var l = View.Layout;
            float? best = null;
            var tried = new HashSet<int>();
            foreach (var kv in l.Blocks)
            {
                if (kv.Value.role != BlockRole.Ladder || !tried.Add(kv.Key.x)) continue;
                bool ok = true;
                for (int r = lo; r < hi && ok; r++)
                {
                    var d = l.Get(new Vector2Int(kv.Key.x, r));
                    ok = d != null && d.role == BlockRole.Ladder;
                }
                if (!ok) continue;
                float x = View.CellCenter(new Vector2Int(kv.Key.x, 0)).x;
                if (best == null || Mathf.Abs(x - nearX) < Mathf.Abs(best.Value - nearX)) best = x;
            }
            return best;
        }

        // ---------- Redes ----------
        public bool IsBedFree(Vector2Int c, CrewLife who) => !beds.TryGetValue(c, out var owner) || owner == null || owner == who;

        public bool ReserveBed(Vector2Int c, CrewLife who)
        {
            if (!IsBedFree(c, who)) return false;
            ReleaseBeds(who);
            beds[c] = who;
            return true;
        }

        public void ReleaseBeds(CrewLife who)
        {
            var mine = new List<Vector2Int>();
            foreach (var kv in beds) if (kv.Value == who) mine.Add(kv.Key);
            foreach (var c in mine) beds.Remove(c);
        }

        // ---------- Sujeira ----------
        public bool HasFreeDirt
        {
            get
            {
                foreach (var d in dirt) if (d != null && (!dirtClaims.TryGetValue(d, out var w) || w == null)) return true;
                return false;
            }
        }

        public GameObject ClaimDirt(CrewLife who, float nearX)
        {
            GameObject best = null;
            foreach (var d in dirt)
            {
                if (d == null || (dirtClaims.TryGetValue(d, out var w) && w != null && w != who)) continue;
                if (best == null || Mathf.Abs(d.transform.position.x - nearX) < Mathf.Abs(best.transform.position.x - nearX)) best = d;
            }
            if (best != null) dirtClaims[best] = who;
            return best;
        }

        public void ReleaseDirt(CrewLife who)
        {
            var mine = new List<GameObject>();
            foreach (var kv in dirtClaims) if (kv.Value == who) mine.Add(kv.Key);
            foreach (var d in mine) dirtClaims.Remove(d);
        }

        public void Clean(GameObject d)
        {
            if (d == null) return;
            dirt.Remove(d);
            dirtClaims.Remove(d);
            Destroy(d);
        }

        void SpawnDirt()
        {
            var cells = FloorCells(true);
            if (cells.Count == 0) return;
            var c = cells[Random.Range(0, cells.Count)];
            var go = new GameObject("Sujeira").AddComponent<SpriteRenderer>();
            go.transform.SetParent(View.transform, false);
            go.transform.localPosition = new Vector3(c.x + Random.Range(0.25f, 0.75f), c.y + 1f + 0.06f, 0);
            go.sprite = dirtSprite;
            go.sortingOrder = 2;
            dirt.Add(go.gameObject);
        }

        void ClearDirt()
        {
            foreach (var d in dirt) if (d != null) Destroy(d);
            dirt.Clear();
            dirtClaims.Clear();
        }

        /// <summary>Mancha de gaivota (branca com borda cinza), desenhada pixel a pixel.</summary>
        static Sprite MakeDirtSprite()
        {
            string[] art =
            {
                "..XX.X..",
                ".XWWXWX.",
                "XWWWWWWX",
                ".XXXXXX.",
            };
            var tex = new Texture2D(8, 4) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var white = new Color32(236, 232, 220, 255);
            var gray = new Color32(150, 146, 150, 255);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 8; x++)
                {
                    char ch = art[3 - y][x];
                    tex.SetPixel(x, y, ch == 'W' ? (Color)white : ch == 'X' ? (Color)gray : Color.clear);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 8, 4), new Vector2(0.5f, 0f), 32f);
        }
    }
}
