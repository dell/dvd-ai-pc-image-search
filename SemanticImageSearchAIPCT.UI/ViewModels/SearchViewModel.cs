
using CommunityToolkit.Mvvm.Input;
using SemanticImageSearchAIPCT.UI.Audio;
using SemanticImageSearchAIPCT.UI.Service;
using System.Diagnostics;
using LogMelSpectrogramCS;
using Newtonsoft.Json;
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
        private static readonly int _melChannels = 80;
        private static readonly int _fftSize = 400;
        private static readonly int _hopSize = 160; //_fftSize / 2;
        private static readonly int TOKEN_SOT = 50257;
        private static readonly int TOKEN_EOT = 50256;
        private static readonly int SAMPLE_BEGIN = 1;

        // Whisper constants
        private static readonly int TOKEN_BLANK = 220;
        private static readonly int TOKEN_NO_TIMESTAMP = 50362;
        private static readonly int TOKEN_TIMESTAMP_BEGIN = 50363;
        private static readonly int TOKEN_NO_SPEECH = 50361;
        private static readonly double NO_SPEECH_THR = 0.6;

        private static readonly int MEAN_DECODE_LEN = 224;

        private double precision = 0.02; // in seconds
        private double maxInitialTimestamp = 1.0; // in seconds
        private int maxInitialTimestampIndex;

        //private static readonly int numDecoderBlocks = 1;// whisper.num_decoder_blocks;
        //private static readonly int numDecoderHeads = 2;//whisper.num_decoder_heads;
        //private static readonly int attentionDim = 3;//whisper.attention_dim;
        //private static readonly int sampleLenn = 3;//whisper.sampleLen;


        private bool _isBusy = false;
        private int _bitDepth;

        public ICommand MicCommand { get; }
        private WhisperDecoderInferenceService _decoderService;
        private WhisperEncoderInferenceService _encoderService;
        private readonly AudioService _audioService;
        private readonly FileService _fileService;
        private readonly ValidationService _validationService;
        private LogMelSpectrogramCSBinding _logMelSpectrogram;
        private SemanticImageSearchAIPCT.UI.Tokenizer.Tokenizer _tokenizer;


        public SearchViewModel()
        {
            _audioService = new AudioService();
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

                maxInitialTimestampIndex = (int)(maxInitialTimestamp / precision);

                Debug.WriteLine($"fftSize: {_fftSize}");
                Debug.WriteLine($"hopSize: {_hopSize}");
                Debug.WriteLine($"melChannels: {_melChannels}");
                Debug.WriteLine($"targetFrames: {_targetFrames}");
                Debug.WriteLine($"targetFrames: {_batchSize}");
                Debug.WriteLine($"SampleRate: {_sampleRate}");
                Debug.WriteLine($"Channels: {_numChannels}");
                Debug.WriteLine($"BitDepth: {_bitDepth}");

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
                _encoderService = new WhisperEncoderInferenceService(_encoderModelPath);
                _decoderService = new WhisperDecoderInferenceService(_decoderModelPath);               
                _logMelSpectrogram = new LogMelSpectrogramCSBinding(_melBinFilePath);

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while initializing AudioSettings: {ex.Message}");
            }
        }

        [ObservableProperty]
        private string outputText;

        [ObservableProperty]
        private string? queryText;

        #region Text_Commands
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
        #endregion

        private (float[], float[]) ApplyTimestampRules(float[] logits, List<int> tokens)
        {

            logits[TOKEN_NO_TIMESTAMP] = float.NegativeInfinity;

            var seq = tokens.Skip(SAMPLE_BEGIN).ToList();
            bool lastWasTimestamp = seq.Count >= 1 && seq.Last() >= TOKEN_TIMESTAMP_BEGIN;
            bool penultimateWasTimestamp = seq.Count < 2 || seq[^2] >= TOKEN_TIMESTAMP_BEGIN;

            if (lastWasTimestamp)
            {
                if (penultimateWasTimestamp) // has to be non-timestamp
                {
                    for (int i = TOKEN_TIMESTAMP_BEGIN; i < logits.Length; i++)
                        logits[i] = float.NegativeInfinity;
                }
                else // cannot be normal text tokens
                {
                    for (int i = 0; i < TOKEN_EOT; i++)
                        logits[i] = float.NegativeInfinity;
                }
            }

            var timestamps = tokens.Where(t => t >= TOKEN_TIMESTAMP_BEGIN).ToList();
            if (timestamps.Count > 0)
            {
                int timestampLast = lastWasTimestamp && !penultimateWasTimestamp ? timestamps.Last() : timestamps.Last() + 1;
                for (int i = TOKEN_TIMESTAMP_BEGIN; i < timestampLast; i++)
                    logits[i] = float.NegativeInfinity;
            }

            if (tokens.Count == SAMPLE_BEGIN)
            {
                // Suppress generating non-timestamp tokens at the beginning
                for (int i = 0; i < TOKEN_TIMESTAMP_BEGIN; i++)
                    logits[i] = float.NegativeInfinity;

                // Apply the `max_initial_timestamp` option
                int lastAllowed = TOKEN_TIMESTAMP_BEGIN + maxInitialTimestampIndex;
                for (int i = lastAllowed + 1; i < logits.Length; i++)
                    logits[i] = float.NegativeInfinity;
            }

            // Calculate log probabilities



            float[] logprobs = LogSoftmax(logits);
            float timestampLogprob = LogSumExp(logprobs.Skip(TOKEN_TIMESTAMP_BEGIN).ToArray());
            float maxTextTokenLogprob = logprobs.Take(TOKEN_TIMESTAMP_BEGIN).Max();

            if (timestampLogprob > maxTextTokenLogprob)
            {
                // Mask out all but timestamp tokens
                for (int i = 0; i < TOKEN_TIMESTAMP_BEGIN; i++)
                    logits[i] = float.NegativeInfinity;
            }

            return (logits, logprobs);
        }

        public static float LogSumExp(float[] logprobs)
        {
            // Find the maximum log probability to use for scaling to avoid numerical instability
            float maxLogProb = logprobs.Max();

            // Compute the scaled sum of exponentials
            double sumExp = logprobs.Select(logprob => Math.Exp(logprob - maxLogProb)).Sum();

            // Return the log of the computed sum plus the scaling factor we subtracted initially
            return (float)(Math.Log(sumExp) + maxLogProb);
        }

        public static float[] LogSoftmax(float[] logits)
        {
            float maxLogit = logits.Max(); // For numerical stability, subtract the max logit
            float[] exps = new float[logits.Length];
            float sumExps = 0;

            for (int i = 0; i < logits.Length; i++)
            {
                exps[i] = (float)Math.Exp(logits[i] - maxLogit);
                sumExps += exps[i];
            }

            float[] logSoftmax = new float[logits.Length];
            for (int i = 0; i < logits.Length; i++)
            {
                logSoftmax[i] = (float)Math.Log(exps[i] / sumExps);
            }

            return logSoftmax;
        }

        private async Task StartOrStopRecordingAsync()
        {
            try
            {
                //_audioService.Start_Stop_Recording();
                //if (!_audioService.IsRecording())
                //{
                //    // Read the WAV file in 30 second chunks
                //    _audioService.ReadWavFileInChunks(_recordedFilePath);
                //}
                ProcessAudio();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while starting or stopping recording: {ex.Message}");
            }
        }

        private void ProcessAudio()
        {

            List<float> result_logMelSpectrogram = _logMelSpectrogram.load_wav_audio_and_compute(_recordedFilePath);
            //Debug.WriteLine("log Mel Spectrogram From Audio File Output:");
            //if (result_logMelSpectrogram.Count > 10)
            //{
            //    for (int i = 0; i < 10; i++)
            //    {
            //        Debug.WriteLine($"log Mel Spectrogram[{i}]: {result_logMelSpectrogram[i]}");
            //    }
            //}
            //Debug.WriteLine("");
            float[] inputArray = result_logMelSpectrogram.ToArray();

            //int count = Math.Min(inputArray.Length, 10);
            //for (int i = 0; i < count; i++)
            //{
            //    Debug.WriteLine($"inputArray[{i}]: {inputArray[i]}");
            //}

            var (k_cache_cross, v_cache_cross) = _encoderService.RunInference(inputArray, _batchSize, 80, _targetFrames);
            //Debug.WriteLine($"k_cache_cross[0,0,0,0]: {k_cache_cross[0, 0, 0, 0]}");
            //Debug.WriteLine($"k_cache_cross[0,0,0,1]: {k_cache_cross[0, 0, 0, 1]}");
            //Debug.WriteLine($"k_cache_cross[0,0,0,2]: {k_cache_cross[0, 0, 0, 2]}");
            //Debug.WriteLine($"k_cache_cross[0,0,0,3]: {k_cache_cross[0, 0, 0, 3]}");

            //Debug.WriteLine($"v_cache_cross[0,0,0,0]: {v_cache_cross[0, 0, 0, 0]}");
            //Debug.WriteLine($"v_cache_cross[0,0,0,1]: {v_cache_cross[0, 0, 0, 1]}");
            //Debug.WriteLine($"v_cache_cross[0,0,0,2]: {v_cache_cross[0, 0, 0, 2]}");
            //Debug.WriteLine($"v_cache_cross[0,0,0,3]: {v_cache_cross[0, 0, 0, 3]}");

            //Start decoding

            int[,] x = new int[,] { { TOKEN_SOT } };
            List<int> decoded_tokens = new List<int> { TOKEN_SOT };
            int sampleLen = MEAN_DECODE_LEN;

            //int dimPerHead = attentionDim / numDecoderHeads;
            // Initialize k_cache_self with zeros
            //float[,,,] k_cache_self = new float[numDecoderBlocks, numDecoderHeads, dimPerHead, sampleLen];
            //// Initialize v_cache_self with zeros
            //float[,,,] v_cache_self = new float[numDecoderBlocks, numDecoderHeads, sampleLen, dimPerHead];

            float[,,,] k_cache_self = new float[6, 8, 64, 224];
            float[,,,] v_cache_self = new float[6, 8, 224, 64];
            float[,,] logits;
            float sumLogprobs = 0;
            for (int i = 0; i < sampleLen; i++)
            {

                int[,] index = new int[,] { { 0 } };
                index[0, 0] = i;
                var decoder_out = _decoderService.RunInference(x, index, k_cache_cross, v_cache_cross, k_cache_self, v_cache_self);

                // Assuming decoderOutput is a tuple where:
                // decoderOutput.Item1 corresponds to logits,
                // decoderOutput.Item2 corresponds to the updated k_cache_self,
                // decoderOutput.Item3 corresponds to the updated v_cache_self.
                // Update k_cache_self and v_cache_self with the new values from the decoder output

                //logit has shape (1, decoded_len, 51864)

                logits = decoder_out.Item1;
                k_cache_self = decoder_out.Item2;
                v_cache_self = decoder_out.Item3;
                //if (i == 0 || i == 1 || i == 2)
                //{
                //    Debug.WriteLine($"i: {i}");
                //    Debug.WriteLine($"logits[0,0,0]: {logits[0, 0, 0]}");
                //    Debug.WriteLine($"logits[0,0,1]: {logits[0, 0, 1]}");
                //    Debug.WriteLine($"logits[0,0,2]: {logits[0, 0, 2]}");
                //    Debug.WriteLine($"logits[0,0,3]: {logits[0, 0, 3]}");

                //    Debug.WriteLine($"k_cache_self[0,0,0,0]: {k_cache_self[0, 0, 0, 0]}");
                //    Debug.WriteLine($"k_cache_self[0,0,0,1]: {k_cache_self[0, 0, 0, 1]}");
                //    Debug.WriteLine($"k_cache_self[0,0,0,2]: {k_cache_self[0, 0, 0, 2]}");
                //    Debug.WriteLine($"k_cache_self[0,0,0,3]: {k_cache_self[0, 0, 0, 3]}");

                //    Debug.WriteLine($"v_cache_self[0,0,0,0]: {v_cache_self[0, 0, 0, 0]}");
                //    Debug.WriteLine($"v_cache_self[0,0,0,1]: {v_cache_self[0, 0, 0, 1]}");
                //    Debug.WriteLine($"v_cache_self[0,0,0,2]: {v_cache_self[0, 0, 0, 2]}");
                //    Debug.WriteLine($"v_cache_self[0,0,0,3]: {v_cache_self[0, 0, 0, 3]}");
                //}
                // logit has shape (51864,)
                float[] lastTokenLogits = new float[logits.GetLength(2)];
                for (int j = 0; j < logits.GetLength(2); j++)
                {
                    lastTokenLogits[j] = logits[0, logits.GetLength(1) - 1, j];
                }

                //Applying filters.
                if (i == 0)
                {
                    lastTokenLogits[TOKEN_EOT] = float.NegativeInfinity;
                    lastTokenLogits[TOKEN_BLANK] = float.NegativeInfinity;
                }
                //SuppressTokens
                foreach (var token in Constants.NON_SPEECH_TOKENS)
                {
                    lastTokenLogits[token] = float.NegativeInfinity;
                }

                (float[] updatedLogits, float[] logprobs) = ApplyTimestampRules(lastTokenLogits, decoded_tokens);

                if (i == 0)
                {
                    //detect no_speech
                    var noSpeechProb = Math.Exp(logprobs[TOKEN_NO_SPEECH]);
                    if (noSpeechProb > NO_SPEECH_THR)
                    {
                        break;
                    }
                }

                //Debug.WriteLine($"Count of logits: {lastTokenLogits.Count()}");
                // Printing the values of lastTokenLogits
                //int counter = 0;
                //foreach (float value in lastTokenLogits)
                //{
                //    if (counter < 100)
                //    {
                //        Debug.WriteLine($"logits: {value}");
                //        counter++;
                //    }
                //    else
                //    {
                //        break; // Exit the loop after printing the first 100 values
                //    }
                //}
                int nextToken = Array.IndexOf(updatedLogits, updatedLogits.Max());

                if (nextToken == TOKEN_EOT)
                {
                    break;
                }

                sumLogprobs += logprobs[nextToken];

                x = new int[,] { { nextToken } };
                decoded_tokens.Add(nextToken);

            }
            Debug.WriteLine("started decoded");
            var text = _tokenizer.Decode(decoded_tokens.Skip(1).ToList());
            Debug.WriteLine(text);
            outputText = text;          

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
        private void HandleAudio_DataAvailable(object sender, AudioDataEventArgs e)
        {
            try
            {

                //if (_isBusy)
                //{
                //    return;
                //}
                _isBusy = true;

                //Debug.WriteLine($"Chunk Duration Seconds: {e.Duration.TotalSeconds}");
                //byte[] audioChunk = e.Data;

                ////int expectedLength = _batchSize * _melChannels * _targetFrames;

                //int expectedLength = 30 * _sampleRate;

                //if (audioChunk.All(value => value == 0))
                //    Debug.WriteLine("Raw Audio data buffer is entirely zero.");

                //float[] float_audioData = _audioService.ConvertBytesToFloatArray(audioChunk, expectedLength);

                //_fileService.SaveFloatArrayToFile(float_audioData, _modelDir);

                //bool has_audioNonZeroValues = _validationService.ContainsNonZeroValues(float_audioData);

                ////if (float_audioData.All(value => value == 0))
                ////    Debug.WriteLine("Flaot Audio data buffer is entirely zero.");                 

                //Debug.WriteLine($"Float Audio Data Contains Non-Zero Values: {has_audioNonZeroValues}");

                //float[][] melSpectrogram = MelSpectrogramConverter.GenerateMelSpectrogram(float_audioData, _sampleRate, _fftSize, _hopSize, _melChannels, _melFilterbank);

                //bool has_melSpectrogramNonZeroValues = _validationService.CheckMelSpectrogram(melSpectrogram);

                ////Debug.WriteLine($"Mel Spectrogram Contains Non-Zero Values: {has_melSpectrogramNonZeroValues}");

                //// Convert to Logarithmic Mel Spectrogram
                //float[][] logMelSpectrogram = MelSpectrogramConverter.LogarithmicMelSpectrogram(melSpectrogram);

                //_fileService.SaveMelSpectrogramToFile(logMelSpectrogram, _modelDir);

                //// Ensure correct dimensions
                ////80 frames and 3000 frequency bins per framer 
                //float[][] finalSpectrogram = _validationService.EnsureCorrectDimensions(logMelSpectrogram, _targetFrames);

                //bool is_DimensionCorrect = finalSpectrogram.Length == _targetFrames && finalSpectrogram.All(frame => frame.Length == _melChannels);
                //Debug.WriteLine($"Dimension Correct: {is_DimensionCorrect}");

                ////bool is_DataRangeValid = finalSpectrogram.All(frame => frame.All(value => value >= 0 && value <= 1)); // Adjust range as necessary
                ////Debug.WriteLine($"Data Range Valid: {is_DataRangeValid}");

                //// Flatten the spectrogram
                //float[] flattenedSpectrogram = finalSpectrogram.SelectMany(x => x).ToArray();

                //// Ensure flattenedSpectrogram has length 240000
                //if (flattenedSpectrogram.Length != 240000)
                //{
                //    throw new InvalidOperationException($"Expected flattened spectrogram length of 240000, but got {flattenedSpectrogram.Length}");
                //}
                ////Debug.WriteLine($"Encoder input length: {flattenedSpectrogram.Length}");
                //var (encodedOutput_0, encodedOutput_1) = _encoderService.RunInference(flattenedSpectrogram, _batchSize, 80, _targetFrames);

                //int[] x = new int[] { 1 };
                //int[] index = new int[] { 1 };

                //float[,,,] k_cache_cross = encodedOutput_0;
                //float[,,,] v_cache_cross = encodedOutput_1;

                //float[,,,] k_cache_self = new float[6, 8, 64, 224];
                //float[,,,] v_cache_self = new float[6, 8, 224, 64];

                //var (decoder_output_0, decoder_output_1, decoder_output_2) = _decoderService.RunInference(x, index, k_cache_cross, v_cache_cross, k_cache_self, v_cache_self);

                //try
                //{
                //    int dimension0Length = decoder_output_0.GetLength(0); // Assuming you're interested in the first dimension
                //    int[] tokenIds = new int[dimension0Length];
                //    for (int i = 0; i < dimension0Length; i++)
                //    {

                //        tokenIds[i] = (int)Math.Round(decoder_output_0[i, 0, 0]);

                //    }
                //    // string decodedText = ConvertTokensToText(tokenIds);

                //    // Debug.WriteLine($"Decoded Text: {decodedText}");
                //}
                //catch (Exception ex)
                //{

                //    Debug.WriteLine($"Error while initializing model directory: {ex.Message}");
                //}
                _isBusy = false;
            }
            catch (Exception ex)
            {
                _isBusy = false;
                Debug.WriteLine($"Error while processing audio data: {ex.Message}");
            }
        }
        #endregion

        public void Dispose()
        {

            _audioService.Start_Stop_Recording();
            _audioService.OnAudioDataAvailable -= HandleAudio_DataAvailable;

        }
    }
}


