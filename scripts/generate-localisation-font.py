#!/usr/bin/env python3
"""Generate Yokko's complete portable-BMP UI fonts for osu!framework."""

from __future__ import annotations

import argparse
import re
import struct
import urllib.request
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


NOTO_CJK_COMMIT = "f8d157532fbfaeda587e826d4cd5b21a49186f7c"
FONT_URLS = {
    "NotoSansCJK": (
        "https://raw.githubusercontent.com/notofonts/noto-cjk/"
        f"{NOTO_CJK_COMMIT}/Sans/OTF/SimplifiedChinese/"
        "NotoSansCJKsc-Medium.otf"
    ),
}
LOCALISATION_FONT_SIZE = 64
ATLAS_WIDTH = 2048
ATLAS_HEIGHT = 2048
PADDING = 4
INVISIBLE_FORMAT_CHARACTERS = "\u200b\u2060\ufeff"


@dataclass
class Glyph:
    codepoint: int
    x: int
    y: int
    width: int
    height: int
    x_offset: int
    y_offset: int
    x_advance: int
    page: int


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--strings",
        type=Path,
        default=Path("Yokko.Game/Localisation/YokkoLocalisation.cs"),
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("Yokko.Resources/Fonts/NotoSansCJK"),
    )
    parser.add_argument(
        "--cache",
        type=Path,
        default=Path("F:/YokkoArtifacts/font-generator") / NOTO_CJK_COMMIT,
    )
    parser.add_argument(
        "--family",
        choices=("all", *FONT_URLS),
        default="all",
        help="Generate one family while iterating, or all families by default.",
    )
    return parser.parse_args()


def collect_localisation_characters(strings_path: Path) -> list[str]:
    source = strings_path.read_text(encoding="utf-8")
    characters = set(chr(codepoint) for codepoint in range(32, 127))

    for literal in re.findall(r'"((?:[^"\\]|\\.)*)"', source):
        # Decode C# \uXXXX escapes so escape-written characters still reach the subset.
        decoded = re.sub(
            r"\\u([0-9a-fA-F]{4})",
            lambda match: chr(int(match.group(1), 16)),
            literal,
        )
        characters.update(character for character in decoded if ord(character) >= 127)

    return sorted(characters, key=ord)


def collect_supported_bmp_characters(font_path: Path) -> list[str]:
    """Read the font's Unicode cmap and retain every renderable BMP scalar.

    osu!framework's current bitmap glyph API is char-based, so supplementary
    plane characters cannot be represented by a BMFont atlas. Covering the
    complete BMP still includes Latin, Greek, Cyrillic, Japanese kana, CJK,
    full modern Hangul, and the symbols used by imported song metadata.
    """
    data = font_path.read_bytes()
    table_count = struct.unpack_from(">H", data, 4)[0]
    tables: dict[bytes, tuple[int, int]] = {}

    for index in range(table_count):
        tag, _, offset, length = struct.unpack_from(
            ">4sIII", data, 12 + index * 16
        )
        tables[tag] = (offset, length)

    cmap_offset, _ = tables[b"cmap"]
    subtable_count = struct.unpack_from(">H", data, cmap_offset + 2)[0]
    format_12_offsets: list[int] = []

    for index in range(subtable_count):
        platform, encoding, relative_offset = struct.unpack_from(
            ">HHI", data, cmap_offset + 4 + index * 8
        )
        subtable_offset = cmap_offset + relative_offset
        cmap_format = struct.unpack_from(">H", data, subtable_offset)[0]
        if cmap_format == 12 and (
            platform == 0 or (platform == 3 and encoding == 10)
        ):
            format_12_offsets.append(subtable_offset)

    if not format_12_offsets:
        raise ValueError(f"{font_path} has no Unicode format 12 cmap")

    subtable_offset = format_12_offsets[0]
    group_count = struct.unpack_from(">I", data, subtable_offset + 12)[0]
    codepoints: set[int] = set()

    for index in range(group_count):
        start, end, _ = struct.unpack_from(
            ">III", data, subtable_offset + 16 + index * 12
        )
        if start > 0xFFFF:
            continue
        codepoints.update(range(max(32, start), min(0xFFFF, end) + 1))

    # Surrogates are not Unicode scalar values and private-use glyphs are not
    # portable user text. Keep all other supported BMP characters.
    return [
        chr(codepoint)
        for codepoint in sorted(codepoints)
        if not 0xD800 <= codepoint <= 0xDFFF
        and not 0xE000 <= codepoint <= 0xF8FF
    ]


def download_font(url: str, destination: Path) -> None:
    request = urllib.request.Request(url, headers={"User-Agent": "Yokko font generator"})
    with urllib.request.urlopen(request) as response:
        destination.write_bytes(response.read())


def next_power_of_two(value: int) -> int:
    return 1 << max(0, value - 1).bit_length()


