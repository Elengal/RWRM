using System.IO;
using HarmonyLib;
using Verse;
using RuMod.Utils;

namespace RuMod.Patches
{
    /// <summary>
    /// Сохранение настроек мода в Config/RimWorldRu/Config.xml вместо Mod_RimWorldRu_RuModClass.xml.
    /// При первом запуске копирует старый файл в новое место и удаляет его после проверки.
    /// </summary>
    [HarmonyPatch(typeof(LoadedModManager))]
    [HarmonyPatch("GetSettingsFilename")]
    [HarmonyPatch(new[] { typeof(string), typeof(string) })]
    public static class LoadedModManager_GetSettingsFilename_Patch
    {
        private const string ModIdentifier = "RimWorldRu";
        private const string ModHandleName = "RuModClass";
        private const string Subfolder = "RimWorldRu";
        private const string ConfigFileName = "Config.xml";

        public static bool Prefix(string modIdentifier, string modHandleName, ref string __result)
        {
            if (modIdentifier != ModIdentifier || modHandleName != ModHandleName)
                return true;

            string configDir = GenFilePaths.ConfigFolderPath;
            string dir = Path.Combine(configDir, Subfolder);
            string newPath = Path.Combine(dir, ConfigFileName);
            string oldPath = Path.Combine(configDir, GenText.SanitizeFilename($"Mod_{ModIdentifier}_{ModHandleName}.xml"));

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(newPath) && File.Exists(oldPath))
            {
                try
                {
                    File.Copy(oldPath, newPath);
                    if (File.Exists(newPath))
                    {
                        try { File.Delete(oldPath); }
                        catch (System.Exception exDel) { RuModLog.OldConfigFileCopiedButNotDeleted(exDel); }
                    }
                }
                catch (System.Exception ex)
                {
                    RuModLog.OldConfigCopyFailed(newPath, ex);
                }
            }

            __result = newPath;
            return false;
        }
    }
}
