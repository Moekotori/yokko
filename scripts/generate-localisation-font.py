#!/usr/bin/env python3
"""Generate Yokko's CJK bitmap-font subset for osu!framework."""

from __future__ import annotations

import argparse
import re
import struct
import urllib.request
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


CHILL_ROUND_GOTHIC_COMMIT = "53505f0818983d2fcdda00dc66e051ad13e81ffb"
FONT_URLS = {
    "Yokko": (
        "https://raw.githubusercontent.com/Warren2060/ChillRoundGothic/"
        f"{CHILL_ROUND_GOTHIC_COMMIT}/ttf/ChillRoundGothic_Regular.ttf"
    ),
    "Yokko-Bold": (
        "https://raw.githubusercontent.com/Warren2060/ChillRoundGothic/"
        f"{CHILL_ROUND_GOTHIC_COMMIT}/ttf/ChillRoundGothic_Bold.ttf"
    ),
}
LOCALISATION_FONT_SIZE = 64
SEARCH_FONT_SIZE = 40
ATLAS_WIDTH = 2048
PADDING = 4


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
        default=Path("Yokko.Resources/Fonts/Yokko"),
    )
    parser.add_argument(
        "--cache",
        type=Path,
        default=(
            Path.home()
            / ".cache"
            / "yokko-font-generator"
            / CHILL_ROUND_GOTHIC_COMMIT
        ),
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


def collect_search_characters(strings_path: Path) -> list[str]:
    characters = set(collect_localisation_characters(strings_path))

    # Text boxes accept user-provided text, so a localisation-only subset is
    # insufficient. GB2312 level 1 contains the 3,755 most commonly used
    # Simplified Chinese characters. It is emitted as a separate, smaller
    # regular-weight atlas so normal UI text does not pay this memory cost.
    for lead in range(0xB0, 0xD8):
        for trail in range(0xA1, 0xFF):
            try:
                characters.add(bytes((lead, trail)).decode("gb2312"))
            except UnicodeDecodeError:
                continue

    return sorted(characters, key=ord)


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

    placements: list[tuple[int, int]] = []
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

        placements.append((x, y))
        x += width + PADDING
        row_height = max(row_height, height)

    atlas_height = next_power_of_two(y + row_height + PADDING)
    atlas = Image.new("RGBA", (ATLAS_WIDTH, atlas_height), (255, 255, 255, 0))
    glyphs: list[Glyph] = []

    for (character, (left, top, _, _), advance, mask), (glyph_x, glyph_y) in zip(rendered, placements):
        width = mask.width
        height = mask.height
        white = Image.new("RGBA", mask.size, (255, 255, 255, 255))
        atlas.paste(white, (glyph_x, glyph_y), mask)

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
            )
        )

    output.mkdir(parents=True, exist_ok=True)
    atlas.save(output / f"{font_name}_0.png", optimize=True)
    write_binary_font(
        output / f"{font_name}.bin",
        font_name,
        line_height,
        baseline,
        ATLAS_WIDTH,
        atlas_height,
        font_size,
        glyphs,
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
        1,
        0,
        0,
        4,
        4,
        4,
    )
    pages = f"{font_name}_0.png".encode("utf-8") + b"\0"
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
            0,
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
    search_characters = collect_search_characters(args.strings)

    cache = args.cache
    cache.mkdir(parents=True, exist_ok=True)

    for font_name, url in FONT_URLS.items():
        font_path = cache / f"{font_name}.ttf"
        if not font_path.exists():
            download_font(url, font_path)
        render_font(
            font_path,
            font_name,
            localisation_characters,
            args.output,
            LOCALISATION_FONT_SIZE,
        )

    render_font(
        cache / "Yokko.ttf",
        "YokkoInput",
        search_characters,
        args.output.parent / "YokkoInput",
        SEARCH_FONT_SIZE,
    )

    print(
        f"Generated {len(localisation_characters)} localisation glyphs per font "
        f"and {len(search_characters)} search glyphs"
    )


if __name__ == "__main__":
    main()
