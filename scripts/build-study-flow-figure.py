"""Rebuild the original October 1 study schematic from vector primitives.

Requires reportlab; emits an editable SVG and vector PDF. Render the PDF to PNG
with pdftoppm for inspection. Content reflects the local runtime and a proposed
operator flow, not an approved protocol or hardware validation.
"""
from pathlib import Path
import math
from reportlab.graphics.shapes import Drawing, Rect, Circle, Line, Polygon, String
from reportlab.graphics import renderPDF, renderSVG
from reportlab.lib.colors import HexColor, white
from reportlab.pdfbase.pdfmetrics import stringWidth

ROOT = Path(__file__).resolve().parents[1]
SVG = ROOT / 'resources/figures/follow-my-voice-study-flow-20261001.svg'
PDF = ROOT / 'output/pdf/follow-my-voice-study-flow-20261001.pdf'
INK, MUTED, RULE = map(HexColor, ['#172535', '#536371', '#B8C3CB'])
BLUE, TEAL, TINT = map(HexColor, ['#235D92', '#237568', '#F1F5F7'])
W, H = 510, 530  # 180 mm wide; use at full two-column width, not reduced to one.
d = Drawing(W + 16, H + 16)
d.translate(8, 8)
d.add(Rect(0, 0, W, H, fillColor=white, strokeColor=None))

def text(x, y, value, size=8, color=INK, anchor='start', bold=False):
    font = 'Helvetica-Bold' if bold else 'Helvetica'
    width = stringWidth(value, font, size)
    left = x - (width / 2 if anchor == 'middle' else width if anchor == 'end' else 0)
    if left < 0 or left + width > W or y < 0 or y + size > H:
        raise ValueError('Text exceeds figure bounds: '+value)
    d.add(String(x, y, value, fontName=font,
                 fontSize=size, fillColor=color, textAnchor=anchor))

def line(x1, y1, x2, y2, color=RULE, width=.8, dashed=False):
    d.add(Line(x1, y1, x2, y2, strokeColor=color, strokeWidth=width,
               strokeDashArray=[3, 2] if dashed else None))

def arrow(x1, y1, x2, y2, color=MUTED, width=.8):
    line(x1, y1, x2, y2, color, width)
    theta=math.atan2(y2-y1, x2-x1); length=4
    points=[x2,y2]
    for angle in (theta+2.65, theta-2.65):
        points += [x2+length*math.cos(angle), y2+length*math.sin(angle)]
    d.add(Polygon(points, fillColor=color, strokeColor=None))

def box(x, y, w, h, lines, accent=False, size=8):
    d.add(Rect(x,y,w,h,rx=3,ry=3,fillColor=TINT if accent else white,
               strokeColor=BLUE if accent else RULE,strokeWidth=.9))
    gap=10; first=y+h/2+(len(lines)-1)*gap/2-2.5
    for i,value in enumerate(lines):
        if stringWidth(value,'Helvetica',size)>w-10:
            raise ValueError('Text exceeds box: '+value)
        text(x+w/2,first-i*gap,value,size,anchor='middle')

text(0, 514, 'Follow My Voice: setup and session flow', 14, bold=True)
text(0, 499, 'October 1, 2026  |  Local implementation + proposed operator flow; headset acceptance pending',7.8,MUTED)
line(0,489,W,489)
text(0,472,'A  Seated mixed-reality setup',10,bold=True)
text(270,472,'B  Session flow',10,bold=True)
text(270,459,'Preferred / self-similar voice order is counterbalanced',7.4,MUTED)

# Original top-view schematic. Virtual panels are dashed; equipment is solid.
cx,cy,radius=122,341,83
vertices=[(cx+radius*math.cos(math.radians(22.5+i*45)),
           cy+radius*math.sin(math.radians(22.5+i*45))) for i in range(8)]
for i,(x1,y1) in enumerate(vertices):
    x2,y2=vertices[(i+1)%8]
    line(x1,y1,x2,y2,MUTED,1,True)
    for f in (.16,.32,.5,.68,.84):
        x,y=x1+(x2-x1)*f,y1+(y2-y1)*f
        x,y=cx+(x-cx)*.95,cy+(y-cy)*.95
        d.add(Circle(x,y,1.6,fillColor=RULE,strokeColor=None))
