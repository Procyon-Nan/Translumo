using System;
using System.Drawing;

namespace Translumo.Processing.Interfaces
{
    public interface IScreenCapturer : IDisposable
    {
        int CaptureAttempts { get; set; }

        RectangleF CaptureArea { get; }

        void Initialize();
        byte[] CaptureScreen();
        byte[] CaptureScreen(RectangleF captureArea);
    }
}
