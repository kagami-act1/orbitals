"""Render the simple planet and comet mark at each native Windows icon size."""
from pathlib import Path
from PIL import Image, ImageDraw
import math

root = Path(__file__).resolve().parent
svg = '''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">
  <rect x="8" y="8" width="240" height="240" rx="60" fill="#11151b"/>
  <circle cx="115" cy="135" r="48" fill="#93e9c1"/>
  <path d="M 44,177 C 82,217 210,137 207,74" fill="none" stroke="#edf3f6" stroke-width="11" stroke-linecap="round"/>
  <circle cx="207" cy="74" r="11" fill="#edf3f6"/>
</svg>
'''
(root / 'Orbitals.svg').write_text(svg, encoding='utf-8')

def render(size):
    scale = 6
    image = Image.new('RGBA', (size*scale, size*scale))
    draw = ImageDraw.Draw(image)
    factor = size*scale/256
    def box(values): return tuple(value*factor for value in values)
    draw.rounded_rectangle(box((8,8,248,248)), radius=60*factor, fill='#11151b')
    draw.ellipse(box((67,87,163,183)), fill='#93e9c1')
    def point(t):
        u=1-t
        return ((u**3*44+3*u*u*t*82+3*u*t*t*210+t**3*207)*factor,
                (u**3*177+3*u*u*t*217+3*u*t*t*137+t**3*74)*factor)
    path=[point(index/128) for index in range(129)]
    left,right=[],[]
    for index,at in enumerate(path):
        a,b=path[max(0,index-1)],path[min(len(path)-1,index+1)]
        dx,dy=b[0]-a[0],b[1]-a[1]
        length=math.hypot(dx,dy)
        nx,ny=-dy/length*5.5*factor,dx/length*5.5*factor
        left.append((at[0]+nx,at[1]+ny));right.append((at[0]-nx,at[1]-ny))
    draw.polygon(left+right[::-1],fill='#edf3f6')
    draw.ellipse(box((38.5,171.5,49.5,182.5)), fill='#edf3f6')
    draw.ellipse(box((196,63,218,85)), fill='#edf3f6')
    return image.resize((size,size), Image.Resampling.LANCZOS)

sizes=[16,20,24,32,40,48,64,128,256]
frames=[render(size) for size in sizes]
frames[-1].save(root/'OrbitRing.ico',format='ICO',append_images=frames[:-1],sizes=[(size,size) for size in sizes])
frames[-1].save(root/'OrbitRing.png')
