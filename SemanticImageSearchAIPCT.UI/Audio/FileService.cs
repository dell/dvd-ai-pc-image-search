

namespace SemanticImageSearchAIPCT.UI.Audio
{
    using Newtonsoft.Json;
    using System;
    using System.Diagnostics;
    using System.IO;

    public class FileService
    {
        public void SaveFloatArrayToFile(float[] floatData, string folderPath)
        {
            // Ensure the folder exists
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // Create a unique file name based on the current date and time
            string fileName = $"Float_audioData_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string filePath = Path.Combine(folderPath, fileName);

            // Convert float array to string with each float on a new line
            string content = string.Join(Environment.NewLine, floatData);

            try
            {
                // Write the string content to a new text file
                File.WriteAllText(filePath, content);
                Debug.WriteLine($"Audio  saved successfully: {filePath}");
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., access rights, disk not available)
                Debug.WriteLine("An error occurred: " + ex.Message);
            }
        }

        public void SaveMelSpectrogramToFile(float[][] logMelSpectrogram, string directoryPath)
        {
            // Ensure the directory exists
            Directory.CreateDirectory(directoryPath);

            // Create a unique file name with the current timestamp
            string fileName = $"LogMelSpe_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string filePath = Path.Combine(directoryPath, fileName);

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                // Iterate over each frame in the mel spectrogram
                foreach (float[] frame in logMelSpectrogram)
                {
                    // For each frame, iterate over each mel band value
                    for (int i = 0; i < frame.Length; i++)
                    {
                        // Write the value to the file, followed by a space or comma
                        writer.Write(frame[i].ToString("F6")); // "F6" for formatting to 6 decimal places

                        // Check if we should write a delimiter or a new line
                        if (i < frame.Length - 1)
                        {
                            writer.Write(" "); // delimiter between mel band values in the same frame
                        }
                    }

                    // After writing all mel band values for a frame, write a new line
                    writer.WriteLine();
                }
            }
        }

        public void SaveFilterbankToJson(float[][] filterbank, string directoryPath)
        {
            //string jsonFilePath = @"C:\AIPCTemplate\SemanticImageSearchAIPCT\SemanticImageSearchAIPCT.UI\assets\filters.json";

            Directory.CreateDirectory(directoryPath);

            // Create a unique file name with the current timestamp
            string fileName = $"MelFilter{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string filePath = Path.Combine(directoryPath, fileName);

            // Serialize the filterbank to JSON
            string json = JsonConvert.SerializeObject(filterbank, Formatting.Indented);

            // Save the JSON to a file
            File.WriteAllText(filePath, json);
        }

    }

}
