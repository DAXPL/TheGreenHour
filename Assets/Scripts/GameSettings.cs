
using System.IO;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using static UnityEngine.Rendering.DebugUI;

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
            Debug.Log($"Loaded settings: {SettingsFilePath}");
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

    public static class GraphicsSettingsApplier
    {
        private static AudioMixer audioMixer;
        private static AudioMixer AudioMixer
        {
            get
            {
                if (audioMixer == null)
                {
                    audioMixer = Resources.Load<AudioMixer>("Audio/MainAudioMixer");
                    if (audioMixer == null)
                        Debug.LogError("MainMixer.mixer not found in Resources/Audio!");
                }
                return audioMixer;
            }
        }
        public static void LoadSettings()
        {
            Settings settings = GameSettings.CurrentSettings;
            SetMasterVolume(settings.MasterVolume);
            SetMusicVolume(settings.MusicVolume);
            SetSFXVolume(settings.SFXVolume);
            SetFullscreen(settings.Fullscreen);
            Resolution res = new Resolution();
            res.width = settings.ScreenWidth;
            res.height = settings.ScreenHeight;
            SetResolution(res);
            ApplyVsync(settings.Vsync);
            SetGraphicsQuality(settings.GraphicsQuality);
            Debug.Log("Applied all game settings.");
        }
        public static void SetMixerVolume(string parameterName, float normalizedValue)
        {
            if(AudioMixer == null)
            {
                Debug.LogError("No audio mixer!");
                return;
            }
            float dB = Mathf.Lerp(-80f, 0f, normalizedValue);
            AudioMixer.SetFloat(parameterName, dB);
        }
        
        public static void SetMasterVolume(float value)
        {
            SetMixerVolume("Master", value);
            GameSettings.CurrentSettings.MasterVolume = value;
        }

        public static void SetMusicVolume(float value)
        {
            SetMixerVolume("Music", value);
            GameSettings.CurrentSettings.MusicVolume = value;
        }

        public static void SetSFXVolume(float value)
        {
            SetMixerVolume("SFX", value);
            GameSettings.CurrentSettings.SFXVolume = value;
        }
        
        public static void SetFullscreen(bool arg)
        {
            Screen.fullScreen = arg;
            GameSettings.CurrentSettings.Fullscreen = arg;
        }

        public static void SetResolution(Resolution res)
        {
            Screen.SetResolution(res.width, res.height, Screen.fullScreenMode, res.refreshRateRatio);

            GameSettings.CurrentSettings.ScreenWidth = res.width;
            GameSettings.CurrentSettings.ScreenHeight = res.height;
            GameSettings.SaveSettings();
        }

        public static void ApplyVsync(bool arg)
        {
            QualitySettings.vSyncCount = arg ? 1 : 0;
            GameSettings.CurrentSettings.Vsync = arg;
        }

        public static void SetGraphicsQuality(int index)
        {
            QualitySettings.SetQualityLevel(index);
            GameSettings.CurrentSettings.GraphicsQuality = index;
        }

        //"None","DLSS","FSR"
        public static void SetUpscaling(int value)
        {
            GameSettings.CurrentSettings.UpscalingMethod = value;
            HDAdditionalCameraData hdCam = GetHDAdditionalData();
            if(hdCam == null) return;

            hdCam.allowDynamicResolution = value > 0;

            hdCam.allowDeepLearningSuperSampling = (value == 1);
            hdCam.deepLearningSuperSamplingUseCustomQualitySettings = (value == 1);

            hdCam.allowFidelityFX2SuperResolution = (value == 2);
            hdCam.fidelityFX2SuperResolutionUseCustomQualitySettings = (value == 2);
        }
        public static void SetUpscalingLevel(int value)
        {
            GameSettings.CurrentSettings.UpscalingLevel = value;
            HDAdditionalCameraData hdCam = GetHDAdditionalData();
            if (hdCam == null) return;

            hdCam.fidelityFX2SuperResolutionQuality = (uint)value;
            hdCam.deepLearningSuperSamplingQuality = (uint)value;
        }

        public static HDAdditionalCameraData GetHDAdditionalData()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("No Main Camera found for upscaling!");
                return null;
            }

            if (!cam.TryGetComponent<HDAdditionalCameraData>(out var hdCam))
            {
                Debug.LogError("Main Camera has no HDAdditionalCameraData (HDRP required).");
                return null;
            }

            return hdCam;
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
        public int UpscalingMethod = 0;
        public int UpscalingLevel = 0;
        public int GraphicsQuality = 2;
        public float SmellIntensity = 1.0f;
        public bool enableImmersionGiver = false;
        public bool enableImmersionReader = false;
        public string selectedMicrophone = "";
    }
}