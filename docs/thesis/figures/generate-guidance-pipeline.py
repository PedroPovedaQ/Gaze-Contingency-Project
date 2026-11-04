"""Guidance pipeline figure: real application screens along the path from gaze to spoken cue.

The search scene comes from the Unity room render and Trial Review from a development recording,
so the figure shows software, not participant data.
The 1000-px canvas prints at text width and is nearly square, so it fills most of a page.
Run: DYLD_FALLBACK_LIBRARY_PATH=/opt/homebrew/lib uv run --no-project --with cairosvg --with pypdf --with pillow python docs/thesis/figures/generate-guidance-pipeline.py
"""
import base64
import io
import subprocess
import sys
from pathlib import Path
from xml.sax.saxutils import escape
import cairosvg
from PIL import Image

F = Path(__file__).resolve().parent
EXPERIMENT = Path('/Users/pedro.poveda/.codex/worktrees/gaze-recovered/Gaze Contingency Project')
AVATARS = EXPERIMENT / 'Assets/Resources/VoiceAvatars'
INK, MUTED, LINE, PALE = '#2a3034', '#6b7479', '#3f464a', '#eef1f3'
SELF, NEUTRAL, TEAL = '#7199D6', '#E8B44F', '#2f7f83'
W, H, TOP = 1000, 968, 6
TS, DS = 17, 15
s = [f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="{W}" height="{H}" viewBox="0 {TOP} {W} {H}">',
     f'<rect y="{TOP}" width="{W}" height="{H}" fill="white"/>',
     f'<defs><marker id="a" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="5.5" markerHeight="5.5" orient="auto"><path d="M0,0 L10,5 L0,10 z" fill="{LINE}"/></marker>'
     f'<marker id="t" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="5.5" markerHeight="5.5" orient="auto"><path d="M0,0 L10,5 L0,10 z" fill="{TEAL}"/></marker></defs>']


def text(x, y, lines, size=14, bold=False, anchor='middle', color=INK, italic=False):
    for i, line in enumerate([lines] if isinstance(lines, str) else lines):
        s.append(f'<text x="{x}" y="{y + i * size * 1.22:.1f}" text-anchor="{anchor}" font-family="Helvetica,Arial,sans-serif" '
                 f'font-size="{size}" font-weight="{"bold" if bold else "normal"}" font-style="{"italic" if italic else "normal"}" fill="{color}">{escape(line)}</text>')


def label(x, y, title, detail=None):
    titles = [title] if isinstance(title, str) else title
    text(x, y, titles, TS, True, color='#000000')
    if detail:
        text(x, y + len(titles) * TS * 1.22, detail, DS, color=MUTED)


def image(source, crop, x, y, w, h, border=True):
    picture = Image.open(source).convert('RGB')
    if crop:
        picture = picture.crop(crop)
    scale = min(w * 3 / picture.width, 1) if picture.width > w * 3 else 1
    picture = picture.resize((round(picture.width * scale), round(picture.height * scale)), Image.LANCZOS)
    data = io.BytesIO()
    picture.save(data, 'JPEG', quality=90)
    encoded = base64.b64encode(data.getvalue()).decode()
    s.append(f'<image x="{x}" y="{y}" width="{w}" height="{h}" preserveAspectRatio="xMidYMid slice" xlink:href="data:image/jpeg;base64,{encoded}"/>')
    if border:
        s.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="none" stroke="{INK}" stroke-width="1.6"/>')


def portrait(name, x, y, size, color):
    picture = Image.open(AVATARS / f'{name}.png').convert('RGBA').resize((size * 3, size * 3), Image.LANCZOS)
    background = Image.new('RGBA', picture.size, 'white')
    background.alpha_composite(picture)
    data = io.BytesIO()
    background.convert('RGB').save(data, 'JPEG', quality=92)
    encoded = base64.b64encode(data.getvalue()).decode()
    s.append(f'<image x="{x}" y="{y}" width="{size}" height="{size}" xlink:href="data:image/jpeg;base64,{encoded}"/>')
    s.append(f'<rect x="{x}" y="{y}" width="{size}" height="{size}" rx="5" fill="none" stroke="{color}" stroke-width="3"/>')


def route(points, dashed=False, color=LINE, radius=9, marker='a'):
    """Orthogonal connector with rounded corners."""
    d = [f'M{points[0][0]},{points[0][1]}']
    for i in range(1, len(points) - 1):
        (ax, ay), (bx, by), (cx, cy) = points[i - 1], points[i], points[i + 1]
        r1 = min(radius, (abs(bx - ax) + abs(by - ay)) / 2)
        r2 = min(radius, (abs(cx - bx) + abs(cy - by)) / 2)
        ux, uy = (bx > ax) - (bx < ax), (by > ay) - (by < ay)
        vx, vy = (cx > bx) - (cx < bx), (cy > by) - (cy < by)
        d.append(f'L{bx - ux * r1},{by - uy * r1} Q{bx},{by} {bx + vx * r2},{by + vy * r2}')
    d.append(f'L{points[-1][0]},{points[-1][1]}')
    style = ' stroke-dasharray="6 4"' if dashed else ''
    s.append(f'<path d="{" ".join(d)}" fill="none" stroke="{color}" stroke-width="2"{style} marker-end="url(#{marker})"/>')


