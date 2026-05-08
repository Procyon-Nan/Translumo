using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using System.Windows.Input;
using Translumo.Dialog;
using Translumo.Dialog.Stages;
using Translumo.Infrastructure.Language;
using Translumo.MVVM.Common;
using Translumo.MVVM.Models;
using Translumo.Translation;
using Translumo.Translation.AI;
using Translumo.Translation.Configuration;
using Translumo.TTS;
using Translumo.Utils;
using Translumo.Utils.Extensions;
using RelayCommand = Microsoft.Toolkit.Mvvm.Input.RelayCommand;

namespace Translumo.MVVM.ViewModels
{
    public sealed class LanguagesSettingsViewModel : BindableBase, IAdditionalPanelController, IDisposable
    {
        public event EventHandler<bool> PanelStateIsChanged;


        public IList<DisplayLanguage> AvailableLanguages { get; set; }
        public IList<DisplayLanguage> AvailableTranslationLanguages { get; set; }
        public IList<Translators> AvailableTranslators { get; set; }

        public TranslationConfiguration Model { get; set; }

        public TtsConfiguration TtsSettings { get; set; }

        private ObservableCollection<VoiceInfo> _availableVoices;
        public ObservableCollection<VoiceInfo> AvailableVoices
        {
            get => _availableVoices;
            set => SetProperty(ref _availableVoices, value);
        }

        private VoiceInfo _selectedVoice;
        public VoiceInfo SelectedVoice
        {
            get => _selectedVoice;
            set
            {
                SetProperty(ref _selectedVoice, value);
                if (value != null)
                {
                    Action updateVoiceAction = () =>
                    {
                        TtsSettings.SelectedVoiceName = value.Name;
                    };
                    
                    _ = ReconfigureTts(TtsSettings.TtsLanguage, TtsSettings.TtsSystem, updateVoiceAction);
                }
            }
        }

        public bool IsTtsWindowsSelected => TtsSettings.TtsSystem == TTSEngines.WindowsTTS;

        public bool IsTtsEnabled => TtsSettings.TtsSystem != TTSEngines.None;

        public bool IsAiTranslatorSelected => Translator == Translators.AI;

        public bool IsLegacyTranslatorSelected => Translator != Translators.AI;

        public ObservableCollection<string> AiOcrModels
        {
            get => _aiOcrModels;
            set
            {
                SetProperty(ref _aiOcrModels, value);
            }
        }

        public string AiOcrModelsStatus
        {
            get => _aiOcrModelsStatus;
            set
            {
                SetProperty(ref _aiOcrModelsStatus, value);
            }
        }

        public ObservableCollection<string> AiTranslationModels
        {
            get => _aiTranslationModels;
            set
            {
                SetProperty(ref _aiTranslationModels, value);
            }
        }

        public string AiTranslationModelsStatus
        {
            get => _aiTranslationModelsStatus;
            set
            {
                SetProperty(ref _aiTranslationModelsStatus, value);
            }
        }

        public DisplayLanguage SelectedAiPromptLanguage
        {
            get => _selectedAiPromptLanguage;
            set
            {
                SetProperty(ref _selectedAiPromptLanguage, value);
                OnPropertyChanged(nameof(AiPromptOverrideText));
            }
        }

        public string AiPromptOverrideText
        {
            get
            {
                return SelectedAiPromptLanguage == null
                    ? string.Empty
                    : Model.GetAiPromptOverride(SelectedAiPromptLanguage.LanguageDescriptor.Language);
            }
            set
            {
                if (SelectedAiPromptLanguage != null)
                {
                    Model.SetAiPromptOverride(SelectedAiPromptLanguage.LanguageDescriptor.Language, value);
                }
            }
        }

        public ObservableCollection<ProxyCardItem> ProxyCollection
        {
            get => _proxyCollection;
            set
            {
                SetProperty(ref _proxyCollection, value);
            }
        }
        public bool ProxySettingsIsOpened
        {
            get => _proxySettingsIsOpened;
            set
            {
                SetProperty(ref _proxySettingsIsOpened, value);
                PanelStateIsChanged?.Invoke(this, value);
            }
        }

        public Languages TranslateFromLang
        {
            get => Model.TranslateFromLang;
            set
            {
                ChangeSourceLanguage(value);
            }
        }

        public Languages TranslateToLang
        {
            get => Model.TranslateToLang;
            set
            {
                ChangeTargetLanguage(value);
            }
        }

        public Translators Translator
        {
            get => Model.Translator;
            set
            {
                if (Model.Translator == value)
                {
                    return;
                }

                Model.Translator = value;
                if (value == Translators.AI)
                {
                    ProxySettingsIsOpened = false;
                }

                OnPropertyChanged(nameof(Translator));
                OnPropertyChanged(nameof(IsAiTranslatorSelected));
                OnPropertyChanged(nameof(IsLegacyTranslatorSelected));
            }
        }

