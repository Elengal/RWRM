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
        
        // Плоский список всех переведенных значений, чтобы случайно не добавить их как ключи
        private static HashSet<string> _translatedValues = new HashSet<string>();
        
        // Быстрый поиск перевода по оригиналу (чтобы не бегать по всем категориям в рантайме)
        private static Dictionary<string, string> _quickLookup = new Dictionary<string, string>();

        // Приставки: ключи, к которым игра приклеивает значение —
        // «DEV: Spawn » + название вещи, «DEV: Nutrition: » + число.
        // Целиком такую строку в словаре не найти, поэтому ищем по началу.
        // Отбираем по последнему знаку: пробел, скобка или двоеточие.
        private static List<string> _prefixes = new List<string>();

        // Путь для чтения готовых словарей (из мода)
        private static string _dictionaryDir;
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


        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            _translatedValues.Clear();
            _quickLookup.Clear();

            if (!Directory.Exists(DictionaryDir)) return;

            try
            {
                // Читаем все .json файлы из папки DevMode
                foreach (string file in Directory.GetFiles(DictionaryDir, "*.json"))
                {
                    string json = File.ReadAllText(file);
                    
                    var fileDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (fileDict != null)
                    {
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

            _prefixes.Clear();
            foreach (var key in _quickLookup.Keys)
            {
                if (key.Length < 5) continue;
                char last = key[key.Length - 1];
                if (last == ' ' || last == '(' || last == ':')
                    _prefixes.Add(key);
            }
            // Длинные вперёд: «DEV: Fill with » должно побеждать «DEV: ».
            _prefixes.Sort((a, b) => b.Length.CompareTo(a.Length));
        }


        /// <summary>
        /// Для подписей, к которым игра приклеила значение: «DEV: Spawn Steel».
        /// Сначала пробуем целиком, потом переводим только начало, хвост оставляем.
        /// Нужен отдельный вход, потому что хвост может быть уже русским
        /// («DEV: Killed Наташа»), а такую строку Translate отвергает целиком.
        /// </summary>
        public static string TranslateWithPrefix(string original, string category = "Uncategorized")
        {
            if (string.IsNullOrEmpty(original)) return original;

            string whole = Translate(original, category);
            if (!ReferenceEquals(whole, original) && whole != original) return whole;

            if (!_loaded) Load();
            for (int i = 0; i < _prefixes.Count; i++)
            {
                string p = _prefixes[i];
                if (original.Length > p.Length && original.StartsWith(p, StringComparison.Ordinal))
                {
                    // Хвост — обычно имя дефа: «DEV: Spawn Steel». Прогоняем и его,
                    // тогда подставится подпись из базы: «DEV: Создать Сталь».
                    string tail = original.Substring(p.Length);
                    return _quickLookup[p] + Translate(tail, category);
                }
            }
            return original;
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

            // Дев-меню метит инструменты префиксом «T: », а подменю — многоточием
            // (DebugTabMenu_Actions.cs:51-76). Ищем по голой подписи: тогда один ключ
            // покрывает и «Give Birth», и «T: Give Birth», и «Give Birth...».
            // Метка вернётся на место сама — ниже стоит original.Replace(trimmed, ...).
            if (trimmed.StartsWith("T: ")) trimmed = trimmed.Substring(3);
            
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

            // 1б. В словаре нет — но это может быть имя дефа, у которого перевод
            // уже лежит в label или title (DefInjected). Незачем переводить дважды.
            string fromDef = DevModeDefLabels.TryGet(trimmed);
            if (fromDef != null)
            {
                return original.Replace(trimmed, fromDef);
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
                return original;
            }

            return original;
        }

    }
}