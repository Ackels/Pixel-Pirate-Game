"""Ícone do mapa da Fortaleza do Capitão Lendário (24x24, no estilo dos outros ícones de ilha do ninja_export.py).

Uso: python -P tools/arte/fortaleza_export.py
"""
import os, sys
sys.path.insert(0, os.path.dirname(__file__))
import png

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Game', 'Assets', 'Art', 'Ninja', 'Icons')
MC = {'o': (20, 27, 27, 255), 's': (255, 203, 141, 255), 'S': (255, 173, 93, 255), 'g': (173, 188, 58, 255),
      'G': (116, 163, 52, 255), 'r': (214, 60, 52, 255), 'R': (150, 40, 40, 255), 'k': (40, 40, 45, 255),
      'c': (178, 182, 196, 255), 'C': (128, 132, 150, 255), 'w': (241, 230, 200, 255), '.': None}


def icon_island():
    img = [[(0, 0, 0, 0)] * 24 for _ in range(24)]
    for y in range(24):
        for x in range(24):
            sand = ((x - 11.5) / 10.5) ** 2 + ((y - 18.5) / 3.6) ** 2 <= 1
            grass = ((x - 11.5) / 8.2) ** 2 + ((y - 17.0) / 2.4) ** 2 <= 1 and y <= 18
            if sand: img[y][x] = MC['S'] if y >= 20 else MC['s']
            if grass: img[y][x] = MC['G'] if y >= 18 else MC['g']
    out = [row[:] for row in img]
    for y in range(24):
        for x in range(24):
            if img[y][x][3]: continue
            if any(0 <= y + dy < 24 and 0 <= x + dx < 24 and img[y + dy][x + dx][3] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                out[y][x] = MC['o']
    return out


# Castelo de pedra com bandeira vermelha de caveira
FORT = [
    '......okrrr.',
    '......orwrr.',
    '......orrrr.',
    '......o.....',
    'oco.oco.oco.',
    'ocooocooocoo',
    'occcccccccco',
    'oCccCCCCccCo',
    'oCccCkkCccCo',
    'oCCCCkkCCCCo',
    'oCCCCkkCCCCo',
    'oooooooooooo',
]
img = icon_island()
for y, row in enumerate(FORT):
    for x, ch in enumerate(row):
        c = MC[ch]
        if c: img[5 + y][6 + x] = c
os.makedirs(OUT, exist_ok=True)
png.write(os.path.join(OUT, 'mapa_fortaleza.png'), 24, 24, img)
print('fortaleza ok')