        public TTSEngines TtsSystem
        {
            get => TtsSettings.TtsSystem;
            set
            {
                ChangeTtsSystem(value);
            }
        }

        public ICommand ProxySettingsClickedCommand => new RelayCommand(OnProxySettingsClicked);
        public ICommand ProxyItemDeletedCommand => new RelayCommand<ProxyCardItem>(OnProxyItemDeletedCommand);
        public ICommand ProxyItemAddCommand => new RelayCommand(OnProxyItemAddCommand);
        public ICommand ProxySettingsSubmitCommand => new RelayCommand<bool>(OnProxySettingsSubmit);
        public ICommand LoadAiOcrModelsCommand => new RelayCommand(async () => await LoadAiOcrModelsAsync());
        public ICommand LoadAiTranslationModelsCommand => new RelayCommand(async () => await LoadAiTranslationModelsAsync());
        public ICommand ResetAiOcrPromptCommand => new RelayCommand(OnResetAiOcrPrompt);
        public ICommand ResetAiDefaultPromptCommand => new RelayCommand(OnResetAiDefaultPrompt);
        public ICommand ClearAiPromptOverrideCommand => new RelayCommand(OnClearAiPromptOverride);

        private ObservableCollection<ProxyCardItem> _proxyCollection;
        private ObservableCollection<string> _aiOcrModels;
        private ObservableCollection<string> _aiTranslationModels;
        private string _aiOcrModelsStatus;
        private string _aiTranslationModelsStatus;
        private DisplayLanguage _selectedAiPromptLanguage;
        private bool _proxySettingsIsOpened;

        private readonly DialogService _dialogService;
        private readonly LanguageService _languageService;
        private readonly OpenAiCompatibleClient _aiClient;
        private readonly ILogger _logger;

        public LanguagesSettingsViewModel(LanguageService languageService, TranslationConfiguration translationConfiguration,
            TtsConfiguration ttsConfiguration, DialogService dialogService,
            ILogger<LanguagesSettingsViewModel> logger)
        {
            var languages = languageService.GetAll(true)
                .Select(lang => (lang.TranslationOnly, new DisplayLanguage(lang, GetLanguageDisplayName(lang))))
                .ToArray();
            this.AvailableLanguages = languages
                .Where(lang => !lang.TranslationOnly)
                .OrderBy(lang => lang.Item2.DisplayName)
                .Select(lang => lang.Item2)
                .ToList();

            this.AvailableTranslationLanguages = languages
                .OrderBy(lang => lang.Item2.DisplayName)
                .Select(lang => lang.Item2)
                .ToList();
            this.AvailableTranslators = new[]
            {
                Translators.AI,
                Translators.Deepl,
                Translators.Yandex,
                Translators.Google,
                Translators.Papago
            };
            this.Model = translationConfiguration;
            this.TtsSettings = ttsConfiguration;
            this.TtsSettings.TtsLanguage = this.Model.TranslateToLang;
            this.SelectedAiPromptLanguage = this.AvailableTranslationLanguages.FirstOrDefault(lang =>
                lang.LanguageDescriptor.Language == this.Model.TranslateToLang);
            this.AiOcrModels = new ObservableCollection<string>();
            this.AiTranslationModels = new ObservableCollection<string>();
            InitializeAiModels();
            this.AiOcrModelsStatus = string.Empty;
            this.AiTranslationModelsStatus = string.Empty;

            this.AvailableVoices = new ObservableCollection<VoiceInfo>();
            
            if (this.TtsSettings.TtsSystem == TTSEngines.WindowsTTS)
            {
                var languageCode = languageService.GetLanguageDescriptor(this.TtsSettings.TtsLanguage).Code;
                LoadAvailableVoices(languageCode);
            }

            this._languageService = languageService;
            this._dialogService = dialogService;
            this._logger = logger;
            this._aiClient = new OpenAiCompatibleClient(logger);
        }

        private void LoadAvailableVoices(string languageCode)
        {
            try
            {
                var voices = GetAvailableVoicesForLanguage(languageCode);
                AvailableVoices = new ObservableCollection<VoiceInfo>(voices);
                
                if (!string.IsNullOrEmpty(TtsSettings.SelectedVoiceName))
                {
                    _selectedVoice = AvailableVoices.FirstOrDefault(v => 
                        v.Name.Equals(TtsSettings.SelectedVoiceName, StringComparison.OrdinalIgnoreCase));
                }
                
                if (_selectedVoice == null && AvailableVoices.Count > 0)
                {
                    _selectedVoice = AvailableVoices[0];
                    TtsSettings.SelectedVoiceName = _selectedVoice.Name;
                }
                
                OnPropertyChanged(nameof(SelectedVoice));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Load available voices error");
                AvailableVoices = new ObservableCollection<VoiceInfo>();
            }
        }