def stroke(body, width=3):
    s.append(f'<g fill="none" stroke="{INK}" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round">{body}</g>')


def eye(cx, cy):
    stroke(f'<path d="M{cx - 26},{cy} Q{cx},{cy - 22} {cx + 26},{cy} Q{cx},{cy + 22} {cx - 26},{cy} z"/><circle cx="{cx}" cy="{cy}" r="8"/>')
    s.append(f'<circle cx="{cx}" cy="{cy}" r="3.5" fill="{INK}"/>')


def headset(cx, cy):
    stroke(f'<rect x="{cx - 24}" y="{cy - 12}" width="48" height="24" rx="9"/><path d="M{cx - 24},{cy - 2} h-7 M{cx + 24},{cy - 2} h7"/>'
           f'<circle cx="{cx - 10}" cy="{cy}" r="4.5"/><circle cx="{cx + 10}" cy="{cy}" r="4.5"/>')


def controller(cx, cy):
    stroke(f'<circle cx="{cx}" cy="{cy - 7}" r="13"/><path d="M{cx - 6},{cy + 5} l-3,17 h18 l-3,-17"/><circle cx="{cx}" cy="{cy - 7}" r="3"/>')


def cloud(cx, cy):
    stroke(f'<path d="M{cx - 34},{cy + 18} h66 a17,17 0 0 0 3,-34 a24,24 0 0 0 -45,-9 a18,18 0 0 0 -27,14 a15,15 0 0 0 3,29 z"/>')
    bars = ''.join(f'<path d="M{cx - 15 + i * 7.5},{cy + 3 - h} v{2 * h}"/>' for i, h in enumerate((3, 8, 11, 6, 3)))
    stroke(bars, 2.6)


def form_mic(cx, cy):
    """Intake form with a microphone badge."""
    x, y = cx - 46, cy - 64
    s.append(f'<rect x="{x}" y="{y}" width="84" height="112" rx="6" fill="white"/>')
    stroke(f'<rect x="{x}" y="{y}" width="84" height="112" rx="6"/>' + ''.join(
        f'<rect x="{x + 12}" y="{y + 18 + i * 22}" width="9" height="9" rx="1.5"/><path d="M{x + 28},{y + 22.5 + i * 22} h{40 - 12 * (i == 2)}"/>'
        for i in range(3)), 2.6)
    mx, my = cx + 34, cy + 40
    s.append(f'<circle cx="{mx}" cy="{my}" r="28" fill="white" stroke="{INK}" stroke-width="2.6"/>')
    stroke(f'<rect x="{mx - 7}" y="{my - 18}" width="14" height="24" rx="7"/>'
           f'<path d="M{mx - 13},{my - 3} q0,16 13,16 q13,0 13,-16 M{mx},{my + 13} v6 M{mx - 7},{my + 19} h14"/>', 2.4)


def speaker(cx, cy):
    stroke(f'<circle cx="{cx - 20}" cy="{cy}" r="4"/>' + ''.join(
        f'<path d="M{cx - 15 + i * 9},{cy - 8 - i * 7} q{9 + i * 2},{8 + i * 7} 0,{16 + i * 14}"/>' for i in range(3)))


def table(cx, cy):
    stroke(f'<rect x="{cx - 26}" y="{cy - 20}" width="52" height="40" rx="3"/><path d="M{cx - 26},{cy - 7} h52 M{cx - 26},{cy + 7} h52 M{cx - 9},{cy - 20} v40 M{cx + 9},{cy - 20} v40"/>', 2.6)


def band(y, h, name):
    s.append(f'<rect x="10" y="{y}" width="{W - 20}" height="{h}" rx="10" fill="{PALE}" opacity="0.55"/>')
    text(24, y + 25, name, 17, True, 'start', MUTED)


def scaled(k, cx, cy, draw):
    s.append(f'<g transform="translate({cx},{cy}) scale({k}) translate({-cx},{-cy})">')
    draw(cx, cy)
    s.append('</g>')


# Bands
band(10, 280, 'Before the session')
band(302, 428, 'During each trial')
band(742, 228, 'After the session')

