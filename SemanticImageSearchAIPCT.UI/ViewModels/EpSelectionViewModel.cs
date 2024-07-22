using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using SemanticImageSearchAIPCT.UI.Services;
using SemanticImageSearchAIPCT.UI.Common;
 

namespace SemanticImageSearchAIPCT.UI.ViewModels
{
    partial class EpSelectionViewModel : ObservableObject
    {
        private readonly IClipInferenceService _clipInferenceService;       
        private readonly IWhisperEncoderInferenceService _WhisperEncoderService;
        private readonly IWhisperDecoderInferenceService _WhisperDecoderService;

        [ObservableProperty]
        private string selectedEp = "Cpu";

        public EpSelectionViewModel()
        {
            _clipInferenceService = ServiceHelper.GetService<IClipInferenceService>();
            _WhisperEncoderService = ServiceHelper.GetService<IWhisperEncoderInferenceService>();
            _WhisperDecoderService = ServiceHelper.GetService<IWhisperDecoderInferenceService>();
        }

        [RelayCommand]
        public void SetEp()
        {
            Debug.WriteLine($"Ep set to {SelectedEp}");

            try
            {
                Enum.TryParse(SelectedEp, false, out ExecutionProviders ep);
                _clipInferenceService.SetExecutionProvider(ep);
                _WhisperEncoderService.SetExecutionProvider(ep);
                _WhisperDecoderService.SetExecutionProvider(ep);
            }

            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing ep option {ex}");
            }
        }
}
}
