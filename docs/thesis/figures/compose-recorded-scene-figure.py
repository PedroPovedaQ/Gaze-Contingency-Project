from pathlib import Path
from PIL import Image
import base64,io,json
import cairosvg
root=Path(__file__).resolve().parent
sources=root/'recorded-scene-sources'
def data(im):
 b=io.BytesIO();im.save(b,format='PNG');return 'data:image/png;base64,'+base64.b64encode(b.getvalue()).decode()
def img(im,x,y,w,h):return f'<image x="{x}" y="{y}" width="{w}" height="{h}" href="{data(im)}"/>'
def txt(x,y,t,size=24,fill='#111',weight='normal'):return f'<text x="{x}" y="{y}" font-family="Arial, sans-serif" font-size="{size}" fill="{fill}" font-weight="{weight}">{t}</text>'
a=Image.open(sources/'first-person-final-full.png').convert('RGB')
b=Image.open(sources/'overhead-framed-full.png').convert('RGB')
# Crop viewer chrome only. Source screenshots are retained unchanged.
scene=(363,267,1920,1088)
# Overhead centered square crop; selected after visual inspection of the full capture.
overhead=(500,267,1770,1088)
parts=['<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="2496" height="2596" viewBox="0 0 1248 1298">','<rect width="1248" height="1298" fill="white"/><g transform="translate(24,16)">']
parts += [txt(0,29,'(a) Recorded first-person view',26,weight='bold'),img(a.crop(scene),0,44,1200,633)]
parts += [txt(0,720,'(b) Overhead arrangement',26,weight='bold'),txt(866,720,'(c) Object examples',26,weight='bold')]
parts += [img(b.crop(overhead),0,738,830,520)]
# Close-ups rendered from recorded objects using an aimed, optically zoomed replay camera.
# No change to object meshes, transforms, colors or the recorded session time.
objects=[('Cube','r00_o89',(830,365,1450,985)),('Sphere','r00_o100',(920,455,1360,895)),('Pyramid','r00_o64',(940,475,1340,875)),('Cylinder','r00_o86',(850,385,1430,965)),('Capsule','r00_o93',(860,395,1420,955)),('Star','r00_o107',(930,435,1350,855))]
for i,(label,oid,crop) in enumerate(objects):
 x=866+(i%2)*172;y=752+(i//2)*168
 zoom=Image.open(sources/f'zoom-{label.lower()}-{oid}.png').convert('RGB')
 parts += [img(zoom.crop(crop),x,y,140,122),txt(x+6,y+148,label,21)]
parts += ['</g></svg>']
svg='\n'.join(parts)
(root/'recorded-search-scene.svg').write_text(svg)
cairosvg.svg2png(bytestring=svg.encode(),write_to=str(root/'recorded-search-scene.png'))
cairosvg.svg2pdf(bytestring=svg.encode(),write_to=str(root/'recorded-search-scene.pdf'))
print(root/'recorded-search-scene.png')
