using GreenHour.Gameplay.Events;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace GreenHour.GameSettings
{
    public class SettingsUIManager : MonoBehaviour
    {
        public GameObject defaultItem;
        private EventSystem eventSystem;
        [Header("Screen")]
        public TMP_Dropdown resolutionDropdown;
        public TMP_Dropdown graphicsDropdown;
        public TMP_Dropdown upscalingDropdown;
        public TMP_Dropdown upscalinglevelDropdown;
        [Space]
        public Toggle fullscreenToggle;
        public Toggle vsyncToggle;
        public Toggle immersionInToggle;
        public Toggle immersionOutToggle;

        [Header("Audio")]
        public Slider masterSlider;
        public Slider musicSlider;
        public Slider sfxSlider;
        public Slider smellSlider;

        private Resolution[] availableResolutions;

        [Header("Events")]
        [SerializeField] private GameEvent enableMovement;
        [SerializeField] private GameEvent disableMovement;

        [ContextMenu("Initialize")]

        private void Start()
        {
            if (eventSystem == null)
            {
                eventSystem = FindFirstObjectByType<EventSystem>();
            }

            InitializeResolutions();
            InitializeGraphicsQuality();
            InitializeAudioSliders();
            InitializeOther();
        }

        private void OnEnable()
        {
            if (eventSystem == null) eventSystem = FindFirstObjectByType<EventSystem>();
            if (disableMovement)disableMovement.Raise();
            if (eventSystem && defaultItem) eventSystem.SetSelectedGameObject(defaultItem);
        }

        private void OnDisable()
        {
            if(enableMovement)enableMovement.Raise();
            SaveSettings();
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

            if (fullscreenToggle)
            {
                fullscreenToggle.isOn = Screen.fullScreen;
                if (fullscreenToggle) fullscreenToggle.onValueChanged.AddListener(GraphicsSettingsApplier.SetFullscreen);
            }

            if (vsyncToggle)
            { 
                vsyncToggle.isOn = QualitySettings.vSyncCount > 0;
                vsyncToggle.onValueChanged.AddListener(GraphicsSettingsApplier.ApplyVsync);
            }
            if (upscalingDropdown)
            {
                List<string> upscalerOptions = new List<string> { "None", "DLSS", "FSR" };
                upscalingDropdown.AddOptions(upscalerOptions);
                upscalingDropdown.onValueChanged.AddListener(GraphicsSettingsApplier.SetUpscaling);
                upscalingDropdown.onValueChanged.AddListener(index =>
                {
                    bool enableLevels = index != 0;
                    if (upscalinglevelDropdown) upscalinglevelDropdown.interactable = enableLevels;
                });
            }

            if (upscalinglevelDropdown)
            {
                List<string> upscalerLevels = new List<string> { "Quality", "Balanced", "Performance", "Maximum Performance" };
                upscalinglevelDropdown.AddOptions(upscalerLevels);
                upscalinglevelDropdown.onValueChanged.AddListener(GraphicsSettingsApplier.SetUpscalingLevel);
            }    
        }

        private void SetResolution(int index)
        {
            GraphicsSettingsApplier.SetResolution(availableResolutions[index]);
        }

        //Quality
        private void InitializeGraphicsQuality()
        {
            graphicsDropdown.ClearOptions();
            var qualityLevels = QualitySettings.names.ToList();
            graphicsDropdown.AddOptions(qualityLevels);

            graphicsDropdown.value = QualitySettings.GetQualityLevel();
            graphicsDropdown.RefreshShownValue();

            graphicsDropdown.onValueChanged.AddListener(GraphicsSettingsApplier.SetGraphicsQuality);
        }
        
        //Audio
        private void InitializeAudioSliders()
        {
            masterSlider.value = GameSettings.CurrentSettings.MasterVolume;
            musicSlider.value = GameSettings.CurrentSettings.MusicVolume;
            sfxSlider.value = GameSettings.CurrentSettings.SFXVolume;
            masterSlider.onValueChanged.AddListener(GraphicsSettingsApplier.SetMasterVolume);
            musicSlider.onValueChanged.AddListener(GraphicsSettingsApplier.SetMusicVolume);
            sfxSlider.onValueChanged.AddListener(GraphicsSettingsApplier.SetSFXVolume);
        }

        //Overall
        private void InitializeOther()
        {
            if (smellSlider)
            {
                smellSlider.value = GameSettings.CurrentSettings.SmellIntensity;
                smellSlider.onValueChanged.AddListener(GraphicsSettingsApplier.SetSmellIntensity);
            }
            
            if (immersionOutToggle)
            {
                immersionOutToggle.isOn = GameSettings.CurrentSettings.enableImmersionGiver;
                immersionOutToggle.onValueChanged.AddListener(GraphicsSettingsApplier.SetImmersionGiver);
            }
            
            if (immersionOutToggle)
            {
                immersionOutToggle.isOn = GameSettings.CurrentSettings.enableImmersionReader;
                immersionInToggle.onValueChanged.AddListener(GraphicsSettingsApplier.SetImmersionReader);
            }
            

        }

        [ContextMenu("Save settings")]
        public void SaveSettings()
        {
            GameSettings.SaveSettings();
        }
    }
}