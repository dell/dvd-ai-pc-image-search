using NAudio.Wave;
using SemanticImageSearchAIPCT.UI.ViewModels;
using System.Diagnostics;
namespace SemanticImageSearchAIPCT.UI.Audio
{
    public class AudioService : IDisposable
    {
        private readonly WaveInEvent _waveIn;
        private readonly BufferedWaveProvider _bufferedWaveProvider;
        private bool _isRecording = false;

        public event Action<byte[]> OnAudioDataAvailable;
        private List<byte> _audioBuffer = new List<byte>();

        // Chunk duration constraints    
        private const float SilenceThreshold = 0.01f;
        private const int _chunkSizeMilliseconds = 3000; // 3 seconds 
        private readonly int _sampleRate;
        private readonly int _bytesPerSample;
        private readonly int _bytesPerChunk;
        private byte[] _audioDataBuffer;
        private int _noOfTimesSilent = 0;
        private readonly SearchViewModel _searchViewModel;

        public AudioService(SearchViewModel searchViewModel)
        {
            _searchViewModel = searchViewModel;
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
            _bytesPerChunk = _sampleRate * (_chunkSizeMilliseconds / 1000) * _bytesPerSample;
            _audioDataBuffer = new byte[_waveIn.WaveFormat.AverageBytesPerSecond * (_chunkSizeMilliseconds / 1000)];

        }

        public void Start_Stop_Recording()
        {
            //_isRecording = false;
            if (!_isRecording)
            {
                _noOfTimesSilent = 0;
                _waveIn.StartRecording();
                _isRecording = true;
                Debug.WriteLine("Start Recording.");
            }
            else
            {
                _waveIn.StopRecording();
                _isRecording = false;               
                Debug.WriteLine("Stop Recording.");
                _searchViewModel.StartQuery();
            }
        }
        public bool IsRecording()
        {
            return _isRecording;
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

        public float[] ConvertBytesToFloatArray(byte[] bytes, int expectedLength)
        {
            if (bytes.Length % 2 != 0)
            {
                Debug.WriteLine($"Byte array length must be even to convert to float array");
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

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            try
            {

                _bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
                _audioBuffer.AddRange(e.Buffer.Take(e.BytesRecorded));

                // Check if the buffer has enough data for 3 seconds
                if (_audioBuffer.Count >= _bytesPerChunk)
                {
                    // Extract the first 3 seconds of audio data
                    var audioChunkSegment = new ArraySegment<byte>(_audioBuffer.ToArray(), 0, _bytesPerChunk);

                    //var endSegment = new ArraySegment<byte>(_audioBuffer.ToArray(), _bytesPerChunk - _bytesPerSilenceCheck, _bytesPerSilenceCheck);


                    if (DetectSilence(audioChunkSegment))
                    {
                        Debug.WriteLine("The audio chunk is full of silence.");
                        _audioBuffer.RemoveRange(0, _bytesPerChunk);
                        _noOfTimesSilent++;
                        if (_noOfTimesSilent >= 1)
                        {
                            _isRecording = false;
                            _waveIn.StopRecording();                          
                            Debug.WriteLine($"Stop Recording No of Silent Chunks: {_noOfTimesSilent}");
                        }
                        return;
                    }
                    _noOfTimesSilent = 0;
                    _audioBuffer.RemoveRange(0, _bytesPerChunk);
                    OnAudioDataAvailable?.Invoke(audioChunkSegment.ToArray());
                   
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WaveIn_DataAvailable: {ex.Message}");
            }
        }


        private bool DetectSilence(ArraySegment<byte> bufferSegment)
        {
            float sum = 0;
            for (int i = bufferSegment.Offset; i < bufferSegment.Offset + bufferSegment.Count; i += 2)
            {
                short sample = BitConverter.ToInt16(bufferSegment.Array, i);
                sum += Math.Abs(sample / (float)short.MaxValue);
            }

            float averageAmplitude = sum / (bufferSegment.Count / 2);
            return averageAmplitude < SilenceThreshold;
        }


        public void Dispose()
        {
            _waveIn?.Dispose();
            _bufferedWaveProvider?.ClearBuffer();
        }

    }

}

