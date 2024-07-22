
using CommunityToolkit.Mvvm.Input;
using SemanticImageSearchAIPCT.UI.Audio;

using System.Diagnostics;
using LogMelSpectrogramCS;
using SemanticImageSearchAIPCT.UI.Tokenizer;
using SemanticImageSearchAIPCT.UI.Services;

namespace SemanticImageSearchAIPCT.UI.ViewModels
{
    public partial class SearchViewModel : ObservableObject
    {

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
        private readonly FileService _fileService;
        private readonly ValidationService _validationService;
        private LogMelSpectrogramCSBinding _logMelSpectrogram;
        private SemanticImageSearchAIPCT.UI.Tokenizer.Tokenizer _tokenizer;

        private readonly IClipInferenceService _clipInferenceService;
        private readonly IWhisperEncoderInferenceService _WhisperEncoderService;
        private readonly IWhisperDecoderInferenceService _WhisperDecoderService;
        public SearchViewModel()
        {
            _audioService = new AudioService(this);
            _fileService = new FileService();
            _validationService = new ValidationService();


            _sampleRate = _audioService.GetSampleRate();
            _numChannels = _audioService.GetChannels();
            _bitDepth = _audioService.GetBitDepth();

            InitModelPath();
            InitAudioSettings();
            _tokenizer = TokenizerFactory.GetTokenizer(false, 99, "en", "transcribe");
            _audioService.OnAudioDataAvailable += HandleAudio_DataAvailable;
            MicCommand = new AsyncRelayCommand(StartOrStopRecordingAsync);
            _clipInferenceService = ServiceHelper.GetService<IClipInferenceService>();
            _WhisperEncoderService = ServiceHelper.GetService<IWhisperEncoderInferenceService>();
            _WhisperDecoderService = ServiceHelper.GetService<IWhisperDecoderInferenceService>();
        }


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
                Debug.WriteLine($"Error while initializing model directory: {ex.Message}");
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
                Debug.WriteLine($"Error while initializing AudioSettings: {ex.Message}");
            }
        }

        [ObservableProperty]
        private string? queryText;

        #region Text_Commands
        [RelayCommand]
        public async Task StartQuery()
        {
            Debug.WriteLine($"Starting query with {QueryText}");
            if (QueryText == null)
            {
                return;
            }

            //ClipInferenceService.CalculateSimilarities(QueryText);
            await _clipInferenceService.CalculateSimilaritiesAsync(QueryText);
        }

        [RelayCommand]
        public void TextChanged(TextChangedEventArgs text)
        {
            //Debug.WriteLine($"Txt changed {text.OldTextValue}, {text.NewTextValue}, {QueryText}");
        }
        #endregion


        private async Task StartOrStopRecordingAsync()
        {
            try
            {
                _audioService.Start_Stop_Recording();             
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while starting or stopping recording: {ex.Message}");
            }
        }

        private void HandleAudio_DataAvailable(byte[] audioData)
        {          
            ProcessAudio(audioData);
        }
        private void ProcessAudio(byte[] audioChunk)
        {
            try
            {
                int expectedLength = _batchSize * _melChannels * _targetFrames;
                int bytesPerSample = _bitDepth / 8; // Convert bit depth to bytes
                int numSamples = audioChunk.Length / bytesPerSample / _numChannels;    
                double durationInSeconds = (double)numSamples / _sampleRate; 

                Debug.WriteLine($"Audio chunk duration: {durationInSeconds} seconds");

                float[] float_audioData = _audioService.ConvertBytesToFloatArray(audioChunk, expectedLength);
                List<float> floatAudioDataList = float_audioData.ToList();

                List<float> result_logMelSpectrogram = _logMelSpectrogram.load_audio_chunk(floatAudioDataList);
           
                float[] inputArray = result_logMelSpectrogram.ToArray();         
      
                var (k_cache_cross, v_cache_cross) = _WhisperEncoderService.RunInference(inputArray, _batchSize, 80, _targetFrames);

                var decoded_tokens_list = _WhisperDecoderService.DecoderInference(k_cache_cross, v_cache_cross);

                Debug.WriteLine("started decoded");
                if (decoded_tokens_list.Count > 1)
                {
                    var text = _tokenizer.Decode(decoded_tokens_list.Skip(1).ToList());
                    Debug.WriteLine(text);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (QueryText == null)
                        {
                            QueryText = text;
                        }
                        else
                        {
                            QueryText += text;
                        }
                    });
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while starting or stopping recording: {ex.Message}");
            }
        }


        #region Methods
        /// <summary>
        //Batch size: 1 (Number of sequences processed simultaneously)
        //Number of channels: 80 (Number of frames or time steps in the spectrogram)
        //Number of samples: 3000 (Number of frequency bins per frame)
        //The product of these dimensions is 1×80×3000=2,40,000

        //Mono Audio: Single channel.
        //Stereo Audio: Two channels

        //1.	Extract Audio Frames and Apply Window Function
        //2.	Compute Fourier Transform(FFT) to Get Frequency Domain Data
        //3.	Convert FFT Output to Power Spectrogram
        //4.	Apply Mel Filter Bank to Convert Power Spectrogram to Mel Spectrogram
        //5.	Apply Logarithmic Scaling to the Mel Spectrogram
        //6.	Ensure Correct Dimensions for Model Input
        /// </summary>
        //private void HandleAudio_DataAvailable(object sender, AudioDataEventArgs e)
        //{
        //    try
        //    {

        //        //if (_isBusy)
        //        //{
        //        //    return;
        //        //}
        //        _isBusy = true;

        //        //Debug.WriteLine($"Chunk Duration Seconds: {e.Duration.TotalSeconds}");
        //        //byte[] audioChunk = e.Data;

        //        ////int expectedLength = _batchSize * _melChannels * _targetFrames;

        //        //int expectedLength = 30 * _sampleRate;

        //        //if (audioChunk.All(value => value == 0))
        //        //    Debug.WriteLine("Raw Audio data buffer is entirely zero.");

        //        //float[] float_audioData = _audioService.ConvertBytesToFloatArray(audioChunk, expectedLength);

        //        //_fileService.SaveFloatArrayToFile(float_audioData, _modelDir);

        //        //bool has_audioNonZeroValues = _validationService.ContainsNonZeroValues(float_audioData);

        //        ////if (float_audioData.All(value => value == 0))
        //        ////    Debug.WriteLine("Flaot Audio data buffer is entirely zero.");                 

        //        //Debug.WriteLine($"Float Audio Data Contains Non-Zero Values: {has_audioNonZeroValues}");

        //        //float[][] melSpectrogram = MelSpectrogramConverter.GenerateMelSpectrogram(float_audioData, _sampleRate, _fftSize, _hopSize, _melChannels, _melFilterbank);

        //        //bool has_melSpectrogramNonZeroValues = _validationService.CheckMelSpectrogram(melSpectrogram);

        //        ////Debug.WriteLine($"Mel Spectrogram Contains Non-Zero Values: {has_melSpectrogramNonZeroValues}");

        //        //// Convert to Logarithmic Mel Spectrogram
        //        //float[][] logMelSpectrogram = MelSpectrogramConverter.LogarithmicMelSpectrogram(melSpectrogram);

        //        //_fileService.SaveMelSpectrogramToFile(logMelSpectrogram, _modelDir);

        //        //// Ensure correct dimensions
        //        ////80 frames and 3000 frequency bins per framer 
        //        //float[][] finalSpectrogram = _validationService.EnsureCorrectDimensions(logMelSpectrogram, _targetFrames);

        //        //bool is_DimensionCorrect = finalSpectrogram.Length == _targetFrames && finalSpectrogram.All(frame => frame.Length == _melChannels);
        //        //Debug.WriteLine($"Dimension Correct: {is_DimensionCorrect}");

        //        ////bool is_DataRangeValid = finalSpectrogram.All(frame => frame.All(value => value >= 0 && value <= 1)); // Adjust range as necessary
        //        ////Debug.WriteLine($"Data Range Valid: {is_DataRangeValid}");

        //        //// Flatten the spectrogram
        //        //float[] flattenedSpectrogram = finalSpectrogram.SelectMany(x => x).ToArray();

        //        //// Ensure flattenedSpectrogram has length 240000
        //        //if (flattenedSpectrogram.Length != 240000)
        //        //{
        //        //    throw new InvalidOperationException($"Expected flattened spectrogram length of 240000, but got {flattenedSpectrogram.Length}");
        //        //}
        //        ////Debug.WriteLine($"Encoder input length: {flattenedSpectrogram.Length}");
        //        //var (encodedOutput_0, encodedOutput_1) = _encoderService.RunInference(flattenedSpectrogram, _batchSize, 80, _targetFrames);

        //        //int[] x = new int[] { 1 };
        //        //int[] index = new int[] { 1 };

        //        //float[,,,] k_cache_cross = encodedOutput_0;
        //        //float[,,,] v_cache_cross = encodedOutput_1;

        //        //float[,,,] k_cache_self = new float[6, 8, 64, 224];
        //        //float[,,,] v_cache_self = new float[6, 8, 224, 64];

        //        //var (decoder_output_0, decoder_output_1, decoder_output_2) = _decoderService.RunInference(x, index, k_cache_cross, v_cache_cross, k_cache_self, v_cache_self);

        //        //try
        //        //{
        //        //    int dimension0Length = decoder_output_0.GetLength(0); // Assuming you're interested in the first dimension
        //        //    int[] tokenIds = new int[dimension0Length];
        //        //    for (int i = 0; i < dimension0Length; i++)
        //        //    {

        //        //        tokenIds[i] = (int)Math.Round(decoder_output_0[i, 0, 0]);

        //        //    }
        //        //    // string decodedText = ConvertTokensToText(tokenIds);

        //        //    // Debug.WriteLine($"Decoded Text: {decodedText}");
        //        //}
        //        //catch (Exception ex)
        //        //{

        //        //    Debug.WriteLine($"Error while initializing model directory: {ex.Message}");
        //        //}
        //        _isBusy = false;
        //    }
        //    catch (Exception ex)
        //    {
        //        _isBusy = false;
        //        Debug.WriteLine($"Error while processing audio data: {ex.Message}");
        //    }
        //}
        #endregion

        public void Dispose()
        {

            _audioService.Start_Stop_Recording();
            _audioService.OnAudioDataAvailable -= HandleAudio_DataAvailable;

        }
    }
}


