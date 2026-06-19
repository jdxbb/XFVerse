from pathlib import Path
import sys
import xml.etree.ElementTree as ET

from PIL import Image, ImageDraw


def main() -> int:
    if len(sys.argv) != 4:
        print(
            "usage: generate_app_icon.py <svg-input> <ico-output> <png-output>",
            file=sys.stderr,
        )
        return 1

    svg_path = Path(sys.argv[1])
    ico_path = Path(sys.argv[2])
    png_path = Path(sys.argv[3])
    ico_path.parent.mkdir(parents=True, exist_ok=True)
    png_path.parent.mkdir(parents=True, exist_ok=True)

    base = render_logo(svg_path, 1024)
    base.save(png_path)
    sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
    base.save(ico_path, sizes=sizes)
    return 0


def render_logo(svg_path: Path, size: int) -> Image.Image:
    root = ET.parse(svg_path).getroot()
    view_box = _parse_numbers(root.attrib["viewBox"])
    if len(view_box) != 4 or view_box[2] <= 0 or view_box[3] <= 0:
        raise ValueError("SVG viewBox must contain four positive dimensions.")

    supersampling = 4
    render_size = size * supersampling
    scale_x = render_size / view_box[2]
    scale_y = render_size / view_box[3]
    if abs(scale_x - scale_y) > 0.000001:
        raise ValueError("Only square, uniformly scaled app-logo SVG files are supported.")

    scale = scale_x
    offset_x = -view_box[0] * scale
    offset_y = -view_box[1] * scale
    image = Image.new("RGBA", (render_size, render_size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    for element in root.iter():
        tag = _local_name(element.tag)
        if tag == "rect":
            x = _number(element, "x", 0) * scale + offset_x
            y = _number(element, "y", 0) * scale + offset_y
            width = _number(element, "width") * scale
            height = _number(element, "height") * scale
            radius = _number(element, "rx", 0) * scale
            draw.rounded_rectangle(
                [round(x), round(y), round(x + width), round(y + height)],
                radius=round(radius),
                fill=_parse_color(_inherited_attribute(element, root, "fill")),
            )
        elif tag == "line":
            stroke_width = max(
                1,
                round(float(_inherited_attribute(element, root, "stroke-width")) * scale),
            )
            start = (
                round(_number(element, "x1") * scale + offset_x),
                round(_number(element, "y1") * scale + offset_y),
            )
            end = (
                round(_number(element, "x2") * scale + offset_x),
                round(_number(element, "y2") * scale + offset_y),
            )
            color = _parse_color(_inherited_attribute(element, root, "stroke"))
            line_cap = _inherited_attribute(element, root, "stroke-linecap", "butt")
            draw.line([start, end], fill=color, width=stroke_width)
            if line_cap == "round":
                radius = stroke_width / 2
                for point in (start, end):
                    draw.ellipse(
                        [
                            round(point[0] - radius),
                            round(point[1] - radius),
                            round(point[0] + radius),
                            round(point[1] + radius),
                        ],
                        fill=color,
                    )
            elif line_cap != "butt":
                raise ValueError(f"Unsupported stroke-linecap: {line_cap}")
        elif tag not in {"svg", "g"}:
            raise ValueError(f"Unsupported SVG element: {tag}")

    return image.resize((size, size), Image.Resampling.LANCZOS)


def _local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def _parse_numbers(value: str) -> list[float]:
    return [float(part) for part in value.replace(",", " ").split()]


def _number(element: ET.Element, name: str, default: float | None = None) -> float:
    value = element.attrib.get(name)
    if value is None:
        if default is None:
            raise ValueError(f"SVG element is missing required attribute: {name}")
        return default
    return float(value)


def _inherited_attribute(
    element: ET.Element,
    root: ET.Element,
    name: str,
    default: str | None = None,
) -> str:
    value = element.attrib.get(name)
    if value is not None:
        return value

    parent_by_child = {child: parent for parent in root.iter() for child in parent}
    parent = parent_by_child.get(element)
    while parent is not None:
        value = parent.attrib.get(name)
        if value is not None:
            return value
        parent = parent_by_child.get(parent)

    if default is not None:
        return default
    raise ValueError(f"SVG element is missing required inherited attribute: {name}")


def _parse_color(value: str) -> tuple[int, int, int, int]:
    if value == "none":
        return (0, 0, 0, 0)
    if len(value) == 7 and value.startswith("#"):
        return (
            int(value[1:3], 16),
            int(value[3:5], 16),
            int(value[5:7], 16),
            255,
        )
    raise ValueError(f"Unsupported SVG color: {value}")


if __name__ == "__main__":
    raise SystemExit(main())
