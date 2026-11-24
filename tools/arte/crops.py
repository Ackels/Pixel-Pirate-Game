import sys, os
sys.path.insert(0, os.path.dirname(__file__))
import png
A = sys.argv[1]  # ArteBaixada
OUT = sys.argv[2]  # Game/Assets/Art/ArtTest
N = A + '/NinjaAdventure/Ninja Adventure - Asset Pack'
T = A + '/TreasureHunters/Treasure Hunters'
FL = N + '/Backgrounds/Tilesets/TilesetFloor.png'
WA = N + '/Backgrounds/Tilesets/TilesetWater.png'
HO = N + '/Backgrounds/Tilesets/TilesetHouse.png'
NA = N + '/Backgrounds/Tilesets/TilesetNature.png'
EL = N + '/Backgrounds/Tilesets/TilesetElement.png'
CH = N + '/Actor/Character'
SH = T + '/Pirate Ship/Sprites/Tilesets/Terrain and Back Wall (32x32).png'
TE = T + '/Palm Tree Island/Sprites/Terrain/Terrain (32x32).png'
def t16(c, r, w=1, h=1): return (c * 16, r * 16, w * 16, h * 16)
def t32(c, r, w=1, h=1): return (c * 32, r * 32, w * 32, h * 32)
V = {
    'tile_grass_0': (FL, t16(0, 12)), 'tile_grass_1': (FL, t16(1, 12)), 'tile_grass_2': (FL, t16(2, 12)),
    'tile_grass_3': (FL, t16(3, 12)), 'tile_grass_4': (FL, t16(4, 12)),
    'tile_dirt_tl': (FL, t16(0, 7)), 'tile_dirt_t': (FL, t16(1, 7)), 'tile_dirt_tr': (FL, t16(2, 7)),
    'tile_dirt_l': (FL, t16(0, 8)), 'tile_dirt_c': (FL, t16(8, 8)), 'tile_dirt_r': (FL, t16(2, 8)),
    'tile_dirt_bl': (FL, t16(0, 9)), 'tile_dirt_b': (FL, t16(1, 9)), 'tile_dirt_br': (FL, t16(2, 9)),
    'tile_sand_0': (FL, t16(0, 5)), 'tile_sand_1': (FL, t16(1, 5)), 'tile_sand_2': (FL, t16(2, 5)),
    'tile_shore': (WA, t16(1, 0)), 'tile_water': (WA, t16(1, 1)), 'tile_dock': (WA, t16(1, 13)),
    'obj_house_a': (HO, t16(0, 0, 4, 3)), 'obj_house_b': (HO, t16(4, 0, 4, 3)),
    'obj_tavern': (HO, t16(25, 14, 4, 5)), 'obj_shop': (HO, t16(19, 19, 3, 3)),
    'obj_tree_a': (NA, t16(0, 0, 2, 2)), 'obj_tree_b': (NA, t16(16, 0, 2, 2)), 'obj_tree_c': (NA, t16(18, 0, 2, 2)),
    'obj_bush': (NA, t16(0, 10)), 'obj_flower': (NA, t16(0, 11)),
    'obj_market': (EL, t16(14, 2, 2, 2)), 'obj_farm': (EL, t16(13, 4, 3, 3)), 'obj_barrel': (EL, t16(0, 0)),
    'obj_crate': (EL, t16(1, 0)),
    'obj_boat': (N + '/Backgrounds/Vehicles/Boat.png', None),
}
for i, n in enumerate(['Noble', 'Boy', 'Hunter', 'Child', 'Villager', 'Villager4', 'OldMan', 'Shaman', 'Skeleton']):
    V[f'char_{i}_{n}'] = (f'{CH}/{n}/SpriteSheet.png', (0, 0, 16, 16))
