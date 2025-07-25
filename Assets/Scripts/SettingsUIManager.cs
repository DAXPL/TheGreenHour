using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
namespace GreenHour.GameSettings
{
    public class SettingsUIManager : MonoBehaviour
    {
        [Header("Screen")]
        public TMP_Dropdown resolutionDropdown;
        public TMP_Dropdown graphicsDropdown;
        public Toggle fullscreenToggle;
        public Toggle vsyncToggle;

        [Header("Audio")]
        public AudioMixer audioMixer;
        public Slider masterSlider;
        public Slider musicSlider;
        public Slider sfxSlider;

        private Resolution[] availableResolutions;
        
        [ContextMenu("Initialize")]
        public void Initialize()
        {
            InitializeResolutions();
            InitializeGraphicsQuality();
            InitializeAudioSliders();
        }

        private void OnEnable()
        {
            Initialize();
        }
        //Screen
        private void InitializeResolutions()
        {
            availableResolutions = Screen.resolutions
                .Select(res => new Resolution { width = res.width, height = res.height, refreshRateRatio = res.refreshRateRatio })
                .Distinct()
                .ToArray();

            resolutionDropdown.ClearOptions();
            var options = availableResolutions.Select(r => $"{r.width}x{r.height} {r.refreshRateRatio}Hz").ToList();

            resolutionDropdown.AddOptions(options);

            int currentIndex = availableResolutions
                .ToList()
                .FindIndex(r => r.width == Screen.currentResolution.width && r.height == Screen.currentResolution.height);

            resolutionDropdown.value = currentIndex;
            resolutionDropdown.RefreshShownValue();

            resolutionDropdown.onValueChanged.AddListener(SetResolution);

            fullscreenToggle.isOn = Screen.fullScreen;
            vsyncToggle.isOn = QualitySettings.vSyncCount > 0;
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
            vsyncToggle.onValueChanged.AddListener(SetVSync);
        }

        private void SetVSync(bool arg)
        {
            QualitySettings.vSyncCount = arg ? 1 : 0;
            GameSettings.CurrentSettings.Vsync = arg;
        }

        private void SetFullscreen(bool arg)
        {
            Screen.fullScreen = arg;
            GameSettings.CurrentSettings.Fullscreen = arg;
        }

        private void SetResolution(int index)
        {
            Resolution res = availableResolutions[index];
            Screen.SetResolution(res.width, res.height, Screen.fullScreenMode, res.refreshRateRatio);

            GameSettings.CurrentSettings.ScreenWidth = res.width;
            GameSettings.CurrentSettings.ScreenHeight = res.height;
            GameSettings.SaveSettings();
        }

        //Quality
        private void InitializeGraphicsQuality()
        {
            graphicsDropdown.ClearOptions();
            var qualityLevels = QualitySettings.names.ToList();
            graphicsDropdown.AddOptions(qualityLevels);

            graphicsDropdown.value = QualitySettings.GetQualityLevel();
            graphicsDropdown.RefreshShownValue();

            graphicsDropdown.onValueChanged.AddListener(SetGraphicsQuality);
        }

        private void SetGraphicsQuality(int index)
        {
            QualitySettings.SetQualityLevel(index);

            GreenHour.GameSettings.GameSettings.CurrentSettings.GraphicsQuality = index;
            GreenHour.GameSettings.GameSettings.SaveSettings();
        }


        //Audio
        private void InitializeAudioSliders()
        {
            masterSlider.value = GameSettings.CurrentSettings.MasterVolume;
            musicSlider.value = GameSettings.CurrentSettings.MusicVolume;
            sfxSlider.value = GameSettings.CurrentSettings.SFXVolume;

            masterSlider.onValueChanged.AddListener(SetMasterVolume);
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);

            SetMixerVolume("Master", masterSlider.value);
            SetMixerVolume("Music", musicSlider.value);
            SetMixerVolume("SFX", sfxSlider.value);
        }

        private void SetMasterVolume(float value)
        {
            SetMixerVolume("Master", value);
            GameSettings.CurrentSettings.MasterVolume = value;
            GameSettings.SaveSettings();
        }

        private void SetMusicVolume(float value)
        {
            SetMixerVolume("Music", value);
            GameSettings.CurrentSettings.MusicVolume = value;
            GameSettings.SaveSettings();
        }

        private void SetSFXVolume(float value)
        {
            SetMixerVolume("SFX", value);
            GameSettings.CurrentSettings.SFXVolume = value;
            GameSettings.SaveSettings();
        }

        private void SetMixerVolume(string parameterName, float normalizedValue)
        {
            float dB = Mathf.Lerp(-80f, 0f, normalizedValue);
            audioMixer.SetFloat(parameterName, dB);
        }
    }
}