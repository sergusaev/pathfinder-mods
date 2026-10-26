# The mod's pictures, from the scroll of the games' Journal card with its quill taken away (scroll_km.png,
# scroll_wotr.png, scroll_wotr_small.png; cut out of the games' sprites once):
#   km_learn.png / km_window.png               Kingmaker's buttons and small card sign: white silhouettes, the window's
#                                              with a gear
#   wotr_card_big.png                          WotR's card drawing: the picture where the game's sprite has it in its
#                                              whole rect of 378x537 (the sprite is packed without its empty margins)
#   wotr_card_small.png                        WotR's corner sign: the small sign in the card's frame colour
#   mark_{like,dislike}_{km,wotr}.png          the games' level up thumbs (like_*, dislike_*) in a disc in the style of a pressed button
#   wotr_{learn,window}_{state}.png            WotR's buttons: the silhouette in grey on the buff bar's square
# Usage: python make_art.py (writes next to itself). Needs Pillow.
import math, os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
K = 4  # supersampling


def gear(d, cx, cy, r, teeth, fill, k=K, grow=0.0):
    # grow: the same outline pushed out by this much, for the gap that follows the teeth.
    pts = []
    n = teeth * 4
    for i in range(n):
        a = 2 * math.pi * i / n
        rr = (r if i % 4 in (1, 2) else r * 0.78) + grow
        pts.append(((cx + rr * math.cos(a)) * k, (cy + rr * math.sin(a)) * k))
    d.polygon(pts, fill=fill)


def silhouette(src, ink, light, with_gear):
    # The paper solid, the ink lines and shadows let through; a gear over the lower right with a gap round it.
    pic = Image.open(os.path.join(HERE, src)).convert('RGBA')
    w, h = pic.size
    p = pic.load()
    m = Image.new('L', (w, h))
    q = m.load()
    for y in range(h):
        for x in range(w):
            c = p[x, y]
            lum = c[0] * 0.3 + c[1] * 0.59 + c[2] * 0.11
            q[x, y] = int(c[3] * max(0.0, min(1.0, (lum - ink) / (light - ink))))
    if with_gear:
        big = m.resize((w * K, h * K), Image.LANCZOS)
        d = ImageDraw.Draw(big)
        cx, cy, r = 0.68 * w, 0.5 * h, 0.176 * w
        # The gap follows the gear's outline, so the sheet round it is not bitten.
        gear(d, cx, cy, r, 8, 0, grow=0.12 * r)
        gear(d, cx, cy, r, 8, 255)
        d.ellipse([(cx - r * 0.34) * K, (cy - r * 0.34) * K, (cx + r * 0.34) * K, (cy + r * 0.34) * K], fill=0)
        m = big.resize((w, h), Image.LANCZOS)
    out = Image.new('RGBA', (w, h), (255, 255, 255, 0))
    out.putalpha(m)
    return out


def scroll_art():
    silhouette('scroll_km.png', 60, 190, False).save(os.path.join(HERE, 'km_learn.png'))
    silhouette('scroll_km.png', 60, 190, True).save(os.path.join(HERE, 'km_window.png'))
    ink, frame = (0x2B, 0x24, 0x1F), (0x5E, 0x7A, 0x4C)
    pic = Image.open(os.path.join(HERE, 'scroll_wotr_small.png')).convert('RGBA')
    p = pic.load()
    for y in range(pic.height):
        for x in range(pic.width):
            c = p[x, y]
            t = max(0.0, min(1.0, (c[0] * 0.3 + c[1] * 0.59 + c[2] * 0.11 - 50) / 90))
            p[x, y] = tuple(round(ink[i] + (frame[i] - ink[i]) * t) for i in range(3)) + (c[3],)
    pic.save(os.path.join(HERE, 'wotr_card_small.png'))
    # UI_CardJournalBig: rect 378x537, its pixels at (62, 51) from the bottom left.
    big = Image.open(os.path.join(HERE, 'scroll_wotr.png')).convert('RGBA')
    whole = Image.new('RGBA', (378, 537), (0, 0, 0, 0))
    whole.paste(big, (62, 537 - 51 - big.height))
    whole.save(os.path.join(HERE, 'wotr_card_big.png'))


