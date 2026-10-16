"""验证网格检测在不同 UI 倍率下的尺度鲁棒性。

用图像缩放模拟 UI 倍率变化（注意：真实 UI 缩放由游戏重新渲染，边缘更锐利，
本测试的缩放会引入插值模糊，因此结果应视为保守下界）。
"""

import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import analyze_grid as ag

BASE_PERIOD = 69.0


def detect(im):
    w, h = im.size
    px = list(im.getdata())
    ce = ag.col_edges(px, w, h)
    re_ = ag.row_edges(px, w, h)
    pc = ag.dominant_period(ce, 6, max(7, w // 2))
    pr = ag.dominant_period(re_, 6, max(7, h // 2))
    return pc, pr


def main():
    src = sys.argv[1]
    base = Image.open(src).convert("L")
    w0, h0 = base.size
    print("原始: %d x %d   基准格边长 %.0f px" % (w0, h0, BASE_PERIOD))
    print()
    print("%-7s %-11s %-9s %-24s %-24s" % ("倍率", "尺寸", "期望", "检出(列向)", "检出(行向)"))
    print("-" * 80)
    for scale in [0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 3.0]:
        w = max(24, int(round(w0 * scale)))
        h = max(24, int(round(h0 * scale)))
        im = base.resize((w, h), Image.LANCZOS)
        pc, pr = detect(im)
        exp = BASE_PERIOD * scale
        cs = "未检出" if not pc else "p=%-3d c=%.2f" % pc
        rs = "未检出" if not pr else "p=%-3d c=%.2f" % pr
        print("%-7s %-11s %-9s %-24s %-24s" % (
            "%.2fx" % scale, "%dx%d" % (w, h), "%.0f" % exp, cs, rs))


if __name__ == "__main__":
    main()
