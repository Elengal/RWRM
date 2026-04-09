using RimWorld;
using System;
using System.Reflection;
using Verse;

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

        /// <summary>
        /// Переводит русскую фамилию из мужской формы в женскую (для родственников: игра подставляет фамилию родителя как есть).
        /// Петров → Петрова, Киселёв → Киселёва, Кузнецов → Кузнецова и т.д.
        /// </summary>
        public static string ToFemaleSurname(string maleSurname)
        {
            if (string.IsNullOrWhiteSpace(maleSurname) || !ContainsRussianCharacters(maleSurname)) return maleSurname;
            string s = maleSurname.Trim();
            if (s.Length < 2) return maleSurname;
            // -ов → -ова, -ев → -ева, -ёв → -ёва
            if (s.EndsWith("ов")) return s.Substring(0, s.Length - 2) + "ова";
            if (s.EndsWith("ев")) return s.Substring(0, s.Length - 2) + "ева";
            if (s.EndsWith("ёв")) return s.Substring(0, s.Length - 2) + "ёва";
            // -ин → -ина (Кузьмин → Кузьмина, но не трогаем уже -ина)
            if (s.EndsWith("ин") && !s.EndsWith("ина")) return s.Substring(0, s.Length - 2) + "ина";
            // -ий → -ая (редко: Толстой → Толстая)
            if (s.EndsWith("ий")) return s.Substring(0, s.Length - 2) + "ая";
            if (s.EndsWith("ый")) return s.Substring(0, s.Length - 2) + "ая";
            // -ский → -ская
            if (s.EndsWith("ский")) return s.Substring(0, s.Length - 4) + "ская";
            if (s.EndsWith("ской")) return s.Substring(0, s.Length - 4) + "ская";
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
            if (s.EndsWith("ова")) return s.Substring(0, s.Length - 3) + "ов";
            if (s.EndsWith("ева")) return s.Substring(0, s.Length - 3) + "ев";
            if (s.EndsWith("ёва")) return s.Substring(0, s.Length - 3) + "ёв";
            if (s.EndsWith("ина")) return s.Substring(0, s.Length - 3) + "ин";
            if (s.EndsWith("ая")) return s.Substring(0, s.Length - 2) + "ой"; // Толстая → Толстой
            if (s.EndsWith("ская")) return s.Substring(0, s.Length - 4) + "ский";
            return femaleSurname;
        }

        /// <summary>
        /// Проверяет, похожа ли фамилия на мужскую форму (окончания -ов, -ев, -ин и т.д.).
        /// </summary>
        public static bool LooksLikeMaleSurname(string last)
        {
            if (string.IsNullOrWhiteSpace(last) || last.Length < 2) return false;
            return last.EndsWith("ов") || last.EndsWith("ев") || last.EndsWith("ёв")
                || (last.EndsWith("ин") && !last.EndsWith("ина"))
                || last.EndsWith("ий") || last.EndsWith("ый")
                || last.EndsWith("ский") || last.EndsWith("ской");
        }

        /// <summary>
        /// Проверяет, похожа ли фамилия на женскую форму (-ова, -ева, -ина, -ая, -ская).
        /// </summary>
        public static bool LooksLikeFemaleSurname(string last)
        {
            if (string.IsNullOrWhiteSpace(last) || last.Length < 2) return false;
            return last.EndsWith("ова") || last.EndsWith("ева") || last.EndsWith("ёва")
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

        /// <summary>
        /// Заменяет английское имя на русское, если активен русский язык
        /// </summary>
        /// <param name="originalName">Оригинальное имя для замены</param>
        /// <param name="gender">Пол пешки</param>
        /// <param name="nameCategory">Категория имени</param>
        /// <param name="forcedLastName">Принудительная фамилия (опционально)</param>
        /// <param name="forceNoNick">Запретить никнейм</param>
        /// <param name="context">Контекст (используется только для совместимости, не логируется)</param>
        /// <returns>Русское имя или null, если замена не удалась</returns>
        public static NameTriple TryReplaceWithRussianName(NameTriple originalName, Gender gender, 
            PawnNameCategory nameCategory, string forcedLastName = null, bool forceNoNick = false, string context = "")
        {
            // Проверяем, активен ли русский язык
            if (LanguageDatabase.activeLanguage == null || 
                (LanguageDatabase.activeLanguage.folderName != "Russian (Русский)" && 
                 LanguageDatabase.activeLanguage.folderName != "Russian"))
            {
                return null;
            }

            // Проверяем, содержит ли имя русские символы
            bool hasRussianChars = ContainsRussianCharacters(originalName.First) || 
                                 ContainsRussianCharacters(originalName.Last) ||
                                 (originalName.Nick != null && ContainsRussianCharacters(originalName.Nick));

            // Имя и фамилия уже русские, но кличка может быть английской (напр. «Slick» из RulePack) — подменяем только кличку
            if (hasRussianChars)
            {
                bool firstOk = IsAcceptableRussianName(originalName.First);
                bool lastOk = IsAcceptableRussianName(originalName.Last);
                bool nickBad = originalName.Nick != null && (ContainsLatinCharacters(originalName.Nick) || !IsAcceptableRussianName(originalName.Nick));
                if (firstOk && lastOk && nickBad && !forceNoNick)
                {
                    NameBank nameBank = nameCategory != PawnNameCategory.NoName ? PawnNameDatabaseShuffled.BankOf(nameCategory) : null;
                    if (nameBank != null)
                    {
                        for (int i = 0; i < 15; i++)
                        {
                            Gender g = (i < 8) ? gender : (i < 12) ? (gender == Gender.Female ? Gender.Male : Gender.Female) : Gender.None;
                            string n = nameBank.GetName(PawnNameSlot.Nick, g, true);
                            if (n != null && IsAcceptableRussianName(n))
                                return new NameTriple(originalName.First, n, originalName.Last);
                        }
                    }
                }
                return null;
            }

            try
            {
                if (nameCategory != PawnNameCategory.NoName)
                {
                    // Вызываем приватный метод GeneratePawnName_Shuffled через рефлексию
                    MethodInfo method = typeof(PawnBioAndNameGenerator).GetMethod(
                        "GeneratePawnName_Shuffled", 
                        BindingFlags.NonPublic | BindingFlags.Static);
                    
                    if (method != null)
                    {
                        // Для родственников игра передаёт фамилию родителя (forcedLastName) — для женщины нужна женская форма.
                        string lastToPass = (gender == Gender.Female && !string.IsNullOrEmpty(forcedLastName) && LooksLikeMaleSurname(forcedLastName))
                            ? ToFemaleSurname(forcedLastName) : forcedLastName;

                        // Пробуем больше раз, чтобы получить русское имя с русским никнеймом
                        // Особенно важно для solid bio, где никнеймы часто остаются английскими
                        for (int attempt = 0; attempt < 10; attempt++)
                        {
                            NameTriple russianName = (NameTriple)method.Invoke(null, new object[] 
                            { 
                                nameCategory, gender, lastToPass, forceNoNick 
                            });
                            
                            // Проверяем, что ВСЕ части имени русские (First, Last, и Nick если есть)
                            if (russianName != null)
                            {
                                // Родственник: игра могла подставить фамилию родителя в мужской форме — переводим в женскую.
                                if (gender == Gender.Female && !string.IsNullOrEmpty(russianName.Last) && LooksLikeMaleSurname(russianName.Last))
                                    russianName = new NameTriple(russianName.First, russianName.Nick, ToFemaleSurname(russianName.Last));

                                bool firstIsRussian = ContainsRussianCharacters(russianName.First);
                                bool lastIsRussian = ContainsRussianCharacters(russianName.Last);
                                bool nickIsRussian = russianName.Nick == null || ContainsRussianCharacters(russianName.Nick);
                                
                                // Если имя и фамилия русские, но никнейм английский - заменяем никнейм вручную
                                if (firstIsRussian && lastIsRussian && !nickIsRussian && russianName.Nick != null)
                                {
                                    // Пытаемся получить русский никнейм из NameBank
                                    NameBank nameBank = PawnNameDatabaseShuffled.BankOf(nameCategory);
                                    
                                    // Пробуем получить русский никнейм для правильного гендера
                                    // ВАЖНО: Сначала пробуем с правильным гендером (Female -> Nick_Female, Male -> Nick_Male)
                                    string russianNick = null;
                                    for (int nickAttempt = 0; nickAttempt < 15; nickAttempt++)
                                    {
                                        Gender nickGender;
                                        if (nickAttempt < 8)
                                        {
                                            // Первые 8 попыток - используем правильный гендер
                                            nickGender = gender;
                                        }
                                        else if (nickAttempt < 12)
                                        {
                                            // Следующие 4 попытки - используем противоположный гендер (на случай ошибки)
                                            nickGender = (gender == Gender.Female) ? Gender.Male : Gender.Female;
                                        }
                                        else
                                        {
                                            // Последние попытки - Gender.None (Unisex)
                                            nickGender = Gender.None;
                                        }
                                        
                                        string testNick = nameBank.GetName(PawnNameSlot.Nick, nickGender, true);
                                        if (testNick != null && ContainsRussianCharacters(testNick))
                                        {
                                            russianNick = testNick;
                                            break;
                                        }
                                    }
                                    
                                    // Если нашли русский никнейм, заменяем
                                    if (russianNick != null)
                                    {
                                        russianName = new NameTriple(russianName.First, russianNick, russianName.Last);
                                        nickIsRussian = true;
                                    }
                                }
                                
                                if (firstIsRussian && lastIsRussian && nickIsRussian)
                                {
                                    return russianName;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Игнорируем ошибки при замене имени
            }

            return null;
        }
    }
}

