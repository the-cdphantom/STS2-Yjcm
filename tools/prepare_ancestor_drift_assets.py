"""Decode supplied MP3s as mono PCM16/48k WAV without altering the originals.

Uses miniaudio==1.61, installed in artifacts/tools/audio-python (local only).
"""
import argparse
from pathlib import Path
import sys
import wave
import shutil

repo = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(repo / "artifacts/tools/audio-python"))
import miniaudio

parser = argparse.ArgumentParser()
parser.add_argument("--materials", type=Path, default=Path("E:/sts2mod/素材"))
args = parser.parse_args()
for name, key in (("祖荫", "zu_yin"), ("漂萍", "piao_ping")):
    source = args.materials / "语音原素材/威董卓" / f"{name}.mp3"
    target = repo / "assets/loose/Voice/WeiDongZhuo" / f"{key}.wav"
    backup = args.materials / "语音素材/威董卓" / f"{name}.wav"
    sound = miniaudio.decode(source.read_bytes(), output_format=miniaudio.SampleFormat.SIGNED16,
                                  nchannels=1, sample_rate=48000)
    target.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(target), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(48000)
        output.writeframes(sound.samples.tobytes())
    with wave.open(str(target), "rb") as result:
        assert result.getnchannels() == 1 and result.getsampwidth() == 2
        assert result.getframerate() == 48000 and result.getnframes() > 0
        print(f"VOICE_OK {key} frames={result.getnframes()} rate=48000 channels=1 bits=16")
    backup.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(target, backup)
