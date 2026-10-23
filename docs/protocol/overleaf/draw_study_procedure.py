from pathlib import Path
from math import sin,cos,pi,atan2
from reportlab.graphics.shapes import Drawing,Rect,Line,String,Polygon,Circle,PolyLine
from reportlab.graphics import renderPDF,renderSVG
from reportlab.lib.colors import HexColor, Color, white
R=Path(__file__).resolve().parent;R.mkdir(exist_ok=True)
W,H=1600,650;d=Drawing(W,H)
ink='#20282B'; muted='#526165'; teal='#397A80'; pale='#EAF3F3';gold='#AD8000';yellow='#FFF5D4'
def rect(x,y,w,h,fill='#FFFFFF',stroke=ink,r=8):d.add(Rect(x,H-y-h,w,h,rx=r,ry=r,fillColor=HexColor(fill),strokeColor=HexColor(stroke),strokeWidth=1.6))
def text(x,y,s,size=16,bold=False,color=ink,anchor='start'):d.add(String(x,H-y,s,fontName='Helvetica-Bold' if bold else 'Helvetica',fontSize=size,fillColor=HexColor(color),textAnchor=anchor))
def lines(x,y,arr,size=16,step=25,color=ink):
 for i,s in enumerate(arr):text(x,y+i*step,s,size,color=color)
def arrow(points,color=ink,width=2,dash=None):
 pts=[(x,H-y) for x,y in points];p=PolyLine([v for xy in pts for v in xy],strokeColor=HexColor(color),strokeWidth=width,fillColor=None)
 if dash:p.strokeDashArray=dash
 d.add(p);x,y=pts[-1];px,py=pts[-2];a=atan2(y-py,x-px);d.add(Polygon([x,y,x-10*cos(a)+4*sin(a),y-10*sin(a)-4*cos(a),x-10*cos(a)-4*sin(a),y-10*sin(a)+4*cos(a)],fillColor=HexColor(color),strokeColor=None))
def circle(x,y,r,fill,stroke=ink): d.add(Circle(x,H-y,r,fillColor=HexColor(fill),strokeColor=HexColor(stroke),strokeWidth=1.3))

rect(0,0,W,H,stroke='#FFFFFF',r=0)
text(25,35,'(a) Study procedure',24,True)
steps=[('Consent + baseline',['Background and experience','Pre-exposure symptoms']),('Voice setup',['Preferred catalog voice','Natural-voice recording']),('Calibration + practice',['Independent validation','1 practice trial']),('Block 1',['40 measured trials','Assigned first voice']),('Break',['Rest and comfort check','Tracking recheck']),('Block 2',['40 measured trials','Other assigned voice']),('Final measures',['Ratings for both blocks','Post-SSQ + debrief'])]
for i,(title,body) in enumerate(steps):
 x=25+i*225;fill=yellow if i in [3,5] else pale
 rect(x,78,200,140,fill, gold if i in [3,5] else teal)
 text(x+14,105,str(i+1).zfill(2),14,True,color=gold if i in [3,5] else teal)
 text(x+14,137,title,17,True)
 lines(x+14,166,body,14,24)
 if i<6:arrow([(x+200,148),(x+222,148)])
text(800,258,'Counterbalanced voice order: preferred -> self-similar  |  self-similar -> preferred',18,True,anchor='middle')
text(800,287,'80 measured trials + 1 practice trial; identical guidance policy and right-controller selection',17,anchor='middle')
text(25,340,'(b) Within each trial',24,True)
trials=[('Readiness cross',['Valid gaze establishes readiness','Target identity remains hidden']),('Directional opening cue',['Objects remain hidden','Cue plays before search timing']),('Reveal + guided search',['Objects appear; timer starts','Turn, refine, acquire target']),('Correct trigger selection',['Right ray + fresh trigger press','Timer stops; success feedback'])]
for i,(title,body) in enumerate(trials):
 x=25+i*400;rect(x,375,350,133,yellow if i==2 else '#F8FAFA',gold if i==2 else teal)
 text(x+18,410,title,20,True);lines(x+18,447,body,16,26)
 if i<3:arrow([(x+350,439),(x+395,439)])
rect(25,551,1550,75,'#F3F5F5','#A6AFB2',5)
text(800,580,'Primary outcome: active search-completion time, adjusted for pre-cue starting theta',19,True,anchor='middle')
text(800,608,'Height delta = target Y - readiness-cross Y. Questionnaires and breaks remain outside active-search timing.',16,anchor='middle')
renderPDF.drawToFile(d,str(R/'study-procedure-current.pdf'))
renderSVG.drawToFile(d,str(R/'study-procedure-current.svg'))
print(R)
