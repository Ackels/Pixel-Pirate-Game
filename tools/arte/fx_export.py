"""Animações de combate e de vida no mar (quadros soltos) para Game/Assets/Art/Mar.

- Fx/explosao_<n>, clarao_<n> (fogo na boca do canhão, apontando para a esquerda), respingo_<n>,
  fumaca_<n>, lasca_<n> (pedaços de madeira), brilho_<n> (reflexo na água), dano_<n> (rachaduras no casco).
- Navio/canhao_tiro_<n>: o canhão recuando ao atirar (mesmo recorte do bloco canhao, apontando para a direita).
- Navio/flamula_<n>: bloco Bandeira tremulando (ponta do mastro + pano; 48x32, pivô no mastro).
- Navio/bandeira_pirata_<n>, bandeira_mercante_<n>: bandeiras dos navios inimigos tremulando.

Uso: python -P tools/arte/fx_export.py ArteBaixada Game/Assets/Art/Mar
"""
import os, sys, random
sys.path.insert(0, os.path.dirname(__file__))
import png

A, OUT = sys.argv[1], sys.argv[2]
T = A + '/TreasureHunters/Treasure Hunters/'
NF = A + '/NinjaAdventure/Ninja Adventure - Asset Pack/FX/'
CLEAR = (0, 0, 0, 0)


def load(p): return png.read(p)[2]
def save(sub, name, px):
    os.makedirs(f'{OUT}/{sub}', exist_ok=True)
    png.write(f'{OUT}/{sub}/{name}.png', len(px[0]), len(px), px)
def frames(folder): return [load(folder + f) for f in sorted(os.listdir(folder), key=lambda s: int(''.join(c for c in s if c.isdigit()) or 0)) if f.endswith('.png')]
def flip(px): return [r[::-1] for r in px]
def hx(s): return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


SC = T + 'Shooter Traps/Sprites/Cannon/'
for i, f in enumerate(frames(SC + 'Cannon Ball Explosion/'), 1): save('Fx', f'explosao_{i}', f)
for i, f in enumerate(frames(SC + 'Cannon Fire Effect/'), 1): save('Fx', f'clarao_{i}', f)
for i, f in enumerate(frames(T + 'Merchant Ship/Sprites/Water/Water Splash 1/'), 1): save('Fx', f'respingo_{i}', f)
for i, f in enumerate(frames(T + 'Merchant Ship/Sprites/Water/Reflexes 1/'), 1): save('Fx', f'brilho_{i}', f)

