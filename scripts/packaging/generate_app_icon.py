from pathlib import Path
import sys

from PIL import Image, ImageDraw


def main() -> int:
    if len(sys.argv) != 3:
        print("usage: generate_app_icon.py <ico-output> <png-output>", file=sys.stderr)
        return 1

    ico_path = Path(sys.argv[1])
    png_path = Path(sys.argv[2])
    ico_path.parent.mkdir(parents=True, exist_ok=True)
    png_path.parent.mkdir(parents=True, exist_ok=True)

    base = render_logo(1024)
    base.save(png_path)
    sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
    base.save(ico_path, sizes=sizes)
    return 0


def render_logo(size: int) -> Image.Image:
    scale = size / 1024
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    rect = [round(88 * scale), round(88 * scale), round(936 * scale), round(936 * scale)]
    radius = round(196 * scale)
    stroke_width = max(1, round(84 * scale))
    draw.rounded_rectangle(rect, radius=radius, fill=(11, 11, 13, 255))

    stroke = (247, 247, 245, 255)
    lines = [
        ((258, 350), (605, 732)),
        ((605, 292), (258, 682)),
        ((612, 292), (612, 732)),
        ((612, 292), (804, 292)),
        ((612, 512), (804, 512)),
    ]

    for start, end in lines:
        draw.line(
            [tuple(round(v * scale) for v in start), tuple(round(v * scale) for v in end)],
            fill=stroke,
            width=stroke_width,
            joint="curve",
        )

    return image


if __name__ == "__main__":
    raise SystemExit(main())
