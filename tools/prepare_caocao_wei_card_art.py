"""Crop the supplied Wei card portraits without modifying source artwork.

Crop boxes are individually framed in source pixels. No stretching, recoloring,
or generated fill is used. Run with the project's Pillow environment.
"""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

REPO = Path(__file__).resolve().parent.parent
# Source filename, processed filename, runtime filename, source crop box.
PORTRAITS = [
    ("威曹丕", "典论", "dian_lun", (230, 0, 1430, 900)),
    ("胡班", "崇义", "chong_yi", (18, 0, 860.6666667, 632)),
    ("肃军", "肃军", "su_jun", (250, 0, 1450, 900)),
    ("砾锋", "砥锋", "di_feng", (190, 0, 1390, 900)),
    ("钟毓", "智对", "zhi_dui", (350, 0, 1550, 900)),
    ("谋蒋济", "应时", "ying_shi", (200, 0, 1400, 900)),
    ("谋程昱", "识诈", "shi_zha", (180, 0, 1380, 900)),
    ("星张春华", "梁燕", "liang_yan", (250, 0, 1450, 900)),
    ("谋荀彧阳", "势谋阳", "shi_mou_yang", (18, 0, 860.6666667, 632)),
    ("谋荀彧阴", "势谋阴", "shi_mou_yin", (200, 0, 1400, 900)),
    ("谋司马懿阳", "权谋阳", "quan_mou_yang", (200, 0, 1400, 900)),
    ("谋司马懿阴", "权谋阴", "quan_mou_yin", (160, 0, 1360, 900)),
    ("谋郭嘉阳", "先谋阳", "xian_mou_yang", (200, 0, 1400, 900)),
    ("谋郭嘉阴", "先谋阴", "xian_mou_yin", (100, 0, 1300, 900)),
    ("散士", "散士", "san_shi", (350, 0, 1550, 900)),
    ("沉略", "沉略", "chen_lue", (18, 0, 860.6666667, 632)),
]
SECOND_BATCH = [
    ("卞喜", "钝袭", "dun_xi", (140, 0, 1340, 900)),
    ("车胄", "慑伏", "she_fu", (18, 0, 860.6666667, 632)),
    ("谋黄权", "巧对", "qiao_dui", (18, 0, 860.6666667, 632)),
    ("神邓艾", "拓域", "tuo_yu", (18, 0, 860.6666667, 632)),
    ("神典韦", "掣挟", "che_xie", (18, 0, 860.6666667, 632)),
    ("孙礼", "伉俪", "kang_li", (18, 0, 860.6666667, 632)),
    ("吴质", "伪涕", "wei_ti", (18, 0, 860.6666667, 632)),
    ("夏侯玄", "博玄", "bo_xuan", (18, 0, 860.6666667, 632)),
    ("闫柔", "仇讨", "chou_tao", (18, 0, 860.6666667, 632)),
    ("羊祜", "明伐", "ming_fa", (18, 0, 860.6666667, 632)),
    ("周宣", "寤寐", "wu_mei", (18, 0, 860.6666667, 632)),
    ("朱灵", "战意", "zhan_yi", (18, 0, 860.6666667, 632)),
    ("SP关羽", "怒嗔", "nu_chen", (18, 0, 860.6666667, 632)),
]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--materials", type=Path, default=Path("E:/sts2mod/素材"))
    parser.add_argument("--batch", choices=["first", "second", "all"], default="all")
    args = parser.parse_args()
    raw = args.materials / "图片素材/威曹操"
    processed = args.materials / "图片处理后/威曹操"
    runtime = REPO / "mod/Experimental/WeiCaoCao/images"
    suffix = "-batch2" if args.batch == "second" else "-all" if args.batch == "all" else ""
    report = REPO / ("artifacts/design/caocao-card-art-20261103" + suffix)
    for folder in [processed, runtime, report]:
        folder.mkdir(parents=True, exist_ok=True)
    selected = (PORTRAITS if args.batch != "second" else []) + (SECOND_BATCH if args.batch != "first" else [])
    plan = [(raw / (source + ".png"), label, slug, box) for source, label, slug, box in selected]
    if args.batch != "first":
        # The user's finished Su Yue portrait is copied byte-for-byte, never cropped.
        plan.append((processed / "苏越.png", "宫图", "gong_tu", None))
    # Validate all inputs before writing any outputs.
    for original, _, _, box in plan:
        with Image.open(original) as image:
            if box is None:
                assert image.size == (800, 600), "Finished Su Yue art must keep its supplied dimensions"
            else:
                assert 0 <= box[0] < box[2] <= image.width
                assert 0 <= box[1] < box[3] <= image.height
                assert abs((box[2] - box[0]) / (box[3] - box[1]) - 4 / 3) < 1e-6
    sheet = Image.new("RGB", (1280, 250 * ((len(plan) + 3) // 4)), (25, 27, 34))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 20)
    manifest = []
    for index, (original, label, slug, box) in enumerate(plan):
        with Image.open(original) as image:
            art = image.convert("RGB")
            if box is not None:
                art = art.resize((800, 600), Image.Resampling.LANCZOS, box=box)
        target = runtime / (slug + ".png")
        if box is None:
            target.write_bytes(original.read_bytes())
        else:
            art.save(target)
            (processed / (label + ".png")).write_bytes(target.read_bytes())
        thumb = art.resize((312, 234), Image.Resampling.LANCZOS)
        x, y = index % 4 * 320, index // 4 * 250
        sheet.paste(thumb, (x + 4, y))
        draw.rectangle((x + 4, y + 208, x + 316, y + 234), fill=(20, 23, 30))
        draw.text((x + 10, y + 209), label, font=font, fill="white")
        manifest.append({"source": str(original), "sourceSha256": hashlib.sha256(original.read_bytes()).hexdigest(),
                         "crop": box, "size": [800, 600], "runtime": str(target),
                         "sha256": hashlib.sha256(target.read_bytes()).hexdigest()})
    sheet.save(report / "cropped-contact-sheet.jpg", quality=94)
    (report / "crop-manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"CAOCAO_CARD_ART_OK portraits={len(manifest)} size=800x600 sources=unchanged")


if __name__ == "__main__":
    main()
