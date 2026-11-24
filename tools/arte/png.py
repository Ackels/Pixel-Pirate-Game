"""PNG puro em Python (sem Pillow): ler, escrever, ampliar com grade."""
import struct, zlib, sys


def read(path):
    data = open(path, 'rb').read()
    assert data[:8] == b'\x89PNG\r\n\x1a\n'
    pos = 8
    idat = b''
    plte = None
    trns = None
    while pos < len(data):
        ln, typ = struct.unpack('>I4s', data[pos:pos + 8])
        chunk = data[pos + 8:pos + 8 + ln]
        pos += 12 + ln
        if typ == b'IHDR':
            w, h, bd, ct, _, _, il = struct.unpack('>IIBBBBB', chunk)
        elif typ == b'PLTE':
            plte = [tuple(chunk[i:i + 3]) for i in range(0, len(chunk), 3)]
        elif typ == b'tRNS':
            trns = chunk
        elif typ == b'IDAT':
            idat += chunk
        elif typ == b'IEND':
            break
    assert il == 0, 'interlaced not supported'
    raw = zlib.decompress(idat)
    chans = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[ct]
    bpp_bits = chans * bd
    stride = (w * bpp_bits + 7) // 8
    bpp = max(1, bpp_bits // 8)
    rows = []
    prev = bytearray(stride)
    i = 0
    for y in range(h):
        f = raw[i]; i += 1
        line = bytearray(raw[i:i + stride]); i += stride
        for x in range(stride):
            a = line[x - bpp] if x >= bpp else 0
            b = prev[x]
            c = prev[x - bpp] if x >= bpp else 0
            if f == 1: line[x] = (line[x] + a) & 255
            elif f == 2: line[x] = (line[x] + b) & 255
            elif f == 3: line[x] = (line[x] + ((a + b) >> 1)) & 255
            elif f == 4:
                p = a + b - c; pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pr = a if pa <= pb and pa <= pc else (b if pb <= pc else c)
                line[x] = (line[x] + pr) & 255
        rows.append(line); prev = line
    px = []
    for line in rows:
        row = []
        if bd < 8:
            vals = []
            for byte in line:
                for k in range(8 // bd):
                    vals.append((byte >> (8 - bd * (k + 1))) & ((1 << bd) - 1))
            vals = vals[:w * chans]
        elif bd == 16:
            vals = [line[j] for j in range(0, len(line), 2)]
        else:
            vals = list(line)
        for x in range(w):
            if ct == 6: r, g, b_, a_ = vals[x * 4:x * 4 + 4]
            elif ct == 2:
                r, g, b_ = vals[x * 3:x * 3 + 3]; a_ = 255
            elif ct == 0:
                v = vals[x] * (255 // ((1 << bd) - 1)) if bd < 8 else vals[x]; r = g = b_ = v; a_ = 255
            elif ct == 4:
                r = g = b_ = vals[x * 2]; a_ = vals[x * 2 + 1]
            elif ct == 3:
                idx = vals[x]; r, g, b_ = plte[idx]; a_ = trns[idx] if trns and idx < len(trns) else 255
            row.append((r, g, b_, a_))
        px.append(row)
    return w, h, px


def write(path, w, h, px):
    raw = bytearray()
    for row in px:
        raw.append(0)
        for p in row:
            raw.extend(bytes(p))
    def chunk(t, d):
        c = struct.pack('>I', len(d)) + t + d
        return c + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    out = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
    out += chunk(b'IDAT', zlib.compress(bytes(raw), 9)) + chunk(b'IEND', b'')
    open(path, 'wb').write(out)


DIGITS = {
    '0': ['111', '101', '101', '101', '111'], '1': ['010', '110', '010', '010', '111'],
    '2': ['111', '001', '111', '100', '111'], '3': ['111', '001', '111', '001', '111'],
    '4': ['101', '101', '111', '001', '001'], '5': ['111', '100', '111', '001', '111'],
    '6': ['111', '100', '111', '101', '111'], '7': ['111', '001', '010', '010', '010'],
    '8': ['111', '101', '111', '101', '111'], '9': ['111', '101', '111', '001', '111'],
}


def grid(src, dst, cell=16, scale=3, region=None):
    """Amplia a imagem, desenha grade a cada `cell` px e numera colunas/linhas."""
    w, h, px = read(src)
    x0, y0, x1, y1 = region or (0, 0, w, h)
    margin = 14
    W, H = (x1 - x0) * scale + margin, (y1 - y0) * scale + margin
    out = [[(40, 40, 48, 255)] * W for _ in range(H)]
    for y in range(y0, y1):
        for x in range(x0, x1):
            r, g, b, a = px[y][x]
            # fundo xadrez para transparência
            bg = 90 if ((x // 4 + y // 4) % 2) else 70
            al = a / 255
            c = (int(r * al + bg * (1 - al)), int(g * al + bg * (1 - al)), int(b * al + bg * (1 - al)), 255)
            for dy in range(scale):
                for dx in range(scale):
                    out[margin + (y - y0) * scale + dy][margin + (x - x0) * scale + dx] = c
    for gx in range(x0, x1 + 1):
        if (gx % cell) == 0:
            X = margin + (gx - x0) * scale
            if X < W:
                for Y in range(margin, H): out[Y][X] = (255, 0, 255, 255)
    for gy in range(y0, y1 + 1):
        if (gy % cell) == 0:
            Y = margin + (gy - y0) * scale
            if Y < H:
                for X in range(margin, W): out[Y][X] = (255, 0, 255, 255)
    def text(s, X, Y):
        for ch in s:
            for r, line in enumerate(DIGITS[ch]):
                for c, v in enumerate(line):
                    if v == '1' and 0 <= Y + r < H and 0 <= X + c < W: out[Y + r][X + c] = (255, 255, 0, 255)
            X += 4
    for gx in range(x0 - x0 % cell, x1, cell):
        if gx >= x0: text(str(gx // cell), margin + (gx - x0) * scale + 2, 4)
    for gy in range(y0 - y0 % cell, y1, cell):
        if gy >= y0: text(str(gy // cell), 1, margin + (gy - y0) * scale + 2)
    write(dst, W, H, out)


if __name__ == '__main__':
    src, dst = sys.argv[1], sys.argv[2]
    cell = int(sys.argv[3]) if len(sys.argv) > 3 else 16
    scale = int(sys.argv[4]) if len(sys.argv) > 4 else 3
    region = tuple(map(int, sys.argv[5].split(','))) if len(sys.argv) > 5 else None
    grid(src, dst, cell, scale, region)
