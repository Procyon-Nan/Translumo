using System;
using System.Collections.Generic;
using System.Linq;
using Translumo.Infrastructure.Language;
using Translumo.Translation;
using Translumo.Utils;

namespace Translumo.Translation.Configuration
{
    public class TranslationConfiguration : BindableBase
    {
        public const string TranslationTextStartTag = "<text>";
        public const string TranslationTextEndTag = "</text>";
        public const string AiNoTextMarker = "<no_text/>";
        public const int DefaultAiRequestTimeoutSeconds = 120;
        public const int MinAiRequestTimeoutSeconds = 5;
        public const int MaxAiRequestTimeoutSeconds = 600;
        public const string DefaultAiOcrSystemPromptTemplate = "You are a professional OCR engine. Read all visible text in the image. The source language hint is {sourceLanguage}. Preserve line breaks and reading order when possible. Output only the extracted text. Do not translate, explain, or add markdown. If no readable text exists, output exactly <no_text/>.";
        public const string DefaultAiSystemPromptTemplate = "You are a professional translation engine. Translate from {sourceLanguage} to {targetLanguage}. Only translate the content inside <text>...</text>. Preserve meaning, names, numbers, and line breaks when possible. Output only the translated text. Do not explain.";

        public static TranslationConfiguration Default => new TranslationConfiguration()
        {
            TranslateFromLang = Languages.English,
            TranslateToLang = Languages.Russian,
            Translator = Translators.AI,
            AiOcrRequestTimeoutSeconds = DefaultAiRequestTimeoutSeconds,
            AiTranslationRequestTimeoutSeconds = DefaultAiRequestTimeoutSeconds,
            AiOcrSystemPrompt = DefaultAiOcrSystemPromptTemplate,
            AiDefaultSystemPrompt = DefaultAiSystemPromptTemplate,
            ProxySettings = new List<Proxy>()
        };

        public Languages TranslateFromLang
        {
            get => _translateFromLang;
            set
            {
                SetProperty(ref _translateFromLang, value);
            }
        }

        public Languages TranslateToLang
        {
            get => _translateToLang;
            set
            {
                SetProperty(ref _translateToLang, value);
            }
        }

        public Translators Translator
        {
            get => _translator;
            set
            {
                SetProperty(ref _translator, value);
            }
        }

        public List<Proxy> ProxySettings
        {
            get => _proxySettings;
            set
            {
                SetProperty(ref _proxySettings, value ?? new List<Proxy>());
            }
        }

        public string AiBaseUrl
        {
            get => _aiBaseUrl;
            set
            {
                SetProperty(ref _aiBaseUrl, value);
                MigrateLegacyValue(value, ref _aiOcrBaseUrl, nameof(AiOcrBaseUrl));
                MigrateLegacyValue(value, ref _aiTranslationBaseUrl, nameof(AiTranslationBaseUrl));
            }
        }

        public string AiApiKey
        {
            get => _aiApiKey;
            set
            {
                SetProperty(ref _aiApiKey, value);
                MigrateLegacyValue(value, ref _aiOcrApiKey, nameof(AiOcrApiKey));
                MigrateLegacyValue(value, ref _aiTranslationApiKey, nameof(AiTranslationApiKey));
            }
        }

        public string AiModel
        {
            get => _aiModel;
            set
            {
                SetProperty(ref _aiModel, value);
                MigrateLegacyValue(value, ref _aiOcrModel, nameof(AiOcrModel));
                MigrateLegacyValue(value, ref _aiTranslationModel, nameof(AiTranslationModel));
            }
        }

        public string AiOcrBaseUrl
        {
            get => _aiOcrBaseUrl;
            set
            {
                SetProperty(ref _aiOcrBaseUrl, value);
            }
        }

        public string AiOcrApiKey
        {
            get => _aiOcrApiKey;
            set
            {
                SetProperty(ref _aiOcrApiKey, value);
            }
        }

        public string AiOcrModel
        {
            get => _aiOcrModel;
            set
            {
                SetProperty(ref _aiOcrModel, value);
            }
        }

        public int AiOcrRequestTimeoutSeconds
        {
            get => _aiOcrRequestTimeoutSeconds;
            set
            {
                SetProperty(ref _aiOcrRequestTimeoutSeconds, NormalizeAiRequestTimeoutSeconds(value));
            }
        }

        public string AiTranslationBaseUrl
        {
            get => _aiTranslationBaseUrl;
            set
            {
                SetProperty(ref _aiTranslationBaseUrl, value);
            }
        }

        public string AiTranslationApiKey
        {
            get => _aiTranslationApiKey;
            set
            {
                SetProperty(ref _aiTranslationApiKey, value);
            }
        }

