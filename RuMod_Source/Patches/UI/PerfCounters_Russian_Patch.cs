using HarmonyLib;
using RimWorld;
using Verse;

namespace RuMod.Patches
{
    /// <summary>
    /// Русификация оверлея FPS/TPS в правом верхнем углу.
    /// Заменяет строки "FPS: ... (..ms/frame)" и "TPS: ... (..ms/tick)" на русские варианты.
    /// Работает только когда активен русский язык и включён мод RuMod.
    /// </summary>
    [HarmonyPatch]
    public static class PerfCounters_Russian_Patch
    {
        private static bool ShouldUseRussian()
        {
            var lang = LanguageDatabase.activeLanguage;
            if (lang == null) return false;

            // Папка стандарта: "Russian (Русский)" и т.п.
            return lang.folderName != null && lang.folderName.Contains("Russian");
        }

        [HarmonyPatch(typeof(GlobalControlsUtility), nameof(GlobalControlsUtility.DrawFpsCounter))]
        [HarmonyPrefix]
        public static bool DrawFpsCounter_Prefix(float leftX, float width, ref float curBaseY)
        {
            if (!ShouldUseRussian())
            {
                return true; // дать отработать оригиналу
            }

            float averageFrameTime = Root.AverageFrameTime;
            float fps = 1000f / averageFrameTime;

            var rect = new UnityEngine.Rect(leftX, curBaseY - 26f, width - 7f, 26f);
            var oldAnchor = Text.Anchor;
            Text.Anchor = UnityEngine.TextAnchor.MiddleRight;
            Widgets.Label(rect, string.Format("FPS: {0:F1} ({1:F2} мс/кадр)", fps, averageFrameTime));
            Text.Anchor = oldAnchor;
            curBaseY -= 26f;

            return false; // оригинальный метод не вызываем
        }

        [HarmonyPatch(typeof(GlobalControlsUtility), nameof(GlobalControlsUtility.DrawTpsCounter))]
        [HarmonyPrefix]
        public static bool DrawTpsCounter_Prefix(float leftX, float width, ref float curBaseY)
        {
            if (!ShouldUseRussian())
            {
                return true;
            }

            float meanTickTime = Find.TickManager.MeanTickTime;
            float tps = 1000f / meanTickTime;
            float maxTps = 60f * Find.TickManager.TickRateMultiplier;
            tps = UnityEngine.Mathf.Min(tps, maxTps);

            var rect = new UnityEngine.Rect(leftX, curBaseY - 26f, width - 7f, 26f);
            var oldAnchor = Text.Anchor;
            Text.Anchor = UnityEngine.TextAnchor.MiddleRight;
            Widgets.Label(rect, string.Format("TPS: {0:F1} ({1:F2} мс/тик)", tps, meanTickTime));
            Text.Anchor = oldAnchor;
            curBaseY -= 26f;

            return false;
        }
    }
}

