"""Exporta a arte do mar e das ilhas (Treasure Hunters, blocos de 32 px) para Game/Assets/Art/Mar.

Uso: python -P mar_export.py <ArteBaixada> <Game/Assets/Art/Mar>

- Ceu/: horizonte, nuvens, água (fundo opaco, superfície e camada translúcida).
- Navio/: blocos do navio (casco, convés, mastro, vela, cabine, canhão), prancha e bandeiras.
- Ilha/: terreno com peças de borda automáticas (terra_<máscara>, areia_<máscara>), coqueiros, baús.
- Bichos/: caranguejo, estrela-do-mar e baleote (quadros de corrida).
- Fx/: bala de canhão, explosão, respingo, poeira, golpe de espada.
"""
import sys, os
sys.path.insert(0, os.path.dirname(__file__))
import png

A, OUT = sys.argv[1], sys.argv[2]
T = A + '/TreasureHunters/Treasure Hunters'
cache = {}


def load(path):
    if path not in cache: cache[path] = png.read(path)
    return cache[path]


def px_of(path): return load(path)[2]


def crop(px, x, y, w, h): return [row[x:x + w] for row in px[y:y + h]]


def save(sub, name, px):
    os.makedirs(f'{OUT}/{sub}', exist_ok=True)
    png.write(f'{OUT}/{sub}/{name}.png', len(px[0]), len(px), px)


def hx(s): return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


def recolor(px, mapping):
    m = {hx(k)[:3]: hx(v)[:3] for k, v in mapping.items()}
    return [[(m[p[:3]] + (p[3],)) if p[3] and p[:3] in m else p for p in row] for row in px]


def flip(px): return [row[::-1] for row in px]


def trim_bottom(frames):
    """Corta as linhas vazias de baixo (iguais em todos os quadros), para o pé ficar no pivô."""
    h = len(frames[0])
    last = max(max((y for y in range(h) if any(p[3] for p in f[y])), default=h - 1) for f in frames)
    return [f[:last + 1] for f in frames]


# ---------- Céu e água ----------
BG = T + '/Palm Tree Island/Sprites/Background/'
save('Ceu', 'horizonte', px_of(BG + 'BG Image.png'))
save('Ceu', 'nuvem_grande', px_of(BG + 'Big Clouds.png'))
for i in range(3): save('Ceu', f'nuvem_{i}', px_of(BG + f'Small Cloud {i + 1}.png'))
save('Ceu', 'agua_fundo', px_of(BG + 'Additional Water.png'))
WT = T + '/Merchant Ship/Sprites/Water/Water/'
save('Ceu', 'agua_topo', px_of(WT + 'Top/' + sorted(os.listdir(WT + 'Top'))[0]))
save('Ceu', 'agua_baixo', px_of(WT + 'Bottom/' + sorted(os.listdir(WT + 'Bottom'))[0]))

# ---------- Navio ----------
OUTL, HI, MID, DK, SEAM, RAIL, RAILHI = map(hx, ['33323d', 'bc997f', 'ae7764', '985252', '833f4c', '94a2bd', 'cae1ed'])
CLEAR = (0, 0, 0, 0)


def tile(): return [[CLEAR] * 32 for _ in range(32)]


def planks(px, y0, y1, offset=0, pal=(HI, MID, DK, SEAM)):
    hi, mid, dk, seam = pal
    for y in range(y0, y1):
        k = (y - y0) % 8
        plank = (y - y0) // 8
        col = hi if k == 0 else mid if k < 6 else dk if k == 6 else seam
        for x in range(32): px[y][x] = col
        if 0 < k < 7:
            sx = (plank * 13 + 7 + offset) % 32
            px[y][sx] = seam
            if k in (2, 4): px[y][(sx + 2) % 32] = OUTL


hull = tile(); planks(hull, 0, 32)
save('Navio', 'casco', hull)
deck = tile()
for x in range(32):
    deck[0][x] = OUTL; deck[1][x] = RAILHI; deck[2][x] = RAIL; deck[3][x] = OUTL
planks(deck, 4, 32, 5)
save('Navio', 'conves', deck)

# Mastro: poste de madeira vertical (repete para cima)
mast = tile()
for y in range(32):
    for x in range(12, 20):
        mast[y][x] = OUTL if x in (12, 19) else HI if x == 13 else DK if x == 18 else MID
    if y % 16 == 8:
        for x in range(13, 19): mast[y][x] = SEAM
save('Navio', 'mastro', mast)

# Vela: lona clara (fica colorida pelo sailTint do navio). Peças que se encaixam na vertical:
# topo (com a verga de madeira), meio, base (barriga curva) e única (topo + base).
import math
CL, CLS, CLD = (240, 232, 214, 255), (222, 208, 182, 255), (196, 178, 150, 255)


