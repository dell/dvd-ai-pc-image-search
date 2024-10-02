using System.Numerics.Tensors;

namespace SemanticImageSearchAIPCT.Models
{
    public class ImageModel
    {
        public string Filename { get; set; }
        public Tensor<float>? Encodings { get; set; }

        public ImageModel()
        {
            Filename = "";
        }
    }
}
