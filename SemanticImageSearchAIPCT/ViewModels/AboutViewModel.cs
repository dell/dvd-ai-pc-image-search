using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace SemanticImageSearchAIPCT.ViewModels
{
    internal class AboutViewModel
    {
        public string Title => "Semantic Image Search Sample App";
        public string OpenAiClipModelInfoUrl => "https://huggingface.co/qualcomm/OpenAI-Clip";
        public string WhisperModelInfoUrl => "https://huggingface.co/openai/whisper-large-v3";
        public string Message => "This MAUI application utilizes OpenAI-Clip to perform semantic image search on a set of images, displaying related images based on user prompts. You can also use your voice to generate the prompts.";
        public ICommand ShowOpenAiClipCommand { get; }
        public ICommand ShowWhisperModelCommand { get; }

        public AboutViewModel()
        {
            ShowOpenAiClipCommand = new AsyncRelayCommand(ShowOpenAiClipModelInfo);
            ShowWhisperModelCommand = new AsyncRelayCommand(ShowWhisperModelInfo);
        }

        async Task ShowOpenAiClipModelInfo() =>
            await Launcher.Default.OpenAsync(OpenAiClipModelInfoUrl);

        async Task ShowWhisperModelInfo() =>
            await Launcher.Default.OpenAsync(WhisperModelInfoUrl);
    }
}
