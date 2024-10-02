using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.Input;
using SemanticImageSearchAIPCT.Services;
using SemanticImageSearchAIPCT.Common;

namespace SemanticImageSearchAIPCT.ViewModels
{
    public partial class ImportImagesViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEnabled))]
        private bool isProcessing = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEnabled))]
        private bool isInferenceReady = false;

        public bool IsEnabled
        {
            get { return !IsProcessing && IsInferenceReady; }
        }

        private string currentImageFolder;
        private readonly IClipInferenceService _clipInferenceService;

        public ImportImagesViewModel()
        {
            _clipInferenceService = ServiceHelper.GetService<IClipInferenceService>();
            _clipInferenceService.ImageProcessingCompleted += OnImageProcessingCompleted;
            ApplicationEvents.InferenceServicesReadyStateChanged += OnInferenceServicesReadyStateChanged;
        }

        private void OnInferenceServicesReadyStateChanged(object? sender, bool e)
        {
            IsInferenceReady = e;
            if (IsInferenceReady)
                GenerateImageEncodings();
        }

        ~ImportImagesViewModel()
        {
            _clipInferenceService.ImageProcessingCompleted -= OnImageProcessingCompleted;
            ApplicationEvents.InferenceServicesReadyStateChanged -= OnInferenceServicesReadyStateChanged;
        }

        private void OnImageProcessingCompleted()
        {
            LoggingService.LogDebug("Processing complete");
            IsProcessing = false;
        }

        [RelayCommand]
        async Task PickFolder(CancellationToken cancellationToken)
        {
            var folderPickerResult = await FolderPicker.Default.PickAsync(cancellationToken);

            if (folderPickerResult.IsSuccessful)
            {
                currentImageFolder = folderPickerResult.Folder.Path;
                GenerateImageEncodings();
            }
        }

        private void GenerateImageEncodings()
        {
            if (string.IsNullOrWhiteSpace(currentImageFolder) == false && Directory.Exists(currentImageFolder))
            {
                IsProcessing = true;
                Task.Run(() => { _clipInferenceService.GenerateImageEncodings(currentImageFolder); });
            }
        }
    }
}