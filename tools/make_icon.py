#!/usr/bin/env python3
"""生成程序图标：Assets/app.ico（多尺寸）+ Assets/app.png（窗口用）。

画面是一枚金币，币面浮雕一艘潜艇，风格照着游戏里的金币图标来：
金铜渐变、亮面高光、深色描边、压出来的立体感。

纯标准库，不装 Pillow。图形用有符号距离场（SDF）画，边缘自带抗锯齿，
每个尺寸单独按矢量重画，所以 16 px 也是清晰的，不是大图缩下来的糊影。

改样式只改下面「设计参数」那一段，坐标都在 256×256 的设计空间里。

用法：
    python tools/make_icon.py             # 写入 Assets/
    python tools/make_icon.py --sheet     # 顺带出 docs/ 的两张图
    python tools/make_icon.py --preview   # 在终端打 ASCII 预览，肉眼核对
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

COIN_RADIUS = 118.0
COIN_CENTER = (128.0, 128.0)
BEZEL_WIDTH = 13.0        # 外圈亮边的宽度
FIELD_RADIUS = COIN_RADIUS - BEZEL_WIDTH - 2.0

OUTLINE = "#2A1D0C"
BEZEL_LIGHT = "#FFF0BC"
BEZEL_DARK = "#7E5C2A"
GROOVE = "#4A3418"

FIELD_LIGHT = "#F2D79F"
FIELD_DARK = "#B98A42"
FIELD_EDGE = "#8A6531"

RELIEF_TOP = "#FFF7DA"
PROP_LIGHT = "#FFF8DC"
RELIEF_BOTTOM = "#E2C079"
RELIEF_EDGE = "#4A3418"

# 潜艇浮雕：艇身、指挥塔、潜望镜、螺旋桨
# 艇身右端收在 184、桨毂放 192：中间留一道窄缝，不然螺旋桨会被艇身吞掉
# （胶囊的端头是圆的，半径会多伸出 24，之前桨毂整个落在艇身里）。
# 「含螺旋桨」的轮廓要正好落在圆心，币面才不会一半空一半挤。
HULL = (70.0, 141.0, 156.0, 141.0, 24.0)
TOWER = (110.0, 118.0, 138.0, 118.0, 12.5)
PERISCOPE = (124.0, 105.0, 124.0, 92.0, 3.8)
PERISCOPE_HEAD = (124.0, 92.0, 135.0, 92.0, 3.0)
PROP_HUB = (194.0, 141.0, 6.5)
PORTHOLE = (86.0, 141.0, 10.0)

# 桨叶：一根朝下、两根朝上斜，侧视下最像螺旋桨
PROP_ANGLES = (90.0, 210.0, 330.0)
PROP_BLADE_LENGTH = 16.0
PROP_BLADE_RADIUS = 5.0


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

def body_distance(px: float, py: float) -> float:
    """艇身、指挥塔、潜望镜。"""
    return min(
        sd_capsule(px, py, *HULL),
        sd_capsule(px, py, *TOWER),
        sd_capsule(px, py, *PERISCOPE),
        sd_capsule(px, py, *PERISCOPE_HEAD),
    )


def propeller_distance(px: float, py: float) -> float:
    """桨毂与三片桨叶。单独算，因为它用更亮的金属色，不然和艇身糊成一片。"""
    d = sd_circle(px, py, *PROP_HUB)

    for angle in PROP_ANGLES:
        radians = math.radians(angle)
        bx = PROP_HUB[0] + math.cos(radians) * PROP_BLADE_LENGTH
        by = PROP_HUB[1] + math.sin(radians) * PROP_BLADE_LENGTH
        d = min(d, sd_capsule(px, py, PROP_HUB[0], PROP_HUB[1], bx, by, PROP_BLADE_RADIUS))

    return d


def submarine_distance(px: float, py: float) -> float:
    """整艘潜艇的轮廓（投影用）。"""
    return min(body_distance(px, py), propeller_distance(px, py))


def shade(px: float, py: float, scale: float) -> tuple[float, float, float, float]:
    """算一个像素的颜色。px/py 在设计空间，scale 是「设计单位 → 像素」的比例。"""
    cx, cy = COIN_CENTER

    def px_(value: float) -> float:
        return value * scale

    result = (0.0, 0.0, 0.0, 0.0)

    distance = sd_circle(px, py, cx, cy, COIN_RADIUS)
    if distance > 0:
        return result

    # 1) 外圈：金铜竖直渐变，上亮下暗，像被光照着的金属边
    vertical = clamp((py - (cy - COIN_RADIUS)) / (2 * COIN_RADIUS))
    bezel = mix(rgb(BEZEL_LIGHT), rgb(BEZEL_DARK), vertical * 0.9 + 0.05)
    result = over(result, bezel, cover(px_(distance)))

    # 2) 外轮廓线。半宽只跟尺寸走：加固定的像素底会让它在小尺寸下比整条亮边还宽，
    #    把外圈整个涂黑，金币看着就剩一小圈。
    result = over(result, rgb(OUTLINE), band(px_(distance), 0.9 * scale + 0.3) * 0.95)

    # 3) 外圈与币面之间的凹槽
    groove_d = abs(sd_circle(px, py, cx, cy, COIN_RADIUS - BEZEL_WIDTH))
    result = over(result, rgb(GROOVE), band(px_(groove_d), 1.0 * scale) * 0.85)

    # 4) 币面：渐变 + 靠边压暗，做出微微隆起的弧度
    field_d = sd_circle(px, py, cx, cy, FIELD_RADIUS)
    field_alpha = cover(px_(field_d))
    if field_alpha > 0:
        field = mix(rgb(FIELD_LIGHT), rgb(FIELD_DARK), vertical * 0.85)
        rim = clamp((-field_d) / FIELD_RADIUS)
        field = mix(field, rgb(FIELD_EDGE), clamp((rim - 0.72) / 0.28) * 0.55)
        result = over(result, field, field_alpha)

    # 5) 潜艇浮雕：先投影，再本体与螺旋桨
    # 投影：整体往右下错开，看着像凸出版面
    shadow_d = submarine_distance(px - 2.6, py - 3.0)
    result = over(result, rgb(RELIEF_EDGE), cover(px_(shadow_d)) * 0.45)

    body_d = body_distance(px, py)
    body_alpha = cover(px_(body_d))
    if body_alpha > 0:
        relief = mix(rgb(RELIEF_TOP), rgb(RELIEF_BOTTOM), clamp((py - 88.0) / 77.0))
        result = over(result, relief, body_alpha)

        # 左上缘高光：本体减去「往右下偏移后的本体」，剩下的那道月牙就是受光边。
        # 直接拿偏移后的覆盖度当高光会把整个艇身洗白，纵向渐变全被抹平。
        lit_d = body_distance(px - 2.0, py - 2.6)
        result = over(result, rgb(RELIEF_TOP), body_alpha * (1.0 - cover(px_(lit_d))) * 0.75)

    # 螺旋桨用更亮的金属色，否则和艇身同色、糊成一片，尾巴看着是秃的
    prop_alpha = cover(px_(propeller_distance(px, py)))
    if prop_alpha > 0:
        result = over(result, rgb(PROP_LIGHT), prop_alpha)
        prop_lit = propeller_distance(px - 1.4, py - 1.8)
        result = over(result, (1.0, 1.0, 1.0), prop_alpha * (1.0 - cover(px_(prop_lit))) * 0.45)

        # 不加描边：潜望镜这类细部件在小尺寸下只有半个像素宽，整根都落在「边缘」上，
        # 描边会把它们涂成一团黑。立体感交给投影与上缘高光。

    # 6) 舷窗：凸起的小圆环，玻璃反光
    hole_d = sd_circle(px, py, *PORTHOLE)
    hole_alpha = cover(px_(hole_d))
    if hole_alpha > 0:
        # 环用暗金而不是全黑，否则看着像艇身上破了个洞
        result = over(result, rgb(RELIEF_BOTTOM), hole_alpha)
        glass_d = sd_circle(px, py, PORTHOLE[0], PORTHOLE[1], PORTHOLE[2] - 2.6)
        glass_alpha = cover(px_(glass_d))
        result = over(result, rgb(RELIEF_TOP), glass_alpha)
        glint = cover(px_(sd_circle(px, py, PORTHOLE[0] - 2.4, PORTHOLE[1] - 2.6, 2.2))) * 0.9
        result = over(result, (1.0, 1.0, 1.0), glint * glass_alpha)

    return result


def render(size: int) -> bytes:
    """渲染一张 size×size 的 RGBA 原始数据（PNG 行格式，每行前面一个 0 字节）。"""
    scale = size / DESIGN
    out = bytearray()

    for y in range(size):
        out.append(0)
        py = (y + 0.5) / scale
        for x in range(size):
            px = (x + 0.5) / scale
            r, g, b, a = shade(px, py, scale)
            out += bytes((
                int(clamp(r) * 255 + 0.5),
                int(clamp(g) * 255 + 0.5),
                int(clamp(b) * 255 + 0.5),
                int(clamp(a) * 255 + 0.5),
            ))

    return bytes(out)


# ---------------------------------------------------------------- 出图

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


def sheet(sizes=(256, 64, 48, 32, 24, 16)) -> bytes:
    """浅色与深色背景的尺寸对照图。图标在两种底上都得能看。"""
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

    blit(big, pad, pad)
    blit(big, width // 2 + pad, pad)

    for base in (pad, width // 2 + pad):
        x = base
        for size in sizes[1:]:
            blit(size, x, pad + big - size + 8)
            x += size + 18

    raw = bytearray()
    for row in canvas:
        raw.append(0)
        for r, g, b, a in row:
            raw += bytes((r, g, b, a))
    return png_bytes(width, raw)


def preview(size: int = 32) -> None:
    """终端里的 ASCII 预览：按亮度分档，够看出轮廓与留白。"""
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

        icon_png = os.path.join(docs, "icon.png")
        with open(icon_png, "wb") as handle:
            handle.write(images[256])

        sheet_path = os.path.join(docs, "icon-sizes.png")
        with open(sheet_path, "wb") as handle:
            handle.write(sheet())

        print(f"写入 {icon_png}（{os.path.getsize(icon_png)} 字节）")
        print(f"写入 {sheet_path}（{os.path.getsize(sheet_path)} 字节）")

    if "--preview" in sys.argv:
        preview(32)
        preview(16)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
