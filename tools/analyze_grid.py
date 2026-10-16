"""背包截图网格结构分析：量出格子边长、间距、网格起点与空格子特征。

思路：先求边缘能量投影，再用自相关找主周期（格子边长），
最后用相位搜索把网格对齐到真实边界。
只依赖 Pillow。
"""

import sys
from PIL import Image, ImageDraw


def load(path):
    im = Image.open(path).convert("L")
    w, h = im.size
    return im, w, h, list(im.getdata())


def col_edges(px, w, h):
    out = [0.0] * w
    for x in range(1, w):
        s = 0
        for y in range(h):
            i = y * w + x
            s += abs(px[i] - px[i - 1])
        out[x] = s
    return out


def row_edges(px, w, h):
    out = [0.0] * h
    for y in range(1, h):
        base = y * w
        prev = base - w
        s = 0
        for x in range(w):
            s += abs(px[base + x] - px[prev + x])
        out[y] = s
    return out


def autocorr(sig, min_lag, max_lag):
    n = len(sig)
    mean = sum(sig) / n
    s = [v - mean for v in sig]
    denom = sum(v * v for v in s) or 1.0
    res = []
    for lag in range(min_lag, max_lag + 1):
        c = 0.0
        for i in range(n - lag):
            c += s[i] * s[i + lag]
        res.append((lag, c / denom))
    return res


def dominant_period(sig, min_lag, max_lag):
    res = autocorr(sig, min_lag, max_lag)
    if not res:
        return None
    peaks = []
    for i in range(1, len(res) - 1):
        if res[i][1] >= res[i - 1][1] and res[i][1] >= res[i + 1][1] and res[i][1] > 0:
            peaks.append(res[i])
    if not peaks:
        return max(res, key=lambda t: t[1])
    return max(peaks, key=lambda t: t[1])


def best_phase(sig, period):
    best = None
    for ph in range(int(round(period))):
        s = 0.0
        x = float(ph)
        while x < len(sig) - 0.5:
            s += sig[int(round(x))]
            x += period
        if best is None or s > best[1]:
            best = (ph, s)
    return best[0] if best else 0


def grid_lines(sig, period, phase):
    lines = []
    x = float(phase)
    while x < len(sig) - 0.5:
        lines.append(int(round(x)))
        x += period
    return lines


def patch_stats(px, w, xs, ys, r, c):
    x0, x1 = xs[c], xs[c + 1]
    y0, y1 = ys[r], ys[r + 1]
    n = 0
    s = 0
    s2 = 0
    for y in range(y0, y1):
        base = y * w
        for x in range(x0, x1):
            v = px[base + x]
            s += v
            s2 += v * v
            n += 1
    if n == 0:
        return None
    mean = s / n
    var = max(0.0, s2 / n - mean * mean)
    return mean, var ** 0.5


def main():
    path = sys.argv[1]
    out_prefix = sys.argv[2] if len(sys.argv) > 2 else "grid"

    im, w, h, px = load(path)
    print("== 图像 ==")
    print("size: %d x %d" % (w, h))
    print("gray: min=%d max=%d mean=%.1f" % (min(px), max(px), sum(px) / len(px)))

    ce = col_edges(px, w, h)
    re_ = row_edges(px, w, h)

    print()
    print("== 列方向：格子宽度 ==")
    pc = dominant_period(ce, 20, max(21, w // 2))
    if pc:
        period_c, corr_c = pc
        phase_c = best_phase(ce, period_c)
        xs = grid_lines(ce, period_c, phase_c)
        print("  period=%d  corr=%.3f  phase=%d" % (period_c, corr_c, phase_c))
        print("  列数≈%d  竖线: %s" % (len(xs) - 1, xs))
    else:
        print("  未检出")
        xs = []

    print()
    print("== 行方向：格子高度 ==")
    pr = dominant_period(re_, 20, max(21, h // 2))
    if pr:
        period_r, corr_r = pr
        phase_r = best_phase(re_, period_r)
        ys = grid_lines(re_, period_r, phase_r)
        print("  period=%d  corr=%.3f  phase=%d" % (period_r, corr_r, phase_r))
        print("  行数≈%d  横线: %s" % (len(ys) - 1, ys))
    else:
        print("  未检出")
        ys = []

    print()
    print("== 格位灰度 均值/标准差（空格子应低均值低方差）==")
    if len(xs) >= 2 and len(ys) >= 2:
        for r in range(len(ys) - 1):
            line = []
            for c in range(len(xs) - 1):
                st = patch_stats(px, w, xs, ys, r, c)
                line.append("----" if st is None else "%3.0f/%3.0f" % st)
            print("  ".join(line))

    scale = max(2, 900 // max(w, 1))
    vis = im.convert("RGB").resize((w * scale, h * scale), Image.NEAREST)
    d = ImageDraw.Draw(vis)
    for x in xs:
        d.line([(x * scale, 0), (x * scale, h * scale)], fill=(255, 0, 0), width=1)
    for y in ys:
        d.line([(0, y * scale), (w * scale, y * scale)], fill=(0, 255, 255), width=1)
    vis.save(out_prefix + "_grid.png")
    print()
    print("overlay saved:", out_prefix + "_grid.png", "x%d" % scale)


if __name__ == "__main__":
    main()
