using SemanticImageSearchAIPCT.Common;
using SemanticImageSearchAIPCT.Models;
using System.Numerics.Tensors;
using System.Text.RegularExpressions;

namespace SemanticImageSearchAIPCT.Services
{
    public interface IClipInferenceService
    {
        public delegate void QueryStartedEvent(string query);
        public delegate void QueryCompletedEvent(bool success);
        public delegate void ImageProcessingCompletedEvent();
        public delegate void ImageProcessingStatusUpdatedEvent(int current, int total);

        event QueryStartedEvent QueryStarted;
        event QueryCompletedEvent QueryCompleted;
        event ImageProcessingCompletedEvent ImageProcessingCompleted;
        event ImageProcessingStatusUpdatedEvent ImageProcessingStatusUpdated;

        void SetExecutionProvider(ExecutionProviders ep);
        Task SetExecutionProviderAsync(ExecutionProviders ep);
        void GenerateImageEncodings(string folderPath);
        Task GenerateImageEncodingsAsync(string folderPath);
        Task CalculateSimilaritiesAsync(string searchQuery);
        Task<List<string>> GetTopNResultsAsync(int n, float threshold = 0.5f);
    }
}
