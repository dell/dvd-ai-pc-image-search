using SemanticImageSearchAIPCT.UI.Common;
using SemanticImageSearchAIPCT.UI.Models;
using System.Numerics.Tensors;
using System.Text.RegularExpressions;

namespace SemanticImageSearchAIPCT.UI.Services
{
    public interface IWhisperDecoderInferenceService
    {  

        void SetExecutionProvider(ExecutionProviders ep);

        List<int> DecoderInference(
         float[,,,] k_cache_cross, float[,,,] v_cache_cross);

    }
}
