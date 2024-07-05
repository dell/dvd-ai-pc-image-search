using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.OnnxRuntime;
using System.Diagnostics;
using SemanticImageSearchAIPCT.UI.Common;


namespace SemanticImageSearchAIPCT.UI.Service
{
    public class WhisperEncoderInferenceService
    {
        private InferenceSession _session;
        private Conversion _conversion;

        private static ExecutionProviders executionProvider = ExecutionProviders.Cpu;
        private static Dictionary<string, string> qnnOptions = new() { { "backend_path", "QnnHtp.dll" } };

        public WhisperEncoderInferenceService(string modelPath)
        {
            InitModel();
            _conversion = new Conversion();
        }
        private void InitModel()
        {
            if (_session != null)
            {
                return;
            }

            string _baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);
            string _modelDir = Path.Combine(_baseDir, "AIModels");

            string _encoderModelPath = Path.Combine(_modelDir, "whisper_base_en-whisperencoder.onnx");

            using var sessionOptions = new SessionOptions();
            var providerName = "QNN";

            //sessionOptions.AppendExecutionProvider(providerName, qnnOptions);

            _session = new InferenceSession(_encoderModelPath, sessionOptions);
            //Debug.WriteLine($"First input name: {_session.InputMetadata.Keys.First()}");
            //foreach (var inputMeta in _session.InputMetadata)
            //{
            //    Debug.WriteLine($"Encoder Key: {inputMeta.Key}");
            //    Debug.WriteLine($"Encoder Dimensions: {string.Join(",", inputMeta.Value.Dimensions)}");
            //    Debug.WriteLine($"Encoder ElementType: {inputMeta.Value.ElementType}");
            //    Debug.WriteLine($"Encoder input is : {inputMeta.Value.Dimensions.Length}, dimensional.");
            //}
            //Debug.WriteLine("Listing Encoder Output output metadata:");
            //foreach (var outputMeta in _session.OutputMetadata)
            //{
            //    Debug.WriteLine($"Encoder Output _name: {outputMeta.Key}, Encoder Output Type: {outputMeta.Value.ElementType}, Encoder Output Dimensions: {string.Join(", ", outputMeta.Value.Dimensions)}");
            //}

        }

        public static void SetExecutionProvider(ExecutionProviders ep)
        {
            Debug.WriteLine($"Setting ep as {ep}");
            executionProvider = ep;
        }              

        public SessionOptions GetSessionOptionsForEp()
        {
            var sessionOptions = new SessionOptions();


            //switch (ExecutionProviderTarget)
            //{
            //case ExecutionProvider.DirectML:
            //    sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            //    sessionOptions.EnableMemoryPattern = false;
            //    sessionOptions.AppendExecutionProvider_DML(DeviceId);
            //    sessionOptions.AppendExecutionProvider_CPU();
            //    return sessionOptions;

            //default:
            //case ExecutionProvider.Cpu:
            sessionOptions.AppendExecutionProvider_CPU();
            return sessionOptions;
            //}

        }

        //public static (string? epName, Dictionary<string, string>? epOptions) UpdateSessionsOptions()
        //{
        //    switch (executionProvider)
        //    {
        //        case ExecutionProviders.QnnCpu:
        //            var epName = "QNN";
        //            qnnOptions["backend_path"] = "QnnCpu.dll";
        //            var epOptions = qnnOptions;
        //            return (epName, epOptions);
        //        case ExecutionProviders.QnnHtp:
        //            epName = "QNN";
        //            qnnOptions["backend_path"] = "QnnHtp.dll";
        //            epOptions = qnnOptions;
        //            return (epName, epOptions);
        //        default:
        //            return (null, null);
        //    }
        //}

        public (float[,,,] output_0, float[,,,] output_1) RunInference(float[] input, int batchSize, int numChannels, int numSamples)
        {
            float[,,,] output_0 = new float[6, 8, 64, 1500];
            float[,,,] output_1 = new float[6, 8, 1500, 64];

            int numSamples1 = input.Length / numChannels;

            int expectedLength = batchSize * numChannels * numSamples;

            if (input.Length != expectedLength)
            {
                throw new ArgumentException($"Input data length ({input.Length}) does not match the expected length of {expectedLength}.");
            }

            int[] inputShape = { batchSize, numChannels, numSamples };
            try
            {

                var tensor = new DenseTensor<float>(input, inputShape);
               // var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_session.InputMetadata.Keys.First(), tensor) };
                var namedInput = NamedOnnxValue.CreateFromTensor("audio", tensor);
                var inputs = new List<NamedOnnxValue> { namedInput };
                IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = null;

                List<string> outputs = new List<string> { "output_0", "output_1" };
                using var runOptions = new RunOptions();


                results = _session.Run(inputs, outputs);
                var output_0_tensor = results.First(r => r.Name == "output_0").AsTensor<float>();
                var output_1_tensor = results.First(r => r.Name == "output_1").AsTensor<float>();

                output_0 = _conversion.To4DArray(output_0_tensor, 6, 8, 64, 1500);
                output_1 = _conversion.To4DArray(output_1_tensor, 6, 8, 1500, 64);

                return (output_0, output_1);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while Inference: {ex.Message}");
                return (output_0, output_1);
            }
            finally
            {

            }
        }
    }
}