        private List<VoiceInfo> GetAvailableVoicesForLanguage(string languageTag)
        {
            using var synth = new SpeechSynthesizer();
            var result = new List<VoiceInfo>();
            
            try
            {
                var voices = synth.GetInstalledVoices(new CultureInfo(languageTag));
                if (voices.Count > 0)
                {
                    result.AddRange(voices.Select(v => v.VoiceInfo));
                    return result;
                }
            }
            catch
            {
            }

            try
            {
                var shortTag = languageTag.Split('-')[0];
                var voices = synth.GetInstalledVoices(new CultureInfo(shortTag));
                if (voices.Count > 0)
                {
                    result.AddRange(voices.Select(v => v.VoiceInfo));
                }
            }
            catch
            {
            }

            return result;
        }

        private void OnProxySettingsClicked()
        {
            InitializeProxyCollection();
            ProxySettingsIsOpened = true;
        }

        private void OnProxyItemDeletedCommand(ProxyCardItem itemToDelete)
        {
            _proxyCollection.Remove(itemToDelete);
        }

        private void OnProxyItemAddCommand()
        {
            _proxyCollection.Add(new ProxyCardItem());
        }

        private void OnProxySettingsSubmit(bool applyProxy)
        {
            if (applyProxy)
            {
                Model.ProxySettings = ProxyCollection.Where(pr => pr.IsValid())
                    .Select(pr => pr.MapTo<ProxyCardItem, Proxy>())
                    .ToList();
            }

            ProxySettingsIsOpened = false;
        }

        private void OnResetAiOcrPrompt()
        {
            Model.AiOcrSystemPrompt = TranslationConfiguration.DefaultAiOcrSystemPromptTemplate;
        }

        private void OnResetAiDefaultPrompt()
        {
            Model.AiDefaultSystemPrompt = TranslationConfiguration.DefaultAiSystemPromptTemplate;
        }

        private void OnClearAiPromptOverride()
        {
            if (SelectedAiPromptLanguage == null)
            {
                return;
            }

            Model.SetAiPromptOverride(SelectedAiPromptLanguage.LanguageDescriptor.Language, string.Empty);
            OnPropertyChanged(nameof(AiPromptOverrideText));
        }

        private Task LoadAiOcrModelsAsync()
        {
            return LoadAiModelsAsync(
                Model.AiOcrBaseUrl,
                Model.AiOcrApiKey,
                Model.AiOcrModel,
                Model.AiOcrRequestTimeoutSeconds,
                models => AiOcrModels = models,
                status => AiOcrModelsStatus = status,
                nameof(AiOcrModels),
                model => Model.AiOcrModel = model,
                "Str.AiSettings.LoadingOcrModels",
                "Str.AiSettings.OcrModelsLoaded",
                "Failed to load AI OCR models");
        }

        private Task LoadAiTranslationModelsAsync()
        {
            return LoadAiModelsAsync(
                Model.AiTranslationBaseUrl,
                Model.AiTranslationApiKey,
                Model.AiTranslationModel,
                Model.AiTranslationRequestTimeoutSeconds,
                models => AiTranslationModels = models,
                status => AiTranslationModelsStatus = status,
                nameof(AiTranslationModels),
                model => Model.AiTranslationModel = model,
                "Str.AiSettings.LoadingTranslationModels",
                "Str.AiSettings.TranslationModelsLoaded",
                "Failed to load AI translation models");
        }

        private async Task LoadAiModelsAsync(
            string baseUrl,
            string apiKey,
            string selectedModel,
            int timeoutSeconds,
            Action<ObservableCollection<string>> setModels,
            Action<string> setStatus,
            string modelsPropertyName,
            Action<string> setSelectedModel,
            string loadingLocalizationKey,
            string loadedLocalizationKey,
            string logMessage)
        {
            try
            {
                setStatus(LocalizationManager.GetValue(loadingLocalizationKey));
                var models = await _aiClient.GetModelsAsync(baseUrl, apiKey, timeoutSeconds).ConfigureAwait(true);

                var loadedModels = new ObservableCollection<string>(models);
                if (!string.IsNullOrWhiteSpace(selectedModel) && !loadedModels.Contains(selectedModel))
                {
                    loadedModels.Insert(0, selectedModel);
                }

                setModels(loadedModels);
                if (loadedModels.Count > 0 && string.IsNullOrWhiteSpace(selectedModel))
                {
                    setSelectedModel(loadedModels[0]);
                }

                setStatus(string.Format(LocalizationManager.GetValue(loadedLocalizationKey), loadedModels.Count));
                OnPropertyChanged(modelsPropertyName);
            }
            catch (Exception ex)
            {
                setStatus(ex.Message);
                _logger.LogError(ex, logMessage);
            }
        }

