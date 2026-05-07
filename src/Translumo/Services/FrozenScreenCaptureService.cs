using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Translumo.Configuration;
using Translumo.Processing;
using Translumo.Processing.Exceptions;
using Translumo.Processing.Interfaces;
using Translumo.Utils;
using Translumo.Utils.IntertopStruct;

namespace Translumo.Services
{
    public sealed class FrozenScreenCaptureService
    {
        private const uint MonitorDefaultToNearest = 2;
        private const int CaptureAttemptsPerBackend = 3;
        private const int BlackFrameRetryDelayMs = 120;

        private readonly ICapturerFactory _capturerFactory;
        private readonly ScreenCaptureConfiguration _captureConfiguration;
        private readonly ScreenshotArchiveService _screenshotArchiveService;
        private readonly ILogger _logger;
        private readonly object _syncRoot = new object();

        public FrozenScreenCaptureService(
            ICapturerFactory capturerFactory,
            ScreenCaptureConfiguration captureConfiguration,
            ScreenshotArchiveService screenshotArchiveService,
            ILogger<FrozenScreenCaptureService> logger)
        {
            _capturerFactory = capturerFactory;
            _captureConfiguration = captureConfiguration;
            _screenshotArchiveService = screenshotArchiveService;
            _logger = logger;
        }

        public FrozenScreenCapture CaptureMouseScreen()
        {
            lock (_syncRoot)
            {
                var iterationId = Guid.NewGuid();
                var screenBounds = GetMouseScreenBounds();
                var previousCaptureArea = _captureConfiguration.CaptureArea;

                _logger.LogInformation("Frozen screen capture started: iterationId={IterationId}, screenBounds={ScreenBounds}, previousCaptureArea={PreviousCaptureArea}",
                    iterationId,
                    screenBounds,
                    previousCaptureArea);

                try
                {
                    _captureConfiguration.CaptureArea = screenBounds;
                    var screenshot = CaptureNonBlackScreenshot(iterationId, screenBounds);
                    _screenshotArchiveService.Save(screenshot, iterationId, "full");

                    return new FrozenScreenCapture()
                    {
                        IterationId = iterationId,
                        ScreenBounds = screenBounds,
                        ImageBytes = screenshot
                    };
                }
                finally
                {
                    _captureConfiguration.CaptureArea = previousCaptureArea;
                }
            }
        }

        private byte[] CaptureNonBlackScreenshot(Guid iterationId, RectangleF screenBounds)
        {
            Exception lastException = null;
            var reliabilityModes = new[]
            {
                true,
                false
            };

            foreach (var reliabilityPrioritize in reliabilityModes)
            {
                using var capturer = _capturerFactory.CreateCapturer(reliabilityPrioritize);
                if (capturer == null)
                {
                    continue;
                }

                for (var attempt = 1; attempt <= CaptureAttemptsPerBackend; attempt++)
                {
                    try
                    {
                        var rawScreenshot = capturer.CaptureScreen();
                        var screenshot = ConvertToOpaquePng(rawScreenshot);
                        var blackFrame = IsProbablyBlack(screenshot);

                        _logger.LogInformation("Frozen screen capture attempt completed: iterationId={IterationId}, capturer={Capturer}, reliabilityPrioritize={ReliabilityPrioritize}, attempt={Attempt}, screenBounds={ScreenBounds}, rawScreenshotBytes={RawScreenshotBytes}, screenshotBytes={ScreenshotBytes}, blackFrame={BlackFrame}",
                            iterationId,
                            capturer.GetType().Name,
                            reliabilityPrioritize,
                            attempt,
                            screenBounds,
                            rawScreenshot?.Length ?? 0,
                            screenshot?.Length ?? 0,
                            blackFrame);

                        if (!blackFrame)
                        {
                            return screenshot;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        _logger.LogWarning(ex, "Frozen screen capture attempt failed: iterationId={IterationId}, capturer={Capturer}, reliabilityPrioritize={ReliabilityPrioritize}, attempt={Attempt}, screenBounds={ScreenBounds}",
                            iterationId,
                            capturer.GetType().Name,
                            reliabilityPrioritize,
                            attempt,
                            screenBounds);
                    }

                    Thread.Sleep(BlackFrameRetryDelayMs);
                }
            }

            throw new CaptureException("Frozen screenshot is black after trying available capture backends.", lastException);
        }

        private static byte[] ConvertToOpaquePng(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                throw new CaptureException("Frozen screenshot is empty.");
            }

            using var input = new MemoryStream(imageBytes);
            using var sourceImage = Image.FromStream(input);
            using var sourceBitmap = new Bitmap(sourceImage);
            using var opaqueBitmap = sourceBitmap.Clone(
                new Rectangle(0, 0, sourceBitmap.Width, sourceBitmap.Height),
                PixelFormat.Format24bppRgb);

            return opaqueBitmap.ToBytes(ImageFormat.Png);
        }

        private static bool IsProbablyBlack(byte[] imageBytes)
        {
            using var input = new MemoryStream(imageBytes);
            using var image = Image.FromStream(input);
            using var bitmap = new Bitmap(image);

            var xStep = Math.Max(1, bitmap.Width / 64);
            var yStep = Math.Max(1, bitmap.Height / 64);
            var maxChannel = 0;

            for (var y = 0; y < bitmap.Height; y += yStep)
            {
                for (var x = 0; x < bitmap.Width; x += xStep)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    maxChannel = Math.Max(maxChannel, Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)));
                    if (maxChannel > 8)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private RectangleF GetMouseScreenBounds()
        {
            if (Win32Interfaces.GetCursorPos(out var cursorPoint))
            {
                var monitorHandle = Win32Interfaces.MonitorFromPoint(cursorPoint, MonitorDefaultToNearest);
                if (monitorHandle != IntPtr.Zero)
                {
                    var monitorInfo = new MONITORINFO()
                    {
                        cbSize = Marshal.SizeOf<MONITORINFO>()
                    };

                    if (Win32Interfaces.GetMonitorInfo(monitorHandle, ref monitorInfo))
                    {
                        return ToRectangle(monitorInfo.rcMonitor);
                    }
                }
            }

            var left = Win32Interfaces.GetSystemMetrics(SystemMetricTypes.SM_XVIRTUALSCREEN);
            var top = Win32Interfaces.GetSystemMetrics(SystemMetricTypes.SM_YVIRTUALSCREEN);
            var width = Win32Interfaces.GetSystemMetrics(SystemMetricTypes.SM_CXVIRTUALSCREEN);
            var height = Win32Interfaces.GetSystemMetrics(SystemMetricTypes.SM_CYVIRTUALSCREEN);

            _logger.LogWarning("Failed to resolve mouse monitor bounds. Falling back to virtual screen bounds: left={Left}, top={Top}, width={Width}, height={Height}",
                left,
                top,
                width,
                height);

            return new RectangleF(left, top, width, height);
        }

        private static RectangleF ToRectangle(RECT rect)
        {
            return new RectangleF(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }
    }
}
