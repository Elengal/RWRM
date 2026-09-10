using HarmonyLib;
using UnityEngine;
using Verse;
using RuMod.Utils;

namespace RuMod.Patches
{
    /// <summary>
    /// Кнопки DEV на пешках и постройках — «DEV: Start Labor», «DEV: Next trimester».
    /// Их около двух сотен, и раскиданы они по всей игре: Hediff_Pregnant, Building_Bookcase,
    /// Ability и десятки других файлов. К отладочному меню отношения не имеют,
    /// поэтому ни один из патчей DevMode их не видел.
    ///
    /// ПОЧЕМУ ИМЕННО ЗДЕСЬ. Подпись живёт в открытом поле Command.defaultLabel,
    /// а читают её Command.Label и Command.LabelCap — оба однострочники, которые
    /// Mono встраивает в вызывающий код (та же ловушка, что с NameBank.AddNames).
    /// Патч на них не сработает. Зато GizmoOnGUI большой, встраивания не будет,
    /// а поле открытое — переписываем подпись прямо перед отрисовкой.
    ///
    /// Повторный вызов безвреден: после подмены в строке кириллица,
    /// и DevModeTranslator.IsValidForTranslation её отвергает, вернув как есть.
    /// </summary>
    [HarmonyPatch(typeof(Command), nameof(Command.GizmoOnGUI))]
    public static class Command_DevGizmo_Patch
    {
        static void Prefix(Command __instance)
        {
            if (!Prefs.DevMode) return;
            string label = __instance.defaultLabel;
            // Сужаем до отладочных кнопок: обычный интерфейс игры не трогаем вовсе.
            if (label == null || label.Length < 4 || label[0] != 'D'
                || !label.StartsWith("DEV")) return;
            __instance.defaultLabel = DevModeTranslator.TranslateWithPrefix(label, "Кнопки_DEV");
        }
    }
}
