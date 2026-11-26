"""Convert supplied Wei design voices to mono 48kHz PCM16 WAV, preserving MP3s."""
import argparse
from pathlib import Path
import shutil
import sys
import wave

REPO = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(REPO / "artifacts/tools/audio-python"))
import miniaudio

VOICES = {
    "弼佐": "bi_zuo", "典论": "dian_lun", "应时": "ying_shi", "识诈": "shi_zha",
    "势谋阳": "shi_mou_yang", "势谋阴": "shi_mou_yin", "先谋阳": "xian_mou_yang",
    "先谋阴": "xian_mou_yin", "巧对": "qiao_dui", "散士": "san_shi", "沉略": "chen_lue",
    "崇义": "chong_yi", "肃军": "su_jun", "砾锋": "di_feng", "智对": "zhi_dui",
    "梁燕": "liang_yan", "逆嗔": "nu_chen", "钝袭": "dun_xi", "挈携": "che_xie",
    "宫图": "gong_tu", "博玄": "bo_xuan", "仇讨": "chou_tao", "伉历": "kang_li",
    "伪涕": "wei_ti", "战意": "zhan_yi", "寤寐": "wu_mei", "慑伏": "she_fu",
    "明伐": "ming_fa", "拓域": "tuo_yu",
    "骤袭": "zhou_xi", "恃武": "shi_wu", "秉纪": "bing_ji", "破锐": "po_rui", "犷骜": "guang_ao", "凝袍": "ning_pao", "溃叛": "kui_pan",
    "归心形态": "gui_xin_xing_tai", "挟天子以令诸侯": "xie_tian_zi",
    "界军合": "jie_jun_he", "界掇月1": "jie_duo_yue_1", "界掇月2": "jie_duo_yue_2",
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--materials", type=Path, default=Path("E:/sts2mod/素材"))
    args = parser.parse_args()
    output = REPO / "assets/experimental/WeiCaoCao/Voice/WeiCaoCao"
    backup = args.materials / "语音素材/威曹操"
    output.mkdir(parents=True, exist_ok=True)
    backup.mkdir(parents=True, exist_ok=True)
    for name, slug in VOICES.items():
        original = args.materials / "语音原素材/威曹操" / (name + ".mp3")
        sound = miniaudio.decode(original.read_bytes(), output_format=miniaudio.SampleFormat.SIGNED16,
                                 nchannels=1, sample_rate=48000)
        destination = output / (slug + ".wav")
        with wave.open(str(destination), "wb") as wav:
            wav.setnchannels(1)
            wav.setsampwidth(2)
            wav.setframerate(48000)
            wav.writeframes(sound.samples.tobytes())
        shutil.copy2(destination, backup / (name + ".wav"))
        with wave.open(str(destination), "rb") as wav:
            assert (wav.getnchannels(), wav.getsampwidth(), wav.getframerate()) == (1, 2, 48000)
            assert wav.getnframes() > 0
            print(f"CAOCAO_VOICE_OK {slug} seconds={wav.getnframes()/48000:.2f}")
    print(f"CAOCAO_WEI_VOICES_OK voices={len(VOICES)} sourceMP3s=unchanged")


if __name__ == "__main__":
    main()
