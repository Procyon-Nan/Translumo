using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using Translumo.Infrastructure;
using Translumo.Infrastructure.Constants;
using Translumo.Processing.Configuration;
using Translumo.Processing.Interfaces;
using Translumo.Translation;
using Translumo.Translation.Configuration;
using Translumo.Translation.Exceptions;
using Translumo.TTS;
using Translumo.TTS.Engines;

namespace Translumo.Processing
{
    public class TranslationProcessingService : IProcessingService, IDisposable
    {
        private readonly IChatTextMediator _chatTextMediator;
        private readonly AiTextRecognitionService _aiTextRecognitionService;
        private readonly ScreenshotArchiveService _screenshotArchiveService;
        private readonly TranslatorFactory _translatorFactory;
        private readonly TtsFactory _ttsFactory;
        private readonly TtsConfiguration _ttsConfiguration;
        private readonly TextProcessingConfiguration _textProcessingConfiguration;
        private readonly IProcessingTextLocalizer _textLocalizer;
        private readonly ILogger _logger;
        private static readonly object _obj = new object();

        private ITTSEngine _ttsEngine;
        private ITranslator _translator;
        private TranslationConfiguration _translationConfiguration;

        private int _onceTranslationBusy;

        public TranslationProcessingService(
            IChatTextMediator chatTextMediator,
            AiTextRecognitionService aiTextRecognitionService,
            ScreenshotArchiveService screenshotArchiveService,
            TranslatorFactory translationFactory,
            TtsFactory ttsFactory,
            TtsConfiguration ttsConfiguration,
            TranslationConfiguration translationConfiguration,
            TextProcessingConfiguration textConfiguration,
            IProcessingTextLocalizer textLocalizer,
            ILogger<TranslationProcessingService> logger)
        {
            _logger = logger;
            _chatTextMediator = chatTextMediator;
            _aiTextRecognitionService = aiTextRecognitionService;
            _screenshotArchiveService = screenshotArchiveService;
            _translationConfiguration = translationConfiguration;
            _translatorFactory = translationFactory;
            _ttsFactory = ttsFactory;
            _ttsConfiguration = ttsConfiguration;
            _ttsEngine = ttsFactory.CreateTtsEngine(ttsConfiguration);
            _textProcessingConfiguration = textConfiguration;
            _textLocalizer = textLocalizer;
            _translator = _translatorFactory.CreateTranslator(_translationConfiguration);

            _translationConfiguration.PropertyChanged += TranslationConfigurationOnPropertyChanged;
            _ttsConfiguration.PropertyChanged += TtsConfigurationOnPropertyChanged;
        }

        public void ProcessOnce(FrozenScreenCapture frozenCapture, RectangleF selectedArea)
        {
            if (Interlocked.CompareExchange(ref _onceTranslationBusy, 1, 0) != 0)
            {
                _chatTextMediator.SendText(_textLocalizer.Get("Str.Chat.SingleTranslationBusy"), TextTypes.Info);
                return;
            }

            _logger.LogInformation("Single-shot translation requested: iterationId={IterationId}, screenBounds={ScreenBounds}, selectedArea={SelectedArea}, frozenImageBytes={FrozenImageBytes}, translator={Translator}, sourceLanguage={SourceLanguage}, targetLanguage={TargetLanguage}",
                frozenCapture?.IterationId,
                frozenCapture?.ScreenBounds,
                selectedArea,
                frozenCapture?.ImageBytes?.Length ?? 0,
                _translationConfiguration.Translator,
                _translationConfiguration.TranslateFromLang,
                _translationConfiguration.TranslateToLang);

            _ = Task.Run(async () =>
            {
                try
                {
                    await TranslateOnceInternal(frozenCapture, selectedArea).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Exchange(ref _onceTranslationBusy, 0);
                }
            });
        }

        private async Task TranslateOnceInternal(FrozenScreenCapture frozenCapture, RectangleF selectedArea)
        {
            try
            {
                if (frozenCapture == null || frozenCapture.ImageBytes == null || frozenCapture.ImageBytes.Length == 0)
                {
                    throw new TranslationException("Frozen screenshot is empty.");
                }

                var croppedScreenshot = CropFrozenScreenshot(frozenCapture, selectedArea);
                _screenshotArchiveService.Save(croppedScreenshot, frozenCapture.IterationId, "crop");
                await ProcessCapturedImageAsync(croppedScreenshot, frozenCapture.IterationId).ConfigureAwait(false);
            }
            catch (TranslationException ex)
            {
                _chatTextMediator.SendText(_textLocalizer.Get("Str.Chat.ProcessingFailedTemplate", ex.Message), TextTypes.Error);
            }
            catch (Exception ex)
            {
                _chatTextMediator.SendText(_textLocalizer.Get("Str.Chat.ProcessingFailedTemplate", ex.Message), TextTypes.Error);
                _logger.LogError(ex, "Single-shot processing failed due to unknown error");
            }
        }

