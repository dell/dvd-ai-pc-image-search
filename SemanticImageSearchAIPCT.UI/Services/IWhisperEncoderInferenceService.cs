using SemanticImageSearchAIPCT.UI.Common;


namespace SemanticImageSearchAIPCT.UI.Services
{
    public  interface IWhisperEncoderInferenceService
    {
        void SetExecutionProvider(ExecutionProviders ep);
        (float[,,,] output_0, float[,,,] output_1) RunInference(float[] input, int batchSize, int numChannels, int numSamples);

        Task InitializeEncoderModel();

    }
}