arrow(cx,433,cx,451,INK);text(cx+10,440,'Fixed room-front reference',7.2)
d.add(Circle(cx,cy-8,21,fillColor=TINT,strokeColor=INK,strokeWidth=.8))
d.add(Rect(cx-11,cy-18,22,21,rx=5,ry=5,fillColor=HexColor('#D8E1E7'),strokeColor=INK,strokeWidth=.7))
d.add(Circle(cx,cy+8,10,fillColor=white,strokeColor=INK,strokeWidth=.8))
d.add(Rect(cx-11,cy+10,22,5,rx=2,ry=2,fillColor=INK,strokeColor=None))
d.add(Rect(cx+25,cy-7,5,11,rx=2,ry=2,fillColor=BLUE,strokeColor=None))
arrow(cx,cy+17,cx-13,cy+72,TEAL,1)
arrow(cx+28,cy+5,cx+62,cy+44,BLUE,1)
text(cx,cy-39,'Swivel chair + headset',7.5,anchor='middle')
text(cx+35,cy-21,'Right',7.1,BLUE);text(cx+35,cy-30,'controller',7.1,BLUE)
text(cx,246,'8 virtual surfaces around the seated participant',7.5,anchor='middle')
text(cx,233,'168 scattered objects; dots are schematic',7.5,MUTED,anchor='middle')
line(4,217,20,217,MUTED,1,True);text(25,214,'Virtual surface, not a physical wall',7.5)
line(4,202,20,202,TEAL,1);text(25,199,'Eye gaze informs guidance and measurement',7.5)
line(4,187,20,187,BLUE,1);text(25,184,'Right ray highlights; trigger selects',7.5)

steps=[
 ['Consent and baseline questions'],
 ['Fit; choose gender + 1 of 6 voices', 'Natural sample + audio level checks'],
 ['Eye calibration + independent spatial check'],
 ['2 practice trials: one with each voice'],
 ['Block 1: 40 measured trials'],
 ['Block questions, break, fit / drift check'],
 ['Block 2: 40 trials with the other voice'],
 ['Block questions, final interview and debrief'],
]
for i,labels in enumerate(steps):
    y=424-i*32
    box(270,y,240,25,labels,accent=i in (4,6),size=7.9)
    if i<7:arrow(390,y,390,y-7)
text(270,184,'Setup activities are grouped; final survey route is open.',7.2,MUTED)

line(0,168,W,168)
text(0,151,'Within each trial',10,bold=True)
labels=[['Previous-wall cross', '+ spoken readiness'],['Reveal objects;', 'start search clock'],['Horizontal guidance,', 'then up / down'],['Right-ray highlight;', 'trigger confirmation'],['Record correct capture;', 'chime + praise, advance']]
for i,lines in enumerate(labels):
    x=i*104
    box(x,103,94,31,lines,accent=i in (2,3),size=7.2)
    if i<4:arrow(x+95,118,x+103,118)
line(359,103,359,87);line(359,87,255,87);arrow(255,87,255,103)
text(305,75,'Incorrect: same target; continue',7.2,MUTED,anchor='middle')
text(0,59,'The agent knows the target; its identity is not announced or shown to the participant.',8,bold=True)
text(0,44,'Requested pauses stop active-search timing. Invalid / stale gaze suspends new guidance decisions.',7.6)
text(0,28,'Implementation: 40 + 40 trials, shared command wording, right controller. Proposal: operator / survey flow.',7.5,MUTED)
text(0,15,'Open: final surveys, signed-height quotas, participant sample size, spatial / timing acceptance and hardware checks.',7.2,MUTED)
SVG.parent.mkdir(parents=True,exist_ok=True);PDF.parent.mkdir(parents=True,exist_ok=True)
renderSVG.drawToFile(d,str(SVG));renderPDF.drawToFile(d,str(PDF))
print(SVG);print(PDF)
