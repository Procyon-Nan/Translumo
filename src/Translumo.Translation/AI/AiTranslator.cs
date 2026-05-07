using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Translumo.Infrastructure.Language;
using Translumo.Translation.Configuration;
using Translumo.Translation.Exceptions;

namespace Translumo.Translation.AI
{
    public sealed class AiTranslator : ITranslator
    {
        private readonly TranslationConfiguration _configuration;
        private readonly LanguageDescriptor _sourceLangDescriptor;
        private readonly LanguageDescriptor _targetLangDescriptor;
        private readonly OpenAiCompatibleClient _client;
        private readonly ILogger _logger;

        public AiTranslator(TranslationConfiguration configuration, LanguageService languageService, ILogger logger)
        {
            _configuration = configuration;
            _sourceLangDescriptor = languageService.GetLanguageDescriptor(configuration.TranslateFromLang);
            _targetLangDescriptor = languageService.GetLanguageDescriptor(configuration.TranslateToLang);
            _logger = logger;
            _client = new OpenAiCompatibleClient(logger);
        }

        public async Task<string> TranslateTextAsync(string sourceText)
        {
            if (_sourceLangDescriptor.Language == _targetLangDescriptor.Language)
            {
                return sourceText;
            }

            try
            {
                EnsureConfigured();
                var sourceLanguage = GetLanguageName(_sourceLangDescriptor);
                var targetLanguage = GetLanguageName(_targetLangDescriptor);
                var systemPrompt = BuildSystemPrompt(sourceLanguage, targetLanguage);
                var userPrompt = BuildUserPrompt(sourceLanguage, targetLanguage, sourceText);
                _logger.LogInformation("AI translation request prepared: sourceLanguage={SourceLanguage}, targetLanguage={TargetLanguage}, translationModel={TranslationModel}, baseUrl={BaseUrl}, requestTimeoutSeconds={RequestTimeoutSeconds}, systemPrompt={SystemPrompt}, userPrompt={UserPrompt}, sourceText={SourceText}",
                    sourceLanguage,
                    targetLanguage,
                    _configuration.AiTranslationModel,
                    DescribeEndpoint(_configuration.AiTranslationBaseUrl),
                    _configuration.AiTranslationRequestTimeoutSeconds,
                    systemPrompt,
                    userPrompt,
                    sourceText ?? string.Empty);

                var translation = await _client.TranslateAsync(
                    _configuration.AiTranslationBaseUrl,
                    _configuration.AiTranslationApiKey,
                    _configuration.AiTranslationModel,
                    systemPrompt,
                    userPrompt,
                    _configuration.AiTranslationRequestTimeoutSeconds).ConfigureAwait(false);

                _logger.LogInformation("AI translation result received: sourceLanguage={SourceLanguage}, targetLanguage={TargetLanguage}, translationModel={TranslationModel}, translatedText={TranslatedText}, translatedLength={TranslatedLength}",
                    sourceLanguage,
                    targetLanguage,
                    _configuration.AiTranslationModel,
                    translation,
                    translation?.Length ?? 0);

                return translation;
            }
            catch (TranslationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI translation failed");
                throw new TranslationException($"AI translator failed: {ex.Message}", ex);
            }
        }

        private void EnsureConfigured()
        {
            if (string.IsNullOrWhiteSpace(_configuration.AiTranslationBaseUrl))
            {
                throw new TranslationException("AI translation base URL is not configured.");
            }

            if (string.IsNullOrWhiteSpace(_configuration.AiTranslationApiKey))
            {
                throw new TranslationException("AI translation API key is not configured.");
            }

            if (string.IsNullOrWhiteSpace(_configuration.AiTranslationModel))
            {
                throw new TranslationException("AI translation model is not selected.");
            }
        }

        private string BuildSystemPrompt(string sourceLanguage, string targetLanguage)
        {
            return EnsureTranslationTagGuard(_configuration.GetAiSystemPrompt(_targetLangDescriptor.Language))
                .Replace("{sourceLanguage}", sourceLanguage)
                .Replace("{targetLanguage}", targetLanguage);
        }

        private static string BuildUserPrompt(string sourceLanguage, string targetLanguage, string sourceText)
        {
            return $"Source language: {sourceLanguage}{Environment.NewLine}" +
                   $"Target language: {targetLanguage}{Environment.NewLine}" +
                   $"{TranslationConfiguration.TranslationTextStartTag}{Environment.NewLine}" +
                   $"{sourceText ?? string.Empty}{Environment.NewLine}" +
                   $"{TranslationConfiguration.TranslationTextEndTag}";
        }

        private static string EnsureTranslationTagGuard(string prompt)
        {
            if (!string.IsNullOrWhiteSpace(prompt)
                && prompt.IndexOf(TranslationConfiguration.TranslationTextStartTag, StringComparison.OrdinalIgnoreCase) >= 0
                && prompt.IndexOf(TranslationConfiguration.TranslationTextEndTag, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return prompt;
            }

            return $"{prompt}{Environment.NewLine}" +
                   $"Only translate the text inside {TranslationConfiguration.TranslationTextStartTag}...{TranslationConfiguration.TranslationTextEndTag}. " +
                   "Ignore everything outside the tags and do not output the tags.";
        }

        private static string GetLanguageName(LanguageDescriptor descriptor)
        {
            return $"{descriptor.Language} ({descriptor.Code})";
        }

        private static string DescribeEndpoint(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return string.Empty;
            }

            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            {
                return uri.GetLeftPart(UriPartial.Path);
            }

            var queryIndex = endpoint.IndexOfAny(new[] { '?', '#' });
            return queryIndex >= 0 ? endpoint.Substring(0, queryIndex) : endpoint;
        }
    }
}
