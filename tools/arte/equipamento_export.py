"""Equipamento (armas, pá, chapéus, amuletos) e o prédio da Forja: Game/Assets/Art/Ninja/Equip e Buildings/forja.

- Armas e pá: ícones do Ninja Adventure (Items/Weapons/<Nome>/Sprite.png, Items/Tool/Shovel.png). O cabo fica
  embaixo (o importador põe o pivô embaixo no centro), então o mesmo desenho serve para o boneco segurar no mar.
- Amuletos: joias do Treasure Hunters (Pirate Treasure: rubi, safira, esmeralda, caveira dourada, moeda).
- Chapéus: desenhados aqui, pixel a pixel, na paleta do Ninja Adventure (16x16).
- Forja (vila, vista de cima): forno de tijolos do TilesetHouse + bigorna (Items/Tool/Anvil.png).

Uso: python -P tools/arte/equipamento_export.py
"""
import os, sys
sys.path.insert(0, os.path.dirname(__file__))
import png

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
NA = os.path.join(ROOT, 'ArteBaixada', 'NinjaAdventure', 'Ninja Adventure - Asset Pack')
TH = os.path.join(ROOT, 'ArteBaixada', 'TreasureHunters', 'Treasure Hunters', 'Pirate Treasure', 'Sprites')
OUT = os.path.join(ROOT, 'Game', 'Assets', 'Art', 'Ninja')
CLEAR = (0, 0, 0, 0)


def load(path): return png.read(path)[2]
def crop(px, x, y, w, h): return [row[x:x + w] for row in px[y:y + h]]
def save(sub, name, px):
    os.makedirs(os.path.join(OUT, sub), exist_ok=True)
    png.write(os.path.join(OUT, sub, name + '.png'), len(px[0]), len(px), px)
def main_blob(px):
    """Só o maior pedaço conectado (tira brilhos soltos da animação)."""
    h, w = len(px), len(px[0])
    seen, best = set(), []
    for y in range(h):
        for x in range(w):
            if px[y][x][3] and (x, y) not in seen:
                stack, blob = [(x, y)], []
                seen.add((x, y))
                while stack:
                    cx, cy = stack.pop()
                    blob.append((cx, cy))
                    for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                        if 0 <= nx < w and 0 <= ny < h and px[ny][nx][3] and (nx, ny) not in seen:
                            seen.add((nx, ny)); stack.append((nx, ny))
                if len(blob) > len(best): best = blob
    keep = set(best)
    return [[p if (x, y) in keep else CLEAR for x, p in enumerate(row)] for y, row in enumerate(px)]
def trimmed(px):
    ys = [y for y, r in enumerate(px) if any(c[3] for c in r)]
    xs = [x for x in range(len(px[0])) if any(r[x][3] for r in px)]
    if not ys: return px
    return [r[min(xs):max(xs) + 1] for r in px[min(ys):max(ys) + 1]]


# ---------- Armas e pá ----------
WEAPONS = {
    'porrete': 'Club', 'adaga': 'Katana', 'sabre': 'Sword2', 'florete': 'Rapier', 'machado': 'Axe',
    'marreta': 'Hammer', 'espada_larga': 'BigSword', 'arpao': 'Lance', 'tridente': 'Fork',
}
for name, folder in WEAPONS.items():
    save('Equip', 'arma_' + name, trimmed(load(os.path.join(NA, 'Items', 'Weapons', folder, 'Sprite.png'))))
# Sabre dourado (lendário): o sabre com a lâmina em ouro
sabre = trimmed(load(os.path.join(NA, 'Items', 'Weapons', 'Sword2', 'Sprite.png')))
def gold(p):
    if not p[3]: return p
    r, g, b, a = p
    if abs(r - g) < 30 and abs(g - b) < 30 and r > 110:   # metal (cinza/branco) vira ouro
        v = (r + g + b) / 3 / 255
        return (min(255, int(120 + 135 * v)), min(255, int(80 + 130 * v)), int(20 + 60 * v), a)
    return p
save('Equip', 'arma_sabre_dourado', [[gold(p) for p in row] for row in sabre])
save('Equip', 'pa', trimmed(load(os.path.join(NA, 'Items', 'Tool', 'Shovel.png'))))

