using HarmonyLib;
using RimWorld.Planet;
using Verse;
using RuMod.Utils;

namespace RuMod.Patches
{
    /// <summary>
    /// Снимает лимит на максимальное число видимых/добавляемых фракций
    /// в окне создания мира (страница выбора фракций).
    ///
    /// В ваниле проверка делается в локальной функции
    /// &lt;DoWindowContents&gt;g__CanAddFaction|1,
    /// которую компилятор генерирует как метод во внутреннем классе
    /// WorldFactionsUIUtility.&lt;&gt;c__DisplayClass8_0.
    ///
    /// Harmony не находит её по типу WorldFactionsUIUtility, поэтому
    /// таргетим метод вручную через TargetMethod и AccessTools.Inner.
    /// </summary>
    [HarmonyPatch]
    public static class WorldFactionsUIUtility_Patch
    {
        /// <summary>
        /// Включена ли логика снятия лимита. Управляется настройкой мода.
        /// </summary>
        public static bool IsEnabled;

        // Явно указываем, какую функцию патчить: локальный метод
        // <DoWindowContents>g__CanAddFaction|1 во внутреннем классе <>c__DisplayClass8_0.
        static System.Reflection.MethodBase TargetMethod()
        {
            // 1) Основной способ — точное имя inner-класса и локальной функции.
            try
            {
                var displayClass = AccessTools.Inner(typeof(WorldFactionsUIUtility), "<>c__DisplayClass8_0");
                var method = AccessTools.Method(displayClass, "<DoWindowContents>g__CanAddFaction|1");
                if (method != null)
                    return method;
            }
            catch (System.Exception ex)
            {
                RuModLog.WorldFactionsPrimaryTargetLookupFailed(ex);
            }

            // 2) Fallback: ищем метод по сигнатуре bool(FactionDef) во внутренних типах WorldFactionsUIUtility.
            try
            {
                var nestedTypes = typeof(WorldFactionsUIUtility).GetNestedTypes(
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

                foreach (var t in nestedTypes)
                {
                    var methods = t.GetMethods(
                        System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.Static);

                    foreach (var m in methods)
                    {
                        if (m.ReturnType != typeof(bool))
                            continue;

                        var parameters = m.GetParameters();
                        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(RimWorld.FactionDef))
                        {
                            RuModLog.WorldFactionsFallbackMatched(t.FullName, m.Name);
                            return m;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                RuModLog.WorldFactionsFallbackTargetLookupFailed(ex);
            }

            RuModLog.WorldFactionsNoTargetFound();
            return null;
        }

        // В Postfix можно не указывать аргументы оригинального метода,
        // достаточно перехватить ref AcceptanceReport __result.
        static void Postfix(ref AcceptanceReport __result)
        {
            // Если галочка в настройках выключена — ведём себя как ванила.
            if (!IsEnabled)
                return;

            // Игнорируем внутренние ограничения (в том числе MaxVisibleFactions)
            // и всегда разрешаем добавление фракции.
            __result = AcceptanceReport.WasAccepted;
        }
    }
}

