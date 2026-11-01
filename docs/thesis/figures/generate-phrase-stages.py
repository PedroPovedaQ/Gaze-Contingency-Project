"""Example spoken phrases at each stage of a trial, quoted exactly from DirectionalHintPolicy,
VoicePromptText and RoundEncouragement. The 1000-px canvas prints at text width.
Run: DYLD_FALLBACK_LIBRARY_PATH=/opt/homebrew/lib uv run --no-project --with cairosvg --with pypdf python docs/thesis/figures/generate-phrase-stages.py
"""
import subprocess, sys
from pathlib import Path
from xml.sax.saxutils import escape
import cairosvg

F = Path(__file__).resolve().parent
INK, MUTED, LINE = '#1f2a30', '#56636b', '#8a969c'
FILL, EDGE = '#eef1f3', '#a9b4ba'
W, H = 1000, 512
s = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">',
     f'<rect width="{W}" height="{H}" fill="white"/>',
     '<defs><marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto">'
     f'<path d="M0,0 L10,5 L0,10 z" fill="{LINE}"/></marker></defs>']


def text(x, y, lines, size=15, bold=False, color=INK):
    for i, line in enumerate([lines] if isinstance(lines, str) else lines):
        s.append(f'<text x="{x}" y="{y + i * size * 1.28:.1f}" font-family="Helvetica,Arial,sans-serif" '
                 f'font-size="{size}" font-weight="{"bold" if bold else "normal"}" fill="{color}">{escape(line)}</text>')


def card(x, y, w, h, title, note, quotes):
    s.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="8" fill="{FILL}" stroke="{EDGE}" stroke-width="1.4"/>')
    text(x + 12, y + 24, title, 17, True)
    text(x + 12, y + 44, note, 13, color=MUTED)
    text(x + 12, y + 68, quotes, 15)


def arrow(x1, y1, x2, y2):
    s.append(f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{LINE}" stroke-width="1.8" marker-end="url(#arrow)"/>')


X0, CW, GAP, CH = 20, 232, 11, 130
rows = [(34, 'Coarse: find the object plane', [
            ('Readiness', 'Before each trial', ['“Look at the cross and', 'hold your gaze to begin', 'the next round.”']),
            ('Opening cue', 'Objects still hidden', ['“The object is to the', 'left.”']),
            ('Turning', 'Yaw only', ['“Turn way left.”', '“Keep turning left.”']),
            ('Arrival', 'Pitch joins in', ['“Right area. A little', 'higher.”'])]),
        (204, 'Fine: find the object', [
            ('Refinement', 'Yaw and pitch', ['“Look way higher.”', '“A little higher and left.”']),
            ('Progress', 'After measured progress', ['“Keep going to the left.”', '“A bit more.”']),
            ('On target', 'Gaze or pointing', ['“Right there.”', '“Yep, that\'s it!”']),
            ('Trigger reminder', 'Target held at the deadline', ['“Time up. Almost! Don\'t', 'forget to press the right', 'trigger to select the object.”'])])]
for y, label, cards in rows:
    text(20, y - 10, label, 18, True)
    for i, (t, n, q) in enumerate(cards):
        x = X0 + i * (CW + GAP)
        card(x, y, CW, CH, t, n, q)
        if i < 3:
            arrow(x + CW + 1, y + CH / 2, x + CW + GAP - 1, y + CH / 2)
Y = 374
text(20, Y - 10, 'As needed during a trial, and at the end', 18, True)
for i, (t, n, q) in enumerate([
        ('Correction', 'Overshoot or departure', ['“Go back a little to the', 'left.”']),
        ('Wrong selection', 'A tone, then speech', ['“Try another object.”']),
        ('Timeout', '25-second deadline', ['“Time up. Let\'s shake', 'that round off and start', 'the next.”']),
        ('Success', 'Correct selection', ['“Nice work. You found it', 'on your first selection.”'])]):
    card(X0 + i * (CW + GAP), Y, CW, CH, t, n, q)
s.append('</svg>')
svg = ''.join(s)
name = 'phrase-stages'
(F / f'{name}.svg').write_text(svg)
cairosvg.svg2pdf(bytestring=svg.encode(), write_to=str(F / f'{name}.pdf'))
cairosvg.svg2png(bytestring=svg.encode(), write_to=f'/tmp/{name}.png', output_width=1400)
subprocess.run([sys.executable, str(F / 'normalize-pdf-font-maps.py'), str(F / f'{name}.pdf')], check=True)
print('wrote', name)
