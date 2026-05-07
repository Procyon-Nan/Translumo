using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using Translumo.Configuration;
using Translumo.Processing.Exceptions;
using Translumo.Processing.Interfaces;
using Translumo.Utils;
using Translumo.Utils.IntertopStruct;

namespace Translumo.Services
{
    public class BitBltScreenCapture : IScreenCapturer
    {
        public int CaptureAttempts { get; set; } = 3;
        public int AttemptDelayMs { get; set; } = 300;
        public RectangleF CaptureArea => _configuration.CaptureArea;

        private int _height;
        private int _left;
        private int _top;
        private int _width;

        private readonly ScreenCaptureConfiguration _configuration;

        public BitBltScreenCapture(ScreenCaptureConfiguration configuration)
        {
            this._configuration = configuration;
        }

        public void Initialize()
        {
            _left = Win32Interfaces.GetSystemMetrics(SystemMetricTypes.SM_XVIRTUALSCREEN);
            _top = Win32Interfaces.GetSystemMetrics(SystemMetricTypes.SM_YVIRTUALSCREEN);
            _width = Win32Interfaces.GetSystemMetrics(SystemMetricTypes.SM_CXVIRTUALSCREEN);
            _height = Win32Interfaces.GetSystemMetrics(SystemMetricTypes.SM_CYVIRTUALSCREEN);
        }

        public byte[] CaptureScreen()
        {
            if (_configuration.CaptureArea.IsEmpty)
            {
                throw new CaptureException($"Capture area is not selected");
            }

            return MakeScreenshotInternal(_configuration.CaptureArea, 1);
        }

        public byte[] CaptureScreen(RectangleF captureArea)
        {
            return MakeScreenshotInternal(captureArea, 1);
        }

        public void Dispose()
        {
        }

        private byte[] MakeScreenshotInternal(RectangleF captureArea, int curAttempt)
        {
            IntPtr hdcSrc = IntPtr.Zero;
            IntPtr hdcDest = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;

            try
            { 
                hdcSrc = Win32Interfaces.GetDCEx(IntPtr.Zero, IntPtr.Zero,
                    DeviceContextValues.Window | DeviceContextValues.Cache | DeviceContextValues.LockWindowUpdate);
                hdcDest = Win32Interfaces.CreateCompatibleDC(hdcSrc);
                hBitmap = Win32Interfaces.CreateCompatibleBitmap(hdcSrc, _width, _height);
                var hOld = Win32Interfaces.SelectObject(hdcDest, hBitmap);
                Win32Interfaces.BitBlt(hdcDest, 0, 0, _width, _height, hdcSrc, _left, _top,
                    TernaryRasterOperations.SRCCOPY | TernaryRasterOperations.CAPTUREBLT);
                Win32Interfaces.SelectObject(hdcDest, hOld);

                using var img = Image.FromHbitmap(hBitmap);
                using var bitmap = img.Clone(GetBitmapRelativeArea(captureArea), PixelFormat.Format32bppArgb);

                return bitmap.ToBytes(ImageFormat.Tiff);
            }
            catch (Exception e)
            {
                if (curAttempt >= CaptureAttempts)
                {
                    throw new CaptureException("Failed to capture screen", e);
                }

                Thread.Sleep(AttemptDelayMs);

                return MakeScreenshotInternal(captureArea, ++curAttempt);
            }
            finally
            {
                if (hdcDest != IntPtr.Zero)
                {
                    Win32Interfaces.DeleteDC(hdcDest);
                }

                if (hdcSrc != IntPtr.Zero)
                {
                    Win32Interfaces.ReleaseDC(IntPtr.Zero, hdcSrc);
                }

                if (hBitmap != IntPtr.Zero)
                {
                    Win32Interfaces.DeleteObject(hBitmap);
                }
            }
        }

        private RectangleF GetBitmapRelativeArea(RectangleF captureArea)
        {
            var left = Math.Max(0, captureArea.Left - _left);
            var top = Math.Max(0, captureArea.Top - _top);
            var right = Math.Min(_width, captureArea.Right - _left);
            var bottom = Math.Min(_height, captureArea.Bottom - _top);

            if (right <= left || bottom <= top)
            {
                throw new CaptureException($"Capture area is outside the virtual screen ({captureArea})");
            }

            return new RectangleF(left, top, right - left, bottom - top);
        }
    }
}
