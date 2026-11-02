#!/usr/bin/env python3
"""把一张源图做成程序图标：Assets/app.ico（多尺寸）+ Assets/app.png。

源图是 `tools/icon-source.png`（1254×1254、纯黑背景的金币）。
流程：抠底 → 裁到不透明包围盒 → 补成正方形留边 → 面积平均重采样到各尺寸。

纯标准库，不装 Pillow。

几个不能省的细节（都踩过）：
- **按连通区域抠底，不套椭圆遮罩**。这枚币的边缘各角度半径在 472–490 之间，
  套圆形遮罩要么切掉一块，要么留一圈黑边。
- **重采样走预乘 alpha**。不预乘的话，透明区域的黑色会渗进边缘，一圈脏边。
- **面积平均而不是双线性**。1254 → 16 是 78 倍缩小，双线性只采 4 个点，
  细节全丢还起锯齿；面积平均按覆盖度累加，等于自带低通。
- 先裁到包围盒再居中，边距按像素算。不裁会偏小且不居中，16 px 看得出来。

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

SOURCE = os.path.join("tools", "icon-source.png")
SIZES = (16, 24, 32, 48, 64, 128, 256)
LARGEST = max(SIZES)

# 亮度低于这个值、且能从画布边界连通过来的像素算背景
BACKGROUND_LUMA = 24
# 四周留白，占最终边长的比例
MARGIN = 0.02

# 各尺寸的对比度补偿。源图的浮雕是细纹理，缩小后被平均掉，不补就是一块平色。
# 大尺寸保持原样，小尺寸逐级加码。
CONTRAST = {16: 1.9, 24: 1.7, 32: 1.45, 48: 1.25, 64: 1.1}

# 高光压缩：币缘有一圈接近纯白的高光，缩到 19 px 就成白斑，看着像脏点
HIGHLIGHT_KNEE = 0.80
HIGHLIGHT_SLOPE = 0.35


# ---------------------------------------------------------------- 读 PNG

def load_png(path: str):
    """只认 8 位、非隔行的灰度/RGB/RGBA——够用，不写通用解码器。"""
    data = open(path, "rb").read()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise SystemExit(f"{path} 不是 PNG")

    position = 8
    compressed = bytearray()
    palette = None
    transparency = None
    width = height = depth = color_type = interlace = 0

    while position < len(data):
        length = struct.unpack(">I", data[position:position + 4])[0]
        tag = data[position + 4:position + 8]
        body = data[position + 8:position + 8 + length]

        if tag == b"IHDR":
            width, height, depth, color_type, _, _, interlace = struct.unpack(">IIBBBBB", body)
        elif tag == b"PLTE":
            palette = body
        elif tag == b"tRNS":
            transparency = body
        elif tag == b"IDAT":
            compressed += body
        elif tag == b"IEND":
            break

        position += 12 + length

    if depth != 8 or interlace != 0 or color_type not in (0, 2, 3, 6):
        raise SystemExit(f"不支持的 PNG：位深 {depth}、颜色类型 {color_type}、隔行 {interlace}")

    channels = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[color_type]
    raw = zlib.decompress(bytes(compressed))
    stride = width * channels

    pixels = bytearray(height * stride)
    previous = bytearray(stride)
    cursor = 0

    for y in range(height):
        filter_type = raw[cursor]
        cursor += 1
        line = bytearray(raw[cursor:cursor + stride])
        cursor += stride

        if filter_type == 1:
            for i in range(channels, stride):
                line[i] = (line[i] + line[i - channels]) & 0xFF
        elif filter_type == 2:
            for i in range(stride):
                line[i] = (line[i] + previous[i]) & 0xFF
        elif filter_type == 3:
            for i in range(stride):
                left = line[i - channels] if i >= channels else 0
                line[i] = (line[i] + ((left + previous[i]) >> 1)) & 0xFF
        elif filter_type == 4:
            for i in range(stride):
                left = line[i - channels] if i >= channels else 0
                up = previous[i]
                up_left = previous[i - channels] if i >= channels else 0
                estimate = left + up - up_left
                da, db, dc = abs(estimate - left), abs(estimate - up), abs(estimate - up_left)
                if da <= db and da <= dc:
                    predictor = left
                elif db <= dc:
                    predictor = up
                else:
                    predictor = up_left
                line[i] = (line[i] + predictor) & 0xFF
        elif filter_type != 0:
            raise SystemExit(f"不认识的 PNG 行过滤类型 {filter_type}")

        pixels[y * stride:(y + 1) * stride] = line
        previous = line

    if color_type == 3:
        pixels = expand_palette(pixels, palette or b"", transparency, width, height)
        channels = 4

    return width, height, channels, pixels


def expand_palette(pixels, palette, transparency, width, height):
    """调色板图展开成 RGBA。"""
    count = len(palette) // 3
    out = bytearray(width * height * 4)

    for i in range(width * height):
        index = pixels[i]
        out[i * 4 + 0] = palette[index * 3 + 0] if index < count else 0
        out[i * 4 + 1] = palette[index * 3 + 1] if index < count else 0
        out[i * 4 + 2] = palette[index * 3 + 2] if index < count else 0
        out[i * 4 + 3] = transparency[index] if transparency and index < len(transparency) else 255

    return out


# ---------------------------------------------------------------- 抠底

def background_mask(width: int, height: int, channels: int, pixels) -> bytearray:
    """从画布四边出发，把连通的暗像素标成背景。

    不能用「亮度低就当背景」一刀切：币面凹陷处的暗部会被一起挖掉。
    这里只在能从外面连通进来时才认，币内部再暗也留着。
    """
    dark = bytearray(width * height)

    for i in range(width * height):
        base = i * channels
        if channels >= 3:
            luma = (299 * pixels[base] + 587 * pixels[base + 1] + 114 * pixels[base + 2]) // 1000
        else:
            luma = pixels[base]
        if luma < BACKGROUND_LUMA:
            dark[i] = 1

    stack = []
    for x in range(width):
        stack.append(x)
        stack.append((height - 1) * width + x)
    for y in range(height):
        stack.append(y * width)
        stack.append(y * width + width - 1)

    visited = bytearray(width * height)
    while stack:
        index = stack.pop()
        if visited[index] or not dark[index]:
            continue
        visited[index] = 1

        x = index % width
        if x > 0:
            stack.append(index - 1)
        if x < width - 1:
            stack.append(index + 1)
        if index >= width:
            stack.append(index - width)
        if index + width < width * height:
            stack.append(index + width)

    return visited


def clean_silhouette(buffer, canvas: int):
    """把不规则的币缘修成正圆。

    源图边缘各角度的半径在 471–492 之间摆动，缩到 19 px 就是一圈毛刺。
    做法：量出每个角度的边界半径，取平均作为正圆半径；圆内原本透明的地方，
    用同方向「边界往里 2 px」的颜色补上——那里是币的暗边，补出来还是一圈
    暗边，颜色一致。直接取最小半径做遮罩也能变圆，但会削掉 3.5% 的直径，
    而且各角度切到的颜色不一致，边上照样花。
    """
    center = canvas / 2.0
    limit = canvas // 2
    steps = 720

    boundary = []
    for step in range(steps):
        angle = step * 2.0 * math.pi / steps
        cos_a, sin_a = math.cos(angle), math.sin(angle)
        radius = 0
        while radius < limit:
            x = int(center + cos_a * radius)
            y = int(center + sin_a * radius)
            if not (0 <= x < canvas and 0 <= y < canvas):
                break
            if buffer[(y * canvas + x) * 4 + 3] < 0.5:
                break
            radius += 1
        boundary.append(radius)

    radius = sum(boundary) / steps
    feather = 1.0

    for y in range(canvas):
        dy = y + 0.5 - center
        for x in range(canvas):
            dx = x + 0.5 - center
            distance = math.hypot(dx, dy)
            if distance > radius + feather:
                base = (y * canvas + x) * 4
                buffer[base:base + 4] = [0.0, 0.0, 0.0, 0.0]
                continue

            base = (y * canvas + x) * 4
            if distance > radius - feather:
                fade = (radius + feather - distance) / (2.0 * feather)
                for channel in range(4):
                    buffer[base + channel] *= fade

            if buffer[base + 3] >= 0.999:
                continue

            # 圆内但原图透明：沿同方向取边界往里 2 px 的颜色（币的暗边）
            angle = math.atan2(dy, dx)
            step = int(math.floor(angle * steps / (2.0 * math.pi))) % steps
            inner = max(0, boundary[step] - 2)
            cos_a, sin_a = math.cos(angle), math.sin(angle)
            sx = int(center + cos_a * inner)
            sy = int(center + sin_a * inner)
            if not (0 <= sx < canvas and 0 <= sy < canvas):
                continue
            source = (sy * canvas + sx) * 4
            alpha = fade if distance > radius - feather else 1.0
            for channel in range(3):
                buffer[base + channel] = buffer[source + channel] * alpha
            buffer[base + 3] = alpha

    print(f"币缘修圆：边界 {min(boundary)}–{max(boundary)}，取半径 {radius:.0f} / {limit}")
    return buffer


def compress_highlights(buffer, knee: float = HIGHLIGHT_KNEE, slope: float = HIGHLIGHT_SLOPE):
    """把接近纯白的高光压下去，缩小后不会变成白斑。"""
    for i in range(0, len(buffer), 4):
        alpha = buffer[i + 3]
        if alpha <= 0.0:
            continue
        for channel in range(3):
            value = buffer[i + channel] / alpha / 255.0
            if value > knee:
                value = knee + (value - knee) * slope
            buffer[i + channel] = value * alpha * 255.0

    return buffer


def contrast(buffer, size: int, factor: float):
    """以中灰为轴拉对比度。只动颜色，不动 alpha。"""
    if factor == 1.0:
        return buffer

    for i in range(0, len(buffer), 4):
        alpha = buffer[i + 3]
        if alpha <= 0.0:
            continue
        for channel in range(3):
            value = buffer[i + channel] / alpha / 255.0
            value = min(1.0, max(0.0, 0.5 + (value - 0.5) * factor))
            buffer[i + channel] = value * alpha * 255.0

    return buffer


# ---------------------------------------------------------------- 重采样

def resample(source, source_width: int, source_height: int,
             target_width: int, target_height: int):
    """面积平均缩放。source 是预乘 alpha 的 float 列表，每像素 4 个分量。"""
    horizontal = [0.0] * (target_width * source_height * 4)

    scale_x = source_width / target_width
    for y in range(source_height):
        source_row = y * source_width * 4
        target_row = y * target_width * 4

        for x in range(target_width):
            start = x * scale_x
            end = start + scale_x
            total = 0.0
            accumulator = [0.0, 0.0, 0.0, 0.0]

            first = int(start)
            last = min(int(end - 1e-9) + 1, source_width)
            for i in range(first, last):
                weight = min(end, i + 1) - max(start, i)
                if weight <= 0.0:
                    continue
                base = source_row + i * 4
                accumulator[0] += source[base + 0] * weight
                accumulator[1] += source[base + 1] * weight
                accumulator[2] += source[base + 2] * weight
                accumulator[3] += source[base + 3] * weight
                total += weight

            base = target_row + x * 4
            if total > 0.0:
                horizontal[base + 0] = accumulator[0] / total
                horizontal[base + 1] = accumulator[1] / total
                horizontal[base + 2] = accumulator[2] / total
                horizontal[base + 3] = accumulator[3] / total

    result = [0.0] * (target_width * target_height * 4)
    scale_y = source_height / target_height

    for y in range(target_height):
        start = y * scale_y
        end = start + scale_y
        first = int(start)
        last = min(int(end - 1e-9) + 1, source_height)

        for x in range(target_width):
            total = 0.0
            accumulator = [0.0, 0.0, 0.0, 0.0]

            for j in range(first, last):
                weight = min(end, j + 1) - max(start, j)
                if weight <= 0.0:
                    continue
                base = (j * target_width + x) * 4
                accumulator[0] += horizontal[base + 0] * weight
                accumulator[1] += horizontal[base + 1] * weight
                accumulator[2] += horizontal[base + 2] * weight
                accumulator[3] += horizontal[base + 3] * weight
                total += weight

            base = (y * target_width + x) * 4
            if total > 0.0:
                result[base + 0] = accumulator[0] / total
                result[base + 1] = accumulator[1] / total
                result[base + 2] = accumulator[2] / total
                result[base + 3] = accumulator[3] / total

    return result


def prepare(path: str):
    """抠底、裁到包围盒、补成正方形，返回预乘 alpha 的 float 缓冲。"""
    width, height, channels, pixels = load_png(path)
    background = background_mask(width, height, channels, pixels)

    min_x, min_y, max_x, max_y = width, height, -1, -1
    for y in range(height):
        for x in range(width):
            if not background[y * width + x]:
                if x < min_x:
                    min_x = x
                if x > max_x:
                    max_x = x
                if y < min_y:
                    min_y = y
                if y > max_y:
                    max_y = y

    if max_x < 0:
        raise SystemExit(f"{path} 抠完底什么都不剩，检查背景阈值")

    box_width = max_x - min_x + 1
    box_height = max_y - min_y + 1
    side = max(box_width, box_height)

    # 正方形画布，四周按像素留白
    canvas = int(round(side / (1.0 - 2.0 * MARGIN)))
    offset_x = (canvas - box_width) // 2
    offset_y = (canvas - box_height) // 2

    print(f"源图 {width}×{height}，币体 {box_width}×{box_height}，画布 {canvas}×{canvas}")

    buffer = [0.0] * (canvas * canvas * 4)
    for y in range(box_height):
        source_y = min_y + y
        target_y = offset_y + y
        for x in range(box_width):
            source_x = min_x + x
            index = source_y * width + source_x
            if background[index]:
                continue

            base = index * channels
            if channels >= 3:
                red, green, blue = pixels[base], pixels[base + 1], pixels[base + 2]
            else:
                red = green = blue = pixels[base]
            alpha = pixels[base + 3] / 255.0 if channels == 4 else 1.0
            if alpha <= 0.0:
                continue

            target = (target_y * canvas + offset_x + x) * 4
            # 预乘：透明处的黑色不会渗进边缘
            buffer[target + 0] = red * alpha
            buffer[target + 1] = green * alpha
            buffer[target + 2] = blue * alpha
            buffer[target + 3] = alpha

    return buffer, canvas


# ---------------------------------------------------------------- 出图

def to_rgba(buffer, width: int, height: int) -> bytes:
    """预乘还原成直通 alpha 的 RGBA 字节。"""
    out = bytearray(width * height * 4)
    for i in range(width * height):
        base = i * 4
        alpha = buffer[base + 3]
        if alpha <= 0.0:
            continue
        out[base + 0] = min(255, int(buffer[base + 0] / alpha + 0.5))
        out[base + 1] = min(255, int(buffer[base + 1] / alpha + 0.5))
        out[base + 2] = min(255, int(buffer[base + 2] / alpha + 0.5))
        out[base + 3] = min(255, int(alpha * 255.0 + 0.5))
    return bytes(out)


def png_bytes(size: int, rgba: bytes) -> bytes:
    def chunk(tag: bytes, data: bytes) -> bytes:
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    raw = bytearray()
    stride = size * 4
    for y in range(size):
        raw.append(0)
        raw += rgba[y * stride:(y + 1) * stride]

    header = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", header)
            + chunk(b"IDAT", zlib.compress(bytes(raw), 9))
            + chunk(b"IEND", b""))


def ico_bytes(images: dict[int, bytes]) -> bytes:
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


def sheet(images: dict[int, bytes], sizes=(256, 64, 48, 32, 24, 16)) -> bytes:
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

    def blit(size: int, left: int, top: int) -> None:
        rgba = images[size]
        for y in range(size):
            for x in range(size):
                base = (y * size + x) * 4
                alpha = rgba[base + 3]
                if alpha == 0:
                    continue
                target = canvas[top + y][left + x]
                t = alpha / 255.0
                canvas[top + y][left + x] = (
                    int(rgba[base + 0] * t + target[0] * (1 - t) + 0.5),
                    int(rgba[base + 1] * t + target[1] * (1 - t) + 0.5),
                    int(rgba[base + 2] * t + target[2] * (1 - t) + 0.5),
                    255,
                )

    blit(big, pad, pad)
    blit(big, width // 2 + pad, pad)

    for base_x in (pad, width // 2 + pad):
        x = base_x
        for size in sizes[1:]:
            blit(size, x, pad + big - size + 8)
            x += size + 18

    raw = bytearray()
    for row in canvas:
        raw.append(0)
        for red, green, blue, alpha in row:
            raw += bytes((red, green, blue, alpha))
    return png_bytes(width, raw)


def preview(images: dict[int, bytes], size: int = 32) -> None:
    rgba = images[size]
    ramp = " .:-=+*#%@"
    print(f"--- {size}×{size} 预览 ---")
    for y in range(size):
        line = []
        for x in range(size):
            base = (y * size + x) * 4
            alpha = rgba[base + 3]
            if alpha < 24:
                line.append(" ")
                continue
            luma = (299 * rgba[base] + 587 * rgba[base + 1] + 114 * rgba[base + 2]) / 255000.0
            line.append(ramp[int(min(max(luma, 0.0), 1.0) * (len(ramp) - 1))])
        print("".join(line))


def main() -> int:
    here = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    source = os.path.join(here, SOURCE)
    if not os.path.exists(source):
        raise SystemExit(f"找不到源图 {source}")

    buffer, canvas = prepare(source)
    buffer = clean_silhouette(buffer, canvas)
    master = compress_highlights(resample(buffer, canvas, canvas, LARGEST, LARGEST))
    master_rgba = to_rgba(master, LARGEST, LARGEST)

    images = {}
    for size in SIZES:
        if size == LARGEST:
            images[size] = master_rgba
        else:
            scaled = resample(master, LARGEST, LARGEST, size, size)
            scaled = contrast(scaled, size, CONTRAST.get(size, 1.0))
            # 顺序不能反：对比度会把压下去的高光重新拉到 255
            images[size] = to_rgba(compress_highlights(scaled), size, size)

    assets = os.path.join(here, "src", "FFXIVSubmarineLedger.Desktop", "Assets")
    os.makedirs(assets, exist_ok=True)

    ico_path = os.path.join(assets, "app.ico")
    with open(ico_path, "wb") as handle:
        handle.write(ico_bytes({size: png_bytes(size, images[size]) for size in SIZES}))

    png_path = os.path.join(assets, "app.png")
    with open(png_path, "wb") as handle:
        handle.write(png_bytes(LARGEST, images[LARGEST]))

    print(f"写入 {ico_path}（{len(SIZES)} 个尺寸，{os.path.getsize(ico_path)} 字节）")
    print(f"写入 {png_path}（{os.path.getsize(png_path)} 字节）")

    if "--sheet" in sys.argv:
        docs = os.path.join(here, "docs")
        os.makedirs(docs, exist_ok=True)

        icon_png = os.path.join(docs, "icon.png")
        with open(icon_png, "wb") as handle:
            handle.write(png_bytes(LARGEST, images[LARGEST]))

        sheet_path = os.path.join(docs, "icon-sizes.png")
        with open(sheet_path, "wb") as handle:
            handle.write(sheet(images))

        print(f"写入 {icon_png}（{os.path.getsize(icon_png)} 字节）")
        print(f"写入 {sheet_path}（{os.path.getsize(sheet_path)} 字节）")

    if "--preview" in sys.argv:
        preview(images, 32)
        preview(images, 16)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
