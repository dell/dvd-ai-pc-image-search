using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using SemanticImageSearchAIPCT.UI.Services;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.Maui.Graphics;
using System.Data;

namespace SemanticImageSearchAIPCT.UI.ViewModels
{
    internal partial class QueryViewModel : ObservableObject
    {
        [ObservableProperty]
        private string? queryText;

        [ObservableProperty]
        private string? currentQueryText;

        public ObservableCollection<string> ImageResults { get; private set; } = [];

        private List<string> imageResults = [];

        public QueryViewModel()
        {
            //ImageResults.CollectionChanged += HandleImageResultsCollectionChanged;
        }

        [RelayCommand]
        public void StartQuery()
        {
            Debug.WriteLine($"Starting query with {QueryText}");
            if (QueryText == null)
            {
                return;
            }

            ClipInferenceService.CalculateSimilarities(QueryText);
        }

        [RelayCommand]
        public void TextChanged(TextChangedEventArgs text)
        {
            Debug.WriteLine($"Txt changed {text.OldTextValue}, {text.NewTextValue}, {QueryText}");
        }
    }
}
