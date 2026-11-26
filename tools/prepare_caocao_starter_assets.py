"""Prepare developer-only Cao Cao assets; retain originals and WAV source copies."""
import argparse
from pathlib import Path
import shutil
import sys
import wave
from PIL import Image

repo = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(repo / 'artifacts/tools/audio-python'))
import miniaudio

parser = argparse.ArgumentParser()
parser.add_argument('--materials', type=Path, default=Path('E:/sts2mod/素材'))
parser.add_argument('--relic-source', type=Path, required=True)
args = parser.parse_args()
images = repo / 'mod/Experimental/WeiCaoCao/images'
images.mkdir(parents=True, exist_ok=True)
source = args.materials / '图片处理后/威曹操'
for src, dest in [('头像与地图图标.png', 'avatar.png'), ('选人界面.png', 'select_icon.png'),
                  ('军合buff.png', 'jun_he_buff.png'), ('掇月.png', 'duo_yue.png')]:
    shutil.copy2(source / src, images / dest)
shutil.copy2(args.materials / '图片素材/威曹操/护驾.png', images / 'hu_jia.png')
# Only normalize the generated artwork's file dimensions; do not alter its design or alpha.
with Image.open(args.relic_source) as image:
    assert image.mode == 'RGBA' and image.getextrema()[3][0] == 0, 'Relic must have actual transparency'
    image.resize((256, 256), Image.Resampling.LANCZOS).save(images / 'jun_he.png')
shutil.copy2(images / 'jun_he.png', source / '军合遗物.png')
with Image.open(images / 'jun_he.png') as image:
    assert image.size == (256, 256) and image.mode == 'RGBA'

for src, key in [('军合1', 'jun_he_1'), ('军合2', 'jun_he_2'), ('护驾', 'hu_jia'),
                 ('掇月攻击', 'duo_yue_attack'), ('掇月摸牌', 'duo_yue_draw'),
                 ('选中音效', 'character_select')]:
    original = args.materials / '语音原素材/威曹操' / (src + '.mp3')
    target = repo / 'assets/experimental/WeiCaoCao/Voice/WeiCaoCao' / (key + '.wav')
    sound = miniaudio.decode(original.read_bytes(), output_format=miniaudio.SampleFormat.SIGNED16,
                             nchannels=1, sample_rate=48000)
    target.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(target), 'wb') as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(48000)
        output.writeframes(sound.samples.tobytes())
    backup = args.materials / '语音素材/威曹操' / (src + '.wav')
    backup.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(target, backup)
    with wave.open(str(target), 'rb') as check:
        assert check.getnchannels() == 1 and check.getsampwidth() == 2
        assert check.getframerate() == 48000 and check.getnframes() > 0
        print(f'CAOCAO_VOICE_OK {key} frames={check.getnframes()}')
print('CAOCAO_STARTER_ASSETS_OK images=6 relic=256x256 voices=6')
