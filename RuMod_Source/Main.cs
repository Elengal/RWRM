using HarmonyLib;
using LudeonTK;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;
using UnityEngine;
using RimWorld;
using RuMod.Patches;
using RuMod.Utils;

namespace RuMod
{
    public class RuModClass : Mod
    {
        public static RuModClass Instance { get; private set; }
        private enum TransparencyTarget
        {
            TranslationPanel,
            DlcPanel,
            DebugLog
        }

        private static TransparencyTarget _currentTransparencyTarget = TransparencyTarget.TranslationPanel;
        public RuModClass(ModContentPack content) : base(content)
        {
            Instance = this;
            // Патч пути к конфигу нужно применить ДО первого GetSettings, иначе игра читает
            // настройки из стандартного файла (Mod_RimWorldRu_RuModClass.xml), а пишем мы в Config\RimWorldRu\Config.xml.
            var harmony = new Harmony("com.rumod.devtranslation");
            try { harmony.PatchAll(Assembly.GetExecutingAssembly()); }
            catch (Exception ex) { RuModLog.PatchAllFailed(ex); }
            var s = GetSettings<RuModSettings>();
            NameSourceLogger.IsEnabled = s.LogNameSources;
            Patches.WorldFactionsUIUtility_Patch.IsEnabled = s.NoFactionLimitEnabled;
            Patches.Dialog_Debug_Tooltips_Patch.IsEnabled = s.DevTooltipsEnabled;

            // Остальные патчи (списки DevMode, RimHUD, PregnancyUtility)
            // 1. Пытаемся безопасно применить патч для списков
            Patch_Dialog_DebugOptionListLister(harmony);

            // 3. Патч RimHUD: склонение год/года/лет в возрасте
            Patches.RimHUD_GenderRaceAndAgeValue_Patch.Patch(harmony);

            // 4. Патч фамилии новорождённого — применяем после загрузки DefOf, иначе PregnancyUtility..cctor падает
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                try
                {
                    var type = AccessTools.TypeByName("RimWorld.PregnancyUtility");
                    var method = type?.GetMethod("ApplyBirthOutcome", BindingFlags.Public | BindingFlags.Static);
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(Patches.PregnancyUtility_ApplyBirthOutcome_Patch).GetMethod("Postfix"));
                        harmony.Patch(method, postfix: postfix);
                    }
                }
                catch (Exception ex2)
                {
                    RuModLog.PregnancyPatchFailed(ex2);
                }
            });
        }

        void Patch_Dialog_DebugOptionListLister(Harmony harmony)
        {
            try 
            {
                // Ищем класс Dialog_DebugOptionListLister
                // Сначала пробуем по полному имени в LudeonTK
                Type type = AccessTools.TypeByName("LudeonTK.Dialog_DebugOptionListLister");
                
                // Если не найден, ищем во всех типах (на случай изменения namespace)
                if (type == null) 
                {
                    type = AccessTools.AllTypes().FirstOrDefault(t => t.Name == "Dialog_DebugOptionListLister");
                }
                
                if (type == null) 
                {
                    RuModLog.DialogDebugOptionClassNotFound();
                    return;
                }
                
                // Ищем конструктор: (IEnumerable<DebugMenuOption>, string)
                ConstructorInfo ctor = null;
                
                // Пробуем явный поиск
                try 
                {
                    ctor = AccessTools.Constructor(type, new Type[] { typeof(IEnumerable<DebugMenuOption>), typeof(string) });
                }
                catch {}

                // Если явный поиск не сработал (например, типы изменились), ищем более гибко
                if (ctor == null)
                {
                    var ctors = AccessTools.GetDeclaredConstructors(type);
                    foreach (var c in ctors)
                    {
                        var p = c.GetParameters();
                        if (p.Length >= 1 && p[0].ParameterType.Name.Contains("DebugMenuOption"))
                        {
                            ctor = c;
                            break;
                        }
                    }
                }

                if (ctor != null)
                {
                    var prefix = new HarmonyMethod(typeof(RuMod.Patches.Debug.Dialog_DebugOptionListLister_Patch).GetMethod("Prefix"));
                    harmony.Patch(ctor, prefix: prefix);
                    RuModLog.DialogDebugOptionPatched();
                }
                else
                {
                    RuModLog.DialogDebugOptionCtorNotFound();
                }
            }
            catch (Exception ex)
            {
                RuModLog.DialogDebugOptionManualPatchFailed(ex);
            }
        }

        public override string SettingsCategory() => "RimWorld RU";

        public override void WriteSettings()
        {
            base.WriteSettings();
            var s = GetSettings<RuModSettings>();
            NameSourceLogger.IsEnabled = s.LogNameSources;
            Patches.WorldFactionsUIUtility_Patch.IsEnabled = s.NoFactionLimitEnabled;
            Patches.Dialog_Debug_Tooltips_Patch.IsEnabled = s.DevTooltipsEnabled;
            MainMenuDrawer_Init_Patch.ApplyRimWorldRuBackground();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var settings = GetSettings<RuModSettings>();
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            string menuBgLabel = GetMenuBackgroundLabel(settings.MenuBackgroundRimWorldRu);
            if (listing.ButtonTextLabeledPct("Фон главного меню", menuBgLabel, 0.6f, TextAnchor.MiddleLeft, null, null, null))
            {
                var opts = new List<FloatMenuOption> { new FloatMenuOption("Выкл", () => { settings.MenuBackgroundRimWorldRu = ""; MainMenuDrawer_Init_Patch.ApplyRimWorldRuBackground(); }) };
                foreach (string contentPath in MainMenuDrawer_Init_Patch.GetRimWorldRuMenuContentPaths())
                {
                    string path = contentPath;
                    string label = MainMenuDrawer_Init_Patch.GetDisplayNameFromContentPath(path);
                    opts.Add(new FloatMenuOption(label, () => { settings.MenuBackgroundRimWorldRu = path; MainMenuDrawer_Init_Patch.ApplyRimWorldRuBackground(); }));
                }
                opts.Add(new FloatMenuOption("Случайно", () => { settings.MenuBackgroundRimWorldRu = "Random"; MainMenuDrawer_Init_Patch.ApplyRimWorldRuBackground(); }));
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            listing.Gap(4f);

            listing.CheckboxLabeled("Патчи NameBank (русские имена)", ref settings.NameBankPatchesEnabled,
                "Включает или отключает все подмены имён: загрузка из файлов мода, выбор только русских имён, фамилии по полу, родственники, замена английских кличек. Одна галочка — всё под контролем. Отключите при конфликтах с другими модами.");

            listing.CheckboxLabeled("Снять лимит фракций при создании мира", ref settings.NoFactionLimitEnabled,
                "Убирает ограничение игры на максимум 12 видимых/добавляемых фракций в окне новой игры. Влияет только на экран выбора фракций, не зависит от DevMode.");

            listing.GapLine();
            listing.Label("Для разработчиков");
            listing.Gap(4f);
            listing.CheckboxLabeled("Перетаскиваемое окно переводчика на главном экране", ref settings.TranslationPanelDraggable,
                "Включено: окно «RimWorld переводчика» можно перетаскивать за верхнюю полоску. Позиция сохраняется между запусками игры. Выключите, если окно должно оставаться на месте.");

            string vacuumLabel = settings.DevModeTranslationLogging
                ? "<color=#ff5555>Пылесос</color>"
                : "<color=#55ff55>Пылесос</color>";
            listing.CheckboxLabeled(vacuumLabel, ref settings.DevModeTranslationLogging,
                "Пылесос всего, что видишь в DevMode, и сохраняет английские строки в JSON. Лучше не трогать, если не собираешься заниматься переводом разработчика.");
            listing.CheckboxLabeled("Показывать всплывающие подсказки в Dev-меню", ref settings.DevTooltipsEnabled,
                "При наведении курсора на пункт Dev-меню показывает полный текст команды во всплывающем окне. По умолчанию включено.");
            listing.CheckboxLabeled("Логировать источники имён (имя, фамилия, кличка)", ref settings.LogNameSources,
                "При создании/спавне пешки записывает в файл, откуда взято каждое имя (слот, пол, файл-источник, банк). Файл на рабочем столе: RuMod_NameSources.txt");
            if (settings.LogNameSources)
            {
                listing.Label($"Путь к файлу: {NameSourceLogger.GetFilePath()}");
            }

            listing.GapLine();
            listing.Label("Визуальные настройки");
            listing.Gap(4f);

            // Выбор окна для настройки прозрачности
            if (!Prefs.DevMode && _currentTransparencyTarget == TransparencyTarget.DebugLog)
            {
                _currentTransparencyTarget = TransparencyTarget.TranslationPanel;
            }

            string targetLabel = _currentTransparencyTarget switch
            {
                TransparencyTarget.TranslationPanel => "Панель переводчика",
                TransparencyTarget.DlcPanel => "Панель DLC",
                TransparencyTarget.DebugLog => "Окно Debug log",
                _ => "Панель переводчика"
            };

            if (listing.ButtonTextLabeledPct("Окно для настройки прозрачности", targetLabel, 0.6f, TextAnchor.MiddleLeft, null, null, null))
            {
                var opts = new List<FloatMenuOption>
                {
                    new FloatMenuOption("Панель переводчика", () => _currentTransparencyTarget = TransparencyTarget.TranslationPanel),
                    new FloatMenuOption("Панель DLC", () => _currentTransparencyTarget = TransparencyTarget.DlcPanel)
                };
                if (Prefs.DevMode)
                {
                    opts.Add(new FloatMenuOption("Окно Debug log", () => _currentTransparencyTarget = TransparencyTarget.DebugLog));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }

            // Ползунок прозрачности для выбранного окна
            float currentAlpha = 0.25f;
            switch (_currentTransparencyTarget)
            {
                case TransparencyTarget.TranslationPanel:
                    currentAlpha = settings.TranslationPanelAlpha;
                    break;
                case TransparencyTarget.DlcPanel:
                    currentAlpha = settings.DlcPanelAlpha;
                    break;
                case TransparencyTarget.DebugLog:
                    currentAlpha = settings.DebugLogAlpha;
                    break;
            }

            currentAlpha = Mathf.Clamp(currentAlpha, 0.1f, 0.5f);
            listing.Label($"Прозрачность: {(int)(currentAlpha * 100f)}%");
            Rect sliderRect = listing.GetRect(22f);
            currentAlpha = Widgets.HorizontalSlider(sliderRect, currentAlpha, 0.1f, 0.5f, true, "", "10%", "50%");

            switch (_currentTransparencyTarget)
            {
                case TransparencyTarget.TranslationPanel:
                    settings.TranslationPanelAlpha = currentAlpha;
                    break;
                case TransparencyTarget.DlcPanel:
                    settings.DlcPanelAlpha = currentAlpha;
                    break;
                case TransparencyTarget.DebugLog:
                    settings.DebugLogAlpha = currentAlpha;
                    break;
            }

            listing.Gap(6f);

            bool debugTweaks = settings.DebugLogTweaksEnabled;
            listing.CheckboxLabeled("Визуальные правки окна Debug log (фон, ширина)",
                ref debugTweaks,
                "Делает фон окна Debug log полупрозрачным и может подстраивать его под русские подписи. При конфликтах с другими модами RuMod может автоматически отключить эту опцию.");
            if (debugTweaks != settings.DebugLogTweaksEnabled)
            {
                settings.DebugLogTweaksEnabled = debugTweaks;
                if (debugTweaks)
                {
                    RuMod.Utils.DebugLogTweakManager.AcceptCurrentConflicts();
                }
            }

            listing.End();
        }

        private static string GetMenuBackgroundLabel(string choice)
        {
            if (string.IsNullOrEmpty(choice)) return "Выкл";
            if (choice == "Random") return "Случайно";
            if (choice == "Default") return MainMenuDrawer_Init_Patch.GetDefaultChoiceDisplayName();
            return MainMenuDrawer_Init_Patch.GetDisplayNameFromContentPath(choice);
        }
    }
}
