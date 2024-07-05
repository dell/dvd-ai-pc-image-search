using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Numerics.Tensors;
using System.Threading.Tasks;
using System.Diagnostics;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Microsoft.ML.OnnxRuntime.Tensors;
using SemanticImageSearchAIPCT.UI.Models;
using SemanticImageSearchAIPCT.UI.Common;
using Microsoft.UI.Xaml;

namespace SemanticImageSearchAIPCT.UI.Services
{
    //TODO: Convert to singleton
    internal partial class ClipInferenceService
    {
        public delegate void QueryStartedEvent(string query);
        public delegate void QueryCompletedEvent(bool success);

        public static event QueryStartedEvent? QueryStarted;
        public static event QueryCompletedEvent? QueryCompleted;

        private static readonly string BASE_DIRECTORY = AppDomain.CurrentDomain.BaseDirectory;

        [GeneratedRegex(@"\.jpg$|\.png$")]
        private static partial Regex ImageExtensionsRegex();

        //private static List<System.Numerics.Tensors.Tensor<float>> imageFeatures = [];
        private static Dictionary<Guid, ImageModel> imageModelsById = [];
        private static List<Tuple<float, Guid>> querySimilarities = [];
        private static Dictionary<string, string> qnnOptions = new() { { "backend_path", "QnnHtp.dll" } };

        private static int datasetSize = 0;
        private static ExecutionProviders executionProvider = ExecutionProviders.Cpu;

        public static void SetExecutionProvider(ExecutionProviders ep)
        {
            Debug.WriteLine($"Setting ep as {ep}");
            executionProvider = ep;
        }

        public static (string? epName, Dictionary<string, string>? epOptions) UpdateSessionsOptions()
        {
            switch (executionProvider)
            {
                case ExecutionProviders.QnnCpu:
                    var epName = "QNN";
                    qnnOptions["backend_path"] = "QnnCpu.dll";
                    var epOptions = qnnOptions;
                    return (epName, epOptions);
                case ExecutionProviders.QnnHtp:
                    epName = "QNN";
                    qnnOptions["backend_path"] = "QnnHtp.dll";
                    epOptions = qnnOptions;
                    return (epName, epOptions);
                default:
                    return (null, null);
            }
        }

        public static void GenerateImageEncodings(string folderPath)
        {
            try
            {
                imageModelsById.Clear();

                var files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);

                List<string> imageFiles = [];
                foreach (string filename in files)
                {
                    if (ImageExtensionsRegex().IsMatch(filename))
                        imageFiles.Add(filename);
                }

                Debug.WriteLine($"Found images {imageFiles.Count}");
                datasetSize = imageFiles.Count;
                CalculateImageEncodings(imageFiles);

                //CalculateSimilarities("a bag of chips");
                //List<string> paths = GetTopNResults(2, 0.2f);
                //Debug.WriteLine($"top 2 results {string.Join(' ', paths)}");
            }

            catch (Exception e)
            {
                Debug.WriteLine(folderPath);
                Debug.WriteLine(e);
            }
        }

        public static int[] TokenizeText(string text)
        {
            // Create Tokenizer and tokenize the sentence.
            var tokenizerOnnxPath = BASE_DIRECTORY + ("\\AIModels\\cliptok.onnx");

            // Create session options for custom op of extensions
            using var sessionOptions = new SessionOptions();
            var customOp = "ortextensions.dll";
            sessionOptions.RegisterCustomOpLibraryV2(customOp, out var libraryHandle);
            var (epName, epOptions) = UpdateSessionsOptions();

            if (epName != null)
            {
                Debug.WriteLine($"Running with ep {epName} {epOptions?["backend_path"]}");
                sessionOptions.AppendExecutionProvider(epName, epOptions);
            }

            else
            {
                Debug.WriteLine($"Running with ep Cpu");
            }

            // Create an InferenceSession from the onnx clip tokenizer.
            using var tokenizeSession = new InferenceSession(tokenizerOnnxPath, sessionOptions);

            // Create input tensor from text
            using var inputTensor = OrtValue.CreateTensorWithEmptyStrings(OrtAllocator.DefaultInstance, new long[] { 1 });
            inputTensor.StringTensorSetElementAt(text.AsSpan(), 0);

            Debug.WriteLine($"input name {String.Join(" ", tokenizeSession.InputNames)}");
            var inputs = new Dictionary<string, OrtValue>
            {
                {  "string_input", inputTensor }
            };

            // Run session and send the input data in to get inference output. 
            using var runOptions = new RunOptions();
            using var tokens = tokenizeSession.Run(runOptions, inputs, tokenizeSession.OutputNames);

            var inputIds = tokens[0].GetTensorDataAsSpan<long>();

            // Cast inputIds to Int32
            var InputIdsInt = new int[inputIds.Length];
            for (int i = 0; i < inputIds.Length; i++)
            {
                InputIdsInt[i] = (int)inputIds[i];
            }

            Debug.WriteLine(String.Join(" ", InputIdsInt));

            var modelMaxLength = 77;
            // Pad array with 49407 until length is modelMaxLength
            if (InputIdsInt.Length < modelMaxLength)
            {
                var pad = Enumerable.Repeat(49407, 77 - InputIdsInt.Length).ToArray();
                InputIdsInt = InputIdsInt.Concat(pad).ToArray();
            }
            return InputIdsInt;
        }

