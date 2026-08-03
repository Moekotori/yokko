# Yokko UI font atlases

`NotoSansCJK` is a bitmap-font atlas generated from:

- Noto Sans CJK SC Medium
- Source repository: `notofonts/noto-cjk`
- Source commit: `f8d157532fbfaeda587e826d4cd5b21a49186f7c`

Noto Sans CJK is licensed under the SIL Open Font License 1.1. The generated
bitmap atlas is distributed under the same licence. It contains every
portable Basic Multilingual Plane glyph from the pinned font (excluding
private-use codepoints) so UI strings, search input, and imported song
metadata share one stable family and metrics. Yokko intentionally uses one
complete medium weight at the original 64 px source size to avoid duplicating
the full CJK glyph set in memory without shrinking the rendered interface.

Run `python scripts/generate-localisation-font.py` from the repository root to
regenerate the atlases.
