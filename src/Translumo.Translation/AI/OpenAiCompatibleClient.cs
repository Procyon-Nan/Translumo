using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Translumo.Translation.Configuration;
using Translumo.Translation.Exceptions;

namespace Translumo.Translation.AI
{
    public class OpenAiCompatibleClient
    {
        private static readonly HttpClient HttpClient = new HttpClient()
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan
        };

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly ILogger _logger;

        public OpenAiCompatibleClient(ILogger logger = null)
        {
            _logger = logger;
        }

        public async Task<IReadOnlyList<string>> GetModelsAsync(string baseUrl, string apiKey, int timeoutSeconds = TranslationConfiguration.DefaultAiRequestTimeoutSeconds)
        {
            timeoutSeconds = NormalizeTimeoutSeconds(timeoutSeconds);
            try
            {
                EnsureConfigured(baseUrl, apiKey, null);

                var endpoint = BuildEndpoint(baseUrl, "models");
                var stopwatch = Stopwatch.StartNew();
                _logger?.LogInformation("Loading AI models: endpoint={Endpoint}, requestTimeoutSeconds={RequestTimeoutSeconds}",
                    DescribeEndpoint(endpoint),
                    timeoutSeconds);

                using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                using var timeout = CreateTimeout(timeoutSeconds);
                using var response = await HttpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                stopwatch.Stop();
                _logger?.LogInformation("AI model list response: endpoint={Endpoint}, statusCode={StatusCode}, elapsedMs={ElapsedMs}, requestTimeoutSeconds={RequestTimeoutSeconds}",
                    DescribeEndpoint(endpoint), (int)response.StatusCode, stopwatch.ElapsedMilliseconds, timeoutSeconds);
                if (!response.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("AI model list error body: endpoint={Endpoint}, responseBody={ResponseBody}",
                        DescribeEndpoint(endpoint), TrimForLog(body));
                    throw new TranslationException(BuildErrorMessage("Failed to load model list", response, body));
                }

                var modelsResponse = JsonSerializer.Deserialize<OpenAiModelsResponse>(body, JsonOptions);
                var models = modelsResponse?.Data?
                    .Select(item => item.Id)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (models == null || models.Length == 0)
                {
                    throw new TranslationException("AI provider returned an empty model list.");
                }

                _logger?.LogInformation("AI model list loaded: endpoint={Endpoint}, modelCount={ModelCount}",
                    DescribeEndpoint(endpoint), models.Length);

                return models;
            }
            catch (TranslationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw BuildRequestException("Failed to load AI models", ex, timeoutSeconds);
            }
        }

        public async Task<string> TranslateAsync(
            string baseUrl,
            string apiKey,
            string model,
            string systemPrompt,
            string userPrompt,
            int timeoutSeconds = TranslationConfiguration.DefaultAiRequestTimeoutSeconds)
        {
            timeoutSeconds = NormalizeTimeoutSeconds(timeoutSeconds);
            try
            {
                EnsureConfigured(baseUrl, apiKey, model);

                var payload = new OpenAiChatCompletionRequest()
                {
                    Model = model,
                    Stream = false,
                    Messages = new[]
                    {
                        new OpenAiChatMessage()
                        {
                            Role = "system",
                            Content = systemPrompt
                        },
                        new OpenAiChatMessage()
                        {
                            Role = "user",
                            Content = userPrompt
                        }
                    }
                };

                return await SendChatCompletionAsync(
                    baseUrl,
                    apiKey,
                    payload,
                    "AI translation",
                    "AI translation request failed",
                    "AI translator returned an empty response.",
                    null,
                    timeoutSeconds).ConfigureAwait(false);
            }
            catch (TranslationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw BuildRequestException("AI translation request failed", ex, timeoutSeconds);
            }
        }

