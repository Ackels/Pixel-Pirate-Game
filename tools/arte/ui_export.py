"""Peças da interface (HUD) no estilo pirata: madeira e papel do Treasure Hunters + ícones do Ninja Adventure.

Saída em Game/Assets/Resources/UI (carregadas por UIKit.Skin). As bordas de 9 fatias ficam no
importador (Editor/UISkinImporter.cs). Uso: python -P tools/arte/ui_export.py
"""
import os, sys
sys.path.insert(0, os.path.dirname(__file__))
import png

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
WP = os.path.join(ROOT, 'ArteBaixada', 'TreasureHunters', 'Treasure Hunters', 'Wood and Paper UI', 'Sprites')
NI = os.path.join(ROOT, 'ArteBaixada', 'NinjaAdventure', 'Ninja Adventure - Asset Pack', 'Items')
OUT = os.path.join(ROOT, 'Game', 'Assets', 'Resources', 'UI')
os.makedirs(OUT, exist_ok=True)
CLEAR = (0, 0, 0, 0)


def load(*parts): return png.read(os.path.join(*parts))[2]
def crop(px, x, y, w, h): return [row[x:x + w] for row in px[y:y + h]]


def save(name, px):
    png.write(os.path.join(OUT, name + '.png'), len(px[0]), len(px), px)
    print('ok', name, len(px[0]), 'x', len(px))


def scale(c, k): return (int(c[0] * k), int(c[1] * k), int(c[2] * k), c[3])


# --- Moldura de madeira (9 fatias): cantos do quadro pronto, bordas de uma coluna/linha limpa, miolo vazio ---
board = load(WP, 'Prefabs', '4.png')          # quadro 64x64 com moldura fina
L, T = 6, 8                                     # espessura da moldura: lados 6, topo/base 8
x0, x1, y0, y1 = 5, 58, 5, 58                   # contorno do quadro dentro da imagem
frame = [[CLEAR] * (L * 2 + 1) for _ in range(T * 2 + 1)]
cols = list(range(x0, x0 + L)) + [15] + list(range(x1 - L + 1, x1 + 1))
rows = list(range(y0, y0 + T)) + [30] + list(range(y1 - T + 1, y1 + 1))
for j, y in enumerate(rows):
    for i, x in enumerate(cols):
        if i == L and j == T: continue           # miolo transparente (o fundo de tábuas aparece)
        frame[j][i] = board[y][x]
save('moldura', frame)

# --- Fundo de tábuas (repete em mosaico): peça central do quadro, escurecida para o texto claro ler bem ---
planks = load(WP, 'Yellow Board', '5.png')
save('tabuas', [[scale(c, 0.55) if c[3] else c for c in row] for row in planks])

# --- Botões (9 fatias, bordas 3/3/3/5): amarelo normal e verde para "selecionado" ---
save('botao', load(WP, 'Yellow Button', '1.png'))
save('botao_verde', load(WP, 'Green Button', '1.png'))

# --- Papel (cartões e dicas) ---
paper = load(WP, 'Prefabs', '7.png')
save('papel', crop(paper, 13, 15, 39, 34))


# --- Ícones da barra de baixo (16x16) ---
def canvas(w=16, h=16): return [[CLEAR] * w for _ in range(h)]


def put(dst, src, ox, oy):
    for y, row in enumerate(src):
        for x, c in enumerate(row):
            if c[3] and 0 <= oy + y < len(dst) and 0 <= ox + x < len(dst[0]): dst[oy + y][ox + x] = c
    return dst


save('icone_martelo', load(NI, 'Tool', 'Hammer.png'))
save('icone_bomba', put(canvas(), load(NI, 'Projectile', 'Bomb.png'), 2, 2))
save('icone_espada', put(canvas(16, 17), load(NI, 'Weapons', 'Sword', 'Sprite.png'), 5, 0))
save('icone_livro', load(NI, 'Object', 'Book.png'))
save('icone_pergaminho', put(canvas(), load(NI, 'Scroll', 'Scroll.png'), 1, 1))

# Menu: três tábuas (desenhado à mão com as cores do pacote)
OUTL, TAN, HI, SH = (51, 50, 61, 255), (218, 171, 118, 255), (235, 217, 161, 255), (183, 115, 87, 255)
menu = canvas(14, 13)
for y0_ in (0, 5, 10):
    for x in range(14):
        menu[y0_][x] = OUTL; menu[y0_ + 2][x] = OUTL
        menu[y0_ + 1][x] = OUTL if x in (0, 13) else (HI if x < 4 else TAN)
save('icone_menu', menu)

# Âncora (ícone do ofício Marujo): forma em metal com contorno escuro automático
ANCHOR = [
    '....###....',
    '....#.#....',
    '....###....',
    '..#######..',
    '.....#.....',
    '.....#.....',
    '.....#.....',
    '.....#.....',
    '##...#...##',
    '.#...#...#.',
    '.##..#..##.',
    '..##.#.##..',
    '...#####...',
]
METAL, METAL_HI = (150, 162, 178, 255), (205, 214, 226, 255)
h_, w_ = len(ANCHOR) + 2, len(ANCHOR[0]) + 2
anchor = canvas(w_, h_)
shape = {(x + 1, y + 1) for y, row in enumerate(ANCHOR) for x, ch in enumerate(row) if ch == '#'}
for (x, y) in shape:
    anchor[y][x] = METAL_HI if (x - 1, y) not in shape and (x, y - 1) not in shape else METAL
for y in range(h_):
    for x in range(w_):
        if (x, y) in shape: continue
        if any((x + dx, y + dy) in shape for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))): anchor[y][x] = OUTL
save('icone_ancora', anchor)


# --- Ícones da barra de comandos da tripulação (mover, interagir, reunir, cavar) ---
def outlined(rows, fill, hi):
    h_, w_ = len(rows) + 2, len(rows[0]) + 2
    img = canvas(w_, h_)
    shape = {(x + 1, y + 1) for y, row in enumerate(rows) for x, ch in enumerate(row) if ch == '#'}
    for (x, y) in shape:
        img[y][x] = hi if (x, y - 1) not in shape else fill
    for y in range(h_):
        for x in range(w_):
            if (x, y) not in shape and any((x + dx, y + dy) in shape for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                img[y][x] = OUTL
    return img


ARROW = [
    '.......#.....',
    '.......##....',
    '.......###...',
    '############.',
    '#############',
    '############.',
    '.......###...',
    '.......##....',
    '.......#.....',
]
save('icone_mover', outlined(ARROW, (240, 196, 92, 255), (255, 236, 160, 255)))
save('icone_mao', put(canvas(), load(NI, 'Action', 'Interact.png'), 0, 0))
save('icone_pa', load(NI, 'Tool', 'Shovel.png'))
FL = os.path.join(ROOT, 'ArteBaixada', 'NinjaAdventure', 'Ninja Adventure - Asset Pack', 'Backgrounds', 'Animated', 'Flag')
save('icone_bandeira', crop(load(FL, 'FlagRed16x16.png'), 0, 0, 16, 16))
