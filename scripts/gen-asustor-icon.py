#!/usr/bin/env python3
"""gen-asustor-icon.py — one-off generator for the placeholder Asustor App Central icon.

Not part of the build pipeline: run manually to (re)produce
deploy/nas/asustor/CONTROL/icon.png. Pure stdlib (zlib), no Pillow dependency,
since build/dev machines aren't guaranteed to have image libraries installed.

Draws a simple flat "scan pulse" glyph (concentric ring + crosshair) in the
IoTSpy brand navy/cyan palette. Replace with real artwork whenever available.
"""
import struct
import zlib

SIZE = 128
BG = (0x0B, 0x16, 0x2A, 255)      # dark navy
RING = (0x38, 0xE0, 0xC8, 255)    # cyan accent
CENTER = (SIZE // 2, SIZE // 2)
OUTER_R = 46
INNER_R = 40
DOT_R = 8


def pixel(x, y):
    dx, dy = x - CENTER[0], y - CENTER[1]
    dist = (dx * dx + dy * dy) ** 0.5
    if dist <= DOT_R:
        return RING
    if INNER_R <= dist <= OUTER_R:
        return RING
    # crosshair ticks
    if abs(dx) <= 2 and OUTER_R < abs(dy) <= OUTER_R + 14:
        return RING
    if abs(dy) <= 2 and OUTER_R < abs(dx) <= OUTER_R + 14:
        return RING
    return BG


def build_png(path):
    rows = []
    for y in range(SIZE):
        row = bytearray([0])  # filter type 0 per scanline
        for x in range(SIZE):
            row.extend(pixel(x, y))
        rows.append(bytes(row))
    raw = b"".join(rows)
    compressed = zlib.compress(raw, 9)

    def chunk(tag, data):
        return (
            struct.pack(">I", len(data))
            + tag
            + data
            + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
        )

    sig = b"\x89PNG\r\n\x1a\n"
    ihdr = struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0)  # 8-bit RGBA
    with open(path, "wb") as f:
        f.write(sig)
        f.write(chunk(b"IHDR", ihdr))
        f.write(chunk(b"IDAT", compressed))
        f.write(chunk(b"IEND", b""))


if __name__ == "__main__":
    import sys

    out = sys.argv[1] if len(sys.argv) > 1 else "deploy/nas/asustor/CONTROL/icon.png"
    build_png(out)
    print(f"wrote {out}")
