using Verse;
using RuMod.Utils;

namespace RuMod
{
    [StaticConstructorOnStartup]
    /// <summary>Подгружает словарь DevMode на старте, чтобы первый перевод не читал 35 файлов посреди кадра.</summary>
    public static class DevModeDictionaryPreload
    {
        static DevModeDictionaryPreload()
        {
            if (!Prefs.DevMode) return;

            RuModLog.DevModeFrameworkInitializing();
            
            // 1. Загружаем словарь
            DevModeTranslator.Load();

            RuModLog.DevModeFrameworkInitialized();
        }
    }
}
