using System.Text.RegularExpressions;
namespace SemanticImageSearchAIPCT.Tokenizer
{ 
    public class CoreBPE
    {
        private readonly Dictionary<byte[], int> _mergeableRanks;
        private readonly Dictionary<string, int> _specialTokens;
        private readonly string _pattern;
        private readonly Regex _tokenizerRegex;
        private Dictionary<int, string> SpecialTokensDecoder { get; set; }
        private Dictionary<int, byte[]> Decoder { get; set; }
        public CoreBPE(Dictionary<byte[], int> mergeableRanks, 
            Dictionary<string, int> specialTokens, string pattern)
        {
            _mergeableRanks = mergeableRanks;
            _specialTokens = specialTokens;
            _pattern = pattern;
            _tokenizerRegex = new Regex(pattern);

            Decoder = mergeableRanks
           .ToDictionary(
               static x => x.Value,
               static x => x.Key);

            SpecialTokensDecoder = _specialTokens
          .ToDictionary(
              static x => x.Value,
              static x => x.Key);
        }       

        public List<int> Encode(string text)
        {
            var tokens = Tokenize(text);
            var encodedTokens = new List<int>();

            foreach (var token in tokens)
            {
                if (_specialTokens.ContainsKey(token))
                {
                    encodedTokens.Add(_specialTokens[token]);
                }
                else
                {
                    encodedTokens.AddRange(ApplyBPE(token));
                }
            }

            return encodedTokens;
        }

        private List<string> Tokenize(string text)
        {
            var matches = _tokenizerRegex.Matches(text);
            return matches.Select(m => m.Value).ToList();
        }

        private List<int> ApplyBPE(string token)
        {
            if (token.Length == 1)
            {
                var tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);
                return new List<int> { _mergeableRanks[tokenBytes] };
            }

            var pairs = GetPairs(token);
            while (pairs.Count > 0)
            {
                var minPair = pairs.OrderBy(p =>
                {
                    System.Text.Encoding.UTF8.GetBytes(p.Item1 + p.Item2);
                    var combined = System.Text.Encoding.UTF8.GetBytes(p.Item1 + p.Item2);
                    return _mergeableRanks.ContainsKey(combined) ? _mergeableRanks[combined] : int.MaxValue;
                }).FirstOrDefault();

                var combinedMinPair = System.Text.Encoding.UTF8.GetBytes(minPair.Item1 + minPair.Item2);

                if (!_mergeableRanks.ContainsKey(combinedMinPair))
                {
                    break;
                }

                var newToken = token.Replace(minPair.Item1 + minPair.Item2, System.Text.Encoding.UTF8.GetString(combinedMinPair));
                token = newToken;
                pairs = GetPairs(token);
            }

            return token.Select(c => _mergeableRanks[System.Text.Encoding.UTF8.GetBytes(c.ToString())]).ToList();
        }

        private List<(string, string)> GetPairs(string token)
        {
            var pairs = new List<(string, string)>();
            for (int i = 0; i < token.Length - 1; i++)
            {
                pairs.Add((token[i].ToString(), token[i + 1].ToString()));
            }

            return pairs;
        }

        public byte[] DecodeBytes(IReadOnlyCollection<int> tokens)
        {
            tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));

            var ret = new List<byte>(tokens.Count * 2);
            foreach (var token in tokens)
            {
                byte[] tokenBytes = Array.Empty<byte>();
                if (Decoder.TryGetValue(token, out var value))
                {
                    tokenBytes = value;
                }
                else
                {
                    if (SpecialTokensDecoder.TryGetValue(token, out var valueS))
                    {
                        tokenBytes = System.Text.Encoding.UTF8.GetBytes(valueS);
                    }
                }

                if (tokenBytes.Length > 0)
                {
                    ret.AddRange(tokenBytes);
                }
            }
            return ret.ToArray();
        }
        
    }
}