        private byte[] CropFrozenScreenshot(FrozenScreenCapture frozenCapture, RectangleF selectedArea)
        {
            using var source = Cv2.ImDecode(frozenCapture.ImageBytes, ImreadModes.Color);
            if (source == null || source.Empty())
            {
                throw new TranslationException("Frozen screenshot could not be decoded.");
            }

            var relativeLeft = selectedArea.Left - frozenCapture.ScreenBounds.Left;
            var relativeTop = selectedArea.Top - frozenCapture.ScreenBounds.Top;
            var relativeRight = selectedArea.Right - frozenCapture.ScreenBounds.Left;
            var relativeBottom = selectedArea.Bottom - frozenCapture.ScreenBounds.Top;

            var x = Math.Max(0, (int)Math.Floor(relativeLeft));
            var y = Math.Max(0, (int)Math.Floor(relativeTop));
            var right = Math.Min(source.Width, (int)Math.Ceiling(relativeRight));
            var bottom = Math.Min(source.Height, (int)Math.Ceiling(relativeBottom));
            var width = right - x;
            var height = bottom - y;

            if (width <= 0 || height <= 0)
            {
                throw new TranslationException("Selected area is outside the frozen screenshot.");
            }

            var cropRect = new OpenCvSharp.Rect(x, y, width, height);
            using var cropped = new Mat(source, cropRect);
            if (!Cv2.ImEncode(".png", cropped, out var croppedBytes) || croppedBytes == null || croppedBytes.Length == 0)
            {
                throw new TranslationException("Selected area could not be cropped from the frozen screenshot.");
            }

            _logger.LogInformation("Frozen screenshot cropped: iterationId={IterationId}, screenBounds={ScreenBounds}, selectedArea={SelectedArea}, cropRect={CropRect}, cropBytes={CropBytes}",
                frozenCapture.IterationId,
                frozenCapture.ScreenBounds,
                selectedArea,
                cropRect,
                croppedBytes.Length);

            return croppedBytes;
        }

        private async Task ProcessCapturedImageAsync(byte[] screenshot, Guid iterationId)
        {
            _logger.LogInformation("Processing captured image started: iterationId={IterationId}, screenshotBytes={ScreenshotBytes}, screenshotFormat={ScreenshotFormat}",
                iterationId,
                screenshot?.Length ?? 0,
                "png");

            var recognizedText = await _aiTextRecognitionService.RecognizeTextAsync(screenshot).ConfigureAwait(false);
            recognizedText = NormalizeRecognizedText(recognizedText);
            if (string.IsNullOrWhiteSpace(recognizedText))
            {
                _logger.LogInformation("Processing captured image skipped: iterationId={IterationId}, reason={Reason}",
                    iterationId,
                    "OCR returned no text");
                return;
            }

            _logger.LogInformation("Recognized text ready for translation: iterationId={IterationId}, recognizedText={RecognizedText}, recognizedLength={RecognizedLength}",
                iterationId,
                recognizedText,
                recognizedText.Length);

            await TranslateTextAsync(recognizedText, iterationId).ConfigureAwait(false);
        }

        private async Task TranslateTextAsync(string text, Guid iterationId)
        {
            var translation = await _translator.TranslateTextAsync(text).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(translation))
            {
                _logger.LogInformation("Translation skipped: iterationId={IterationId}, reason={Reason}, sourceText={SourceText}",
                    iterationId,
                    "translator returned no text",
                    text);
                return;
            }

            _chatTextMediator.SendText(translation, true);
            _ttsEngine.SpeechText(translation);
            _logger.LogInformation("Translation delivered: iterationId={IterationId}, translatedText={TranslatedText}, translatedLength={TranslatedLength}, ttsEngine={TtsEngine}",
                iterationId,
                translation,
                translation.Length,
                _ttsEngine.GetType().Name);
        }

        private string NormalizeRecognizedText(string text)
        {
            var result = text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(result) || _textProcessingConfiguration.KeepFormatting)
            {
                return result;
            }

            var flattened = result
                .Replace("\r\n", " ")
                .Replace('\r', ' ')
                .Replace('\n', ' ');

            return RegexStorage.MultipleSpacesRegex.Replace(flattened, " ").Trim();
        }

        private void TranslationConfigurationOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            _translator = _translatorFactory.CreateTranslator(_translationConfiguration);
            _logger.LogInformation("Translation configuration changed: propertyName={PropertyName}, translator={Translator}, sourceLanguage={SourceLanguage}, targetLanguage={TargetLanguage}",
                e.PropertyName,
                _translationConfiguration.Translator,
                _translationConfiguration.TranslateFromLang,
                _translationConfiguration.TranslateToLang);
        }

        private void TtsConfigurationOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_ttsConfiguration.TtsLanguage)
                || e.PropertyName == nameof(_ttsConfiguration.TtsSystem))
            {
                _ttsEngine.Dispose();
                _ttsEngine = _ttsFactory.CreateTtsEngine(_ttsConfiguration);
            }
        }

        public void Dispose()
        {
            _ttsEngine.Dispose();
        }
    }
}
