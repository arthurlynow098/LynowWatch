# Banner LynowWatch

Arquivo final: `assets/lynowwatch-banner.png` (2172 × 724). Mascote isolado: `assets/project-animal.png`. Ícone do executável: `assets/lynowwatch.ico`.

Composição dos banners da linha Lynow: fundo `#090A08`, tile arredondado com o animal em gravura à esquerda e logotipo em Inter Display Black, com `Lynow` em marfim `#F3EEE1` e `Watch` em âmbar `#F5A623`.

## Como a arte foi produzida

A gravura da coruja foi gerada por código, sem ferramenta de geração de imagens:

1. `assets/arte/coruja.py` desenha a coruja como formas vetoriais sombreadas (disco facial, penas, tufos).
2. `assets/arte/gravura.py` converte os tons em linhas de buril com espessura variável, hachura cruzada e pontilhado, sobre o tile âmbar.
3. `assets/arte/banner.py` monta o banner e ajusta o logotipo às margens.

```bash
python coruja.py
python gravura.py coruja.svg "#F2A516" coruja-tile.png
python banner.py coruja-tile.png Watch "#F5A623" lynowwatch-banner.png
```

Requer Python 3, Pillow, NumPy, `rsvg-convert` e a fonte Inter Display.

## Para refazer com geração de imagens

> Use case: logo-brand. Wide GitHub repository banner for LynowWatch matching input banner composition: ultra-wide 3:1 near-black #090a08 background, large rounded square mascot tile at left, huge heavy clean sans-serif logotype at right. Mascot: a great horned owl bust in detailed ivory engraving style with fine cross-hatching, large round eyes, feathered ear tufts, on amber #F2A516 tile. Exact single word text: LynowWatch. 'Lynow' in warm off-white and 'Watch' in amber #F5A623. No tagline, no border, no shadow.
