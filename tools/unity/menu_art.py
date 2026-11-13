"""Generate the Forest Café menu pictures (256×256 PNG) used by the Unity slice.

Original flat illustrations in the project palette, drawn at 4× and downsampled for smooth edges.
Run: python3 tools/unity/menu_art.py  (needs Pillow)
"""
from pathlib import Path
from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parents[2] / "games/unity/Assets/Resources/SliceMenu"
S = 4
SIZE = 256 * S
INK = (41, 66, 56)
CREAM = (250, 244, 232)


def canvas(bg):
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.ellipse(box(18, 18, 238, 238), fill=bg)
    return image, draw


def box(x0, y0, x1, y1):
    return [x0 * S, y0 * S, x1 * S, y1 * S]


def pts(*coords):
    return [(x * S, y * S) for x, y in coords]


def line_width(width):
    return int(width * S)


def saucer(draw):
    draw.ellipse(box(52, 176, 204, 206), fill=(255, 255, 255), outline=INK, width=line_width(4))


def cup(draw, liquid, foam=None, art=None, dots=False, handle=True):
    saucer(draw)
    draw.polygon(pts((70, 104), (186, 104), (174, 186), (82, 186)), fill=(255, 255, 255), outline=INK)
    draw.line(pts((70, 104), (82, 186), (174, 186), (186, 104)), fill=INK, width=line_width(5), joint="curve")
    if handle:
        draw.arc(box(168, 116, 214, 162), 270, 90, fill=INK, width=line_width(7))
    draw.ellipse(box(70, 92, 186, 116), fill=foam or liquid, outline=INK, width=line_width(5))
    if art == "heart":
        draw.ellipse(box(110, 98, 128, 110), fill=liquid)
        draw.ellipse(box(126, 98, 144, 110), fill=liquid)
        draw.polygon(pts((111, 105), (143, 105), (127, 114)), fill=liquid)
    if dots:
        for x, y in ((104, 102), (124, 100), (144, 104), (132, 108), (114, 108)):
            draw.ellipse(box(x, y, x + 6, y + 4), fill=(120, 74, 46))
    if art == "cream":
        for x, y in ((96, 82), (120, 72), (146, 82), (122, 88)):
            draw.ellipse(box(x, y, x + 34, y + 26), fill=(255, 250, 240), outline=INK, width=line_width(3))
    for x in (110, 130, 150):  # steam
        draw.arc(box(x - 10, 50, x + 6, 80), 200, 340, fill=(160, 170, 165), width=line_width(4))


def teacup(draw, liquid, flower=False):
    saucer(draw)
    draw.chord(box(62, 80, 194, 196), 0, 180, fill=(255, 255, 255), outline=INK, width=line_width(5))
    draw.ellipse(box(62, 124, 194, 150), fill=liquid, outline=INK, width=line_width(5))
    draw.arc(box(176, 130, 214, 168), 270, 90, fill=INK, width=line_width(7))
    draw.line(pts((90, 132), (78, 100), (70, 96)), fill=(200, 160, 90), width=line_width(3))
    draw.rectangle(box(62, 86, 78, 100), fill=(240, 210, 140), outline=INK, width=line_width(3))
    if flower:
        for dx, dy in ((0, -10), (10, 0), (0, 10), (-10, 0)):
            draw.ellipse(box(140 + dx, 130 + dy, 152 + dx, 142 + dy), fill=(255, 255, 255), outline=INK, width=line_width(2))
        draw.ellipse(box(142, 132, 150, 140), fill=(242, 193, 78))


def glass(draw, liquid, garnish=None, ice=True, straw=(217, 121, 28)):
    draw.polygon(pts((80, 70), (176, 70), (166, 200), (90, 200)), fill=(240, 248, 248))
    draw.polygon(pts((84, 100), (172, 100), (166, 196), (90, 196)), fill=liquid)
    if ice:
        for x, y in ((100, 110), (130, 122), (112, 146)):
            draw.rounded_rectangle(box(x, y, x + 24, y + 22), radius=4 * S, fill=(255, 255, 255, 170), outline=(220, 235, 240), width=line_width(2))
    draw.line(pts((80, 70), (90, 200), (166, 200), (176, 70)), fill=INK, width=line_width(5), joint="curve")
    draw.line(pts((150, 40), (136, 150)), fill=straw, width=line_width(8))
    if garnish == "orange":
        draw.pieslice(box(150, 52, 206, 108), 180, 360, fill=(240, 150, 40), outline=INK, width=line_width(4))
    if garnish == "lemon":
        draw.pieslice(box(150, 52, 206, 108), 180, 360, fill=(246, 222, 90), outline=INK, width=line_width(4))
        draw.ellipse(box(66, 54, 88, 74), fill=(98, 160, 96))
    if garnish == "berries":
        for x, y, c in ((92, 56, (88, 70, 160)), (110, 50, (190, 50, 80)), (124, 58, (88, 70, 160))):
            draw.ellipse(box(x, y, x + 20, y + 20), fill=c, outline=INK, width=line_width(3))
    if garnish == "cucumber":
        draw.ellipse(box(150, 60, 196, 100), fill=(190, 222, 150), outline=INK, width=line_width(4))
        draw.ellipse(box(162, 70, 184, 90), fill=(220, 240, 190))
    if garnish == "apple":
        draw.ellipse(box(150, 58, 196, 100), fill=(220, 70, 60), outline=INK, width=line_width(4))


