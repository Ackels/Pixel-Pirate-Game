"""Exporta a arte do Ninja Adventure usada na vila para Game/Assets/Art/Ninja.

- Auto/: peças de borda automáticas (água, areia, caminho), uma por "máscara" de vizinhos.
- Tiles/: grama, detalhes de areia, píer.
- Buildings/ e Props/: prédios e decoração recortados (pivô embaixo no centro).
- Characters/<Nome>/: SpriteSheet (16x16 por quadro) e Faceset (retrato).
"""
import sys, os, shutil
sys.path.insert(0, os.path.dirname(__file__))
import png

A, OUT = sys.argv[1], sys.argv[2]
N = A + '/NinjaAdventure/Ninja Adventure - Asset Pack'
TS = N + '/Backgrounds/Tilesets/'
T = A + '/TreasureHunters/Treasure Hunters'

cache = {}
def load(path):
    if path not in cache: cache[path] = png.read(path)
    return cache[path]

def crop(path, x, y, w, h):
    _, _, p = load(path)
    return [row[x:x + w] for row in p[y:y + h]]

def save(sub, name, px):
    os.makedirs(f'{OUT}/{sub}', exist_ok=True)
    png.write(f'{OUT}/{sub}/{name}.png', len(px[0]), len(px), px)

# ---------- Peças de borda automáticas ----------
# Bits da máscara: 1=N 2=NE 4=E 8=SE 16=S 32=SW 64=W 128=NW (canto só vale se os dois lados valem)
SAMPLES = [(8, 2, 1), (13, 2, 2), (13, 8, 4), (13, 13, 8), (8, 13, 16), (2, 13, 32), (2, 8, 64), (2, 2, 128)]
def normalize(m):
    if not (m & 1 and m & 4): m &= ~2
    if not (m & 4 and m & 16): m &= ~8
    if not (m & 16 and m & 64): m &= ~32
    if not (m & 64 and m & 1): m &= ~128
    return m

def autotiles(name, px, cols, rows, x0, y0, filled):
    found = {}
    for r in range(rows):
        for c in range(cols):
            tile = [row[x0 + c * 16:x0 + c * 16 + 16] for row in px[y0 + r * 16:y0 + r * 16 + 16]]
            if len(tile) < 16 or len(tile[0]) < 16: continue
            m = 0
            for sx, sy, bit in SAMPLES:
                if filled(tile[sy][sx]): m |= bit
            if not filled(tile[8][8]) and m == 0: continue
            m = normalize(m)
            if m not in found:
                found[m] = tile
    # Completa máscaras que faltam espelhando as que existem
    def fh(m):  # espelho horizontal: E<->W, NE<->NW, SE<->SW
        bits = {1: 1, 2: 128, 4: 64, 8: 32, 16: 16, 32: 8, 64: 4, 128: 2}
        return sum(v for k, v in bits.items() if m & k)
    def fv(m):  # espelho vertical: N<->S, NE<->SE, NW<->SW
        bits = {1: 16, 2: 8, 4: 4, 8: 2, 16: 1, 32: 128, 64: 64, 128: 32}
        return sum(v for k, v in bits.items() if m & k)
    base = dict(found)
    for m, tile in base.items():
        for mm, tt in ((fh(m), [row[::-1] for row in tile]), (fv(m), tile[::-1]), (fh(fv(m)), [row[::-1] for row in tile[::-1]])):
            if mm not in found: found[mm] = tt
    for m, tile in found.items():
        save('Auto', f'{name}_{m:03d}', tile)
    print(name, 'máscaras:', len(found), sorted(found))

_, _, water = load(TS + 'TilesetWater.png')
autotiles('water', water, 11, 5, 0, 0, lambda p: p[3] > 0 and p[2] >= 150)

_, _, floor = load(TS + 'TilesetFloor.png')
autotiles('path', floor, 11, 5, 0, 7 * 16, lambda p: p[3] > 0 and p[0] - p[1] > 30)

# Areia sobre grama: o conjunto "neve" (TilesetFloorB) recolorido para areia
_, _, fb = load(TS + 'TilesetFloorB.png')
SAND, SAND_EDGE = (255, 203, 141, 255), (255, 173, 93, 255)
sand = [[(SAND if p[:3] == (242, 234, 241) else SAND_EDGE if p[:3] == (184, 220, 229) else p) if p[3] else p for p in row] for row in fb]
autotiles('sand', sand, 11, 5, 0, 0, lambda p: p[3] > 0)

# ---------- Tiles soltos ----------
FL = TS + 'TilesetFloor.png'
for i in range(5): save('Tiles', f'grass_{i}', crop(FL, i * 16, 12 * 16, 16, 16))
save('Tiles', 'sand_detail_0', crop(FL, 0, 4 * 16, 16, 16))
save('Tiles', 'sand_detail_1', crop(FL, 16, 4 * 16, 16, 16))
save('Tiles', 'dock', crop(TS + 'TilesetWater.png', 16, 13 * 16, 16, 16))

