import sys, os
sys.path.insert(0, os.path.dirname(__file__))
import png
G = sys.argv[1]  # Game/Assets/Art/ArtTest/Sea
A = sys.argv[2]  # ArteBaixada
def hx(s): return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)
OUT, HI, MID, DK, SEAM, RAIL, RAILHI = map(hx, ['33323d', 'bc997f', 'ae7764', '985252', '833f4c', '94a2bd', 'cae1ed'])
CLEAR = (0, 0, 0, 0)

def planks(px, y0, y1, offset=0):
    for y in range(y0, y1):
        k = (y - y0) % 8
        plank = (y - y0) // 8
        col = HI if k == 0 else MID if k < 6 else DK if k == 6 else SEAM
        for x in range(32):
            px[y][x] = col
        if 0 < k < 7:
            sx = (plank * 13 + 7 + offset) % 32
            px[y][sx] = SEAM
            if k in (2, 4): px[y][(sx + 2) % 32] = OUT  # pregos

def tile(): return [[CLEAR] * 32 for _ in range(32)]
hull = tile(); planks(hull, 0, 32)
png.write(G + '/ship_hull.png', 32, 32, hull)
deck = tile()
for x in range(32):
    deck[0][x] = OUT; deck[1][x] = RAILHI; deck[2][x] = RAIL; deck[3][x] = OUT
planks(deck, 4, 32, 5)
png.write(G + '/ship_deck.png', 32, 32, deck)
keel = tile(); planks(keel, 0, 26, 9)
for y in range(26, 32):
    for x in range(32): keel[y][x] = SEAM if y < 29 else OUT
png.write(G + '/ship_keel.png', 32, 32, keel)

# Palmeira da frente: tronco e base recortados do tileset de 96x96
T = A + '/TreasureHunters/Treasure Hunters/Palm Tree Island/Sprites/Front Palm Trees/Front Palm Bottom and Grass (32x32).png'
w, h, p = png.read(T)
def crop(x, y, cw, ch): return [row[x:x + cw] for row in p[y:y + ch]]
png.write(G + '/palm_trunk.png', 32, 32, crop(0, 0, 32, 32))
png.write(G + '/palm_base.png', 32, 32, crop(0, 32, 32, 32))
png.write(G + '/island_grass.png', 64, 32, crop(32, 64, 64, 32))

# Tripulação: troca de cores do capitão (casaco/chapéu roxo -> outras cores)
w, h, p = png.read(G + '/captain.png')
swaps = {
    'crew_green': {'7b7bab': '5f9e6e', '68538a': '3f6e57', 'd06c63': 'e0b25a'},
    'crew_red': {'7b7bab': 'c45a5a', '68538a': '8f3a4a', 'd06c63': '6f8fc9'},
    'crew_brown': {'7b7bab': 'a0785a', '68538a': '6e5040', 'd06c63': 'd8d0b0'},
}
for name, sw in swaps.items():
    m = {hx(k)[:3]: hx(v) for k, v in sw.items()}
    q = [[(m[c[:3]][:3] + (c[3],)) if c[3] and c[:3] in m else c for c in row] for row in p]
    png.write(G + f'/{name}.png', w, h, q)
print('ok')
