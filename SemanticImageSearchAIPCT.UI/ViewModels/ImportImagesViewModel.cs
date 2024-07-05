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

namespace SemanticImageSearchAIPCT.UI.ViewModels
{
    public partial class ImportImagesViewModel
    {
        public ImportImagesViewModel() {}

        [RelayCommand]
        async Task PickFolder(CancellationToken cancellationToken)
        {
            var folderPickerResult = await FolderPicker.Default.PickAsync(cancellationToken);
            if (folderPickerResult.IsSuccessful)
            {
                ClipInferenceService.GenerateImageEncodings(folderPickerResult.Folder.Path);
            }
            else
            {
            }
        }
    }
}