using Microsoft.ML.OnnxRuntime;
using System.Text.RegularExpressions;
using System.Numerics.Tensors;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Microsoft.ML.OnnxRuntime.Tensors;
using SemanticImageSearchAIPCT.Models;
using SemanticImageSearchAIPCT.Common;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace SemanticImageSearchAIPCT.Services
{
    internal partial class ClipInferenceService : IClipInferenceService, IDisposable
    {
        public event IClipInferenceService.QueryStartedEvent? QueryStarted;
        public event IClipInferenceService.QueryCompletedEvent? QueryCompleted;
        public event IClipInferenceService.ImageProcessingCompletedEvent? ImageProcessingCompleted;
        public event IClipInferenceService.ImageProcessingStatusUpdatedEvent? ImageProcessingStatusUpdated;

        private readonly string BASE_DIRECTORY = AppDomain.CurrentDomain.BaseDirectory;

        [GeneratedRegex(@"\.jpg$|\.png$")]
        private partial Regex ImageExtensionsRegex();

        private readonly Dictionary<Guid, ImageModel> imageModelsById = [];
        private readonly List<Tuple<float, Guid>> querySimilarities = [];
        private readonly Dictionary<string, string> qnnOptions = [];
        private string epName = string.Empty;

        // inference sessions
        private InferenceSession? tokenizeTextSession;
        private InferenceSession? encodeTextSession;
        private InferenceSession? imageEncodeSession;

        private ExecutionProviders executionProvider = ExecutionProviders.Cpu;

        public void SetExecutionProvider(ExecutionProviders ep)
        {
            LoggingService.LogInformation($"Setting Clip Inference EP as {ep}");
            executionProvider = ep;
            imageModelsById.Clear();
            LoggingService.LogInformation($"Cleared images");
            UpdateSessionsOptions();
            CreateSessions();
        }

        public async Task SetExecutionProviderAsync(ExecutionProviders ep)
        {
            await Task.Run(() => { SetExecutionProvider(ep); });
        }

        public async Task GenerateImageEncodingsAsync(string folderPath)
        {
            await Task.Run(() =>
            {
                GenerateImageEncodings(folderPath);
            });
        }

        public async Task CalculateSimilaritiesAsync(string searchQuery)
        {
            await Task.Run(() =>
            {
                CalculateSimilarities(searchQuery);
            });
        }

        public async Task<List<string>> GetTopNResultsAsync(int n, float threshold = 0.5f)
        {
            List<string> imagePaths = [];

            await Task.Run(() =>
            {
                List<Tuple<float, Guid>> topNResults = querySimilarities.Where(i => i.Item1 >= threshold).ToList();
                topNResults = topNResults.OrderByDescending(i => i.Item1).Take(n).ToList();

                foreach (Tuple<float, Guid> tuple in topNResults)
                {
                    LoggingService.LogDebug($"{tuple.Item1}, {imageModelsById[tuple.Item2].Filename}");
                    imagePaths.Add(imageModelsById[tuple.Item2].Filename);
                }

            });

            return imagePaths;
        }

        private void UpdateSessionsOptions()
        {
            qnnOptions.Clear();
            epName = string.Empty;
            switch (executionProvider)
            {
                case ExecutionProviders.QnnCpu:
                    epName = "QNN";
                    qnnOptions["backend_path"] = "QnnCpu.dll";
                    break;
                case ExecutionProviders.QnnHtp:
                    epName = "QNN";
                    qnnOptions["backend_path"] = "QnnHtp.dll";
                    qnnOptions["enable_htp_fp16_precision"] = "1";
                    break;
            }
        }

        public void GenerateImageEncodings(string folderPath)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                imageModelsById.Clear();

                var files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);

                List<string> imageFiles = [];
                foreach (string filename in files)
                {
                    if (ImageExtensionsRegex().IsMatch(filename))
                        imageFiles.Add(filename);
                }

                LoggingService.LogDebug($"Found images {imageFiles.Count}");
                Application.Current?.Dispatcher.Dispatch(() => { ImageProcessingStatusUpdated?.Invoke(0, imageFiles.Count); });
                CalculateImageEncodings(imageFiles);

                stopwatch.Stop();
                LoggingService.LogDebug($"Generate Image Encodings Duration: {stopwatch.Elapsed.TotalSeconds} seconds");
                Application.Current?.Dispatcher.Dispatch(() => { ImageProcessingCompleted?.Invoke(); });
            }

            catch (Exception e)
            {
                LoggingService.LogDebug(folderPath);
                LoggingService.LogDebug(e.Message);
            }
        }

        private void CreateSessions()
        {
            DisposeSessions();
            var stopwatch = Stopwatch.StartNew();

            CreateTokenizeTextSession();
            CreateEncodeTextSession();
            CreateImageEncodeSession();

            stopwatch.Stop();
            LoggingService.LogDebug($"Create Clip Inference Sessions Duration: {stopwatch.Elapsed.TotalSeconds} seconds");
        }

        private int[] TokenizeText(string text)
        {
            if (tokenizeTextSession == null)
            {
                throw new NullReferenceException($"{nameof(tokenizeTextSession)} is not initialized.");
            }

            var stopwatch = Stopwatch.StartNew();
            // Create input tensor from text
            using var inputTensor = OrtValue.CreateTensorWithEmptyStrings(OrtAllocator.DefaultInstance, new long[] { 1 });
            inputTensor.StringTensorSetElementAt(text.AsSpan(), 0);

            LoggingService.LogDebug($"input name {string.Join(" ", tokenizeTextSession.InputNames)}");
            var inputs = new Dictionary<string, OrtValue>
            {
                {  "string_input", inputTensor }
            };

            // Run session and send the input data in to get inference output. 
            using var runOptions = new RunOptions();
            using var tokens = tokenizeTextSession.Run(runOptions, inputs, tokenizeTextSession.OutputNames);

            var inputIds = tokens[0].GetTensorDataAsSpan<long>();

            // Cast inputIds to Int32
            var inputIdsInt = new int[inputIds.Length];
            for (int i = 0; i < inputIds.Length; i++)
            {
                inputIdsInt[i] = (int)inputIds[i];
            }

            LoggingService.LogDebug(string.Join(" ", inputIdsInt));

            var modelMaxLength = 77;
            // Pad array with 49407 until length is modelMaxLength
            if (inputIdsInt.Length < modelMaxLength)
            {
                var pad = Enumerable.Repeat(49407, 77 - inputIdsInt.Length).ToArray();
                inputIdsInt = inputIdsInt.Concat(pad).ToArray();
            }
            return inputIdsInt;
        }

        private void CreateTokenizeTextSession()
        {
            // Create Tokenizer and tokenize the sentence.
            var tokenizerOnnxPath = Path.Combine(BASE_DIRECTORY, "AIModels", "cliptok.onnx");
            using var sessionOptions = new SessionOptions();
            var customOp = "ortextensions.dll";
            sessionOptions.RegisterCustomOpLibraryV2(customOp, out var libraryHandle);

            if (string.IsNullOrEmpty(epName) == false)
            {
                LoggingService.LogDebug($"Running with ep {epName} {qnnOptions?["backend_path"]}");
                sessionOptions.AppendExecutionProvider(epName, qnnOptions);
            }
            else
            {
                LoggingService.LogDebug($"Running with ep Cpu");
            }

            // Create an InferenceSession from the onnx clip tokenizer.
            tokenizeTextSession = new InferenceSession(tokenizerOnnxPath, sessionOptions);
        }

        private ReadOnlySpan<float> TextEncoder(int[] tokenizedInput)
        {
            if (encodeTextSession == null)
            {
                throw new NullReferenceException($"{nameof(encodeTextSession)} is not initialized.");
            }

            using var input_ids = OrtValue.CreateTensorValueFromMemory<int>(tokenizedInput, [1, tokenizedInput.Length]);

            // Pre-allocate the output so it goes to a managed buffer
            // we know the shape: tensor: float32[1,512] via netron examination
            var lastHiddenState = new float[1 * 512];
            using var outputOrtValue = OrtValue.CreateTensorValueFromMemory<float>(lastHiddenState, [1, 512]);

            var input_names = new string[] { "text" };
            var inputs = new OrtValue[] { input_ids };

            var output_names = new List<string> { encodeTextSession.OutputNames[0] };
            var outputs = new List<OrtValue> { outputOrtValue };

            // Run inference.
            using var runOptions = new RunOptions();
            encodeTextSession.Run(runOptions, input_names, inputs, output_names, outputs);

            // Normalize the result 
            // TODO: Create util function
            var resultAsSpan = new ReadOnlySpan<float>(lastHiddenState);
            var normResultAsSpan = new Span<float>(lastHiddenState);
            var normAsSpan = TensorPrimitives.Norm<float>(resultAsSpan);
            TensorPrimitives.Divide<float>(resultAsSpan, normAsSpan, normResultAsSpan);

            return normResultAsSpan;
        }

        private void CreateEncodeTextSession()
        {
            // Create input tensor. OrtValue will not copy, will read from managed memory

            var textEncoderOnnxPath = Path.Combine(BASE_DIRECTORY, "AIModels", "openai_clip-cliptextencoder.onnx");

            using var sessionOptions = new SessionOptions();

            if (string.IsNullOrEmpty(epName) == false)
            {
                sessionOptions.AppendExecutionProvider(epName, qnnOptions);
            }

            encodeTextSession = new InferenceSession(textEncoderOnnxPath, sessionOptions);
        }

        private void CalculateSimilarities(string searchQuery)
        {
            LoggingService.LogDebug($"Finding best match for query {searchQuery}");
            querySimilarities.Clear();
            Application.Current?.Dispatcher.Dispatch(() => { QueryStarted?.Invoke(searchQuery); });
            var stopwatch = Stopwatch.StartNew();

            var textTokens = TokenizeText(searchQuery);
            var textEncodings = TextEncoder(textTokens);

            // Compute text encoding with each image encoding using Cosine similarity
            List<float> similarityResults = [];
            foreach (KeyValuePair<Guid, ImageModel> imageModelEntry in imageModelsById)
            {
                ReadOnlySpan<float> imageFeatureSpan = new ReadOnlySpan<float>(imageModelEntry.Value.Encodings?.ToArray());
                float similarity = TensorPrimitives.CosineSimilarity<float>(textEncodings, imageFeatureSpan);
                querySimilarities.Add(new Tuple<float, Guid>(similarity, imageModelEntry.Key));
            }

            stopwatch.Stop();
            LoggingService.LogDebug($"Calculate Image Similarities Duration: {stopwatch.Elapsed.TotalSeconds} seconds");

            Application.Current?.Dispatcher.Dispatch(() => { QueryCompleted?.Invoke(true); });
        }

        public void CalculateImageEncodings(List<string> filenames)
        {
            if (imageEncodeSession == null)
            {
                throw new NullReferenceException($"{nameof(imageEncodeSession)} is not initialized.");
            }
            var stopwatch = Stopwatch.StartNew();

            // TODO: Fix batch implementation
            // Model currently does not support batching
            //int batchSize = 10;
            var batchSize = 1;
            var numBatches = Convert.ToInt32(Math.Ceiling((decimal)(filenames.Count) / batchSize));
            var fileCount = filenames.Count;

            var resultsArray = new List<System.Numerics.Tensors.Tensor<float>>();

            for (int i = 0; i < numBatches; i++)
            {
                LoggingService.LogInformation($"Processing image batch {i + 1}");
                Application.Current?.Dispatcher.Dispatch(() => { ImageProcessingStatusUpdated?.Invoke(i + 1, fileCount); });
                var batchedImages = filenames.GetRange(i * batchSize, batchSize);
                var processedImage = BatchComputeImageFeatures(batchedImages)[0];
                //combinedFeatureArray = combinedFeatureArray.Concat(BatchComputeImageFeatures(batchedImages)).ToList();
                //ReadOnlySpan<System.Numerics.Tensors.Tensor<float>> combinedFeatureSpan = new ReadOnlySpan<System.Numerics.Tensors.Tensor<float>>(combinedFeatureArray.ToArray());
                //System.Numerics.Tensors.Tensor<float> combinedPreprocessedImages = System.Numerics.Tensors.Tensor.Concatenate(combinedFeatureSpan);

                // Pre-allocate the output so it goes to a managed buffer
                // we know the shape
                //var result = new float[filenames.Count * 512];
                //using var outputOrtValue = OrtValue.CreateTensorValueFromMemory<float>(result, new long[] { filenames.Count, 512 });
                //using var inputOrtValue = OrtValue.CreateTensorValueFromMemory<float>(combinedPreprocessedImages.ToArray(), new long[] { filenames.Count, 3, 224, 224 });
                var result = new float[1 * 512];
                using var outputOrtValue = OrtValue.CreateTensorValueFromMemory<float>(result, [1, 512]);
                using var inputOrtValue = OrtValue.CreateTensorValueFromMemory<float>(processedImage.Encodings?.ToArray(), [1, 3, 224, 224]);

                var input_names = new string[]{ "image" };
                var inputs = new OrtValue[]{ inputOrtValue };

                var output_names = new string[] { "output_0" };
                var outputs = new OrtValue[] { outputOrtValue };

                // Run inference.
                using var runOptions = new RunOptions();
                imageEncodeSession.Run(runOptions, input_names, inputs, output_names, outputs);

                // Normalize the result
                var resultAsSpan = new ReadOnlySpan<float>(result);
                var normResultAsSpan = new Span<float>(result);
                var normAsSpan = TensorPrimitives.Norm<float>(resultAsSpan);
                TensorPrimitives.Divide<float>(resultAsSpan, normAsSpan, normResultAsSpan);

                // Add it to the final list of results
                processedImage.Encodings = normResultAsSpan.ToArray();
                imageModelsById[Guid.NewGuid()] = processedImage;

                resultsArray.Add(normResultAsSpan.ToArray());
            }


            stopwatch.Stop();
            LoggingService.LogDebug($"Calculate Image Encodings Duration: {stopwatch.Elapsed.TotalSeconds} seconds");
            //imageFeatures = resultsArray;
        }
        private void CreateImageEncodeSession()
        {
            var textEncoderOnnxPath = Path.Combine(BASE_DIRECTORY, "AIModels", "openai_clip-clipimageencoder.onnx");

            using var sessionOptions = new SessionOptions();

            if (string.IsNullOrEmpty(epName) == false)
            {
                sessionOptions.AppendExecutionProvider(epName, qnnOptions);
            }

            imageEncodeSession = new InferenceSession(textEncoderOnnxPath, sessionOptions);
        }

        private List<ImageModel> BatchComputeImageFeatures(List<string> imageBatch)
        {
            List<ImageModel> combinedModels = [];

            foreach (string imagePath in imageBatch)
            {
                ImageModel imageModel = new ImageModel();
                System.Numerics.Tensors.Tensor<float> processedImage = PreprocessImage(imagePath);
                imageModel.Filename = imagePath;
                imageModel.Encodings = processedImage;
                combinedModels.Add(imageModel);
            }

            return combinedModels;
        }

        private System.Numerics.Tensors.Tensor<float> PreprocessImage(string imagePath)
        {
            // Load the image as a Rgb24 type
            using Image<Rgb24> image = SixLabors.ImageSharp.Image.Load<Rgb24>(imagePath);
            IImageFormat format = SixLabors.ImageSharp.Image.DetectFormat(imagePath);

            // Resize image to 224 x 224 pixels, as well as performing a center crop
            using Stream imageStream = new MemoryStream();
            image.Mutate(x =>
            {
                x.Resize(new ResizeOptions
                {
                    Size = new SixLabors.ImageSharp.Size(224, 224),
                    Mode = SixLabors.ImageSharp.Processing.ResizeMode.Crop
                });
            });
            image.Save(imageStream, format);

            // We use DenseTensor for multi-dimensional access to populate the image data
            // Mean and stddev taken from https://github.com/openai/CLIP/blob/main/clip/clip.py
            var mean = new[] { 0.48145466f, 0.4578275f, 0.40821073f };
            var stddev = new[] { 0.26862954f, 0.26130258f, 0.27577711f };
            DenseTensor<float> processedImage = new(new[] { 1, 3, 224, 224 });

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    Span<Rgb24> pixelSpan = accessor.GetRowSpan(y);
                    for (int x = 0; x < accessor.Width; x++)
                    {
                        processedImage[0, 0, y, x] = ((pixelSpan[x].R / 255f) - mean[0]) / stddev[0];
                        processedImage[0, 1, y, x] = ((pixelSpan[x].G / 255f) - mean[1]) / stddev[1];
                        processedImage[0, 2, y, x] = ((pixelSpan[x].B / 255f) - mean[2]) / stddev[2];
                    }
                }
            });

            return processedImage.ToArray();
        }

        public void Dispose()
        {
            DisposeSessions();
        }

        private void DisposeSessions()
        {
            tokenizeTextSession?.Dispose();
            tokenizeTextSession = null;
            encodeTextSession?.Dispose();
            encodeTextSession = null;
            imageEncodeSession?.Dispose();
            imageEncodeSession = null;
        }
    }
}
