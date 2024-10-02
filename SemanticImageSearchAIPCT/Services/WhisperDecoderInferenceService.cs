
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Diagnostics;
using SemanticImageSearchAIPCT.Common;
using SemanticImageSearchAIPCT.Tokenizer;

namespace SemanticImageSearchAIPCT.Services
{
    public class WhisperDecoderInferenceService : IWhisperDecoderInferenceService, IDisposable
    {
        #region Fields

        private InferenceSession? _decoderSession;
        private string _decoderModelPath;

        private static ExecutionProviders executionProvider = ExecutionProviders.Cpu;
        private static Dictionary<string, string> qnnOptions = new() { { "backend_path", "QnnHtp.dll" } };
        private static readonly int _token_Sot = 50257;
        private static readonly int _token_Eot = 50256;

        // Whisper constants
        private static readonly int _token_Blank = 220;
        private static readonly int _token_NoTimestamp = 50362;
        private static readonly int _token_TimestampBegin = 50363;
        private static readonly int _token_NoSpeech = 50361;
        private static readonly double _noSpeechThr = 0.6;

        private static readonly int _meanDecodeLen = 224;
        private static readonly int _sampleBegin = 1;
        private double _precision = 0.02; // in seconds
        private double _maxInitialTimestamp = 1.0; // in seconds
        private int _maxInitialTimestampIndex;
        private string _modelDir;

        private int[] k_cache_self_shape;
        private int[] v_cache_self_shape;

        private float[,,,] k_cache_self;
        private float[,,,] v_cache_self;

        private int[] logits_shape;
        private int[] k_cache_shape;
        private int[] v_cache_shape;

        private float[,,] logits;
        private float[,,,] k_cache;
        private float[,,,] v_cache;

        private List<string> _outputKeys;
        private Dictionary<string, int[]> _outputDimensions;

        #endregion

        #region Constructor
        public WhisperDecoderInferenceService()
        {
            var _baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);
            _modelDir = Path.Combine(_baseDir, "AIModels");
            _maxInitialTimestampIndex = (int)(_maxInitialTimestamp / _precision);
        }

        #endregion

        #region Execution Provider Setup
        public void SetExecutionProvider(ExecutionProviders ep)
        {
            LoggingService.LogInformation($"Setting Whisper Decoder EP as {ep}");
            executionProvider = ep;
            _decoderSession?.Dispose();
            _decoderSession = null;
            CreateSession();
        }

        public async Task SetExecutionProviderAsync(ExecutionProviders ep)
        {
            await Task.Run(() => { SetExecutionProvider(ep); });
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
                        result = ("QNN", epOptions, "whisper_base_en-whisperdecoder.quant.onnx");
                        break;
                    case ExecutionProviders.QnnHtp:
                        qnnOptions["backend_path"] = "QnnHtp.dll";
                        qnnOptions["enable_htp_fp16_precision"] = "1";
                        epOptions = qnnOptions;
                        result = ("QNN", epOptions, "whisper_base_en-whisperdecoder.onnx");
                        break;
                    default:
                        result = (null, null, "whisper_base_en-whisperdecoder.onnx");
                        break;

                }
                LoggingService.LogDebug($"Decoder modelpath: {result.modelpath}");
                return result;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error Decoder UpdateSessionsOptions:", ex);

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
                var stopwatch = Stopwatch.StartNew(); // Start timing
                using var sessionOptions = new SessionOptions();
                var (epName, epOptions, modelpath) = UpdateSessionsOptions();

                if (epName != null)
                {
                    sessionOptions.AppendExecutionProvider(epName, epOptions);
                }
                var _modelAIpath = Path.Combine(_modelDir, modelpath);
                _decoderSession = new InferenceSession(_modelAIpath, sessionOptions);

