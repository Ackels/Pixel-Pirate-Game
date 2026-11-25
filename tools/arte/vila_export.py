"""Materiais, prédios de produção novos e decorações da vila (vista de cima, Ninja Adventure).

- Icons/madeira|pedra|ferro: ícones dos materiais.
- Buildings/lenhador (tenda + pilha de toras), pedreira (rochedo + picareta), fundicao (forno de pedra + barras de ferro).
- Decor/: poste (lanterna), canteiro (borda de pedra com flores), estatua, coluna, estandarte_caveira,
  estandarte_vermelho, vaso, caixotes. A última linha da imagem é o chão (pivô embaixo no centro).

Uso: python -P tools/arte/vila_export.py
"""
import os, sys
sys.path.insert(0, os.path.dirname(__file__))
import png

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
NA = os.path.join(ROOT, 'ArteBaixada', 'NinjaAdventure', 'Ninja Adventure - Asset Pack')
TS = os.path.join(NA, 'Backgrounds', 'Tilesets')
OUT = os.path.join(ROOT, 'Game', 'Assets', 'Art', 'Ninja')
CLEAR = (0, 0, 0, 0)


def load(path): return png.read(path)[2]
def crop(px, x, y, w, h): return [row[x:x + w] for row in px[y:y + h]]
def save(sub, name, px):
    os.makedirs(os.path.join(OUT, sub), exist_ok=True)
    png.write(os.path.join(OUT, sub, name + '.png'), len(px[0]), len(px), px)
def trimmed(px):
    ys = [y for y, r in enumerate(px) if any(c[3] for c in r)]
    xs = [x for x in range(len(px[0])) if any(r[x][3] for r in px)]
    if not ys: return px
    return [r[min(xs):max(xs) + 1] for r in px[min(ys):max(ys) + 1]]
def canvas(w, h): return [[CLEAR] * w for _ in range(h)]
def paste(dst, src, x, y):
    for yy, row in enumerate(src):
        for xx, p in enumerate(row):
            if p[3] and 0 <= y + yy < len(dst) and 0 <= x + xx < len(dst[0]): dst[y + yy][x + xx] = p
def bottom(src, w, h, x):
    """Cola src encostado no chão (última linha) de uma tela w x h, na posição x."""
    c = canvas(w, h)
    paste(c, src, x, h - len(src))
    return c


CA = load(os.path.join(TS, 'tileset_camp.png'))
HO = load(os.path.join(TS, 'TilesetHouse.png'))
EL = load(os.path.join(TS, 'TilesetElement.png'))
NT = load(os.path.join(TS, 'TilesetNature.png'))
def item(*parts): return trimmed(load(os.path.join(NA, 'Items', *parts)))

# ---------- Materiais ----------
save('Icons', 'madeira', trimmed(crop(CA, 0, 0, 32, 16)))
save('Icons', 'pedra', item('Resource', 'Rock.png'))
save('Icons', 'ferro', item('Resource', 'BarIron.png'))

# ---------- Prédios ----------
# Lenhador: tenda de lona com a pilha de toras na frente, à direita
tent = trimmed(crop(CA, 64, 0, 48, 48))
logs = trimmed(crop(CA, 0, 16, 32, 32))
lum = canvas(72, 48)
paste(lum, tent, 0, 48 - len(tent))
paste(lum, logs, 72 - len(logs[0]), 48 - len(logs))   # do lado da tenda (a porta fica livre)
save('Buildings', 'lenhador', lum)

# Pedreira: rochedo grande, pedras soltas e a picareta encostada
rock = trimmed(crop(NT, 192, 80, 64, 48))
quarry = canvas(64, 48)
paste(quarry, rock, (64 - len(rock[0])) // 2, 48 - len(rock))
for (x, y, w, h), (px, py) in (((240, 144, 16, 16), (2, 36)), ((288, 144, 16, 16), (50, 38))):
    small = trimmed(crop(NT, x, y, w, h))
    paste(quarry, small, px, 48 - len(small) - (48 - py - len(small)) // 4)
pick = item('Tool', 'Pickaxe.png')
paste(quarry, pick, 40, 48 - len(pick) - 2)
save('Buildings', 'pedreira', quarry)

# Fundição: forno de pedra cinza com barras de ferro empilhadas do lado
kiln = trimmed(crop(HO, 464, 208, 32, 32))
bar = item('Resource', 'BarIron.png')
foundry = canvas(48, 40)
paste(foundry, kiln, 2, 40 - len(kiln))
paste(foundry, bar, 32, 40 - len(bar))
paste(foundry, bar, 34, 40 - len(bar) - 5)
save('Buildings', 'fundicao', foundry)

# ---------- Decorações ----------
save('Decor', 'poste', trimmed(crop(CA, 96, 80, 16, 32)))
save('Decor', 'estatua', trimmed(crop(HO, 16, 240, 32, 48)))
save('Decor', 'coluna', trimmed(crop(HO, 0, 240, 16, 48)))
save('Decor', 'estandarte_caveira', trimmed(crop(HO, 448, 320, 16, 32)))
save('Decor', 'estandarte_vermelho', trimmed(crop(HO, 368, 320, 16, 32)))
save('Decor', 'vaso', trimmed(crop(EL, 0, 112, 16, 32)))
barrel = trimmed(crop(EL, 0, 0, 16, 16))
barrels = canvas(32, 16)
paste(barrels, barrel, 0, 16 - len(barrel))
paste(barrels, barrel, 16, 16 - len(barrel))
save('Decor', 'caixotes', barrels)
# Canteiro: borda de pedra com terra (tileset_camp) e flores do TilesetNature por cima
bed = trimmed(crop(CA, 160, 48, 32, 32))
for i, (fx, fy) in enumerate(((5, 5), (17, 4), (8, 15), (19, 15), (12, 10))):
    flower = trimmed(crop(NT, (i % 4) * 16, 176, 16, 16))
    paste(bed, flower, fx, fy)
save('Decor', 'canteiro', bed)
print('vila ok')
