"""Peças do estaleiro (visão lateral, 32 px, paleta do Treasure Hunters) para Game/Assets/Art/Mar/Navio.

- casco_<cor>_<forma>: cheio, rampa (triângulo), curvo (quarto de círculo), meio (meio bloco); cores natural,
  escuro e vermelho. casco_<cor>_cheio_topo = com o friso claro em cima (quando nada sólido está em cima).
  As formas são desenhadas na orientação 0 (sólido embaixo à direita, ◢); o jogo espelha para as outras.
- vela_<estilo>_<máscara>: velas que se emendam com as vizinhas iguais (máscara: 1=cima 2=baixo 4=esq 8=dir).
- piso (plataforma vazada), escada, parede (fundo), parede_janela, parede_rede (beliche).
- bandeira, lanterna, barril, caixote, ancora, timao, amurada.

Uso: python -P tools/arte/navio_export.py ArteBaixada Game/Assets/Art/Mar
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import png

A, OUT = sys.argv[1], sys.argv[2]
T = A + '/TreasureHunters/Treasure Hunters'
CLEAR = (0, 0, 0, 0)


def hx(s): return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)
def tile(): return [[CLEAR] * 32 for _ in range(32)]
def load(p): return png.read(p)[2]
def save(name, px):
    os.makedirs(f'{OUT}/Navio', exist_ok=True)
    png.write(f'{OUT}/Navio/{name}.png', len(px[0]), len(px), px)


def paste(dst, src, ox, oy):
    for y, row in enumerate(src):
        for x, c in enumerate(row):
            if c[3] and 0 <= oy + y < 32 and 0 <= ox + x < 32: dst[oy + y][ox + x] = c
    return dst


def trimmed(px):
    ys = [y for y, r in enumerate(px) if any(c[3] for c in r)]
    xs = [x for x in range(len(px[0])) if any(r[x][3] for r in px)]
    return [r[min(xs):max(xs) + 1] for r in px[min(ys):max(ys) + 1]]


def bottom_center(src):
    """Cola um enfeite em pé, centrado, encostado na base da célula."""
    s = trimmed(src)
    return paste(tile(), s, (32 - len(s[0])) // 2, 32 - len(s))


OUTL = hx('33323d')
RAIL, RAILHI = hx('94a2bd'), hx('cae1ed')
WOODS = {
    'natural': (hx('bc997f'), hx('ae7764'), hx('985252'), hx('833f4c')),
    'escuro': (hx('8c6c5c'), hx('76584c'), hx('5e4440'), hx('4a3236')),
    'vermelho': (hx('d06a58'), hx('b44c42'), hx('923a38'), hx('742c30')),
}


def planks(pal, offset=0):
    hi, mid, dk, seam = pal
    t = tile()
    for y in range(32):
        k, plank = y % 8, y // 8
        col = hi if k == 0 else mid if k < 6 else dk if k == 6 else seam
        for x in range(32): t[y][x] = col
        if 0 < k < 7:
            sx = (plank * 13 + 7 + offset) % 32
            t[y][sx] = seam
            if k in (2, 4): t[y][(sx + 2) % 32] = OUTL
    return t


def mask_shape(t, inside, edge):
    out = tile()
    for y in range(32):
        for x in range(32):
            if inside(x, y): out[y][x] = OUTL if edge(x, y) else t[y][x]
    return out


def top_trim(t):
    t = [r[:] for r in t]
    for x in range(32):
        t[0][x] = OUTL; t[1][x] = RAILHI; t[2][x] = RAIL; t[3][x] = OUTL
    return t


R = 32.0
for cor, pal in WOODS.items():
    full = planks(pal)
    save(f'casco_{cor}_cheio', full)
    save(f'casco_{cor}_cheio_topo', top_trim(full))
    # Rampa ◢: sólido embaixo à direita; contorno na diagonal
    save(f'casco_{cor}_rampa', mask_shape(full, lambda x, y: x + y >= 31, lambda x, y: x + y <= 32))
    # Curvo ◢: quarto de círculo com centro no canto de baixo à direita
    def d(x, y): return math.hypot(32 - (x + 0.5), 32 - (y + 0.5))
    save(f'casco_{cor}_curvo', mask_shape(full, lambda x, y: d(x, y) <= R, lambda x, y: d(x, y) > R - 1.6))
    # Meio bloco: metade de baixo, com friso em cima
    half = tile()
    for y in range(16, 32): half[y] = full[y][:]
    for x in range(32):
        half[16][x] = OUTL; half[17][x] = RAILHI; half[18][x] = RAIL; half[19][x] = OUTL
    save(f'casco_{cor}_meio', half)

# ---------- Velas que se emendam ----------
SAILS = {
    'branca': (hx('f0e8d6'), hx('ded0b6'), hx('c4b296'), None),
    'listrada': (hx('f0e8d6'), hx('ded0b6'), hx('c4b296'), (hx('c8463e'), hx('a8363a'))),
    'preta': (hx('4a4452'), hx('3c3644'), hx('2c2834'), None),
}
YHI, YMID, YDK = hx('bc997f'), hx('ae7764'), hx('985252')
for estilo, (cl, cls, cld, stripe) in SAILS.items():
    for m in range(16):
        up, down, left, right = m & 1, m & 2, m & 4, m & 8
        t = tile()
        y_end = [31 if down else 25 + int(round(3 * math.sin(math.pi * x / 31))) for x in range(32)]
        for x in range(32):
            for y in range(0, y_end[x] + 1):
                band = stripe is not None and (y // 8) % 2 == 1
                base, shade = (stripe if band else (cl, cls))
                c = base
                if (not left and x == 0) or (not right and x == 31) or (not down and y == y_end[x]): c = OUTL
                elif (not left and x == 1) or (not right and x == 30) or (not down and y == y_end[x] - 1): c = cld
                elif left and x == 0: c = shade                                   # costura entre painéis
                elif y % 8 == 0 or (not left and x == 2) or (not right and x == 29): c = shade
                t[y][x] = c
        if not up:   # verga de madeira em cima
            for x in range(32):
                for y in range(5):
                    end = (not left and x == 0) or (not right and x == 31)
                    t[y][x] = OUTL if y in (0, 4) or end else (YHI if y == 1 else YMID if y < 3 else YDK)
        save(f'vela_{estilo}_{m}', t)

    # Aba de ligação com o mastro: desenhada na célula do mastro, do lado direito (o jogo espelha para o outro lado).
    # O pano vai da beira do mastro (x=20) até a vela; na peça de cima a verga atravessa o mastro inteiro.
    # Máscara: 1 = tem vela igual em cima, 2 = embaixo.
    for m in range(4):
        up, down = m & 1, m & 2
        t = tile()
        y_end = 31 if down else 25
        for x in range(20, 32):
            for y in range(0, y_end + 1):
                band = stripe is not None and (y // 8) % 2 == 1
                base, shade = (stripe if band else (cl, cls))
                c = base
                if x == 20 or (not down and y == y_end): c = OUTL
                elif x == 21 or (not down and y == y_end - 1): c = cld
                elif y % 8 == 0 or x == 22: c = shade
                t[y][x] = c
        if not up:
            for x in range(11, 32):
                for y in range(5):
                    t[y][x] = OUTL if y in (0, 4) or x == 11 else (YHI if y == 1 else YMID if y < 3 else YDK)
        save(f'vela_{estilo}_aba_{m}', t)


# ---------- Estrutura ----------
plat = load(T + '/Pirate Ship/Sprites/Tilesets/Platforms (32x32).png')
piso = [r[64:96] for r in plat[32:64]]
save('piso', piso)

ladder = tile()
LH, LM, LD = hx('bc997f'), hx('ae7764'), hx('985252')
for y in range(32):
    for x0 in (6, 22):
        ladder[y][x0] = OUTL; ladder[y][x0 + 1] = LH; ladder[y][x0 + 2] = LM; ladder[y][x0 + 3] = OUTL
for ry in (3, 11, 19, 27):
    for x in range(10, 22):
        ladder[ry][x] = OUTL; ladder[ry + 1][x] = LH; ladder[ry + 2][x] = LD; ladder[ry + 3][x] = OUTL
save('escada', ladder)

wallsheet = load(T + '/Pirate Ship/Sprites/Tilesets/Terrain and Back Wall (32x32).png')
wall = [r[64:96] for r in wallsheet[256:288]]
save('parede', wall)
win_dir = T + '/Pirate Ship/Sprites/Decorations/Window/Window/'
window = load(win_dir + sorted(os.listdir(win_dir))[0])
save('parede_janela', paste([r[:] for r in wall], window, 0, 0))

# Rede de dormir (beliche): lona pendurada entre dois ganchos
hammock = [r[:] for r in wall]
CANVAS, CANVAS_DK, ROPE = hx('e8dcc0'), hx('c4b296'), hx('6e5a4a')
for x in range(3, 29):
    sag = int(round(7 * math.sin(math.pi * (x - 3) / 25)))
    y = 12 + sag
    for k in range(4):
        hammock[y + k][x] = OUTL if k in (0, 3) else (CANVAS if k == 1 else CANVAS_DK)
for (x, y) in ((2, 9), (2, 10), (2, 11), (29, 9), (29, 10), (29, 11)): hammock[y][x] = ROPE
for x in (1, 2, 3, 28, 29, 30): hammock[8][x] = OUTL
save('parede_rede', hammock)

# ---------- Enfeites ----------
flag = load(T + '/Palm Tree Island/Sprites/Objects/Flag/Flag 01.png')
# A bandeira do pacote tem 34x93 (com o mastro): fica só o pano de cima, com o pedaço do mastro no meio da célula
cloth = trimmed([r[:] for r in flag[:30]])
cols = [sum(1 for y in range(len(cloth)) if cloth[y][x][3]) for x in range(len(cloth[0]))]
pole_x = max(range(len(cols)), key=lambda x: cols[x])                    # coluna mais cheia = o mastro da bandeira
# A ponta do mastro (mesmo desenho e grossura do bloco Mastro) com um pino dourado no topo
# e o pano preso na lateral direita (sem o mastrinho original do pacote)
# Imagem de 48x32: o mastro ocupa as colunas 12-19 (igual ao bloco Mastro; o pivô fica no meio do mastro,
# configurado no importador) e o pano sai pela direita, para fora da célula.
mast = load(f'{OUT}/Navio/mastro.png')
fl = [[CLEAR] * 48 for _ in range(32)]
for y in range(4, 32):
    for x in range(32): fl[y][x] = mast[y][x]
GOLD, GOLD_DK = hx('f0c050'), hx('b88a30')
for y, (x0, x1) in enumerate([(14, 17), (13, 18), (13, 18), (14, 17)]):
    for x in range(x0, x1 + 1):
        fl[y][x] = OUTL if x in (x0, x1) or y in (0, 3) else (GOLD if x < 16 else GOLD_DK)
pano = trimmed([r[pole_x + 2:] for r in cloth])
for y, row in enumerate(pano):
    for x, c in enumerate(row):
        if c[3] and 4 + y < 32 and 20 + x < 48: fl[4 + y][20 + x] = c
for y in range(4, min(32, 4 + len(pano))):          # amarração: o pano encosta no mastro
    if fl[y][20][3]: fl[y][20] = OUTL
png.write(f'{OUT}/Navio/bandeira.png', 48, 32, fl)
candle_dir = T + '/Pirate Ship/Sprites/Decorations/Candle/Candle/'
candle = load(candle_dir + sorted(os.listdir(candle_dir))[0])
save('lanterna', bottom_center(candle))
bar_dir = T + '/Pirate Ship/Sprites/Decorations/Barrels and Bottles/'
save('barril', bottom_center(load(bar_dir + sorted(os.listdir(bar_dir))[0])))
box_dir = T + '/Merchant Ship/Sprites/Box/Idle/'
save('caixote', bottom_center(load(box_dir + sorted(os.listdir(box_dir))[0])))
anc_dir = T + '/Merchant Ship/Sprites/Ship/Anchor/'
anchor = trimmed(load(anc_dir + sorted(os.listdir(anc_dir))[0]))
save('ancora', paste(tile(), anchor, (32 - len(anchor[0])) // 2, (32 - len(anchor)) // 2))

# Timão: roda de madeira com raios, num pedestal
helm = tile()
cx, cy = 15.5, 13.5
for y in range(32):
    for x in range(32):
        r = math.hypot(x - cx, y - cy)
        ang = math.atan2(y - cy, x - cx)
        if 8.5 <= r <= 11: helm[y][x] = OUTL if r > 10.2 or r < 9.3 else LH
        elif r < 8.5 and min(abs(((ang * 4 / math.pi) % 1) - 0.5), 0.5) > 0.38: helm[y][x] = LM
        elif r <= 2.5: helm[y][x] = LD
        elif 11 < r <= 13 and min(abs(((ang * 4 / math.pi) % 1) - 0.5), 0.5) > 0.42: helm[y][x] = OUTL if r > 12.3 else LM
for y in range(24, 32):
    for x in range(13, 19): helm[y][x] = OUTL if x in (13, 18) or y == 31 else (LH if x == 14 else LM)
save('timao', helm)

# Amurada: corrimão com balaústres (fica em cima do convés)
rail = tile()
for x in range(32):
    for y in (12, 13, 14, 15): rail[y][x] = OUTL if y in (12, 15) else (LH if y == 13 else LM)
for x0 in (3, 13, 23):
    for y in range(16, 32):
        for x in range(x0, x0 + 5): rail[y][x] = OUTL if x in (x0, x0 + 4) else (LH if x == x0 + 1 else LM)
save('amurada', rail)
print('pecas do navio ok')
