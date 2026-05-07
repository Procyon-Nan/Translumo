using System;
using System.Drawing;

namespace Translumo.Processing
{
    public sealed class FrozenScreenCapture
    {
        public Guid IterationId { get; set; }

        public RectangleF ScreenBounds { get; set; }

        public byte[] ImageBytes { get; set; }
    }
}
