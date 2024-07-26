using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;


namespace SemanticImageSearchAIPCT.UI.Audio
{
    public class MelSpectrogramConverter
    {
        // Convert frequency from Hz to Mel
        public static double HzToMel(double freqHz)
        {
            return 2595 * Math.Log10(1 + freqHz / 700.0);
        }

        // Convert frequency from Mel to Hz
        public static double MelToHz(double mel)
        {
            return 700 * (Math.Pow(10, mel / 2595) - 1);
        }

        #region Methods
        /// <summary>
        //1.	Extract Audio Frames and Apply Window Function
        //2.	Compute Fourier Transform(FFT) to Get Frequency Domain Data
        //3.	Convert FFT Output to Power Spectrogram
        //4.	Apply Mel Filter Bank to Convert Power Spectrogram to Mel Spectrogram
        /// </summary>
        public static float[][] GenerateMelSpectrogram(float[] audioData, int sampleRate, int fftSize, int hopSize, int melChannels, float[][] melFilterbank)
        {
            // Calculate the number of frames in the spectrogram
            int numFrames = 1 + (audioData.Length - fftSize) / hopSize;
            float[][] melSpectrogram = new float[numFrames][];

            //double[] window = MathNet.Numerics.Window.Hamming(fftSize);   

            // Using Hann window from MathNet.Numerics
            double[] window = MathNet.Numerics.Window.Hann(fftSize);

            for (int i = 0; i < numFrames; i++)
            {
                // 1.Extract Audio Frames and Apply Window Function
                // Slice the continuous audio signal into overlapping frames(using a technique like windowing to
                // reduce spectral leakage).
                // Apply a window function(e.g., Hamming) to each frame to minimize the edge effects in the FFT.

                Complex32[] frame = new Complex32[fftSize];
                for (int j = 0; j < fftSize; j++)
                {
                    int audioDataIndex = i * hopSize + j;
                    double windowedValue = (audioDataIndex < audioData.Length) ? audioData[audioDataIndex] * window[j] : 0;
                    frame[j] = new Complex32((float)windowedValue, 0);
                }


                // 2.Compute Fourier Transform (FFT)
                //For each frame, compute the FFT to transform the data from the time domain to the frequency domain.
                //This step provides the frequency components present in each frame.
                // Perform FFT on the frame

                Fourier.Forward(frame, FourierOptions.Matlab);


                // 3.Convert FFT Output to Power Spectrogram
                // Calculate the power spectrum from the FFT output for each frame, which involves squaring
                // the magnitude of each FFT bin.

                float[] powerSpectrum = new float[fftSize / 2];
                for (int j = 0; j < fftSize / 2; j++)
                {
                    powerSpectrum[j] = (float)frame[j].MagnitudeSquared;
                }

                //Debug.WriteLine($"Power Spectrum for Frame {i}: {string.Join(", ", powerSpectrum)}");


                // 4.Apply the Mel filterbank to the power spectrum
                // Use a mel filter bank to map the power spectrum to the mel scale, which is a perceptual
                // scale of pitches judged by listeners to be equal in distance from one another.

                float[] melFrame = ApplyMelFilterbank(powerSpectrum, melFilterbank);
                melSpectrogram[i] = melFrame;
            }

            return melSpectrogram;
        }
        #endregion

        private static double TruncateToTwoDecimalPlaces(double value)
        {
            return Math.Truncate(value * 100) / 100;
        }

        public static float[][] CreateMelFilterbank_log(int sampleRate, int fftSize, int numMelFilters)
        {
            string filePath = @"C:\AIExamples\Wavfile\C_createfilter.txt";
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                double maxFrequency = sampleRate / 2.0; // Nyquist frequency
                double minMel = HzToMel(0); // Typically 0 Hz
                double maxMel = HzToMel(maxFrequency);

                // Compute Mel filter points in Hz
                double[] melPoints = new double[numMelFilters + 2]; // +2 for the edges
                for (int i = 0; i < melPoints.Length; i++)
                {
                    double mel = minMel + (maxMel - minMel) / (numMelFilters + 1) * i;
                    melPoints[i] = TruncateToTwoDecimalPlaces(MelToHz(mel));
                }

                // Log Mel points and corresponding frequencies for verification
                writer.WriteLine("Mel Points and Corresponding Frequencies:");
                foreach (var point in melPoints)
                {
                    writer.WriteLine($"Mel: {HzToMel(point)}, Frequency: {point}");
                }

                // Convert Mel filter points to FFT bin numbers
                double[] bin = new double[melPoints.Length];
                //writer.WriteLine("Frequency to Bin Mapping:");
                for (int i = 0; i < melPoints.Length; i++)
                {
                    bin[i] = Math.Floor((fftSize + 1) * melPoints[i] / sampleRate);
                    writer.WriteLine($"Frequency {melPoints[i]} Hz corresponds to bin {bin[i]}");

                }

                // Create the Mel filterbank
                float[][] filterbank = new float[numMelFilters][];
                writer.WriteLine("Bin Ranges:");
                for (int i = 0; i < numMelFilters; i++)
                {
                    filterbank[i] = new float[fftSize / 2];
                    int start = (int)bin[i];
                    int peak = (int)bin[i + 1];
                    int end = (int)bin[i + 2];
                    writer.WriteLine($"Filter {i}: Start {start}, Peak {peak}, End {end}");

                    // Populate filter values
                    for (int j = start; j < peak; j++)
                    {
                        filterbank[i][j] = (float)((j - bin[i]) / (bin[i + 1] - bin[i]));
                    }
                    for (int j = peak; j < end; j++)
                    {
                        filterbank[i][j] = (float)((bin[i + 2] - j) / (bin[i + 2] - bin[i + 1]));
                    }
                }
                return filterbank;
            }
        }

