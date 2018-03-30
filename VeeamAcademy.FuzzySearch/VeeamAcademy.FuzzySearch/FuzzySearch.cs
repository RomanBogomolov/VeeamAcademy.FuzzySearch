using System;
using System.Collections.Generic;
using System.Linq;

namespace VeeamAcademy.FuzzySearch
{
    public class CFuzzySearch
    {
        public CFuzzySearch()
        {
            Samples = new List<CLanguageSet>();
        }

        private class CWord
        {
            public CWord()
            {
                Codes = new List<int>();
            }

            public string Text { get; set; }
            public List<int> Codes { get; set; }
        }

        private class CAnalizeObject
        {
            public CAnalizeObject()
            {
                Words = new List<CWord>();
            }

            public string Origianl { get; set; }
            public List<CWord> Words { get; set; }
        }

        private class CLanguageSet
        {
            public CLanguageSet()
            {
                Rus = new CAnalizeObject();
                Eng = new CAnalizeObject();
            }

            public CAnalizeObject Rus { get; } 
            public CAnalizeObject Eng { get; }
        }

        private List<CLanguageSet> Samples { get; set; }

        public void SetData(List<Tuple<string, string>> datas)
        {
            var CodeKeys = s_codeKeysRus.Concat(s_codeKeysEng).ToList();
            foreach (var Data in datas)
            {
                var LanguageSet = new CLanguageSet();
                LanguageSet.Rus.Origianl = Data.Item1;
                if (Data.Item1.Length > 0)
                    LanguageSet.Rus.Words = Data.Item1.Split(' ').Select(w => new CWord()
                    {
                        Text = w.ToLower(),
                        Codes = GetKeyCodes(CodeKeys, w)
                    }).ToList();
                LanguageSet.Eng.Origianl = Data.Item2;
                if (Data.Item2.Length > 0)
                    LanguageSet.Eng.Words = Data.Item2.Split(' ').Select(w => new CWord()
                    {
                        Text = w.ToLower(),
                        Codes = GetKeyCodes(CodeKeys, w)
                    }).ToList();
                Samples.Add(LanguageSet);
            }
        }
        public List<Tuple<string, string, double, int>> Search(string targetStr)
        {
            var CodeKeys = s_codeKeysRus.Concat(s_codeKeysEng).ToList();
            var OriginalSearchObj = new CAnalizeObject();
            if (targetStr.Length > 0)
                OriginalSearchObj.Words = targetStr.Split(' ').Select(w => new CWord()
                {
                    Text = w.ToLower(),
                    Codes = GetKeyCodes(CodeKeys, w)
                }).ToList();
            var TranslationSearchObj = new CAnalizeObject();
            if (targetStr.Length > 0)
                TranslationSearchObj.Words = targetStr.Split(' ').Select(w => {
                    var TranslateStr = Transliterate(w.ToLower(), s_translitRuEn);
                    return new CWord()
                    {
                        Text = TranslateStr,
                        Codes = GetKeyCodes(CodeKeys, TranslateStr)
                    };
                }).ToList();
            var Result = new List<Tuple<string, string, double, int>>();
            foreach (var Sampl in Samples)
            {
                var LanguageType = 1;
                var Cost = GetRangePhrase(Sampl.Rus, OriginalSearchObj, false);
                var TempCost = GetRangePhrase(Sampl.Eng, OriginalSearchObj, false);
                if (Cost > TempCost)
                {
                    Cost = TempCost;
                    LanguageType = 3;
                }

                TempCost = GetRangePhrase(Sampl.Rus, TranslationSearchObj, true);
                if (Cost > TempCost)
                {
                    Cost = TempCost;
                    LanguageType = 2;
                }
                TempCost = GetRangePhrase(Sampl.Eng, TranslationSearchObj, true);
                if (Cost > TempCost)
                {
                    Cost = TempCost;
                    LanguageType = 3;
                }
                Result.Add(new Tuple<string, string, double, int>(Sampl.Rus.Origianl, Sampl.Eng.Origianl, Cost, LanguageType));
            }
            return Result;
        }
        private double GetRangePhrase(CAnalizeObject source, CAnalizeObject search, bool translation)
        {
            if (!source.Words.Any())
            {
                if (!search.Words.Any())
                    return 0;
                return search.Words.Sum(w => w.Text.Length) * 2 * 100;
            }
            if (!search.Words.Any())
                return source.Words.Sum(w => w.Text.Length) * 2 * 100;
            double Result = 0;
            for (var I = 0; I < search.Words.Count; I++)
            {
                var MinRangeWord = double.MaxValue;
                var MinIndex = 0;
                for (var J = 0; J < source.Words.Count; J++)
                {
                    var CurrentRangeWord = GetRangeWord(source.Words[J], search.Words[I], translation);
                    if (CurrentRangeWord < MinRangeWord)
                    {
                        MinRangeWord = CurrentRangeWord;
                        MinIndex = J;
                    }
                }
                //TODO вычислить коэффициент (100)
                Result += MinRangeWord * 100 + Math.Abs(I - MinIndex) / 10.0;
            }
            return Result;
        }
        private double GetRangeWord(CWord source, CWord target, bool translation)
        {
            var MinDistance = double.MaxValue;
            var CroppedSource = new CWord();
            var Length = Math.Min(source.Text.Length, target.Text.Length + 1);
            for (var I = 0; I <= source.Text.Length - Length; I++)
            {
                CroppedSource.Text = source.Text.Substring(I, Length);
                CroppedSource.Codes = source.Codes.Skip(I).Take(Length).ToList();
                //TODO Подобрать коэффициент, в зависимости от размера справочника (10)
                MinDistance = Math.Min(MinDistance, LevenshteinDistance(CroppedSource, target, CroppedSource.Text.Length == source.Text.Length, translation) + I * 2 / 10.0);
            }
            return MinDistance;
        }

