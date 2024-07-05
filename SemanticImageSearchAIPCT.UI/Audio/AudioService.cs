using NAudio.Wave;
using System.Diagnostics;
using System.Collections.Concurrent;


//namespace SemanticImageSearchAIPCT.UI.Audio
//{
//    public class AudioService : IDisposable
//    {
//        private readonly WaveInEvent _waveIn;
//        private readonly BufferedWaveProvider _bufferedWaveProvider;
//        private bool _isRecording = false;
//        public int NumberOfChannels { get; private set; }
//        public event Action<byte[]> OnAudioDataAvailable;
//        public int _bitDepth { get; private set; }
//        private List<byte> _audioBuffer = new List<byte>();

//        // Chunk duration constraints
//        private const int _maxChunkDurationMilliseconds = 15000; // 15 seconds
//        private const int _silenceDurationMilliseconds = 1000; // 1 second
//        private DateTime _lastAudioTime = DateTime.MinValue;
//        private const float SilenceThreshold = 0.01f;
//        private const int _chunkSizeMilliseconds = 30000;  // 30 seconds in milliseconds
//        private const int _bufferMilliseconds = 500; // 0.5 seconds

//        private float _waveFile_sampleRate;
//        private float _wavefile_numChannels;
//        private float _wavefile_bitsPerSample;

//        private ConcurrentQueue<byte[]> _audioDataQueue = new ConcurrentQueue<byte[]>();
//        private AutoResetEvent _dataAvailableEvent = new AutoResetEvent(false);


//        public AudioService()
//        {
//            _waveIn = new WaveInEvent
//            {
//                WaveFormat = new WaveFormat(16000, 1)// Mono, 16 kHz
//            };
//            _waveIn.DataAvailable += WaveIn_DataAvailable;
//            _bufferedWaveProvider = new BufferedWaveProvider(_waveIn.WaveFormat);
//            NumberOfChannels = _bufferedWaveProvider.WaveFormat.Channels;
//            _bitDepth = _waveIn.WaveFormat.BitsPerSample;
//        }

//        public void Start_Stop_Recording()
//        {
//            _isRecording = false;
//            if (!_isRecording)
//            {
//                _waveIn.StartRecording();
//                _isRecording = true;

//                ProcessAudioData(); // Start processing audio data
//            }
//            else
//            {
//                _waveIn.StopRecording();
//                _isRecording = false;
//            }
//        }

//        public bool IsRecording()
//        {
//            return _isRecording;
//        }
//        public int GetSampleRate()
//        {
//            return _waveIn.WaveFormat.SampleRate;
//        }

//        public int GetChannels()
//        {
//            return _waveIn.WaveFormat.Channels;
//        }
//        public int GetBitDepth()
//        {
//            return _waveIn.WaveFormat.BitsPerSample;
//        }

//        //private void WaveIn_DataAvailable11(object sender, WaveInEventArgs e)
//        //{
//        //    try
//        //    {
//        //        int sampleRate = _waveIn.WaveFormat.SampleRate;
//        //        int durationInSeconds = 3;
//        //        int bytesPerSample = 2; // 16-bit PCM
//        //        int bytesPerChunk = sampleRate * durationInSeconds * bytesPerSample;
//        //        _bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
//        //        // Check if the buffer has enough data for 3 seconds
//        //        if (_audioBuffer.Count >= bytesPerChunk)
//        //        {
//        //            // Extract the first 3 seconds of audio data
//        //            byte[] audioChunk = _audioBuffer.Take(bytesPerChunk).ToArray();
//        //            _audioBuffer.RemoveRange(0, bytesPerChunk);

//        //            // Trigger the event or callback with the 3-second audio chunk
//        //            OnAudioDataAvailable?.Invoke(audioChunk);
//        //        }
//        //    }
//        //    catch (Exception ex)
//        //    {

//        //        Debug.WriteLine($"WaveIn_DataAvailable: {ex.Message}");
//        //    }
//        //}


//        private void WaveIn_DataAvailable_old(object sender, WaveInEventArgs e)
//        {
//            try
//            {
//                _bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
//                double AverageBytesPerSecond = _bufferedWaveProvider.WaveFormat.AverageBytesPerSecond;
//                Debug.WriteLine($"AverageBytesPerSecond: {AverageBytesPerSecond}");