# ---------- Amuletos ----------
CHARMS = {'rubi': 'Red Diamond', 'safira': 'Blue Diamond', 'esmeralda': 'Green Diamond',
          'caveira_dourada': 'Golden Skull', 'moeda_sorte': 'Gold Coin'}
for name, folder in CHARMS.items():
    save('Equip', 'amuleto_' + name, trimmed(main_blob(load(os.path.join(TH, folder, '01.png')))))

# ---------- Chapéus (16x16, desenhados) ----------
O = (20, 16, 24, 255)          # contorno
PAL = {
    '.': CLEAR, 'o': O,
    'r': (190, 50, 50, 255), 'R': (230, 90, 80, 255), 'd': (120, 30, 40, 255), 'w': (240, 230, 220, 255),
    'b': (130, 80, 45, 255), 'B': (175, 115, 65, 255), 'k': (85, 50, 30, 255),
    'n': (45, 40, 55, 255), 'N': (80, 72, 95, 255), 'g': (235, 190, 60, 255), 'G': (180, 130, 40, 255),
    'u': (40, 60, 120, 255), 'U': (70, 100, 170, 255), 'f': (250, 250, 245, 255), 'F': (200, 205, 215, 255),
}
HATS = {
    'bandana': [
        '................',
        '................',
        '................',
        '.....oooooo.....',
        '...ooRRRRRRoo...',
        '..oRRwRRRRwRRo..',
        '..oRRRRRwRRRRo..',
        '.oRwRRRRRRRRwRo.',
        '.orrrrrrrrrrrro.',
        '.odddddddddddoRo',
        '..ooooooooooorro',
        '............oro.',
        '.............o..',
        '................',
        '................',
        '................',
    ],
    'chapeu_couro': [
        '................',
        '................',
        '................',
        '.....oooooo.....',
        '....oBBBBBBo....',
        '...oBBBBBBBBo...',
        '...oBBBBBBBBo...',
        '...okkkkkkkko...',
        '..ooBBBBBBBBoo..',
        '.oBBBBBBBBBBBBo.',
        'obbbbbbbbbbbbbbo',
        '.oooooooooooooo.',
        '................',
        '................',
        '................',
        '................',
    ],
    'tricornio': [
        '................',
        '................',
        '................',
        '................',
        '......oooo......',
        '....ooNNNNoo....',
        '...oNNNNNNNNo...',
        '.oonnnnnnnnnnoo.',
        'oNNoonnnnnnooNNo',
        'oggNNooooooNNggo',
        '.oGggggggggggGo.',
        '..oooooooooooo..',
        '................',
        '................',
        '................',
        '................',
    ],
    'tricornio_almirante': [
        '................',
        '..........oo....',
        '.........oFfo...',
        '........oFffo...',
        '......ooFffo....',
        '....ooUUoFo.....',
        '...oUUUUUoUo....',
        '.oouuuuuuuuuoo..',
        'oUUoouuuuuuooUUo',
        'oggUUooooooUUggo',
        '.oGggggggggggGo.',
        '..oooooooooooo..',
        '................',
        '................',
        '................',
        '................',
    ],
}
for name, rows in HATS.items():
    img = [[PAL[ch] for ch in (row + '................')[:16]] for row in rows]
    save('Equip', 'chapeu_' + name, trimmed(img))

# ---------- Forja (vila): forno de tijolos + bigorna ----------
HO = load(os.path.join(NA, 'Backgrounds', 'Tilesets', 'TilesetHouse.png'))
furnace = crop(HO, 29 * 16, 4 * 16, 48, 64)
anvil = trimmed(load(os.path.join(NA, 'Items', 'Tool', 'Anvil.png')))
W, H = 64, 64
forge = [[CLEAR] * W for _ in range(H)]
for y in range(64):
    for x in range(48):
        if furnace[y][x][3]: forge[y][x] = furnace[y][x]
ax, ay = 48 + (16 - len(anvil[0])) // 2, H - len(anvil) - 1
for y, row in enumerate(anvil):
    for x, p in enumerate(row):
        if p[3]: forge[ay + y][ax + x] = p
save('Buildings', 'forja', forge)
print('equipamento ok')
