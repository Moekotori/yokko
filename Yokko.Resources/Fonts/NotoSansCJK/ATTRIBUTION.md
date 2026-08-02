# Yokko UI font atlases

`NotoSansCJK` and `NotoSansCJK-Bold` are bitmap-font atlases generated from:

- Noto Sans CJK SC Regular
- Noto Sans CJK SC Bold
- Source repository: `notofonts/noto-cjk`
- Source commit: `f8d157532fbfaeda587e826d4cd5b21a49186f7c`

Noto Sans CJK is licensed under the SIL Open Font License 1.1. The generated
bitmap atlases are distributed under the same licence. They contain every
portable Basic Multilingual Plane glyph from the pinned fonts (excluding
private-use codepoints) so UI strings, search input, and imported song
metadata share one stable family and metrics.

Run `python scripts/generate-localisation-font.py` from the repository root to
regenerate the atlases.
