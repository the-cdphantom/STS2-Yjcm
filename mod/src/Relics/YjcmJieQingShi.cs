using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

[RegisterRelic(typeof(SharedRelicPool))]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "STS001")]
public sealed class YjcmJieQingShi : QingShiRelic
{
    public static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(YjcmJieQingShi).Assembly.Location)!,
        "Voice", "Bank", "Assets", "jie_qing_shi.wav");

    protected override bool IsRefined => true;
    protected override string TriggerVoicePath => VoicePath;
}
