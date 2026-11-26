using Godot;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

#nullable enable

namespace Yjcm;

/// <summary>谋诸葛亮专属事件“子午谷奇谋”。</summary>
[RegisterSharedEvent]
public sealed class ZiWuGuQiMou : ModEventTemplate
{
    private const string PortraitPath = "res://Yjcm/Images/events/zi_wu_gu_qi_mou.png";
    private const string EntryBgmPath = "res://Yjcm/Audio/Events/zi_wu_gu_qi_mou_bgm.wav";

    private static AudioStream? _entryBgm;
    private AudioStreamPlayer? _entryBgmPlayer;

    public override EventAssetProfile AssetProfile => new(InitialPortraitPath: PortraitPath);

    // 共享事件在队伍中有谋诸葛亮时才会加入可出现事件池；同时受随机事件范围设置约束。
    public override bool IsAllowed(IRunState runState) =>
        IsAllowedForScope(runState, WuZhugeliangSettings.ModEventScope);

    internal static bool IsAllowedForScope(IRunState runState, string scope) =>
        WuZhugeliangSettings.MatchesAnyPlayerScope(runState, scope)
        && runState.Players.Any(player => player.Character is WuZhugeliang);

    protected override Task BeforeEventStarted(bool isPreFinished)
    {
        PlayEntryBgm();
        return Task.CompletedTask;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption(this, SupportWeiYan, InitialOptionKey("SUPPORT_WEI_YAN"),
            HoverTipFactory.FromCardWithCardHoverTips<ShiWeiYanZhongAo>()),
        new EventOption(this, SupportZhugeLiang, InitialOptionKey("SUPPORT_ZHUGE_LIANG"),
            HoverTipFactory.FromCardWithCardHoverTips<MouZhugeliangHuoJi>())
    ];

    private async Task SupportWeiYan()
    {
        await AddToDeck<ShiWeiYanZhongAo>();
        await AddToDeck<ShiWeiYanZhuangShi>();
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.SUPPORT_WEI_YAN.description"));
    }

    private async Task SupportZhugeLiang()
    {
        await AddToDeck<MouZhugeliangHuoJi>();
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.SUPPORT_ZHUGE_LIANG.description"));
    }

    private async Task AddToDeck<T>() where T : CardModel
    {
        var card = Owner!.RunState.CreateCard<T>(Owner!);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck), 2f);
    }

    protected override void OnEventFinished()
    {
        StopEntryBgm();
        base.OnEventFinished();
    }

    private void PlayEntryBgm()
    {
        StopEntryBgm();
        _entryBgm ??= GD.Load<AudioStream>(EntryBgmPath);
        if (_entryBgm is null)
        {
            MainFile.Logger.Warn($"子午谷奇谋入场音乐加载失败: {EntryBgmPath}");
            return;
        }

        float linear = SaveManager.Instance.SettingsSave.VolumeMaster * SaveManager.Instance.SettingsSave.VolumeBgm;
        _entryBgmPlayer = new AudioStreamPlayer
        {
            Name = "ZiWuGuQiMouEntryBgm",
            Stream = _entryBgm,
            VolumeDb = linear <= 0f ? -80f : Mathf.LinearToDb(linear)
        };
        _entryBgmPlayer.Finished += () =>
        {
            if (_entryBgmPlayer != null && GodotObject.IsInstanceValid(_entryBgmPlayer))
                _entryBgmPlayer.QueueFree();
            _entryBgmPlayer = null;
        };

        if (Engine.GetMainLoop() is not SceneTree tree)
        {
            _entryBgmPlayer.QueueFree();
            _entryBgmPlayer = null;
            return;
        }

        tree.Root.AddChild(_entryBgmPlayer);
        _entryBgmPlayer.Play();
    }

    private void StopEntryBgm()
    {
        if (_entryBgmPlayer == null || !GodotObject.IsInstanceValid(_entryBgmPlayer))
        {
            _entryBgmPlayer = null;
            return;
        }

        _entryBgmPlayer.Stop();
        _entryBgmPlayer.QueueFree();
        _entryBgmPlayer = null;
    }
}
