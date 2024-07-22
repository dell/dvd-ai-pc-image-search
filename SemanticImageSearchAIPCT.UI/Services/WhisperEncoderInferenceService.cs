using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.OnnxRuntime;
using System.Diagnostics;
using SemanticImageSearchAIPCT.UI.Common;
using OpenAI_API.Moderation;


namespace SemanticImageSearchAIPCT.UI.Services
{
    public class WhisperEncoderInferenceService : IWhisperEncoderInferenceService
    {
        private InferenceSession _session;
        private Conversion _conversion;
        private string _encoderModelPath;
        private string _modelDir;

        private int[] k_cache_cross_shape;
        private int[] v_cache_cross_shape;
        private float[,,,] k_cache_cross;
        private float[,,,] v_cache_cross;
        private static ExecutionProviders executionProvider = ExecutionProviders.Cpu;
        private static Dictionary<string, string> qnnOptions = new() { { "backend_path", "QnnHtp.dll" } };

        private List<string> _outputKeys;
        private Dictionary<string, int[]> _outputDimensions;
        private Dictionary<string, Tensor<float>> _outputTensors;
        private Dictionary<string, int[]> _outputTensorDimensions;

        public WhisperEncoderInferenceService()
        {

            _conversion = new Conversion();
            var _baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);
            _modelDir = Path.Combine(_baseDir, "AIModels");
        }


        public void SetExecutionProvider(ExecutionProviders ep)
        {
            Debug.WriteLine($"Setting ep as {ep}");
            executionProvider = ep;
            CreateSession();
        }

        private (string? epName, Dictionary<string, string>? epOptions, string modelpath) UpdateSessionsOptions()
        {
            try
            {
                (string? epName, Dictionary<string, string>? epOptions, string modelpath) result;
                Dictionary<string, string> epOptions;
                switch (executionProvider)
                {
                    case ExecutionProviders.QnnCpu:
                        qnnOptions["backend_path"] = "QnnCpu.dll";
                        epOptions = qnnOptions;
                        result = ("QNN", epOptions, "whisper_base_en-whisperencoder.quant.onnx");
                        break;
                    case ExecutionProviders.QnnHtp:
                        qnnOptions["backend_path"] = "QnnHtp.dll";
                        epOptions = qnnOptions;
                        result = ("QNN", epOptions, "whisper_base_en-whisperencoder.quant.onnx");
                        break;
                    default:
                        result = (null, null, "whisper_base_en-whisperencoder.onnx");
                        break;
                }
                Debug.WriteLine($"epName: {result.epName ?? "CPU"}");
                if (result.epOptions != null)
                {
                    foreach (var option in result.epOptions)
                    {
                        Debug.WriteLine($"epOption: {option.Key} = {option.Value}");
                    }
                }
                Debug.WriteLine($"Encoder modelpath: {result.modelpath}");

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error Encoder UpdateSessionsOptions: {ex.Message}");
                throw;
            }
        }
        private void CreateSession()
        {
            try
            {

                using var sessionOptions = new SessionOptions();
                var (epName, epOptions, modelpath) = UpdateSessionsOptions();

                if (epName != null)
                {
                    sessionOptions.AppendExecutionProvider(epName, epOptions);
                }
                var _modelAIpath = Path.Combine(_modelDir, modelpath);
                _session = new InferenceSession(_modelAIpath, sessionOptions);

                InitializeOutputMetadata();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error Encoder CreateSession: {ex.Message}");
                throw;
            }
        }

