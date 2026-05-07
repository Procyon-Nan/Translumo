using System.Drawing;

namespace Translumo.Processing
{
    public interface IProcessingService
    {
        void ProcessOnce(FrozenScreenCapture frozenCapture, RectangleF selectedArea);
    }
}
