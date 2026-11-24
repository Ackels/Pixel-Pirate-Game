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

# ---------- Ícones da interface ----------
IT = N + '/Items/'
save('Icons', 'peixe', load(IT + 'Food/Fish.png')[2])
save('Icons', 'rum', load(T + '/Pirate Treasure/Sprites/Green Bottle/01.png')[2])
save('Icons', 'cana', crop(TS + 'TilesetNature.png', 11 * 16, 8 * 16, 16, 16))
save('Icons', 'ouro', load(T + '/Pirate Treasure/Sprites/Gold Coin/01.png')[2])
save('Icons', 'tripulacao', load(N + '/Actor/Character/Noble/Faceset.png')[2])


def tight(frames):
    """Recorta todos os quadros pela mesma caixa (a união do que tem pixel)."""
    xs, ys = [], []
    for f in frames:
        for y, row in enumerate(f):
            for x, p in enumerate(row):
                if p[3]: xs.append(x); ys.append(y)
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    return [[row[x0:x1 + 1] for row in f[y0:y1 + 1]] for f in frames]


# ---------- Bichos da faixa de expedições (desenhados virados para a direita) ----------
CR = T + '/The Crusty Crew/Sprites/Crabby/02-Run/'
crab = tight([load(CR + f)[2] for f in sorted(os.listdir(CR))[:2]])
for i, f in enumerate(crab): save('Monsters', f'caranguejo_{i}', f)
SN = N + '/Actor/Monster/Snake/Snake.png'
for i in range(2): save('Monsters', f'serpente_{i}', crop(SN, 3 * 16, i * 16, 16, 16))
SK = N + '/Actor/Character/Skeleton/SpriteSheet.png'
for i in range(2): save('Monsters', f'esqueleto_{i}', crop(SK, 3 * 16, i * 16, 16, 16))

# ---------- Fogueira (vista de lado, no estilo do Ninja Adventure) ----------
PAL = {'.': (0, 0, 0, 0), 'o': (20, 27, 27, 255), 'b': (150, 87, 70, 255), 'B': (228, 109, 58, 255),
       'r': (214, 60, 52, 255), 'f': (247, 118, 34, 255), 'y': (254, 174, 52, 255), 'w': (255, 236, 140, 255)}
FIRE = [
    '................',
    '.......o........',
    '......ofo.......',
    '......ofoo......',
    '.....ofyfo......',
    '....offyffo.o...',
    '...ofyyyyfoofo..',
    '...ofywwyyffyo..',
    '..ofyywwwyyyfo..',
    '..orfywwwwyyfo..',
    '..orffyywyyfro..',
    '.oobbrffffrbboo.',
    'obBBBoorrooBBBbo',
    'obbBBBBooBBBBbbo',
    '.oobbbboobbbboo.',
    '...oooo..oooo...',
]
save('Props', 'fogueira', [[PAL[c] for c in row] for row in FIRE])
print('extras ok')

# ---------- Construções das ilhas vistas de lado (as do pacote são vistas de cima) ----------
OUTL, LIGHT, MID, DARK, WOOD = (20, 27, 27, 255), (241, 196, 113, 255), (215, 139, 74, 255), (145, 82, 44, 255), (163, 117, 78, 255)
NONE = (0, 0, 0, 0)


def line(img, x0, y0, x1, y1, c):
    """Linha de pixels (Bresenham)."""
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
    err = dx + dy
    while True:
        if 0 <= y0 < len(img) and 0 <= x0 < len(img[0]) and img[y0][x0][3] == 0: img[y0][x0] = c
        if x0 == x1 and y0 == y1: break
        e2 = 2 * err
        if e2 >= dy: err += dy; x0 += sx
        if e2 <= dx: err += dx; y0 += sy


