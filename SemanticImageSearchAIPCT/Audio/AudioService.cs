using NAudio.Wave;
using SemanticImageSearchAIPCT.Services;
using System.Diagnostics;

namespace SemanticImageSearchAIPCT.Audio
{
    public class AudioService : IDisposable
    {
        #region Fields
        private readonly WaveInEvent _waveIn;
        private readonly BufferedWaveProvider _bufferedWaveProvider;
        private bool _isRecording = false;   
        private List<byte> _audioBuffer = new List<byte>();

        // Chunk duration constraints    
        private const float SilenceThreshold = 0.02f;
        private const int _chunkSizeMilliseconds = 3000; // 3 seconds 
        private readonly int _sampleRate;
        private readonly int _bytesPerSample;
        private readonly int _bytesPerSecond;
        private readonly int _bytesPerChunk;
        private byte[] _audioDataBuffer;
        private int _noOfTimesSilent = 0;

        public bool IsRecording { 
            get {  return _isRecording; }
            private set { 
                _isRecording = value;
                IsRecordingChanged?.Invoke(this, value);
            }
        }
        #endregion

        #region Events
        public event EventHandler<byte[]> AudioDataAvailable;
        public event EventHandler<bool> IsRecordingChanged;
        #endregion

        #region Constructor
        public AudioService()
        {
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 1),
                BufferMilliseconds = 500, // Smaller buffer size for more frequent callbacks
                NumberOfBuffers = 10
            };

