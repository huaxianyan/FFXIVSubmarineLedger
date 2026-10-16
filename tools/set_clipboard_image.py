"""开发期辅助：把一张图片放进系统剪贴板（CF_DIB），用于验证「读取剪贴板」这条链路。

用法：python tools/set_clipboard_image.py <图片路径>
"""

from __future__ import annotations

import ctypes
import ctypes.wintypes as wintypes
import io
import sys

from PIL import Image

user32 = ctypes.windll.user32
kernel32 = ctypes.windll.kernel32

CF_DIB = 8
GMEM_MOVEABLE = 0x0002

kernel32.GlobalAlloc.argtypes = [wintypes.UINT, ctypes.c_size_t]
kernel32.GlobalAlloc.restype = wintypes.HGLOBAL
kernel32.GlobalLock.argtypes = [wintypes.HGLOBAL]
kernel32.GlobalLock.restype = ctypes.c_void_p
kernel32.GlobalUnlock.argtypes = [wintypes.HGLOBAL]
kernel32.GlobalUnlock.restype = wintypes.BOOL

user32.OpenClipboard.argtypes = [wintypes.HWND]
user32.OpenClipboard.restype = wintypes.BOOL
user32.EmptyClipboard.restype = wintypes.BOOL
user32.SetClipboardData.argtypes = [wintypes.UINT, wintypes.HANDLE]
user32.SetClipboardData.restype = wintypes.HANDLE
user32.CloseClipboard.restype = wintypes.BOOL


def main() -> int:
    path = sys.argv[1]

    buffer = io.BytesIO()
    Image.open(path).convert("RGB").save(buffer, "BMP")

    # BMP 文件头 14 字节去掉，剩下的就是 CF_DIB 需要的 DIB 数据。
    dib = buffer.getvalue()[14:]

    if not user32.OpenClipboard(None):
        print("打不开剪贴板")
        return 1

    try:
        user32.EmptyClipboard()

        handle = kernel32.GlobalAlloc(GMEM_MOVEABLE, len(dib))
        pointer = kernel32.GlobalLock(handle)
        ctypes.memmove(pointer, dib, len(dib))
        kernel32.GlobalUnlock(handle)

        if not user32.SetClipboardData(CF_DIB, handle):
            print("写入剪贴板失败")
            return 1
    finally:
        user32.CloseClipboard()

    print(f"已把 {path} 放进剪贴板（{len(dib)} 字节 DIB）")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
