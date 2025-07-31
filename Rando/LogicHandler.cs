using System;
using System.Reflection;
using RandomizerCore.Json;
using RandomizerCore.Logic;
using RandomizerMod.RC;
using RandomizerMod.Settings;

namespace PaleCourtCharms.Rando
{
    internal static class LogicHandler
    {
        internal static void Hook()
        {
            if (PaleCourtCharms.GlobalSettings.AddCharms)
            {


                RCData.RuntimeLogicOverride.Subscribe(0f, ApplyLogic);
            }
        }

     private static void ApplyLogic(GenerationSettings gs, LogicManagerBuilder lmb)
{
    if (!PaleCourtCharms.GlobalSettings.AddCharms)
        return;

    Assembly asm = Assembly.GetExecutingAssembly();
    JsonLogicFormat fmt = new();

    using (var s = asm.GetManifestResourceStream("PaleCourtCharms.Rando.Terms.json"))
        lmb.DeserializeFile(LogicFileType.Terms, fmt, s);


    using (var s = asm.GetManifestResourceStream("PaleCourtCharms.Rando.Locations.json"))
        lmb.DeserializeFile(LogicFileType.Locations, fmt, s);

 
    using (var s = asm.GetManifestResourceStream("PaleCourtCharms.Rando.Items.json"))
        lmb.DeserializeFile(LogicFileType.ItemStrings, fmt, s);
}

    }
}
