using BepInEx.Configuration;

namespace Landoria.HuginnCam
{
    // Stores and exposes the user-configurable HuginnCam settings.
    internal static class HuginnCamPreference
    {
        private static ConfigEntry<string> _ffmpegPath;
        private static ConfigEntry<bool> _generatePreviewImage;
        private static ConfigEntry<bool> _keepIntermediateFile;
        private static ConfigEntry<int> _antiAliasingSamples;

        internal static string FfmpegPath => _ffmpegPath.Value?.Trim();
        internal static bool GeneratePreviewImage => _generatePreviewImage.Value;
        internal static bool KeepIntermediateFile => _keepIntermediateFile.Value;
        internal static int AntiAliasingSamples
        {
            get
            {
                int value = _antiAliasingSamples.Value;
                return value == 2 || value == 4 || value == 8 ? value : 1;
            }
        }

        // Registers HuginnCam settings with the BepInEx configuration file.
        internal static void Initialize(ConfigFile config)
        {
            _ffmpegPath = config.Bind(
                "Recording",
                "FfmpegPath",
                string.Empty,
                "Full path to ffmpeg.exe. Leave empty to resolve ffmpeg.exe from PATH.");
            _keepIntermediateFile = config.Bind(
                "Recording",
                "KeepIntermediateFile",
                false,
                "Keep the intermediate MKV after the MP4 is created successfully.");
            _generatePreviewImage = config.Bind(
                "Recording",
                "GeneratePreviewImage",
                true,
                "Save camera and gameplay PNG images immediately before video capture starts.");
            _antiAliasingSamples = config.Bind(
                "Recording",
                "AntiAliasingSamples",
                4,
                "MSAA sample count for the cinematic camera. Supported values: 1, 2, 4 or 8.");
        }
    }
}
