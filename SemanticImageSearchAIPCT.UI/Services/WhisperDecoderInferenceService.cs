using Microsoft.Maui.Graphics;
using Microsoft.Maui.Primitives;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using SemanticImageSearchAIPCT.UI.Common;

namespace SemanticImageSearchAIPCT.UI.Service
{
    public class WhisperDecoderInferenceService
    {
        private InferenceSession _session;

        private Conversion _conversion;
        public WhisperDecoderInferenceService(string modelPath)
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

            string _decoderModelPath = Path.Combine(_modelDir, "whisper_base_en-whisperdecoder.onnx");

            using var sessionOptions = new SessionOptions();
            var providerName = "QNN";

            //sessionOptions.AppendExecutionProvider(providerName, qnnOptions);

            _session = new InferenceSession(_decoderModelPath, sessionOptions);
            //Debug.WriteLine($"First input name: {_session.InputMetadata.Keys.First()}");
            //foreach (var inputMeta in _session.InputMetadata)
            //{
            //    Debug.WriteLine($"Decoder Key: {inputMeta.Key}");
            //    Debug.WriteLine($"Decoder Dimensions: {string.Join(",", inputMeta.Value.Dimensions)}");
            //    Debug.WriteLine($"Decoder ElementType: {inputMeta.Value.ElementType}");
            //    Debug.WriteLine($"Decoder input is : {inputMeta.Value.Dimensions.Length}, dimensional");

            //}
            //// List and inspect the output metadata
            //Debug.WriteLine("Listing Decoder output metadata:");
            //foreach (var outputMeta in _inferenceSession.OutputMetadata)
            //{
            //    Debug.WriteLine($"Decoder Output _name: {outputMeta.Key}, Decoder Output Type: {outputMeta.Value.ElementType}, Decoder Output Dimensions: {string.Join(", ", outputMeta.Value.Dimensions)}");
            //}


        }

        public (float[,,] output_0, float[,,,] output_1, float[,,,] output_2) RunInference(int[,] x, int[,] index,
            float[,,,] k_cache_cross, float[,,,] v_cache_cross, float[,,,] k_cache_self, float[,,,] v_cache_self)
        {
            // 2, dimensional
            var xTensor = new DenseTensor<int>(x.Cast<int>().ToArray(), new[] { x.GetLength(0), x.GetLength(1) });

            var indexTensor = new DenseTensor<int>(index.Cast<int>().ToArray(), new[] { index.GetLength(0), index.GetLength(1) });

            // 4, dimensional
            var kCacheCrossTensor = new DenseTensor<float>(_conversion.Flatten(k_cache_cross), new[] { k_cache_cross.GetLength(0), k_cache_cross.GetLength(1), k_cache_cross.GetLength(2), k_cache_cross.GetLength(3) });

            // 4, dimensional
            var vCacheCrossTensor = new DenseTensor<float>(_conversion.Flatten(v_cache_cross), new[] { v_cache_cross.GetLength(0), v_cache_cross.GetLength(1), v_cache_cross.GetLength(2), v_cache_cross.GetLength(3) });

            // 4, dimensional
            var kCacheSelfTensor = new DenseTensor<float>(_conversion.Flatten(k_cache_self), new[] { k_cache_self.GetLength(0), k_cache_self.GetLength(1), k_cache_self.GetLength(2), k_cache_self.GetLength(3) });

            // 4, dimensional
            var vCacheSelfTensor = new DenseTensor<float>(_conversion.Flatten(v_cache_self), new[] { v_cache_self.GetLength(0), v_cache_self.GetLength(1), v_cache_self.GetLength(2), v_cache_self.GetLength(3) });


            var inputs = new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor("x", xTensor),
                NamedOnnxValue.CreateFromTensor("index", indexTensor),
                NamedOnnxValue.CreateFromTensor("k_cache_cross", kCacheCrossTensor),
                NamedOnnxValue.CreateFromTensor("v_cache_cross", vCacheCrossTensor),
                NamedOnnxValue.CreateFromTensor("k_cache_self", kCacheSelfTensor),
                NamedOnnxValue.CreateFromTensor("v_cache_self", vCacheSelfTensor) };

            float[,,] output_0 = new float[1, 1, 51864];
            float[,,,] output_1 = new float[6, 8, 64, 224];
            float[,,,] output_2 = new float[6, 8, 224, 64];


            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = null;

            List<string> outputs = new List<string> { "output_0", "output_1", "output_2" };
            using var runOptions = new RunOptions();

            results = _session.Run(inputs, outputs);

            var output_0_tensor = results.First(r => r.Name == "output_0").AsTensor<float>();
            var output_1_tensor = results.First(r => r.Name == "output_1").AsTensor<float>();
            var output_2_tensor = results.First(r => r.Name == "output_2").AsTensor<float>();

            output_0 = _conversion.To3DArray(output_0_tensor, 1, 1, 51864);
            output_1 = _conversion.To4DArray(output_1_tensor, 6, 8, 64, 224);
            output_2 = _conversion.To4DArray(output_2_tensor, 6, 8, 224, 64);
            return (output_0, output_1, output_2);


        }        

    }
}
