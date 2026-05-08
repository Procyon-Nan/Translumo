using System;
using Translumo.Infrastructure;

namespace Translumo.Processing.Interfaces
{
    public interface IChatTextMediator
    {
        Guid SendText(string text, bool successful);

        Guid SendText(string text, TextTypes textType);

        void AppendText(Guid textId, string text);

        void ReplaceText(Guid textId, string text, TextTypes textType);

        void ClearTexts();
    }
}
