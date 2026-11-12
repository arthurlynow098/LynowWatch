import sys
from PIL import Image, ImageDraw, ImageFont
tile, palavra, cor, saida = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
W, H, T, GAP, MARGEM = 2172, 724, 468, 64, 150
FONTE = "/usr/share/fonts/opentype/inter/InterDisplay-Black.otf"
img = Image.new("RGB", (W, H), "#090a08")
d = ImageDraw.Draw(img)
def medir(f, s, track): return sum(d.textlength(c, font=f) + track for c in s) - track
tamanho = 236
while True:
    f = ImageFont.truetype(FONTE, tamanho); track = -tamanho * 0.042
    larg = medir(f, "Lynow", track) + tamanho * 0.025 + medir(f, palavra, track)
    if T + GAP + larg <= W - 2 * MARGEM or tamanho <= 120: break
    tamanho -= 4
x0 = int((W - (T + GAP + larg)) / 2)
t = Image.open(tile).resize((T, T), Image.LANCZOS)
img.paste(t, (x0, (H - T) // 2), t)
b = d.textbbox((0, 0), "L", font=f); y = H // 2 - (b[1] + b[3]) // 2
x = x0 + T + GAP
for s, c in (("Lynow", "#f3eee1"), (palavra, cor)):
    for ch in s:
        d.text((x, y), ch, font=f, fill=c); x += d.textlength(ch, font=f) + track
    x += -track + tamanho * 0.025
print(saida, "fonte", tamanho, "margens", x0, W - int(x))
img.save(saida, optimize=True)