//                // Calculate the size of the 30-second chunk in bytes
//                long chunkSizeBytes = _bufferedWaveProvider.WaveFormat.AverageBytesPerSecond * (_chunkSizeMilliseconds / 1000);

//                while (_bufferedWaveProvider.BufferedBytes >= chunkSizeBytes)
//                {
//                    byte[] buffer = new byte[chunkSizeBytes];
//                    int bytesRead = _bufferedWaveProvider.Read(buffer, 0, buffer.Length);

//                    // Emit the 30-second audio chunk
//                    OnAudioDataAvailable?.Invoke(buffer);

//                    // Note: If bytesRead < chunkSizeBytes, it means you're at the end of the file or input source,
//                    // you may need to handle this scenario depending on your application's needs.
//                }
//            }
//            catch (Exception ex)
//            {
//                Debug.WriteLine($"WaveIn_DataAvailable: {ex.Message}");
//            }
//        }

//        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
//        {
//            byte[] bufferCopy = new byte[e.BytesRecorded];
//            Array.Copy(e.Buffer, bufferCopy, e.BytesRecorded);
//            _audioDataQueue.Enqueue(bufferCopy);
//            _dataAvailableEvent.Set();
//        }

//        private void ProcessAudioData()
//        {
//            Task.Run(() =>
//            {
//                while (true)
//                {
//                    _dataAvailableEvent.WaitOne();
//                    List<byte> currentChunk = new List<byte>();

//                    while (_audioDataQueue.TryDequeue(out byte[] audioChunk))
//                    {
//                        currentChunk.AddRange(audioChunk);

//                        Debug.WriteLine($"AverageBytesPerSecond: {_bufferedWaveProvider.WaveFormat.AverageBytesPerSecond}");
//                        // Check if we have enough data for a 30-second chunk
//                        if (currentChunk.Count >= _bufferedWaveProvider.WaveFormat.AverageBytesPerSecond * _chunkSizeMilliseconds / 1000)
//                        {
//                            // Emit the 30-second audio chunk
//                            byte[] chunkToEmit = currentChunk.Take(_bufferedWaveProvider.WaveFormat.AverageBytesPerSecond * _chunkSizeMilliseconds / 1000).ToArray();
//                            OnAudioDataAvailable?.Invoke(chunkToEmit);

//                            // Remove the emitted chunk from the currentChunk
//                            currentChunk.RemoveRange(0, _bufferedWaveProvider.WaveFormat.AverageBytesPerSecond * _chunkSizeMilliseconds / 1000);
//                        }
//                    }
//                }
//            });
//        }


//        public void ReadWavFileInChunks(string filePath)
//        {
//            try
//            {
//                using (var reader = new AudioFileReader(filePath))
//                {
//                    long bytesPerSample = reader.WaveFormat.BitsPerSample / 8;
//                    // Calculate bytes for one second of audio
//                    long bytesPerSecond = reader.WaveFormat.SampleRate * reader.WaveFormat.Channels * bytesPerSample;
//                    // Calculate the size of each chunk
//                    long bytesPerChunk = bytesPerSecond * _chunkSizeMilliseconds / 1000;

//                    byte[] buffer = new byte[bytesPerChunk];
//                    int bytesRead;
//                    while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
//                    {
//                        if (bytesRead < buffer.Length)
//                        {
//                            // If the last read is smaller than the buffer, resize the array to fit the remaining data.
//                            Array.Resize(ref buffer, bytesRead);
//                        }

//                        // Here you would handle the audio data, e.g., process, store, or transmit it.
//                        OnAudioDataAvailable?.Invoke(buffer);

//                        // Reinitialize buffer if it was resized
//                        if (buffer.Length != bytesPerChunk)
//                        {
//                            buffer = new byte[bytesPerChunk];
//                        }
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                Debug.WriteLine($"ReadWavFileInChunks: {ex.Message}");
//            }
//        }


//        public float[] ConvertBytesToFloatArray(byte[] bytes, int expectedLength)
//        {
//            if (bytes.Length % 2 != 0)
//            {
//                Debug.WriteLine($"Byte array length must be even to convert to float array");
//                throw new ArgumentException("Byte array length must be even to convert to float array");
//            }

//            int numSamples = bytes.Length / 2;


