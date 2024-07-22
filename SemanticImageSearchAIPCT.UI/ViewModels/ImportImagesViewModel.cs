using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SemanticImageSearchAIPCT.UI.Services;
using System.Diagnostics;

namespace SemanticImageSearchAIPCT.UI.ViewModels
{
    public partial class ImportImagesViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool isProcessing;

        private readonly IClipInferenceService _clipInferenceService;

        public ImportImagesViewModel() 
        {
            _clipInferenceService = ServiceHelper.GetService<IClipInferenceService>();
            _clipInferenceService.ImageProcessingCompleted += _clipInferenceService_ImageProcessingCompletedEventhandler;
        }

        //~ImportImagesViewModel()
        //{
        //    _clipInferenceService.ImageProcessingCompleted -= _clipInferenceService_ImageProcessingCompletedEventhandler;
        //}

        private void _clipInferenceService_ImageProcessingCompletedEventhandler()
        {
            Debug.WriteLine("Processing complete");
            IsProcessing = false;
        }

        [RelayCommand]
        async Task PickFolder(CancellationToken cancellationToken)
        {
            var folderPickerResult = await FolderPicker.Default.PickAsync(cancellationToken);

            if (folderPickerResult.IsSuccessful)
            {
                IsProcessing = true;
                //await _clipInferenceService.GenerateImageEncodingsAsync(folderPickerResult.Folder.Path);
                //_clipInferenceService.GenerateImageEncodings(folderPickerResult.Folder.Path);
                Task.Run(() => { _clipInferenceService.GenerateImageEncodings(folderPickerResult.Folder.Path); });
            }
            else
            {
            }
        }
    }
}