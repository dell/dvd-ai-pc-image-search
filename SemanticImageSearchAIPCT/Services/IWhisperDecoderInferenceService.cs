using SemanticImageSearchAIPCT.Common;

namespace SemanticImageSearchAIPCT.Services
{
    public interface IWhisperDecoderInferenceService
    {

        void SetExecutionProvider(ExecutionProviders ep);

        Task SetExecutionProviderAsync(ExecutionProviders ep);

        List<int> DecoderInference(float[,,,] k_cache_cross, float[,,,] v_cache_cross);
    
        Task InitializeDecoderModel();
    }
}
