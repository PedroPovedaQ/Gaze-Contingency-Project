#!/usr/bin/env python3
"""Author the initial 300-entry seated navigation catalog. Does not synthesize/bill audio.
JSON is an exchange format; import with Unity's Tools > Codex > Import Navigation Catalog.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BOUNDS = [-180, -150, -120, -90, -60, -35, -15, -5, 5, 15, 35, 60, 90, 120, 150, 180]
DISTANCES = [(0, 1), (1, 2), (2, 4), (4, 8), (8, 10000)]
LEFT = ["Turn left toward the back", "Turn well to your left", "Turn farther left", "Turn left",
        "Look left", "Look a little left", "Look just left of center"]
CUES = LEFT + ["Keep facing this direction"] + [v.replace('left', 'right') for v in LEFT[::-1]]
TONES = ['direct', 'technical', 'casual', 'encouraging']


def catalog():
    entries = []
    for angle_bin, cue in enumerate(CUES):
        lo, hi = BOUNDS[angle_bin:angle_bin + 2]
        midpoint = abs((lo + hi) / 2)
        bearing = "left" if hi <= 0 else "right"
        technical = (f"Turn {bearing} approximately {midpoint:g} degrees" if midpoint else "Maintain your horizontal heading")
        texts = {
            'direct': [f"{cue}.", f"{cue}, please.", f"{cue} now.", f"{cue} from here.", f"For now, {cue.lower()}."],
            'technical': [f"{technical}.", f"{technical} from this heading.", f"From here, {technical.lower()}.", f"Please {technical.lower()}.", f"Next, {technical.lower()}."],
            'casual': [f"Okay, {cue.lower()}.", f"Now, {cue.lower()}.", f"Let's {cue.lower()}.", f"Try this: {cue.lower()}.", f"All right, {cue.lower()}."],
            'encouraging': [f"Take your time; {cue.lower()}.", f"Gently, {cue.lower()}.", f"At your own pace, {cue.lower()}.", f"No rush; {cue.lower()}.", f"When you're ready, {cue.lower()}."]
        }
        for distance_bin, (minimum, maximum) in enumerate(DISTANCES):
            for tone in TONES:
                entries.append(dict(id=f"nav_a{angle_bin:02d}_d{distance_bin}_{tone}", text=texts[tone][distance_bin],
                    constraints=dict(min_angle=lo, max_angle=hi, min_distance_meters=minimum, max_distance_meters=maximum),
                    metadata=dict(tone=tone, urgency='low')))
    assert len(entries) == len({p['id'] for p in entries}) == len({p['text'] for p in entries}) == 300
    return dict(version='signed-navigation-v1', navigation_phrases=entries)


if __name__ == '__main__':
    path = ROOT / 'Assets/Resources/SpatialVoice/navigation-phrases.json'
    path.write_text(json.dumps(catalog(), indent=2) + '\n')
    print(f'Wrote 300 phrases to {path}')
