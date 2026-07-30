from PIL import Image, ImageChops
import sys

a = Image.open(".tmp/pause-final3-comfortable.png").convert("L")
b = Image.open(".tmp/pause-final4-comfortable.png").convert("L")
region = (200, 430, 1000, 730)
ca = a.crop(region)
cb = b.crop(region)
diff = ImageChops.difference(ca, cb)
bbox = diff.getbbox()
hist = diff.histogram()
changed = sum(hist[16:])
print("diff bbox:", bbox, "pixels changed (>16):", changed)