# ---------- Prédios ----------
HO, EL, CA = TS + 'TilesetHouse.png', TS + 'TilesetElement.png', TS + 'tileset_camp.png'
def t(path, c, r, w, h): return crop(path, c * 16, r * 16, w * 16, h * 16)
save('Buildings', 'casa', t(HO, 26, 0, 3, 3))
save('Buildings', 'taverna', t(HO, 19, 0, 3, 3))
save('Buildings', 'destilaria', t(HO, 23, 0, 3, 3))
save('Buildings', 'mercado', t(HO, 16, 0, 3, 3))
save('Buildings', 'estaleiro', t(HO, 25, 14, 4, 5))
save('Buildings', 'horta', t(EL, 13, 5, 2, 2))
save('Buildings', 'cabana_pesca', t(CA, 4, 0, 3, 3))
save('Buildings', 'quadro', t(CA, 7, 5, 2, 2))

# ---------- Decoração ----------
NA = TS + 'TilesetNature.png'
save('Props', 'arvore_0', t(NA, 0, 0, 2, 2))
save('Props', 'arvore_1', t(NA, 16, 0, 2, 2))
save('Props', 'arvore_2', t(NA, 18, 0, 2, 2))
FD = TS + 'TilesetFloorDetail.png'
save('Props', 'moita_0', t(FD, 0, 2, 1, 1))
save('Props', 'moita_1', t(FD, 2, 2, 1, 1))
save('Props', 'flor_0', t(NA, 0, 11, 1, 1))
save('Props', 'flor_1', t(NA, 1, 11, 1, 1))
save('Props', 'barril', t(EL, 0, 0, 1, 1))
save('Props', 'caixa', t(EL, 1, 0, 1, 1))

# Coqueiro: copa + tronco do Treasure Hunters, recoloridos com a paleta das árvores do Ninja Adventure
TOP = T + '/Palm Tree Island/Sprites/Front Palm Trees/Front Palm Tree Top 01.png'
TRK = T + '/Palm Tree Island/Sprites/Front Palm Trees/Front Palm Bottom and Grass (32x32).png'
tw, th, top = load(TOP)
trunk = crop(TRK, 0, 0, 32, 32)
base = crop(TRK, 0, 32, 32, 32)
def lum(c): return 0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2]
def remap(px, targets):
    cols = sorted({p[:3] for row in px for p in row if p[3]}, key=lum)
    # o mais escuro vira contorno; o resto é distribuído pelos alvos (escuro -> claro)
    mapping = {}
    for i, c in enumerate(cols):
        mapping[c] = targets[min(len(targets) - 1, int(i * len(targets) / max(1, len(cols))))]
    return [[(mapping[p[:3]] + (p[3],)) if p[3] else p for p in row] for row in px]
LEAF = [(20, 27, 27), (74, 127, 75), (116, 163, 52), (173, 188, 58)]
BARK = [(20, 27, 27), (150, 87, 70), (228, 109, 58), (241, 160, 98)]
top = remap(top, LEAF)
trunk = remap(trunk, BARK)
base = remap(base, BARK)
W, H = 40, 30 + 24
palm = [[(0, 0, 0, 0)] * W for _ in range(H)]
def blit(src, ox, oy):
    for y, row in enumerate(src):
        for x, p in enumerate(row):
            if p[3] and 0 <= oy + y < H and 0 <= ox + x < W: palm[oy + y][ox + x] = p
blit([r[:] for r in trunk[8:32]], 4, H - 24)       # tronco
blit(top, 0, 0)                                     # copa
save('Props', 'coqueiro', palm)

# ---------- Personagens ----------
LOOKS = ['Noble', 'Boy', 'Hunter', 'Child', 'Villager', 'Villager2', 'Villager3', 'Villager4', 'Villager5',
         'Villager6', 'OldMan', 'OldMan2', 'OldMan3', 'Shaman', 'Woman', 'OldWoman', 'Cavegirl', 'Caveman',
         'FighterRed', 'Monk', 'Inspector', 'Princess', 'Sultan', 'Skeleton', 'Eskimo']
for name in LOOKS:
    src = f'{N}/Actor/Character/{name}'
    dst = f'{OUT}/Characters/{name}'
    os.makedirs(dst, exist_ok=True)
    shutil.copyfile(f'{src}/SpriteSheet.png', f'{dst}/SpriteSheet.png')
    if os.path.exists(f'{src}/Faceset.png'): shutil.copyfile(f'{src}/Faceset.png', f'{dst}/Faceset.png')
print('ok')
