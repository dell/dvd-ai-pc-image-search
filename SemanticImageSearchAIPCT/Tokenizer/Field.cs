using System.Collections.ObjectModel;

namespace SemanticImageSearchAIPCT.Tokenizer
{
    public class Field
    {
        public string Name { get; set; }
        public Type FieldType { get; set; }
        public object Default { get; set; }
        public Func<object> DefaultFactory { get; set; }
        public bool Init { get; set; }
        public bool Repr { get; set; }
        public bool Hash { get; set; }
        public bool Compare { get; set; }
        public ReadOnlyDictionary<string, object> Metadata { get; set; }
        public bool KwOnly { get; set; }
        private object _fieldType; 

        private static readonly ReadOnlyDictionary<string, object> _emptyMetadata = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());

        public Field(object defaultValue = null, Func<object> defaultFactory = null, bool init = true, bool repr = true, bool hash = true, bool compare = true, IDictionary<string, object> metadata = null, bool kwOnly = false)
        {
            Name = null;
            FieldType = null;
            Default = defaultValue;
            DefaultFactory = defaultFactory;
            Init = init;
            Repr = repr;
            Hash = hash;
            Compare = compare;
            Metadata = metadata == null ? _emptyMetadata : new ReadOnlyDictionary<string, object>(metadata);
            KwOnly = kwOnly;
            _fieldType = null;
        }

  
    }
}
