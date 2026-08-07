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

        private enum SettingsTab { General, Visual, Dev }
        private enum TransparencyTarget { TranslationPanel, DlcPanel, DebugLog }

        private static SettingsTab _currentSettingsTab = SettingsTab.General;
        private static TransparencyTarget _currentTransparencyTarget = TransparencyTarget.TranslationPanel;
        private static Vector2 _settingsScrollPosition = Vector2.zero;
        private static float _settingsScrollHeight = 1000f; // с запасом на первый кадр, дальше считается сама

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

            // --- Шапка: табы слева, «Сбросить» справа ---
            Rect headerRect = new Rect(inRect.x, inRect.y, inRect.width, 32f);
            DrawTabBar(headerRect, settings);

            // --- Прокручиваемое тело: только выбранный таб ---
            Rect bodyOuter = new Rect(inRect.x, inRect.y + 38f, inRect.width, inRect.height - 38f);
            Rect viewRect = new Rect(0f, 0f, bodyOuter.width - 16f, _settingsScrollHeight);
            Widgets.BeginScrollView(bodyOuter, ref _settingsScrollPosition, viewRect);

            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            switch (_currentSettingsTab)
            {
                case SettingsTab.General:
                    DrawGeneralTab(listing, settings);
                    break;
                case SettingsTab.Visual:
                    DrawVisualTab(listing, settings);
                    break;
                case SettingsTab.Dev:
                    DrawDevTab(listing, settings);
                    break;
            }

            _settingsScrollHeight = listing.CurHeight;
            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawTabBar(Rect rect, RuModSettings settings)
        {
            const float resetWidth = 170f;
            Rect tabsRect = new Rect(rect.x, rect.y, rect.width - resetWidth - 8f, rect.height);
            Rect resetRect = new Rect(rect.xMax - resetWidth, rect.y, resetWidth, rect.height);

            var tabs = new (SettingsTab tab, string label)[]
            {
                (SettingsTab.General, "Общее"),
                (SettingsTab.Visual, "Визуал"),
                (SettingsTab.Dev, "Для разработчиков"),
            };

            float tabW = tabsRect.width / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                Rect tabRect = new Rect(tabsRect.x + tabW * i, tabsRect.y, tabW - 4f, tabsRect.height);
                bool selected = _currentSettingsTab == tabs[i].tab;
                GUI.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.6f);
                if (Widgets.ButtonText(tabRect, tabs[i].label))
                {
                    _currentSettingsTab = tabs[i].tab;
                }
                GUI.color = Color.white;
                if (selected)
                {
                    Widgets.DrawLineHorizontal(tabRect.x, tabRect.yMax - 2f, tabRect.width);
                }
            }

            if (Widgets.ButtonText(resetRect, "Сбросить настройки"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Сбросить все настройки RimWorld RU к значениям по умолчанию? Позиция панели переводчика и все ползунки прозрачности тоже вернутся к исходным.",
                    () =>
                    {
                        settings.ResetToDefaults();
                        WriteSettings();
                    },
                    true));
            }
        }

        private void DrawGeneralTab(Listing_Standard listing, RuModSettings settings)
        {
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
            listing.Gap(8f);

            listing.CheckboxLabeled("Патчи NameBank (русские имена)", ref settings.NameBankPatchesEnabled,
                "Включает или отключает все подмены имён: загрузка из файлов мода, выбор только русских имён, фамилии по полу, родственники, замена английских кличек. Одна галочка — всё под контролем. Отключите при конфликтах с другими модами.");

            listing.CheckboxLabeled("Снять лимит фракций при создании мира", ref settings.NoFactionLimitEnabled,
                "Убирает ограничение игры на максимум 12 видимых/добавляемых фракций в окне новой игры. Влияет только на экран выбора фракций, не зависит от DevMode.");
        }

        private void DrawVisualTab(Listing_Standard listing, RuModSettings settings)
        {
            listing.CheckboxLabeled("Перетаскиваемое окно переводчика на главном экране", ref settings.TranslationPanelDraggable,
                "Включено: окно «RimWorld переводчика» можно перетаскивать за верхнюю полоску. Позиция сохраняется между запусками игры. Выключите, если окно должно оставаться на месте.");
            listing.Gap(8f);
            listing.GapLine();
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

            if (listing.ButtonTextLabeledPct("Окно для настройки непрозрачности", targetLabel, 0.6f, TextAnchor.MiddleLeft, null, null, null))
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

            // Ползунок непрозрачности для выбранного окна.
            // Специально "непрозрачность", а не "прозрачность": значение растёт слева направо
            // и напрямую совпадает с alpha (0.1 = почти прозрачно, 0.5 = плотнее), без путаницы сторон.
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
            listing.Label($"Непрозрачность фона: {(int)(currentAlpha * 100f)}%");
            Rect sliderRect = listing.GetRect(22f);
            currentAlpha = Widgets.HorizontalSlider(sliderRect, currentAlpha, 0.1f, 0.5f, true, "", "прозрачнее", "плотнее");

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

            listing.Gap(10f);
            listing.GapLine();
            listing.Gap(4f);

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
        }

        private void DrawDevTab(Listing_Standard listing, RuModSettings settings)
        {
            listing.CheckboxLabeled("Показывать всплывающие подсказки в Dev-меню", ref settings.DevTooltipsEnabled,
                "При наведении курсора на пункт Dev-меню показывает полный текст команды во всплывающем окне. По умолчанию включено.");

            listing.CheckboxLabeled("Логировать источники имён (имя, фамилия, кличка)", ref settings.LogNameSources,
                "При создании/спавне пешки записывает в файл, откуда взято каждое имя (слот, пол, файл-источник, банк). Файл на рабочем столе: RuMod_NameSources.txt");
            if (settings.LogNameSources)
            {
                listing.Label($"Путь к файлу: {NameSourceLogger.GetFilePath()}");
            }

            listing.Gap(8f);
            listing.GapLine();
            listing.Gap(4f);

            string vacuumLabel = settings.DevModeTranslationLogging
                ? "<color=#ff5555>Пылесос английских строк (DevMode)</color>"
                : "<color=#55ff55>Пылесос английских строк (DevMode)</color>";
            listing.CheckboxLabeled(vacuumLabel, ref settings.DevModeTranslationLogging,
                "Собирает всё, что видишь в DevMode, и сохраняет английские строки в JSON. Лучше не трогать, если не занимаешься переводом мода.");
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
