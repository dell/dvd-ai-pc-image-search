using CommunityToolkit.Mvvm.Input;
using SemanticImageSearchAIPCT.UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticImageSearchAIPCT.UI.ViewModels
{
    internal partial class QueryResultsViewModel : ObservableObject
    {
        [ObservableProperty]
        private string? currentQueryText;

        public ObservableCollection<string> ImageResults { get; } = [];

        private List<string> imageResults = [];

        public QueryResultsViewModel()
        {
            //ImageResults.CollectionChanged += HandleImageResultsCollectionChanged;
            ClipInferenceService.QueryStarted += ClipInferenceService_QueryStartedEventHandler;
            ClipInferenceService.QueryCompleted += ClipInferenceService_QueryCompletedEventHandler;
        }

        private void ClipInferenceService_QueryCompletedEventHandler(bool success)
        {
            Debug.WriteLine($"received query completed event");
            if (success)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    imageResults = ClipInferenceService.GetTopNResults(2, 0.2f);
                    foreach (var image in imageResults)
                    {
                        ImageResults.Add(image);
                    }
                    //ImageResults = new ObservableCollection<string>(imageResults);
                    Debug.WriteLine($"query completed getting results {string.Join(" ", ImageResults)}");
                });
            }
        }

        private void ClipInferenceService_QueryStartedEventHandler(string query)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                CurrentQueryText = query;
                ImageResults.Clear();
            });
        }
        //private void HandleImageResultsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        //{
        //    OnPropertyChanged(nameof(ImageResults));
        //}
    }
}
