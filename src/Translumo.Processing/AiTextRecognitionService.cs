using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using Translumo.Infrastructure.Language;
using Translumo.Translation.AI;
using Translumo.Translation.Configuration;
using Translumo.Translation.Exceptions;

namespace Translumo.Processing
{
    public sealed class AiTextRecognitionService
    {
        private readonly TranslationConfiguration _configuration;
        private readonly LanguageService _languageService;
        private readonly OpenAiCompatibleClient _client;
        private readonly ILogger _logger;

        public AiTextRecognitionService(TranslationConfiguration configuration, LanguageService languageService, ILogger<AiTextRecognitionService> logger)
        {
            _configuration = configuration;
            _languageService = languageService;
            _logger = logger;
            _client = new OpenAiCompatibleClient(logger);
        }

        public async Task<string> RecognizeTextAsync(byte[] screenshot)
        {
            try
            {
                EnsureConfigured();
                var sourceDescriptor = _languageService.GetLanguageDescriptor(_configuration.TranslateFromLang);
                var sourceLanguage = GetLanguageName(sourceDescriptor);
                var systemPrompt = BuildSystemPrompt(sourceLanguage);
                var userPrompt = BuildUserPrompt(sourceLanguage);
                _logger.LogInformation("AI OCR image preparation started: sourceLanguage={SourceLanguage}, ocrModel={OcrModel}, baseUrl={BaseUrl}, originalScreenshotBytes={OriginalScreenshotBytes}, originalImageFormat={OriginalImageFormat}",
                    sourceLanguage,
                    _configuration.AiOcrModel,
                    DescribeEndpoint(_configuration.AiOcrBaseUrl),
                    screenshot?.Length ?? 0,
                    DetectImageFormat(screenshot));

                var image = PreparePng(screenshot);
                _logger.LogInformation("AI OCR image prepared: sourceLanguage={SourceLanguage}, width={ImageWidth}, height={ImageHeight}, originalScreenshotBytes={OriginalScreenshotBytes}, pngBytes={PngBytes}, imageSha256Prefix={ImageSha256Prefix}, pngConverted={PngConverted}",
                    sourceLanguage,
                    image.Width,
                    image.Height,
                    image.OriginalLength,
                    image.PngBytes.Length,
                    image.Sha256Prefix,
                    image.PngConverted);

                _logger.LogInformation("AI OCR request prepared: sourceLanguage={SourceLanguage}, ocrModel={OcrModel}, baseUrl={BaseUrl}, requestTimeoutSeconds={RequestTimeoutSeconds}, systemPrompt={SystemPrompt}, userPrompt={UserPrompt}",
                    sourceLanguage,
                    _configuration.AiOcrModel,
                    DescribeEndpoint(_configuration.AiOcrBaseUrl),
                    _configuration.AiOcrRequestTimeoutSeconds,
                    systemPrompt,
                    userPrompt);

                var recognizedText = await _client.RecognizeImageTextAsync(
                    _configuration.AiOcrBaseUrl,
                    _configuration.AiOcrApiKey,
                    _configuration.AiOcrModel,
                    systemPrompt,
                    userPrompt,
                    image.PngBytes,
                    "image/png",
                    _configuration.AiOcrRequestTimeoutSeconds).ConfigureAwait(false);

                var normalizedText = NormalizeNoTextMarker(recognizedText);
                _logger.LogInformation("AI OCR result received: recognizedText={RecognizedText}, normalizedText={NormalizedText}, recognizedLength={RecognizedLength}, normalizedLength={NormalizedLength}",
                    recognizedText,
                    normalizedText,
                    recognizedText?.Length ?? 0,
                    normalizedText?.Length ?? 0);

                return normalizedText;
            }
            catch (TranslationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI text recognition failed");
                throw new TranslationException($"AI text recognition failed: {ex.Message}", ex);
            }
        }

