using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Verse;
using System.Linq;

namespace RuMod.Utils
{
    public static class DevModeTranslator
    {
        // Теперь словарь хранит структуру: Категория -> (Оригинал -> Перевод)
        private static Dictionary<string, Dictionary<string, string>> _translations = new Dictionary<string, Dictionary<string, string>>();
        
        // Плоский список всех переведенных значений, чтобы случайно не добавить их как ключи
        private static HashSet<string> _translatedValues = new HashSet<string>();
        
        // Быстрый поиск перевода по оригиналу (чтобы не бегать по всем категориям в рантайме)
        private static Dictionary<string, string> _quickLookup = new Dictionary<string, string>();

        // Путь для чтения готовых словарей (из мода)
        private static string _dictionaryDir;
        // Путь для записи новых ключей «пылесосом» (на рабочий стол / в Config)
        private static string _outputDir;
        private static bool _dirty = false;
        private static bool _loaded = false;

        /// <summary>
        /// Папка с готовыми JSON-словарами для DevMode внутри мода.
        /// Отсюда читаем переводы, которые уже перенесены в мод.
        /// </summary>
        public static string DictionaryDir
        {
            get
            {
                if (_dictionaryDir != null) return _dictionaryDir;

                string modRoot = RuModClass.Instance.Content.RootDir;
                // Внутри мода храним готовые JSON в отдельной папке DevModeJson,
                // чтобы не путать её с любой другой "DevMode" из перевода.
                _dictionaryDir = Path.Combine(modRoot, "1.6", "DevMod", "Languages", "Russian (Русский)", "DevModeJson");

                try
                {
                    if (!Directory.Exists(_dictionaryDir))
                    {
                        Directory.CreateDirectory(_dictionaryDir);
                    }
                }
                catch (Exception ex)
                {
                    RuModLog.DevModeModDirCreateFailed(_dictionaryDir, ex);
                }

                return _dictionaryDir;
            }
        }

        /// <summary>
        /// Папка, куда пылесос сохраняет новые ключи (для редактирования).
        /// </summary>
        public static string OutputDir
        {
            get
            {
                if (_outputDir != null) return _outputDir;

                try
                {
                    // Основной вариант: рабочий стол пользователя
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    _outputDir = Path.Combine(desktop, "RimWorld_DevMode_Translations");

                    if (!Directory.Exists(_outputDir))
                    {
                        Directory.CreateDirectory(_outputDir);
                    }
                }
                catch (Exception ex)
                {
                    RuModLog.DevModeDesktopDirCreateFailed(_outputDir ?? "<Desktop>", ex);
                    // Фолбэк: Config\RimWorldRu\DevMode_Translations
                    string baseConfig = GenFilePaths.ConfigFolderPath;
                    _outputDir = Path.Combine(baseConfig, "RimWorldRu", "DevMode_Translations");
                    try
                    {
                        if (!Directory.Exists(_outputDir))
                        {
                            Directory.CreateDirectory(_outputDir);
                        }
                    }
                    catch (Exception ex2)
                    {
                        RuModLog.DevModeFallbackDirCreateFailed(_outputDir, ex2);
                    }
                }

                return _outputDir;
            }
        }

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            _translations.Clear();
            _translatedValues.Clear();
            _quickLookup.Clear();

            if (!Directory.Exists(DictionaryDir)) return;

