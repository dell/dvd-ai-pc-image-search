using SemanticImageSearchAIPCT.UI.Common;

namespace SemanticImageSearchAIPCT.UI.Services
{
    public interface IWhisperDecoderInferenceService
    {

        void SetExecutionProvider(ExecutionProviders ep);

        List<int> DecoderInference(
         float[,,,] k_cache_cross, float[,,,] v_cache_cross);
    
        Task InitializeDecoderModel();
    }
}
