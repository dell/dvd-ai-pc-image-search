namespace SemanticImageSearchAIPCT.Tokenizer
{
    public class Tokenizer
    {
        public Encoding _encoding { get; }
        public string _language { get; }
        public string _task { get; }
        public int[] _sotSequence { get; private set; }
        public Dictionary<string, int> _specialTokens { get; set; }

        public Tokenizer(Encoding encoding, string language = null, string task = null)
        {
            try
            {
                _encoding = encoding;
                _language = language;
                _task = task;
                _sotSequence = Array.Empty<int>();
                _specialTokens = (Dictionary<string, int>)FieldFactory.CreateField(
                defaultFactory: () => new Dictionary<string, int>()
             ).DefaultFactory();

                foreach (var special in _encoding.SpecialTokensSet)
                {
                    //LoggingService.LogDebug($"special: {special}");
                    var specialToken = _encoding.EncodeSingleToken(special);
                    //LoggingService.LogDebug($"specialToken: {specialToken}");
                    _specialTokens[special] = specialToken;
                }

                int sot = _specialTokens["<|startoftranscript|>"];
                int translate = _specialTokens["<|translate|>"];
                int transcribe = _specialTokens["<|transcribe|>"];


                var langs = Constants.LANGUAGES.Keys.ToArray();
                var sotSequence = new List<int> { sot };
                if (!string.IsNullOrEmpty(language))
                {
                    sotSequence.Add(sot + 1 + Array.IndexOf(langs, language));
                }
                if (!string.IsNullOrEmpty(task))
                {
                    var taskToken = task == "transcribe" ? transcribe : translate;
                    sotSequence.Add(taskToken);
                }

                _sotSequence = sotSequence.ToArray();
            }
            catch (Exception e)
            {
                throw new ArgumentOutOfRangeException("encoding", "Invalid encoding", e.Message);
            }
        }
           
        public string Decode(List<int> tokenIds, params object[] kwargs)
        {
            tokenIds = tokenIds.Where(t => t < TimestampBegin).ToList();

            string errors = "replace";
          
            foreach (var kwarg in kwargs)
            {
                if (kwarg is KeyValuePair<string, string> kvp && kvp.Key == "errors")
                {
                    errors = kvp.Value;
                    break;
                }
            }

            return _encoding.Decode(tokenIds, errors);
        } 

        public int TimestampBegin => _specialTokens["<|0.00|>"];  
      
    }
}