def side_tent():
    W, H = 40, 28
    img = [[NONE] * W for _ in range(H)]
    apex, base, cx = 4, H - 1, 20            # a última linha é o chão: o pivô fica rente à grama
    edges = {}
    for y in range(apex + 1, base + 1):
        half = max(1, int(round((y - apex) * 16 / (base - apex))))
        x0, x1 = cx - half, cx + half - 1
        edges[y] = (x0, x1)
        for x in range(x0, x1 + 1):
            left = x < cx
            c = LIGHT if left else MID
            if (x - cx) % 6 == 0 and x not in (x0, x1): c = MID if left else DARK   # dobras da lona
            if x in (x0, x1) or y == base: c = OUTL
            img[y][x] = c
    # Porta: triângulo escuro com as abas abertas
    for y in range(17, base):
        half = int(round((y - 17) * 4 / (base - 17)))
        for x in range(cx - half - 1, cx + half + 1): img[y][x] = OUTL
        if half >= 1:
            img[y][cx - half - 2] = WOOD
            img[y][cx + half + 1] = DARK
    # Mastro saindo do topo da lona
    for y in range(0, apex + 1):
        img[y][cx - 1] = OUTL
        img[y][cx] = OUTL if y in (0, apex) else WOOD
    # Cordas: do meio de cada lado da lona até uma estaca no chão
    ry = 15
    lx, rx = edges[ry]
    line(img, lx - 1, ry, 1, base - 1, DARK)
    line(img, rx + 1, ry, W - 2, base - 1, DARK)
    for x in (0, 1, 2, W - 3, W - 2, W - 1): img[base][x] = OUTL
    img[base - 1][1] = WOOD; img[base - 1][W - 2] = WOOD
    return img


def side_stall():
    """Barraca de feira: toldo listrado, postes, balcão de madeira e mercadorias."""
    W, H = 48, 40
    img = [[NONE] * W for _ in range(H)]
    RED, CLOTH, CLOTHS = (214, 60, 52, 255), (240, 230, 208, 255), (214, 196, 166, 255)
    base = H - 1
    # Postes
    for x in (4, 5, W - 6, W - 5):
        for y in range(10, base + 1): img[y][x] = OUTL if x in (4, W - 5) else WOOD
    # Balcão (caixa de tábuas)
    top, bot = 26, base
    for y in range(top, bot + 1):
        for x in range(2, W - 2):
            c = MID if (y - top) % 4 else DARK
            if (x - 2) % 11 == 0: c = DARK
            if y in (top, bot) or x in (2, W - 3): c = OUTL
            img[y][x] = c
    for x in range(1, W - 1):                      # tampo do balcão
        img[top - 1][x] = OUTL; img[top][x] = LIGHT if 1 < x < W - 2 else OUTL
    # Toldo listrado com a borda de baixo em ondas
    for y in range(4, 13):
        inset = max(0, 4 - (y - 4))
        for x in range(inset, W - inset):
            stripe = ((x - inset) // 6) % 2 == 0
            c = RED if stripe else CLOTH
            if y == 12 and (x % 6) in (0, 5): continue  # recorte das ondas
            if y in (4,) or x in (inset, W - inset - 1): c = OUTL
            elif y == 5 and not stripe: c = CLOTHS
            img[y][x] = c
    for x in range(W):
        if img[12][x][3] and img[11][x][3]: pass
    for x in range(W):                                # contorno de baixo do toldo
        for y in range(12, 14):
            if img[y][x][3] == 0 and img[y - 1][x][3] and img[y - 1][x] != OUTL:
                img[y][x] = OUTL
                break
    # Placa pendurada
    for y in range(14, 19):
        for x in range(19, 29):
            img[y][x] = OUTL if y in (14, 18) or x in (19, 28) else LIGHT
    for x in (21, 23, 25): img[16][x] = DARK
    return img


save('Props', 'tenda_lado', side_tent())
stall = side_stall()
# Mercadorias pequenas em cima do balcão: peixes, garrafas de rum e um barrilzinho
GOODS = {'o': (20, 27, 27, 255), 'f': (113, 200, 230, 255), 'F': (190, 236, 250, 255), 'g': (60, 150, 90, 255),
         'G': (120, 200, 120, 255), 'c': (163, 117, 78, 255), 'b': (215, 139, 74, 255), 'B': (145, 82, 44, 255), '.': None}
FISH = ['.oooo.o', 'oFFfoof', 'offffof', '.oooo.o']
BOTTLE = ['.oo.', '.cc.', 'oggo', 'oGgo', 'oggo', 'oggo', 'oooo']
BARREL = ['.oooo.', 'obbbbo', 'oBBBBo', 'obbbbo', 'oBBBBo', 'obbbbo', '.oooo.']
def stamp(dst, art, ox, oy):
    for y, row in enumerate(art):
        for x, ch in enumerate(row):
            c = GOODS[ch]
            if c: dst[oy + y][ox + x] = c
stamp(stall, FISH, 8, 21)
stamp(stall, FISH, 9, 18)
stamp(stall, BOTTLE, 30, 18)
stamp(stall, BOTTLE, 35, 18)
stamp(stall, BARREL, 18, 18)
save('Props', 'barraca_lado', stall)
print('construcoes ok')
