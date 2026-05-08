using System;
using System.Threading.Tasks;

namespace Translumo.Translation
{
    public interface IStreamingTranslator : ITranslator
    {
        Task<string> TranslateTextAsync(string sourceText, Action<string> onDelta);
    }
}
