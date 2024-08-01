using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.OnnxRuntime;
using System.Diagnostics;
using SemanticImageSearchAIPCT.UI.Common;


namespace SemanticImageSearchAIPCT.UI.Services
{
    public class WhisperEncoderInferenceService : IWhisperEncoderInferenceService
    {
        #region Fields
        private InferenceSession? _encoderSession;
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
        #endregion

        #region Constructor
        public WhisperEncoderInferenceService()
        {

            _conversion = new Conversion();
            var _baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);
            _modelDir = Path.Combine(_baseDir, "AIModels");
        }

        #endregion

        #region Execution Provider Setup
        public void SetExecutionProvider(ExecutionProviders ep)
        {
            Debug.WriteLine($"Setting ep as {ep}");
            executionProvider = ep;
            _encoderSession?.Dispose();
            _encoderSession = null;
            // CreateSession();
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
                        qnnOptions["enable_htp_fp16_precision"] = "1";
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
        #endregion

        #region Inference Session Management
        /// <summary>
        /// Creates and initializes an ONNX inference session for the Whisper model.
        /// </summary>
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
                _encoderSession = new InferenceSession(_modelAIpath, sessionOptions);

                // Initialize model dimensions dynamically based on the model metadata
                //InitializeOutputMetadata();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error Encoder CreateSession: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Dynamically initializes the dimensions for the model outputs based on the model metadata.
        /// </summary>
        public async Task InitializeEncoderModel()
        {
            try
            {
                if (_encoderSession == null)
                {
                    CreateSession();
                }
                Debug.WriteLine("Initialize Encoder Model Dimensions");
                // Get the shape of k_cache_cross,v_cache_cross dynamically
                var outputMetadata = _encoderSession.OutputMetadata;

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

        #endregion

        #region Inference Methods
        /// <summary>
        /// Runs the inference on the Whisper encoder model using the provided audio input data.
        /// </summary>
        /// <param name="input">The input audio data as a float array.</param>
        /// <param name="batchSize">The batch size for the model input.</param>
        /// <param name="numChannels">The number of channels in the audio data.</param>
        /// <param name="numSamples">The number of samples per channel in the audio data.</param>
        /// <returns>A tuple containing the k_cache_cross and v_cache_cross outputs as 4-dimensional float arrays.</returns>
        public (float[,,,] output_0, float[,,,] output_1) RunInference(float[] input, int batchSize, int numChannels, int numSamples)
        {
            // Ensure the inference session is created
            if (_encoderSession == null)
            {
                CreateSession();
            }

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

                // Create the input tensor and named input for the model
                var tensor = new DenseTensor<float>(input, inputShape);
                var namedInput = NamedOnnxValue.CreateFromTensor("audio", tensor);
                var inputs = new List<NamedOnnxValue> { namedInput };
                IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = null;

                // Define the output keys and run option
                List<string> outputs = _outputKeys;
                using var runOptions = new RunOptions();

                results = _encoderSession.Run(inputs, outputs);

                // Extract and convert the results to 4-dimensional arrays
                var k_cache_cross_tensor = results.First(r => r.Name == _outputKeys[0]).AsTensor<float>();
                var v_cache_cross_tensor = results.First(r => r.Name == _outputKeys[1]).AsTensor<float>();

                k_cache_cross = _conversion.To4DArray(k_cache_cross_tensor, k_cache_cross_shape[0], k_cache_cross_shape[1], k_cache_cross_shape[2], k_cache_cross_shape[3]);
                v_cache_cross = _conversion.To4DArray(v_cache_cross_tensor, v_cache_cross_shape[0], v_cache_cross_shape[1], v_cache_cross_shape[2], v_cache_cross_shape[3]);

                // Return the results
                return (k_cache_cross, v_cache_cross);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while Inference: {ex.Message}");
                throw;
            }

        }
        #endregion

    }
}