        private int LevenshteinDistance(CWord source, CWord target, bool fullWord, bool translation)
        {
            if (string.IsNullOrEmpty(source.Text))
            {
                if (string.IsNullOrEmpty(target.Text))
                    return 0;
                return target.Text.Length * 2;
            }
            if (string.IsNullOrEmpty(target.Text))
                return source.Text.Length * 2;
            var N = source.Text.Length;
            var M = target.Text.Length;
            var Distance = new int[3, M + 1];
            for (var J = 1; J <= M; J++)
                Distance[0, J] = J * 2;
            var CurrentRow = 0;
            for (var I = 1; I <= N; ++I)
            {
                CurrentRow = I % 3;
                var PreviousRow = (I - 1) % 3;
                Distance[CurrentRow, 0] = I * 2;
                for (var J = 1; J <= M; J++)
                {
                    Distance[CurrentRow, J] = Math.Min(Math.Min(
                                Distance[PreviousRow, J] + (!fullWord && I == N ? 2 - 1 : 2),
                                Distance[CurrentRow, J - 1] + (!fullWord && I == N ? 2 - 1 : 2)),
                                Distance[PreviousRow, J - 1] + CostDistanceSymbol(source, I - 1, target, J - 1, translation));

                    if (I > 1 && J > 1 && source.Text[I - 1] == target.Text[J - 2]
                                       && source.Text[I - 2] == target.Text[J - 1])
                        Distance[CurrentRow, J] = Math.Min(Distance[CurrentRow, J], Distance[(I - 2) % 3, J - 2] + 2);
                }
            }
            return Distance[CurrentRow, M];
        }
        private int CostDistanceSymbol(CWord source, int sourcePosition, CWord search, int searchPosition, bool translation)
        {
            if (source.Text[sourcePosition] == search.Text[searchPosition])
                return 0;
            if (translation)
                return 2;
            if (source.Codes[sourcePosition] != 0 && source.Codes[sourcePosition] == search.Codes[searchPosition])
                return 0;

            int ResultWeight;
            List<int> nearKeys;
            if (!s_distanceCodeKey.TryGetValue(source.Codes[sourcePosition], out nearKeys))
                ResultWeight = 2;
            else
                ResultWeight = nearKeys.Contains(search.Codes[searchPosition]) ? 1 : 2;

            List<char> phoneticGroups;
            if (PhoneticGroupsRus.TryGetValue(search.Text[searchPosition], out phoneticGroups))
                ResultWeight = Math.Min(ResultWeight, phoneticGroups.Contains(source.Text[sourcePosition]) ? 1 : 2);
            if (PhoneticGroupsEng.TryGetValue(search.Text[searchPosition], out phoneticGroups))
                ResultWeight = Math.Min(ResultWeight, phoneticGroups.Contains(source.Text[sourcePosition]) ? 1 : 2);
            return ResultWeight;
        }
        private List<int> GetKeyCodes(List<KeyValuePair<char, int>> codeKeys, string word)
        {
            return word.ToLower().Select(ch => codeKeys.FirstOrDefault(ck => ck.Key == ch).Value).ToList();
        }
        private string Transliterate(string text, Dictionary<char, string> cultureFrom)
        {
            var TranslateText = text.SelectMany(t => {
                string translateChar;
                if (cultureFrom.TryGetValue(t, out translateChar))
                    return translateChar;
                return t.ToString();
            });
            return string.Concat(TranslateText);
        }
        #region Блок Фонетических групп

