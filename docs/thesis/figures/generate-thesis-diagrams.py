from pathlib import Path
from html import escape
import math,cairosvg
import subprocess,sys
P=Path(__file__).resolve().parent.parent
F=P/'figures'
G='#8a630a'; B='#245b78'; T='#173c48'; grey='#617078'; light='#eef3f5'; gold='#fff0c7'
class Drawing:
 def __init__(self,h=600):
  self.h=h; self.s=['<svg xmlns="http://www.w3.org/2000/svg" width="1000" height="%s" viewBox="0 0 1000 %s"><defs><marker id="a" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto"><path d="M0 0L8 4L0 8Z" fill="#617078"/></marker></defs><rect width="1000" height="100%%" fill="white"/>'%(h,h)]
 def text(self,x,y,lines,size=21,bold=False,anchor='start',color=T):
  if isinstance(lines,str):lines=[lines]
  for i,line in enumerate(lines):self.s.append(f'<text x="{x}" y="{y+i*size*1.35}" text-anchor="{anchor}" font-family="Helvetica,Arial,sans-serif" font-size="{size}" font-weight="{"bold" if bold else "normal"}" fill="{color}">{escape(line)}</text>')
 def box(self,x,y,w,h,title,body=None,fill=light):
  self.s.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="12" fill="{fill}" stroke="#bac6cc"/>'); self.text(x+18,y+32,title,22,True)
  if body:self.text(x+18,y+65,body,19)
 def line(self,x1,y1,x2,y2,color=grey,arrow=True,dash=False,width=2.5):
  self.s.append(f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{color}" stroke-width="{width}"'+(' marker-end="url(#a)"' if arrow else '')+(' stroke-dasharray="8 6"' if dash else '')+'/>')
 def circle(self,x,y,r,fill='white',stroke=B):self.s.append(f'<circle cx="{x}" cy="{y}" r="{r}" fill="{fill}" stroke="{stroke}" stroke-width="3"/>')
 def path(self,d,color=B,fill='none',dash=False):self.s.append(f'<path d="{d}" fill="{fill}" stroke="{color}" stroke-width="3"'+(' stroke-dasharray="7 6"' if dash else '')+'/>')
 def title(self,t,sub):self.text(35,35,t,26,True);self.text(35,66,sub,18,color=grey)
 def save(self,name):
  s=''.join(self.s)+'</svg>'; (F/f'{name}.svg').write_text(s);cairosvg.svg2pdf(bytestring=s.encode(),write_to=str(F/f'{name}.pdf'))
  subprocess.run([sys.executable,str(F/'normalize-pdf-font-maps.py'),str(F/f'{name}.pdf')],check=True)

d=Drawing(480);d.title('Voice identity within a shared search task','Proposed within-participant comparison; arrows describe the design, not observed effects.')
d.box(35,105,420,125,'Neutral voice',['Participant chooses from the catalog.','Assigned to one measured block.'])
d.box(545,105,420,125,'Self-similar voice',['Synthesized from the standard passage.','Assigned to the other measured block.'],gold)
for x in [245,755]:d.line(x,230,x,270)
d.box(170,270,660,95,'Shared task and guidance mechanism',['Hidden target · gaze-contingent cues · right-trigger selection'])
d.line(500,365,500,405);d.text(500,440,'Compare performance and retrospective experience',23,True,'middle');d.save('comparison-framework')

d=Drawing(690);d.title('Guidance states and evidence','Implemented state families; arrows show common paths, not an exhaustive transition graph.')
d.box(35,100,280,85,'Opening',['Cue before reveal']);d.box(360,100,280,85,'Turning',['Outside target wall']);d.box(685,100,280,85,'Refining',['Within target wall'])
d.line(315,143,360,143);d.line(640,143,685,143)
d.box(685,245,280,100,'AcquiringTarget',['Valid winning vicinity;','qualification in progress']);d.line(825,185,825,245)
d.box(360,245,280,100,'Acquired',['Qualified gaze evidence;','no selection implied']);d.line(685,295,640,295)
d.box(35,245,280,100,'ReadyToSelect',['Qualified controller hold','with required gaze evidence']);d.line(360,295,315,295)
d.box(35,400,440,100,'Evidence can change the stage',['Overshoot, departure, and reacquisition','can return guidance to an earlier state.'])
d.box(525,400,440,100,'Paused / Suspended',['Pause or invalid/stale gaze suspends','new gaze-dependent instructions.'])
d.box(35,550,930,95,'Completed · Stopped · Inactive',['Correct trigger selection can complete any active search stage; acknowledgments are not prerequisites.'],gold)
d.save('guidance-states')

d=Drawing(600);d.title('Three different events','Implemented interaction gates; neither ray contact nor vicinity qualification establishes fixation.')
rows=[(110,'Gaze acknowledgment','Valid winning vicinity', '100 ms qualifying evidence','“You are looking at it.”'),(270,'Controller acknowledgment','Right ray intersects target','1 s hold + valid nearby gaze','“You are pointing at it.”'),(430,'Trial completion','Correct right-trigger selection','Trigger confirms selection','Search ends')]
for y,a,b,c,e in rows:
 d.box(30,y,290,120,a,[b],gold if y==430 else light);d.line(320,y+55,355,y+55);d.box(355,y,350,120,c,['Distinct event and timestamp']);d.line(705,y+55,740,y+55);d.text(745,y+45,e,18,True)
d.save('evidence-events')

d=Drawing(560);d.title('Bounded memory passed with current state','Implemented limits are engineering settings, not cognitive-memory estimates.')
d.box(35,105,450,180,'Spatial history',['Up to 3 s / 384 observations','Target-relative gaze and turn progress','Clears on invalidity, source change,','or observation gap greater than 100 ms'])
d.box(515,105,450,180,'Speech and transition history',['At most 4 speech + 4 state entries','Request excludes entries older than 15 s','Tracks actual speech and transitions','Clears when guidance resets'])
d.line(260,285,420,350);d.line(740,285,580,350)
d.box(250,350,500,95,'Immutable request snapshot',['Current evidence + eligible choices + memory'],gold)
d.line(500,445,500,490);d.text(500,525,'Recheck live state before any returned choice plays',22,True,'middle');d.save('bounded-memory')

d=Drawing(650);d.title('Nominal wall category versus exact target bearing','Conceptual overhead geometry; target position within a wall changes the realized angle.')
cx,cy=285,350;r=215
pts=[(cx+r*math.sin(math.radians(a)),cy-r*math.cos(math.radians(a))) for a in [22.5+45*k for k in range(8)]]
d.path('M'+'L'.join(f'{x},{y}' for x,y in pts)+'Z',color='#adb9bd')
d.circle(cx,cy,12,fill=B);d.text(cx-15,cy+43,'o',21,True)
d.line(cx,cy,cx,151,color=B);d.text(210,100,'Cross reference c',19)
d.line(cx,cy,426,209,color=grey,dash=True);d.text(455,180,'Wall center',18)
d.line(cx,cy,465,249,color=G);d.circle(465,249,7,fill=gold,stroke=G);d.text(480,272,'Target p',19)
d.path('M285 275 A75 75 0 0 1 347 308',G);d.text(318,261,'θ₀',24,True,color=G)
d.box(570,110,390,155,'Allocation category',['0°, 45°, 90°, 135°, or 180°','Eight trials per category per block','Uses nominal wall relationship'])
d.box(570,310,390,155,'Exact starting bearing',['Saved origin → cross versus target','Horizontal projection; signed angle','Pre-cue snapshot is still required'],gold)
d.text(45,610,'An octagon and target are schematic; this is not a recorded layout.',19,color=grey);d.save('starting-geometry')

d=Drawing(550);d.title('Vertical demand has an explicit reference','Proposed allocation uses cross-relative height, distinct from live eye-to-target guidance.')
d.line(80,440,520,440,arrow=False);d.text(80,475,'Side elevation',18,color=grey)
d.line(200,135,200,440,color=grey,arrow=False,dash=True)
d.line(175,300,225,300,color=B,arrow=False,width=5);d.line(200,275,200,325,color=B,arrow=False,width=5);d.text(55,270,'Cross c',20)
d.circle(450,170,12,fill=gold,stroke=G);d.text(350,140,'Target p above c',20)
d.line(230,300,485,300,arrow=False,dash=True);d.line(230,170,485,170,arrow=False,dash=True)
d.line(490,295,490,175,color=G);d.text(500,242,'Δh',24,True,color=G)
d.box(610,105,350,140,'Planned measured blocks',['20 targets above / 20 below','Near-zero rule remains open','Allocation not yet enforced'],gold)
d.box(610,285,350,145,'Live refinement',['Uses current gaze geometry','A neighboring highlight is not','automatically its reference'])
d.text(45,520,'Δh = target height − cross height; preserve sign and the saved reference.',21);d.save('height-reference')

d=Drawing(520);d.title('From evidence to software playback','Conceptual timing diagram; horizontal lengths are illustrative, not latency measurements.')
xs=[75,255,490,720,925];names=[['First valid','evidence'],['Qualification','tq'],['Decision /','admission'],['Final live','recheck'],['Playback','tp']]
d.line(75,260,940,260,width=4)
for x,n in zip(xs,names):d.circle(x,260,7,fill=B);d.text(x,190,n,20,True,'middle')
d.line(80,330,250,330,color=G,arrow=False,width=7);d.text(165,367,'Qualification gate',18,anchor='middle')
d.line(260,330,920,330,color=B,arrow=False,width=7);d.text(590,367,'Software delay: tp − tq',21,True,'middle')
d.box(75,405,850,85,'Attribution requires recorded events',['Model response, model wait, scheduling, existing speech, and audio readiness can overlap.'])
d.text(500,105,'Qualified local feedback can bypass a new Jev request.',20,anchor='middle');d.save('latency-definition')

d=Drawing(590);d.title('Evidence and derived data remain separate','Implemented recording architecture with proposed analysis and validation requirements.')
d.box(35,110,280,130,'Source records',['Gaze, head, controller poses','Object layout and identifiers','Events and playback starts'])
d.line(315,175,365,175);d.box(365,110,270,130,'Versioned processing',['Clock and validity checks','Trial and block joins','Missingness preserved'])
d.line(635,175,685,175);d.box(685,110,280,130,'Trial summaries',['Performance and geometry','Questionnaire references','Provenance and versions'],gold)
d.line(175,240,175,320);d.box(35,320,410,115,'Trial Review reconstruction',['Timeline, scene, state, and speech','Uses original observations and events'])
d.line(825,240,825,320);d.box(555,320,410,115,'Analysis tables',['Trials nested within participants','Ratings retain participant × block scope'])
d.text(500,510,['Derived CSV rows do not replace raw records.','Repeating a block score across 40 rows does not create 40 ratings.'],21,True,'middle');d.save('data-provenance')

d=Drawing(560);d.title('Questionnaire administration and reference scope','Protocol proposal: no between-block questionnaires; Qualtrics synchronization remains on hold.')
d.box(30,115,205,100,'Block 1',['40 measured trials']);d.box(280,115,160,100,'Break',['Comfort checks']);d.box(485,115,205,100,'Block 2',['40 measured trials']);d.box(735,115,235,100,'Final session',['Questionnaires + debrief'],gold)
for a,b in [(235,280),(440,485),(690,735)]:d.line(a,165,b,165)
d.box(100,310,365,140,'Reference: Block 1',['Workload, IMI, agent experience','Actual voice mapped by saved order','65 numeric ratings'])
d.box(535,310,365,140,'Reference: Block 2',['Same instruments and item versions','Actual voice mapped by saved order','65 numeric ratings'])
d.line(840,215,280,300);d.line(860,215,720,300)
d.text(500,515,'Administration time = final session; response scope = participant × referenced block.',20,True,'middle');d.save('survey-scopes')

d=Drawing(570);d.title('Repeated trials do not replace independent participants','Proposed analysis: within-participant contrast with explicit clustering and observation scope.')
d.box(25,105,950,400,'Participant i')
d.box(60,165,410,120,'Voice condition A',['40 trial outcomes with starting geometry','1 set of block-referenced ratings'])
d.box(530,165,410,120,'Voice condition B',['40 trial outcomes with starting geometry','1 set of block-referenced ratings'],gold)
d.line(270,285,400,340);d.line(730,285,600,340)
d.box(230,340,540,115,'Paired contribution to the study estimate',['Counterbalanced order + participant clustering','Separate performance and questionnaire models'])
d.text(500,545,'Repeat across participants; determine sample size through a prospective power analysis.',20,anchor='middle');d.save('analysis-units')
# Normalize font aliases in reused editable sources too.
for name in ['study-procedure-current','voice-guidance-memory-architecture']:
 p=F/f'{name}.svg';s=p.read_text().replace('font-family: Helvetica-Bold;', 'font-family: Helvetica; font-weight: bold;');p.write_text(s);cairosvg.svg2pdf(bytestring=s.encode(),write_to=str(F/f'{name}.pdf'))
print('Generated 10 new vector figures and refreshed 2 reused vectors.')
