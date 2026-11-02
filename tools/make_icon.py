#!/usr/bin/env python3
"""生成程序图标：Assets/app.ico（多尺寸）+ Assets/app.png（窗口用）。

画面是游戏内的金币图标（`tools/assets/gil-72.png`，取自 XIVAPI 的
`ui/icon/065000/065002_hr1.tex`）。源图只有 72×72，所以这里用 Catmull-Rom
双三次重采样逐尺寸生成，并按预乘 alpha 计算，避免边缘出现黑边。

纯标准库，不装 Pillow：PNG 的解码（含五种行滤波）、重采样、ICO 打包都在这里。

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

SIZES = (16, 24, 32, 48, 64, 128, 256)

# 图标四周留一点边，免得金币贴着边缘显得挤。
PADDING = 0.04

SOURCE = os.path.join("tools", "assets", "gil-72.png")


# ---------------------------------------------------------------- PNG 解码

def decode_png(data: bytes) -> tuple[int, int, list[list[tuple[int, int, int, int]]]]:
    """解 8 位 RGB/RGBA、非隔行的 PNG，五种行滤波都还原。"""
    pos, idat = 8, b""
    width = height = 0
    color_type = 6

    while pos < len(data):
        length = struct.unpack(">I", data[pos:pos + 4])[0]
        tag = data[pos + 4:pos + 8]
        body = data[pos + 8:pos + 8 + length]

        if tag == b"IHDR":
            width, height, depth, color_type, _, _, interlace = struct.unpack(">IIBBBBB", body)
            if depth != 8 or interlace != 0 or color_type not in (2, 6):
                raise ValueError(f"只支持 8 位 RGB/RGBA 非隔行 PNG，这份是 {depth} 位 / 类型 {color_type}")
        elif tag == b"IDAT":
            idat += body

        pos += 12 + length

    channels = 4 if color_type == 6 else 3
    raw = zlib.decompress(idat)
    stride = width * channels
    previous = bytearray(stride)
    rows: list[list[tuple[int, int, int, int]]] = []
    offset = 0

    for _ in range(height):
        method = raw[offset]
        offset += 1
        line = bytearray(raw[offset:offset + stride])
        offset += stride

        if method == 1:
            for i in range(channels, stride):
                line[i] = (line[i] + line[i - channels]) & 0xFF
        elif method == 2:
            for i in range(stride):
                line[i] = (line[i] + previous[i]) & 0xFF
        elif method == 3:
            for i in range(stride):
                left = line[i - channels] if i >= channels else 0
                line[i] = (line[i] + ((left + previous[i]) >> 1)) & 0xFF
        elif method == 4:
            for i in range(stride):
                left = line[i - channels] if i >= channels else 0
                up = previous[i]
                upleft = previous[i - channels] if i >= channels else 0
                pa, pb, pc = abs(up - upleft), abs(left - upleft), abs(left + up - 2 * upleft)
                predictor = left if (pa <= pb and pa <= pc) else (up if pb <= pc else upleft)
                line[i] = (line[i] + predictor) & 0xFF

        if channels == 4:
            rows.append([tuple(line[x * 4:x * 4 + 4]) for x in range(width)])
        else:
            rows.append([(line[x * 3], line[x * 3 + 1], line[x * 3 + 2], 255) for x in range(width)])

        previous = line

    return width, height, rows


# ---------------------------------------------------------------- 重采样

def _catmull_rom(x: float) -> float:
    ax = abs(x)
    if ax < 1.0:
        return 1.5 * ax ** 3 - 2.5 * ax ** 2 + 1.0
    if ax < 2.0:
        return -0.5 * ax ** 3 + 2.5 * ax ** 2 - 4.0 * ax + 2.0
    return 0.0


def _weights(source_size: int, target_size: int) -> list[list[tuple[int, float]]]:
    """每个目标像素对应的源像素与权重。缩小的时候按比例放宽核宽，免得丢细节。"""
    scale = source_size / target_size
    filter_scale = max(scale, 1.0)
    support = 2.0 * filter_scale
    result = []

    for index in range(target_size):
        center = (index + 0.5) * scale
        first = int(math.floor(center - support))
        last = int(math.ceil(center + support))
        taps = []
        total = 0.0

        for source_index in range(first, last):
            weight = _catmull_rom((source_index + 0.5 - center) / filter_scale)
            if weight == 0.0:
                continue
            # 越界的采样点夹到边缘，避免边缘变暗。
            clamped = min(max(source_index, 0), source_size - 1)
            taps.append((clamped, weight))
            total += weight

        result.append([(i, w / total) for i, w in taps])

    return result


def _alpha_bounds(rows, width: int, height: int) -> tuple[int, int, int, int]:
    """不透明像素的包围盒。"""
    left, top, right, bottom = width - 1, height - 1, 0, 0

    for y in range(height):
        for x in range(width):
            if rows[y][x][3] > 8:
                left = min(left, x)
                right = max(right, x)
                top = min(top, y)
                bottom = max(bottom, y)

    return left, top, right, bottom


def _byte(value: float) -> int:
    """钳到 0..255 并四舍五入。"""
    return int(min(max(value, 0.0), 255.0) + 0.5)


def resample(rows, source_width: int, source_height: int, size: int):
    """缩放到 size×size。全程按预乘 alpha 算，边缘不会发黑。"""
    horizontal = _weights(source_width, size)
    vertical = _weights(source_height, size)

    # 先横向
    intermediate = []
    for y in range(source_height):
        line = rows[y]
        out = []
        for taps in horizontal:
            r = g = b = a = 0.0
            for index, weight in taps:
                pr, pg, pb, pa = line[index]
                alpha = pa / 255.0
                r += pr * alpha * weight
                g += pg * alpha * weight
                b += pb * alpha * weight
                a += alpha * weight
            out.append((r, g, b, a))
        intermediate.append(out)

    # 再纵向
    result = []
    for taps in vertical:
        out = []
        for x in range(size):
            r = g = b = a = 0.0
            for index, weight in taps:
                pr, pg, pb, pa = intermediate[index][x]
                r += pr * weight
                g += pg * weight
                b += pb * weight
                a += pa * weight
            out.append((r, g, b, a))
        result.append(out)

    # 解预乘
    pixels = []
    for line in result:
        row = []
        for r, g, b, a in line:
            if a <= 1e-6:
                row.append((0, 0, 0, 0))
            else:
                # Catmull-Rom 有负瓣，重采样会越界，这里钳住。
                row.append((
                    _byte(r / a),
                    _byte(g / a),
                    _byte(b / a),
                    _byte(a * 255.0),
                ))
        pixels.append(row)
    return pixels


# ---------------------------------------------------------------- 出图

def render(size: int) -> bytes:
    """渲染一张 size×size 的 RGBA 原始数据（PNG 行格式，每行前面一个 0 字节）。"""
    global _source
    if _source is None:
        path = os.path.join(_REPO, SOURCE)
        with open(path, "rb") as handle:
            width, height, rows = decode_png(handle.read())

        # 源图四周自带透明边，先裁到不透明范围，否则各尺寸都偏小且不居中。
        left, top, right, bottom = _alpha_bounds(rows, width, height)
        rows = [row[left:right + 1] for row in rows[top:bottom + 1]]
        _source = (right - left + 1, bottom - top + 1, rows)

    source_width, source_height, rows = _source

    # 边距按像素算，左右上下相等；用比例算会在奇数尺寸上偏一格。
    margin = max(1, int(round(size * PADDING)))
    inner = max(1, size - 2 * margin)
    scaled = resample(rows, source_width, source_height, inner)
    offset = margin

    out = bytearray()
    for y in range(size):
        out.append(0)
        sy = y - offset
        for x in range(size):
            sx = x - offset
            if 0 <= sy < inner and 0 <= sx < inner:
                out += bytes(scaled[sy][sx])
            else:
                out += b"\x00\x00\x00\x00"
    return bytes(out)


_source = None
_REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


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
            line.append(ramp[int(min(max(lum, 0.0), 1.0) * (len(ramp) - 1))])
        print("".join(line))


def main() -> int:
    assets = os.path.join(_REPO, "src", "FFXIVSubmarineLedger.Desktop", "Assets")
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
        docs = os.path.join(_REPO, "docs")
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
