
using System.Diagnostics;

namespace SemanticImageSearchAIPCT.UI.Audio
{
    public class ValidationService
    {

        public float[][] EnsureCorrectDimensions(float[][] melSpectrogram, int targetFrames)
        {
            int currentFrames = melSpectrogram.Length;
            int melChannels = melSpectrogram[0].Length;
            Debug.WriteLine($"currentFrames: {currentFrames}");
            Debug.WriteLine($"melChannels: {melChannels}");
            float[][] correctedSpectrogram = new float[targetFrames][];

            if (currentFrames < targetFrames)
            {
                for (int i = 0; i < targetFrames; i++)
                {
                    if (i < currentFrames)
                    {
                        correctedSpectrogram[i] = melSpectrogram[i];
                    }
                    else
                    {
                        correctedSpectrogram[i] = new float[melChannels]; // Zero padding
                    }
                }
            }
            else if (currentFrames > targetFrames)
            {
                correctedSpectrogram = melSpectrogram.Take(targetFrames).ToArray();
            }
            else
            {
                correctedSpectrogram = melSpectrogram;
            }

            return correctedSpectrogram;
        }

        public bool CheckMelSpectrogram(float[][] melSpectrogram)
        {
            // Iterate through each frame in the spectrogram
            foreach (var frame in melSpectrogram)
            {
                // Iterate through each frequency bin in the frame
                foreach (var value in frame)
                {
                    // Check if the value is not zero
                    if (value != 0.0f)
                    {
                        // Found a non-zero value, no need to check further
                        return true;
                    }
                }
            }
            // If we reach this point, no non-zero values were found
            return false;
        }

        public bool ContainsNonZeroValues(float[] data)
        {
            return data.Any(value => value != 0.0f);
        }
    }
}
