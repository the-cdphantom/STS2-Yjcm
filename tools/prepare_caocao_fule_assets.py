"""Copy supplied FuLe art and convert eight supplied voices, developer profile only."""
from pathlib import Path
import shutil, sys, wave
from PIL import Image
repo=Path(__file__).resolve().parent.parent
materials=Path('E:/sts2mod/素材')
sys.path.insert(0,str(repo/'artifacts/tools/audio-python'))
import miniaudio
images=repo/'mod/Experimental/WeiCaoCao/images'
for name,key in [('赋乐','fu_le'),('赋图标','fu_enchantment'),('文澜buff图标','wen_lan_buff')]:
    source=materials/'图片处理后/威曹操'/f'{name}.png'
    with Image.open(source) as image:
        if key=='fu_le':shutil.copy2(source,images/f'{key}.png')
        else:image.convert('RGBA').resize((256,256),Image.Resampling.LANCZOS).save(images/f'{key}.png')
for prefix,key in [('赋乐','fu_le'),('文澜','wen_lan')]:
    for n in range(1,5):
        source=materials/'语音原素材/威曹操'/f'{prefix}{n}.mp3'
        dest=repo/'assets/experimental/WeiCaoCao/Voice/WeiCaoCao'/f'{key}_{n}.wav'
        sound=miniaudio.decode(source.read_bytes(),output_format=miniaudio.SampleFormat.SIGNED16,nchannels=1,sample_rate=48000)
        with wave.open(str(dest),'wb') as output:
            output.setnchannels(1);output.setsampwidth(2);output.setframerate(48000);output.writeframes(sound.samples.tobytes())
        backup=materials/'语音素材/威曹操'/f'{prefix}{n}.wav'
        backup.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(dest,backup)
        with wave.open(str(dest),'rb') as check:
            assert check.getnchannels()==1 and check.getframerate()==48000 and check.getsampwidth()==2 and check.getnframes()>0
print('CAOCAO_FULE_ASSETS_OK images=3 voices=8 mono48k PCM16')
