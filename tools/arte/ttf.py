"""Leitura e escrita mínimas de fontes TrueType de pixel (contornos retos numa grade)."""
import struct


class TTF:
    def __init__(self, path):
        self.data = bytearray(open(path, 'rb').read())
        d = self.data
        num = struct.unpack('>H', d[4:6])[0]
        self.tables = {}
        for i in range(num):
            tag, cs, off, ln = struct.unpack('>4sIII', d[12 + 16 * i:28 + 16 * i])
            self.tables[tag.decode()] = (off, ln)
        self.upem = struct.unpack('>H', d[self.tables['head'][0] + 18:self.tables['head'][0] + 20])[0]
        self.locfmt = struct.unpack('>h', d[self.tables['head'][0] + 50:self.tables['head'][0] + 52])[0]

    def gid(self, ch):
        d = self.data
        co = self.tables['cmap'][0]
        n = struct.unpack('>H', d[co + 2:co + 4])[0]
        for i in range(n):
            pid, eid, off = struct.unpack('>HHI', d[co + 4 + 8 * i:co + 12 + 8 * i])
            o = co + off
            if struct.unpack('>H', d[o:o + 2])[0] != 4: continue
            segX2 = struct.unpack('>H', d[o + 6:o + 8])[0]; seg = segX2 // 2
            ends = struct.unpack('>%dH' % seg, d[o + 14:o + 14 + segX2]); st = o + 16 + segX2
            starts = struct.unpack('>%dH' % seg, d[st:st + segX2]); dl = st + segX2
            deltas = struct.unpack('>%dh' % seg, d[dl:dl + segX2]); ro = dl + segX2
            ranges = struct.unpack('>%dH' % seg, d[ro:ro + segX2])
            c = ord(ch)
            for k in range(seg):
                if starts[k] <= c <= ends[k]:
                    if ranges[k] == 0: return (c + deltas[k]) & 0xFFFF
                    a = ro + 2 * k + ranges[k] + 2 * (c - starts[k])
                    g = struct.unpack('>H', d[a:a + 2])[0]
                    return (g + deltas[k]) & 0xFFFF if g else 0
        return 0

    def loca(self, g):
        d = self.data; lo = self.tables['loca'][0]
        if self.locfmt == 0:
            return struct.unpack('>H', d[lo + 2 * g:lo + 2 * g + 2])[0] * 2, struct.unpack('>H', d[lo + 2 * g + 2:lo + 2 * g + 4])[0] * 2
        return struct.unpack('>I', d[lo + 4 * g:lo + 4 * g + 4])[0], struct.unpack('>I', d[lo + 4 * g + 4:lo + 4 * g + 8])[0]

    def contours(self, g):
        """Lista de contornos (listas de pontos (x, y) em unidades da fonte), só pontos na curva."""
        d = self.data
        a, b = self.loca(g)
        if a == b: return []
        o = self.tables['glyf'][0] + a
        nc = struct.unpack('>h', d[o:o + 2])[0]
        if nc < 0: return []   # glifo composto (não usado aqui)
        ends = struct.unpack('>%dH' % nc, d[o + 10:o + 10 + 2 * nc])
        p = o + 10 + 2 * nc
        il = struct.unpack('>H', d[p:p + 2])[0]; p += 2 + il
        npts = ends[-1] + 1
        flags = []
        while len(flags) < npts:
            f = d[p]; p += 1; flags.append(f)
            if f & 8:
                r = d[p]; p += 1; flags.extend([f] * r)
        xs, ys = [], []
        v = 0
        for f in flags:
            if f & 2: dv = d[p]; p += 1; v += dv if f & 16 else -dv
            elif not f & 16: v += struct.unpack('>h', d[p:p + 2])[0]; p += 2
            xs.append(v)
        v = 0
        for f in flags:
            if f & 4: dv = d[p]; p += 1; v += dv if f & 32 else -dv
            elif not f & 32: v += struct.unpack('>h', d[p:p + 2])[0]; p += 2
            ys.append(v)
        out, s = [], 0
        for e in ends:
            out.append([(xs[i], ys[i]) for i in range(s, e + 1)]); s = e + 1
        return out

    def bitmap(self, g, px):
        """Rasteriza o glifo numa grade de px unidades (regra par-ímpar no centro de cada pixel)."""
        cs = self.contours(g)
        if not cs: return {}
        allx = [x for c in cs for x, _ in c]; ally = [y for c in cs for _, y in c]
        on = set()
        for gy in range(int(min(ally) // px) - 1, int(max(ally) // px) + 2):
            for gx in range(int(min(allx) // px) - 1, int(max(allx) // px) + 2):
                cx, cy = (gx + 0.5) * px, (gy + 0.5) * px
                inside = False
                for c in cs:
                    for i in range(len(c)):
                        x1, y1 = c[i]; x2, y2 = c[(i + 1) % len(c)]
                        if (y1 > cy) != (y2 > cy) and cx < x1 + (cy - y1) * (x2 - x1) / (y2 - y1): inside = not inside
                if inside: on.add((gx, gy))
        return on