        public static float[][] CreateMelFilterbank(int sampleRate, int fftSize, int numMelFilters)
        {
            double maxFrequency = sampleRate / 2.0; // Nyquist frequency
            double minMel = HzToMel(0); // Typically 0 Hz
            double maxMel = HzToMel(maxFrequency);

            // Compute Mel filter points in Hz
            double[] melPoints = new double[numMelFilters + 2]; // +2 for the edges
            for (int i = 0; i < melPoints.Length; i++)
            {
                double mel = minMel + (maxMel - minMel) / (numMelFilters + 1) * i;
                melPoints[i] = TruncateToTwoDecimalPlaces(MelToHz(mel));
            }

            // Convert Mel filter points to FFT bin numbers
            double[] bin = new double[melPoints.Length];

            for (int i = 0; i < melPoints.Length; i++)
            {
                bin[i] = Math.Floor((fftSize + 1) * melPoints[i] / sampleRate);

            }

            // Create the Mel filterbank
            float[][] filterbank = new float[numMelFilters][];

            for (int i = 0; i < numMelFilters; i++)
            {
                filterbank[i] = new float[fftSize / 2];
                int start = (int)bin[i];
                int peak = (int)bin[i + 1];
                int end = (int)bin[i + 2];


                // Populate filter values
                for (int j = start; j < peak; j++)
                {
                    filterbank[i][j] = (float)((j - bin[i]) / (bin[i + 1] - bin[i]));
                }
                for (int j = peak; j < end; j++)
                {
                    filterbank[i][j] = (float)((bin[i + 2] - j) / (bin[i + 2] - bin[i + 1]));
                }
            }
            return filterbank;
        }


        private static float[] ApplyMelFilterbank(float[] powerSpectrum, float[][] melFilterbank)
        {
            // Initialize the Mel spectrum array with the number of Mel filters
            float[] melSpectrum = new float[melFilterbank.Length];

            // Iterate over each Mel filter
            for (int i = 0; i < melFilterbank.Length; i++)
            {
                float melEnergy = 0f; // Accumulator for the energy in this Mel band
                float[] filter = melFilterbank[i]; // Current Mel filter

                // Apply the current Mel filter to the power spectrum
                for (int j = 0; j < filter.Length; j++)
                {
                    melEnergy += powerSpectrum[j] * filter[j];
                    //Debug.WriteLine($"powerSpectrum: {powerSpectrum[j]}");
                    //Debug.WriteLine($"melFilterbank: {melFilterbank[i][j]}");
                    //if (melFilterbank[i][j] != 0)
                    //{
                    //    Debug.WriteLine($"Satya melFilterbank= {melFilterbank[i][j]}");
                    //}
                }

                melSpectrum[i] = melEnergy; // Assign the calculated energy to the Mel spectrum

                // Log the calculated Mel energy for this filter
                //Debug.WriteLine($"Mel Filter {i}: Energy = {melEnergy}");
            }

            return melSpectrum;
        }

        //5. Apply Logarithmic Scaling
        //Convert the linear mel spectrogram values to a logarithmic scale to better match human 
        //auditory perception and to compress the dynamic range, making the model less sensitive to loudness variations.
        public static float[][] LogarithmicMelSpectrogram(float[][] melSpectrogram)
        {
            int numFrames = melSpectrogram.Length;
            int melBands = melSpectrogram[0].Length;
            float[][] logMelSpectrogram = new float[numFrames][];

            for (int i = 0; i < numFrames; i++)
            {
                logMelSpectrogram[i] = new float[melBands];
                for (int j = 0; j < melBands; j++)
                {
                    // Ensure no log of zero by adding a small constant, often machine epsilon or a small value like 1e-10
                    logMelSpectrogram[i][j] = (float)Math.Log(melSpectrogram[i][j] + 1e-10);
                }
            }

            return logMelSpectrogram;
        }



    }
}
