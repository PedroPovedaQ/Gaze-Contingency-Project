"""Study procedure figure: shared setup, color-coded counterbalanced voice orders, and trial timing.

Colors match the voice-condition panels (analysis/thesis_voice_condition_plots.py):
neutral voice gold, self-similar voice blue. The 1000-px canvas prints at text width,
so 15-px body text is about 7 pt on the page.
Run: DYLD_FALLBACK_LIBRARY_PATH=/opt/homebrew/lib uv run --no-project --with cairosvg --with pypdf python docs/thesis/figures/generate-study-procedure.py
"""
import subprocess, sys
from pathlib import Path
from xml.sax.saxutils import escape
import cairosvg

F = Path(__file__).resolve().parent
INK, MUTED, LINE = '#1f2a30', '#56636b', '#8a969c'
SETUP, SETUP_EDGE = '#eef1f3', '#a9b4ba'
SELF, SELF_LIGHT = '#7199D6', '#d4e1f4'
NEUTRAL, NEUTRAL_LIGHT = '#E8B44F', '#f8e6c0'
W, H = 1000, 715
s = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">',
     f'<rect width="{W}" height="{H}" fill="white"/>',
     '<defs><marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto">'
     f'<path d="M0,0 L10,5 L0,10 z" fill="{LINE}"/></marker></defs>']


def text(x, y, lines, size=15, bold=False, anchor='start', color=INK):
    for i, line in enumerate([lines] if isinstance(lines, str) else lines):
        s.append(f'<text x="{x}" y="{y + i * size * 1.25:.1f}" text-anchor="{anchor}" '
                 f'font-family="Helvetica,Arial,sans-serif" font-size="{size}" '
                 f'font-weight="{"bold" if bold else "normal"}" fill="{color}">{escape(line)}</text>')


def box(x, y, w, h, fill, edge, dash=False, width=1.4):
    s.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="8" fill="{fill}" stroke="{edge}" '
             f'stroke-width="{width}"' + (' stroke-dasharray="6 4"' if dash else '') + '/>')