        private void InitializeAiModels()
        {
            if (!string.IsNullOrWhiteSpace(Model.AiOcrModel))
            {
                AiOcrModels.Add(Model.AiOcrModel);
            }

            if (!string.IsNullOrWhiteSpace(Model.AiTranslationModel))
            {
                AiTranslationModels.Add(Model.AiTranslationModel);
            }
        }

        private async Task ChangeSourceLanguage(Languages language)
        {
            try
            {
                var changeLangStage = StagesFactory.CreateLanguageChangeStages(_dialogService, () => Model.TranslateFromLang = language,
                    _logger);

                await changeLangStage.ExecuteAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error during source language change");
            }

            OnPropertyChanged(nameof(TranslateFromLang));
        }

        private async Task ChangeTargetLanguage(Languages language)
        {
            var changeLanguageAction = () =>
            {
                this.TtsSettings.TtsLanguage = language;
                this.Model.TranslateToLang = language;
                this.SelectedAiPromptLanguage = this.AvailableTranslationLanguages.FirstOrDefault(lang =>
                    lang.LanguageDescriptor.Language == language);
                
                if (TtsSettings.TtsSystem == TTSEngines.WindowsTTS)
                {
                    var langCode = _languageService.GetLanguageDescriptor(language).Code;
                    LoadAvailableVoices(langCode);
                }

                OnPropertyChanged(nameof(AiPromptOverrideText));
            };

            await this.ReconfigureTts(language, TtsSettings.TtsSystem, changeLanguageAction);
            OnPropertyChanged(nameof(TranslateToLang));
            OnPropertyChanged(nameof(IsTtsWindowsSelected));
            OnPropertyChanged(nameof(IsTtsEnabled));
        }

        private async Task ChangeTtsSystem(TTSEngines engine)
        {
            Action changeTtsEngineAction = () => 
            {
                this.TtsSettings.TtsSystem = engine;
                
                if (engine == TTSEngines.WindowsTTS)
                {
                    var langCode = _languageService.GetLanguageDescriptor(TtsSettings.TtsLanguage).Code;
                    LoadAvailableVoices(langCode);
                }
                else
                {
                    AvailableVoices = new ObservableCollection<VoiceInfo>();
                    _selectedVoice = null;
                    OnPropertyChanged(nameof(SelectedVoice));
                }
            };
            
            await this.ReconfigureTts(TtsSettings.TtsLanguage, engine, changeTtsEngineAction);
            OnPropertyChanged(nameof(TtsSystem));
            OnPropertyChanged(nameof(IsTtsWindowsSelected));
            OnPropertyChanged(nameof(IsTtsEnabled));
        }

        private async Task ReconfigureTts(Languages language, TTSEngines engine, Action changeParameter)
        {
            try
            {
                var changeLangStage = StagesFactory.CreateLanguageChangeStages(
                    _dialogService,
                    changeParameter,
                    _logger);

                if (engine == TTSEngines.WindowsTTS
                    && !TtsSettings.InstalledWinTtsLanguages.Contains(language))
                {
                    var langCode = _languageService.GetLanguageDescriptor(language).Code;
                    changeLangStage.AddNextStage(new ActionInteractionStage(_dialogService, () =>
                    {
                        this.TtsSettings.InstalledWinTtsLanguages.Add(language);
                        return Task.CompletedTask;
                    }));
                    changeLangStage = StagesFactory.CreateWindowsTtsCheckingStages(_dialogService, langCode, changeLangStage, _logger);
                }
                await changeLangStage.ExecuteAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error during source language change");
            }
        }

        private string GetLanguageDisplayName(LanguageDescriptor languageDescriptor)
        {
            return LocalizationManager.GetValue($"Str.Languages.{languageDescriptor.Language}", false,
               OnLocalizedValueChanged, this);
        }

        private void OnLocalizedValueChanged(string key, string oldValue)
        {
            var availableLang = AvailableTranslationLanguages.FirstOrDefault(lang => lang.DisplayName == oldValue);
            if (availableLang != null)
            {
                availableLang.DisplayName = LocalizationManager.GetValue(key, false, OnLocalizedValueChanged, this);
                return;
            }
        }

        private void InitializeProxyCollection()
        {
            ProxyCollection = new ObservableCollection<ProxyCardItem>(Model.ProxySettings.Select(st => st.MapTo<Proxy, ProxyCardItem>()));
        }

        public void ClosePanel()
        {
            ProxySettingsIsOpened = false;
        }

        public void Dispose()
        {
            LocalizationManager.ReleaseChangedValuesCallbacks(this);
        }
    }
}
