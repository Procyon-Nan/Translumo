using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using Translumo.Translation.Exceptions;

namespace Translumo.Processing
{
    public sealed class ScreenshotArchiveService
    {
        private const int MaxIterations = 5;
        private static readonly Regex IterationFileNameRegex = new Regex(@"^[0-9]{8}-[0-9]{9}-([0-9a-fA-F]{32})(?:-[^.]+)?\.", RegexOptions.Compiled);

        private readonly ILogger _logger;
        private readonly string _directoryPath;

        public ScreenshotArchiveService(ILogger<ScreenshotArchiveService> logger)
        {
            _logger = logger;
            _directoryPath = Path.Combine(Path.GetTempPath(), "Translumo", "Screenshots");
        }

        public string Save(byte[] imageBytes, Guid iterationId, string role = null)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                throw new TranslationException("Captured screenshot is empty.");
            }

            var imageInfo = ReadImageInfo(imageBytes);
            string filePath;
            try
            {
                Directory.CreateDirectory(_directoryPath);
                var safeRole = GetSafeRole(role);
                var fileName = $"{DateTime.Now:yyyyMMdd-HHmmssfff}-{iterationId:N}{safeRole}{imageInfo.Extension}";
                filePath = Path.Combine(_directoryPath, fileName);
                File.WriteAllBytes(filePath, imageBytes);
                CleanupOldScreenshots();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to archive screenshot: iterationId={IterationId}, role={Role}, directory={Directory}, width={ImageWidth}, height={ImageHeight}, imageBytes={ImageBytes}, imageFormat={ImageFormat}, imageSha256Prefix={ImageSha256Prefix}",
                    iterationId,
                    role,
                    _directoryPath,
                    imageInfo.Width,
                    imageInfo.Height,
                    imageBytes.Length,
                    imageInfo.Format,
                    imageInfo.Sha256Prefix);
                return null;
            }

            _logger.LogInformation("Screenshot archived: iterationId={IterationId}, role={Role}, path={Path}, width={ImageWidth}, height={ImageHeight}, imageBytes={ImageBytes}, imageFormat={ImageFormat}, imageSha256Prefix={ImageSha256Prefix}",
                iterationId,
                role,
                filePath,
                imageInfo.Width,
                imageInfo.Height,
                imageBytes.Length,
                imageInfo.Format,
                imageInfo.Sha256Prefix);

            return filePath;
        }

        private void CleanupOldScreenshots()
        {
            var screenshots = new DirectoryInfo(_directoryPath)
                .EnumerateFiles()
                .Where(file => IsArchivedScreenshot(file.Extension))
                .GroupBy(file => GetIterationKey(file))
                .OrderByDescending(group => group.Max(file => file.LastWriteTimeUtc))
                .Skip(MaxIterations)
                .SelectMany(group => group)
                .ToArray();

            foreach (var screenshot in screenshots)
            {
                try
                {
                    screenshot.Delete();
                    _logger.LogInformation("Old archived screenshot removed: path={Path}", screenshot.FullName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to remove old archived screenshot: path={Path}", screenshot.FullName);
                }
            }
        }

        private static string GetSafeRole(string role)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                return string.Empty;
            }

            var safeRole = Regex.Replace(role.Trim(), @"[^a-zA-Z0-9_-]+", "-").Trim('-');
            return string.IsNullOrWhiteSpace(safeRole) ? string.Empty : $"-{safeRole}";
        }

        private static string GetIterationKey(FileInfo file)
        {
            var match = IterationFileNameRegex.Match(file.Name);
            return match.Success ? match.Groups[1].Value.ToLowerInvariant() : file.Name;
        }

        private static ImageInfo ReadImageInfo(byte[] imageBytes)
        {
            using var decoded = Cv2.ImDecode(imageBytes, ImreadModes.Color);
            if (decoded == null || decoded.Empty())
            {
                throw new TranslationException("Captured screenshot could not be decoded as an image.");
            }

            var format = DetectImageFormat(imageBytes);
            return new ImageInfo()
            {
                Width = decoded.Width,
                Height = decoded.Height,
                Format = format,
                Extension = GetExtension(format),
                Sha256Prefix = Convert.ToHexString(SHA256.HashData(imageBytes)).Substring(0, 16)
            };
        }

        private static bool IsArchivedScreenshot(string extension)
        {
            return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".tif", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
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

        private static string GetExtension(string format)
        {
            return format switch
            {
                "png" => ".png",
                "jpeg" => ".jpg",
                "bmp" => ".bmp",
                "tiff" => ".tiff",
                _ => ".img"
            };
        }

        private sealed class ImageInfo
        {
            public int Width { get; set; }

            public int Height { get; set; }

            public string Format { get; set; }

            public string Extension { get; set; }

            public string Sha256Prefix { get; set; }
        }
    }
}