def arrow(x1, y1, x2, y2):
    s.append(f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{LINE}" stroke-width="1.8" marker-end="url(#arrow)"/>')


def card(x, y, w, h, title, body, number=None, size=15):
    box(x, y, w, h, SETUP, SETUP_EDGE)
    ty = y + 24
    if number:
        text(x + 12, ty, number, 13, True, color=MUTED)
        ty += 21
    text(x + 12, ty, title, 18, True)
    text(x + 12, ty + 22, body, size)


X0, CW, GAP = 20, 232, 11


def row_of_cards(y, h, items, numbered):
    for i, (t, b) in enumerate(items):
        x = X0 + i * (CW + GAP)
        card(x, y, CW, h, t, b, f'0{i + 1}' if numbered else None)
        if i < len(items) - 1:
            arrow(x + CW + 1, y + h / 2, x + CW + GAP - 1, y + h / 2)


# (a) Study procedure
text(20, 30, '(a) Study procedure', 22, True)
SY, SH = 46, 126
row_of_cards(SY, SH, [('Consent', ['Signed consent before', 'any voice capture']),
                      ('Selected Voice', ['Audition the catalog;', 'choose the neutral voice']),
                      ('Audio intake', ['Nine spoken questions;', 'eligibility review;', 'clone and cache voice']),
                      ('Headset setup', ['Fit, vendor calibration,', 'spatial validation'])], True)

RY, RH, RG = 206, 96, 12
PX, PW = 118, 104
B1, BW = PX + PW + 12, 228
BK, KW = B1 + BW + 12, 86
B2 = BK + KW + 12
FX = B2 + BW + 14
FW = W - 20 - FX
arrow(W / 2, SY + SH + 3, W / 2, RY - 6)
rows = [('Order A', ['Self-similar', 'first'], (SELF, SELF_LIGHT, 'Self-similar voice'), (NEUTRAL, 'Neutral voice')),
        ('Order B', ['Neutral', 'first'], (NEUTRAL, NEUTRAL_LIGHT, 'Neutral voice'), (SELF, 'Self-similar voice'))]
for r, (name, sub, (c1, l1, v1), (c2, v2)) in enumerate(rows):
    y = RY + r * (RH + RG)
    text(20, y + 36, name, 19, True)
    text(20, y + 58, sub, 14, color=MUTED)
    box(PX, y, PW, RH, l1, c1, dash=True, width=2)
    text(PX + 10, y + 28, 'Practice', 16, True)
    text(PX + 10, y + 50, ['1 trial,', 'not analyzed'], 13)
    for bx, label, voice, color in ((B1, 'Block 1', v1, c1), (B2, 'Block 2', v2, c2)):
        box(bx, y, BW, RH, color, color)
        text(bx + 12, y + 30, label, 17, True)
        text(bx + 12, y + 54, voice, 16, True)
        text(bx + 12, y + 78, '32 measured trials', 14)
    box(BK, y, KW, RH, SETUP, SETUP_EDGE)
    text(BK + KW / 2, y + 40, 'Break', 16, True, 'middle')
    text(BK + KW / 2, y + 60, ['Tracking', 'recheck'], 13, anchor='middle')
    for a, b in ((PX + PW, B1), (B1 + BW, BK), (BK + KW, B2), (B2 + BW, FX)):
        arrow(a + 1, y + RH / 2, b - 1, y + RH / 2)
box(FX, RY, FW, 2 * RH + RG, SETUP, SETUP_EDGE)
text(FX + 12, RY + 24, '05', 13, True, color=MUTED)
text(FX + 12, RY + 46, 'Final survey', 17, True)
text(FX + 12, RY + 70, ['Both voices rated', 'side by side;', 'preference;', 'debrief'], 14)

LY = RY + 2 * RH + RG + 34
x = 20
for fill, edge, dash, label in ((SELF, SELF, False, 'Self-similar voice (participant’s clone)'),
                                (NEUTRAL, NEUTRAL, False, 'Neutral voice (Selected Voice)'),
                                (SELF_LIGHT, LINE, True, 'Practice: first assigned voice, excluded')):
    box(x, LY - 15, 26, 19, fill, edge, dash, 1.8)
    text(x + 34, LY, label, 14)
    x += 34 + len(label) * 7.4 + 34

# (b) Within each trial
TY = LY + 30
s.append(f'<line x1="20" y1="{TY}" x2="{W - 20}" y2="{TY}" stroke="#d5dbde" stroke-width="1.2"/>')
text(20, TY + 34, '(b) Within each trial', 22, True)
row_of_cards(TY + 50, 92, [('Readiness cross', ['Valid gaze sets readiness;', 'target stays hidden']),
                           ('Opening cue', ['Objects hidden; cue plays', 'before search timing']),
                           ('Reveal and search', ['Objects appear; timer', 'starts; agent guides']),
                           ('Trigger selection', ['Correct right-trigger', 'press stops the timer'])], False)
FY = TY + 50 + 92 + 30
text(20, FY, 'Primary outcome: active search-completion time, adjusted for pre-cue starting theta.', 15, True)
text(20, FY + 22, ['A 25-second active-search deadline ends unsuccessful attempts. Questionnaires and breaks are outside',
                   'active-search timing. Height delta = target Y − readiness-cross Y.'], 14, color=MUTED)
s.append('</svg>')

svg = ''.join(s)
name = 'study-procedure-current'
(F / f'{name}.svg').write_text(svg)
cairosvg.svg2pdf(bytestring=svg.encode(), write_to=str(F / f'{name}.pdf'))
cairosvg.svg2png(bytestring=svg.encode(), write_to=f'/tmp/{name}.png', output_width=1500)
subprocess.run([sys.executable, str(F / 'normalize-pdf-font-maps.py'), str(F / f'{name}.pdf')], check=True)
print('wrote', name)
