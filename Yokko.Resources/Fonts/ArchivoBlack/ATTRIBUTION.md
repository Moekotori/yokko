# Archivo Black display font

The `ArchivoBlack` bitmap-font atlas is generated from:

- Archivo Black Regular (Omnibus-Type)
- Source repository: `google/fonts` (`ofl/archivoblack`)
- License: SIL Open Font License 1.1 (see `OFL.txt`)

The atlas covers printable ASCII (codepoints 32–126) and is used for the
playful sticker-style display text on the home screen. CJK text falls back to
the `Yokko` atlas.

Regenerate with the shared BMFont pipeline (downloads the source TTF first):

```bash
curl -sL -o artifacts/fontgen/ArchivoBlack-Regular.ttf \
  "https://raw.githubusercontent.com/google/fonts/main/ofl/archivoblack/ArchivoBlack-Regular.ttf"
python - <<'EOF'
import importlib.util, sys
from pathlib import Path

spec = importlib.util.spec_from_file_location("gen", "scripts/generate-localisation-font.py")
gen = importlib.util.module_from_spec(spec)
sys.modules["gen"] = gen
spec.loader.exec_module(gen)

gen.render_font(
    Path("artifacts/fontgen/ArchivoBlack-Regular.ttf"),
    "ArchivoBlack",
    [chr(c) for c in range(32, 127)],
    Path("Yokko.Resources/Fonts/ArchivoBlack"),
)
EOF
```
