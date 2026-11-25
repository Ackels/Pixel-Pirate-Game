"""Construções das ilhas em visão lateral (como no Pixel Piracy): peças para Game/Assets/Art/Mar/Casa (32 px = 1 bloco).

- parede_fundo: parede do fundo (tijolos rosados do Treasure Hunters, escurecidos: é o lado de dentro).
- pilar, pilar_topo, pilar_base: colunas de tijolo das laterais; viga, viga_esq, viga_dir: viga de cima.
- telhado, telhado_esq, telhado_dir: telhas desenhadas à mão com a paleta do pacote (32x24, a ponta é inclinada).
- Móveis: mesa, cadeira, janela, barril, garrafa_azul/verde/vermelha, caveira (Pirate Bomb); barril_deitado,
  barril_frente, garrafas_1..3, vela_1..6 (Treasure Hunters); prateleira (plataforma do Treasure Hunters).
- Coisas pequenas do Ninja Adventure (16 px = meio bloco): comida_peixe/carne/bife/camarao, arma_espada/machado/lanca.

Uso: python -P tools/arte/casa_export.py
"""
import os, sys
sys.path.insert(0, os.path.dirname(__file__))
import png

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
A = os.path.join(ROOT, 'ArteBaixada')
TH = os.path.join(A, 'TreasureHunters', 'Treasure Hunters')
PB = os.path.join(A, 'PirateBomb', 'Sprites', '7-Objects', '12-Other Objects')
NI = os.path.join(A, 'NinjaAdventure', 'Ninja Adventure - Asset Pack', 'Items')
OUT = os.path.join(ROOT, 'Game', 'Assets', 'Art', 'Mar', 'Casa')
os.makedirs(OUT, exist_ok=True)
CLEAR = (0, 0, 0, 0)


def load(*p): return png.read(os.path.join(*p))[2]
def crop(px, x, y, w, h): return [row[x:x + w] for row in px[y:y + h]]
def save(name, px):
    png.write(os.path.join(OUT, name + '.png'), len(px[0]), len(px), px)
    print('ok', name, len(px[0]), 'x', len(px))
def shade(px, k): return [[(int(c[0] * k), int(c[1] * k), int(c[2] * k), c[3]) if c[3] else c for c in r] for r in px]
def trimmed(px):
    ys = [y for y, r in enumerate(px) if any(c[3] for c in r)]
    xs = [x for x in range(len(px[0])) if any(r[x][3] for r in px)]
    return [r[min(xs):max(xs) + 1] for r in px[min(ys):max(ys) + 1]]


# --- Paredes do Treasure Hunters (tileset 32x32 com parede da frente e do fundo) ---
tiles = load(TH, 'Pirate Ship', 'Sprites', 'Tilesets', 'Terrain and Back Wall (32x32).png')
save('parede_fundo', shade(crop(tiles, 64, 256, 32, 32), 0.72))
save('pilar_topo', crop(tiles, 160, 32, 32, 32))
save('pilar', crop(tiles, 160, 64, 32, 32))
save('pilar_base', crop(tiles, 160, 96, 32, 32))
save('viga_esq', crop(tiles, 32, 160, 32, 32))
save('viga', crop(tiles, 64, 160, 32, 32))
save('viga_dir', crop(tiles, 96, 160, 32, 32))

# --- Telhado (telhas em escama) ---
OUTL, DK, MID, HI, RIDGE = (51, 30, 30, 255), (110, 40, 34, 255), (156, 59, 43, 255), (196, 85, 58, 255), (90, 50, 40, 255)


def roof(kind):
    W, H = 32, 24
    img = [[CLEAR] * W for _ in range(H)]
    for y in range(H):
        for x in range(W):
            # ponta inclinada: a telha "corta" em diagonal nas extremidades
            if kind == 'esq' and x < (H - 1 - y): continue
            if kind == 'dir' and x > W - 1 - (H - 1 - y): continue
            if y < 3: img[y][x] = RIDGE if y > 0 else OUTL          # cumeeira
            else:
                row = (y - 3) // 5
                yy = (y - 3) % 5
                xx = (x + (row % 2) * 4) % 8
                if yy == 4 or (yy == 3 and xx in (0, 7)): img[y][x] = DK   # borda de baixo da escama
                elif xx == 0: img[y][x] = DK
                elif yy == 0: img[y][x] = HI
                else: img[y][x] = MID
            if y == H - 1: img[y][x] = OUTL                           # beiral
    # contorno na diagonal
    for y in range(H):
        for x in range(W):
            if img[y][x][3] and ((kind == 'esq' and (x == 0 or not img[y][x - 1][3])) or (kind == 'dir' and (x == W - 1 or not img[y][x + 1][3]))):
                img[y][x] = OUTL
    return img


save('telhado', roof('meio'))
save('telhado_esq', roof('esq'))
save('telhado_dir', roof('dir'))

# --- Móveis ---
save('mesa', load(PB, 'Table.png'))
save('cadeira', load(PB, 'Chair.png'))
save('janela', load(PB, 'Windows.png'))
save('barril', load(PB, 'Barrel.png'))
save('garrafa_azul', load(PB, 'Blue Bottle.png'))
save('garrafa_verde', load(PB, 'Green Bottle.png'))
save('garrafa_vermelha', load(PB, 'Red Bottle.png'))
save('caveira', load(PB, 'Skull.png'))
BB = os.path.join(TH, 'Pirate Ship', 'Sprites', 'Decorations', 'Barrels and Bottles')
save('barril_deitado', trimmed(load(BB, '01.png')))
save('barril_frente', trimmed(load(BB, '02.png')))
for i, f in enumerate(('03.png', '04.png', '05.png'), 1): save(f'garrafas_{i}', trimmed(load(BB, f)))
CD = os.path.join(TH, 'Pirate Ship', 'Sprites', 'Decorations', 'Candle', 'Candle')
for i in range(1, 7): save(f'vela_{i}', load(CD, f'0{i}.png'))
plat = load(TH, 'Pirate Ship', 'Sprites', 'Tilesets', 'Platforms (32x32).png')
save('prateleira', trimmed(crop(plat, 0, 32, 96, 32)))

# --- Coisas pequenas (Ninja Adventure, 16 px) ---
for name, f in (('peixe', 'Fish.png'), ('carne', 'Meat.png'), ('bife', 'Beaf.png'), ('camarao', 'Shrimp.png')):
    save('comida_' + name, load(NI, 'Food', f))
W = os.path.join(NI, 'Weapons')
for name, d in (('espada', 'Sword'), ('machado', 'Axe'), ('lanca', 'Lance')):
    p = os.path.join(W, d, 'Sprite.png')
    if os.path.exists(p): save('arma_' + name, load(p))
print('casa ok')
