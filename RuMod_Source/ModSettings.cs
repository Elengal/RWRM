using System.IO;
using Verse;

namespace RuMod
{
    /// <summary>
    /// Все настройки мода сохраняются в Config (RimWorld): Config\RimWorldRu\Config.xml
    /// Путь к Config: %USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\
    /// Каждое поле класса записано в ExposeData() и попадает в этот файл при сохранении.
    /// </summary>
    public class RuModSettings : ModSettings
    {
        public bool LoggingEnabled = false;
        public bool NameBankPatchesEnabled = true;
        /// <summary>Писать в файл, откуда взято имя/фамилия/кличка при создании пешки.</summary>
        public bool LogNameSources = false;
        /// <summary>Убрать лимит на количество видимых/добавляемых фракций в окне создания мира. По умолчанию включено.</summary>
        public bool NoFactionLimitEnabled = true;
        /// <summary>Показывать всплывающие подсказки в Dev-меню при наведении курсора.</summary>
        public bool DevTooltipsEnabled = true;
        /// <summary>Включает сбор непереведенных ключей DevMode в JSON-словарь. Включить сбор ключей для перевода.</summary>
        public bool DevModeTranslationLogging = false;
        /// <summary>Фон главного меню: "" = Выкл, "Default" = RimWorldRu → Случайно → Выкл, "Random" = Случайно, "UI/HeroArt/ИмяФайла" = конкретный фон.</summary>
        public string MenuBackgroundRimWorldRu = "Default";
        /// <summary>Разрешить перетаскивание окна переводчика на главном экране за верхнюю полоску. Позиция сохраняется между сессиями. По умолчанию выключено — окно в фиксированной позиции.</summary>
        public bool TranslationPanelDraggable = false;
        /// <summary>Смещение окна переводчика по X (после перетаскивания), сохраняется. Чтобы сделать сохранённую позицию стандартной: взять эти значения из Config и прибавить к PanelOffsetX/Y в патче, затем оставить дефолты 0.</summary>
        public float TranslationPanelDragOffsetX;
        /// <summary>Смещение окна переводчика по Y (после перетаскивания), сохраняется.</summary>
        public float TranslationPanelDragOffsetY;
        /// <summary>Прозрачность фона панели переводчика в главном меню (0.1–0.5). По умолчанию 0.25.</summary>
        public float TranslationPanelAlpha = 0.25f;
        /// <summary>Прозрачность фона панели DLC в главном меню (0.1–0.5). По умолчанию 0.25.</summary>
        public float DlcPanelAlpha = 0.25f;
        /// <summary>Прозрачность фона окна Debug log (0.1–0.5). По умолчанию 0.25.</summary>
        public float DebugLogAlpha = 0.25f;
        /// <summary>Включены ли визуальные правки окна Debug log (фон, ширина и т.п.).</summary>
        public bool DebugLogTweaksEnabled = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref LoggingEnabled, "LoggingEnabled", false);
            Scribe_Values.Look(ref NameBankPatchesEnabled, "NameBankPatchesEnabled", true);
            Scribe_Values.Look(ref LogNameSources, "LogNameSources", false);
            Scribe_Values.Look(ref NoFactionLimitEnabled, "NoFactionLimitEnabled", true);
            Scribe_Values.Look(ref DevTooltipsEnabled, "DevTooltipsEnabled", true);
            Scribe_Values.Look(ref DevModeTranslationLogging, "DevModeTranslationLogging", false);
            Scribe_Values.Look(ref MenuBackgroundRimWorldRu, "MenuBackgroundRimWorldRu", "Default");
            Scribe_Values.Look(ref TranslationPanelDraggable, "TranslationPanelDraggable", false);
            Scribe_Values.Look(ref TranslationPanelDragOffsetX, "TranslationPanelDragOffsetX", 0f);
            Scribe_Values.Look(ref TranslationPanelDragOffsetY, "TranslationPanelDragOffsetY", 0f);
            Scribe_Values.Look(ref TranslationPanelAlpha, "TranslationPanelAlpha", 0.25f);
            Scribe_Values.Look(ref DlcPanelAlpha, "DlcPanelAlpha", 0.25f);
            Scribe_Values.Look(ref DebugLogAlpha, "DebugLogAlpha", 0.25f);
            Scribe_Values.Look(ref DebugLogTweaksEnabled, "DebugLogTweaksEnabled", true);
        }
    }
}
