using Microsoft.Extensions.Logging;
using Translumo.Infrastructure.Language;
using Translumo.TTS.Engines;

namespace Translumo.TTS
{
    public class TtsFactory
    {
        private readonly LanguageService _languageService;
        private readonly ILogger _logger;

        public TtsFactory(LanguageService languageService, ILogger<TtsFactory> logger)
        {
            _languageService = languageService;
            _logger = logger;
        }

        public ITTSEngine CreateTtsEngine(TtsConfiguration ttsConfiguration) =>
            ttsConfiguration.TtsSystem switch
            {
                TTSEngines.None => new NoneTTSEngine(),
                TTSEngines.WindowsTTS => new WindowsTTSEngine(
                    GetLangCode(ttsConfiguration), 
                    ttsConfiguration.SelectedVoiceName),
                _ => throw new NotSupportedException()
            };

        private string GetLangCode(TtsConfiguration ttsConfiguration) =>
            _languageService.GetLanguageDescriptor(ttsConfiguration.TtsLanguage).Code;
    }
}