//            float[] floats = new float[expectedLength];
//            for (int i = 0; i < expectedLength; i++)
//            {
//                if (i < numSamples)
//                {
//                    short sample = BitConverter.ToInt16(bytes, i * 2);
//                    floats[i] = sample / 32768f;
//                }
//                else
//                {
//                    floats[i] = 0f; // Padding with zeros if the data is shorter than expected
//                }
//            }
//            return floats;
//        }

//        //public void GetWavFileProperties(string filePath)
//        //{
//        //    using (var reader = new WaveFileReader(filePath))
//        //    {

//        //        _waveFile_sampleRate = reader.WaveFormat.SampleRate;
//        //        _wavefile_numChannels = reader.WaveFormat.Channels;
//        //        _wavefile_bitsPerSample = reader.WaveFormat.BitsPerSample;      
//        //    }
//        //}

//        private bool DetectSilence(byte[] buffer)
//        {
//            float sum = 0;
//            for (int i = 0; i < buffer.Length; i += 2)
//            {
//                short sample = BitConverter.ToInt16(buffer, i);
//                sum += Math.Abs(sample / (float)short.MaxValue);
//            }

//            float averageAmplitude = sum / (buffer.Length / 2);

//            return averageAmplitude < SilenceThreshold;
//        }

//        public bool ContainsNonZeroValues(float[] data)
//        {
//            return data.Any(value => value != 0.0f);
//        }

//        //public double CalculateDurationFromBytes(byte[] audioChunk, _wavefile_numChannels)
//        //{
//        //    // Assuming _sampleRate, _numChannels, and bitsPerSample are initialized correctly
//        //    //int bitsPerSample = 16; // This is a common value, but you should use the actual bit depth of your audio data

//        //    // Calculate the total number of samples in the audioChunk
//        //    double totalSamples = audioChunk.Length / (_wavefile_numChannels * (_wavefile_bitsPerSample / 8.0));

//        //    // Calculate the duration in seconds
//        //    double durationInSeconds = totalSamples / _waveFile_sampleRate;

//        //    return durationInSeconds;
//        //}

//        public double CalculateDurationFromBytes_Live(byte[] audioChunk, int _numChannels, int _bitsPerSample, int _sampleRate)
//        {
//            // Calculate the total number of samples in the audioChunk
//            double totalSamples = audioChunk.Length / (_numChannels * (_bitsPerSample / 8.0));

//            // Calculate the duration in seconds
//            double durationInSeconds = totalSamples / _sampleRate;

//            return durationInSeconds;
//        }


//        public void Dispose()
//        {
//            _waveIn?.Dispose();
//            _bufferedWaveProvider?.ClearBuffer();
//        }


//    }
//}




namespace SemanticImageSearchAIPCT.UI.Audio
{
    public class AudioService : IDisposable
    {
        private readonly WaveInEvent _waveIn;
        private readonly BufferedWaveProvider _bufferedWaveProvider;
        private bool _isRecording = false;
       
        //public event Action<byte[]> OnAudioDataAvailable;
    
        private List<byte> _audioBuffer = new List<byte>();
        public event EventHandler<AudioDataEventArgs> OnAudioDataAvailable;
        // Chunk duration constraints
        private const int _maxChunkDurationMilliseconds = 15000; // 15 seconds
        private const int _silenceDurationMilliseconds = 1000; // 1 second
        private DateTime _lastAudioTime = DateTime.MinValue;
        private const float SilenceThreshold = 0.01f;
        private const int _chunkSizeMilliseconds = 10000;  // 30 seconds in milliseconds
        private const int _bufferMilliseconds = 500; // 0.5 seconds
        private int _totalBytesRead = 0;
        private byte[] _audioDataBuffer;

        private int _lastProcessTime = Environment.TickCount;
        public AudioService()
        {
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 1),
                BufferMilliseconds = 500, // Smaller buffer size for more frequent callbacks
                NumberOfBuffers = 5
            };

