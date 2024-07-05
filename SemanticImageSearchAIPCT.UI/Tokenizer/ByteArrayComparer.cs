 namespace SemanticImageSearchAIPCT.UI.Tokenizer
{
    public class ByteArrayComparer : IEqualityComparer<byte[]>
    {      

        public bool Equals(byte[]? x, byte[]? y)
        {
            if (x == null || y == null)
            {
                return x == y;
            }
            if (x.Length != y.Length)
            {
                return false;
            }
            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] != y[i])
                {
                    return false;
                }
            }
            return true;
        }

        public int GetHashCode(byte[] obj)
        {
            obj = obj ?? throw new ArgumentNullException(nameof(obj));

            return obj.Aggregate(17, (current, b) => current * 31 + b);
        }
    }
}
