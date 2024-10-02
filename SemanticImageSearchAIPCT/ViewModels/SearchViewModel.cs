
using CommunityToolkit.Mvvm.Input;
using LogMelSpectrogramCS;
using SemanticImageSearchAIPCT.Audio;
using SemanticImageSearchAIPCT.Common;
using SemanticImageSearchAIPCT.Services;
using SemanticImageSearchAIPCT.Tokenizer;

namespace SemanticImageSearchAIPCT.ViewModels
{
    public partial class SearchViewModel : ObservableObject
    {
        #region Fields
        private static readonly int _batchSize = 1;
        private static readonly int _targetFrames = 3000;
        private static readonly int _melChannels = 80; // The number of Mel features per audio context

        public ICommand MicCommand { get; }

        private readonly AudioService _audioService;
        private readonly IClipInferenceService _clipInferenceService;
        private readonly IWhisperEncoderInferenceService _whisperEncoderService;
        private readonly IWhisperDecoderInferenceService _whisperDecoderService;

        private int _sampleRate; // Sample rate in Hz
        private int _numChannels;    // Mono audio  
        private int _bitDepth;
        private LogMelSpectrogramCSBinding _logMelSpectrogram;
        private Tokenizer.Tokenizer _tokenizer;
        private string _inputBuffer;

        #endregion

        #region Constructor
        public SearchViewModel()
        {
            _audioService = new AudioService();

            _sampleRate = _audioService.GetSampleRate();
            _numChannels = _audioService.GetChannels();
            _bitDepth = _audioService.GetBitDepth();

            var melBinFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "mel_80.bin");
            _logMelSpectrogram = new LogMelSpectrogramCSBinding(melBinFilePath);
            _tokenizer = TokenizerFactory.GetTokenizer(false, 99, "en", "transcribe");
            _audioService.AudioDataAvailable += OnAudioDataAvailable;
            _audioService.IsRecordingChanged += OnIsRecordingChanged;
            MicCommand = new AsyncRelayCommand(StartOrStopRecordingAsync);

            _clipInferenceService = ServiceHelper.GetService<IClipInferenceService>();
            _whisperEncoderService = ServiceHelper.GetService<IWhisperEncoderInferenceService>();
            _whisperDecoderService = ServiceHelper.GetService<IWhisperDecoderInferenceService>();

            ApplicationEvents.InferenceServicesReadyStateChanged += OnInferenceServicesReadyStateChanged;
        }

        ~SearchViewModel()
        {
            ApplicationEvents.InferenceServicesReadyStateChanged -= OnInferenceServicesReadyStateChanged;
            _audioService.AudioDataAvailable -= OnAudioDataAvailable;
        }

        #endregion

        #region Properties
        [ObservableProperty]
        private string? queryText;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEnabled))]
        private bool isRecording;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEnabled))]
        private bool isInferenceReady = false;

        public bool IsEnabled
        {
            get { return !IsRecording && IsInferenceReady; }
        }

        #endregion

        #region Commands
        [RelayCommand]
        public async Task StartQuery()
        {
            if (!IsRecording)
            {
                LoggingService.LogDebug($"Starting query with {QueryText}");
                if (!string.IsNullOrEmpty(QueryText))
                {
                    await _clipInferenceService.CalculateSimilaritiesAsync(QueryText);
                }
            }
        }

        #endregion

        #region Command Execution Logic

        private async Task StartOrStopRecordingAsync()
        {
            try
            {
                await _audioService.Start_Stop_Recording();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error while starting or stopping recording:", ex);
            }
        }

        #endregion

        #region Event Handlers
        private void OnInferenceServicesReadyStateChanged(object? sender, bool value)
        {
            IsInferenceReady = value;
        }

        private void OnAudioDataAvailable(object? sender, byte[] audioData)
        {
            ProcessAudio(audioData);
        }

        private void OnIsRecordingChanged(object? sender, bool value)
        {
            if (IsRecording != value)
            {
                async void exec()
                {
                    IsRecording = value;
                    if (IsRecording)
                    {
                        _inputBuffer = string.Empty;
                    }
                    else
                    {
                        QueryText = _inputBuffer;
                        await StartQuery();
                    }
                    (MicCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
                }
                if (MainThread.IsMainThread)
                {
                    exec();
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(exec);
                }
            }
        }
        #endregion

        #region Methods

        /// <summary>
        /// Processes the audio chunk, converts it to a log Mel spectrogram, and runs inference using Whisper models encoder and decoder.
        /// Converts the decoder output of the tokens into text and updates the query with text for OpneAi Clip similaritie.
        /// After updating the UI with text, enables the Mic button.

        /// Batch size: 1 (Number of sequences processed simultaneously)
        /// Number of channels: 80 (Number of frames or time steps in the spectrogram)
        /// Number of samples: 3000 (Number of frequency bins per frame)
        /// The product of these dimensions is 1×80×3000=2,40,000
        /// </summary>
        /// <param name="audioChunk">The byte array of the audio chunk to process.</param>
        /// 
        private void ProcessAudio(byte[] audioChunk)
        {
            try
            {
                int expectedLength = _batchSize * _melChannels * _targetFrames;
                int bytesPerSample = _bitDepth / 8; // Convert bit depth to bytes
                int numSamples = audioChunk.Length / bytesPerSample / _numChannels;
                double durationInSeconds = (double)numSamples / _sampleRate;

                LoggingService.LogDebug($"Audio chunk duration: {durationInSeconds} seconds");

                // Convert the byte array audio chunk to a float array with the expected length
                float[] float_audioData = _audioService.ConvertBytesToFloatArray(audioChunk, expectedLength);
                List<float> floatAudioDataList = float_audioData.ToList();

                // Generate a log Mel spectrogram from the audio data
                List<float> result_logMelSpectrogram = _logMelSpectrogram.load_audio_chunk(floatAudioDataList);

                float[] inputArray = result_logMelSpectrogram.ToArray();

                // Run inference on the Whisper encoder model to get the k_cache_cross and v_cache_cross outputs
                LoggingService.LogDebug("Begin Whisper encoding inference");
                var (k_cache_cross, v_cache_cross) = _whisperEncoderService.RunInference(inputArray, _batchSize, 80, _targetFrames);

                // Run inference on the Whisper decoder model to get the decoded tokens
                LoggingService.LogDebug("Begin Whisper decoding inference");
                var decoded_tokens_list = _whisperDecoderService.DecoderInference(k_cache_cross, v_cache_cross);

                // If decoded tokens are available, decode them into text and update the query text
                LoggingService.LogDebug("Begin decoding tokens");
                if (decoded_tokens_list.Count > 0)
                {
                    var text = _tokenizer.Decode(decoded_tokens_list.Skip(1).ToList());
                    LoggingService.LogDebug("Processed the audio chunk to: " + text);
                    // Update the query text with the decoded text
                    _inputBuffer += text;
                }

            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error processing recording:", ex);
            }
        }

        #endregion

        #region IDisposable Implementation
        public async void Dispose()
        {
            await _audioService.Start_Stop_Recording();
        }
        #endregion
    }
}