            _waveIn.DataAvailable += WaveIn_DataAvailable;
            _bufferedWaveProvider = new BufferedWaveProvider(_waveIn.WaveFormat)
            {
                BufferLength = _waveIn.WaveFormat.AverageBytesPerSecond * 5, // Each buffer holds 1000 milliseconds (1 second) of audio data
                DiscardOnBufferOverflow = true //  Total buffer capacity is 3 * 1000 milliseconds = 3000 milliseconds (3 seconds)
            };
            _sampleRate = _waveIn.WaveFormat.SampleRate;
            _bytesPerSample = 2; // 16-bit PCM
            _bytesPerSecond = _sampleRate * _bytesPerSample * 1;
            _bytesPerChunk = _sampleRate * (_chunkSizeMilliseconds / 1000) * _bytesPerSample;
            _audioDataBuffer = new byte[_waveIn.WaveFormat.AverageBytesPerSecond * (_chunkSizeMilliseconds / 1000)];

        }
        #endregion

        #region Public Methods

        /// <summary>
        /// Starts or stops the audio recording based on the current recording state.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task Start_Stop_Recording()
        {
            if (IsRecording)
            {
                // Stop recording
                _waveIn.StopRecording();
                IsRecording = false;
                LoggingService.LogDebug("Stop Recording.");
            }
            else
            {
                _noOfTimesSilent = 0;

                // Clear the buffer before adding new samples
                _bufferedWaveProvider.ClearBuffer();
                _audioBuffer.Clear();

                // Start recording
                _waveIn.StartRecording();
                IsRecording = true;
                LoggingService.LogDebug("Start Recording.");
            }
        }

        public int GetSampleRate()
        {
            return _waveIn.WaveFormat.SampleRate;
        }

        public int GetChannels()
        {
            return _waveIn.WaveFormat.Channels;
        }

        public int GetBitDepth()
        {
            return _waveIn.WaveFormat.BitsPerSample;
        }

        // <summary>
        /// Converts a byte array of audio data to a float array, with optional padding if the data is shorter than the expected length.
        /// </summary>
        /// <param name="bytes">The byte array of audio data to convert.</param>
        /// <param name="expectedLength">The expected length of the resulting float array.</param>
        /// <returns>A float array containing the converted audio data, padded with zeros if necessary.</returns>
        /// <exception cref="ArgumentException">Thrown if the length of the byte array is not even.</exception>
        public float[] ConvertBytesToFloatArray(byte[] bytes, int expectedLength)
        {
            try
            {
                if (bytes.Length % 2 != 0)
                {
                    throw new ArgumentException("Byte array length must be even to convert to float array");
                }

                int numSamples = bytes.Length / 2;


                float[] floats = new float[expectedLength];
                for (int i = 0; i < expectedLength; i++)
                {
                    if (i < numSamples)
                    {
                        short sample = BitConverter.ToInt16(bytes, i * 2);
                        floats[i] = sample / 32768f;
                    }
                    else
                    {
                        floats[i] = 0f; // Padding with zeros if the data is shorter than expected
                    }
                }
                return floats;
            }
            catch (Exception e)
            {
                LoggingService.LogError("ConvertBytesToFloatArray:", e);
                throw;
            }
        }
        #endregion

        #region Private Methods

        /// <summary>
        /// Handles the DataAvailable event from the WaveInEvent and processes the recorded audio data.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The WaveInEventArgs containing the audio data.</param>
        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            try
            {
                _bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
                _audioBuffer.AddRange(e.Buffer.Take(e.BytesRecorded));

                // Check if the buffer has enough data for 3 seconds
                if (_audioBuffer.Count >= _bytesPerChunk)
                {
                    var audioBufferArray = _audioBuffer.ToArray();
                    int chunkSize = _bytesPerChunk;
                    bool silenceDetected = false;

                    // Process the audio buffer in chunks until silence is detected or all data is processed
                    while (!silenceDetected && chunkSize <= _audioBuffer.Count)
                    {
                        LoggingService.LogDebug("The audio check");
                        var audioChunkSegment = new ArraySegment<byte>(audioBufferArray, 0, chunkSize);

                        // Detect silence in the current chunk
                        if (DetectSilence(audioChunkSegment))
                        {
                            LoggingService.LogDebug($"The audio chunk of duration {chunkSize / _bytesPerSecond:F2} seconds is full of silence.");
                            _audioBuffer.RemoveRange(0, chunkSize);
                            _noOfTimesSilent++;
                            silenceDetected = true;
                            AudioDataAvailable?.Invoke(this, audioChunkSegment.ToArray());

                            if (_noOfTimesSilent >= 1)
                            {
                                IsRecording = false;
                                _waveIn.StopRecording();
                                LoggingService.LogDebug($"Stop Recording No of Silent Chunks: {_noOfTimesSilent}");
                            }
                            break;
                        }
                        else
                        {
                            LoggingService.LogDebug("No silence detected in the current chunk.");
                            // Continue to process the audio until silence is found
                            if (chunkSize + _bytesPerSecond <= _audioBuffer.Count)
                            {
                                chunkSize += _bytesPerSecond; // Increase chunk size by 1 second of audio data
                            }
                            else
                            {
                                // If the buffer doesn't have enough data for another second, wait for more data
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {              
                LoggingService.LogError("Error WaveIn_DataAvailable:", ex);
            }
        }

        /// <summary>
        /// Detects if the given audio buffer segment contains silence based on the average amplitude.
        /// </summary>
        /// <param name="bufferSegment">The segment of the audio buffer to analyze.</param>
        /// <returns>True if the average amplitude of the segment is below the silence threshold; otherwise, false.</returns>
        private bool DetectSilence(ArraySegment<byte> bufferSegment)
        {
            float sum = 0;
            for (int i = bufferSegment.Offset; i < bufferSegment.Offset + bufferSegment.Count; i += 2)
            {
                short sample = BitConverter.ToInt16(bufferSegment.Array, i);
                sum += Math.Abs(sample / (float)short.MaxValue);
            }

            float averageAmplitude = sum / (bufferSegment.Count / 2);

            // Return true if the average amplitude is below the silence threshold, indicating silence
            return averageAmplitude < SilenceThreshold;
        }
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            _waveIn?.Dispose();
            _bufferedWaveProvider?.ClearBuffer();
        }
        #endregion
    }

}

