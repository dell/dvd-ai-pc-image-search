using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics.Tensors;

namespace SemanticImageSearchAIPCT.UI.Models
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
