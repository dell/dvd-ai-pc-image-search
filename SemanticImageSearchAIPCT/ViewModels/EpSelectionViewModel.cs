using SemanticImageSearchAIPCT.Common;
using SemanticImageSearchAIPCT.Services;
using System.Diagnostics;

namespace SemanticImageSearchAIPCT.ViewModels
{
    partial class EpSelectionViewModel : ObservableObject
    {
        private readonly IClipInferenceService _clipInferenceService;
        private readonly IWhisperEncoderInferenceService _whisperEncoderService;
        private readonly IWhisperDecoderInferenceService _whisperDecoderService;

        [ObservableProperty]
        private EpDropDownOption selectedEp;

        [ObservableProperty]
        private List<EpDropDownOption> epDropDownOptions;

        [ObservableProperty]
        private bool isInferencingReady = false;

        public EpSelectionViewModel()
        {
            _clipInferenceService = ServiceHelper.GetService<IClipInferenceService>();
            _whisperEncoderService = ServiceHelper.GetService<IWhisperEncoderInferenceService>();
            _whisperDecoderService = ServiceHelper.GetService<IWhisperDecoderInferenceService>();

            epDropDownOptions = [
                new EpDropDownOption {Name= "Qualcomm CPU", ExecutionProvider=ExecutionProviders.Cpu},
                new EpDropDownOption {Name= "Qualcomm iNPU", ExecutionProvider=ExecutionProviders.QnnHtp}
            ];
            selectedEp = epDropDownOptions[0];
            OnSelectedEpChanged(selectedEp);
        }

        partial void OnIsInferencingReadyChanged(bool value)
        {
            ApplicationEvents.RaiseInferenceServicesReadyStateChanged(value);
        }

        partial void OnSelectedEpChanged(EpDropDownOption value)
        {
            IsInferencingReady = false;
            if (value != null)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        LoggingService.LogInformation($"Initializing services for {value}");
                        var stopwatch = Stopwatch.StartNew();
                        await _clipInferenceService.SetExecutionProviderAsync(value.ExecutionProvider);
                        await _whisperEncoderService.SetExecutionProviderAsync(value.ExecutionProvider);
                        await _whisperDecoderService.SetExecutionProviderAsync(value.ExecutionProvider);
                        //Task.WaitAll(initClip, initWhisperEncoder, initWhisperDecoder); // no benefit is observed
                        stopwatch.Stop();
                        IsInferencingReady = true;
                        LoggingService.LogDebug($"Changing to {value} services took: {stopwatch.Elapsed.TotalSeconds} seconds.");
                        LoggingService.LogInformation($"{value} services ready for inferencing");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogError($"Error setting execution provider: {ex}", ex);
                    }
                });
            }
        }
    }

    public class EpDropDownOption
    {
        public required string Name;
        public required ExecutionProviders ExecutionProvider;
        public override string ToString()
        {
            return Name;
        }
    }
}