        private void EnsureConfigured()
        {
            if (string.IsNullOrWhiteSpace(_configuration.AiOcrBaseUrl))
            {
                throw new TranslationException("AI OCR base URL is not configured.");
            }

            if (string.IsNullOrWhiteSpace(_configuration.AiOcrApiKey))
            {
                throw new TranslationException("AI OCR API key is not configured.");
            }

            if (string.IsNullOrWhiteSpace(_configuration.AiOcrModel))
            {
                throw new TranslationException("AI OCR model is not selected.");
            }
        }

        private string BuildSystemPrompt(string sourceLanguage)
        {
            var prompt = _configuration.GetAiOcrSystemPrompt()
                .Replace("{sourceLanguage}", sourceLanguage);

            if (prompt.IndexOf(TranslationConfiguration.AiNoTextMarker, StringComparison.OrdinalIgnoreCase) < 0)
            {
                prompt += Environment.NewLine + $"If no readable text exists, output exactly {TranslationConfiguration.AiNoTextMarker}.";
            }

            return prompt;
        }

        private static string BuildUserPrompt(string sourceLanguage)
        {
            return $"Read the text from the attached image. Source language hint: {sourceLanguage}. Return only the extracted text.";
        }

        private static string NormalizeNoTextMarker(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var normalized = text.Trim().Replace(" ", string.Empty);
            if (normalized.Equals(TranslationConfiguration.AiNoTextMarker, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return text.Trim();
        }

        private static ImageConversionResult PreparePng(byte[] screenshot)
        {
            if (screenshot == null || screenshot.Length == 0)
            {
                throw new TranslationException("AI text recognition failed: captured image is empty.");
            }

            using var decoded = Cv2.ImDecode(screenshot, ImreadModes.Color);
            if (decoded == null || decoded.Empty())
            {
                throw new TranslationException("AI text recognition failed: captured image could not be decoded.");
            }

            if (IsPng(screenshot))
            {
                return new ImageConversionResult()
                {
                    PngBytes = screenshot,
                    OriginalLength = screenshot.Length,
                    Width = decoded.Width,
                    Height = decoded.Height,
                    Sha256Prefix = GetSha256Prefix(screenshot),
                    PngConverted = false
                };
            }

            if (!Cv2.ImEncode(".png", decoded, out var pngScreenshot) || pngScreenshot == null || pngScreenshot.Length == 0)
            {
                throw new TranslationException("AI text recognition failed: captured image could not be converted to PNG.");
            }

            return new ImageConversionResult()
            {
                PngBytes = pngScreenshot,
                OriginalLength = screenshot?.Length ?? 0,
                Width = decoded.Width,
                Height = decoded.Height,
                Sha256Prefix = GetSha256Prefix(pngScreenshot),
                PngConverted = true
            };
        }

        private static string GetLanguageName(LanguageDescriptor descriptor)
        {
            return $"{descriptor.Language} ({descriptor.Code})";
        }

        private static string GetSha256Prefix(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            return Convert.ToHexString(SHA256.HashData(bytes)).Substring(0, 16);
        }

        private static string DetectImageFormat(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 4)
            {
                return "unknown";
            }

            if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            {
                return "png";
            }

            if (bytes[0] == 0xFF && bytes[1] == 0xD8)
            {
                return "jpeg";
            }

            if (bytes[0] == 0x42 && bytes[1] == 0x4D)
            {
                return "bmp";
            }

            if ((bytes[0] == 0x49 && bytes[1] == 0x49 && bytes[2] == 0x2A && bytes[3] == 0x00)
                || (bytes[0] == 0x4D && bytes[1] == 0x4D && bytes[2] == 0x00 && bytes[3] == 0x2A))
            {
                return "tiff";
            }

            return "unknown";
        }

        private static bool IsPng(byte[] bytes)
        {
            return bytes != null
                && bytes.Length >= 4
                && bytes[0] == 0x89
                && bytes[1] == 0x50
                && bytes[2] == 0x4E
                && bytes[3] == 0x47;
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

        private sealed class ImageConversionResult
        {
            public byte[] PngBytes { get; set; }

            public int OriginalLength { get; set; }

            public int Width { get; set; }

            public int Height { get; set; }

            public string Sha256Prefix { get; set; }

            public bool PngConverted { get; set; }
        }
    }
}
