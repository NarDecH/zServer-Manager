# -*- coding: utf-8 -*-
"""สร้างโลโก้ zServer Manager: icon.ico ครบขนาด + PNG สำหรับ README"""
from PIL import Image, ImageDraw, ImageFilter, ImageFont
import os

ROOT = r"C:\zServer"
ICO_OUT = os.path.join(ROOT, r"src\ZServer.Manager\assets\zserver.ico")
LOGO_OUT = os.path.join(ROOT, r"docs\logo.png")
BANNER_OUT = os.path.join(ROOT, r"docs\banner.png")

S = 1024          # ขนาดงานหลัก
SS = 4            # supersample (วาด 4 เท่า แล้วย่อ ขอบเรียบ)
W = S * SS

BG_TOP = (13, 20, 38)
BG_BOT = (27, 42, 74)
BORDER = (66, 92, 148)
Z_TOP = (56, 189, 248)     # cyan
Z_BOT = (52, 211, 153)     # green
GLOW = (34, 211, 238)
LED_COLORS = [(52, 211, 153), (56, 189, 248), (167, 139, 250)]

def rounded_tile(size, radius_ratio=0.23):
    """พื้นหลังจัตุรัสมุมมน + gradient แนวตั้ง + ขอบสว่าง"""
    r = int(size * radius_ratio)
    tile = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    # gradient แนวตั้ง
    grad = Image.new("RGBA", (1, size))
    for y in range(size):
        t = y / max(1, size - 1)
        c = tuple(int(BG_TOP[i] + (BG_BOT[i] - BG_TOP[i]) * t) for i in range(3)) + (255,)
        grad.putpixel((0, y), c)
    grad = grad.resize((size, size))
    mask = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(mask)
    d.rounded_rectangle([0, 0, size - 1, size - 1], radius=r, fill=255)
    tile.paste(grad, (0, 0), mask)
    # ขอบ
    d = ImageDraw.Draw(tile)
    d.rounded_rectangle([1, 1, size - 2, size - 2], radius=r - 1, outline=BORDER + (230,), width=max(2, size // 420))
    # highlight ด้านบนเบา ๆ
    hl = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    dh = ImageDraw.Draw(hl)
    dh.rounded_rectangle([0, 0, size - 1, int(size * 0.5)], radius=r, fill=(120, 170, 255, 26))
    hl = hl.filter(ImageFilter.GaussianBlur(size * 0.03))
    tile.alpha_composite(hl)
    return tile

def z_polygon(size):
    """polygon ตัว Z เชิงเรขาคณิต (กระฉับ) อยู่กลาง canvas size"""
    u = size / 1024.0
    x0, x1 = int(258 * u), int(766 * u)
    y0, y1 = int(238 * u), int(790 * u)
    bar = int(104 * u)
    diag = int(196 * u)
    return [
        (x0, y0), (x1, y0), (x1, y0 + bar),
        (x1 - diag, y1 - bar), (x1, y1 - bar), (x1, y1),
        (x0, y1), (x0, y1 - bar), (x0 + diag, y0 + bar), (x0, y0 + bar),
    ]

def z_lightning_polygon(size):
    """Z แบบมัดย่อ — เส้นทแยงเป็นสายฟ้า"""
    u = size / 1024.0
    x0, x1 = int(258 * u), int(766 * u)
    y0, y1 = int(238 * u), int(790 * u)
    bar = int(104 * u)
    return [
        (x0, y0), (x1, y0), (x1, y0 + bar),
        # สายฟ้า: ลงซ้าย → ตัดขวาบน → ลงซ้าย
        (int(520 * u), int(470 * u)), (int(648 * u), int(500 * u)),
        (int(424 * u), y1 - bar), (x1, y1 - bar), (x1, y1),
        (x0, y1), (x0, y1 - bar),
        (int(584 * u), int(560 * u)), (int(452 * u), int(528 * u)),
        (int(672 * u), y0 + bar), (x0, y0 + bar),
    ]

def gradient_fill(img_size, poly, top, bottom):
    g = Image.new("RGBA", img_size, (0, 0, 0, 0))
    grad = Image.new("RGBA", (1, img_size[1]))
    for y in range(img_size[1]):
        t = y / max(1, img_size[1] - 1)
        c = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,)
        grad.putpixel((0, y), c)
    grad = grad.resize(img_size)
    mask = Image.new("L", img_size, 0)
    ImageDraw.Draw(mask).polygon(poly, fill=255)
    g.paste(grad, (0, 0), mask)
    return g

def make_icon(kind="bars"):
    """kind: bars = มี server-LED 3 ดวงมุมขวา / plain = เรียบ"""
    canvas = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    canvas.alpha_composite(rounded_tile(W))

    u = W / 1024.0
    zpoly = z_lightning_polygon(W) if kind == "lightning" else z_polygon(W)

    # glow หลังตัว Z
    glow = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    ImageDraw.Draw(glow).polygon(zpoly, fill=GLOW + (150,))
    glow = glow.filter(ImageFilter.GaussianBlur(int(60 * u)))
    canvas.alpha_composite(glow)

    # ตัว Z gradient
    canvas.alpha_composite(gradient_fill((W, W), zpoly, Z_TOP, Z_BOT))

    # เงาขาวเฉียงบน (มิติ)
    sheen = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    ds = ImageDraw.Draw(sheen)
    ds.polygon([(p[0], p[1]) for p in zpoly], outline=(255, 255, 255, 60), width=max(2, int(4 * u)))
    sheen = sheen.filter(ImageFilter.GaussianBlur(int(3 * u)))
    canvas.alpha_composite(sheen)

    if kind == "bars":
        # LED 3 ดวง (สถานะเซิร์ฟเวอร์) มุมขวาบน + glow
        leds = Image.new("RGBA", (W, W), (0, 0, 0, 0))
        dl = ImageDraw.Draw(leds)
        cx = int(866 * u)
        for i, col in enumerate(LED_COLORS):
            cy = int((132 + 62 * i) * u)
            r = int(21 * u)
            dl.ellipse([cx - r, cy - r, cx + r, cy + r], fill=col + (255,))
        glowled = leds.filter(ImageFilter.GaussianBlur(int(14 * u)))
        canvas.alpha_composite(glowled)
        canvas.alpha_composite(leds)

    return canvas.resize((S, S), Image.LANCZOS)

def make_banner(icon_img):
    bw, bh = 1000, 280
    b = Image.new("RGBA", (bw, bh), (0, 0, 0, 0))
    d = ImageDraw.Draw(b)
    d.rounded_rectangle([0, 0, bw - 1, bh - 1], radius=36, fill=(13, 20, 38, 255), outline=BORDER + (255,), width=2)
    ic = icon_img.resize((220, 220), Image.LANCZOS)
    b.alpha_composite(ic, (28, 30))
    f_title = ImageFont.truetype(r"C:\Windows\Fonts\segoeuib.ttf", 78)
    # ภาษาไทยใช้ Leelawadee UI (Segoe UI บางเวอร์ชันไม่มี glyph ไทย)
    f_sub = ImageFont.truetype(r"C:\Windows\Fonts\LeelawUI.ttf", 38)
    d.text((280, 62), "zServer Manager", font=f_title, fill=(240, 248, 255, 255))
    d.text((284, 172), "Apache · PHP · MariaDB — ควบคุมในที่เดียว", font=f_sub, fill=(150, 175, 215, 255))
    return b

a = make_icon("bars")       # Design A: Z + LED server stack
b = make_icon("lightning")  # Design B: Z สายฟ้า
a.save(os.path.join(ROOT, r"tools\icon\design-A-bars.png"))
b.save(os.path.join(ROOT, r"tools\icon\design-B-lightning.png"))

# ใช้ Design A เป็นตัวจริง (ปรับได้)
chosen = a
os.makedirs(os.path.dirname(ICO_OUT), exist_ok=True)
chosen.save(ICO_OUT, format="ICO", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
chosen.save(LOGO_OUT)
make_banner(chosen).save(BANNER_OUT)
print("done:", ICO_OUT, LOGO_OUT, BANNER_OUT, "size:", os.path.getsize(ICO_OUT), "bytes")