            _waveIn.DataAvailable += WaveIn_DataAvailable;
            //_bufferedWaveProvider = new BufferedWaveProvider(_waveIn.WaveFormat);
            _bufferedWaveProvider = new BufferedWaveProvider(_waveIn.WaveFormat)
            {
                BufferLength = _waveIn.WaveFormat.AverageBytesPerSecond * 30 * 2, // Buffer size to hold at least 60 seconds of audio
                DiscardOnBufferOverflow = true // Automatically discard old data if buffer gets full
            };
           
            
        }

        public void Start_Stop_Recording()
        {
            _isRecording = false;
            //if (!_isRecording)
            //{
            //    _waveIn.StartRecording();
            //    _isRecording = true;
            //}
            //else
            //{
            //    _waveIn.StopRecording();
            //    _isRecording = false;
            //}
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

        //private void WaveIn_DataAvailable11(object sender, WaveInEventArgs e)
        //{
        //    try
        //    {
        //        int sampleRate = _waveIn.WaveFormat.SampleRate;
        //        int durationInSeconds = 3;
        //        int bytesPerSample = 2; // 16-bit PCM
        //        int bytesPerChunk = sampleRate * durationInSeconds * bytesPerSample;
        //        _bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
        //        // Check if the buffer has enough data for 3 seconds
        //        if (_audioBuffer.Count >= bytesPerChunk)
        //        {
        //            // Extract the first 3 seconds of audio data
        //            byte[] audioChunk = _audioBuffer.Take(bytesPerChunk).ToArray();
        //            _audioBuffer.RemoveRange(0, bytesPerChunk);

        //            // Trigger the event or callback with the 3-second audio chunk
        //            OnAudioDataAvailable?.Invoke(audioChunk);
        //        }
        //    }
        //    catch (Exception ex)
        //    {

        //        Debug.WriteLine($"WaveIn_DataAvailable: {ex.Message}");
        //    }
        //}


        //private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        //{
        //    try
        //    {
        //        _bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);

        //        // Calculate the size of the 30-second chunk in bytes
        //        int chunkSizeBytes = _bufferedWaveProvider.WaveFormat.AverageBytesPerSecond * (_chunkSizeMilliseconds / 1000);

        //        if (_bufferedWaveProvider.BufferLength < chunkSizeBytes * 2)
        //        {
        //            _bufferedWaveProvider.BufferLength = chunkSizeBytes * 2;
        //        }

        //        while (_bufferedWaveProvider.BufferedBytes >= chunkSizeBytes)
        //        {
        //            byte[] buffer = new byte[chunkSizeBytes];
        //            int bytesRead = _bufferedWaveProvider.Read(buffer, 0, buffer.Length);

        //            // Emit the 30-second audio chunk
        //            OnAudioDataAvailable?.Invoke(buffer);

        //            if (bytesRead < chunkSizeBytes)
        //            {
        //                // Process remaining data or pad with zeros if needed
        //                byte[] finalChunk = new byte[chunkSizeBytes];
        //                Array.Copy(buffer, finalChunk, bytesRead);
        //                OnAudioDataAvailable?.Invoke(finalChunk);
        //                break;  // Exit the loop if we're at the end of the stream
        //            } 
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine($"WaveIn_DataAvailable: {ex.Message}");
        //    }
        //}

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            try
            {
                _bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
                ProcessBufferedData();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WaveIn_DataAvailable error: {ex.Message}");
            }
        }

        private void ProcessBufferedData()
        {
            int chunkSizeBytes = _waveIn.WaveFormat.AverageBytesPerSecond * 30;
            int currentTime = Environment.TickCount;
            Debug.WriteLine($"Debug: Starting to check buffer. Required bytes: {chunkSizeBytes}, Available bytes: {_bufferedWaveProvider.BufferedBytes}, LastProcessTime: {_lastProcessTime}");

            while (_bufferedWaveProvider.BufferedBytes >= chunkSizeBytes || (currentTime - _lastProcessTime > 30000 && _bufferedWaveProvider.BufferedBytes > 0))
            {
                int bytesToRead = Math.Min(chunkSizeBytes - _totalBytesRead, _bufferedWaveProvider.BufferedBytes);
                Debug.WriteLine($"Debug: Trying to read {bytesToRead} bytes.");
                int bytesRead = _bufferedWaveProvider.Read(_audioDataBuffer, _totalBytesRead, bytesToRead);
                _totalBytesRead += bytesRead;
                _lastProcessTime = currentTime;
                Debug.WriteLine($"Debug: Read {bytesRead} bytes. Total bytes read: {_totalBytesRead}");
                if (_totalBytesRead == chunkSizeBytes || (currentTime - _lastProcessTime > 30000 && _bufferedWaveProvider.BufferedBytes > 0))
                {
                    ProcessAudioChunk(_audioDataBuffer, _totalBytesRead);
                    _totalBytesRead = 0; // Reset the count after processing a full chunk or timeout
                }
            }
            Debug.WriteLine("Debug: Exiting the loop.");
        }

        private void ProcessAudioChunk(byte[] buffer, int bytesRead)
        {
            try
            {
                byte[] completeChunk = new byte[bytesRead];
                Array.Copy(buffer, completeChunk, bytesRead);
                //OnAudioDataAvailable?.Invoke(completeChunk);

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProcessAudioChunk: {ex.Message}");
            }
        }

        public void ReadWavFileInChunks_old(string filePath)
        {
            try
            {
                using (var reader = new AudioFileReader(filePath))
                {
                    int bytesPerSample = reader.WaveFormat.BitsPerSample / 8;
                    // Calculate bytes for one second of audio
                    int bytesPerSecond = reader.WaveFormat.SampleRate * reader.WaveFormat.Channels * bytesPerSample;
                    // Calculate the size of each chunk
                    int bytesPerChunk = bytesPerSecond * _chunkSizeMilliseconds / 1000;

                    byte[] buffer = new byte[bytesPerChunk];
                    int bytesRead;
                    while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (bytesRead < buffer.Length)
                        {
                            // If the last read is smaller than the buffer, resize the array to fit the remaining data.
                            Array.Resize(ref buffer, bytesRead);
                        }

                        // Here you would handle the audio data, e.g., process, store, or transmit it.
                       // OnAudioDataAvailable?.Invoke(buffer);

                        // Reinitialize buffer if it was resized
                        if (buffer.Length != bytesPerChunk)
                        {
                            buffer = new byte[bytesPerChunk];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ReadWavFileInChunks: {ex.Message}");
            }
        }

        public void ReadWavFileInChunks(string filePath)
        {
            try
            {
                using (var reader = new AudioFileReader(filePath))
                {
                    int bytesPerSample = reader.WaveFormat.BitsPerSample / 8;

                    // Calculate bytes for one second of audio
                    int bytesPerSecond = reader.WaveFormat.SampleRate * reader.WaveFormat.Channels * bytesPerSample;
                    // Calculate the size of each chunk for 30 seconds
                    int bytesPerChunk = bytesPerSecond * 30;

                    byte[] buffer = new byte[bytesPerChunk];
                    int bytesRead;
                    Debug.WriteLine($"Audio chunk Channels: {reader.WaveFormat.Channels}");
                    Debug.WriteLine($"Audio chunk SampleRate: {reader.WaveFormat.SampleRate}");
                    while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (bytesRead < buffer.Length)
                        {
                            // If the last read is smaller than the buffer, resize the array to fit the remaining data.
                            Array.Resize(ref buffer, bytesRead);
                        }

                        // Calculate the duration of the chunk
                        double totalSamples = bytesRead / (reader.WaveFormat.Channels * (reader.WaveFormat.BitsPerSample / 8.0));
                        double durationInSeconds = totalSamples / reader.WaveFormat.SampleRate;
                        TimeSpan chunkDuration = TimeSpan.FromSeconds(durationInSeconds);

                        // Invoke the event to process the chunk
                        OnAudioDataAvailable?.Invoke(this, new AudioDataEventArgs(buffer, chunkDuration));

                        // Reinitialize buffer if it was resized
                        if (buffer.Length != bytesPerChunk)
                        {
                            buffer = new byte[bytesPerChunk];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ReadWavFileInChunks: {ex.Message}");
            }
        }

        private bool DetectSilence(byte[] buffer)
        {
            float sum = 0;
            for (int i = 0; i < buffer.Length; i += 2)
            {
                short sample = BitConverter.ToInt16(buffer, i);
                sum += Math.Abs(sample / (float)short.MaxValue);
            }

            float averageAmplitude = sum / (buffer.Length / 2);

            return averageAmplitude < SilenceThreshold;
        }

     
        public void Dispose()
        {
            _waveIn?.Dispose();
            _bufferedWaveProvider?.ClearBuffer();
        }

    }

    public class AudioDataEventArgs : EventArgs
    {
        public byte[] Data { get; }
        public TimeSpan Duration { get; }

        public AudioDataEventArgs(byte[] data, TimeSpan duration)
        {
            Data = data;
            Duration = duration;
        }
    }





}