smoke = load(NF + 'Smoke/Smoke/SpriteSheet.png')
for i in range(len(smoke[0]) // 32): save('Fx', f'fumaca_{i + 1}', [r[i * 32:(i + 1) * 32] for r in smoke])
wood = load(NF + 'Particle/Wood.png')
for i in range(len(wood[0]) // 16): save('Fx', f'lasca_{i + 1}', [r[i * 16:(i + 1) * 16] for r in wood])

# O canhão recuando: mesmo recorte do bloco "canhao" (mar_export.py), virado para a direita
for i, f in enumerate(frames(SC + 'Cannon Fire/'), 1):
    c32 = [[CLEAR] * 32 for _ in range(32)]
    for y in range(26):
        for x in range(30):
            p = f[y][5 + x] if 5 + x < len(f[0]) else CLEAR
            if p[3]: c32[6 + y][1 + x] = p
    save('Navio', f'canhao_tiro_{i}', flip(c32))

# Rachaduras de dano (por cima dos blocos atingidos): buraco escuro com farpas, 3 variações
OUTL, DK, HOLE = hx('33323d'), hx('5e3a3a'), hx('1c1820')
rng = random.Random(7)
for v in range(1, 4):
    t = [[CLEAR] * 32 for _ in range(32)]
    cx, cy = rng.randint(10, 21), rng.randint(10, 21)
    for y in range(32):
        for x in range(32):
            d = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5
            if d < 3.2: t[y][x] = HOLE
            elif d < 4.6: t[y][x] = OUTL
    for _ in range(5):   # rachaduras saindo do buraco
        x, y = float(cx), float(cy)
        dx, dy = rng.uniform(-1, 1), rng.uniform(-1, 1)
        for _ in range(rng.randint(5, 10)):
            x += dx + rng.uniform(-0.4, 0.4); y += dy + rng.uniform(-0.4, 0.4)
            ix, iy = int(x), int(y)
            if 0 <= ix < 32 and 0 <= iy < 32 and t[iy][ix][3] == 0: t[iy][ix] = DK
    save('Fx', f'dano_{v}', t)

# Bandeira tremulando
FLAG = T + 'Palm Tree Island/Sprites/Objects/Flag/'
flags = [load(FLAG + f'Flag 0{i}.png') for i in range(1, 10)]
mast = load(f'{OUT}/Navio/mastro.png')
GOLD, GOLD_DK = hx('f0c050'), hx('b88a30')


def trimmed(px):
    ys = [y for y, r in enumerate(px) if any(c[3] for c in r)]
    xs = [x for x in range(len(px[0])) if any(r[x][3] for r in px)]
    return [r[min(xs):max(xs) + 1] for r in px[min(ys):max(ys) + 1]]


first = trimmed([r[:] for r in flags[0][:30]])
cols = [sum(1 for y in range(len(first)) if first[y][x][3]) for x in range(len(first[0]))]
pole_x = max(range(len(cols)), key=lambda x: cols[x])
for i, fl_src in enumerate(flags, 1):
    fl = [[CLEAR] * 48 for _ in range(32)]
    for y in range(4, 32):
        for x in range(32): fl[y][x] = mast[y][x]
    for y, (x0, x1) in enumerate([(14, 17), (13, 18), (13, 18), (14, 17)]):
        for x in range(x0, x1 + 1):
            fl[y][x] = OUTL if x in (x0, x1) or y in (0, 3) else (GOLD if x < 16 else GOLD_DK)
    cloth = trimmed([r[:] for r in fl_src[:30]])
    pano = trimmed([r[pole_x + 2:] for r in cloth])
    for y, row in enumerate(pano):
        for x, c in enumerate(row):
            if c[3] and 4 + y < 32 and 20 + x < 48: fl[4 + y][20 + x] = c
    for y in range(4, min(32, 4 + len(pano))):
        if fl[y][20][3]: fl[y][20] = OUTL
    save('Navio', f'flamula_{i}', fl)

    # Bandeiras inteiras dos navios inimigos (com o mastrinho, pivô embaixo)
    save('Navio', f'bandeira_pirata_{i}', fl_src)
    m = {hx(k)[:3]: hx(v)[:3] for k, v in {'7b7bab': '5f8fd0', '68538a': '3f62a0', 'ffffff': '5f8fd0'}.items()}
    save('Navio', f'bandeira_mercante_{i}', [[(m[p[:3]] + (p[3],)) if p[3] and p[:3] in m else p for p in r] for r in fl_src])
# ---------- Céu: sol, lua, estrela e gota de chuva (desenhados à mão, 32 px = 1 bloco) ----------
import math


def disc(size, fill, edge, r_in, glow=None, glow_r=0):
    img = [[CLEAR] * size for _ in range(size)]
    c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - c, y - c)
            if d <= r_in - 1: img[y][x] = fill(x, y, d)
            elif d <= r_in: img[y][x] = edge
            elif glow and d <= glow_r:
                a = int(120 * (1 - (d - r_in) / (glow_r - r_in)) ** 1.5)
                img[y][x] = glow[:3] + (a,)
    return img


SUN, SUN_HI, SUN_EDGE = hx('ffe08a'), hx('fff4c4'), hx('f0b84a')
save('Ceu', 'sol', disc(64, lambda x, y, d: SUN_HI if d < 13 else SUN, SUN_EDGE, 20, (255, 236, 160), 31))
MOON, MOON_DK, MOON_EDGE = hx('e8ecf4'), hx('bcc4d8'), hx('9aa4c0')
craters = [(20, 22, 3.2), (30, 31, 2.4), (24, 34, 1.8), (35, 22, 1.6)]
def moon_fill(x, y, d):
    for cx, cy, r in craters:
        if math.hypot(x - cx, y - cy) <= r: return MOON_DK
    return MOON
save('Ceu', 'lua', disc(56, moon_fill, MOON_EDGE, 14, (200, 210, 255), 26))
star = [[CLEAR] * 3 for _ in range(3)]
for (x, y) in ((1, 0), (0, 1), (1, 1), (2, 1), (1, 2)): star[y][x] = (255, 255, 240, 255) if (x, y) == (1, 1) else (200, 210, 255, 200)
save('Ceu', 'estrela', star)
drop = [[CLEAR] * 3 for _ in range(10)]
for i in range(10):          # risco na diagonal (vento da direita para a esquerda)
    x = 2 - i * 2 // 10
    drop[i][x] = (210, 228, 255, 110 + 12 * i)
save('Ceu', 'gota', drop)
print('fx ok')
