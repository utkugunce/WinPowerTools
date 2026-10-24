from PIL import Image, ImageDraw

def create_power_icon(path):
    sizes = [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)]
    images = []

    for size in sizes:
        img = Image.new("RGBA", size, (0, 0, 0, 0))
        draw = ImageDraw.Draw(img)
        w, h = size
        s = w / 256.0

        # Arka plan yuvarlak kart (Modern Windows 11 App Icon)
        pad = int(12 * s)
        draw.rounded_rectangle(
            [pad, pad, w - pad, h - pad],
            radius=int(48 * s),
            fill=(15, 23, 42, 255), # Deep Slate
            outline=(168, 85, 247, 240), # Neon Mor Vurgu
            width=max(1, int(4 * s))
        )

        # Şimşek / Enerji İkonu (Lightning Bolt)
        # Koordinatlar: 256x256 üzerinde
        pts = [
            (145 * s, 35 * s),
            (75 * s, 135 * s),
            (125 * s, 135 * s),
            (105 * s, 220 * s),
            (185 * s, 115 * s),
            (135 * s, 115 * s),
        ]
        draw.polygon(pts, fill=(168, 85, 247, 255))
        # İç parıltı
        pts_inner = [
            (143 * s, 50 * s),
            (90 * s, 130 * s),
            (125 * s, 130 * s),
            (112 * s, 195 * s),
            (170 * s, 120 * s),
            (135 * s, 120 * s),
        ]
        draw.polygon(pts_inner, fill=(232, 121, 249, 255))

        images.append(img)

    images[0].save(path, format="ICO", sizes=[(s[0], s[1]) for s in sizes], append_images=images[1:])
    print(f"Icon created: {path}")

create_power_icon(r"C:\Users\utkug\.gemini\antigravity\scratch\WinPowerTools\icon.ico")
