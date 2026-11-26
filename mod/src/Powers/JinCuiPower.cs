using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;

#nullable enable

namespace Yjcm;

/// <summary>
/// 尽瘁形态：回合开始观星7，将体力值调整为牌组中武将牌数量的7倍。
/// 打出时替换本场战斗BGM。
/// </summary>
public sealed class JinCuiPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;
    public override string CustomIconPath => "res://Yjcm/Images/Powers/jin_cui.png";
    public override string CustomBigIconPath => "res://Yjcm/Images/Powers/jin_cui.png";

    private int _wuJiangBaseCount;
    private bool _initialized;

    private const string BgmPath = "res://Yjcm/Audio/liuchuqishan.wav";
    private static Godot.AudioStream? _bgmStream;
    private static Godot.AudioStreamPlayer? _bgmPlayer;
    private static bool _bgmStopping;
    private static bool _bgmRequested;

    public static bool IsBgmRequested => _bgmRequested;

    public static void PlayBgm()
    {
        _bgmRequested = true;
        if (MouSimaYiBgmPlayer.IsBattleActive)
        {
            MouSimaYiBgmPlayer.ReconcileMusicPriority();
            return;
        }

        StartRequestedBgm();
    }

    public static void StartRequestedBgm()
    {
        if (!_bgmRequested)
            return;

        _bgmStopping = false;
        if (_bgmPlayer != null && Godot.GodotObject.IsInstanceValid(_bgmPlayer))
        {
            ApplyBgmVolumeSetting();
            return;
        }

        if (_bgmStream == null)
        {
            // PCK 路径区分大小写；旧资源包缺少内置曲目时使用下方外置音频。
            if (Godot.ResourceLoader.Exists(BgmPath))
                _bgmStream = Godot.GD.Load<Godot.AudioStream>(BgmPath);
            if (_bgmStream == null)
            {
                var filePath = Path.Combine(
                    Path.GetDirectoryName(typeof(JinCuiPower).Assembly.Location)!,
                    "Voice", "Bank", "Assets", "liuchuqishan.wav");
                if (!Godot.FileAccess.FileExists(filePath)) return;
                using var f = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
                f.GetBuffer(4); f.GetBuffer(4); f.GetBuffer(4);
                short ch = 1; int sr = 44100;
                var fmt = Godot.AudioStreamWav.FormatEnum.Format16Bits;
                while (f.GetPosition() < f.GetLength() - 8)
                {
                    var id = f.GetBuffer(4);
                    var sz = BitConverter.ToInt32(f.GetBuffer(4));
                    if (id[0] == 'f' && id[1] == 'm' && id[2] == 't' && id[3] == ' ')
                    {
                        f.GetBuffer(2); ch = BitConverter.ToInt16(f.GetBuffer(2));
                        sr = BitConverter.ToInt32(f.GetBuffer(4)); f.GetBuffer(6);
                        fmt = BitConverter.ToInt16(f.GetBuffer(2)) == 8
                            ? Godot.AudioStreamWav.FormatEnum.Format8Bits : Godot.AudioStreamWav.FormatEnum.Format16Bits;
                        if (sz > 16) f.GetBuffer(sz - 16);
                    }
                    else if (id[0] == 'd' && id[1] == 'a' && id[2] == 't' && id[3] == 'a')
                    {
                        var s = new Godot.AudioStreamWav { Format = fmt, MixRate = sr, Stereo = ch >= 2, Data = f.GetBuffer(sz) };
                        _bgmStream = s;
                        break;
                    }
                    else f.GetBuffer(sz);
                }
            }
            if (_bgmStream == null) return;
        }

        _bgmPlayer = new Godot.AudioStreamPlayer
        {
            Name = "JinCuiBgm",
            Stream = _bgmStream,
            VolumeDb = GetBgmVolumeDb(),
            Autoplay = true,
        };

        _bgmPlayer.Finished += () =>
        {
            if (_bgmStopping) return;
            if (_bgmPlayer != null
                && Godot.GodotObject.IsInstanceValid(_bgmPlayer)
                && !_bgmStopping)
            {
                _bgmPlayer.Play(0f);
            }
        };

        var tree = Godot.Engine.GetMainLoop() as Godot.SceneTree;
        tree?.Root.AddChild(_bgmPlayer);
    }

    public static void ApplyBgmVolumeSetting()
    {
        if (_bgmPlayer != null && Godot.GodotObject.IsInstanceValid(_bgmPlayer))
            _bgmPlayer.VolumeDb = GetBgmVolumeDb();
    }

    public static void StopBgm()
    {
        _bgmRequested = false;
        StopBgmPlayerOnly();
    }

    public static void StopBgmPlayerOnly()
    {
        _bgmStopping = true;
        if (_bgmPlayer != null && Godot.GodotObject.IsInstanceValid(_bgmPlayer))
        {
            _bgmPlayer.Stop();
            ((Godot.Node)_bgmPlayer).QueueFree();
        }
        _bgmPlayer = null;
    }

    private static float GetBgmVolumeDb()
    {
        var settings = SaveManager.Instance.SettingsSave;
        float linear = settings.VolumeMaster
            * settings.VolumeBgm
            * (WuZhugeliangSettings.JinCuiBgmVolume / 100f);
        return linear <= 0f ? -80f : Godot.Mathf.LinearToDb(linear);
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        await base.AfterCombatEnd(room);
        StopBgm();
    }

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        await base.BeforeHandDraw(player, choiceContext, combatState);
        if (player.Creature != Owner) return;

        if (!_initialized)
        {
            var allCards = new List<CardModel>();
            var drawCards = PileTypeExtensions.GetPile(PileType.Draw, player)?.Cards;
            var handCards = PileTypeExtensions.GetPile(PileType.Hand, player)?.Cards;
            var discardCards = PileTypeExtensions.GetPile(PileType.Discard, player)?.Cards;
            if (drawCards != null) allCards.AddRange(drawCards);
            if (handCards != null) allCards.AddRange(handCards);
            if (discardCards != null) allCards.AddRange(discardCards);
            _wuJiangBaseCount = allCards.Count(c => c.Tags.Contains(YjcmCardTags.WuJiang));
            _initialized = true;
        }

        VoicePlayer.PlayWav(TriggerVoicePath);

        await GuanXingHelper.GuanXing(choiceContext, player, 7);

        var targetHp = Math.Min(_wuJiangBaseCount * 7, player.Creature.MaxHp);
        if (player.Creature.CurrentHp < targetHp)
            await CreatureCmd.SetCurrentHp(player.Creature, targetHp);
    }

    public static readonly string TriggerVoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(JinCuiPower).Assembly.Location)!,
        "Voice", "Bank", "Assets", "jin_cui_buff.wav");

    public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || Owner.IsDead || player.PlayerCombatState is not { } state)
            return;

        int draw = 7 - PileType.Hand.GetPile(player).Cards.Count;
        if (draw > 0)
            await CardPileCmd.Draw(choiceContext, draw, player, false);

        if (state.Energy < 7 && !Owner.IsDead && !CombatManager.Instance.IsOverOrEnding)
            state.Energy = 7;
    }
}
