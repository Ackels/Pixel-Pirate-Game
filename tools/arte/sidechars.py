import sys, os
sys.path.insert(0, os.path.dirname(__file__))
import png
CH = sys.argv[1] + '/NinjaAdventure/Ninja Adventure - Asset Pack/Actor/Character'
OUT = sys.argv[2]
os.makedirs(OUT, exist_ok=True)
for n in ['Noble', 'Boy', 'Hunter', 'Child', 'Villager', 'Villager4', 'OldMan', 'Shaman', 'Skeleton']:
    w, h, p = png.read(f'{CH}/{n}/SpriteSheet.png')
    for col, d in ((3, 'right'), (2, 'left')):
        png.write(f'{OUT}/side_{n}_{d}.png', 16, 16, [row[col * 16:col * 16 + 16] for row in p[0:16]])
print('ok')