        public string AiTranslationModel
        {
            get => _aiTranslationModel;
            set
            {
                SetProperty(ref _aiTranslationModel, value);
            }
        }

        public int AiTranslationRequestTimeoutSeconds
        {
            get => _aiTranslationRequestTimeoutSeconds;
            set
            {
                SetProperty(ref _aiTranslationRequestTimeoutSeconds, NormalizeAiRequestTimeoutSeconds(value));
            }
        }

        public string AiOcrSystemPrompt
        {
            get => _aiOcrSystemPrompt;
            set
            {
                SetProperty(ref _aiOcrSystemPrompt, value);
            }
        }

        public string AiDefaultSystemPrompt
        {
            get => _aiDefaultSystemPrompt;
            set
            {
                SetProperty(ref _aiDefaultSystemPrompt, value);
            }
        }

        public List<AiPromptOverride> AiPromptOverrides
        {
            get => _aiPromptOverrides;
            set
            {
                SetProperty(ref _aiPromptOverrides, value ?? new List<AiPromptOverride>());
            }
        }

        private Languages _translateFromLang;
        private Languages _translateToLang;
        private Translators _translator;
        private List<Proxy> _proxySettings = new List<Proxy>();
        private string _aiBaseUrl = string.Empty;
        private string _aiApiKey = string.Empty;
        private string _aiModel = string.Empty;
        private string _aiOcrBaseUrl = string.Empty;
        private string _aiOcrApiKey = string.Empty;
        private string _aiOcrModel = string.Empty;
        private int _aiOcrRequestTimeoutSeconds = DefaultAiRequestTimeoutSeconds;
        private string _aiTranslationBaseUrl = string.Empty;
        private string _aiTranslationApiKey = string.Empty;
        private string _aiTranslationModel = string.Empty;
        private int _aiTranslationRequestTimeoutSeconds = DefaultAiRequestTimeoutSeconds;
        private string _aiOcrSystemPrompt = DefaultAiOcrSystemPromptTemplate;
        private string _aiDefaultSystemPrompt = DefaultAiSystemPromptTemplate;
        private List<AiPromptOverride> _aiPromptOverrides = new List<AiPromptOverride>();

        public bool ShouldSerializeAiBaseUrl() => false;

        public bool ShouldSerializeAiApiKey() => false;

        public bool ShouldSerializeAiModel() => false;

        public string GetAiOcrSystemPrompt()
        {
            return string.IsNullOrWhiteSpace(AiOcrSystemPrompt)
                ? DefaultAiOcrSystemPromptTemplate
                : AiOcrSystemPrompt;
        }

        public string GetAiSystemPrompt(Languages targetLanguage)
        {
            var overridePrompt = GetAiPromptOverride(targetLanguage);
            if (!string.IsNullOrWhiteSpace(overridePrompt))
            {
                return overridePrompt;
            }

            return string.IsNullOrWhiteSpace(AiDefaultSystemPrompt)
                ? DefaultAiSystemPromptTemplate
                : AiDefaultSystemPrompt;
        }

        public string GetAiPromptOverride(Languages targetLanguage)
        {
            return AiPromptOverrides
                .FirstOrDefault(item => item.TargetLanguage == targetLanguage)
                ?.SystemPrompt ?? string.Empty;
        }

        public void SetAiPromptOverride(Languages targetLanguage, string systemPrompt)
        {
            var promptOverride = AiPromptOverrides.FirstOrDefault(item => item.TargetLanguage == targetLanguage);
            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                if (promptOverride != null)
                {
                    AiPromptOverrides.Remove(promptOverride);
                    OnPropertyChanged(nameof(AiPromptOverrides));
                }

                return;
            }

            if (promptOverride == null)
            {
                AiPromptOverrides.Add(new AiPromptOverride()
                {
                    TargetLanguage = targetLanguage,
                    SystemPrompt = systemPrompt
                });
            }
            else
            {
                promptOverride.SystemPrompt = systemPrompt;
            }

            OnPropertyChanged(nameof(AiPromptOverrides));
        }

        private void MigrateLegacyValue(string value, ref string targetField, string targetPropertyName)
        {
            if (string.IsNullOrWhiteSpace(value) || !string.IsNullOrWhiteSpace(targetField))
            {
                return;
            }

            targetField = value;
            OnPropertyChanged(targetPropertyName);
        }

        public static int NormalizeAiRequestTimeoutSeconds(int value)
        {
            if (value <= 0)
            {
                return DefaultAiRequestTimeoutSeconds;
            }

            return Math.Min(MaxAiRequestTimeoutSeconds, Math.Max(MinAiRequestTimeoutSeconds, value));
        }
    }

    public class AiPromptOverride
    {
        public Languages TargetLanguage { get; set; }

        public string SystemPrompt { get; set; }
    }

}
