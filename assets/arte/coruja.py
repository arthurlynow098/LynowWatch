import math, random
random.seed(11)
def path(p): return "M"+" L".join(f"{x:.1f} {y:.1f}" for x,y in p)
# silhueta: cabeça larga com tufos, corpo
cab=[]
for i in range(91):
    t=math.radians(-90+i*4)
    s=math.sin(t); c=math.cos(t)
    rx,ry=330,270
    serr=(0.03 if i%2 else 0) if s>0.1 else 0
    cab.append((500+rx*(1+serr)*c, 430+ry*(1+serr)*s*(0.9 if s<0 else 1)))
def penas(lado):
    out=[]
    for k in range(6):
        bx=500+lado*(175+k*22); by=262+k*6
        tx=500+lado*(250+k*16); ty=80+k*20
        w=30-k*2
        out.append(f'M{bx-w:.0f} {by} C{bx-w*0.6:.0f} {by-90} {tx-6:.0f} {ty+60} {tx:.0f} {ty} C{tx+8:.0f} {ty+70} {bx+w*0.8:.0f} {by-80} {bx+w:.0f} {by} Z')
    return out
tufoE=penas(-1); tufoD=penas(1)
corpo=[]
for i in range(51):
    t=math.radians(180+i*3.6); r=1+(0.035 if i%2 else 0)
    corpo.append((500+440*r*math.cos(t), 1040+400*r*math.sin(t)))
pen=[]
# penas do peito: escamas em arcos
for row in range(10):
    y=720+row*30
    for k in range(-14,15):
        x=500+k*34+(17 if row%2 else 0)
        if ((x-500)/440)**2+((y-1040)/400)**2>0.9: continue
        pen.append(f'<path d="M{x-16} {y} Q{x} {y+26} {x+16} {y}" stroke="#111" stroke-width="5" fill="none"/>')
# fios radiais no disco facial
fios=[]
for cx in (370,630):
    for k in range(70):
        a=k/70*2*math.pi; r1=95; r2=random.uniform(150,185)
        fios.append(f'<path d="M{cx+r1*math.cos(a):.1f} {430+r1*math.sin(a):.1f} L{cx+r2*math.cos(a):.1f} {430+r2*math.sin(a):.1f}" stroke="#3a3a3a" stroke-width="2.4" opacity=".5"/>')
for _ in range(500):
    x=random.uniform(170,830); y=random.uniform(160,330)
    a=math.radians(random.uniform(60,120)); l=random.uniform(16,30)
    fios.append(f'<path d="M{x:.1f} {y:.1f} l{l*math.cos(a):.1f} {l*math.sin(a):.1f}" stroke="#2a2a2a" stroke-width="2.2" opacity=".5"/>')
svg=f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1000 1000" width="1000" height="1000">
<defs>
 <radialGradient id="cab" cx="45%" cy="40%" r="70%"><stop offset="0" stop-color="#eee"/><stop offset=".6" stop-color="#a5a5a5"/><stop offset="1" stop-color="#3a3a3a"/></radialGradient>
 <radialGradient id="disco" cx="50%" cy="50%" r="50%"><stop offset="0" stop-color="#fff"/><stop offset=".75" stop-color="#e0e0e0"/><stop offset="1" stop-color="#8a8a8a"/></radialGradient>
 <radialGradient id="corpo" cx="45%" cy="5%" r="95%"><stop offset="0" stop-color="#e8e8e8"/><stop offset=".6" stop-color="#999"/><stop offset="1" stop-color="#2a2a2a"/></radialGradient>
 <radialGradient id="iris" cx="50%" cy="50%" r="50%"><stop offset="0" stop-color="#fff"/><stop offset=".8" stop-color="#d9d9d9"/><stop offset="1" stop-color="#7a7a7a"/></radialGradient>
</defs>
<rect width="1000" height="1000" fill="#000"/>
<path d="{path(corpo)} Z" fill="url(#corpo)"/>
{''.join(f'<path d="{d}" fill="#{"9a9a9a" if i%2 else "c4c4c4"}" stroke="#1d1d1d" stroke-width="3"/>' for i,d in enumerate(tufoE))}
{''.join(f'<path d="{d}" fill="#{"8a8a8a" if i%2 else "b0b0b0"}" stroke="#1d1d1d" stroke-width="3"/>' for i,d in enumerate(tufoD))}
<path d="{path(cab)} Z" fill="url(#cab)"/>
<clipPath id="cc"><path d="{path(cab)} Z"/></clipPath>
<g clip-path="url(#cc)">{''.join(fios[140:])}</g>
<circle cx="370" cy="430" r="185" fill="url(#disco)"/><circle cx="630" cy="430" r="185" fill="url(#disco)"/>
{''.join(fios[:140])}
<g clip-path="url(#cc)"><path d="M500 300 C470 380 470 420 500 470 C530 420 530 380 500 300Z" fill="#7a7a7a"/></g>
<circle cx="370" cy="430" r="92" fill="#1a1a1a"/><circle cx="630" cy="430" r="92" fill="#1a1a1a"/>
<circle cx="370" cy="430" r="80" fill="url(#iris)"/><circle cx="630" cy="430" r="80" fill="url(#iris)"/>
<g stroke="#555" stroke-width="2.4">{''.join(f'<path d="M{cx+38*math.cos(k/30*6.283):.1f} {430+38*math.sin(k/30*6.283):.1f} L{cx+76*math.cos(k/30*6.283):.1f} {430+76*math.sin(k/30*6.283):.1f}"/>' for cx in (370,630) for k in range(30))}</g>
<circle cx="370" cy="430" r="42" fill="#050505"/><circle cx="630" cy="430" r="42" fill="#050505"/>
<circle cx="352" cy="410" r="13" fill="#fff"/><circle cx="612" cy="410" r="13" fill="#fff"/>
<path d="M500 470 C478 500 476 545 500 600 C524 545 522 500 500 470 Z" fill="#2a2a2a"/>
<path d="M490 490 C484 520 486 550 498 580" stroke="#bbb" stroke-width="5" fill="none"/>
<path d="M300 300 C340 270 400 268 440 300" stroke="#222" stroke-width="7" fill="none"/>
<path d="M560 300 C600 268 660 270 700 300" stroke="#222" stroke-width="7" fill="none"/>
<clipPath id="cp"><path d="{path(corpo)} Z"/></clipPath>
<g clip-path="url(#cp)">{''.join(pen)}</g>
</svg>'''
open("coruja.svg","w").write(svg)
