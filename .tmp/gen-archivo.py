import importlib.util
import sys
from pathlib import Path

spec = importlib.util.spec_from_file_location(
    "gen", "scripts/generate-localisation-font.py")
gen = importlib.util.module_from_spec(spec)
sys.modules["gen"] = gen
spec.loader.exec_module(gen)

gen.FONT_SIZE = 128
characters = [chr(c) for c in range(32, 127)]
source = Path("artifacts/fontgen/ArchivoBlack-Regular.ttf")
output = Path("Yokko.Resources/Fonts/ArchivoBlack")

# Regular 图集 + 一个指向同一字体的 -Bold 别名：
# FontUsage 带 Bold 权重时会解析出 "ArchivoBlack-Bold" 字体名，
# 这样 CJK 回退才会按 Bold 权重落到 Yokko-Bold。
gen.render_font(source, "ArchivoBlack", characters, output)
gen.render_font(source, "ArchivoBlack-Bold", characters, output)
print("done")