def mark_on(back, kind, game):
    # The thumb in a disc in the style of a pressed button of the mod's bar: its inside stretched over the disc, its
    # frame turned into a ring, colour by colour from the outer edge in.
    S, B = 64, 4  # size, supersampling
    w, h = back.size
    p = back.load()
    edge = 12
    profile = [p[x, h // 2] for x in range(edge)]
    inside = back.crop((edge + 1, edge + 1, w - edge - 1, h - edge - 1)).resize((S * B, S * B), Image.LANCZOS)
    q = inside.load()
    big = Image.new('RGBA', (S * B, S * B), (0, 0, 0, 0))
    o = big.load()
    r = S * B / 2 - B
    ring = r * edge / (w / 2)
    c = S * B / 2
    for y in range(S * B):
        for x in range(S * B):
            d = math.hypot(x + 0.5 - c, y + 0.5 - c)
            if d > r:
                continue
            if d > r - ring:
                o[x, y] = profile[min(edge - 1, int((r - d) / ring * edge))][:3] + (255,)
            else:
                o[x, y] = q[x, y][:3] + (255,)
    m = big.resize((S, S), Image.LANCZOS)
    thumb = Image.open(os.path.join(HERE, f'{kind}_{game}.png')).convert('RGBA')
    side = round(S * 0.56)
    scale = side / max(thumb.size)
    thumb = thumb.resize((round(thumb.width * scale), round(thumb.height * scale)), Image.LANCZOS)
    m.alpha_composite(thumb, ((S - thumb.width) // 2, (S - thumb.height) // 2))
    m.save(os.path.join(HERE, f'mark_{kind}_{game}.png'))


def marks():
    # Kingmaker's pressed square: the median of the game's pressed menu buttons (km_button_down.png, made once).
    back = Image.open(os.path.join(HERE, 'km_button_down.png')).convert('RGBA')
    for kind in ['like', 'dislike']:
        mark_on(back, kind, 'km')


scroll_art()
marks()
print('ok')

FORK = os.path.join(HERE, '..', '..', 'wotr-pad-buffs', 'BuffIt2TheLimit', 'Assets', 'icons')


def wotr_buttons():
    from PIL import ImageFilter, ImageChops
    sets = ['apply_buffs', 'apply_buffs_short', 'apply_buffs_important', 'open_buffs', 'show_map']
    for state in ['normal', 'hover', 'down']:
        ims = [Image.open(os.path.join(FORK, f'{s}_{state}.png')).convert('RGBA') for s in sets]
        size = ims[0].size
        back = Image.new('RGBA', size)
        px = [im.load() for im in ims]
        out = back.load()
        lum = lambda c: c[0] * 0.3 + c[1] * 0.59 + c[2] * 0.11
        inner = sorted(lum(p[x, y]) for p in px for y in range(30, 66) for x in range(12, 18))
        plain = inner[len(inner) // 2]
        for y in range(size[1]):
            for x in range(size[0]):
                cs = [p[x, y] for p in px]
                frame = x < 10 or y < 10 or x >= size[0] - 10 or y >= size[1] - 10
                pick = cs if frame else [c for c in cs if plain * 0.7 < lum(c) < plain * 1.4] or cs
                pick.sort(key=lum)
                out[x, y] = pick[len(pick) // 2]
        # The traces of the pictograms left inside the frame are blurred away.
        inside = back.crop((12, 12, size[0] - 12, size[1] - 12)).filter(ImageFilter.GaussianBlur(5))
        back.paste(inside, (12, 12))
        if state == 'down':
            for kind in ['like', 'dislike']:
                mark_on(back, kind, 'wotr')
        for name, gear_on in [('learn', False), ('window', True)]:
            pic = silhouette('scroll_wotr_small.png', 50, 140, gear_on)
            h = 70
            w = round(pic.width * h / pic.height)
            pic = pic.resize((w, h), Image.LANCZOS)
            alpha = pic.getchannel('A')
            grey = Image.new('RGBA', pic.size, (196, 194, 192, 255))
            # Shaded from top to bottom as the game's pictograms.
            grad = Image.linear_gradient('L').resize(pic.size).point(lambda v: 255 - v // 4)
            grey = Image.merge('RGBA', [ImageChops.multiply(c, grad) for c in grey.split()[:3]] + [alpha])
            shadow = Image.new('RGBA', pic.size, (0, 0, 0, 0))
            shadow.putalpha(alpha.filter(ImageFilter.GaussianBlur(2)).point(lambda v: v * 3 // 4))
            img = back.copy()
            ox, oy = (size[0] - w) // 2, (size[1] - h) // 2
            img.alpha_composite(shadow, (ox + 2, oy + 2))
            img.alpha_composite(grey, (ox, oy))
            img.save(os.path.join(HERE, f'wotr_{name}_{state}.png'))


if os.path.isdir(FORK):
    wotr_buttons()
    print('wotr ok')
