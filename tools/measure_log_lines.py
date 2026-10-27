"""量一张 FF14 日文日志截图的版式参数：行距、行高、左边距、时间戳宽度。

纯标准库实现，不依赖 Pillow / numpy——本机没装，也不想为一次测量装。
只读亮度结构，不保留图像内容。
"""

import struct
import sys
import zlib


def load_png(path):
    data = open(path, "rb").read()
    assert data[:8] == b"\x89PNG\r\n\x1a\n", "不是 PNG"
    pos = 8
    idat = bytearray()
    width = height = depth = color = None
    while pos < len(data):
        (length,) = struct.unpack(">I", data[pos : pos + 4])
        ctype = data[pos + 4 : pos + 8]
        chunk = data[pos + 8 : pos + 8 + length]
        pos += 12 + length
        if ctype == b"IHDR":
            width, height, depth, color, _, _, interlace = struct.unpack(">IIBBBBB", chunk)
            assert depth == 8 and interlace == 0 and color in (2, 6), (
                f"暂只支持 8 位非隔行 RGB/RGBA，实际 depth={depth} color={color} interlace={interlace}"
            )
        elif ctype == b"IDAT":
            idat += chunk
        elif ctype == b"IEND":
            break

    channels = 3 if color == 2 else 4
    raw = zlib.decompress(bytes(idat))
    stride = width * channels
    out = bytearray(stride * height)
    prev = bytearray(stride)
    src = 0
    for y in range(height):
        filt = raw[src]
        src += 1
        line = bytearray(raw[src : src + stride])
        src += stride
        if filt == 1:
            for i in range(channels, stride):
                line[i] = (line[i] + line[i - channels]) & 0xFF
        elif filt == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 0xFF
        elif filt == 3:
            for i in range(stride):
                left = line[i - channels] if i >= channels else 0
                line[i] = (line[i] + ((left + prev[i]) >> 1)) & 0xFF
        elif filt == 4:
            for i in range(stride):
                a = line[i - channels] if i >= channels else 0
                b = prev[i]
                c = prev[i - channels] if i >= channels else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pred = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pred) & 0xFF
        elif filt != 0:
            raise AssertionError(f"未知过滤类型 {filt}")
        out[y * stride : (y + 1) * stride] = line
        prev = line

    # 灰度：取 G 通道即可，文字是白色，三通道差异不影响行/列投影。
    lum = bytearray(width * height)
    for y in range(height):
        base = y * stride
        for x in range(width):
            g = out[base + x * channels + 1]
            r = out[base + x * channels]
            b = out[base + x * channels + 2]
            lum[y * width + x] = (r * 299 + g * 587 + b * 114) // 1000
    return width, height, lum


def bands(lum, width, height, bright=200, min_pixels=6):
    rows = []
    for y in range(height):
        count = 0
        base = y * width
        for x in range(width):
            if lum[base + x] >= bright:
                count += 1
                if count >= min_pixels:
                    break
        rows.append(count >= min_pixels)

    result = []
    y = 0
    while y < height:
        if rows[y]:
            start = y
            while y < height and rows[y]:
                y += 1
            result.append((start, y - start))
        else:
            y += 1
    return result


def first_bright(lum, width, top, tall, bright=200):
    for x in range(width):
        for y in range(top, top + tall):
            if lum[y * width + x] >= bright:
                return x
    return -1


def main(path):
    width, height, lum = load_png(path)
    print(f"尺寸 {width} × {height}")

    found = bands(lum, width, height)
    print(f"文本行带 {len(found)} 条")
    for i, (top, tall) in enumerate(found):
        left = first_bright(lum, width, top, tall)
        print(f"  #{i + 1:02d} top={top:4d} 高={tall:3d} 左边距={left:3d}")

    if len(found) >= 2:
        tops = [t for t, _ in found]
        gaps = [b - a for a, b in zip(tops, tops[1:])]
        gaps.sort()
        mid = gaps[len(gaps) // 2]
        heights = sorted(h for _, h in found)
        print(f"行距中位数 {mid}（区间 {gaps[0]}–{gaps[-1]}）")
        print(f"行高中位数 {heights[len(heights) // 2]}（区间 {heights[0]}–{heights[-1]}）")
        print(f"左边距中位数 {sorted(first_bright(lum, width, t, h) for t, h in found)[len(found) // 2]}")


if __name__ == "__main__":
    main(sys.argv[1])
