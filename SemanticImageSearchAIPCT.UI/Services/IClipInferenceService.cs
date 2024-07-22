using SemanticImageSearchAIPCT.UI.Common;
using SemanticImageSearchAIPCT.UI.Models;
using System.Numerics.Tensors;
using System.Text.RegularExpressions;

namespace SemanticImageSearchAIPCT.UI.Services
{
    public interface IClipInferenceService
    {
        public delegate void QueryStartedEvent(string query);
        public delegate void QueryCompletedEvent(bool success);
        public delegate void ImageProcessingCompletedEvent();

        event QueryStartedEvent QueryStarted;
        event QueryCompletedEvent QueryCompleted;
        event ImageProcessingCompletedEvent ImageProcessingCompleted;

        void SetExecutionProvider(ExecutionProviders ep);
        void GenerateImageEncodings(string folderPath);
        Task GenerateImageEncodingsAsync(string folderPath);
        Task CalculateSimilaritiesAsync(string searchQuery);
        Task<List<string>> GetTopNResultsAsync(int n, float threshold = 0.5f);
    }
}