            try
            {
                // Читаем все .json файлы из папки DevMode
                foreach (string file in Directory.GetFiles(DictionaryDir, "*.json"))
                {
                    string category = Path.GetFileNameWithoutExtension(file);
                    string json = File.ReadAllText(file);
                    
                    var fileDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (fileDict != null)
                    {
                        _translations[category] = fileDict;
                        
                        foreach (var kvp in fileDict)
                        {
                            if (!string.IsNullOrWhiteSpace(kvp.Value) && kvp.Value != kvp.Key)
                            {
                                _translatedValues.Add(kvp.Value);
                                // Сохраняем в плоский быстрый словарь для мгновенного доступа в игре
                                _quickLookup[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RuModLog.DevModeDictionariesLoadFailed(ex);
            }
        }

        public static void Save()
        {
            if (!_dirty) return;

            try
            {
                foreach (var kvp in _translations)
                {
                    string category = kvp.Key;
                    var dict = kvp.Value;
                    
                    // Не сохраняем пустые категории
                    if (dict == null || dict.Count == 0) continue;

                    // Сортируем словарь по алфавиту для красоты и удобства
                    var sortedDict = dict.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);

                    string filePath = Path.Combine(OutputDir, $"{category}.json");
                    string json = JsonConvert.SerializeObject(sortedDict, Formatting.Indented);
                    File.WriteAllText(filePath, json);
                }
                
                _dirty = false;
            }
            catch (Exception ex)
            {
                RuModLog.DevModeDictionariesSaveFailed(ex);
            }
        }

        private static bool IsValidForTranslation(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (text.Length <= 1) return false;

            bool hasLetters = false;
            foreach (char c in text)
            {
                if (char.IsLetter(c))
                {
                    hasLetters = true;
                    break;
                }
            }
            if (!hasLetters) return false;

            // Если строка уже содержит русские буквы — это уже перевод, не трогаем её и не логируем
            foreach (char c in text)
            {
                if ((c >= 'А' && c <= 'я') || c == 'Ё' || c == 'ё')
                {
                    return false;
                }
            }

            // Игнорируем логи (начинаются с квадратной скобки с цифрами [22:38:57])
            if (text.StartsWith("[") && text.Length > 5 && char.IsDigit(text[1])) return false;

            // Игнорируем строки с явными путями к файлам/ресурсам, но не режем по "System."
            if (text.Contains(":\\") || text.Contains(":/")) return false;
            if (text.StartsWith("0x") || text.StartsWith("<")) return false;

            return true;
        }

        // Регулярное выражение для поиска чисел (целых и с плавающей точкой, включая знаки + и -)
        private static readonly Regex _numberRegex = new Regex(@"[-+]?\d+(\.\d+)?");

        // Выбор русской формы существительного по числу: 1 день, 2 дня, 5 дней
        private static string SelectRussianPlural(int number, string form1, string form2, string form5)
        {
            int n = Math.Abs(number) % 100;
            int n1 = n % 10;
            if (n > 10 && n < 20) return form5;
            if (n1 > 1 && n1 < 5) return form2;
            if (n1 == 1) return form1;
            return form5;
        }

        // Специальная поддержка шаблонов с днями/годами для корректного склонения
        private static object[] BuildTemplateArgs(string template, List<string> numbers)
        {
            // По умолчанию просто передаём все числа как есть
            object[] defaultArgs = numbers.Cast<object>().ToArray();

            if (numbers.Count == 0)
                return defaultArgs;

            // Пытаемся распарсить первое число для склонения
            if (!int.TryParse(numbers[0], out int n))
                return defaultArgs;

            // День/дни/дней
            if (template == "T: Grow plant {0} day"
                || template == "T: Make {0} day older"
                || template == "T: Rot {0} day"
                || template == "Adaption Progress{0} Days")
            {
                string dayWord = SelectRussianPlural(n, "день", "дня", "дней");
                return new object[] { numbers[0], dayWord };
            }

            // Год/года/лет
            if (template == "T: Make {0} year older")
            {
                string yearWord = SelectRussianPlural(n, "год", "года", "лет");
                return new object[] { numbers[0], yearWord };
            }

            return defaultArgs;
        }

        public static string Translate(string original, string category = "Uncategorized")
        {
            if (!IsValidForTranslation(original)) return original;
            if (!_loaded) Load();

            // Извлекаем чистый текст без пробелов по краям и точек
            string trimmed = original.Trim().TrimEnd('.');
            
            // 1. Точное совпадение
            if (_quickLookup.TryGetValue(trimmed, out string translation))
            {
                return original.Replace(trimmed, translation);
            }

            if (_quickLookup.TryGetValue(original, out translation))
            {
                return translation;
            }

            if (_translatedValues.Contains(trimmed) || _translatedValues.Contains(original))
            {
                return original;
            }

            // 2. Шаблонный поиск (если в строке есть числа)
            MatchCollection matches = _numberRegex.Matches(trimmed);
            if (matches.Count > 0)
            {
                List<string> numbers = new List<string>();
                int index = 0;
                string template = _numberRegex.Replace(trimmed, m =>
                {
                    numbers.Add(m.Value);
                    return "{" + (index++) + "}";
                });

                // Ищем шаблон (например, "{0} points")
                    if (_quickLookup.TryGetValue(template, out string templateTranslation))
                {
                    try
                    {
                            // Подставляем числа (и, при необходимости, склонения) в переведенный шаблон
                            object[] args = BuildTemplateArgs(template, numbers);
                            string formatted = string.Format(templateTranslation, args);
                        return original.Replace(trimmed, formatted);
                    }
                    catch
                    {
                        // В случае ошибки формата игнорируем и выводим оригинал
                    }
                }
                else
                {
                    // Если шаблона нет, добавляем его в словарь (создаем категорию Templates)
                    bool isLoggingTemplate = RuModClass.Instance?.GetSettings<RuModSettings>()?.DevModeTranslationLogging ?? false;
                    if (isLoggingTemplate)
                    {
                        RegisterOriginal(template, "Templates");
                    }
                    return original;
                }
            }

            // 3. Если перевода нет, нет чисел, и включен логгер - добавляем в указанную категорию
            bool isLoggingEnabled = RuModClass.Instance?.GetSettings<RuModSettings>()?.DevModeTranslationLogging ?? false;
            if (isLoggingEnabled)
            {
                RegisterOriginal(trimmed, category);
            }

            return original;
        }

        /// <summary>
        /// Регистрирует строку в определенной категории.
        /// Если строка уже существует в ЛЮБОЙ категории, она не будет добавлена снова.
        /// </summary>
        public static void RegisterOriginal(string original, string category = "Uncategorized")
        {
            if (!IsValidForTranslation(original)) return;

            bool isLoggingEnabled = RuModClass.Instance?.GetSettings<RuModSettings>()?.DevModeTranslationLogging ?? false;
            if (!isLoggingEnabled) return;
            if (!_loaded) Load();

            if (string.IsNullOrEmpty(category)) category = "Uncategorized";

            // Очищаем имя категории для файла (чтобы не было запрещенных символов типа / \ : * ? " < > |)
            category = string.Join("_", category.Split(Path.GetInvalidFileNameChars()));

            // Проверяем, есть ли этот ключ вообще хоть где-нибудь
            bool existsAnywhere = false;
            foreach (var dict in _translations.Values)
            {
                if (dict.ContainsKey(original))
                {
                    existsAnywhere = true;
                    break;
                }
            }

            if (!existsAnywhere)
            {
                if (!_translations.ContainsKey(category))
                {
                    _translations[category] = new Dictionary<string, string>();
                }
                _translations[category][original] = "";
                // Чтобы в текущей сессии повторные обращения к той же строке
                // сразу находили её и не пытались регистрировать снова, кладём
                // оригинал в быстрый словарь как "перевод по умолчанию".
                _quickLookup[original] = original;
                _dirty = true;
            }
        }
    }
}