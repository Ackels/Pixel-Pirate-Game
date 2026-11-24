"""Personagens piratas: pinta acessórios por cima dos bonecos do Ninja Adventure (mesmo estilo e animações).

Cada quadro 16x16 da folha (colunas = baixo, cima, esquerda, direita) ganha o chapéu alinhado ao topo
da cabeça daquele quadro (a cabeça sobe e desce ao andar), a barba no rosto e a roupa recolorida.
O retrato (Faceset 38x38) recebe os mesmos acessórios em tamanho grande.

Uso: python -P tools/arte/pirata_export.py ArteBaixada Game/Assets/Art/Ninja
"""
import os, sys
sys.path.insert(0, os.path.dirname(__file__))
import png

A, OUT = sys.argv[1], sys.argv[2]
CHARS = A + '/NinjaAdventure/Ninja Adventure - Asset Pack/Actor/Character/'

CLEAR = (0, 0, 0, 0)
K = (20, 27, 27, 255)          # contorno do pacote
HAT = (54, 40, 52, 255)        # tricórnio quase preto
HAT_HI = (88, 66, 82, 255)
GOLD = (232, 186, 72, 255)
BONE = (240, 234, 220, 255)
BEARD = (104, 60, 42, 255)
BEARD_DK = (72, 40, 32, 255)

PALETTE = {'K': K, 'H': HAT, 'h': HAT_HI, 'G': GOLD, 'W': BONE, '.': None}


def art(rows): return [[PALETTE[c] for c in r] for r in rows]


# Chapéu de três pontas, 16x6, para cada direção. A última linha fica 4 px abaixo do topo da cabeça.
TRICORN = {
    # Abas laterais levantadas (com borda dourada): é isso que faz o "três pontas" ler como pirata
    'down': art([
        'KK..KKKKKKKK..KK',
        'KGKKHHHhhHHHKKGK',
        'KGHHHHWWWWHHHHGK',
        'KGHHHHWKKWHHHHGK',
        '.KGHHHHWWHHHHGK.',
        '..KGGGGGGGGGGK..',
    ]),
    'up': art([
        'KK..KKKKKKKK..KK',
        'KGKKHHHhhHHHKKGK',
        'KGHHHHHHHHHHHHGK',
        'KGHHHHHHHHHHHHGK',
        '.KGHHHHHHHHHHGK.',
        '..KGGGGGGGGGGK..',
    ]),
    'left': art([
        'KK..KKKKKKKK..K.',
        'KGKKHhhHHHHHKKGK',
        'KGHHWWHHHHHHHHGK',
        '.KGHWWHHHHHHHGK.',
        '.KGGHHHHHHHHGGK.',
        '..KKGGGGGGGGKK..',
    ]),
}
TRICORN['right'] = [row[::-1] for row in TRICORN['left']]

# Retrato: tricórnio grande (30x11), com caveira
TRICORN_FACE = art([
    '..........KKKKKKKKKK..........',
    '.......KKKHHHHhhHHHHKKK.......',
    '.....KKHHHHHhhhhhHHHHHHKK.....',
    '...KKHHHHHHHHWWWHHHHHHHHHKK...',
    '..KHHHHHHHHHWWWWWHHHHHHHHHHK..',
    '.KHHHHHHHHHHWKWKWHHHHHHHHHHHK.',
    'KHHHHHHHHHHHHWWWHHHHHHHHHHHHHK',
    'KGHHHHHHHHHHHWKWHHHHHHHHHHHHGK',
    'KGGGHHHHHHHHHHHHHHHHHHHHHHGGGK',
    '.KKGGGGHHHHHHHHHHHHHHHHGGGGKK.',
    '...KKKKGGGGGGGGGGGGGGGGKKKK...',
])


def head_top(frame):
    """Topo da cabeça = primeira linha com 6+ pixels seguidos (ignora armas finas no alto)."""
    for y, row in enumerate(frame):
        run = best = 0
        start = bstart = 0
        for x, c in enumerate(row):
            if c[3]:
                if run == 0: start = x
                run += 1
                if run > best: best, bstart = run, start
            else:
                run = 0
        if best >= 6: return y, bstart + best / 2
    return None, None


def stamp(frame, img, ox, oy):
    for y, row in enumerate(img):
        for x, c in enumerate(row):
            if c and 0 <= oy + y < 16 and 0 <= ox + x < 16: frame[oy + y][ox + x] = c