def dripper(draw):
    draw.polygon(pts((90, 196), (166, 196), (172, 140), (84, 140)), fill=(240, 248, 248), outline=INK)
    draw.polygon(pts((90, 192), (166, 192), (168, 160), (88, 160)), fill=(110, 60, 40))
    draw.polygon(pts((76, 70), (180, 70), (146, 132), (110, 132)), fill=(222, 196, 160), outline=INK)
    draw.line(pts((76, 70), (110, 132), (146, 132), (180, 70)), fill=INK, width=line_width(5))
    draw.rectangle(box(70, 132, 186, 142), fill=(110, 80, 60))
    for x, y in ((120, 50), (136, 44)):
        draw.ellipse(box(x, y, x + 14, y + 14), fill=(150, 60, 120), outline=INK, width=line_width(3))


def plate(draw):
    draw.ellipse(box(36, 120, 220, 208), fill=(255, 255, 255), outline=INK, width=line_width(5))
    draw.ellipse(box(60, 134, 196, 194), outline=(220, 220, 214), width=line_width(3))


def bowl(draw, fill):
    draw.ellipse(box(46, 104, 210, 140), fill=fill, outline=INK, width=line_width(5))
    draw.chord(box(46, 60, 210, 210), 0, 180, fill=(255, 255, 255), outline=INK, width=line_width(5))


def drawers():
    brown, light, cocoa = (92, 56, 36), (194, 146, 102), (150, 100, 70)
    return {
        "americano": lambda d: cup(d, brown),
        "latte": lambda d: cup(d, light, foam=(244, 226, 200), art="heart"),
        "cappuccino": lambda d: cup(d, light, foam=(250, 240, 222), dots=True),
        "flat-white": lambda d: cup(d, (204, 160, 116), foam=(236, 214, 182), art="heart"),
        "forest-mocha": lambda d: cup(d, cocoa, foam=(120, 80, 58), art="cream"),
        "seasonal-blend": dripper,
        "english-breakfast": lambda d: teacup(d, (186, 110, 48)),
        "matcha": lambda d: cup(d, (140, 180, 100), foam=(180, 210, 140), art="heart"),
        "chamomile": lambda d: teacup(d, (238, 206, 110), flower=True),
        "berry-smoothie": lambda d: glass(d, (150, 70, 140), "berries", ice=False),
        "green-smoothie": lambda d: glass(d, (120, 170, 80), "cucumber", ice=False),
        "orange-juice": lambda d: glass(d, (246, 168, 50), "orange"),
        "apple-ginger": lambda d: glass(d, (226, 196, 100), "apple"),
        "lemonade": lambda d: glass(d, (250, 236, 160), "lemon"),
        "forest-spritz": lambda d: glass(d, (200, 230, 190), "cucumber", straw=(23, 105, 79)),
        "avocado-toast": avocado_toast,
        "pancakes": pancakes,
        "forest-bowl": forest_bowl,
        "chicken-sandwich": sandwich,
        "salmon": salmon,
        "curry": curry,
        "cheesecake": cheesecake,
        "brownie": brownie,
        "croissant": croissant,
        "banana-bread": banana_bread,
    }


def avocado_toast(d):
    plate(d)
    d.rounded_rectangle(box(66, 110, 190, 178), radius=18 * S, fill=(214, 170, 110), outline=INK, width=line_width(5))
    d.rounded_rectangle(box(78, 118, 178, 168), radius=14 * S, fill=(150, 190, 90))
    d.ellipse(box(110, 120, 150, 158), fill=(255, 255, 255), outline=INK, width=line_width(3))
    d.ellipse(box(122, 130, 138, 146), fill=(246, 190, 60))
    for x, y in ((90, 126), (164, 150), (96, 156)):
        d.ellipse(box(x, y, x + 6, y + 6), fill=(200, 60, 40))


def pancakes(d):
    plate(d)
    for i in range(3):
        y = 150 - i * 18
        d.ellipse(box(66, y, 190, y + 34), fill=(222, 170, 100), outline=INK, width=line_width(4))
    d.ellipse(box(76, 100, 180, 128), fill=(196, 120, 40))
    for x, y, c in ((92, 92, (88, 70, 160)), (120, 86, (190, 50, 80)), (148, 92, (88, 70, 160))):
        d.ellipse(box(x, y, x + 18, y + 18), fill=c, outline=INK, width=line_width(3))


