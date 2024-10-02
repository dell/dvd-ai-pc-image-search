
namespace SemanticImageSearchAIPCT.Tokenizer
{
    public static class TokenizerFactory
    {
        private static readonly Dictionary<string, Encoding> EncodingCache = new Dictionary<string, Encoding>();

        public static Tokenizer GetTokenizer(bool multilingual, int num_languages,
            string language = null, string task = null)
        {

            string _encodingName = multilingual ? "multilingual" : "gpt2";
            string _language = null;
            string _task = null;

            if (multilingual)
            {
                _language = GetLanguageCode(language) ?? "en";
                _task = task ?? "transcribe";
            }

            var encoding = GetEncoding(_encodingName, num_languages);
            return new Tokenizer(encoding, _language, _task);
        }

        private static string GetLanguageCode(string language)
        {
            if (!string.IsNullOrEmpty(language))
            {
                var lowerLanguage = language.ToLower();
                string code = null;
                if (Constants.LANGUAGES.ContainsKey(lowerLanguage) || Constants.TO_LANGUAGE_CODE.TryGetValue(lowerLanguage, out code))
                {
                    return code ?? lowerLanguage;
                }
                else
                {
                    throw new ArgumentException($"Unsupported language: {language}");
                }
            }
            return null;
        }

        public static Encoding GetEncoding(string name = "gpt2", int num_languages = 99)
        {
            if (EncodingCache.TryGetValue(name, out var encoding))
            {
                return encoding;
            }

            string vocabPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "assets", $"{name}.tiktoken");

            var ranks = LoadRanks(vocabPath);

            int nVocab = ranks.Count;
            var specialTokens = new Dictionary<string, int>();

            var specials = new List<string>();
            specials.AddRange(Constants.LANGUAGES.Keys.Take(num_languages).Select(lang => $"<|{lang}|>"));
            specials.AddRange(new[]
            {
                Constants.EndOfText,
                Constants.StartofTranscript,
                Constants.Translate,
                Constants.Transcribe,
                Constants.Startoflm,
                Constants.StartofPrev,
                Constants.Nospeech,
                 Constants.NotimeStamps,
            });
            //specials.AddRange(new[]
            //{
            //    "<|endoftext|>",
            //    "<|startoftranscript|>",
            //    "<|translate|>",
            //    "<|transcribe|>",
            //    "<|startoflm|>",
            //    "<|startofprev|>",
            //    "<|nospeech|>",
            //    "<|notimestamps|>",
            //});
            specials.AddRange(Enumerable.Range(0, 1501).Select(i => $"<|{i * 0.02:F2}|>"));

            foreach (var token in specials)
            {
                specialTokens[token] = nVocab;
                nVocab++;
            }

            encoding = new Encoding(
              name: Path.GetFileName(vocabPath),
              patterns: GetPattern(),
              mergeableRanks: ranks,
              specialTokens: specialTokens,
              explicitNVocab: nVocab
          );

            EncodingCache[name] = encoding;
            return encoding;
        }

        private static Dictionary<byte[], int> LoadRanks(string vocabPath)
        {
            var ranks = new Dictionary<byte[], int>(new ByteArrayComparer());
            foreach (var line in File.ReadLines(vocabPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(' ');
                var token = Convert.FromBase64String(parts[0]);
                var rank = int.Parse(parts[1]);
                ranks[token] = rank;
            }
            return ranks;
        }

        private static string GetPattern()
        {
            return @"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^ \p{L}\p{N}]+| ?(,)| ?[\s\u2000-\u200b\u2028\u2029\ufeff]+";
        }


    }
}
