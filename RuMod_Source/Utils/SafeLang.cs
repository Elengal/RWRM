using System;
using Verse;
using RuMod.Utils;

namespace RuMod.Utils
{
    /// <summary>
    /// Безопасные вызовы переводов с защитой от FormatException.
    /// При ошибке формата возвращается перевод без аргументов.
    /// </summary>
    public static class SafeLang
    {
        /// <summary>
        /// Получить перевод по ключу (без аргументов).
        /// </summary>
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            return key.TranslateSimple();
        }

        /// <summary>
        /// Получить перевод по ключу с подстановкой аргументов.
        /// При FormatException возвращает перевод без аргументов и логирует предупреждение.
        /// </summary>
        /// <param name="key">Ключ перевода</param>
        /// <param name="args">Аргументы для string.Format</param>
        /// <returns>Отформатированная строка или ключ без аргументов при ошибке</returns>
        public static string Get(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key)) return key;
            if (args == null || args.Length == 0) return key.TranslateSimple();

            try
            {
                return string.Format(key.TranslateSimple(), args);
            }
            catch (FormatException ex)
            {
                RuModLog.FormatExceptionInKey(key, ex);
                return key.TranslateSimple();
            }
        }

        /// <summary>
        /// Проверить наличие ключа перевода.
        /// </summary>
        public static bool HasKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return LanguageDatabase.activeLanguage != null &&
                   LanguageDatabase.activeLanguage.HaveTextForKey(key);
        }
    }
}
