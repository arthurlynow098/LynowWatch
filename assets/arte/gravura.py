"""Converte uma ilustração sombreada (SVG) em gravura de linhas sobre um tile colorido."""
import sys, subprocess, numpy as np
from PIL import Image, ImageFilter, ImageDraw
svg, cor, saida = sys.argv[1], sys.argv[2], sys.argv[3]
N = 1200
txt = open(svg).read().replace('<rect width="1000" height="1000" fill="#000"/>', '')
tmp = saida + ".tmp.svg"; open(tmp, "w").write(txt)
subprocess.run(["rsvg-convert", "-w", str(N), tmp, "-o", saida + ".tmp.png"], check=True)
im = Image.open(saida + ".tmp.png").convert("RGBA")
a = np.asarray(im).astype(float) / 255
alpha = a[..., 3]
lum = (a[..., 0] * .3 + a[..., 1] * .59 + a[..., 2] * .11)
lum = np.where(alpha > 0, lum, 1)
lum = np.asarray(Image.fromarray((lum * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2))).astype(float) / 255
dark = np.clip((1 - lum) ** 1.15, 0, 1)
yy, xx = np.mgrid[0:N, 0:N].astype(float)
# campo de direção suave: linhas levemente curvas, como buril
gy, gx = np.gradient(np.asarray(Image.fromarray((lum*255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(18))).astype(float))
fase1 = (yy * 0.92 + xx * 0.38 + 9 * np.sin(xx / 47) + 7 * np.sin(yy / 61) + 0.35 * gx) / 7.0
fase2 = (xx * 0.85 - yy * 0.52 + 6 * np.sin(yy / 39)) / 6.2
d1 = np.abs((fase1 % 1) - 0.5) * 2   # 0 no centro da linha
d2 = np.abs((fase2 % 1) - 0.5) * 2
tinta = (d1 < dark * 0.95).astype(float)
tinta = np.maximum(tinta, ((d2 < (dark - 0.55) * 1.6) & (dark > 0.55)).astype(float))
# pontilhado nas meias-luzes
rng = np.random.default_rng(7)
pont = (rng.random((N, N)) < (dark * 0.10)) & (dark > 0.12)
tinta = np.maximum(tinta, pont.astype(float))
tinta = np.where(dark > 0.9, 1, tinta)
# contorno
borda = np.asarray(Image.fromarray((alpha * 255).astype(np.uint8)).filter(ImageFilter.FIND_EDGES).filter(ImageFilter.MaxFilter(5))).astype(float) / 255
tinta = np.clip(np.maximum(tinta, borda * (alpha > 0.1)), 0, 1)
tinta = np.asarray(Image.fromarray((tinta * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.7))).astype(float) / 255
marfim = np.array([243, 238, 225]) / 255; preto = np.array([22, 20, 18]) / 255
c = np.array([int(cor[i:i+2], 16) for i in (1, 3, 5)]) / 255
animal = marfim * (1 - tinta[..., None]) + preto * tinta[..., None]
fundo = np.ones((N, N, 3)) * c
out = fundo * (1 - alpha[..., None]) + animal * alpha[..., None]
img = Image.fromarray((out * 255).astype(np.uint8))
# tile arredondado
m = Image.new("L", (N, N), 0); ImageDraw.Draw(m).rounded_rectangle((0, 0, N - 1, N - 1), radius=150, fill=255)
tile = Image.new("RGBA", (N, N)); tile.paste(img, (0, 0), m)
tile.save(saida)
