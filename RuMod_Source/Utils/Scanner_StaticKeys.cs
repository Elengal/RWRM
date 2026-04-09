using System;
using System.Reflection;
using Verse;
using LudeonTK;
using RuMod.Utils;

namespace RuMod
{
    [StaticConstructorOnStartup]
    public static class Scanner_StaticKeys
    {
        static Scanner_StaticKeys()
        {
            if (!Prefs.DevMode) return;

            RuModLog.DevModeFrameworkInitializing();
            
            // 1. Загружаем словарь
            DevModeTranslator.Load();

            // 2. Сканируем атрибуты только если включен сбор ключей
            bool isLoggingEnabled = RuModClass.Instance?.GetSettings<RuModSettings>()?.DevModeTranslationLogging ?? false;
            if (isLoggingEnabled)
            {
                ScanAttributes();
                
                // 3. Сохраняем новые найденные ключи
                DevModeTranslator.Save();
            }
            
            RuModLog.DevModeFrameworkInitialized();
        }

        private static void ScanAttributes()
        {
            try
            {
                foreach (Type type in GenTypes.AllTypes)
                {
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                    {
                        var actionAttrs = method.GetCustomAttributes(typeof(DebugActionAttribute), false);
                        foreach (DebugActionAttribute attr in actionAttrs)
                        {
                            string category = string.IsNullOrWhiteSpace(attr.category) ? "Uncategorized" : attr.category;
                            if (!string.IsNullOrWhiteSpace(attr.category))
                                DevModeTranslator.RegisterOriginal(attr.category, "Categories");
                            
                            if (!string.IsNullOrWhiteSpace(attr.name))
                                DevModeTranslator.RegisterOriginal(attr.name, category);
                            else
                                DevModeTranslator.RegisterOriginal(method.Name, category);
                        }

                        var outputAttrs = method.GetCustomAttributes(typeof(DebugOutputAttribute), false);
                        foreach (DebugOutputAttribute attr in outputAttrs)
                        {
                            string category = string.IsNullOrWhiteSpace(attr.category) ? "Output" : attr.category;
                            if (!string.IsNullOrWhiteSpace(attr.category))
                                DevModeTranslator.RegisterOriginal(attr.category, "Categories");
                            
                            if (!string.IsNullOrWhiteSpace(attr.name))
                                DevModeTranslator.RegisterOriginal(attr.name, category);
                            else
                                DevModeTranslator.RegisterOriginal(method.Name, category);
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                RuModLog.DevModeScanAttributesError(ex);
            }
        }
    }
}