using System.Reflection;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

internal static class EnergyDescriptionCases
{
    internal static void Run(Assembly assembly, string dependencies, Action<bool,string> check)
    {
        // Exercise the shipped game's formatter, without starting LocManager/Godot.
        var smartAssembly=Assembly.LoadFrom(Path.Combine(dependencies,"SmartFormat.dll"));
        var smartType=smartAssembly.GetType("SmartFormat.Smart",true)!;
        var formatter=smartType.GetMethod("CreateDefaultSmartFormat")!.Invoke(null,[null])!;
        var extensionType=smartAssembly.GetType("SmartFormat.Core.Extensions.IFormatter",true)!;
        var extensions=Array.CreateInstance(extensionType,1);
        extensions.SetValue(Activator.CreateInstance(typeof(EnergyVar).Assembly.GetType("MegaCrit.Sts2.Core.Localization.Formatters.EnergyIconsFormatter",true)!),0);
        formatter.GetType().GetMethod("AddExtensions",[extensionType.MakeArrayType()])!.Invoke(formatter,[extensions]);
        var format=formatter.GetType().GetMethod("Format",[typeof(string),typeof(object[])])!;
        string Render(string text,Dictionary<string,object> vars)=>(string)format.Invoke(formatter,[text,new object[]{vars}])!;
        const string prefix="yjcm_qimen";
#if GAME_V0113
        const int maxRepeatedIcons = 4;
#else
        const int maxRepeatedIcons = 3;
#endif
        foreach(int n in new[]{0,1,2,3,4,7,15,20})
        {
            string text=Render("{energyPrefix:energyIcons("+n+")}",new(){{"energyPrefix",prefix}});
            check(Regex.Matches(text,@"\[img\]").Count==(n >= 1 && n <= maxRepeatedIcons?n:1),"Native energy icon threshold: "+n);
            check(text.Contains("/"+prefix+"_energy_icon.png"),"Description uses selected character energy art");
            if(n==0||n>maxRepeatedIcons)check(text.StartsWith(n.ToString()),"Large/zero energy amounts retain their number: "+n);
        }
        foreach(var (name,key,normal,upgraded) in new[]{("MouPangTongYinMouYang","Energy",2,3),("TunChu","Energy",15,20),("BeiFa","EnergyGain",7,8)})
        {
            var card=(CardModel)Activator.CreateInstance(assembly.GetType("Yjcm."+name,true)!)!;
            check(card.DynamicVars[key] is EnergyVar,"Energy formatter requires EnergyVar, not generic DynamicVar: "+name);
            var energy=(EnergyVar)card.DynamicVars[key];energy.ColorPrefix=prefix;
            foreach(bool upgrade in new[]{false,true})
            {
                if(upgrade)card.GetType().GetMethod("OnUpgrade",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(card,null);
                int n=upgrade?upgraded:normal;
                string text=Render("{Value:energyIcons()}",new(){{"Value",energy}});
                check(energy.BaseValue==n,"Energy amount and upgrade preserved: "+name);
                check(Regex.Matches(text,@"\[img\]").Count==(n<=maxRepeatedIcons?n:1),"Dynamic upgrade energy icons: "+name);
                if(n>maxRepeatedIcons)check(text.Contains(n.ToString()),"Large dynamic energy amount remains visible: "+name);
            }
        }
    }
}