        private static Dictionary<char, List<char>> PhoneticGroupsRus = new Dictionary<char, List<char>>();
        private static Dictionary<char, List<char>> PhoneticGroupsEng = new Dictionary<char, List<char>>();
        #endregion
        static CFuzzySearch()
        {
            SetPhoneticGroups(PhoneticGroupsRus, new List<string>() { "ыий", "эе", "ая", "оёе", "ую", "шщ", "оа" });
            SetPhoneticGroups(PhoneticGroupsEng, new List<string>() { "aeiouy", "bp", "ckq", "dt", "lr", "mn", "gj", "fpv", "sxz", "csz" });
        }
        private static void SetPhoneticGroups(Dictionary<char, List<char>> resultPhoneticGroups, List<string> phoneticGroups)
        {
            foreach (var Group in phoneticGroups)
                foreach (var Symbol in Group)
                    if (!resultPhoneticGroups.ContainsKey(Symbol))
                        resultPhoneticGroups.Add(Symbol, phoneticGroups.Where(pg => pg.Contains(Symbol)).SelectMany(pg => pg).Distinct().Where(ch => ch != Symbol).ToList());
        }

        #region Блок для сопоставления клавиатуры 
        /// <summary>
        /// Близость кнопок клавиатуры
        /// </summary>
        private static readonly Dictionary<int, List<int>> s_distanceCodeKey = new Dictionary<int, List<int>>
        {
            /* '`' */ { 192 , new List<int>(){ 49 }},
            /* '1' */ { 49 , new List<int>(){ 50, 87, 81 }},
            /* '2' */ { 50 , new List<int>(){ 49, 81, 87, 69, 51 }},
            /* '3' */ { 51 , new List<int>(){ 50, 87, 69, 82, 52 }},
            /* '4' */ { 52 , new List<int>(){ 51, 69, 82, 84, 53 }},
            /* '5' */ { 53 , new List<int>(){ 52, 82, 84, 89, 54 }},
            /* '6' */ { 54 , new List<int>(){ 53, 84, 89, 85, 55 }},
            /* '7' */ { 55 , new List<int>(){ 54, 89, 85, 73, 56 }},
            /* '8' */ { 56 , new List<int>(){ 55, 85, 73, 79, 57 }},
            /* '9' */ { 57 , new List<int>(){ 56, 73, 79, 80, 48 }},
            /* '0' */ { 48 , new List<int>(){ 57, 79, 80, 219, 189 }},
            /* '-' */ { 189 , new List<int>(){ 48, 80, 219, 221, 187 }},
            /* '+' */ { 187 , new List<int>(){ 189, 219, 221 }},
            /* 'q' */ { 81 , new List<int>(){ 49, 50, 87, 83, 65 }},
            /* 'w' */ { 87 , new List<int>(){ 49, 81, 65, 83, 68, 69, 51, 50 }},
            /* 'e' */ { 69 , new List<int>(){ 50, 87, 83, 68, 70, 82, 52, 51 }},
            /* 'r' */ { 82 , new List<int>(){ 51, 69, 68, 70, 71, 84, 53, 52 }},
            /* 't' */ { 84 , new List<int>(){ 52, 82, 70, 71, 72, 89, 54, 53 }},
            /* 'y' */ { 89 , new List<int>(){ 53, 84, 71, 72, 74, 85, 55, 54 }},
            /* 'u' */ { 85 , new List<int>(){ 54, 89, 72, 74, 75, 73, 56, 55 }},
            /* 'i' */ { 73 , new List<int>(){ 55, 85, 74, 75, 76, 79, 57, 56 }},
            /* 'o' */ { 79 , new List<int>(){ 56, 73, 75, 76, 186, 80, 48, 57 }},
            /* 'p' */ { 80 , new List<int>(){ 57, 79, 76, 186, 222, 219, 189, 48 }},
            /* '[' */ { 219 , new List<int>(){ 48, 186, 222, 221, 187, 189 }},
            /* ']' */ { 221 , new List<int>(){ 189, 219, 187 }},
            /* 'a' */ { 65 , new List<int>(){ 81, 87, 83, 88, 90 }},
            /* 's' */ { 83 , new List<int>(){ 81, 65, 90, 88, 67, 68, 69, 87, 81 }},
            /* 'd' */ { 68 , new List<int>(){ 87, 83, 88, 67, 86, 70, 82, 69 }},
            /* 'f' */ { 70 , new List<int>(){ 69, 68, 67, 86, 66, 71, 84, 82 }},
            /* 'g' */ { 71 , new List<int>(){ 82, 70, 86, 66, 78, 72, 89, 84 }},
            /* 'h' */ { 72 , new List<int>(){ 84, 71, 66, 78, 77, 74, 85, 89 }},
            /* 'j' */ { 74 , new List<int>(){ 89, 72, 78, 77, 188, 75, 73, 85 }},
            /* 'k' */ { 75 , new List<int>(){ 85, 74, 77, 188, 190, 76, 79, 73 }},
            /* 'l' */ { 76 , new List<int>(){ 73, 75, 188, 190, 191, 186, 80, 79 }},
            /* ';' */ { 186 , new List<int>(){ 79, 76, 190, 191, 222, 219, 80 }},
            /* '\''*/ { 222 , new List<int>(){ 80, 186, 191, 221, 219 }},
            /* 'z' */ { 90 , new List<int>(){ 65, 83, 88 }},
            /* 'x' */ { 88 , new List<int>(){ 90, 65, 83, 68, 67 }},
            /* 'c' */ { 67 , new List<int>(){ 88, 83, 68, 70, 86 }},
            /* 'v' */ { 86 , new List<int>(){ 67, 68, 70, 71, 66 }},
            /* 'b' */ { 66 , new List<int>(){ 86, 70, 71, 72, 78 }},
            /* 'n' */ { 78 , new List<int>(){ 66, 71, 72, 74, 77 }},
            /* 'm' */ { 77 , new List<int>(){ 78, 72, 74, 75, 188 }},
            /* '<' */ { 188 , new List<int>(){ 77, 74, 75, 76, 190 }},
            /* '>' */ { 190 , new List<int>(){ 188, 75, 76, 186, 191 }},
            /* '?' */ { 191 , new List<int>(){ 190, 76, 186, 222 }},
        };
        /// <summary>
        /// Коды клавиш русскоязычной клавиатуры
        /// </summary>
        private static readonly Dictionary<char, int> s_codeKeysRus = new Dictionary<char, int>
        {
            { 'ё' , 192  },
            { '1' , 49  },
            { '2' , 50  },
            { '3' , 51  },
            { '4' , 52  },
            { '5' , 53  },
            { '6' , 54  },
            { '7' , 55  },
            { '8' , 56  },
            { '9' , 57  },
            { '0' , 48  },
            { '-' , 189 },
            { '=' , 187 },
            { 'й' , 81  },
            { 'ц' , 87  },
            { 'у' , 69  },
            { 'к' , 82  },
            { 'е' , 84  },
            { 'н' , 89  },
            { 'г' , 85  },
            { 'ш' , 73  },
            { 'щ' , 79  },
            { 'з' , 80  },
            { 'х' , 219 },
            { 'ъ' , 221 },
            { 'ф' , 65  },
            { 'ы' , 83  },
            { 'в' , 68  },
            { 'а' , 70  },
            { 'п' , 71  },
            { 'р' , 72  },
            { 'о' , 74  },
            { 'л' , 75  },
            { 'д' , 76  },
            { 'ж' , 186 },
            { 'э' , 222 },
            { 'я' , 90  },
            { 'ч' , 88  },
            { 'с' , 67  },
            { 'м' , 86  },
            { 'и' , 66  },
            { 'т' , 78  },
            { 'ь' , 77  },
            { 'б' , 188 },
            { 'ю' , 190 },
            { '.' , 191 },

            { '!' , 49  },
            { '"' , 50  },
            { '№' , 51  },
            { ';' , 52  },
            { '%' , 53  },
            { ':' , 54  },
            { '?' , 55  },
            { '*' , 56  },
            { '(' , 57  },
            { ')' , 48  },
            { '_' , 189 },
            { '+' , 187 },
            { ',' , 191 },
        };
        /// <summary>
        /// Коды клавиш англиской клавиатуры
        /// </summary>
        private static readonly Dictionary<char, int> s_codeKeysEng = new Dictionary<char, int>
        {
            { '`', 192 },
            { '1', 49   },
            { '2', 50   },
            { '3', 51   },
            { '4', 52   },
            { '5', 53   },
            { '6', 54   },
            { '7', 55   },
            { '8', 56   },
            { '9', 57   },
            { '0', 48   },
            { '-', 189  },
            { '=', 187  },
            { 'q', 81   },
            { 'w', 87   },
            { 'e', 69   },
            { 'r', 82   },
            { 't', 84   },
            { 'y', 89   },
            { 'u', 85   },
            { 'i', 73   },
            { 'o', 79   },
            { 'p', 80   },
            { '[', 219  },
            { ']', 221  },
            { 'a', 65   },
            { 's', 83   },
            { 'd', 68   },
            { 'f', 70   },
            { 'g', 71   },
            { 'h', 72   },
            { 'j', 74   },
            { 'k', 75   },
            { 'l', 76   },
            { ';', 186  },
            { '\'', 222 },
            { 'z', 90   },
            { 'x', 88   },
            { 'c', 67   },
            { 'v', 86   },
            { 'b', 66   },
            { 'n', 78   },
            { 'm', 77   },
            { ',', 188  },
            { '.', 190  },
            { '/', 191  },

            { '~' , 192 },
            { '!' , 49  },
            { '@' , 50  },
            { '#' , 51  },
            { '$' , 52  },
            { '%' , 53  },
            { '^' , 54  },
            { '&' , 55  },
            { '*' , 56  },
            { '(' , 57  },
            { ')' , 48  },
            { '_' , 189 },
            { '+' , 187 },

            { '{', 219  },
            { '}', 221  },
            { ':', 186  },
            { '"', 222  },

            { '<', 188  },
            { '>', 190  },
            { '?', 191  },
        };
        #endregion

        #region Блок транслитерации
        /// <summary>
        /// Транслитерация Русский => ASCII (ISO 9-95)
        /// </summary>
        private static readonly Dictionary<char, string> s_translitRuEn = new Dictionary<char, string>
        {
            { 'а', "a" },
            { 'б', "b" },
            { 'в', "v" },
            { 'г', "g" },
            { 'д', "d" },
            { 'е', "e" },
            { 'ё', "yo" },
            { 'ж', "zh" },
            { 'з', "z" },
            { 'и', "i" },
            { 'й', "i" },
            { 'к', "k" },
            { 'л', "l" },
            { 'м', "m" },
            { 'н', "n" },
            { 'о', "o" },
            { 'п', "p" },
            { 'р', "r" },
            { 'с', "s" },
            { 'т', "t" },
            { 'у', "u" },
            { 'ф', "f" },
            { 'х', "x" },
            { 'ц', "c" },
            { 'ч', "ch" },
            { 'ш', "sh" },
            { 'щ', "shh" },
            { 'ъ', "" },
            { 'ы', "y" },
            { 'ь', "'" },
            { 'э', "e" },
            { 'ю', "yu" },
            { 'я', "ya" },
        };
        #endregion
    }
}