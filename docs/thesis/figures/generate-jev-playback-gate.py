"""Editable, vector schematic of the implemented guidance admission boundary."""
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch, FancyArrowPatch

out=Path(__file__).resolve().parent
plt.rcParams.update({'font.family':'DejaVu Sans','font.size':11,'pdf.fonttype':42,'svg.fonttype':'none'})
fig,ax=plt.subplots(figsize=(9,7.8));ax.set(xlim=(0,10),ylim=(0,10));ax.axis('off')
ink='#173c48';blue='#edf4f8';gold='#fff0c7';gray='#64727a'
def box(x,y,w,h,title,body,fill=blue):
    ax.add_patch(FancyBboxPatch((x,y),w,h,boxstyle='round,pad=0.04,rounding_size=0.09',
        linewidth=1.2,edgecolor=gray,facecolor=fill))
    ax.text(x+.15,y+h-.2,title,va='top',fontsize=12,weight='bold',color=ink)
    ax.text(x+.15,y+h-.53,body,va='top',fontsize=10.5,linespacing=1.5,color=ink)
def arrow(a,b,label=None,at=None):
    ax.add_patch(FancyArrowPatch(a,b,arrowstyle='-|>',mutation_scale=13,color=gray,linewidth=1.3))
    if label:ax.text(*at,label,fontsize=10,color=ink,ha='center',va='center',bbox=dict(facecolor='white',edgecolor='none',pad=1.5))

box(.35,8.55,8.85,1.15,'1  UNITY: observe and update state',
    'Target geometry; gaze validity and object hits; controller aim; trial state\nRecent spoken phrases and transitions; audio playback status')
box(.35,6.55,4.15,1.35,'2  Filter eligible choices',
    'Keep directions valid for this state.\nSend a snapshot and recent history.\nOffer only valid strategies and phrases.')
box(5.05,6.55,4.15,1.35,'Qualified local feedback',
    'Gaze / pointing acknowledgments\nand eligible turn milestones can\nbypass a new model request.',fill='#eff3ed')
arrow((2.4,8.55),(2.4,7.9));arrow((7.1,8.55),(7.1,7.9))
box(.35,4.55,4.15,1.4,'3  MiniLM: decide strategy and wording',
    'Choose a strategy, or observe;\nthen rank the offered phrases.\nRequests are paced and bounded.')
arrow((2.4,6.55),(2.4,5.95))
ax.text(7.1,5.2,'No new model request\ndoes not bypass the gate.',ha='center',va='center',fontsize=11,color=ink,
        bbox=dict(facecolor='white',edgecolor='none',pad=4))
arrow((7.1,6.55),(7.1,3.75))
arrow((2.4,4.55),(2.4,3.75),'Candidate for playback', (2.4,4.14))
box(.35,2.0,8.85,1.75,'4  LOCAL SCHEDULER + FINAL PLAYBACK GATE',
    'Same trial and voice; request still current and not canceled?\nCurrent evidence still qualifies this action and direction?\nNo contradictory cue while the controller is on the target?\nPrepared clip ready; speech timing and repetition rules satisfied?',fill=gold)
# A second input emphasizes that the final gate does not reuse an old snapshot alone.
ax.plot([9.2,9.75,9.75],[9.1,9.1,2.85],color=gray,lw=1.3,linestyle='--')
arrow((9.75,2.85),(9.2,2.85))
ax.text(9.62,5.7,'Fresh Unity state at playback',rotation=90,ha='center',va='center',fontsize=9.5,color=ink,
        bbox=dict(facecolor='white',edgecolor='none',pad=2))
box(.35,.1,4.15,1.1,'Not admitted: do not play now',
    'Discard stale or invalid proposals;\nreconsider timing when appropriate.',fill='#f2f2f2')
box(5.05,.1,4.15,1.1,'Admitted: play prepared audio',
    'Record actual playback and update\nspeech memory; ElevenLabs voice.',fill='#eff3ed')
arrow((2.4,2),(2.4,1.2),'Fail', (2.4,1.6));arrow((7.1,2),(7.1,1.2),'Pass', (7.1,1.6))
fig.subplots_adjust(left=.01,right=.99,top=.99,bottom=.01)
for suffix in ['pdf','svg','png']:
    fig.savefig(out/f'jev-final-playback-gate.{suffix}',bbox_inches='tight',dpi=220)
plt.close(fig)
