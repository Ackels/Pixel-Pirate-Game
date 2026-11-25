"""Natureza da ilha da vila (vista de cima), do Ninja Adventure: Game/Assets/Art/Ninja/Natureza e Tiles.

- Natureza/: árvores (redondas, grandes, bosques), rochas e pedras (marrons e cinzas, de vários tamanhos),
  tocos, arbustos (com e sem frutinhas), flores e capim. Recortadas da grade de 16 px do TilesetNature,
  com a borda transparente aparada (o pé fica embaixo: o importador põe o pivô embaixo no centro).
- Natureza/sombra_p|m|g: sombras macias (elipses) para pôr embaixo das coisas.
- Natureza/detalhe_N: tufinhos e florzinhas do chão (TilesetFloorDetail), só enfeite.
- Tiles/grama_N: grama verde mais escura (metade direita do TilesetFloor), mais "tropical".
- Auto/path2_*: caminho de terra com borda nessa grama mais escura.

Uso: python -P tools/arte/natureza_export.py
"""
import os, sys
sys.path.insert(0, os.path.dirname(__file__))
import png

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
TS = os.path.join(ROOT, 'ArteBaixada', 'NinjaAdventure', 'Ninja Adventure - Asset Pack', 'Backgrounds', 'Tilesets')
OUT = os.path.join(ROOT, 'Game', 'Assets', 'Art', 'Ninja')
CLEAR = (0, 0, 0, 0)


def load(name): return png.read(os.path.join(TS, name))[2]
def crop(px, x, y, w, h): return [row[x:x + w] for row in px[y:y + h]]
def save(sub, name, px):
    os.makedirs(os.path.join(OUT, sub), exist_ok=True)
    png.write(os.path.join(OUT, sub, name + '.png'), len(px[0]), len(px), px)
def trimmed(px):
    ys = [y for y, r in enumerate(px) if any(c[3] for c in r)]
    xs = [x for x in range(len(px[0])) if any(r[x][3] for r in px)]
    if not ys: return px
    return [r[min(xs):max(xs) + 1] for r in px[min(ys):max(ys) + 1]]


NA = load('TilesetNature.png')
PIECES = {
    # árvores
    'arvore_redonda_0': (0, 0, 32, 32), 'arvore_redonda_1': (256, 0, 32, 32), 'arvore_redonda_2': (288, 0, 32, 32),
    'arvore_pequena': (96, 128, 32, 32),
    'arvore_grande_0': (48, 288, 48, 48), 'arvore_grande_1': (144, 288, 48, 48), 'arvore_grande_2': (0, 288, 48, 48),
    'bosque_0': (64, 32, 64, 48), 'bosque_1': (256, 32, 64, 48), 'bosque_2': (320, 32, 64, 48),
    'pinheiro': (32, 0, 32, 32),
    # rochas grandes e pedras
    'rochedo_0': (192, 80, 64, 48), 'rochedo_1': (256, 80, 64, 48),
    'rocha_0': (240, 160, 32, 32), 'rocha_1': (272, 160, 64, 48), 'rocha_2': (240, 224, 32, 32), 'rocha_3': (272, 224, 64, 48), 'rocha_4': (336, 160, 32, 48), 'rocha_5': (336, 224, 32, 48),
    'pedra_m_0': (208, 128, 32, 32), 'pedra_m_1': (256, 128, 32, 32), 'pedra_m_2': (240, 192, 32, 32), 'pedra_m_3': (240, 256, 32, 32),
    'pedra_p_0': (240, 144, 16, 16), 'pedra_p_1': (288, 144, 16, 16), 'pedra_p_2': (272, 208, 16, 16), 'pedra_p_3': (288, 208, 16, 16),
    'pedra_p_4': (272, 272, 16, 16), 'pedra_p_5': (288, 272, 16, 16),
    # tocos e arbustos
    'toco_0': (0, 128, 32, 32), 'toco_1': (32, 128, 32, 32),
    'arbusto_0': (0, 160, 16, 16), 'arbusto_1': (16, 160, 16, 16), 'arbusto_2': (32, 160, 16, 16), 'arbusto_3': (48, 160, 16, 16),
    'frutinhas_0': (112, 224, 32, 32), 'frutinhas_1': (144, 224, 32, 32), 'frutinhas_2': (112, 256, 32, 32),
    # flores e capim (enfeite, não bloqueiam)
    'flor_0': (0, 176, 16, 16), 'flor_1': (16, 176, 16, 16), 'flor_2': (32, 176, 16, 16), 'flor_3': (48, 176, 16, 16),
    'capim_0': (64, 160, 16, 16), 'capim_1': (80, 160, 16, 16), 'capim_2': (96, 160, 16, 16), 'capim_3': (64, 176, 16, 16),
    'capim_4': (80, 176, 16, 16), 'capim_5': (112, 176, 16, 16),
}
for name, (x, y, w, h) in PIECES.items():
    save('Natureza', name, trimmed(crop(NA, x, y, w, h)))

# Sombras macias (preto translúcido, mais forte no meio)
def shadow(w, h, alpha):
    img = [[CLEAR] * w for _ in range(h)]
    cx, cy = (w - 1) / 2, (h - 1) / 2
    for y in range(h):
        for x in range(w):
            d = ((x - cx) / (w / 2)) ** 2 + ((y - cy) / (h / 2)) ** 2
            if d < 1: img[y][x] = (20, 30, 25, int(alpha * (1 - d) ** 0.6))
    return img
save('Natureza', 'sombra_p', shadow(14, 6, 90))
save('Natureza', 'sombra_m', shadow(26, 10, 95))
save('Natureza', 'sombra_g', shadow(44, 16, 100))

# Enfeites do chão (tufinhos, florzinhas verdes)
FD = load('TilesetFloorDetail.png')
for i in range(9): save('Natureza', f'detalhe_{i}', trimmed(crop(FD, i * 16, 32, 16, 16)))

# Grama mais escura (metade direita do TilesetFloor, mesma linha da grama clara)
FL = load('TilesetFloor.png')
for i in range(5): save('Tiles', f'grama_{i}', crop(FL, 176 + i * 16, 12 * 16, 16, 16))
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


# Caminho de terra com borda na grama escura (mesmas peças do caminho, metade direita do TilesetFloor)
autotiles('path2', FL, 11, 5, 176, 7 * 16, lambda p: p[3] > 0 and p[0] - p[1] > 30)
print('natureza ok')

