using System.Collections.Generic;
using System.Linq;
using Pixel.Village;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Pixel.EditorTools
{
    /// <summary>
    /// A ilha da vila (vista de cima), gerada sempre igual (semente fixa): costa irregular com o mar em volta,
    /// praia, grama com variação natural de tom, mar mais escuro longe da costa, um caminho do píer até a ilha e a
    /// natureza: coqueiros na praia, uma faixa de mata (árvores, bosques, arbustos, rochas) e um campo aberto no meio
    /// com algumas árvores, pedras, flores e capim. Árvores, pedras, tocos e arbustos ocupam a grade
    /// (NatureObject) e são limpos com Demolir; flores e capim são só enfeite.
    /// </summary>
    public static partial class VillageSceneBuilder
    {
        const int DockX0 = 30, DockX1 = 32;

        struct IslandResult { public Tilemap water, detail; public int shoreY; }

        static IslandResult BuildIsland()
        {
            var rng = new System.Random(42);
            var grassTiles = Enumerable.Range(0, 5).Select(i => NinjaTileAsset($"Tiles/grama_{i}")).ToArray();
            var sandDetail = new[] { NinjaTileAsset("Tiles/sand_detail_0"), NinjaTileAsset("Tiles/sand_detail_1") };
            var dock = NinjaTileAsset("Tiles/dock");

            var grid = new GameObject("Grid", typeof(Grid));
            Tilemap groundMap = CreateTilemap(grid.transform, "Chao", -22);
            Tilemap sandMap = CreateTilemap(grid.transform, "Areia", -21);
            Tilemap waterMap = CreateTilemap(grid.transform, "Agua", -21);
            Tilemap detailMap = CreateTilemap(grid.transform, "Caminhos_Docas", -19);
            var waterCol = waterMap.gameObject.AddComponent<TilemapCollider2D>();
            waterMap.gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            waterMap.gameObject.AddComponent<CompositeCollider2D>();
            waterCol.compositeOperation = Collider2D.CompositeOperation.Merge;

            // ---------- Forma da ilha: elipse deformada por ruído ----------
            float cx = MapW / 2f, cy = MapH / 2f + 0.5f, rx = MapW / 2f - 5f, ry = MapH / 2f - 4.5f;
            var land = new bool[MapW, MapH];
            for (int x = 0; x < MapW; x++)
                for (int y = 0; y < MapH; y++)
                {
                    float nx = (x + 0.5f - cx) / rx, ny = (y + 0.5f - cy) / ry;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float n = Mathf.PerlinNoise(x * 0.085f + 13.7f, y * 0.085f + 7.3f) - 0.5f;
                    float n2 = Mathf.PerlinNoise(x * 0.21f + 40f, y * 0.21f + 9f) - 0.5f;
                    land[x, y] = d + n * 0.42f + n2 * 0.12f < 1f;
                }
            // O píer sempre chega na praia no mesmo lugar (o embarque e os visitantes usam essas posições)
            for (int x = DockX0 - 3; x <= DockX1 + 3; x++) for (int y = 0; y <= 5; y++) land[x, y] = false;
            for (int x = DockX0 - 1; x <= DockX1 + 1; x++) for (int y = 6; y <= 10; y++) land[x, y] = true;
            // Só a ilha principal (ilhotas soltas viram mar)
            var main = new bool[MapW, MapH];
            var stack = new Stack<Vector2Int>();
            stack.Push(new Vector2Int((int)cx, (int)cy));
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                if (c.x < 0 || c.y < 0 || c.x >= MapW || c.y >= MapH || main[c.x, c.y] || !land[c.x, c.y]) continue;
                main[c.x, c.y] = true;
                stack.Push(c + Vector2Int.right); stack.Push(c + Vector2Int.left); stack.Push(c + Vector2Int.up); stack.Push(c + Vector2Int.down);
            }
            land = main;
            bool Land(int x, int y) => x >= 0 && y >= 0 && x < MapW && y < MapH && land[x, y];
            int shoreY = 0;
            while (shoreY < MapH && !Land(DockX0 + 1, shoreY)) shoreY++;

            // Distância até a água (em terra) e até a terra (na água), 8 vizinhos
            var dist = new int[MapW, MapH];
            var q = new Queue<Vector2Int>();
            for (int x = 0; x < MapW; x++)
                for (int y = 0; y < MapH; y++)
                {
                    bool edge = false;
                    for (int dx = -1; dx <= 1 && !edge; dx++)
                        for (int dy = -1; dy <= 1 && !edge; dy++)
                            if (Land(x, y) != Land(x + dx, y + dy) && x + dx >= 0 && y + dy >= 0 && x + dx < MapW && y + dy < MapH) edge = true;
                    dist[x, y] = edge ? 1 : int.MaxValue;
                    if (edge) q.Enqueue(new Vector2Int(x, y));
                }
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int nx = c.x + dx, ny = c.y + dy;
                        if (nx < 0 || ny < 0 || nx >= MapW || ny >= MapH) continue;
                        if (Land(nx, ny) != Land(c.x, c.y)) continue;
                        if (dist[nx, ny] > dist[c.x, c.y] + 1) { dist[nx, ny] = dist[c.x, c.y] + 1; q.Enqueue(new Vector2Int(nx, ny)); }
                    }
            }
            int D(int x, int y) => dist[Mathf.Clamp(x, 0, MapW - 1), Mathf.Clamp(y, 0, MapH - 1)];
            bool Sand(int x, int y) => Land(x, y) && D(x, y) <= (Mathf.PerlinNoise(x * 0.15f + 70f, y * 0.15f) > 0.62f ? 3 : 2);
            bool IsDock(int x, int y) => x >= DockX0 && x <= DockX1 && y < shoreY;
            int Cx(int x) => Mathf.Clamp(x, 0, MapW - 1);
            int Cy(int y) => Mathf.Clamp(y, 0, MapH - 1);
            bool WaterR(int x, int y) => !Land(Cx(x), Cy(y));
            bool SandR(int x, int y) => !Land(Cx(x), Cy(y)) || Sand(Cx(x), Cy(y));

            // Caminho: do píer até um pouco para dentro da ilha
            var pathCells = new HashSet<Vector2Int>();
            for (int y = shoreY; y < shoreY + 6; y++) for (int x = DockX0; x <= DockX1; x++) pathCells.Add(new Vector2Int(x, y));
            bool PathR(int x, int y) => pathCells.Contains(new Vector2Int(x, y));

            // ---------- Pintura do chão ----------
            void Paint(Tilemap map, Vector3Int cell, TileBase tile, Color color)
            {
                map.SetTile(cell, tile);
                map.SetTileFlags(cell, TileFlags.None);
                map.SetColor(cell, color);
            }
            for (int x = 0; x < MapW; x++)
                for (int y = 0; y < MapH; y++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    if (!Land(x, y))
                    {
                        // Mar: claro perto da praia, mais fundo (escuro) longe dela
                        float deep = Mathf.Clamp01((D(x, y) - 1) / 5f);
                        var wc = Color.Lerp(Color.white, new Color(0.5f, 0.66f, 0.86f), deep);
                        var w = AutoTile("water", Mask(WaterR, x, y));
                        if (IsDock(x, y)) { Paint(groundMap, cell, w, wc); detailMap.SetTile(cell, dock); }
                        else Paint(waterMap, cell, w, wc);
                        continue;
                    }
                    // Grama com manchas de tom (claro no campo, mais escuro e úmido perto da mata)
                    float t = Mathf.PerlinNoise(x * 0.11f + 3f, y * 0.11f + 5f);
                    float t2 = Mathf.PerlinNoise(x * 0.33f + 21f, y * 0.33f + 2f);
                    var g = Color.Lerp(new Color(0.84f, 0.9f, 0.82f), Color.white, t * 0.75f + t2 * 0.25f);
                    int dl = D(x, y);
                    if (dl >= 3 && dl <= 6) g *= 0.93f;
                    g.a = 1f;
                    Paint(groundMap, cell, grassTiles[rng.Next(100) < 82 ? 0 : rng.Next(1, grassTiles.Length)], g);
                    if (Sand(x, y))
                    {
                        int m = Mask(SandR, x, y);
                        float s = 0.94f + 0.06f * Mathf.PerlinNoise(x * 0.2f, y * 0.2f + 30f);
                        Paint(sandMap, cell, m == 255 && rng.Next(100) < 10 ? sandDetail[rng.Next(sandDetail.Length)] : AutoTile("sand", m), new Color(s, s, s * 0.98f));
                    }
                }
            foreach (var c in pathCells) detailMap.SetTile((Vector3Int)c, AutoTile("path2", Mask(PathR, c.x, c.y)));

            // ---------- Natureza ----------
            var objects = new GameObject("Cenario").transform;
            var nature = new GameObject("Natureza").transform;
            var taken = new HashSet<Vector2Int>();
            // Perto do píer e do caminho fica livre (chegada do capitão e dos visitantes)
            foreach (var c in pathCells) for (int dx = -2; dx <= 2; dx++) for (int dy = -1; dy <= 2; dy++) taken.Add(c + new Vector2Int(dx, dy));
            for (int dx = -4; dx <= 4; dx++) for (int dy = -3; dy <= 3; dy++) taken.Add(new Vector2Int(31 + dx, 12 + dy));

            // Barris e caixa na chegada do píer
            foreach (var p in new[] { new Vector2(DockX0 - 1.5f, shoreY + 1.2f), new Vector2(DockX0 - 1.5f, shoreY + 2.4f), new Vector2(DockX1 + 2.5f, shoreY + 1.4f) })
                CreateProp(objects, "Barril", Ninja("Props/barril"), p, new Vector2(0.6f, 0.4f), new Vector2(0, 0.25f));
            CreateProp(objects, "Caixa", Ninja("Props/caixa"), new Vector2(DockX1 + 2.5f, shoreY + 2.5f), new Vector2(0.8f, 0.4f), new Vector2(0, 0.25f));

            Sprite N(string n) => Ninja("Natureza/" + n);
            var palm = Ninja("Props/coqueiro");
            var treesSmall = new[] { N("arvore_redonda_0"), N("arvore_redonda_1"), N("arvore_redonda_2"), N("arvore_pequena") };
            var treesBig = new[] { N("arvore_grande_0"), N("arvore_grande_0"), N("arvore_grande_0"), N("arvore_grande_1") };
            var groves = new[] { N("bosque_0"), N("bosque_1"), N("bosque_2") };
            var rocksBig = new[] { N("rochedo_0"), N("rochedo_1"), N("rocha_1"), N("rocha_3"), N("rocha_4"), N("rocha_5") };
            var rocksMed = new[] { N("pedra_m_0"), N("pedra_m_1"), N("pedra_m_2"), N("pedra_m_3"), N("rocha_0"), N("rocha_2") };
            var rocksSmall = new[] { N("pedra_p_0"), N("pedra_p_1"), N("pedra_p_2"), N("pedra_p_3"), N("pedra_p_4"), N("pedra_p_5") };
            var stumps = new[] { N("toco_0"), N("toco_1") };
            var bushes = new[] { N("arbusto_0"), N("arbusto_1"), N("arbusto_2"), N("arbusto_3") };
            var berries = new[] { N("frutinhas_0"), N("frutinhas_1"), N("frutinhas_2") };
            var flowers = new[] { N("flor_0"), N("flor_1"), N("flor_2"), N("flor_3"), Ninja("Props/flor_0"), Ninja("Props/flor_1") };
            var tufts = new[] { N("capim_0"), N("capim_1"), N("capim_3"), N("capim_4"),
                                N("detalhe_0"), N("detalhe_1"), N("detalhe_2"), N("detalhe_3"), N("detalhe_6"), N("detalhe_8") };
            var shadowP = N("sombra_p"); var shadowM = N("sombra_m"); var shadowG = N("sombra_g");
            int nextId = 0;

            bool Free(int x, int y, int w, int h, bool sandOk)
            {
                for (int i = 0; i < w; i++)
                    for (int j = 0; j < h; j++)
                    {
                        int xx = x + i, yy = y + j;
                        if (xx < 1 || yy < 1 || xx >= MapW - 1 || yy >= MapH - 1) return false;
                        if (!Land(xx, yy) || taken.Contains(new Vector2Int(xx, yy)) || PathR(xx, yy)) return false;
                        if (!sandOk && Sand(xx, yy)) return false;
                    }
                return true;
            }

            void Put(Sprite[] set, NatureObject.Kind kind, int x, int y, int w, int h, bool blocks, Sprite shadow, float shadowW, bool sandOk = false, float tintJitter = 0.08f)
            {
                if (set == null || set.Length == 0 || !Free(x, y, w, h, sandOk)) return;
                var sprite = set[rng.Next(set.Length)];
                if (sprite == null) return;
                var go = new GameObject(kind.ToString());
                go.transform.SetParent(nature, false);
                go.transform.position = new Vector2(x + w / 2f, y + 0.1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                sr.flipX = kind != NatureObject.Kind.Rock && rng.Next(2) == 0;
                float j = 1f - (float)rng.NextDouble() * tintJitter;
                sr.color = new Color(j, Mathf.Min(1f, j + tintJitter * 0.3f), j, 1f);
                var n = go.AddComponent<NatureObject>();
                n.id = blocks ? nextId++ : -1;
                n.kind = kind;
                n.blocks = blocks;
                n.cell = new Vector2Int(x, y);
                n.size = new Vector2Int(w, h);
                n.baseColor = sr.color;
                if (blocks)
                {
                    var col = go.AddComponent<BoxCollider2D>();
                    col.size = new Vector2(Mathf.Max(0.4f, w * 0.6f), 0.35f);
                    col.offset = new Vector2(0, 0.2f);
                }
                if (shadow != null)
                {
                    var sh = new GameObject("Sombra").AddComponent<SpriteRenderer>();
                    sh.transform.SetParent(go.transform, false);
                    sh.transform.localPosition = new Vector3(0, 0.08f, 0);
                    float sw = shadowW / (shadow.rect.width / 16f);
                    sh.transform.localScale = new Vector3(sw, sw, 1f);
                    sh.sprite = shadow;
                    sh.sortingOrder = -10;
                }
                for (int i = 0; i < w; i++) for (int k = 0; k < h; k++) taken.Add(new Vector2Int(x + i, y + k));
                // Um pouco de folga em volta das grandes (não ficam grudadas)
                if (blocks && w >= 2) for (int i = -1; i <= w; i++) taken.Add(new Vector2Int(x + i, y + h));
            }

            var cells = new List<Vector2Int>();
            for (int x = 1; x < MapW - 1; x++) for (int y = 1; y < MapH - 1; y++) if (Land(x, y)) cells.Add(new Vector2Int(x, y));
            for (int i = cells.Count - 1; i > 0; i--) { int k = rng.Next(i + 1); (cells[i], cells[k]) = (cells[k], cells[i]); }

            const NatureObject.Kind T = NatureObject.Kind.Tree, R = NatureObject.Kind.Rock, B = NatureObject.Kind.Bush;
            foreach (var c in cells)
            {
                int x = c.x, y = c.y, d = D(x, y);
                double r = rng.NextDouble();
                if (Sand(x, y))
                {
                    // Praia: coqueiros e pedrinhas
                    if (r < 0.10) Put(new[] { palm }, NatureObject.Kind.Palm, x, y, 1, 1, true, shadowP, 1.4f, true, 0.04f);
                    else if (r < 0.125) Put(rocksSmall, R, x, y, 1, 1, true, shadowP, 0.9f, true);
                }
                else if (d <= 6)
                {
                    // Mata em volta da ilha
                    if (r < 0.035) Put(groves, T, x, y, 4, 2, true, shadowG, 4.2f);
                    else if (r < 0.075) Put(treesBig, T, x, y, 3, 1, true, shadowG, 2.8f);
                    else if (r < 0.17) Put(treesSmall, T, x, y, 2, 1, true, shadowM, 2f);
                    else if (r < 0.19) Put(new[] { palm }, NatureObject.Kind.Palm, x, y, 1, 1, true, shadowP, 1.4f, false, 0.04f);
                    else if (r < 0.26) Put(bushes, B, x, y, 1, 1, true, shadowP, 1f);
                    else if (r < 0.28) Put(berries, B, x, y, 2, 1, true, shadowM, 1.8f);
                    else if (r < 0.30) Put(rocksMed, R, x, y, 2, 1, true, shadowM, 1.8f);
                    else if (r < 0.312) Put(rocksBig, R, x, y, 4, 2, true, shadowG, 3.6f);
                    else if (r < 0.324) Put(stumps, NatureObject.Kind.Stump, x, y, 2, 1, true, shadowM, 1.6f);
                    else if (r < 0.36) Put(flowers, NatureObject.Kind.Flower, x, y, 1, 1, false, null, 0f);
                    else if (r < 0.43) Put(tufts, NatureObject.Kind.Grass, x, y, 1, 1, false, null, 0f);
                }
                else
                {
                    // Campo aberto no meio (onde dá para construir): pouca coisa, bastante flor e capim
                    if (r < 0.009) Put(treesSmall, T, x, y, 2, 1, true, shadowM, 2f);
                    else if (r < 0.013) Put(treesBig, T, x, y, 3, 1, true, shadowG, 2.8f);
                    else if (r < 0.023) Put(bushes, B, x, y, 1, 1, true, shadowP, 1f);
                    else if (r < 0.031) Put(rocksSmall, R, x, y, 1, 1, true, shadowP, 0.9f);
                    else if (r < 0.035) Put(rocksMed, R, x, y, 2, 1, true, shadowM, 1.8f);
                    else if (r < 0.038) Put(stumps, NatureObject.Kind.Stump, x, y, 2, 1, true, shadowM, 1.6f);
                    else if (r < 0.07) Put(flowers, NatureObject.Kind.Flower, x, y, 1, 1, false, null, 0f);
                    else if (r < 0.13) Put(tufts, NatureObject.Kind.Grass, x, y, 1, 1, false, null, 0f);
                }
            }
            Debug.Log($"[Pixel] Ilha da vila: {nextId} peças de natureza que ocupam a grade, praia do píer em y={shoreY}.");
            UnityEditor.AssetDatabase.SaveAssets();
            return new IslandResult { water = waterMap, detail = detailMap, shoreY = shoreY };
        }
    }
}