def make_captain(base, coat):
    w, h, sheet = png.read(CHARS + base + '/SpriteSheet.png')
    SKIN, SKIN_SH = (239, 145, 79), (209, 75, 52)
    out = [row[:] for row in sheet]
    for fy in range(h // 16):
        for fx in range(w // 16):
            d = ['down', 'up', 'left', 'right'][fx]
            frame = [out[fy * 16 + y][fx * 16:fx * 16 + 16] for y in range(16)]
            # Roupa: casaco vermelho com camisa clara
            for y in range(16):
                for x in range(16):
                    c = frame[y][x]
                    if c[3] and c[:3] in coat: frame[y][x] = coat[c[:3]] + (255,)
            top, cx = head_top(frame)
            if top is None: continue
            # Barba (frente e lado): linha logo abaixo dos olhos e o queixo
            if d != 'up' and fy <= 4:
                xs = range(2, 14) if d == 'down' else (range(1, 9) if d == 'left' else range(7, 15))
                for yy, base_c in ((top + 8, None), (top + 9, None)):
                    if not 0 <= yy < 16: continue
                    for x in xs:
                        c = frame[yy][x]
                        if c[3] and c[:3] == SKIN: frame[yy][x] = BEARD
                        elif c[3] and c[:3] == SKIN_SH: frame[yy][x] = BEARD_DK
            stamp(frame, TRICORN[d], int(round(cx - 8)), top - 1)
            for y in range(16): out[fy * 16 + y][fx * 16:fx * 16 + 16] = frame[y]
    return w, h, out


def make_face(base, coat):
    w, h, face = png.read(CHARS + base + '/Faceset.png')
    SKIN, SKIN_SH, HAIR = (239, 145, 79), (209, 75, 52), (239, 160, 66)
    out = [row[:] for row in face]
    for y in range(h):
        for x in range(w):
            c = out[y][x][:3]
            if y >= 26 and c in coat: out[y][x] = coat[c] + (255,)
            if c == HAIR: out[y][x] = K                 # o topete fica escondido pelo chapéu
    # Barba: costeletas, bigode e queixo (mantém a boca)
    for y in range(19, 27):
        for x in range(5, 33):
            c = out[y][x][:3]
            if y == 19 and 10 < x < 28: continue
            if c == SKIN: out[y][x] = BEARD
            elif c == SKIN_SH: out[y][x] = BEARD_DK
    for y in range(15, 19):
        for x in list(range(5, 8)) + list(range(29, 33)):
            if out[y][x][:3] in (SKIN, SKIN_SH): out[y][x] = BEARD_DK
    for y, row in enumerate(TRICORN_FACE):
        for x, c in enumerate(row):
            if c: out[2 + y][4 + x] = c
    return w, h, out


def save(name, sheet, face):
    dst = f'{OUT}/Characters/{name}'
    os.makedirs(dst, exist_ok=True)
    png.write(dst + '/SpriteSheet.png', *sheet)
    png.write(dst + '/Faceset.png', *face)
    print('ok', name)


# Capitão: careca do pacote ("Villager") + tricórnio, barba e casaco vermelho com dourado
CAPTAIN_COAT = {
    (84, 135, 137): (168, 40, 46),     # camisa azul-esverdeada -> casaco vermelho
    (46, 57, 57): (104, 24, 32),       # sombra -> vermelho escuro
    (121, 184, 206): (110, 70, 44),    # parte de baixo da camisa -> cinto marrom
}
FACE_COAT = {
    (84, 135, 137): (168, 40, 46),     # casaco
    (121, 184, 206): (236, 224, 200),  # camisa clara por baixo
}
save('CapitaoPirata', make_captain('Villager', CAPTAIN_COAT), make_face('Villager', FACE_COAT))


# =====================================================================================
# Kit pirata genérico (tripulação, inimigos): bandana, tapa-olho, barba, camisa listrada,
# casaco e tricórnio. Tudo segue o formato de cada boneco (cabelo, rosto), quadro a quadro.
# =====================================================================================
from collections import Counter

WHITE = (236, 228, 214)


def is_dark(c): return c[3] and c[0] + c[1] + c[2] < 140          # contorno, olhos, boca
def solid(c): return c[3] and not is_dark(c)
def shade(c, k): return (int(c[0] * k), int(c[1] * k), int(c[2] * k), 255)
def mix(a, b, t): return tuple(int(a[i] * (1 - t) + b[i] * t) for i in range(3)) + (255,)
def dist(a, b): return sum((a[i] - b[i]) ** 2 for i in range(3)) ** 0.5


def most_common(px, rows, xs, exclude=()):
    cnt = Counter()
    for y in rows:
        if not 0 <= y < len(px): continue
        for x in xs:
            c = px[y][x]
            if solid(c) and c[:3] not in exclude: cnt[c[:3]] += 1
    return cnt.most_common(1)[0][0] if cnt else None


def recolor_hat(rows, body, hi):
    m = {HAT: body, HAT_HI: hi}
    return [[m.get(c, c) if c else None for c in r] for r in rows]


# ---------- Quadros 16x16 ----------
def sprite_bandana(f, top, d, col):
    dk, lt = shade(col, 0.6), mix(col, WHITE, 0.5)
    for y in range(top, min(16, top + 4)):
        for x in range(16):
            if not solid(f[y][x]): continue
            f[y][x] = dk if y == top + 3 else (lt if y > top and (x + 2 * y) % 4 == 0 else col)
    # Nó com as pontas soltas atrás da cabeça
    y = top + 3
    if y + 2 >= 16: return
    if d in ('left', 'right'):
        row = [x for x in range(16) if f[y][x][3]]
        if not row: return
        xe, s = (max(row), 1) if d == 'left' else (min(row), -1)
        for (dx, dy, c) in ((1, 0, col), (1, 1, dk), (2, 0, K), (2, 1, K), (1, 2, K)):
            x = xe + s * dx
            if 0 <= x < 16: f[y + dy][x] = c
    elif d == 'up':
        row = [x for x in range(16) if f[y][x][3]]
        if not row: return
        cx = (min(row) + max(row)) // 2
        for (x, dy, c) in ((cx, 1, col), (cx + 1, 1, col), (cx, 2, dk), (cx + 1, 2, dk), (cx - 1, 1, K), (cx + 2, 1, K),
                           (cx - 1, 2, K), (cx + 2, 2, K)):
            if 0 <= x < 16 and y + dy < 16: f[y + dy][x] = c


def sprite_eyes(f, top):
    pts = []
    for y in range(top + 4, min(16, top + 10)):
        for x in range(1, 15):
            if is_dark(f[y][x]) and solid(f[y][x - 1]) and solid(f[y][x + 1]): pts.append((x, y))
    return pts


def sprite_patch(f, top, d):
    """Tapa-olho no olho direito do personagem: o da esquerda (de frente) ou o único (de lado, olhando à direita)."""
    if d not in ('down', 'right'): return
    pts = sprite_eyes(f, top)
    if not pts: return
    x0 = min(x for x, _ in pts) if d == 'down' else max(x for x, _ in pts)
    ys = sorted(y for x, y in pts if x == x0)
    y0 = ys[0]
    px = (x0 - 1, x0) if d == 'down' else (x0, x0 + 1)
    for x in px:
        for y in (y0, y0 + 1):
            if 0 <= x < 16 and y < 16: f[y][x] = K
    # Tira subindo na diagonal até a testa
    sx, step = (px[1] + 1, 1) if d == 'down' else (px[0] - 1, -1)
    y = y0 - 1
    while y > top + 2 and 0 <= sx < 16:
        if f[y][sx][3]: f[y][sx] = K
        sx += step; y -= 1


def sprite_beard(f, top, d, skin, col):
    if d == 'up' or skin is None: return
    for y in (top + 8, top + 9):
        if not 0 <= y < 16: continue
        for x in range(16):
            c = f[y][x]
            if solid(c) and dist(c, skin) < 95:
                f[y][x] = col if sum(c[:3]) >= sum(skin) - 30 else shade(col, 0.7)


def sprite_shirt(f, top, shirt, stripes, coat):
    if shirt is None: return
    for y in range(min(16, top + 10), 16):
        for x in range(16):
            c = f[y][x]
            if c[3] and c[:3] == shirt:
                if coat: f[y][x] = coat
                elif stripes: f[y][x] = WHITE + (255,) if (y - top) % 2 == 0 else stripes
    return


# ---------- Retrato 38x38 ----------
def face_info(px):
    w, h = len(px[0]), len(px)
    bg = px[0][0][:3]
    skin = most_common(px, range(16, 25), range(10, 29), exclude={bg})
    top = next((y for y in range(h) if any(px[y][x][:3] != bg for x in range(4, 34))), 0)
    eye_row, eyes = None, []
    for y in range(top + 4, 28):
        hits = [x for x in range(2, w - 2) if is_dark(px[y][x]) and px[y][x][:3] != bg
                and any(px[y][x - k][:3] == skin for k in (1, 2)) and any(px[y][x + k][:3] == skin for k in (1, 2))]
        if len(hits) >= 2: eye_row, eyes = y, hits; break
    if eye_row is None: eye_row = top + 12
    return bg, skin, top, eye_row, eyes


def face_bandana(px, bg, top, eye_row, col):
    dk, lt = shade(col, 0.6), mix(col, WHITE, 0.5)
    bottom = eye_row - 4
    for y in range(top, bottom + 1):
        for x in range(len(px[0])):
            c = px[y][x]
            if c[:3] == bg or not c[3]: continue
            inside = all(px[yy][xx][:3] != bg for xx, yy in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1))
                         if 0 <= xx < len(px[0]) and 0 <= yy < len(px))
            if is_dark(c) and not inside: continue                    # contorno fica
            if y >= bottom - 1: px[y][x] = dk
            elif is_dark(c): px[y][x] = dk
            else: px[y][x] = lt if (x + 2 * y) % 5 == 0 else col
    # Nó com as pontas do lado direito
    xs = [x for x in range(len(px[0])) if px[bottom][x][:3] != bg]
    if xs:
        xr = max(xs)
        for i in range(6):
            for dx in range(1, 4 - i // 3):
                x, y = xr + dx - 1, bottom - 2 + i
                if 0 <= x < len(px[0]) and y < len(px): px[y][x] = col if i < 3 else dk


def face_patch(px, eye_row, eyes):
    if not eyes: return
    left = [x for x in eyes if x <= min(eyes) + 3]
    x0, x1 = min(left) - 1, max(left) + 1
    for y in range(eye_row - 1, eye_row + 5):
        for x in range(x0, x1 + 1): px[y][x] = (34, 30, 38, 255)
    px[eye_row - 1][x0] = (70, 64, 76, 255)
    x, y = x1 + 1, eye_row - 2
    while y > eye_row - 9 and x < len(px[0]):
        px[y][x] = K; px[y][x + 1 if x + 1 < len(px[0]) else x] = K
        x += 1; y -= 1


def face_beard(px, skin, eye_row, col):
    if skin is None: return
    for y in range(eye_row + 6, min(len(px), eye_row + 13)):
        for x in range(len(px[0])):
            c = px[y][x]
            if solid(c) and dist(c, skin) < 95:
                px[y][x] = col if sum(c[:3]) >= sum(skin) - 30 else shade(col, 0.7)


def face_shirt(px, bg, skin, stripes, coat):
    if not stripes and not coat: return
    shirt = most_common(px, range(30, 38), range(len(px[0])), exclude={bg, skin})
    if shirt is None: return
    for y in range(27, len(px)):
        for x in range(len(px[0])):
            if px[y][x][:3] == shirt:
                px[y][x] = coat if coat else (WHITE + (255,) if (y // 2) % 2 == 0 else stripes)


def face_tricorn(px, bg, eye_row, hat):
    bottom = eye_row - 2
    row = bottom - 2
    xs = [x for x in range(len(px[0])) if px[row][x][:3] != bg]
    cx = (min(xs) + max(xs)) // 2 if xs else 19
    ox, oy = cx - 15, bottom - len(hat) + 1
    for y, r in enumerate(hat):
        for x, c in enumerate(r):
            if c and 0 <= oy + y < len(px) and 0 <= ox + x < len(px[0]): px[oy + y][ox + x] = c


def piratize(name, base, bandana=None, patch=False, beard=None, stripes=None, coat=None, tricorn=None):
    w, h, sheet = png.read(CHARS + base + '/SpriteSheet.png')
    first = [sheet[y][0:16] for y in range(16)]
    t0, _ = head_top(first)
    skin = most_common(first, range(t0 + 5, t0 + 9), range(3, 13))
    shirt = most_common(first, range(t0 + 10, t0 + 12), range(2, 14), exclude={skin})
    out = [row[:] for row in sheet]
    for fy in range(h // 16):
        for fx in range(w // 16):
            d = ['down', 'up', 'left', 'right'][fx]
            f = [out[fy * 16 + y][fx * 16:fx * 16 + 16] for y in range(16)]
            top, cx = head_top(f)
            if top is None: continue
            sprite_shirt(f, top, shirt, stripes, coat)
            if beard and fy <= 4: sprite_beard(f, top, d, skin, beard)
            if bandana: sprite_bandana(f, top, d, bandana)
            if patch and fy <= 4: sprite_patch(f, top, d)
            if tricorn: stamp(f, tricorn[d], int(round(cx - 8)), top - 1)
            for y in range(16): out[fy * 16 + y][fx * 16:fx * 16 + 16] = f[y]

    fw, fh, face = png.read(CHARS + base + '/Faceset.png')
    fc = [row[:] for row in face]
    bg, fskin, ftop, eye_row, eyes = face_info(fc)
    face_shirt(fc, bg, fskin, stripes, coat)
    if beard: face_beard(fc, fskin, eye_row, beard)
    if bandana: face_bandana(fc, bg, ftop, eye_row, bandana)
    if patch: face_patch(fc, eye_row, eyes)
    if tricorn: face_tricorn(fc, bg, eye_row, tricorn['face'])
    save(name, (w, h, out), (fw, fh, fc))


RED, BLUE, GREEN, PURPLE, YELLOW, BLACK = (186, 46, 52, 255), (52, 92, 170, 255), (58, 138, 78, 255), (120, 62, 142, 255), (218, 166, 58, 255), (40, 36, 46, 255)
NAVY, STRIPE_RED = (44, 58, 112, 255), (164, 40, 48, 255)
BROWN, GRAY = (104, 62, 42, 255), (150, 146, 150, 255)

# Tripulação: cada um com uma combinação diferente
CREW = [
    ('PirataBoy', 'Boy', dict(bandana=RED, stripes=NAVY)),
    ('PirataOldMan', 'OldMan', dict(bandana=BLUE, patch=True, beard=GRAY)),
    ('PirataOldMan2', 'OldMan2', dict(stripes=STRIPE_RED)),                     # boina vermelha e barba branca: lobo do mar
    ('PirataCavegirl', 'Cavegirl', dict(bandana=PURPLE, stripes=STRIPE_RED)),
    ('PirataCaveman', 'Caveman', dict(bandana=GREEN, patch=True, stripes=STRIPE_RED)),
    ('PirataCaveman2', 'Caveman2', dict(bandana=YELLOW, stripes=NAVY)),
    ('PirataVillager2', 'Villager2', dict(bandana=RED, patch=True)),
    ('PirataVillager3', 'Villager3', dict(bandana=BLUE, stripes=STRIPE_RED)),
    ('PirataVillager4', 'Villager4', dict(bandana=GREEN, stripes=NAVY)),
    ('PirataVillager5', 'Villager5', dict(bandana=PURPLE, patch=True)),
    ('PirataWoman', 'Woman', dict(bandana=RED, stripes=NAVY)),
    ('PirataHunter', 'Hunter', dict(stripes=NAVY, patch=True)),                # já tem faixa vermelha na cabeça
    ('PirataPrincess', 'Princess', dict(bandana=YELLOW, stripes=STRIPE_RED)),
    ('PirataOldWoman', 'OldWoman', dict(bandana=GREEN)),                          # cabelo roxo longo: sem listras (confundiria com a camisa)
]
for name, base, kit in CREW: piratize(name, base, **kit)

# Pirata inimigo: bandana preta, tapa-olho e roupa escura (bem diferente do seu bando)
piratize('PirataInimigo', 'FighterRed', bandana=BLACK, patch=True, coat=(58, 50, 62, 255))

# Capitão Lendário: velho de barba branca, tricórnio preto com caveira e casaco roxo
LEGEND_HAT = {d: recolor_hat(TRICORN[d], (30, 26, 34, 255), (70, 60, 78, 255)) for d in TRICORN}
LEGEND_HAT['face'] = recolor_hat(TRICORN_FACE, (30, 26, 34, 255), (70, 60, 78, 255))
piratize('CapitaoLendario', 'Master', tricorn=LEGEND_HAT, coat=(92, 44, 116, 255))
