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
     f'<marker id="k" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="5.5" markerHeight="5.5" orient="auto"><path d="M0,0 L10,5 L0,10 z" fill="#000000"/></marker>'
     f'<marker id="t" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="5.5" markerHeight="5.5" orient="auto"><path d="M0,0 L10,5 L0,10 z" fill="{TEAL}"/></marker></defs>']


def text(x, y, lines, size=14, bold=False, anchor='middle', color='#000000', italic=False):
    for i, line in enumerate([lines] if isinstance(lines, str) else lines):
        s.append(f'<text x="{x}" y="{y + i * size * 1.22:.1f}" text-anchor="{anchor}" font-family="Helvetica,Arial,sans-serif" '
                 f'font-size="{size}" font-weight="{"bold" if bold else "normal"}" font-style="{"italic" if italic else "normal"}" fill="{color}">{escape(line)}</text>')


def label(x, y, title, detail=None):
    titles = [title] if isinstance(title, str) else title
    text(x, y, titles, TS, True, color='#000000')
    if detail:
        text(x, y + len(titles) * TS * 1.22, detail, DS, color='#000000')


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


def emoji(char, x, y, size):
    """Apple Color Emoji glyph as an embedded bitmap; SVG renderers do not draw color emoji."""
    from PIL import ImageDraw, ImageFont
    font = ImageFont.truetype('/System/Library/Fonts/Apple Color Emoji.ttc', 160)
    picture = Image.new('RGBA', (160, 160), (0, 0, 0, 0))
    ImageDraw.Draw(picture).text((0, 0), char, font=font, embedded_color=True)
    data = io.BytesIO()
    picture.save(data, 'PNG')
    encoded = base64.b64encode(data.getvalue()).decode()
    s.append(f'<image x="{x}" y="{y}" width="{size}" height="{size}" xlink:href="data:image/png;base64,{encoded}"/>')


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


def headset_front(cx, cy):
    """Front view of a standalone headset: strap, visor shell, faceplate and its cameras."""
    s.append(f'<path d="M{cx - 30},{cy - 16} Q{cx},{cy - 40} {cx + 30},{cy - 16}" fill="none" stroke="#5b6368" stroke-width="5" stroke-linecap="round"/>')
    s.append(f'<rect x="{cx - 37}" y="{cy - 22}" width="74" height="46" rx="15" fill="#2f3539"/>')
    s.append(f'<rect x="{cx - 32}" y="{cy - 17}" width="64" height="36" rx="11" fill="#454d53" stroke="#6b757b" stroke-width="1"/>')
    s.append(f'<path d="M{cx - 8},{cy + 24} Q{cx},{cy + 14} {cx + 8},{cy + 24} z" fill="#f4f6f7"/>')
    for x in (cx - 10, cx + 10):
        s.append(f'<circle cx="{x}" cy="{cy - 2}" r="6" fill="#111417" stroke="#8a949a" stroke-width="1.4"/>')
        s.append(f'<circle cx="{x - 1.8}" cy="{cy - 3.8}" r="1.6" fill="#c9d1d5"/>')
    s.append(f'<rect x="{cx - 4}" y="{cy + 8}" width="8" height="3.5" rx="1.5" fill="#111417"/>')
    for x in (cx - 26, cx + 26):
        for y in (cy - 10, cy + 12):
            s.append(f'<circle cx="{x}" cy="{y}" r="2.6" fill="#111417" stroke="#8a949a" stroke-width=".8"/>')
    s.append(f'<circle cx="{cx + 16}" cy="{cy - 12}" r="1.2" fill="#7ad3a8"/>')


def ear(cx, cy, k=2.4):
    """Ear outline (24-unit icon grid, scaled)."""
    s.append(f'<g transform="translate({cx - 12 * k},{cy - 12 * k}) scale({k})" fill="none" stroke="{INK}" stroke-width="{2.6 / k:.3f}" stroke-linecap="round" stroke-linejoin="round">'
             '<path d="M6 8.5a6.5 6.5 0 1 1 13 0c0 6-6 6-6 10a3.5 3.5 0 1 1-7 0"/><path d="M15 8.5a2.5 2.5 0 0 0-5 0v1a2 2 0 1 1 0 4"/></g>')


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
    text(24, y + 25, name, 17, True, 'start', '#000000')


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
text(725, 192, 'Selected Agent Voice', 15, True, color='#000000')
s.append(f'<rect x="850" y="90" width="80" height="80" rx="5" fill="white" stroke="{SELF}" stroke-width="3"/>')
s.append(f'<g transform="translate(850,90) scale({80 / 62}) translate(-864,-56)">')
stroke('<circle cx="887" cy="76" r="9"/><path d="M871,110 q2,-20 16,-20 q14,0 16,20"/><path d="M906,72 q5,6 0,12 M913,67 q9,11 0,22"/>', 2.6)
s.append('</g>')
text(890, 192, 'Self-Similar Voice', 15, True, color='#000000')
label(790, 74, 'Voice Conditions')

