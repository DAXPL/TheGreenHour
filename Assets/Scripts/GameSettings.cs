
using System.IO;
using UnityEngine;

namespace GreenHour.GameSettings
{
    public static class GameSettings
    {
        private static Settings _currentSettings;
        public static Settings CurrentSettings
        {
            get
            {
                if (_currentSettings == null)
                    LoadSettings();
                return _currentSettings;
            }
            private set
            {
                _currentSettings = value;
            }
        }

        private static readonly string SettingsFilePath = Path.Combine(Application.persistentDataPath, "settings.json");

        public static void LoadSettings()
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                _currentSettings = JsonUtility.FromJson<Settings>(json);
            }
            else
            {
                _currentSettings = new Settings();
                SaveSettings();
            }
            Debug.Log($"{SettingsFilePath}");
        }

        public static void SaveSettings()
        {
            if(_currentSettings == null)
            {
                Debug.LogError("CurrentSettings is null, cannot save settings.");
                return;
            }
            string json = JsonUtility.ToJson(_currentSettings, true);
            File.WriteAllText(SettingsFilePath, json);
        }
    }

    [System.Serializable]
    public class Settings
    {
        public float MasterVolume = 1.0f;
        public float MusicVolume = 1.0f;
        public float SFXVolume = 1.0f;
        public bool Fullscreen = true;
        public bool Vsync = true;
        public int ScreenWidth = 1920;
        public int ScreenHeight = 1080;
        public int GraphicsQuality = 2;
        public float SmellIntensity = 1.0f;
        public bool enableImmersionGiver = false;
        public bool enableImmersionReader = false;
        public string selectedMicrophone = "";
    }
}