        public static ReadOnlySpan<float> TextEncoder(int[] tokenizedInput)
        {
            // Create input tensor. OrtValue will not copy, will read from managed memory
            using var input_ids = OrtValue.CreateTensorValueFromMemory<int>(tokenizedInput, new long[] { 1, tokenizedInput.Count() });

            var textEncoderOnnxPath = BASE_DIRECTORY + ("\\AIModels\\openai_clip-cliptextencoder.onnx");

            using var sessionOptions = new SessionOptions();
            var (epName, epOptions) = UpdateSessionsOptions();

            if (epName != null)
            {
                sessionOptions.AppendExecutionProvider(epName, epOptions);
            }

            using var encodeSession = new InferenceSession(textEncoderOnnxPath, sessionOptions);

            // Pre-allocate the output so it goes to a managed buffer
            // we know the shape
            var lastHiddenState = new float[1 * 512];
            using var outputOrtValue = OrtValue.CreateTensorValueFromMemory<float>(lastHiddenState, new long[] { 1, 512 });
            string[] input_names = { "text" };
            OrtValue[] inputs = { input_ids };

            string[] output_names = { encodeSession.OutputNames[0] };
            OrtValue[] outputs = { outputOrtValue };

            // Run inference.
            using var runOptions = new RunOptions();
            encodeSession.Run(runOptions, input_names, inputs, output_names, outputs);

            // Normalize the result 
            // TODO: Create util function
            ReadOnlySpan<float> resultAsSpan = new ReadOnlySpan<float>(lastHiddenState);
            Span<float> normResultAsSpan = new Span<float>(lastHiddenState);
            float normAsSpan = TensorPrimitives.Norm<float>(resultAsSpan);
            TensorPrimitives.Divide<float>(resultAsSpan, normAsSpan, normResultAsSpan);

            return normResultAsSpan;
        }

        public static void CalculateSimilarities(string searchQuery)
        {
            Debug.WriteLine($"Finding best match for query {searchQuery}");
            querySimilarities.Clear();
            QueryStarted?.Invoke(searchQuery);
            var textTokens = TokenizeText(searchQuery);
            var textEncodings = TextEncoder(textTokens);

            // Compute text encoding with each image encoding using Cosine similarity
            List<float> similarityResults = [];
            //foreach (var imageFeature in imageFeatures)
            //{
            //    ReadOnlySpan<float> imageFeatureSpan = new ReadOnlySpan<float>(imageFeature.ToArray());
                
            //    float similarity = TensorPrimitives.CosineSimilarity<float>(textEncodings, imageFeatureSpan);
            //    similarityResults.Add(similarity);
            //}
            foreach (KeyValuePair<Guid, ImageModel> imageModelEntry in imageModelsById)
            {
                ReadOnlySpan<float> imageFeatureSpan = new ReadOnlySpan<float>(imageModelEntry.Value.Encodings?.ToArray());
                float similarity = TensorPrimitives.CosineSimilarity<float>(textEncodings, imageFeatureSpan);
                querySimilarities.Add(new Tuple<float, Guid>(similarity, imageModelEntry.Key));
            }
            QueryCompleted?.Invoke(true);
        }

