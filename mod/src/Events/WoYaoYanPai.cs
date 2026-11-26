using Godot;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

#nullable enable

namespace Yjcm;

/// <summary>普通随机事件“我要验牌”。</summary>
[RegisterSharedEvent]
public sealed class WoYaoYanPai : ModEventTemplate
{
    // PCK 内路径区分大小写，必须与实际的 Images/events 目录一致。
    private const string PortraitPath =
        "res://Yjcm/Images/events/wo_yao_yan_pai.jpg";
    private const string EntrySfxPath =
        "res://Yjcm/Audio/Events/wo_yao_yan_pai.wav";

    private static AudioStream? _entrySfx;

    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: PortraitPath
    );

    public override bool IsAllowed(IRunState runState) =>
        IsAllowedForScope(runState, WuZhugeliangSettings.ModEventScope);

    internal static bool IsAllowedForScope(IRunState runState, string scope) =>
        WuZhugeliangSettings.MatchesAnyPlayerScope(runState, scope);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new GoldVar(150),
        new DamageVar(7m, ValueProp.Unblockable | ValueProp.Unpowered)
    ];

    protected override Task BeforeEventStarted(bool isPreFinished)
    {
        PlayEntrySfx();
        return Task.CompletedTask;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption(this, AcceptBribe, InitialOptionKey("NO_PROBLEM"),
            HoverTipFactory.FromCardWithCardHoverTips<Shame>()),
        new EventOption(this, ExposeCheating, InitialOptionKey("HAS_PROBLEM"),
            HoverTipFactory.FromRelic<LiuYanZhaoLing>())
    ];

    private async Task AcceptBribe()
    {
        await PlayerCmd.GainGold(DynamicVars.Gold.BaseValue, Owner!);
        await CardPileCmd.AddCurseToDeck<Shame>(Owner!);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.ACCEPTED_BRIBE.description"));
    }

    private async Task ExposeCheating()
    {
        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            Owner!.Creature,
            DynamicVars.Damage,
            null!,
            null!
        );
        await RelicCmd.Obtain<LiuYanZhaoLing>(Owner!);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.EXPOSED_CHEATING.description"));
    }

    private static void PlayEntrySfx()
    {
        _entrySfx ??= GD.Load<AudioStream>(EntrySfxPath);
        if (_entrySfx is null)
        {
            MainFile.Logger.Warn($"事件入场音效加载失败: {EntrySfxPath}");
            return;
        }

        float volume = SaveManager.Instance.SettingsSave.VolumeSfx;
        var player = new AudioStreamPlayer
        {
            Name = "WoYaoYanPaiEntrySfx",
            Stream = _entrySfx,
            VolumeDb = Mathf.LinearToDb(volume)
        };

        player.Finished += player.QueueFree;
        if (Engine.GetMainLoop() is not SceneTree tree)
        {
            player.QueueFree();
            return;
        }

        tree.Root.AddChild(player);
        player.Play();
    }
}
