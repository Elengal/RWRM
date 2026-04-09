using System;
using HarmonyLib;
using LudeonTK;

namespace RuMod.Patches
{
    /// <summary>
    /// Переводит заголовки (первая строка) и значения ячеек таблиц Dev.
    /// Заголовки — через TableHeader_*, ячейки — через Defs/Keyed (части тела, группы и т.д.).
    /// </summary>
    [HarmonyPatch(typeof(Window_DebugTable), MethodType.Constructor, new Type[] { typeof(string[,]) })]
    public static class Window_DebugTable_Patch
    {
        public static void Prefix(string[,] tables)
        {
            if (tables == null)
                return;
            int cols = tables.GetLength(0);
            int rows = tables.GetLength(1);
            for (int c = 0; c < cols; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    string cell = tables[c, r];
                    if (string.IsNullOrEmpty(cell)) continue;
                    // Первая строка — заголовки, остальные — значения
                    string category = (r == 0) ? "DebugTableHeaders" : "DebugTableCells";
                    string translated = RuMod.Utils.DevModeTranslator.Translate(cell, category);
                    if (translated != cell)
                        tables[c, r] = translated;
                }
            }
        }
    }
}
