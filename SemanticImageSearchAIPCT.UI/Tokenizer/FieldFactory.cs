
namespace SemanticImageSearchAIPCT.UI.Tokenizer
{
    public static class FieldFactory
    {
        public static Field CreateField(
            object defaultValue = null,
            Func<object> defaultFactory = null,
            bool init = true,
            bool repr = true,
            bool? hash = null,
            bool compare = true,
            IDictionary<string, object> metadata = null,
            bool? kwOnly = null)
        {
         
            if (defaultValue != null && defaultFactory != null)
            {
                throw new ArgumentException("Cannot specify both default and default_factory");
            }
            bool hashValue = hash ?? true;
            bool kwOnlyValue = kwOnly ?? false;
           
            return new Field(defaultValue, defaultFactory, init, repr, hashValue, compare, metadata, kwOnlyValue);
        }
    }
}
