﻿using CsvHelper;
using CsvHelper.Configuration;
using Otamajakushi;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace SkytomoJbovlaste
{
    class Program
    {
        static void Main(string[] args)
        {
            var gismus = LoadGismu(@"gismu.csv");
            var cmavos = LoadCmavo(@"cmavo.csv");
            Console.WriteLine("CSVファイルの読み込みが完了しました");

            var dictionary = new OneToManyJson();
            ConvertGismu(gismus, dictionary);
            ConvertCmavo(cmavos, dictionary);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                WriteIndented = true,
            };
            var json = OneToManyJsonSerializer.Serialize(dictionary, options);
            File.WriteAllText(@"skaitomon-zei-jbovlaste.json", json);
            Console.WriteLine("JSONファイルの書き込みが完了しました");
        }

        public static List<GismuWord> LoadGismu(string path)
        {
            using (var reader = new StreamReader(path))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Configuration.RegisterClassMap<GismuMap>();
                return csv.GetRecords<GismuWord>().ToList();
            }
        }

        public static List<CmavoWord> LoadCmavo(string path)
        {
            using (var reader = new StreamReader(path))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Configuration.RegisterClassMap<CmavoMap>();
                return csv.GetRecords<CmavoWord>().ToList();
            }
        }

        public static void ConvertGismu(List<GismuWord> gismus, OneToManyJson dictionary)
        {
            foreach (var gismu in gismus)
            {
                var word = new Word
                {
                    Entry = new Entry
                    {
                        Form = gismu.Name,
                    },
                    Translations = new List<Translation>(),
                    Tags = gismu.Tags,
                };
                word.Tags.Insert(0, gismu.IsOfficial ? "標準" : "非標準");
                word.Tags.Insert(1, "ギスム");
                word.Tags.Insert(2, gismu.IsOfficial ? "標準ギスム" : "非標準ギスム");
                foreach (var meaning in gismu.Meanings)
                {
                    word.Translations.Add(new Translation()
                    {
                        Title = "内容語",
                        Forms = new List<string>() { meaning },
                    });
                }
                var translationsTuples = new List<Tuple<string, List<string>>>
                {
                    new Tuple<string, List<string>> ("lo go'i", gismu.Argument1),
                    new Tuple<string, List<string>> ("lo se go'i", gismu.Argument2),
                    new Tuple<string, List<string>> ("lo te go'i", gismu.Argument3),
                    new Tuple<string, List<string>> ("lo ve go'i", gismu.Argument4),
                    new Tuple<string, List<string>> ("lo xe go'i", gismu.Argument5),
                    new Tuple<string, List<string>> ("la go'i", gismu.Cmevla),
                    new Tuple<string, List<string>> ("キーワード", gismu.Keywords),
                    new Tuple<string, List<string>> ("原文", gismu.Original),
                };
                foreach (var (title, forms) in translationsTuples)
                {
                    var newforms = forms.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                    if (newforms.Count > 0)
                    {
                        word.Translations.Add(new Translation()
                        {
                            Title = title,
                            Forms = newforms,
                        });
                    }
                }
                var contentsTuples = new List<Tuple<string, string>>
                {
                    new Tuple<string, string> ("語法", gismu.Usage),
                    new Tuple<string, string> ("使用例", gismu.Example),
                    new Tuple<string, string> ("参照", gismu.References),
                    new Tuple<string, string> ("Tips", gismu.Tips),
                    new Tuple<string, string> ("ロジバンたんのメモ", gismu.Lojbantan),
                    new Tuple<string, string> ("覚え方", gismu.HowToMemorise),
                };
                foreach (var (title, text) in contentsTuples)
                {
                    if (!string.IsNullOrEmpty(text))
                    {
                        word.Contents.Add(new Content()
                        {
                            Title = title,
                            Text = text,
                        });
                    }
                }
                var variations = new List<string>
                {
                    gismu.Rafsi1,
                    gismu.Rafsi2,
                };
                foreach (var variation in variations)
                {
                    if (!string.IsNullOrEmpty(variation))
                    {
                        word.Variations.Add(new Variation()
                        {
                            Title = "rafsi",
                            Form = variation,
                        });
                        dictionary.AddWord(new Word()
                        {
                            Entry = new Entry
                            {
                                Form = variation.Replace("-", string.Empty).Trim(),
                            },
                            Tags = new List<string>() { "ラフシ" },
                            Translations = new List<Translation>()
                            {
                                new Translation ()
                                {
                                    Title = "形態素",
                                    Forms = new List<string>() {gismu.Name + "のrafsi" },
                                },
                            },
                            Relations = new List<Relation>
                            {
                                new Relation
                                {
                                    Title = "gismu",
                                    Entry = word.Entry,
                                }
                            }
                        });
                    }
                }
                dictionary.AddWord(word);
            }
            foreach (var gismu in gismus)
            {
                foreach (var superordinateConcept in gismu.SuperordinateConcept)
                {
                    foreach (var word in dictionary.Words.FindAll(x => x.Entry.Form == superordinateConcept))
                    {
                        dictionary.Words.Find(x => x.Entry.Form == gismu.Name).Relations.Add(new Relation()
                        {
                            Title = "上位概念",
                            Entry = word.Entry,
                        });
                    }
                }
            }
            dictionary.Zpdic = new Zpdic()
            {
                AlphabetOrder = string.Empty,
                WordOrderType = "UNICODE",
                Punctuations = new List<string>() { ",", "、" },
                IgnoredTranslationRegex = "\\s*\\(.+?\\)\\s*|\\s*（.+?）\\s*|～",
                PronunciationTitle = "発音",
                PlainInformationTitles = new List<string>()
                {
                    "The Complete Lojban Language",
                    "はじめてのロジバン 第2版",
                    "ko lojbo .iu ロジバン入門",
                    "Lojban Wave Lessons",
                    "はじめてのロジバン"
                },
                InformationTitleOrder = null,
                FormFontFamily = null,
            };
            dictionary.RelationIdCompletion();
        }

        public static void ConvertCmavo(List<CmavoWord> cmavos, OneToManyJson dictionary)
        {
            foreach (var cmavo in cmavos)
            {
                var word = new Word
                {
                    Entry = new Entry
                    {
                        Form = cmavo.Name,
                    },
                    Translations = new List<Translation>(),
                    Tags = cmavo.Tags,
                };
                switch (cmavo.Type)
                {
                    case "標準":
                    case "非標準":
                        word.Tags.Insert(0, cmavo.Type);
                        word.Tags.Insert(1, "シュマボ");
                        word.Tags.Insert(2, cmavo.Type + "シュマボ");
                        break;
                    case "複合cmavo":
                        word.Tags.Insert(0, "複合シュマボ");
                        break;
                    default:
                        word.Tags.Add(cmavo.Type);
                        break;
                }
                word.Tags.Add(cmavo.Selmaho);
                foreach (var meaning in cmavo.Meanings)
                {
                    word.Translations.Add(new Translation()
                    {
                        Title = "機能語",
                        Forms = new List<string>() { meaning },
                    });
                }
                var translationsTuples = new List<Tuple<string, List<string>>>
                {
                    new Tuple<string, List<string>> ("キーワード", cmavo.Keywords),
                    new Tuple<string, List<string>> ("原文", cmavo.Original),
                };
                foreach (var (title, forms) in translationsTuples)
                {
                    var newforms = forms.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                    if (newforms.Count > 0)
                    {
                        word.Translations.Add(new Translation()
                        {
                            Title = title,
                            Forms = newforms,
                        });
                    }
                }
                var contentsTuples = new List<Tuple<string, string>>
                {
                    new Tuple<string, string> ("語法", cmavo.Usage),
                    new Tuple<string, string> ("文法", cmavo.Grammar),
                    new Tuple<string, string> ("使用例", cmavo.Example),
                    new Tuple<string, string> ("語源", cmavo.Etymology),
                    new Tuple<string, string> ("ロジバンたんのメモ", cmavo.Lojbantan),
                    new Tuple<string, string> ("覚え方", cmavo.HowToMemorise),
                };
                foreach (var (title, text) in contentsTuples)
                {
                    if (!string.IsNullOrEmpty(text))
                    {
                        word.Contents.Add(new Content()
                        {
                            Title = title,
                            Text = text,
                        });
                    }
                }
                var variations = new List<string>
                {
                    cmavo.Rafsi1,
                    cmavo.Rafsi2,
                };
                foreach (var variation in variations)
                {
                    if (!string.IsNullOrEmpty(variation))
                    {
                        word.Variations.Add(new Variation()
                        {
                            Title = "rafsi",
                            Form = variation,
                        });
                        dictionary.AddWord(new Word()
                        {
                            Entry = new Entry
                            {
                                Form = variation.Replace("-", string.Empty).Trim(),
                            },
                            Tags = new List<string>() { "語根《ラフシ》" },
                            Translations = new List<Translation>()
                            {
                                new Translation ()
                                {
                                    Title = "形態素",
                                    Forms = new List<string>() {cmavo.Name + "のrafsi" },
                                },
                            },
                            Relations = new List<Relation>
                            {
                                new Relation
                                {
                                    Title = "gismu",
                                    Entry = word.Entry,
                                }
                            }
                        });
                    }
                }
                dictionary.AddWord(word);
            }
        }
    }
}