        private void InitializeOutputMetadata()
        {
            try
            {


                // Get the shape of k_cache_cross dynamically
                var outputMetadata = _session.OutputMetadata;

                _outputKeys = outputMetadata.Keys.ToList();
                _outputDimensions = new Dictionary<string, int[]>();

                foreach (var name in _outputKeys)
                {
                    var dimensions = outputMetadata[name].Dimensions;
                    _outputDimensions[name] = dimensions;

                    Debug.WriteLine($"Key: {name}, Dimensions: {string.Join(", ", dimensions)}");
                }
                if (_outputDimensions.ContainsKey("output_0") && _outputDimensions["output_0"].Length == 4)
                {
                    k_cache_cross_shape = _outputDimensions["output_0"];
                    k_cache_cross = new float[k_cache_cross_shape[0], k_cache_cross_shape[1], k_cache_cross_shape[2], k_cache_cross_shape[3]];
                }
                else if (_outputDimensions.ContainsKey("k_cache") && _outputDimensions["k_cache"].Length == 4)
                {
                    k_cache_cross_shape = _outputDimensions["k_cache"];
                    k_cache_cross = new float[k_cache_cross_shape[0], k_cache_cross_shape[1], k_cache_cross_shape[2], k_cache_cross_shape[3]];
                }
                else
                {
                    throw new Exception("output_0 or k_cache dimensions are not valid or not found.");
                }
                if (_outputDimensions.ContainsKey("output_1") && _outputDimensions["output_1"].Length == 4)
                {
                    v_cache_cross_shape = _outputDimensions["output_1"];
                    v_cache_cross = new float[v_cache_cross_shape[0], v_cache_cross_shape[1], v_cache_cross_shape[2], v_cache_cross_shape[3]];
                }
                else if (_outputDimensions.ContainsKey("v_cache") && _outputDimensions["v_cache"].Length == 4)
                {
                    v_cache_cross_shape = _outputDimensions["v_cache"];
                    v_cache_cross = new float[v_cache_cross_shape[0], v_cache_cross_shape[1], v_cache_cross_shape[2], v_cache_cross_shape[3]];
                }
                else
                {
                    throw new Exception("output_1 or v_cache dimensions are not valid or not found.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error Encoder InitializeOutputMetadata: {ex.Message}");
                throw;
            }
        }
        public (float[,,,] output_0, float[,,,] output_1) RunInference(float[] input, int batchSize, int numChannels, int numSamples)
        {
            if (_session == null)
            {
                CreateSession();
            }

            //float[,,,] k_cache_cross = new float[6, 8, 64, 1500];
            //float[,,,] v_cache_cross = new float[6, 8, 1500, 64];

            //Small Encoder
            //k_cache_cross: 12, 12, 64, 1500
            //v_cache_cross: 12, 12, 1500, 64

            Debug.WriteLine($"k_cache_cross: {k_cache_cross_shape[0]}, {k_cache_cross_shape[1]}, {k_cache_cross_shape[2]}, {k_cache_cross_shape[3]}");
            Debug.WriteLine($"v_cache_cross: {v_cache_cross_shape[0]}, {v_cache_cross_shape[1]}, {v_cache_cross_shape[2]}, {v_cache_cross_shape[3]}");
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
                var namedInput = NamedOnnxValue.CreateFromTensor("audio", tensor);
                var inputs = new List<NamedOnnxValue> { namedInput };
                IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = null;

                List<string> outputs = _outputKeys;
                using var runOptions = new RunOptions();

                results = _session.Run(inputs, outputs);
                var k_cache_cross_tensor = results.First(r => r.Name == _outputKeys[0]).AsTensor<float>();
                var v_cache_cross_tensor = results.First(r => r.Name == _outputKeys[1]).AsTensor<float>();

                //k_cache_cross = _conversion.To4DArray(output_0_tensor, 6, 8, 64, 1500);
                //v_cache_cross = _conversion.To4DArray(output_1_tensor, 6, 8, 1500, 64);         


                k_cache_cross = _conversion.To4DArray(k_cache_cross_tensor, k_cache_cross_shape[0], k_cache_cross_shape[1], k_cache_cross_shape[2], k_cache_cross_shape[3]);
                v_cache_cross = _conversion.To4DArray(v_cache_cross_tensor, v_cache_cross_shape[0], v_cache_cross_shape[1], v_cache_cross_shape[2], v_cache_cross_shape[3]);

                return (k_cache_cross, v_cache_cross);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while Inference: {ex.Message}");
                throw;
            }

        }


    }
}
