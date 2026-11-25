"""Camadas de fundo desfocadas (profundidade, como no Pixel Piracy): o que está longe fica macio e o que está perto, nítido.

Gera em Game/Assets/Art/Mar:
- Ceu/nuvem_grande_borrada, Ceu/nuvem_N_borrada: nuvens com desfoque.
- Ilha/coqueiro_fundo_N_borrado: coqueiros do fundo desfocados (fileira de trás da selva).
- Ilha/coqueiro_longe_N: ainda mais desfocados, menores e azulados (a fileira mais distante).

Uso: python -P tools/arte/profundidade_export.py
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(__file__))
import png

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
MAR = os.path.join(ROOT, 'Game', 'Assets', 'Art', 'Mar')


def load(rel): return png.read(os.path.join(MAR, rel + '.png'))[2]
def save(rel, px):
    png.write(os.path.join(MAR, rel + '.png'), len(px[0]), len(px), px)
    print('ok', rel, len(px[0]), 'x', len(px))


def blur(px, radius, pad=None):
    """Desfoque gaussiano com alfa pré-multiplicado (as bordas transparentes não escurecem)."""
    pad = pad if pad is not None else int(math.ceil(radius * 2))
    h, w = len(px) + 2 * pad, len(px[0]) + 2 * pad
    sigma = max(0.5, radius)
    k = int(math.ceil(sigma * 2.5))
    kern = [math.exp(-(i * i) / (2 * sigma * sigma)) for i in range(-k, k + 1)]
    s = sum(kern); kern = [v / s for v in kern]
    # pré-multiplica
    src = [[(0.0, 0.0, 0.0, 0.0)] * w for _ in range(h)]
    for y, row in enumerate(px):
        for x, c in enumerate(row):
            a = c[3] / 255.0
            src[y + pad][x + pad] = (c[0] * a, c[1] * a, c[2] * a, a)
    def pass_(img, horiz):
        out = [[(0.0, 0.0, 0.0, 0.0)] * w for _ in range(h)]
        for y in range(h):
            for x in range(w):
                r = g = b = a = 0.0
                for i, kv in enumerate(kern):
                    d = i - k
                    xx, yy = (x + d, y) if horiz else (x, y + d)
                    if 0 <= xx < w and 0 <= yy < h:
                        c = img[yy][xx]
                        r += c[0] * kv; g += c[1] * kv; b += c[2] * kv; a += c[3] * kv
                out[y][x] = (r, g, b, a)
        return out
    img = pass_(pass_(src, True), False)
    res = []
    for row in img:
        line = []
        for r, g, b, a in row:
            if a <= 0.004: line.append((0, 0, 0, 0))
            else: line.append((min(255, int(r / a)), min(255, int(g / a)), min(255, int(b / a)), min(255, int(a * 255))))
        res.append(line)
    return res


def tint(px, color, amount, alpha=1.0):
    return [[(int(c[0] + (color[0] - c[0]) * amount), int(c[1] + (color[1] - c[1]) * amount),
              int(c[2] + (color[2] - c[2]) * amount), int(c[3] * alpha)) if c[3] else c for c in row] for row in px]


def half(px):
    return [[px[y * 2][x * 2] for x in range(len(px[0]) // 2)] for y in range(len(px) // 2)]


save('Ceu/nuvem_grande_borrada', blur(load('Ceu/nuvem_grande'), 2.2))
for i in range(3): save(f'Ceu/nuvem_{i}_borrada', blur(load(f'Ceu/nuvem_{i}'), 1.2))
HAZE = (150, 175, 190)
for i in range(3):
    palm = load(f'Ilha/coqueiro_fundo_{i}')
    save(f'Ilha/coqueiro_fundo_{i}_borrado', tint(blur(palm, 1.0), HAZE, 0.18))
    save(f'Ilha/coqueiro_longe_{i}', tint(blur(palm, 2.0), HAZE, 0.45, 0.9))
print('profundidade ok')
