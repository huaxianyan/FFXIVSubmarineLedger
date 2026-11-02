#!/usr/bin/env python3
"""生成程序图标：Assets/app.ico（多尺寸）+ Assets/app.png（窗口用）。

纯标准库，不装 Pillow。图形用有符号距离场（SDF）画，边缘自带抗锯齿，
每个尺寸单独按矢量重画，所以 16 px 也是清晰的，不是大图缩下来的糊影。

改样式只改下面「设计参数」那一段，坐标都在 256×256 的设计空间里。

用法：
    python tools/make_icon.py            # 写入 Assets/
    python tools/make_icon.py --preview  # 顺便在终端打 ASCII 预览，肉眼核对
"""

from __future__ import annotations

import math
import os
import struct
import sys
import zlib

DESIGN = 256.0
SIZES = (16, 24, 32, 48, 64, 128, 256)

# ---------------------------------------------------------------- 设计参数

BADGE_INSET = 4.0
BADGE_RADIUS = 56.0

SEA_TOP = "#1E5278"
SEA_BOTTOM = "#0B1E2E"
GLOW = "#3C8FCB"

HULL_LIGHT = "#EDF6FD"
HULL_DARK = "#8FB4D2"
HULL_EDGE = "#12304A"

PORTHOLE_AMBER = "#FFDA92"
PORTHOLE_DEEP = "#E08A2A"

COIN_FACE = "#F4D079"
COIN_DEEP = "#C4922F"
COIN_RIM = "#8A6520"
COIN_SHINE = "#FFF6D8"

GOLD_LINE = "#D9B45C"


# ---------------------------------------------------------------- 颜色工具

