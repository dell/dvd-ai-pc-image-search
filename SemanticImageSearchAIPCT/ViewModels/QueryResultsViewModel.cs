using CommunityToolkit.Mvvm.Input;
using SemanticImageSearchAIPCT.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticImageSearchAIPCT.ViewModels
{
    internal partial class QueryResultsViewModel : ObservableObject
    {
        [ObservableProperty]
        private string? currentQueryText;

        private string? currentQuery;

        public ObservableCollection<ImageResult> ImageResults { get; } = [];

        private readonly IClipInferenceService _clipInferenceService;

        public QueryResultsViewModel()
        {
            _clipInferenceService = ServiceHelper.GetService<IClipInferenceService>();

            _clipInferenceService.QueryStarted += ClipInferenceService_QueryStartedEventHandler;
            _clipInferenceService.QueryCompleted += ClipInferenceService_QueryCompletedEventHandler;
        }

        private void ClipInferenceService_QueryCompletedEventHandler(bool success)
        {
            LoggingService.LogDebug($"received query completed event");
            if (success)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    var imageResults = await _clipInferenceService.GetTopNResultsAsync(3, 0.2f);
                    foreach (var image in imageResults)
                    {
                        ImageResults.Add(new ImageResult(image));
                    }

                    CurrentQueryText = $"Showing results for: '{currentQuery}'";
                    LoggingService.LogDebug($"query completed getting results {string.Join(" ", ImageResults.Select(x => x.FileName))}");
                });
            }
        }

        private void ClipInferenceService_QueryStartedEventHandler(string query)
        {
            currentQuery = query;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                CurrentQueryText = "";
                ImageResults.Clear();
            });
        }
    }

    public partial class ImageResult: ObservableObject
    {
        [ObservableProperty]
        public string filePath;
        [ObservableProperty]
        public string fileName;

        public ImageResult(string _filePath)
        {
            filePath = _filePath;
            fileName = Path.GetFileName(_filePath);
        }
    }
}
