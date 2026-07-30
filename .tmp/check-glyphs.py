import re
from pathlib import Path

src = Path("Yokko.Game/Localisation/YokkoLocalisation.cs").read_text(encoding="utf-8")
chars = set()
pattern = re.compile(r'"((?:[^"\\]|\\.)*)"')
for literal in pattern.findall(src):
    decoded = re.sub(r"\\u([0-9a-fA-F]{4})", lambda m: chr(int(m.group(1), 16)), literal)
    chars.update(c for c in decoded if ord(c) >= 127)

needed = set("选择确认重试選択決定")
print("missing:", sorted(needed - chars))
