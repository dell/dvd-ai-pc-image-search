namespace SemanticImageSearchAIPCT.Tokenizer
{   
    public class Encoding
    {
        public string _name { get; private set; }
        private string _pattern;
        private Dictionary<byte[], int> _mergeableRanks;
        private Dictionary<string, int> _specialTokens;
        private int? _explicitNVocab;
        private int _maxTokenValue;
        private HashSet<string> _specialTokensSetCache = null; 
        private CoreBPE _coreBpe;

        public Encoding(string name,
            string patterns, 
            Dictionary<byte[], 
                int> mergeableRanks, 
            Dictionary<string, 
                int> specialTokens, 
            int? explicitNVocab = null)
        {
            _name = name;
            _pattern = patterns;
            _mergeableRanks = mergeableRanks;
            _specialTokens = specialTokens;
            _explicitNVocab = explicitNVocab;

            _maxTokenValue = Math.Max(mergeableRanks.Values.Max(), specialTokens.Values.DefaultIfEmpty(0).Max());
            if (explicitNVocab.HasValue)
            {
                if (mergeableRanks.Count + specialTokens.Count != explicitNVocab.Value)
                    throw new ArgumentException("The number of mergeable tokens and special tokens must equal explicit_n_vocab.");
                if (_maxTokenValue != explicitNVocab.Value - 1)
                    throw new ArgumentException("Max token value must be equal to explicit_n_vocab - 1.");
            }
           
            _coreBpe = new CoreBPE(_mergeableRanks, _specialTokens, _pattern);
        }
                 
        public string Decode(List<int> tokens, string errors = "replace")
        {
            var bytes = _coreBpe.DecodeBytes(tokens);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }       

        public int EncodeSingleToken(string token)
        {
            if (_specialTokens.ContainsKey(token))
            {
                return _specialTokens[token];
            }

            var tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);
            if (_mergeableRanks.ContainsKey(tokenBytes))
            {
                return _mergeableRanks[tokenBytes];
            }

            return ApplyBPE(token);
        }
        private int ApplyBPE(string token)
        {
            if (token.Length == 1)
            {
                var tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);
                return _mergeableRanks.ContainsKey(tokenBytes) ? _mergeableRanks[tokenBytes] : -1;
            }

            List<(string, string)> pairs = GetPairs(token);

            while (pairs.Count > 0)
            {
                var minPair = pairs.OrderBy(p =>
                {
                    var combined = System.Text.Encoding.UTF8.GetBytes(p.Item1 + p.Item2);
                    return _mergeableRanks.ContainsKey(combined) ? _mergeableRanks[combined] : int.MaxValue;
                }).First();

                var combinedMinPair = System.Text.Encoding.UTF8.GetBytes(minPair.Item1 + minPair.Item2);

                if (!_mergeableRanks.ContainsKey(combinedMinPair))
                {
                    break;
                }

                var newToken = token.Replace(minPair.Item1 + minPair.Item2, System.Text.Encoding.UTF8.GetString(combinedMinPair));
                token = newToken;
                pairs = GetPairs(token);
            }

            var tokenFinalBytes = System.Text.Encoding.UTF8.GetBytes(token);
            return _mergeableRanks.ContainsKey(tokenFinalBytes) ? _mergeableRanks[tokenFinalBytes] : -1;
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
        public HashSet<string> SpecialTokensSet
        {
            get
            {
                if (_specialTokensSetCache == null)
                {
                    _specialTokensSetCache = new HashSet<string>(_specialTokens.Keys);
                }
                return _specialTokensSetCache;
            }
        }

    }

}
