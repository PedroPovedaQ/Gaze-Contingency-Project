"""Summarize synthetic Unity pixels; no calibrated perceptual acceptance threshold."""
from pathlib import Path
import json
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

folder = Path(sys.argv[1])
shapes = ['Sphere', 'Cube', 'Pyramid', 'Cylinder', 'Star', 'Capsule']
examples = []
for path in sorted(folder.glob('contrast-*.png')):
    pixels = np.asarray(Image.open(path).convert('RGB')).astype(float)
    assert pixels.shape == (1440, 2560, 3), path
    mask = np.ptp(pixels, axis=2) > 45  # Excludes gray and the low-saturation disk.
    labels, _ = ndimage.label(mask)
    components = []
    for label, bounds in enumerate(ndimage.find_objects(labels), 1):
        if bounds is None:
            continue
        region = labels[bounds] == label
        if region.sum() < 100:
            continue
        y, x = bounds
        components.append((y.start, x.start, label, int(region.sum())))
    floor = path.name.startswith('contrast-floor-')
    assert len(components) == (6 if floor else 12), (path, components)
    rows = [components] if floor else [[c for c in components if c[0] < 720], [c for c in components if c[0] >= 720]]
    for row_index, row in enumerate(rows):
        assert len(row) == 6
        for shape, (_, _, label, count) in zip(shapes[::-1] if floor else shapes, sorted(row, key=lambda c: c[1])):
            component = labels == label
            surround = ndimage.binary_dilation(component, iterations=6) & ~mask
            rgb, counts = np.unique(pixels[surround], axis=0, return_counts=True)
            background = rgb[counts.argmax()]
            median = np.median(pixels[component], axis=0)
            examples.append(dict(image=path.name, shape=shape, view='exterior-floor' if floor else ['seated-top','seated-bottom'][row_index],
                colored_pixels=count, median_rgb=median.tolist(), adjacent_background_rgb=background.tolist(),
                median_rgb_distance=round(float(np.linalg.norm(median-background)), 3)))

def luminance(rgb):
    v = np.asarray(rgb)
    linear = np.where(v <= .04045, v / 12.92, ((v + .055) / 1.055) ** 2.4)
    return float(linear @ [.2126, .7152, .0722])

circle = luminance([.35,.33,.23])
gray = luminance([.32,.32,.32])
report = dict(status='Development digital-image diagnostic only; no photometric or psychophysical validation.',
    circle_srgb=[.35,.33,.23], gray_srgb=[.32,.32,.32], object_yellow_srgb=[.95,.85,.1],
    circle_nominal_relative_luminance=circle, gray_nominal_relative_luminance=gray,
    circle_relative_to_gray_percent=100*(circle/gray-1),
    examples=examples, count=len(examples), minimum_median_rgb_distance=min(e['median_rgb_distance'] for e in examples))
(folder/'contrast.json').write_text(json.dumps(report, indent=2)+'\n')
print(json.dumps({k:v for k,v in report.items() if k != 'examples'}, indent=2))