        public async Task<string> RecognizeImageTextAsync(
            string baseUrl,
            string apiKey,
            string model,
            string systemPrompt,
            string userPrompt,
            byte[] imageBytes,
            string imageMimeType,
            int timeoutSeconds = TranslationConfiguration.DefaultAiRequestTimeoutSeconds)
        {
            timeoutSeconds = NormalizeTimeoutSeconds(timeoutSeconds);
            try
            {
                EnsureConfigured(baseUrl, apiKey, model);
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    throw new TranslationException("AI text recognition image is empty.");
                }

                var imageDataUrl = $"data:{imageMimeType};base64,{Convert.ToBase64String(imageBytes)}";
                _logger?.LogInformation("AI text recognition payload summary: imageMimeType={ImageMimeType}, imageBytes={ImageBytes}, imageDataUrlLength={ImageDataUrlLength}, requestTimeoutSeconds={RequestTimeoutSeconds}, userContentParts={UserContentParts}",
                    imageMimeType,
                    imageBytes.Length,
                    imageDataUrl.Length,
                    timeoutSeconds,
                    2);
                var payload = new OpenAiChatCompletionRequest()
                {
                    Model = model,
                    Stream = false,
                    Messages = new[]
                    {
                        new OpenAiChatMessage()
                        {
                            Role = "system",
                            Content = systemPrompt
                        },
                        new OpenAiChatMessage()
                        {
                            Role = "user",
                            Content = new object[]
                            {
                                new OpenAiTextContentPart()
                                {
                                    Text = userPrompt
                                },
                                new OpenAiImageContentPart()
                                {
                                    ImageUrl = new OpenAiImageUrl()
                                    {
                                        Url = imageDataUrl,
                                        Detail = "high"
                                    }
                                }
                            }
                        }
                    }
                };

                return await SendChatCompletionAsync(
                    baseUrl,
                    apiKey,
                    payload,
                    "AI text recognition",
                    "AI text recognition request failed",
                    "AI text recognition returned an empty response.",
                    "Request reached the AI OCR service. Check whether the selected OCR model supports image input and whether the provider accepts data URL images in chat/completions.",
                    timeoutSeconds).ConfigureAwait(false);
            }
            catch (TranslationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw BuildRequestException("AI text recognition request failed", ex, timeoutSeconds);
            }
        }

        private async Task<string> SendChatCompletionAsync(
            string baseUrl,
            string apiKey,
            OpenAiChatCompletionRequest payload,
            string operationName,
            string failurePrefix,
            string emptyContentMessage,
            string diagnosticHint = null,
            int timeoutSeconds = TranslationConfiguration.DefaultAiRequestTimeoutSeconds)
        {
            var requestJson = JsonSerializer.Serialize(payload, JsonOptions);
            var endpoint = BuildEndpoint(baseUrl, "chat/completions");
            _logger?.LogInformation("{OperationName} request: endpoint={Endpoint}, model={Model}, stream={Stream}, requestTimeoutSeconds={RequestTimeoutSeconds}",
                operationName, DescribeEndpoint(endpoint), payload.Model, payload.Stream, timeoutSeconds);

            var stopwatch = Stopwatch.StartNew();
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            using var timeout = CreateTimeout(timeoutSeconds);
            using var response = await HttpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            stopwatch.Stop();
            _logger?.LogInformation("{OperationName} response: endpoint={Endpoint}, model={Model}, statusCode={StatusCode}, elapsedMs={ElapsedMs}, requestTimeoutSeconds={RequestTimeoutSeconds}",
                operationName, DescribeEndpoint(endpoint), payload.Model, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, timeoutSeconds);
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("{OperationName} error body: endpoint={Endpoint}, model={Model}, responseBody={ResponseBody}",
                    operationName, DescribeEndpoint(endpoint), payload.Model, TrimForLog(body));
                throw new TranslationException(AppendDiagnosticHint(BuildErrorMessage(failurePrefix, response, body), diagnosticHint));
            }

            var completionResponse = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(body, JsonOptions);
            var content = ExtractMessageContent(completionResponse?.Choices?
                .FirstOrDefault()?
                .Message?
                .Content);

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new TranslationException(emptyContentMessage);
            }

            _logger?.LogInformation("{OperationName} content received: endpoint={Endpoint}, model={Model}, contentLength={ContentLength}",
                operationName, DescribeEndpoint(endpoint), payload.Model, content.Length);

            return CleanResponse(content);
        }

        private static void EnsureConfigured(string baseUrl, string apiKey, string model)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new TranslationException("AI base URL is not configured.");
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new TranslationException("AI API key is not configured.");
            }

            if (model != null && string.IsNullOrWhiteSpace(model))
            {
                throw new TranslationException("AI model is not selected.");
            }
        }

        private static string BuildEndpoint(string baseUrl, string relativePath)
        {
            return $"{baseUrl.Trim().TrimEnd('/')}/{relativePath.TrimStart('/')}";
        }

        private static string DescribeEndpoint(string endpoint)
        {
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            {
                return uri.GetLeftPart(UriPartial.Path);
            }

            var queryIndex = endpoint.IndexOfAny(new[] { '?', '#' });
            return queryIndex >= 0 ? endpoint.Substring(0, queryIndex) : endpoint;
        }

        private static CancellationTokenSource CreateTimeout(int timeoutSeconds)
        {
            return new CancellationTokenSource(TimeSpan.FromSeconds(NormalizeTimeoutSeconds(timeoutSeconds)));
        }

        private static int NormalizeTimeoutSeconds(int timeoutSeconds)
        {
            return TranslationConfiguration.NormalizeAiRequestTimeoutSeconds(timeoutSeconds);
        }

        private static TranslationException BuildRequestException(string prefix, Exception ex, int timeoutSeconds)
        {
            if (ex is TaskCanceledException)
            {
                return new TranslationException($"{prefix}: request timed out after {NormalizeTimeoutSeconds(timeoutSeconds)} second(s).", ex);
            }

            if (ex is UriFormatException)
            {
                return new TranslationException($"{prefix}: AI base URL is invalid.", ex);
            }

            if (ex is JsonException)
            {
                return new TranslationException($"{prefix}: provider returned invalid JSON.", ex);
            }

            return new TranslationException($"{prefix}: {ex.Message}", ex);
        }

        private static string BuildErrorMessage(string prefix, HttpResponseMessage response, string body)
        {
            var status = (int)response.StatusCode;
            var reason = response.ReasonPhrase ?? "Unknown";
            var trimmedBody = string.IsNullOrWhiteSpace(body)
                ? string.Empty
                : body.Length > 500 ? body[..500] + "..." : body;

            return string.IsNullOrWhiteSpace(trimmedBody)
                ? $"{prefix} ({status} {reason})."
                : $"{prefix} ({status} {reason}): {trimmedBody}";
        }

        private static string AppendDiagnosticHint(string message, string diagnosticHint)
        {
            return string.IsNullOrWhiteSpace(diagnosticHint)
                ? message
                : $"{message} {diagnosticHint}";
        }

        private static string TrimForLog(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Length <= 2000 ? value : value.Substring(0, 2000) + "...";
        }

        private static string ExtractMessageContent(object content)
        {
            if (content == null)
            {
                return null;
            }

            if (content is string text)
            {
                return text;
            }

            if (content is JsonElement element)
            {
                return ExtractJsonContent(element);
            }

            return content.ToString();
        }

        private static string ExtractJsonContent(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Array:
                    var builder = new StringBuilder();
                    foreach (var item in element.EnumerateArray())
                    {
                        var itemText = ExtractJsonContent(item);
                        if (string.IsNullOrWhiteSpace(itemText))
                        {
                            continue;
                        }

                        if (builder.Length > 0)
                        {
                            builder.AppendLine();
                        }

                        builder.Append(itemText);
                    }

                    return builder.ToString();
                case JsonValueKind.Object:
                    if (element.TryGetProperty("text", out var textElement))
                    {
                        return ExtractJsonContent(textElement);
                    }

                    return null;
                default:
                    return null;
            }
        }

        private static string CleanResponse(string content)
        {
            var result = content.Trim();

            if (result.StartsWith("```", StringComparison.Ordinal) && result.EndsWith("```", StringComparison.Ordinal) && result.Length > 6)
            {
                result = result[3..^3].Trim();
                var newlineIndex = result.IndexOf('\n');
                if (newlineIndex >= 0)
                {
                    var firstLine = result[..newlineIndex].Trim();
                    if (firstLine.Length <= 12 && firstLine.IndexOf(' ') < 0 && firstLine.IndexOf('\r') < 0)
                    {
                        result = result[(newlineIndex + 1)..].Trim();
                    }
                }
            }

            return result;
        }

        private sealed class OpenAiModelsResponse
        {
            [JsonPropertyName("data")]
            public List<OpenAiModelItem> Data { get; set; }
        }

        private sealed class OpenAiModelItem
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }
        }

        private sealed class OpenAiChatCompletionRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; }

            [JsonPropertyName("messages")]
            public OpenAiChatMessage[] Messages { get; set; }

            [JsonPropertyName("stream")]
            public bool Stream { get; set; }
        }

        private sealed class OpenAiChatMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; }

            [JsonPropertyName("content")]
            public object Content { get; set; }
        }

        private sealed class OpenAiTextContentPart
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = "text";

            [JsonPropertyName("text")]
            public string Text { get; set; }
        }

        private sealed class OpenAiImageContentPart
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = "image_url";

            [JsonPropertyName("image_url")]
            public OpenAiImageUrl ImageUrl { get; set; }
        }

        private sealed class OpenAiImageUrl
        {
            [JsonPropertyName("url")]
            public string Url { get; set; }

            [JsonPropertyName("detail")]
            public string Detail { get; set; }
        }

        private sealed class OpenAiChatCompletionResponse
        {
            [JsonPropertyName("choices")]
            public List<OpenAiChoice> Choices { get; set; }
        }

        private sealed class OpenAiChoice
        {
            [JsonPropertyName("message")]
            public OpenAiChatMessage Message { get; set; }
        }
    }
}
