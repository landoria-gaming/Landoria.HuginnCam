using BepInEx.Configuration;
using Landoria.Shared;
using UnityEngine;

namespace Landoria.HuginnCam
{
    // Stores and restores the Huginn Cam configuration.
    internal static class Preference
    {
        private static ConfigEntry<KeyboardShortcut> recordingShortcut;

        internal static KeyboardShortcut RecordingShortcut => recordingShortcut.Value;

        // Creates the saved configuration entries used by the mod.
        internal static void Initialize(ConfigFile config)
        {
            recordingShortcut = config.Bind(
                "Controls",
                "RecordingShortcut",
                new KeyboardShortcut(KeyCode.F8),
                "Shortcut used to start or stop video recording.\n" +
                "\nExamples: F8 or F8 + LeftControl.\n" +
                "\nhttps://docs.unity3d.com/ScriptReference/KeyCode.html");
        }

        // Restores every setting and recreates the configuration file.
        internal static void RestoreDefaults(ConfigFile config)
        {
            recordingShortcut.Value = new KeyboardShortcut(KeyCode.F8);
            config.Save();
            ConfigWatcher.IgnoreCurrentFileVersion();
        }
    }
}