                // Initialize model dimensions dynamically based on the model metadata
                //InitializeModelDimensionsDynamically();
                stopwatch.Stop();
                LoggingService.LogDebug($"Create Image Decoder Session Duration: {stopwatch.Elapsed.TotalSeconds} seconds");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error Decoder CreateSession:", ex);
                throw;
            }
        }

        /// <summary>
        /// Dynamically initializes the dimensions for the model inputs and outputs based on the model metadata.
        /// </summary>
        public async Task InitializeDecoderModel()
        {
            try
            {
                if (_decoderSession == null)
                {
                    CreateSession();
                }
                var stopwatch = Stopwatch.StartNew();

                // Get the shape dynamically

                //Inputs
                var input0Meta = _decoderSession.InputMetadata["k_cache_self"];
                k_cache_self_shape = input0Meta.Dimensions;

                var input1Meta = _decoderSession.InputMetadata["v_cache_self"];
                v_cache_self_shape = input1Meta.Dimensions;

                k_cache_self = new float[k_cache_self_shape[0], k_cache_self_shape[1], k_cache_self_shape[2], k_cache_self_shape[3]];
                v_cache_self = new float[v_cache_self_shape[0], v_cache_self_shape[1], v_cache_self_shape[2], v_cache_self_shape[3]];


                ////outputs
                var outputMetadata = _decoderSession.OutputMetadata;

                _outputKeys = outputMetadata.Keys.ToList();
                _outputDimensions = new Dictionary<string, int[]>();

                foreach (var name in _outputKeys)
                {
                    var dimensions = outputMetadata[name].Dimensions;
                    _outputDimensions[name] = dimensions;
                }

                if (_outputDimensions.ContainsKey("output_0") && _outputDimensions["output_0"].Length == 3)
                {
                    logits_shape = _outputDimensions["output_0"];
                    logits = new float[logits_shape[0], logits_shape[1], logits_shape[2]];
                }
                else if (_outputDimensions.ContainsKey("logits") && _outputDimensions["logits"].Length == 3)
                {
                    logits_shape = _outputDimensions["logits"];
                    logits = new float[logits_shape[0], logits_shape[1], logits_shape[2]];
                }
                else
                {
                    throw new Exception("output_0 or logits dimensions are not valid or not found.");
                }

                if (_outputDimensions.ContainsKey("output_1") && _outputDimensions["output_1"].Length == 4)
                {
                    k_cache_shape = _outputDimensions["output_1"];
                    k_cache = new float[k_cache_shape[0], k_cache_shape[1], k_cache_shape[2], k_cache_shape[3]];
                }
                else if (_outputDimensions.ContainsKey("k_cache") && _outputDimensions["k_cache"].Length == 4)
                {
                    k_cache_shape = _outputDimensions["k_cache"];
                    k_cache = new float[k_cache_shape[0], k_cache_shape[1], k_cache_shape[2], k_cache_shape[3]];
                }
                else
                {
                    throw new Exception("output_0 or k_cache dimensions are not valid or not found.");
                }
                if (_outputDimensions.ContainsKey("output_2") && _outputDimensions["output_2"].Length == 4)
                {
                    v_cache_shape = _outputDimensions["output_2"];
                    v_cache = new float[v_cache_shape[0], v_cache_shape[1], v_cache_shape[2], v_cache_shape[3]];
                }
                else if (_outputDimensions.ContainsKey("v_cache") && _outputDimensions["v_cache"].Length == 4)
                {
                    v_cache_shape = _outputDimensions["v_cache"];
                    v_cache = new float[v_cache_shape[0], v_cache_shape[1], v_cache_shape[2], v_cache_shape[3]];
                }
                else
                {
                    throw new Exception("output_2 or v_cache dimensions are not valid or not found.");
                }

                stopwatch.Stop();
                LoggingService.LogDebug($"Initialize Decoder Model Duration: {stopwatch.Elapsed.TotalSeconds} seconds");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error Initializing Decoder Model:", ex);

                throw;
            }
        }

        #endregion

        #region Inference Methods
        /// <summary>
        /// Performs the decoding inference using the provided k_cache_cross and v_cache_cross outputs from the encoder.
        /// </summary>
        /// <param name="k_cache_cross">The k_cache_cross output from the encoder as a 4-dimensional float array.</param>
        /// <param name="v_cache_cross">The v_cache_cross output from the encoder as a 4-dimensional float array.</param>
        /// <returns>A list of decoded tokens as integers.</returns>
        public List<int> DecoderInference(float[,,,] k_cache_cross, float[,,,] v_cache_cross)
        {
            try
            {
                // Ensure the inference session is created
                if (_decoderSession == null)
                {
                    CreateSession();
                }
                var stopwatch = Stopwatch.StartNew();

                // Initialize the input token and decoded tokens list
                int[,] x = new int[,] { { _token_Sot } };
                List<int> decoded_tokens_list = new List<int> { _token_Sot };
                int sampleLen = _meanDecodeLen;

                float[,,] logits;
                float sumLogprobs = 0;

                // Loop through the sample length to perform inference step-by-step
                for (int i = 0; i < sampleLen; i++)
                {
                    int[,] index = new int[,] { { i } };
                    bool _sessionCreated = i != 0;

                    // Run inference and get logits and updated cache values
                    var decoder_out = RunInference(x, index, k_cache_cross, v_cache_cross, k_cache_self, v_cache_self, _decoderSession);

                    logits = decoder_out.Item1;
                    k_cache_self = decoder_out.Item2;
                    v_cache_self = decoder_out.Item3;

                    float[] lastTokenLogits = new float[logits.GetLength(2)];
                    for (int j = 0; j < logits.GetLength(2); j++)
                    {
                        lastTokenLogits[j] = logits[0, logits.GetLength(1) - 1, j];
                    }

                    //Applying filters.
                    if (i == 0)
                    {
                        lastTokenLogits[_token_Eot] = float.NegativeInfinity;
                        lastTokenLogits[_token_Blank] = float.NegativeInfinity;
                    }
                    //SuppressTokens
                    foreach (var token in Constants.NON_SPEECH_TOKENS)
                    {
                        lastTokenLogits[token] = float.NegativeInfinity;
                    }

                    (float[] updatedLogits, float[] logprobs) = ApplyTimestampRules(lastTokenLogits, decoded_tokens_list);

                    if (i == 0)
                    {
                        //detect no_speech
                        var noSpeechProb = Math.Exp(logprobs[_token_NoSpeech]);
                        if (noSpeechProb > _noSpeechThr)
                        {
                            break;
                        }
                    }
                    int nextToken = Array.IndexOf(updatedLogits, updatedLogits.Max());

                    if (nextToken == _token_Eot)
                    {
                        break;
                    }

                    sumLogprobs += logprobs[nextToken];

                    x = new int[,] { { nextToken } };
                    decoded_tokens_list.Add(nextToken);
                }

                stopwatch.Stop();
                LoggingService.LogDebug($"Whisper Decode Inference Duration: {stopwatch.Elapsed.TotalSeconds} seconds");
                return decoded_tokens_list;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error Decoder Inference:", ex);
                throw;
            }

        }

        /// <summary>
        /// Runs the inference on the Whisper decoder model using the provided input tensors and cache values.
        /// </summary>
        /// <param name="x">The input token tensor as a 2-dimensional int array.</param>
        /// <param name="index">The index tensor as a 2-dimensional int array.</param>
        /// <param name="k_cache_cross">The k_cache_cross tensor from the encoder as a 4-dimensional float array.</param>
        /// <param name="v_cache_cross">The v_cache_cross tensor from the encoder as a 4-dimensional float array.</param>
        /// <param name="k_cache_self">The k_cache_self tensor from the decoder as a 4-dimensional float array.</param>
        /// <param name="v_cache_self">The v_cache_self tensor from the decoder as a 4-dimensional float array.</param>
        /// <param name="session">The ONNX inference session.</param>
        /// <returns>A tuple containing the logits, updated k_cache_self, and updated v_cache_self tensors.</returns>
        public (float[,,] output_0, float[,,,] output_1, float[,,,] output_2) RunInference(int[,] x, int[,] index,
        float[,,,] k_cache_cross, float[,,,] v_cache_cross, float[,,,] k_cache_self, float[,,,] v_cache_self, InferenceSession _session)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                // Convert input arrays to tensors

                // 2, dimensional
                var xTensor = new DenseTensor<int>(x.Cast<int>().ToArray(), new[] { x.GetLength(0), x.GetLength(1) });

                var indexTensor = new DenseTensor<int>(index.Cast<int>().ToArray(), new[] { index.GetLength(0), index.GetLength(1) });

                // 4, dimensional
                var kCacheCrossTensor = new DenseTensor<float>(Conversion.Flatten(k_cache_cross), new[] { k_cache_cross.GetLength(0), k_cache_cross.GetLength(1), k_cache_cross.GetLength(2), k_cache_cross.GetLength(3) });

                // 4, dimensional
                var vCacheCrossTensor = new DenseTensor<float>(Conversion.Flatten(v_cache_cross), new[] { v_cache_cross.GetLength(0), v_cache_cross.GetLength(1), v_cache_cross.GetLength(2), v_cache_cross.GetLength(3) });

                // 4, dimensional
                var kCacheSelfTensor = new DenseTensor<float>(Conversion.Flatten(k_cache_self), new[] { k_cache_self.GetLength(0), k_cache_self.GetLength(1), k_cache_self.GetLength(2), k_cache_self.GetLength(3) });

                // 4, dimensional
                var vCacheSelfTensor = new DenseTensor<float>(Conversion.Flatten(v_cache_self), new[] { v_cache_self.GetLength(0), v_cache_self.GetLength(1), v_cache_self.GetLength(2), v_cache_self.GetLength(3) });

                // Prepare the input list for the inference session
                var inputs = new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor("x", xTensor),
                NamedOnnxValue.CreateFromTensor("index", indexTensor),
                NamedOnnxValue.CreateFromTensor("k_cache_cross", kCacheCrossTensor),
                NamedOnnxValue.CreateFromTensor("v_cache_cross", vCacheCrossTensor),
                NamedOnnxValue.CreateFromTensor("k_cache_self", kCacheSelfTensor),
                NamedOnnxValue.CreateFromTensor("v_cache_self", vCacheSelfTensor) };

                // Define the output keys and run option
                List<string> outputs = _outputKeys;
                using var runOptions = new RunOptions();

                // Run the inference session
                var results = _session.Run(inputs, outputs);

                // Extract and convert the output tensors
                var output_0_tensor = results.First(r => r.Name == _outputKeys[0]).AsTensor<float>();
                var output_1_tensor = results.First(r => r.Name == _outputKeys[1]).AsTensor<float>();
                var output_2_tensor = results.First(r => r.Name == _outputKeys[2]).AsTensor<float>();

                var output_0 = Conversion.To3DArray(output_0_tensor, logits_shape[0], logits_shape[1], logits_shape[2]);
                var output_1 = Conversion.To4DArray(output_1_tensor, k_cache_shape[0], k_cache_shape[1], k_cache_shape[2], k_cache_shape[3]);
                var output_2 = Conversion.To4DArray(output_2_tensor, v_cache_shape[0], v_cache_shape[1], v_cache_shape[2], v_cache_shape[3]);

                stopwatch.Stop();
                LoggingService.LogDebug($"Whisper Decode Cache Cross Calculation Duration: {stopwatch.Elapsed.TotalSeconds} seconds");

                // Return the results
                return (output_0, output_1, output_2);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error Decoder RunInference:", ex);
                throw;
            }
        }
        #endregion

        #region Helper Methods
        private (float[], float[]) ApplyTimestampRules(float[] logits, List<int> tokens)
        {
            try
            {
                logits[_token_NoTimestamp] = float.NegativeInfinity;

                var seq = tokens.Skip(_sampleBegin).ToList();
                bool lastWasTimestamp = seq.Count >= 1 && seq.Last() >= _token_TimestampBegin;
                bool penultimateWasTimestamp = seq.Count < 2 || seq[^2] >= _token_TimestampBegin;

                if (lastWasTimestamp)
                {
                    if (penultimateWasTimestamp) // has to be non-timestamp
                    {
                        for (int i = _token_TimestampBegin; i < logits.Length; i++)
                            logits[i] = float.NegativeInfinity;
                    }
                    else // cannot be normal text tokens
                    {
                        for (int i = 0; i < _token_Eot; i++)
                            logits[i] = float.NegativeInfinity;
                    }
                }

                var timestamps = tokens.Where(t => t >= _token_TimestampBegin).ToList();
                if (timestamps.Count > 0)
                {
                    int timestampLast = lastWasTimestamp && !penultimateWasTimestamp ? timestamps.Last() : timestamps.Last() + 1;
                    for (int i = _token_TimestampBegin; i < timestampLast; i++)
                        logits[i] = float.NegativeInfinity;
                }

                if (tokens.Count == _sampleBegin)
                {
                    // Suppress generating non-timestamp tokens at the beginning
                    for (int i = 0; i < _token_TimestampBegin; i++)
                        logits[i] = float.NegativeInfinity;

                    // Apply the `max_initial_timestamp` option
                    int lastAllowed = _token_TimestampBegin + _maxInitialTimestampIndex;
                    for (int i = lastAllowed + 1; i < logits.Length; i++)
                        logits[i] = float.NegativeInfinity;
                }

                // Calculate log probabilities
                float[] logprobs = LogSoftmax(logits);
                float timestampLogprob = LogSumExp(logprobs.Skip(_token_TimestampBegin).ToArray());
                float maxTextTokenLogprob = logprobs.Take(_token_TimestampBegin).Max();

                if (timestampLogprob > maxTextTokenLogprob)
                {
                    // Mask out all but timestamp tokens
                    for (int i = 0; i < _token_TimestampBegin; i++)
                        logits[i] = float.NegativeInfinity;
                }

                return (logits, logprobs);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error Decoder ApplyTimestampRules:", ex);
                throw;
            }
        }

        public static float LogSumExp(float[] logprobs)
        {
            try
            {
                // Find the maximum log probability to use for scaling to avoid numerical instability
                float maxLogProb = logprobs.Max();

                // Compute the scaled sum of exponentials
                double sumExp = logprobs.Select(logprob => Math.Exp(logprob - maxLogProb)).Sum();

                // Return the log of the computed sum plus the scaling factor we subtracted initially
                return (float)(Math.Log(sumExp) + maxLogProb);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error Decoder LogSumExp:", ex);
                throw;
            }
        }

        public static float[] LogSoftmax(float[] logits)
        {
            try
            {
                float maxLogit = logits.Max(); // For numerical stability, subtract the max logit
                float[] exps = new float[logits.Length];
                float sumExps = 0;

                for (int i = 0; i < logits.Length; i++)
                {
                    exps[i] = (float)Math.Exp(logits[i] - maxLogit);
                    sumExps += exps[i];
                }

                float[] logSoftmax = new float[logits.Length];
                for (int i = 0; i < logits.Length; i++)
                {
                    logSoftmax[i] = (float)Math.Log(exps[i] / sumExps);
                }

                return logSoftmax;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error Decoder LogSoftmax:", ex);
                throw;
            }
        }

        public void Dispose()
        {
            _decoderSession?.Dispose();
        }

        #endregion
    }
}
