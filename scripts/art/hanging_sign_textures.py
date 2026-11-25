"""Generate original sign lettering; Pillow is needed only for this art authoring step."""
from pathlib import Path
import argparse
import os
import random
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('--font', default=str(Path(os.environ.get('SystemRoot', 'C:/Windows')) / 'Fonts/msyh.ttc'))
args = parser.parse_args()
target = root / 'Assets/Art/Textures/Signs'
target.mkdir(parents=True, exist_ok=True)
for kind, label in [('LAN', '本地局域网连接'), ('Relay', '远程房间号连接')]:
    canvas = Image.new('RGB', (1024, 512), (222, 213, 184))
    draw = ImageDraw.Draw(canvas)
    rng = random.Random(26)
    for _ in range(350):
        y = rng.randrange(512); x = rng.randrange(1024)
        draw.line((x, y, min(1023, x + rng.randrange(15, 170)), y + rng.choice([-1, 0, 1])), fill=(210, 202, 174), width=1)
    draw.rounded_rectangle((25, 25, 998, 486), radius=22, outline=(52, 82, 72), width=12)
    font = ImageFont.truetype(args.font, 98)
    draw.text((512, 244), label, font=font, fill=(34, 62, 55), anchor='mm', stroke_width=1)
    draw.text((512, 381), 'LAN' if kind == 'LAN' else 'RELAY', font=ImageFont.truetype(args.font, 46), fill=(70, 96, 81), anchor='mm')
    canvas.save(target / ('Signs-Connection-' + kind + '.png'))
print('Generated two original hanging-sign textures.')
