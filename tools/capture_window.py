"""开发期辅助：抓取本程序窗口截图，用于人工核对 UI。

用 PrintWindow 让窗口把自身渲染到内存位图，所以窗口被别的窗口挡住也能截到。

用法：python tools/capture_window.py <输出文件> [标题关键字]
"""

from __future__ import annotations

import ctypes
import ctypes.wintypes as wintypes
import sys

from PIL import Image

user32 = ctypes.windll.user32
gdi32 = ctypes.windll.gdi32
user32.SetProcessDPIAware()

PW_RENDERFULLCONTENT = 0x00000002
BI_RGB = 0

user32.GetWindowDC.argtypes = [wintypes.HWND]
user32.GetWindowDC.restype = wintypes.HDC
user32.PrintWindow.argtypes = [wintypes.HWND, wintypes.HDC, wintypes.UINT]
user32.PrintWindow.restype = wintypes.BOOL
user32.ReleaseDC.argtypes = [wintypes.HWND, wintypes.HDC]
user32.ReleaseDC.restype = ctypes.c_int

gdi32.CreateCompatibleDC.argtypes = [wintypes.HDC]
gdi32.CreateCompatibleDC.restype = wintypes.HDC
gdi32.CreateCompatibleBitmap.argtypes = [wintypes.HDC, ctypes.c_int, ctypes.c_int]
gdi32.CreateCompatibleBitmap.restype = wintypes.HBITMAP
gdi32.SelectObject.argtypes = [wintypes.HDC, wintypes.HGDIOBJ]
gdi32.SelectObject.restype = wintypes.HGDIOBJ
gdi32.DeleteObject.argtypes = [wintypes.HGDIOBJ]
gdi32.DeleteDC.argtypes = [wintypes.HDC]
gdi32.GetDIBits.argtypes = [
    wintypes.HDC,
    wintypes.HBITMAP,
    wintypes.UINT,
    wintypes.UINT,
    ctypes.c_void_p,
    ctypes.c_void_p,
    wintypes.UINT,
]


class BITMAPINFOHEADER(ctypes.Structure):
    _fields_ = [
        ("biSize", wintypes.DWORD),
        ("biWidth", wintypes.LONG),
        ("biHeight", wintypes.LONG),
        ("biPlanes", wintypes.WORD),
        ("biBitCount", wintypes.WORD),
        ("biCompression", wintypes.DWORD),
        ("biSizeImage", wintypes.DWORD),
        ("biXPelsPerMeter", wintypes.LONG),
        ("biYPelsPerMeter", wintypes.LONG),
        ("biClrUsed", wintypes.DWORD),
        ("biClrImportant", wintypes.DWORD),
    ]


def find_window(keyword: str) -> int:
    """按标题关键字找可见窗口；Avalonia 会额外建一些辅助窗口，所以取面积最大的那个。"""
    found: list[int] = []

    @ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    def callback(hwnd, _lparam):
        if not user32.IsWindowVisible(hwnd):
            return True
        length = user32.GetWindowTextLengthW(hwnd)
        if length == 0:
            return True
        buffer = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(hwnd, buffer, length + 1)
        if keyword in buffer.value:
            found.append(hwnd)
        return True

    user32.EnumWindows(callback, 0)

    if not found:
        return 0

    def area(hwnd: int) -> int:
        rect = wintypes.RECT()
        user32.GetWindowRect(hwnd, ctypes.byref(rect))
        return max(0, rect.right - rect.left) * max(0, rect.bottom - rect.top)

    return max(found, key=area)


def render(hwnd: int) -> Image.Image:
    """把窗口画到内存位图并转成 PIL 图像。"""
    rect = wintypes.RECT()
    user32.GetWindowRect(hwnd, ctypes.byref(rect))
    width = rect.right - rect.left
    height = rect.bottom - rect.top

    window_dc = user32.GetWindowDC(hwnd)
    memory_dc = gdi32.CreateCompatibleDC(window_dc)
    bitmap = gdi32.CreateCompatibleBitmap(window_dc, width, height)
    previous = gdi32.SelectObject(memory_dc, bitmap)

    try:
        user32.PrintWindow(hwnd, memory_dc, PW_RENDERFULLCONTENT)

        header = BITMAPINFOHEADER()
        header.biSize = ctypes.sizeof(BITMAPINFOHEADER)
        header.biWidth = width
        header.biHeight = -height
        header.biPlanes = 1
        header.biBitCount = 32
        header.biCompression = BI_RGB

        buffer = ctypes.create_string_buffer(width * height * 4)
        gdi32.GetDIBits(memory_dc, bitmap, 0, height, buffer, ctypes.byref(header), 0)
        return Image.frombuffer("RGBA", (width, height), buffer, "raw", "BGRA", 0, 1).convert("RGB")
    finally:
        gdi32.SelectObject(memory_dc, previous)
        gdi32.DeleteObject(bitmap)
        gdi32.DeleteDC(memory_dc)
        user32.ReleaseDC(hwnd, window_dc)


def main() -> int:
    output = sys.argv[1]
    keyword = sys.argv[2] if len(sys.argv) > 2 else "潜艇收益"

    hwnd = find_window(keyword)
    if hwnd == 0:
        print(f"未找到标题包含「{keyword}」的窗口")
        return 1

    image = render(hwnd)
    image.save(output)
    print(f"已保存 {output}  尺寸 {image.width}x{image.height}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
