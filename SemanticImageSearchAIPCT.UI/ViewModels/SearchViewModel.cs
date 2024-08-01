
using CommunityToolkit.Mvvm.Input;
using LogMelSpectrogramCS;
using SemanticImageSearchAIPCT.UI.Audio;
using SemanticImageSearchAIPCT.UI.Services;
using SemanticImageSearchAIPCT.UI.Tokenizer;
using System.Diagnostics;

namespace SemanticImageSearchAIPCT.UI.ViewModels
{
    public partial class SearchViewModel : ObservableObject
    {
        #region Fields
        private string _modelDir;
        private string _baseDir;
        private string _decoderModelPath;
        private string _encoderModelPath;
        private string _vocabFilePath;
        private string _recordedFilePath;
        private string _modelassetsDir;
        private string _melBinFilePath;

        private int _sampleRate; // Sample rate in Hz
        private int _numChannels;    // Mono audio  
        private static readonly int _batchSize = 1;
        private static readonly int _targetFrames = 3000;
        private static readonly int _melChannels = 80; // The number of Mel features per audio context
        private static readonly int _fftSize = 400;//Length of the Hann window signal used when applying a FFT to the audio.
        private static readonly int _hopSize = 160;
        private bool _isBusy = false;
        private int _bitDepth;

       
        public ICommand MicCommand { get; }

        private readonly AudioService _audioService;  
        private LogMelSpectrogramCSBinding _logMelSpectrogram;
        private SemanticImageSearchAIPCT.UI.Tokenizer.Tokenizer _tokenizer;
        private readonly IClipInferenceService _clipInferenceService;
        private readonly IWhisperEncoderInferenceService _whisperEncoderService;
        private readonly IWhisperDecoderInferenceService _whisperDecoderService;

        #endregion

        #region Constructor
        public SearchViewModel()
        {
            _audioService = new AudioService(this);

            _sampleRate = _audioService.GetSampleRate();
            _numChannels = _audioService.GetChannels();
            _bitDepth = _audioService.GetBitDepth();

            InitModelPath();
            InitAudioSettings();
            _tokenizer = TokenizerFactory.GetTokenizer(false, 99, "en", "transcribe");
            _audioService.OnAudioDataAvailable += HandleAudio_DataAvailable;
            MicCommand = new AsyncRelayCommand(StartOrStopRecordingAsync, CanExecuteMicCommand);

            _clipInferenceService = ServiceHelper.GetService<IClipInferenceService>();
            _whisperEncoderService = ServiceHelper.GetService<IWhisperEncoderInferenceService>();
            _whisperDecoderService = ServiceHelper.GetService<IWhisperDecoderInferenceService>();
        }

        #endregion

        #region Initialization Methods
        private void InitModelPath()
        {
            try
            {
                _baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);
                _modelDir = Path.Combine(_baseDir, "AIModels");

                _encoderModelPath = Path.Combine(_modelDir, "whisper_base_en-whisperencoder.onnx");
                _decoderModelPath = Path.Combine(_modelDir, "whisper_base_en-whisperdecoder.onnx");
                _vocabFilePath = Path.Combine(_modelDir, "vocab.json");
                _recordedFilePath = Path.Combine(_modelDir, "sample.wav");

                _modelassetsDir = Path.Combine(_baseDir, "assets");
                _melBinFilePath = Path.Combine(_modelassetsDir, "mel_80.bin");

            }
            catch (Exception ex)
            {             
                LoggingService.LogError("Error while initializing model directory:", ex);
            }
        }

        private void InitAudioSettings()
        {
            try
            {
                _logMelSpectrogram = new LogMelSpectrogramCSBinding(_melBinFilePath);

            }
            catch (Exception ex)
            {                
                LoggingService.LogError("Error while initializing AudioSettings:", ex);
            }
        }

        #endregion

        #region Properties
        [ObservableProperty]
        private string? queryText;

        [ObservableProperty]
        private bool isRecording;

        #endregion

        #region Commands
        [RelayCommand]
        public async Task StartQuery()
        {
            Debug.WriteLine($"Starting query with {QueryText}");
            if (QueryText == null)
            {
                return;
            }
            if (!_audioService.IsRecording() && !string.IsNullOrEmpty(QueryText))
            {
                await _clipInferenceService.CalculateSimilaritiesAsync(QueryText);
            }
            //ClipInferenceService.CalculateSimilarities(QueryText);
            //await _clipInferenceService.CalculateSimilaritiesAsync(QueryText);
        }

        [RelayCommand]
        public void TextChanged(TextChangedEventArgs text)
        {

            //Debug.WriteLine($"Txt changed {text.OldTextValue}, {text.NewTextValue}, {QueryText}");
        }
        #endregion

        #region Command Execution Logic
        private bool CanExecuteMicCommand()
        {
            return !_audioService.IsRecording();
        }

        private async Task StartOrStopRecordingAsync()
        {
            try
            {
                await _audioService.Start_Stop_Recording();
                IsRecording = _audioService.IsRecording();
                (MicCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {        
                LoggingService.LogError("Error while starting or stopping recording:", ex);
            }
        }

        #endregion

        #region Event Handlers
        private void HandleAudio_DataAvailable(byte[] audioData)
        {
            ProcessAudio(audioData);
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

                Debug.WriteLine($"Audio chunk duration: {durationInSeconds} seconds");

                // Convert the byte array audio chunk to a float array with the expected length
                float[] float_audioData = _audioService.ConvertBytesToFloatArray(audioChunk, expectedLength);
                List<float> floatAudioDataList = float_audioData.ToList();

                // Generate a log Mel spectrogram from the audio data
                List<float> result_logMelSpectrogram = _logMelSpectrogram.load_audio_chunk(floatAudioDataList);

                float[] inputArray = result_logMelSpectrogram.ToArray();

                // Run inference on the Whisper encoder model to get the k_cache_cross and v_cache_cross outputs
                var (k_cache_cross, v_cache_cross) = _whisperEncoderService.RunInference(inputArray, _batchSize, 80, _targetFrames);

                // Run inference on the Whisper decoder model to get the decoded tokens
                var decoded_tokens_list = _whisperDecoderService.DecoderInference(k_cache_cross, v_cache_cross);

                // If decoded tokens are available, decode them into text and update the query text
                Debug.WriteLine("started decoded");
                if (decoded_tokens_list.Count > 0)
                {
                    var text = _tokenizer.Decode(decoded_tokens_list.Skip(1).ToList());
                    Debug.WriteLine(text);
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        // Update the query text with the decoded text
                        if (QueryText == null)
                        {
                            QueryText = text;
                        }
                        else
                        {
                            QueryText += text;
                        }

                        // Start the query process
                        await StartQuery();

                        // Enable the Mic button
                        (MicCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
                    });
                }

            }
            catch (Exception ex)
            {
           
                LoggingService.LogError("Error while starting or stopping recording:", ex);
            }
        }

        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {

            _audioService.Start_Stop_Recording();
            _audioService.OnAudioDataAvailable -= HandleAudio_DataAvailable;

        }

        #endregion
    }
}