        public static List<string> GetTopNResults(int n, float threshold = 0.5f)
        {
            //querySimilarities.Sort((x, y) => y.Item1.CompareTo(x.Item1));
            List<Tuple<float, Guid>> topNResults = querySimilarities.Where(i => i.Item1 >= threshold).ToList();
            topNResults = topNResults.OrderByDescending(i => i.Item1).Take(n).ToList();
            List<string> imagePaths = [];

            foreach (Tuple<float, Guid> tuple in topNResults)
            {
                Debug.WriteLine($"{tuple.Item1}, {imageModelsById[tuple.Item2].Filename}");
                imagePaths.Add(imageModelsById[tuple.Item2].Filename);
            }

            return imagePaths;
        }

        public static void CalculateImageEncodings(List<string> filenames)
        {
            var textEncoderOnnxPath = BASE_DIRECTORY + ("\\AIModels\\openai_clip-clipimageencoder.qdq.onnx");
            // Model currently does not support batching
            //int batchSize = 10;
            int batchSize = 1;
            int numBatches = Convert.ToInt32(Math.Ceiling((decimal) (filenames.Count) / batchSize));

            // TODO: Fix batch implementation
            //List<System.Numerics.Tensors.Tensor<float>> combinedFeatureArray = [];
            List<System.Numerics.Tensors.Tensor<float>> resultsArray = [];

            using var sessionOptions = new SessionOptions();
            var (epName, epOptions) = UpdateSessionsOptions();

            if (epName != null)
            {
                sessionOptions.AppendExecutionProvider(epName, epOptions);
            }

            using var imageEncodeSession = new InferenceSession(textEncoderOnnxPath, sessionOptions);

            for (int i = 0; i < numBatches; i++)
            {
                Debug.WriteLine($"Processing batch {i+1}");
                List<string> batchedImages = filenames.GetRange(i * batchSize, batchSize);
                ImageModel processedImage = BatchComputeImageFeatures(batchedImages)[0];
                //combinedFeatureArray = combinedFeatureArray.Concat(BatchComputeImageFeatures(batchedImages)).ToList();
                //ReadOnlySpan<System.Numerics.Tensors.Tensor<float>> combinedFeatureSpan = new ReadOnlySpan<System.Numerics.Tensors.Tensor<float>>(combinedFeatureArray.ToArray());
                //System.Numerics.Tensors.Tensor<float> combinedPreprocessedImages = System.Numerics.Tensors.Tensor.Concatenate(combinedFeatureSpan);

                // Pre-allocate the output so it goes to a managed buffer
                // we know the shape
                //var result = new float[filenames.Count * 512];
                var result = new float[1 * 512];
                using var outputOrtValue = OrtValue.CreateTensorValueFromMemory<float>(result, new long[] { 1, 512 });
                //using var outputOrtValue = OrtValue.CreateTensorValueFromMemory<float>(result, new long[] { filenames.Count, 512 });
                //using var inputOrtValue = OrtValue.CreateTensorValueFromMemory<float>(combinedPreprocessedImages.ToArray(), new long[] { filenames.Count, 3, 224, 224 });
                using var inputOrtValue = OrtValue.CreateTensorValueFromMemory<float>(processedImage.Encodings?.ToArray(), new long[] { 1, 3, 224, 224 });

                string[] input_names = { "image" };
                OrtValue[] inputs = { inputOrtValue };

                string[] output_names = { "output_0" };
                OrtValue[] outputs = { outputOrtValue };

                // Run inference.
                using var runOptions = new RunOptions();
                imageEncodeSession.Run(runOptions, input_names, inputs, output_names, outputs);

                // Normalize the result
                ReadOnlySpan<float> resultAsSpan = new ReadOnlySpan<float>(result);
                Span<float> normResultAsSpan = new Span<float>(result);
                float normAsSpan = TensorPrimitives.Norm<float>(resultAsSpan);
                TensorPrimitives.Divide<float>(resultAsSpan, normAsSpan, normResultAsSpan);

                // Add it to the final list of results
                processedImage.Encodings = normResultAsSpan.ToArray();
                Guid imageGuid = Guid.NewGuid();
                imageModelsById[imageGuid] = processedImage;

                resultsArray.Add(normResultAsSpan.ToArray());
            }

            //imageFeatures = resultsArray;
        }

        public static List<ImageModel> BatchComputeImageFeatures(List<string> imageBatch)
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

        public static System.Numerics.Tensors.Tensor<float> PreprocessImage(string imagePath)
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
            Debug.WriteLine($"processed image rank is {processedImage.Dimensions[0]}, {processedImage.Dimensions[1]}, {processedImage.Dimensions[2]}, {processedImage.Dimensions[3]}");
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
    }
}