V['char_parrot'] = (N + '/Actor/Animal/Parrot/SpriteSheetRed.png', (0, 0, 16, 16))
S_ = {
    'sea_sky': (T + '/Palm Tree Island/Sprites/Background/Additional Sky.png', None),
    'sea_horizon': (T + '/Palm Tree Island/Sprites/Background/BG Image.png', None),
    'sea_clouds': (T + '/Palm Tree Island/Sprites/Background/Big Clouds.png', None),
    'sea_cloud_1': (T + '/Palm Tree Island/Sprites/Background/Small Cloud 2.png', None),
    'sea_cloud_2': (T + '/Palm Tree Island/Sprites/Background/Small Cloud 3.png', None),
    'sea_water_deep': (T + '/Palm Tree Island/Sprites/Background/Additional Water.png', None),
    'sea_water_top': (T + '/Merchant Ship/Sprites/Water/Water/Top/' + sorted(os.listdir(T + '/Merchant Ship/Sprites/Water/Water/Top'))[0], None),
    'sea_water_bottom': (T + '/Merchant Ship/Sprites/Water/Water/Bottom/' + sorted(os.listdir(T + '/Merchant Ship/Sprites/Water/Water/Bottom'))[0], None),
   
    'ship_cabin': (SH, t32(2, 8)),
    'island_tl': (TE, t32(0, 0)), 'island_t': (TE, t32(1, 0)), 'island_tr': (TE, t32(2, 0)),
    'island_l': (TE, t32(0, 1)), 'island_c': (TE, t32(1, 1)), 'island_r': (TE, t32(2, 1)),
    'palm_top': (T + '/Palm Tree Island/Sprites/Front Palm Trees/Front Palm Tree Top 01.png', None),
    'palm_back': (T + '/Palm Tree Island/Sprites/Back Palm Trees/Back Palm Tree Regular 01.png', None),
    'chest': (T + '/Palm Tree Island/Sprites/Objects/Chest/Chest Close 01.png', None),
    'flag': (T + '/Palm Tree Island/Sprites/Objects/Flag/' + sorted(os.listdir(T + '/Palm Tree Island/Sprites/Objects/Flag'))[0], None),
    'helm': (T + '/Palm Tree Island/Sprites/Objects/Ship Helm/' + sorted(os.listdir(T + '/Palm Tree Island/Sprites/Objects/Ship Helm'))[0], None),
    'sail': (T + '/Merchant Ship/Sprites/Ship/Sail/Wind/' + sorted(os.listdir(T + '/Merchant Ship/Sprites/Ship/Sail/Wind'))[0], None),
    'merchant': (T + '/Merchant Ship/Sprites/Ship/Ship/Idle/1.png', None),
    'window': (T + '/Pirate Ship/Sprites/Decorations/Window/Window/' + sorted(os.listdir(T + '/Pirate Ship/Sprites/Decorations/Window/Window'))[0], None),
    'barrels': (T + '/Pirate Ship/Sprites/Decorations/Barrels and Bottles/01.png', None),
    'captain': (T + '/Captain Clown Nose/Sprites/Captain Clown Nose/Captain Clown Nose with Sword/09-Idle Sword/Idle Sword 01.png', None),
    'crabby': (T + '/The Crusty Crew/Sprites/Crabby/01-Idle/Idle 01.png', None),
    'fierce': (T + '/The Crusty Crew/Sprites/Fierce Tooth/01-Idle/Idle 01.png', None),
    'pinkstar': (T + '/The Crusty Crew/Sprites/Pink Star/01-Idle/Idle 01.png', None),
    'cannon': (T + '/Shooter Traps/Sprites/Cannon/Cannon Idle/1.png', None),
}
def first(p):
    if os.path.exists(p): return p
    d = os.path.dirname(p)
    while not os.path.isdir(d): d = os.path.dirname(d)
    raise FileNotFoundError(p + '  (existe: ' + ', '.join(os.listdir(d)[:8]) + ')')
def go(table, sub):
    os.makedirs(f'{OUT}/{sub}', exist_ok=True)
    for name, (src, rect) in table.items():
        try:
            w, h, px = png.read(first(src))
        except FileNotFoundError as e:
            print('FALTA', name, e); continue
        x, y, cw, ch = rect or (0, 0, w, h)
        png.write(f'{OUT}/{sub}/{name}.png', cw, ch, [row[x:x + cw] for row in px[y:y + ch]])
    print(sub, len(table))
go(V, 'Village'); go(S_, 'Sea')