def forest_bowl(d):
    bowl(d, (120, 160, 90))
    for x, y in ((80, 102), (120, 96), (156, 104)):
        d.pieslice(box(x, y, x + 32, y + 28), 180, 360, fill=(160, 120, 90), outline=INK, width=line_width(3))
        d.rectangle(box(x + 12, y + 12, x + 20, y + 24), fill=(230, 220, 200))
    for x in (100, 140):
        d.ellipse(box(x, 116, x + 16, 126), fill=(240, 230, 200))


def sandwich(d):
    plate(d)
    d.polygon(pts((60, 160), (196, 160), (128, 92)), fill=(222, 182, 120), outline=INK)
    d.line(pts((60, 160), (128, 92), (196, 160), (60, 160)), fill=INK, width=line_width(5))
    d.line(pts((70, 150), (186, 150)), fill=(110, 170, 80), width=line_width(8))
    d.line(pts((80, 140), (176, 140)), fill=(210, 70, 60), width=line_width(6))


def salmon(d):
    plate(d)
    d.rounded_rectangle(box(70, 128, 160, 168), radius=16 * S, fill=(240, 140, 110), outline=INK, width=line_width(4))
    for x in (84, 104, 124, 144):
        d.line(pts((x, 132), (x + 6, 164)), fill=(255, 210, 190), width=line_width(3))
    for x, y in ((164, 130), (176, 150), (154, 156)):
        d.ellipse(box(x, y, x + 22, y + 18), fill=(236, 206, 130), outline=INK, width=line_width(3))
    d.line(pts((80, 178), (180, 178)), fill=(98, 160, 96), width=line_width(6))


def curry(d):
    bowl(d, (226, 140, 50))
    d.ellipse(box(76, 100, 132, 124), fill=(255, 255, 248), outline=INK, width=line_width(3))
    for x, y in ((150, 106), (166, 114)):
        d.ellipse(box(x, y, x + 10, y + 6), fill=(98, 160, 96))


def cheesecake(d):
    plate(d)
    d.polygon(pts((70, 168), (190, 168), (190, 128), (100, 102)), fill=(250, 236, 200), outline=INK)
    d.line(pts((70, 168), (100, 102), (190, 128), (190, 168), (70, 168)), fill=INK, width=line_width(5))
    d.rectangle(box(70, 160, 190, 170), fill=(196, 140, 80))
    d.arc(box(110, 112, 170, 150), 200, 340, fill=(120, 80, 170), width=line_width(7))


def brownie(d):
    plate(d)
    d.rounded_rectangle(box(70, 120, 160, 176), radius=8 * S, fill=(90, 54, 36), outline=INK, width=line_width(4))
    d.ellipse(box(132, 92, 188, 140), fill=(255, 248, 230), outline=INK, width=line_width(4))
    d.line(pts((80, 130), (150, 130)), fill=(120, 80, 60), width=line_width(3))


def croissant(d):
    plate(d)
    for i, (x0, y0, x1, y1) in enumerate(((60, 120, 112, 170), (92, 104, 164, 170), (144, 120, 196, 170))):
        d.ellipse(box(x0, y0, x1, y1), fill=(222, 160, 80) if i != 1 else (232, 176, 96), outline=INK, width=line_width(4))
    for x in (110, 128, 146):
        d.arc(box(x - 12, 112, x + 12, 168), 250, 290, fill=(170, 110, 50), width=line_width(4))


def banana_bread(d):
    plate(d)
    d.rounded_rectangle(box(66, 112, 186, 172), radius=12 * S, fill=(196, 140, 80), outline=INK, width=line_width(4))
    d.rounded_rectangle(box(74, 120, 178, 164), radius=8 * S, fill=(232, 200, 140))
    for x, y in ((92, 132), (130, 146), (150, 128)):
        d.ellipse(box(x, y, x + 10, y + 8), fill=(120, 80, 50))


BACKGROUNDS = {
    "coffee": (243, 232, 220), "tea": (236, 240, 220), "drink": (253, 240, 225), "food": (229, 242, 236), "dessert": (246, 233, 239),
}
CATEGORY = {
    "americano": "coffee", "latte": "coffee", "cappuccino": "coffee", "flat-white": "coffee", "forest-mocha": "coffee", "seasonal-blend": "coffee",
    "english-breakfast": "tea", "matcha": "tea", "chamomile": "tea",
    "berry-smoothie": "drink", "green-smoothie": "drink", "orange-juice": "drink", "apple-ginger": "drink", "lemonade": "drink", "forest-spritz": "drink",
    "cheesecake": "dessert", "brownie": "dessert", "croissant": "dessert", "banana-bread": "dessert",
}


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for item, draw_item in drawers().items():
        image, draw = canvas(BACKGROUNDS[CATEGORY.get(item, "food")])
        draw_item(draw)
        image.resize((256, 256), Image.LANCZOS).save(OUT / f"{item}.png", optimize=True)
    print(f"Wrote {len(drawers())} menu pictures to {OUT}")


if __name__ == "__main__":
    main()
