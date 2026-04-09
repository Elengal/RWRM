using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using LudeonTK;
using UnityEngine;
using Verse;

namespace RuMod.Utils
{
    /// <summary>
    /// Управляет визуальными правками окна Debug log и обработкой конфликтов с другими модами.
    /// </summary>
    public static class DebugLogTweakManager
    {
        private const string HarmonyId = "com.rumod.devtranslation";
        private const string ConflictsFolderName = "RuMod_DebugLogConflicts";

        private static bool _conflictsChecked;

        private static string GetConflictsDir()
        {
            string baseConfig = GenFilePaths.ConfigFolderPath;
            string dir = Path.Combine(baseConfig, "RimWorldRu", ConflictsFolderName);
            try
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            catch (Exception ex)
            {
                RuModLog.DebugLogConflictsDirCreateFailed(dir, ex);
            }

            return dir;
        }

        private static HashSet<string> LoadAcceptedOwners()
        {
            var accepted = new HashSet<string>();
            string dir = GetConflictsDir();
            if (!Directory.Exists(dir))
                return accepted;

            try
            {
                foreach (var file in Directory.GetFiles(dir, "owner-*.txt"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (name != null && name.StartsWith("owner-"))
                    {
                        string id = name.Substring("owner-".Length);
                        if (!string.IsNullOrEmpty(id))
                        {
                            accepted.Add(id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RuModLog.DebugLogConflictsReadFailed(ex);
            }

            return accepted;
        }

        private static void SaveAcceptedOwners(IEnumerable<string> owners)
        {
            string dir = GetConflictsDir();
            foreach (var owner in owners)
            {
                if (string.IsNullOrEmpty(owner) || owner == HarmonyId)
                    continue;
                string path = Path.Combine(dir, $"owner-{owner}.txt");
                try
                {
                    if (!File.Exists(path))
                    {
                        File.WriteAllText(path, string.Empty);
                    }
                }
                catch (Exception ex)
                {
                    RuModLog.DebugLogConflictFileWriteFailed(owner, ex);
                }
            }
        }

        private static List<string> GetCurrentOwners()
        {
            try
            {
                var method = AccessTools.Method(typeof(EditWindow_Log), "DoWindowContents", new[] { typeof(Rect) });
                if (method == null)
                    return new List<string>();

                var info = Harmony.GetPatchInfo(method);
                if (info == null)
                    return new List<string>();

                IEnumerable<string> owners = info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers)
                    .Select(p => p.owner)
                    .Where(o => !string.IsNullOrEmpty(o));

                return owners.Distinct().ToList();
            }
            catch (Exception ex)
            {
                RuModLog.DebugLogPatchesInspectFailed(ex);
                return new List<string>();
            }
        }

        /// <summary>
        /// Проверяет, можно ли применять твики к окну Debug log с учётом конфликтов.
        /// Может один раз автоматически отключить твики и записать предупреждение.
        /// </summary>
        public static bool ShouldApplyTweaks(RuModSettings settings)
        {
            if (settings == null || !settings.DebugLogTweaksEnabled)
                return false;

            if (!_conflictsChecked)
            {
                _conflictsChecked = true;

                var currentOwners = GetCurrentOwners()
                    .Where(o => o != HarmonyId)
                    .ToList();

                if (currentOwners.Count > 0)
                {
                    var accepted = LoadAcceptedOwners();
                    var newConflicts = currentOwners
                        .Where(o => !accepted.Contains(o))
                        .ToList();

                    if (newConflicts.Count > 0)
                    {
                        settings.DebugLogTweaksEnabled = false;
                        RuModClass.Instance?.WriteSettings();
                        RuModLog.DebugLogTweaksDisabledByConflicts(newConflicts);
                        return false;
                    }
                }
            }

            return settings.DebugLogTweaksEnabled;
        }

        /// <summary>
        /// Пользователь вручную включил твики при наличии конфликтов — считаем их принятыми.
        /// </summary>
        public static void AcceptCurrentConflicts()
        {
            try
            {
                var owners = GetCurrentOwners()
                    .Where(o => o != HarmonyId)
                    .ToList();
                if (owners.Count == 0)
                    return;

                SaveAcceptedOwners(owners);
                _conflictsChecked = false;
            }
            catch (Exception ex)
            {
                RuModLog.DebugLogAcceptConflictsFailed(ex);
            }
        }
    }
}

