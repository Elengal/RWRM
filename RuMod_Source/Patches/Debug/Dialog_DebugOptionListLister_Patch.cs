using HarmonyLib;
using Verse;
using LudeonTK;
using System;
using System.Collections.Generic;
using RuMod.Utils;

namespace RuMod.Patches.Debug
{
    // Патч для перевода списков (например, "Spawn Thing", "Spawn Pawn")
    // Атрибуты HarmonyPatch УБРАНЫ, чтобы избежать краша при авто-загрузке.
    // Патч применяется вручную в Main.cs
    public static class Dialog_DebugOptionListLister_Patch
    {
        // Используем ref для замены списка опций
        public static void Prefix(ref IEnumerable<DebugMenuOption> options)
        {
            if (options == null || !Prefs.DevMode) return;

            var newOptions = new List<DebugMenuOption>();
            foreach (var opt in options)
            {
                var modifiedOpt = opt;

                if (!string.IsNullOrEmpty(modifiedOpt.label))
                {
                    modifiedOpt.label = DevModeTranslator.Translate(modifiedOpt.label);
                }
                newOptions.Add(modifiedOpt);
            }

            options = newOptions;
        }
    }
}