# Row 2: guidance loop
# The decision model region encloses state, link, LLM pipeline and its memory loop.
s.append(f'<rect x="326" y="324" width="474" height="400" rx="12" fill="#fff6dc" stroke="{NEUTRAL}" stroke-width="2" stroke-dasharray="7 4"/>')
image(F / 'virtual-room.png', (90, 66, 1858, 1150), 25, 395, 180, 112)
label(115, 535, ['Search Scene'])
route([(205, 450), (230, 450)])
s.append(f'<rect x="230" y="395" width="85" height="110" rx="8" fill="white" stroke="{INK}" stroke-width="1.8"/>')
headset_front(272, 452)
label(272, 535, ['Headset', 'Tracking'])
text(272, 578, ['(Head Position,', 'Gaze,', 'Controller Ray)'], 14, color='#000000')
route([(315, 450), (333, 450)])
s.append(f'<rect x="335" y="345" width="185" height="215" rx="8" fill="white" stroke="{INK}" stroke-width="1.8"/>')
text(428, 372, ['Guidance State'], 17, True, color='#000000')
for i, item in enumerate(['Gaze valid and recent', 'Angle to target, trend', 'Direction to target', 'Recent gaze direction', 'Last cue and response', 'Past cues and trials', 'Allowed strategies']):
    s.append(f'<circle cx="349" cy="{395 + i * 22:.1f}" r="2.8" fill="#000000"/>')
    text(358, 400 + i * 22, item, 14, anchor='start', color='#000000')
label(428, 585, ['Summarize, Then Ask'])
text(570, 425, ['Wireless', 'ADB link'], 13.5, color='#000000', italic=True)
s.append(f'<rect x="628" y="334" width="164" height="290" rx="8" fill="white" stroke="{INK}" stroke-width="1.8"/>')
text(710, 357, ['Decision LLM', 'Pipeline'], 16, True, color='#000000')
# Inner pipeline: the strategy is chosen first, then a phrase within it.
for top, height in ((390, 126), (538, 72)):
    s.append(f'<rect x="638" y="{top}" width="144" height="{height}" rx="6" fill="#f7f8f9" stroke="{LINE}" stroke-width="1.3"/>')
text(710, 410, '1. Strategy', 14, True)
for i, item in enumerate(['Stay silent', 'Orient', 'Refine', 'Correct', 'Encourage', 'Interrupt']):
    s.append(f'<circle cx="668" cy="{424 + i * 16:.1f}" r="2.3" fill="#000000"/>')
    text(676, 428 + i * 16, item, 13, anchor='start')
route([(710, 516), (710, 536)])
text(710, 558, '2. Phrase', 14, True)
text(710, 577, ['Choose the most', 'natural phrase'], 13)
label(563, 688, ['Gaze-Contingent Guidance Agent'])
text(563, 710, 'Local LLM', 14, True, color='#000000')
route([(520, 450), (626, 450)])
route([(792, 450), (862, 450)])
text(835, 418, ['Agent', 'Guidance'], 13, color='#000000', italic=True)
scaled(1.15, 893, 450, speaker)
ear(950, 450)
label(925, 505, ['Spoken Cue'])
# Both voice conditions feed the spoken cue.
s.append(f'<path d="M725,202 V250 H925 M890,202 V250" fill="none" stroke="{LINE}" stroke-width="2"/>')
route([(925, 250), (925, 420)])
route([(710, 624), (710, 648), (428, 648), (428, 600)], color='#000000', marker='k')
text(560, 640, 'Result sent back to memory', 14, color='#000000', italic=True)
emoji('\U0001F9E0', 652, 623, 22)

# Row 3: review and export
route([(272, 620), (272, 840), (422, 840)])
text(363, 828, 'Transfer', 13.5, color='#000000', italic=True)
image(EXPERIMENT / 'trial-viewer-shots/real-dev-20261103/01-first-person-guidance.png', (0, 70, 1440, 890), 425, 772, 240, 137)
label(545, 933, ['Trial Review'])
route([(665, 840), (795, 840)])
text(730, 828, 'Export', 13.5, color='#000000', italic=True)
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