def rgb(text: str) -> tuple[float, float, float]:
    text = text.lstrip("#")
    return tuple(int(text[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def mix(a, b, t: float):
    t = clamp(t)
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def clamp(x: float, lo: float = 0.0, hi: float = 1.0) -> float:
    return lo if x < lo else hi if x > hi else x


def over(dst, src, alpha: float):
    """把 src 以 alpha 叠到 dst 上（两者都是 RGBA）。"""
    if alpha <= 0.0:
        return dst
    a = dst[3] + alpha * (1.0 - dst[3])
    if a <= 0.0:
        return (0.0, 0.0, 0.0, 0.0)
    out = []
    for i in range(3):
        out.append((src[i] * alpha + dst[i] * dst[3] * (1.0 - alpha)) / a)
    return (out[0], out[1], out[2], a)


# ---------------------------------------------------------------- 距离场

def sd_round_rect(px, py, cx, cy, half_w, half_h, radius):
    qx = abs(px - cx) - (half_w - radius)
    qy = abs(py - cy) - (half_h - radius)
    return math.hypot(max(qx, 0.0), max(qy, 0.0)) + min(max(qx, qy), 0.0) - radius


def sd_capsule(px, py, ax, ay, bx, by, radius):
    pax, pay = px - ax, py - ay
    bax, bay = bx - ax, by - ay
    denom = bax * bax + bay * bay
    h = clamp((pax * bax + pay * bay) / denom) if denom else 0.0
    return math.hypot(pax - bax * h, pay - bay * h) - radius


def sd_circle(px, py, cx, cy, radius):
    return math.hypot(px - cx, py - cy) - radius


def cover(d_px: float) -> float:
    """距离（像素）转覆盖度，1 px 宽的过渡带就是抗锯齿。"""
    return clamp(0.5 - d_px)


def band(d_px: float, half_width: float) -> float:
    """描边：距离绝对值落在半宽内。"""
    return clamp(half_width + 0.5 - abs(d_px))


# ---------------------------------------------------------------- 画面

# 潜艇：艇身、指挥塔、潜望镜、螺旋桨
HULL = (54.0, 140.0, 184.0, 140.0, 31.0)
TOWER = (104.0, 111.0, 146.0, 111.0, 18.0)
PERISCOPE = (125.0, 95.0, 125.0, 76.0, 5.5)
PERISCOPE_HEAD = (125.0, 76.0, 141.0, 76.0, 4.2)
PROP_HUB = (205.0, 140.0, 7.5)

PORTHOLE = (80.0, 140.0, 20.0)

COINS = ((197.0, 202.0, 21.0),)
BUBBLES = ((48.0, 100.0, 5.0), (63.0, 82.0, 3.4))


def shade(px: float, py: float, scale: float, palette) -> tuple[float, float, float, float]:
    """算一个像素的颜色。px/py 在设计空间，scale 是「设计单位 → 像素」的比例。"""
    sea_top, sea_bottom, glow = palette[0], palette[1], palette[2]

    def px_(d):
        return d * scale

    result = (0.0, 0.0, 0.0, 0.0)

    # 1) 圆角方形底：深海竖直渐变
    d_badge = sd_round_rect(px, py, 128, 128, 128 - BADGE_INSET, 128 - BADGE_INSET, BADGE_RADIUS)
    a_badge = cover(px_(d_badge))
    if a_badge > 0:
        base = mix(sea_top, sea_bottom, (py - BADGE_INSET) / (DESIGN - 2 * BADGE_INSET))
        result = over(result, base, a_badge)

        # 上方一团柔光，让底不至于死板
        glow_d = max(sd_circle(px, py, 128, 70, 150), d_badge)
        halo = cover(px_(glow_d)) * 0.32
        result = over(result, glow, halo)

        # FF14 那圈细金线
        gold = rgb(GOLD_LINE)
        line_d = abs(sd_round_rect(px, py, 128, 128, 116, 116, 50))
        result = over(result, gold, band(px_(line_d), 1.0 * scale + 0.4) * 0.75 * a_badge)

    if a_badge <= 0:
        return result

    # 2) 气泡
    bubble = rgb("#9FD0EC")
    for cx, cy, r in BUBBLES:
        a = cover(px_(sd_circle(px, py, cx, cy, r))) * 0.45
        result = over(result, bubble, a)

    # 3) 螺旋桨（在艇身后面，先画）
    prop = rgb("#7FA6C4")
    hub = cover(px_(sd_circle(px, py, *PROP_HUB)))
    blades = 0.0
    for angle in (0.0, 120.0, 240.0):
        rad = math.radians(angle)
        bx = PROP_HUB[0] + math.cos(rad) * 15
        by = PROP_HUB[1] + math.sin(rad) * 15
        blades = max(blades, cover(px_(sd_capsule(px, py, PROP_HUB[0], PROP_HUB[1], bx, by, 4.5))))
    result = over(result, prop, max(hub, blades))

    # 4) 艇身 + 指挥塔 + 潜望镜
    d_hull = sd_capsule(px, py, *HULL)
    d_tower = sd_capsule(px, py, *TOWER)
    d_scope = min(
        sd_capsule(px, py, *PERISCOPE),
        sd_capsule(px, py, *PERISCOPE_HEAD),
    )
    d_body = min(d_hull, d_tower, d_scope)

    a_body = cover(px_(d_body))
    if a_body > 0:
        hull_light, hull_dark, hull_edge = palette[3], palette[4], palette[5]
        t = clamp((py - 109.0) / 62.0)
        result = over(result, mix(hull_light, hull_dark, t), a_body)

        # 描边：深色细线，大尺寸下让轮廓更实
        result = over(result, hull_edge, band(px_(d_body), 1.4 * scale + 0.3) * 0.85)

    # 5) 舷窗：外圈 + 琥珀色玻璃 + 一圈光晕
    amber, deep = palette[6], palette[7]
    cx, cy, r = PORTHOLE
    glow_a = cover(px_(sd_circle(px, py, cx, cy, r + 13))) * 0.45
    result = over(result, amber, glow_a)

    d_ring = sd_circle(px, py, cx, cy, r)
    a_ring = cover(px_(d_ring))
    if a_ring > 0:
        result = over(result, palette[5], a_ring)

    d_glass = sd_circle(px, py, cx, cy, r - 3.2)
    a_glass = cover(px_(d_glass))
    if a_glass > 0:
        result = over(result, mix(amber, deep, clamp((py - (cy - r)) / (2 * r)) * 0.45), a_glass)

        # 玻璃上的一点高光
        hi = cover(px_(sd_circle(px, py, cx - 3.5, cy - 4.0, r * 0.32))) * 0.75
        result = over(result, rgb("#FFF4D6"), hi)

    # 6) 金币：两枚叠着，右下角
    face, deep_coin, rim, shine = palette[8], palette[9], palette[10], palette[11]
    for cx, cy, r in COINS:
        a = cover(px_(sd_circle(px, py, cx, cy, r)))
        if a <= 0:
            continue
        result = over(result, rim, a)
        inner = cover(px_(sd_circle(px, py, cx, cy, r - 2.6)))
        if inner > 0:
            result = over(result, mix(face, deep_coin, clamp((py - (cy - r)) / (2 * r))), inner)
            arc = cover(px_(sd_circle(px, py, cx - r * 0.30, cy - r * 0.34, r * 0.34))) * 0.7
            result = over(result, shine, arc * inner)

    return result


def render(size: int) -> bytes:
    """渲染一张 size×size 的 RGBA。"""
    scale = size / DESIGN
    palette = (
        rgb(SEA_TOP), rgb(SEA_BOTTOM), rgb(GLOW),
        rgb(HULL_LIGHT), rgb(HULL_DARK), rgb(HULL_EDGE),
        rgb(PORTHOLE_AMBER), rgb(PORTHOLE_DEEP),
        rgb(COIN_FACE), rgb(COIN_DEEP), rgb(COIN_RIM), rgb(COIN_SHINE),
    )

    rows = bytearray()
    for y in range(size):
        rows.append(0)  # PNG 每行的 filter 字节
        py = (y + 0.5) / scale
        for x in range(size):
            px = (x + 0.5) / scale
            r, g, b, a = shade(px, py, scale, palette)
            rows += bytes((
                int(clamp(r) * 255 + 0.5),
                int(clamp(g) * 255 + 0.5),
                int(clamp(b) * 255 + 0.5),
                int(clamp(a) * 255 + 0.5),
            ))
    return bytes(rows)


def png_bytes(size: int, raw: bytes) -> bytes:
    def chunk(tag: bytes, data: bytes) -> bytes:
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    header = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", header)
            + chunk(b"IDAT", zlib.compress(raw, 9))
            + chunk(b"IEND", b""))


def ico_bytes(images: dict[int, bytes]) -> bytes:
    """ICO 容器，每项都存 PNG（Windows Vista 以后都认）。"""
    entries = []
    offset = 6 + 16 * len(images)
    for size in sorted(images):
        data = images[size]
        entries.append(struct.pack(
            "<BBBBHHII",
            0 if size >= 256 else size,
            0 if size >= 256 else size,
            0, 0, 1, 32, len(data), offset))
        offset += len(data)

    out = struct.pack("<HHH", 0, 1, len(images)) + b"".join(entries)
    for size in sorted(images):
        out += images[size]
    return out


def preview(size: int = 48) -> None:
    """在终端打一张 ASCII 预览：靠亮度分档，够看出轮廓对不对。"""
    raw = render(size)
    ramp = " .:-=+*#%@"
    stride = size * 4 + 1
    print(f"--- {size}×{size} 预览 ---")
    for y in range(size):
        line = []
        for x in range(size):
            i = y * stride + 1 + x * 4
            r, g, b, a = raw[i], raw[i + 1], raw[i + 2], raw[i + 3]
            if a < 24:
                line.append(" ")
                continue
            lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0
            line.append(ramp[int(clamp(lum) * (len(ramp) - 1))])
        print("".join(line))


def sheet(sizes=(256, 64, 48, 32, 24, 16)) -> bytes:
    """拼一张对照图：同一图标在浅色与深色背景上、各个尺寸的样子。

    图标在深色任务栏与浅色资源管理器里都得能看，这张图就是用来核对的。
    """
    light = (244, 246, 248)
    dark = (26, 30, 36)
    pad = 24
    big = max(sizes)
    width = pad * 3 + big * 2
    height = pad * 2 + big + 24

    canvas = [[(*light, 255)] * width for _ in range(height)]
    for y in range(height):
        for x in range(width // 2, width):
            canvas[y][x] = (*dark, 255)

    rendered = {size: render(size) for size in sizes}

    def blit(size: int, left: int, top: int) -> None:
        raw = rendered[size]
        stride = size * 4 + 1
        for y in range(size):
            for x in range(size):
                i = y * stride + 1 + x * 4
                r, g, b, a = raw[i], raw[i + 1], raw[i + 2], raw[i + 3]
                if a == 0:
                    continue
                dst = canvas[top + y][left + x]
                t = a / 255.0
                canvas[top + y][left + x] = (
                    int(r * t + dst[0] * (1 - t) + 0.5),
                    int(g * t + dst[1] * (1 - t) + 0.5),
                    int(b * t + dst[2] * (1 - t) + 0.5),
                    255,
                )

    # 左浅右深，各放一枚 256 的；下面沿基线排小尺寸
    blit(big, pad, pad)
    blit(big, width // 2 + pad, pad)

    x = pad
    for size in sizes[1:]:
        blit(size, x, pad + big - size + 8)
        x += size + 18

    x = width // 2 + pad
    for size in sizes[1:]:
        blit(size, x, pad + big - size + 8)
        x += size + 18

    raw = bytearray()
    for row in canvas:
        raw.append(0)
        for r, g, b, a in row:
            raw += bytes((r, g, b, a))
    return png_bytes(width, raw)


def main() -> int:
    here = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    assets = os.path.join(here, "src", "FFXIVSubmarineLedger.Desktop", "Assets")
    os.makedirs(assets, exist_ok=True)

    images = {size: png_bytes(size, render(size)) for size in SIZES}

    ico_path = os.path.join(assets, "app.ico")
    with open(ico_path, "wb") as handle:
        handle.write(ico_bytes(images))

    png_path = os.path.join(assets, "app.png")
    with open(png_path, "wb") as handle:
        handle.write(images[256])

    print(f"写入 {ico_path}（{len(SIZES)} 个尺寸，{os.path.getsize(ico_path)} 字节）")
    print(f"写入 {png_path}（{os.path.getsize(png_path)} 字节）")

    if "--sheet" in sys.argv:
        docs = os.path.join(here, "docs")
        os.makedirs(docs, exist_ok=True)

        # README 顶部那张：单枚 256 的图标
        icon_png = os.path.join(docs, "icon.png")
        with open(icon_png, "wb") as handle:
            handle.write(images[256])

        # 对照图：浅色与深色背景、各个尺寸
        sheet_path = os.path.join(docs, "icon-sizes.png")
        with open(sheet_path, "wb") as handle:
            handle.write(sheet())

        print(f"写入 {icon_png}（{os.path.getsize(icon_png)} 字节）")
        print(f"写入 {sheet_path}（{os.path.getsize(sheet_path)} 字节）")

    if "--preview" in sys.argv:
        preview(48)
        preview(16)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