# Row 1: voice preparation
form_mic(350, 118)
label(350, 222, ['Audio Intake,', 'Clone and Synthesize'])
route([(420, 130), (632, 130)])
portrait('Eric', 640, 90, 80, NEUTRAL)
portrait('Janet', 730, 90, 80, NEUTRAL)
text(725, 192, 'Selected Agent Voice', 15, True, color='#9a6f12')
s.append(f'<rect x="850" y="90" width="80" height="80" rx="5" fill="white" stroke="{SELF}" stroke-width="3"/>')
s.append(f'<g transform="translate(850,90) scale({80 / 62}) translate(-864,-56)">')
stroke('<circle cx="887" cy="76" r="9"/><path d="M871,110 q2,-20 16,-20 q14,0 16,20"/><path d="M906,72 q5,6 0,12 M913,67 q9,11 0,22"/>', 2.6)
s.append('</g>')
text(890, 192, 'Self-Similar Voice', 15, True, color='#3f6fb5')
label(790, 232, 'Voice Conditions')

# Row 2: guidance loop
image(F / 'virtual-room.png', (90, 66, 1858, 1150), 25, 395, 180, 112)
label(115, 535, ['Search Scene'])
route([(205, 450), (230, 450)])
s.append(f'<rect x="230" y="350" width="85" height="205" rx="8" fill="white" stroke="{INK}" stroke-width="1.8"/>')
scaled(1.15, 272, 390, eye)
scaled(1.15, 272, 450, headset)
scaled(1.15, 272, 512, controller)
label(272, 582, ['Gaze, Head,', 'Controller'])
route([(315, 450), (333, 450)])
s.append(f'<rect x="335" y="345" width="185" height="215" rx="8" fill="white" stroke="{INK}" stroke-width="1.8"/>')
text(428, 372, ['Guidance State'], 17, True, color='#000000')
for i, item in enumerate(['Gaze valid and recent', 'Angle to target, trend', 'Direction to target', 'Recent gaze direction', 'Last cue and response', 'Past cues and trials', 'Allowed strategies']):
    s.append(f'<circle cx="349" cy="{395 + i * 22:.1f}" r="2.8" fill="{TEAL}"/>')
    text(358, 400 + i * 22, item, 14, anchor='start', color=MUTED)
label(428, 585, ['Summarize, Then Ask'])
text(570, 425, ['Wireless', 'ADB link'], 13.5, color=TEAL, italic=True)
s.append(f'<rect x="620" y="328" width="180" height="320" rx="10" fill="#fff6dc" stroke="{NEUTRAL}" stroke-width="2" stroke-dasharray="7 4"/>')
s.append(f'<rect x="628" y="340" width="164" height="235" rx="8" fill="white" stroke="{INK}" stroke-width="1.8"/>')
text(710, 366, ['Two Decisions'], 17, True, color='#000000')
text(710, 392, '1. Strategy', 15, True, color=TEAL)
text(710, 413, ['Choose one allowed:', 'stay silent, orient,', 'refine, correct,', 'or encourage'], 14, color=MUTED)
text(710, 500, '2. Phrase', 15, True, color=TEAL)
text(710, 521, ['Rank the prepared', 'phrases using', 'their recent use'], 14, color=MUTED)
label(710, 605, ['Decision Model'])
text(710, 636, 'Local LLM', 14, True, color='#8a6410')
route([(520, 450), (626, 450)])
route([(792, 450), (862, 450)])
text(831, 418, ['Chosen', 'phrase'], 13.5, color=TEAL, italic=True)
scaled(1.15, 893, 450, speaker)
scaled(1.15, 950, 450, headset)
label(925, 505, ['Spoken Cue'])
route([(925, 244), (925, 420)])
route([(925, 520), (925, 712), (428, 712), (428, 600)], color=TEAL, marker='t')
text(680, 703, 'What actually played feeds back into memory', 14, color=TEAL, italic=True)

# Row 3: review and export
route([(272, 612), (272, 812)])
text(262, 660, ['Logged every frame', 'Starting angle θ₀', 'saved each trial'], 14, anchor='end', color=TEAL, italic=True)
scaled(1.2, 272, 840, table)
label(272, 895, ['Session Recordings'])
route([(305, 840), (422, 840)])
text(363, 828, 'Transfer', 13.5, color=TEAL, italic=True)
image(EXPERIMENT / 'trial-viewer-shots/real-dev-20261103/01-first-person-guidance.png', (0, 70, 1440, 890), 425, 772, 240, 137)
label(545, 933, ['Trial Review'])
route([(665, 840), (795, 840)])
text(730, 828, 'Export', 13.5, color=TEAL, italic=True)
scaled(1.2, 830, 840, table)
label(830, 895, ['Trial CSVs'])
s.append('</svg>')

svg = ''.join(s)
name = 'guidance-pipeline'
(F / f'{name}.svg').write_text(svg)
cairosvg.svg2pdf(bytestring=svg.encode(), write_to=str(F / f'{name}.pdf'))
cairosvg.svg2png(bytestring=svg.encode(), write_to=f'/tmp/{name}.png', output_width=2000)
subprocess.run([sys.executable, str(F / 'normalize-pdf-font-maps.py'), str(F / f'{name}.pdf')], check=True)
print('wrote', name)
