using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Audio;

namespace Yjcm;

// Route only this character's explicit selection sound to the supplied WAV.
// Do not replay Ironclad's voice or pass a made-up event path to FMOD.
[HarmonyPatch(typeof(NAudioManager), nameof(NAudioManager.PlayOneShot), [typeof(string), typeof(Dictionary<string, float>), typeof(float)])]
internal static class WeiCaoCaoSelectionAudio
{
    internal const string SoundId = "Yjcm.WeiCaoCao.character_select";
    private static bool Prefix(string path)
    {
        if (path != SoundId) return true;
        WeiCaoCaoAudio.Play("character_select");
        return false;
    }
}