def sail_piece(top, bottom):
    t = tile()
    y_end = [31 if not bottom else 25 + int(round(3 * math.sin(math.pi * x / 31))) for x in range(32)]
    for x in range(32):
        for y in range(0, y_end[x] + 1):
            c = CL
            if x in (0, 31) or (bottom and y == y_end[x]): c = OUTL
            elif x in (1, 30) or (bottom and y == y_end[x] - 1): c = CLD
            elif x in (2, 29) or (x * 7 + y // 2) % 13 == 0: c = CLS
            t[y][x] = c
    if top:
        for x in range(32):
            for y in range(0, 5):
                t[y][x] = OUTL if y in (0, 4) or x in (0, 31) else (HI if y == 1 else MID if y < 3 else DK)
    return t


save('Navio', 'vela', sail_piece(False, False))
save('Navio', 'vela_topo', sail_piece(True, False))
save('Navio', 'vela_base', sail_piece(False, True))
save('Navio', 'vela_unica', sail_piece(True, True))

# Cabine: parede de tábuas mais claras com uma janela redonda
LHI, LMID, LDK, LSEAM = hx('d8b89c'), hx('c99a80'), hx('b07868'), hx('9a5c5c')
cabin = tile(); planks(cabin, 0, 32, 3, (LHI, LMID, LDK, LSEAM))
win = px_of(T + '/Pirate Ship/Sprites/Decorations/Window/Window/' + sorted(os.listdir(T + '/Pirate Ship/Sprites/Decorations/Window/Window'))[0])
for y in range(32):
    for x in range(32):
        if win[y][x][3]: cabin[y][x] = win[y][x]
save('Navio', 'cabine', cabin)

# Canhão (o original aponta para a esquerda; o bloco do jogador aponta para a direita)
cannon = px_of(T + '/Shooter Traps/Sprites/Cannon/Cannon Idle/1.png')
c32 = [[CLEAR] * 32 for _ in range(32)]
cx0 = 5
for y in range(26):
    for x in range(30):
        p = cannon[y][cx0 + x]
        if p[3]: c32[6 + y][1 + x] = p
save('Navio', 'canhao', flip(c32))

# Prancha (abordagem / praia) e tábua das docas: miolo da plataforma de madeira
plat = px_of(T + '/Pirate Ship/Sprites/Tilesets/Platforms (32x32).png')
save('Navio', 'prancha', crop(plat, 64, 64, 32, 32))
save('Navio', 'tabua', crop(plat, 64, 32, 32, 32))

# Bandeiras: pirata (original) e mercante (azul, sem caveira)
flag = px_of(T + '/Palm Tree Island/Sprites/Objects/Flag/Flag 01.png')
save('Navio', 'bandeira_pirata', flag)
save('Navio', 'bandeira_mercante', recolor(flag, {'7b7bab': '5f8fd0', '68538a': '3f62a0', 'ffffff': '5f8fd0'}))

# ---------- Ilha: terreno com peças de borda automáticas ----------
TE = px_of(T + '/Palm Tree Island/Sprites/Terrain/Terrain (32x32).png')
SAMPLES = [(16, 4, 1), (27, 4, 2), (27, 16, 4), (27, 27, 8), (16, 27, 16), (4, 27, 32), (4, 16, 64), (4, 4, 128)]


def normalize(m):
    if not (m & 1 and m & 4): m &= ~2
    if not (m & 4 and m & 16): m &= ~8
    if not (m & 16 and m & 64): m &= ~32
    if not (m & 64 and m & 1): m &= ~128
    return m


def fh(m):
    bits = {1: 1, 2: 128, 4: 64, 8: 32, 16: 16, 32: 8, 64: 4, 128: 2}
    return sum(v for k, v in bits.items() if m & k)


def autotiles(name, px, cols, rows):
    found = {}
    for r in range(rows):
        for c in range(cols):
            t = crop(px, c * 32, r * 32, 32, 32)
            if len(t) < 32 or len(t[0]) < 32: continue
            m = 0
            for sx, sy, bit in SAMPLES:
                if t[sy][sx][3] > 0: m |= bit
            if t[16][16][3] == 0 and m == 0: continue
            m = normalize(m)
            if m not in found: found[m] = t
    # terreno só tem espelho horizontal (o de cima tem grama, não dá para virar de ponta-cabeça)
    for m, t in list(found.items()):
        if fh(m) not in found: found[fh(m)] = flip(t)
    for m, t in found.items(): save('Ilha', f'{name}_{m:03d}', t)
    print(name, 'máscaras:', len(found), sorted(found))


# O terreno do Treasure Hunters tem a borda no contorno da peça (não dentro dela), então o mapa é manual:
# (coluna, linha) no tileset de 32 px -> máscara de vizinhos. Cantos internos caem na peça cheia.
TERRAIN_MAP = {
    255: (1, 1), 124: (1, 0), 112: (2, 0), 28: (0, 0), 241: (2, 1), 31: (0, 1),
    199: (1, 2), 7: (0, 2), 193: (2, 2),
    16: (4, 0), 17: (4, 1), 1: (4, 2),
    4: (0, 4), 68: (1, 4), 64: (2, 4), 0: (4, 4),
}


def terrain(name, px):
    for m, (c, r) in TERRAIN_MAP.items(): save('Ilha', f'{name}_{m:03d}', crop(px, c * 32, r * 32, 32, 32))
    print(name, 'peças:', len(TERRAIN_MAP))


terrain('terra', TE)
# Praia: mesmo terreno com a grama virando areia e a pedra puxando para o marrom-areia
SANDMAP = {}
w, h, _ = load(T + '/Palm Tree Island/Sprites/Terrain/Terrain (32x32).png')
greens = sorted({p[:3] for row in TE for p in row if p[3] and p[1] > p[0] + 20 and p[1] > p[2]}, key=lambda c: sum(c))
sands = [(214, 139, 74), (255, 173, 93), (255, 203, 141), (255, 224, 178)]
for i, g in enumerate(greens): SANDMAP[g] = sands[min(len(sands) - 1, i * len(sands) // max(1, len(greens)))]
stones = sorted({p[:3] for row in TE for p in row if p[3] and p[:3] not in SANDMAP and p[:3] != (51, 50, 61)}, key=lambda c: sum(c))
browns = [(120, 78, 62), (150, 98, 70), (178, 124, 86), (204, 152, 104)]
for i, s in enumerate(stones): SANDMAP[s] = browns[min(len(browns) - 1, i * len(browns) // max(1, len(stones)))]
SANDMAP[(51, 50, 61)] = (96, 62, 50)   # interior escuro vira terra marrom
sandpx = [[(SANDMAP[p[:3]] + (p[3],)) if p[3] and p[:3] in SANDMAP else p for p in row] for row in TE]
terrain('areia', sandpx)

# Coqueiros
FP = T + '/Palm Tree Island/Sprites/Front Palm Trees/'
top = px_of(FP + 'Front Palm Tree Top 01.png')
trunkset = px_of(FP + 'Front Palm Bottom and Grass (32x32).png')
trunk, base = crop(trunkset, 0, 0, 32, 32), crop(trunkset, 0, 32, 32, 32)
W, H = 40, 32 + 32 + 26
palm = [[CLEAR] * W for _ in range(H)]


def blit(dst, src, ox, oy):
    for y, row in enumerate(src):
        for x, p in enumerate(row):
            if p[3] and 0 <= oy + y < len(dst) and 0 <= ox + x < len(dst[0]): dst[oy + y][ox + x] = p


blit(palm, base[0:26], 4, H - 26)
blit(palm, trunk, 4, H - 26 - 32)
blit(palm, top, 0, 0)
save('Ilha', 'coqueiro', palm)
BP = T + '/Palm Tree Island/Sprites/Back Palm Trees/'
save('Ilha', 'coqueiro_fundo_0', px_of(BP + 'Back Palm Tree Regular 01.png'))
save('Ilha', 'coqueiro_fundo_1', px_of(BP + 'Back Palm Tree Left 01.png'))
save('Ilha', 'coqueiro_fundo_2', px_of(BP + 'Back Palm Tree Right 01.png'))
save('Ilha', 'grama', trim_bottom([crop(trunkset, 32, 64, 64, 32)])[0])
CH = T + '/Palm Tree Island/Sprites/Objects/Chest/'
save('Ilha', 'bau_fechado', trim_bottom([px_of(CH + 'Chest Close 01.png')])[0])
save('Ilha', 'bau_aberto', trim_bottom([px_of(CH + 'Chest Open 05.png')])[0])

# ---------- Bichos ----------
for name, folder in (('caranguejo', 'Crabby'), ('estrela', 'Pink Star'), ('baleote', 'Fierce Tooth')):
    d = f'{T}/The Crusty Crew/Sprites/{folder}/02-Run'
    frames = trim_bottom([px_of(f'{d}/{f}') for f in sorted(os.listdir(d))])
    for i, f in enumerate(frames): save('Bichos', f'{name}_{i}', f)

# ---------- Efeitos ----------
SC = T + '/Shooter Traps/Sprites/Cannon/'
save('Fx', 'bala', px_of(SC + 'Cannon Ball Idle/1.png'))
save('Fx', 'explosao', px_of(SC + 'Cannon Ball Explosion/1.png'))
save('Fx', 'respingo', px_of(T + '/Merchant Ship/Sprites/Water/Water Splash 1/2.png'))
save('Fx', 'poeira', px_of(T + '/Captain Clown Nose/Sprites/Dust Particles/Jump 02.png'))
save('Fx', 'golpe', px_of(T + '/Captain Clown Nose/Sprites/Captain Clown Nose/Sword Effects/24-Attack 1/Attack 1 02.png'))
print('ok')
