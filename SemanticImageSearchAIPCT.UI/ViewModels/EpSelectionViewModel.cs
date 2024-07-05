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
        [ObservableProperty]
        private string selectedEp = "Cpu";

        [RelayCommand]
        public void SetEp()
        {
            Debug.WriteLine($"Ep set to {SelectedEp}");

            try
            {
                Enum.TryParse(SelectedEp, false, out ExecutionProviders ep);
                ClipInferenceService.SetExecutionProvider(ep);
            }

            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing ep option {ex}");
            }
        }
}
}
