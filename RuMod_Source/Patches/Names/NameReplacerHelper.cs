using RimWorld;
using System.Collections.Generic;
using System;
using System.Reflection;
using Verse;
using RuMod.Utils;

namespace RuMod.Patches
{
    /// <summary>
    /// Вспомогательный класс для замены английских имён на русские
    /// </summary>
    public static class NameReplacerHelper
    {
        /// <summary>
        /// Проверяет, содержит ли строка русские символы (кириллицу).
        /// </summary>
        public static bool ContainsRussianCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (char c in text)
            {
                if ((c >= 'А' && c <= 'Я') || (c >= 'а' && c <= 'я') || c == 'Ё' || c == 'ё')
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Проверяет, есть ли в строке латинские (английские) буквы.
        /// Нужно, чтобы в банк и в выдачу не попадали имена с английскими буквами.
        /// </summary>
        public static bool ContainsLatinCharacters(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Имя подходит для русского банка: есть кириллица и нет латиницы (никаких английских букв).
        /// </summary>
        public static bool IsAcceptableRussianName(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            return ContainsRussianCharacters(text) && !ContainsLatinCharacters(text);
        }

        // ==== Таблица пар: мужская и женская формы фамилии ====
        //
        // Словники мода лежат парами строка в строку: Last_Male_Core против
        // Last_Female_Core, Imperial_Last_Male_Core против Imperial_Last_Female_Core.
        // Это готовый и точный ответ, который правила вывести не могут.
        //
        // Правила ниже написаны под русские фамилии на -ов/-ев/-ёв/-ин и на них
        // безупречны: 83 из 83. Но имперские фамилии византийские, и 41 из 55
        // не имеет русского окончания вовсе — Диоген, Гаврас, Апион, Склир, Ватац.
        // Вывести из них женскую форму нельзя ничем, кроме таблицы: правила
        // возвращали такие фамилии неизменными, и дочь Диогена звалась «Диоген».
        //
        // Поэтому сначала смотрим в таблицу, и только потом — в правила.
        // Правила остаются для фамилий из чужих модов и для принудительных.

        private static readonly string[][] pairedFiles =
        {
            new[] { "Names/Last_Male",          "Names/Last_Female" },
            new[] { "Names/Imperial_Last_Male", "Names/Imperial_Last_Female" },
        };

        private static Dictionary<string, string> maleToFemale;
        private static Dictionary<string, string> femaleToMale;
        private static LoadedLanguage pairsLang;

        /// <summary>Сменился язык или настройки — таблицу собрать заново.</summary>
        public static void ResetPairs()
        {
            maleToFemale = null;
            femaleToMale = null;
            pairsLang = null;
        }

        private static void EnsurePairs()
        {
            var lang = LanguageDatabase.activeLanguage;
            if (maleToFemale != null && ReferenceEquals(lang, pairsLang)) return;

            maleToFemale = new Dictionary<string, string>();
            femaleToMale = new Dictionary<string, string>();
            pairsLang = lang;
            if (lang == null) return;

            foreach (string[] pair in pairedFiles)
            {
                var m = NameReroute.FromLang(lang, pair[0]);
                var f = NameReroute.FromLang(lang, pair[1]);
                if (m == null || f == null) continue;
                if (m.Count != f.Count)
                {
                    // Строки разъехались — соответствие по номеру строки больше не верно,
                    // такой таблице доверять нельзя, работаем по правилам.
                    RuModLog.SurnamePairsMismatch(pair[0], m.Count, f.Count);
                    continue;
                }
                for (int i = 0; i < m.Count; i++)
                {
                    if (!maleToFemale.ContainsKey(m[i])) maleToFemale[m[i]] = f[i];
                    if (!femaleToMale.ContainsKey(f[i])) femaleToMale[f[i]] = m[i];
                }
            }
        }

        /// <summary>
        /// Переводит фамилию из мужской формы в женскую. Сначала таблица пар, потом правила.
        /// Петров → Петрова, Киселёв → Киселёва, Диоген → Диогения.
        /// </summary>
        public static string ToFemaleSurname(string maleSurname)
        {
            if (string.IsNullOrWhiteSpace(maleSurname) || !ContainsRussianCharacters(maleSurname)) return maleSurname;
            string s = maleSurname.Trim();
            if (s.Length < 2) return maleSurname;

            EnsurePairs();
            string paired;
            if (maleToFemale.TryGetValue(s, out paired)) return paired;

            // -ов → -ова, -ев → -ева, -ёв → -ёва
            if (s.EndsWith("ов")) return s.Substring(0, s.Length - 2) + "ова";
            if (s.EndsWith("ев")) return s.Substring(0, s.Length - 2) + "ева";
            if (s.EndsWith("ёв")) return s.Substring(0, s.Length - 2) + "ёва";
            // -ин → -ина (Кузьмин → Кузьмина, но не трогаем уже -ина)
            if (s.EndsWith("ин") && !s.EndsWith("ина")) return s.Substring(0, s.Length - 2) + "ина";
            if (s.EndsWith("ын")) return s.Substring(0, s.Length - 2) + "ына";  // Синицын → Синицына
            // Прилагательные: -ский/-ской/-ий/-ый/-ой → -ская/-ая.
            // Длинные окончания проверяем первыми, иначе -ий перехватит -ский.
            if (s.EndsWith("ский")) return s.Substring(0, s.Length - 4) + "ская";
            if (s.EndsWith("ской")) return s.Substring(0, s.Length - 4) + "ская";
            if (s.EndsWith("ий")) return s.Substring(0, s.Length - 2) + "ая";
            if (s.EndsWith("ый")) return s.Substring(0, s.Length - 2) + "ая";
            if (s.EndsWith("ой")) return s.Substring(0, s.Length - 2) + "ая";  // Толстой → Толстая
            return maleSurname;
        }

        /// <summary>
        /// Переводит русскую фамилию из женской формы в мужскую (для сына, когда фамилию взяли у матери).
        /// Павлова → Павлов, Кузьмина → Кузьмин, Толстая → Толстой и т.д.
        /// </summary>
        public static string ToMaleSurname(string femaleSurname)
        {
            if (string.IsNullOrWhiteSpace(femaleSurname) || !ContainsRussianCharacters(femaleSurname)) return femaleSurname;
            string s = femaleSurname.Trim();
            if (s.Length < 2) return femaleSurname;

            EnsurePairs();
            string paired;
            if (femaleToMale.TryGetValue(s, out paired)) return paired;

            if (s.EndsWith("ова")) return s.Substring(0, s.Length - 3) + "ов";
            if (s.EndsWith("ева")) return s.Substring(0, s.Length - 3) + "ев";
            if (s.EndsWith("ёва")) return s.Substring(0, s.Length - 3) + "ёв";
            if (s.EndsWith("ина")) return s.Substring(0, s.Length - 3) + "ин";
            if (s.EndsWith("ына")) return s.Substring(0, s.Length - 3) + "ын";
            // -ская проверяем раньше -ая, иначе получится «Вознесенской» вместо «Вознесенский»
            if (s.EndsWith("ская")) return s.Substring(0, s.Length - 4) + "ский";
            if (s.EndsWith("ая")) return s.Substring(0, s.Length - 2) + "ой"; // Толстая → Толстой
            return femaleSurname;
        }

        /// <summary>
        /// Проверяет, похожа ли фамилия на мужскую форму (окончания -ов, -ев, -ин и т.д.).
        /// </summary>
        public static bool LooksLikeMaleSurname(string last)
        {
            if (string.IsNullOrWhiteSpace(last) || last.Length < 2) return false;
            EnsurePairs();
            if (maleToFemale.ContainsKey(last)) return true;
            return last.EndsWith("ов") || last.EndsWith("ев") || last.EndsWith("ёв")
                || (last.EndsWith("ин") && !last.EndsWith("ина")) || last.EndsWith("ын")
                || last.EndsWith("ий") || last.EndsWith("ый")
                || last.EndsWith("ский") || last.EndsWith("ской");
        }

        /// <summary>
        /// Проверяет, похожа ли фамилия на женскую форму (-ова, -ева, -ина, -ая, -ская).
        /// </summary>
        public static bool LooksLikeFemaleSurname(string last)
        {
            if (string.IsNullOrWhiteSpace(last) || last.Length < 2) return false;
            EnsurePairs();
            if (femaleToMale.ContainsKey(last)) return true;
            return last.EndsWith("ова") || last.EndsWith("ева") || last.EndsWith("ёва") || last.EndsWith("ына")
                || last.EndsWith("ина") || last.EndsWith("ая") || last.EndsWith("ская");
        }

        /// <summary>
        /// Если у пешки есть кровный родственник с фамилией — подставляет его фамилию (по полу: мужская/женская форма).
        /// Используется для новорождённых (ребёнок получает фамилию родителя) и для странников-родственников.
        /// </summary>
        public static void TryApplyFamilySurname(Pawn pawn)
        {
            if (pawn?.relations == null || !pawn.RaceProps.Humanlike) return;
            NameTriple myName = pawn.Name as NameTriple;
            if (myName == null || string.IsNullOrEmpty(myName.Last)) return;

            string familyLastName = null;
            foreach (DirectPawnRelation rel in pawn.relations.DirectRelations)
            {
                if (!rel.def.familyByBloodRelation) continue;
                NameTriple otherName = rel.otherPawn?.Name as NameTriple;
                if (otherName != null && !string.IsNullOrEmpty(otherName.Last))
                {
                    familyLastName = otherName.Last;
                    break;
                }
            }
            if (string.IsNullOrEmpty(familyLastName)) return;

            string newLast = familyLastName;
            if (pawn.gender == Gender.Female && LooksLikeMaleSurname(familyLastName))
                newLast = ToFemaleSurname(familyLastName);
            else if (pawn.gender == Gender.Male && LooksLikeFemaleSurname(familyLastName))
                newLast = ToMaleSurname(familyLastName);
            // Дополнительно: если у мальчика фамилия в женской форме — исправляем (важно когда familyLastName == myName.Last и мы иначе вышли бы раньше)
            if (pawn.gender == Gender.Male && LooksLikeFemaleSurname(newLast))
                newLast = ToMaleSurname(newLast);
            // Не перезаписываем имя без изменений (фамилия уже верная)
            if (newLast == myName.Last) return;
            pawn.Name = new NameTriple(myName.First, myName.Nick, newLast);
        }
    }
}
