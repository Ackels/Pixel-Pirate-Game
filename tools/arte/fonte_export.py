"""Prepara a fonte da interface: Pixelify Sans Bold (OFL 1.1) com a "boca" do C e do c mais aberta e um "5" que não se confunde com o "S".

No negrito original, o C tem uma abertura de menos de meio pixel do desenho; em letra pequena ela some
e "Casa" vira "Oasa". Aqui a abertura passa a ter ~2 pixels. Ç e ç reaproveitam o desenho do C/c.
A licença OFL permite modificar; o nome da fonte não é reservado. Mantenha o OFL.txt junto do arquivo.

Uso: python -P fonte_export.py <PixelifySans-Bold.ttf original> <destino.ttf>
"""
import struct, sys
sys.path.insert(0, __import__('os').path.dirname(__file__))
import ttf

src, dst = sys.argv[1], sys.argv[2]
font = ttf.TTF(src)
d = font.data


def read_simple(raw):
    """Lê um glifo simples: (cabeçalho, fins de contorno, instruções, flags, xs, ys)."""
    nc, xmin, ymin, xmax, ymax = struct.unpack('>hhhhh', raw[:10])
    ends = list(struct.unpack('>%dH' % nc, raw[10:10 + 2 * nc]))
    p = 10 + 2 * nc
    il = struct.unpack('>H', raw[p:p + 2])[0]
    instr = raw[p + 2:p + 2 + il]
    p += 2 + il
    n = ends[-1] + 1
    flags = []
    while len(flags) < n:
        f = raw[p]; p += 1; flags.append(f)
        if f & 8:
            r = raw[p]; p += 1; flags.extend([f] * r)
    xs, ys, v = [], [], 0
    for f in flags:
        if f & 2: dv = raw[p]; p += 1; v += dv if f & 16 else -dv
        elif not f & 16: v += struct.unpack('>h', raw[p:p + 2])[0]; p += 2
        xs.append(v)
    v = 0
    for f in flags:
        if f & 4: dv = raw[p]; p += 1; v += dv if f & 32 else -dv
        elif not f & 32: v += struct.unpack('>h', raw[p:p + 2])[0]; p += 2
        ys.append(v)
    return (nc, xmin, ymin, xmax, ymax), ends, instr, flags, xs, ys


def write_simple(head, ends, instr, flags, xs, ys):
    """Grava o glifo com deslocamentos de 2 bytes (simples e sempre válido)."""
    nc, xmin, ymin, xmax, ymax = head
    out = bytearray(struct.pack('>hhhhh', nc, min(xs), min(ys), max(xs), max(ys)))
    out += struct.pack('>%dH' % nc, *ends)
    out += struct.pack('>H', len(instr)) + instr
    out += bytes(f & 1 for f in flags)          # só o bit "ponto na curva"
    px = 0
    for x in xs: out += struct.pack('>h', x - px); px = x
    py = 0
    for y in ys: out += struct.pack('>h', y - py); py = y
    while len(out) % 4: out += b'\0'
    return bytes(out)


# Glifos a mudar: no lado direito (x >= 414), troca as alturas que limitam a abertura
CHANGES = {'C': {297: 211, 331: 416}, 'c': {211: 170, 246: 290}}

numglyphs = struct.unpack('>H', d[font.tables['maxp'][0] + 4:font.tables['maxp'][0] + 6])[0]
glyf_off = font.tables['glyf'][0]
glyphs = []
for g in range(numglyphs):
    a, b = font.loca(g)
    glyphs.append(bytes(d[glyf_off + a:glyf_off + b]))

