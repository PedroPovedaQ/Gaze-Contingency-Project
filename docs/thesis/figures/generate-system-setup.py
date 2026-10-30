"""System setup figure: two voice conditions feeding one shared guidance system.

Colors match the study procedure figure and voice-condition panels:
neutral (generic agent) voice gold, self-similar voice blue.
Run: DYLD_FALLBACK_LIBRARY_PATH=/opt/homebrew/lib uv run --no-project --with cairosvg --with pypdf python docs/thesis/figures/generate-system-setup.py
"""
import subprocess, sys
from pathlib import Path
from xml.sax.saxutils import escape
import cairosvg

F = Path(__file__).resolve().parent
INK, LINE = '#1f2a30', '#56636b'
SHARED, SHARED_EDGE = '#eef1f3', '#a9b4ba'
SELF, SELF_LIGHT = '#7199D6', '#d4e1f4'
NEUTRAL, NEUTRAL_LIGHT = '#E8B44F', '#f8e6c0'
W, H = 1000, 330
s = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">',
     f'<rect width="{W}" height="{H}" fill="white"/>',
     '<defs><marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto">'
     f'<path d="M0,0 L10,5 L0,10 z" fill="{LINE}"/></marker></defs>']


def text(x, y, lines, size=18, bold=False, anchor='start'):
    for i, line in enumerate([lines] if isinstance(lines, str) else lines):
        s.append(f'<text x="{x}" y="{y + i * size * 1.3:.1f}" text-anchor="{anchor}" '
                 f'font-family="Helvetica,Arial,sans-serif" font-size="{size}" '
                 f'font-weight="{"bold" if bold else "normal"}" fill="{INK}">{escape(line)}</text>')


def box(x, y, w, h, fill, edge, title, body):
    s.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="10" fill="{fill}" stroke="{edge}" stroke-width="2"/>')
    text(x + w / 2, y + 40, title, 22, True, 'middle')
    text(x + w / 2, y + 72, body, 18, anchor='middle')


def arrow(x1, y1, x2, y2):
    s.append(f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{LINE}" stroke-width="2.5" marker-end="url(#arrow)"/>')


BW, BH, TOP = 420, 110, 20
box(30, TOP, BW, BH, NEUTRAL_LIGHT, NEUTRAL, 'Generic Agent Voice', '(User chooses from a catalog of voices)')
box(W - 30 - BW, TOP, BW, BH, SELF_LIGHT, SELF, 'Self-Similar Voice', '(Voice is matched to the user)')
SY, SW, SH = 205, 640, 110
SX = (W - SW) / 2
arrow(30 + BW / 2, TOP + BH + 2, SX + SW * .3, SY - 3)
arrow(W - 30 - BW / 2, TOP + BH + 2, SX + SW * .7, SY - 3)
box(SX, SY, SW, SH, SHARED, SHARED_EDGE, 'Shared LLM-Based Guidance System',
    '(Audio-based cues from trial state and target object location)')
s.append('</svg>')

svg = ''.join(s)
name = 'system-setup'
(F / f'{name}.svg').write_text(svg)
cairosvg.svg2pdf(bytestring=svg.encode(), write_to=str(F / f'{name}.pdf'))
cairosvg.svg2png(bytestring=svg.encode(), write_to=f'/tmp/{name}.png', output_width=1500)
subprocess.run([sys.executable, str(F / 'normalize-pdf-font-maps.py'), str(F / f'{name}.pdf')], check=True)
print('wrote', name)
