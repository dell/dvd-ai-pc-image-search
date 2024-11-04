using SemanticImageSearchAIPCT.Common;

namespace SemanticImageSearchAIPCT.Services
{
    public static class ModelCacheHelper
    {
        private static readonly string BASE_DIRECTORY = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AIModels");

        public static ModelCacheResult GetModelOrCachePath(ExecutionProviders ep, string modelPath)
        {
            modelPath = Path.Combine(BASE_DIRECTORY, modelPath);
            if (ep == ExecutionProviders.QnnCpu || ep == ExecutionProviders.QnnHtp)
            {
                var modelPathNoExt = Path.GetFileNameWithoutExtension(modelPath);
                var modelPathForCached = Path.Combine(BASE_DIRECTORY, $"{modelPathNoExt}.{ep}.onnx_ctx.onnx");
                if (File.Exists(modelPathForCached))
                {
                    return new ModelCacheResult(modelPath, modelPathForCached, true);
                }
                return new ModelCacheResult(modelPath, modelPathForCached, false);
            }
            return new ModelCacheResult(modelPath, modelPath, false);
        }
    }

    public struct ModelCacheResult
    {
        public readonly string ResolvedModelPath;
        public readonly string OriginalModelPath;
        public readonly bool IsCachedVersion;

        public ModelCacheResult(string originalModelPath, string resolvedModelPath, bool isCachedVersion)
        {
            OriginalModelPath = originalModelPath;
            ResolvedModelPath = resolvedModelPath;
            IsCachedVersion = isCachedVersion;
        }

        public readonly string CurrentModelPath => IsCachedVersion ? ResolvedModelPath : OriginalModelPath;
    }
}