def render_font(
    font_path: Path,
    font_name: str,
    characters: list[str],
    output: Path,
    font_size: int,
) -> None:
    font = ImageFont.truetype(str(font_path), font_size)
    ascent, descent = font.getmetrics()
    line_height = ascent + descent
    baseline = ascent

    rendered: list[tuple[str, tuple[int, int, int, int], int, Image.Image]] = []
    for character in characters:
        left, top, right, bottom = font.getbbox(character, anchor="ls")
        width = max(0, right - left)
        height = max(0, bottom - top)
        advance = round(font.getlength(character))

        if width == 0 or height == 0:
            rendered.append((character, (left, top, right, bottom), advance, Image.new("L", (1, 1), 0)))
            continue

        mask = Image.new("L", (width, height), 0)
        draw = ImageDraw.Draw(mask)
        draw.text((-left, -top), character, font=font, fill=255, anchor="ls")
        rendered.append((character, (left, top, right, bottom), advance, mask))

    placements: list[tuple[int, int, int]] = []
    page = 0
    x = PADDING
    y = PADDING
    row_height = 0

    for _, (_, _, right, bottom), _, mask in rendered:
        width = mask.width
        height = mask.height

        if x + width + PADDING > ATLAS_WIDTH:
            x = PADDING
            y += row_height + PADDING
            row_height = 0

        if y + height + PADDING > ATLAS_HEIGHT:
            page += 1
            x = PADDING
            y = PADDING
            row_height = 0

        placements.append((page, x, y))
        x += width + PADDING
        row_height = max(row_height, height)

    page_count = page + 1
    page_digits = len(str(page_count - 1))
    glyphs: list[Glyph] = []

    for (character, (left, top, _, _), advance, mask), (glyph_page, glyph_x, glyph_y) in zip(rendered, placements):
        width = mask.width
        height = mask.height
        glyphs.append(
            Glyph(
                ord(character),
                glyph_x,
                glyph_y,
                width,
                height,
                left,
                baseline + top,
                advance,
                glyph_page,
            )
        )

    output.mkdir(parents=True, exist_ok=True)
    for stale_page in output.glob(f"{font_name}_*.png"):
        stale_page.unlink()

    for page_index in range(page_count):
        atlas = Image.new(
            "RGBA", (ATLAS_WIDTH, ATLAS_HEIGHT), (255, 255, 255, 0)
        )
        for (_, _, _, mask), (glyph_page, glyph_x, glyph_y) in zip(
            rendered, placements
        ):
            if glyph_page != page_index:
                continue
            white = Image.new("RGBA", mask.size, (255, 255, 255, 255))
            atlas.paste(white, (glyph_x, glyph_y), mask)

        if page_index == page_count - 1:
            used_height = next_power_of_two(y + row_height + PADDING)
            atlas = atlas.crop((0, 0, ATLAS_WIDTH, used_height))
        atlas.save(
            output / f"{font_name}_{page_index:0{page_digits}d}.png",
            optimize=True,
        )

    write_binary_font(
        output / f"{font_name}.bin",
        font_name,
        line_height,
        baseline,
        ATLAS_WIDTH,
        ATLAS_HEIGHT,
        font_size,
        glyphs,
        page_count,
    )


def write_block(stream, block_type: int, payload: bytes) -> None:
    stream.write(struct.pack("<BI", block_type, len(payload)))
    stream.write(payload)


def write_binary_font(
    destination: Path,
    font_name: str,
    line_height: int,
    baseline: int,
    atlas_width: int,
    atlas_height: int,
    font_size: int,
    glyphs: list[Glyph],
    page_count: int,
) -> None:
    bold = font_name.endswith("-Bold")
    info_flags = 0b00000011 | (0b00001000 if bold else 0)
    info = struct.pack(
        "<hBBHBBBBBBBB",
        font_size,
        info_flags,
        0,
        100,
        1,
        0,
        0,
        0,
        0,
        1,
        1,
        0,
    ) + font_name.encode("utf-8") + b"\0"
    common = struct.pack(
        "<HHHHHBBBBB",
        line_height,
        baseline,
        atlas_width,
        atlas_height,
        page_count,
        0,
        0,
        4,
        4,
        4,
    )
    page_digits = len(str(page_count - 1))
    pages = b"".join(
        f"{font_name}_{page:0{page_digits}d}.png".encode("utf-8") + b"\0"
        for page in range(page_count)
    )
    chars = b"".join(
        struct.pack(
            "<IHHHHhhhBB",
            glyph.codepoint,
            glyph.x,
            glyph.y,
            glyph.width,
            glyph.height,
            glyph.x_offset,
            glyph.y_offset,
            glyph.x_advance,
            glyph.page,
            8,
        )
        for glyph in glyphs
    )

    with destination.open("wb") as stream:
        stream.write(b"BMF\x03")
        write_block(stream, 1, info)
        write_block(stream, 2, common)
        write_block(stream, 3, pages)
        write_block(stream, 4, chars)


def main() -> None:
    args = parse_args()
    localisation_characters = collect_localisation_characters(args.strings)

    cache = args.cache
    cache.mkdir(parents=True, exist_ok=True)

    for font_name, url in FONT_URLS.items():
        if args.family != "all" and args.family != font_name:
            continue
        font_path = cache / url.rsplit("/", 1)[-1]
        if not font_path.exists():
            download_font(url, font_path)
        characters = sorted(
            set(collect_supported_bmp_characters(font_path))
            .union(INVISIBLE_FORMAT_CHARACTERS),
            key=ord,
        )
        missing_localisation = sorted(
            set(localisation_characters).difference(characters), key=ord
        )
        if missing_localisation:
            raise ValueError(
                f"{font_name} is missing localisation glyphs: "
                + "".join(missing_localisation)
            )
        render_font(
            font_path,
            font_name,
            characters,
            args.output,
            LOCALISATION_FONT_SIZE,
        )
        print(f"Generated {len(characters)} BMP glyphs for {font_name}")


if __name__ == "__main__":
    main()
