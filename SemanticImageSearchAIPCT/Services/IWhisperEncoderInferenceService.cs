using SemanticImageSearchAIPCT.Common;

namespace SemanticImageSearchAIPCT.Services
{
    public interface IWhisperEncoderInferenceService: IDisposable
    {
        void SetExecutionProvider(ExecutionProviders ep);
        
        Task SetExecutionProviderAsync(ExecutionProviders ep);

        (float[,,,] output_0, float[,,,] output_1) RunInference(float[] input, int batchSize, int numChannels, int numSamples);

        Task InitializeEncoderModel();
    }
}