# "5" redesenhado: no negrito original ele tem o topo redondo e fica igual ao "S" ("50" parecia "S0").
# Mesma grade e espessura das outras letras, com topo reto e quinas vivas.
CUSTOM = {
    '5': [[(149, -11), (149, 74), (61, 74), (61, 211), (188, 211), (188, 125), (414, 125), (414, 331),
           (61, 331), (61, 638), (542, 638), (542, 552), (188, 552), (188, 416), (453, 416), (453, 331),
           (542, 331), (542, 74), (453, 74), (453, -11)]],
    # B: lado esquerdo reto (no original as quinas são redondas e ele vira um "S"/"8")
    'B': [[(61, -11), (61, 638), (453, 638), (453, 552), (542, 552), (542, 331), (453, 331), (453, 297),
           (542, 297), (542, 74), (453, 74), (453, -11)],
          [(188, 125), (414, 125), (414, 246), (237, 246), (237, 381), (414, 381), (414, 501), (188, 501)]],
    # Z: quinas vivas em cima à direita e embaixo à esquerda (no original ele vira um "2")
    'Z': [[(61, -11), (61, 297), (149, 297), (149, 381), (414, 381), (414, 501), (188, 501), (188, 416),
           (61, 416), (61, 638), (542, 638), (542, 331), (453, 331), (453, 246), (188, 246), (188, 125),
           (414, 125), (414, 211), (542, 211), (542, -11)]],
}
for ch, contours in CUSTOM.items():
    g = font.gid(ch)
    head, ends, instr, flags, xs, ys = read_simple(glyphs[g])
    pts = [pt for c in contours for pt in c]
    new_ends, n = [], 0
    for c in contours:
        n += len(c); new_ends.append(n - 1)
    glyphs[g] = write_simple((len(contours), 0, 0, 0, 0), new_ends, instr, [1] * len(pts), [x for x, _ in pts], [y for _, y in pts])
    print(f'{ch}: redesenhado')

for ch, remap in CHANGES.items():
    g = font.gid(ch)
    head, ends, instr, flags, xs, ys = read_simple(glyphs[g])
    ys = [remap.get(y, y) if x >= 414 else y for x, y in zip(xs, ys)]
    glyphs[g] = write_simple(head, ends, instr, flags, xs, ys)
    print(f'{ch}: abertura alterada')

# Remonta glyf + loca (formato longo) e o arquivo inteiro
new_glyf, offsets = bytearray(), []
for gb in glyphs:
    offsets.append(len(new_glyf))
    new_glyf += gb
    while len(new_glyf) % 4: new_glyf += b'\0'
offsets.append(len(new_glyf))
new_loca = b''.join(struct.pack('>I', o) for o in offsets)

tables = {}
for tag, (off, ln) in font.tables.items():
    tables[tag] = bytearray(d[off:off + ln])
tables['glyf'] = new_glyf
tables['loca'] = bytearray(new_loca)
struct.pack_into('>h', tables['head'], 50, 1)     # indexToLocFormat = longo
struct.pack_into('>I', tables['head'], 8, 0)      # checkSumAdjustment recalculado abaixo


def checksum(b):
    b = bytes(b) + b'\0' * ((4 - len(b) % 4) % 4)
    return sum(struct.unpack('>%dI' % (len(b) // 4), b)) & 0xFFFFFFFF


tags = sorted(tables)
num = len(tags)
es = max(k for k in range(16) if 2 ** k <= num)
out = bytearray(struct.pack('>IHHHH', 0x00010000, num, 16 * 2 ** es, es, num * 16 - 16 * 2 ** es))
offset = 12 + 16 * num
body = bytearray()
for tag in tags:
    t = tables[tag]
    out += struct.pack('>4sIII', tag.encode(), checksum(t), offset + len(body), len(t))
    body += t
    while len(body) % 4: body += b'\0'
out += body
adj = (0xB1B0AFBA - checksum(out)) & 0xFFFFFFFF
head_pos = 12 + 16 * num + sum(((len(tables[t]) + 3) // 4) * 4 for t in tags[:tags.index('head')])
struct.pack_into('>I', out, head_pos + 8, adj)
open(dst, 'wb').write(out)
print('fonte gravada:', dst, len(out), 'bytes')
