using System;
using Translumo.Infrastructure;
using Translumo.Processing.Interfaces;
using Translumo.Utils;

namespace Translumo.Services
{
    public class ChatUITextMediator : IChatTextMediator
    {
        public event EventHandler<TranslatedEventArgs> TextRaised;
        public event EventHandler ClearTextsRaised; 

        public Guid SendText(string text, bool successful)
        {
            return SendText(text, successful ? TextTypes.Translation : TextTypes.Error);
        }

        public Guid SendText(string text, TextTypes textType)
        {
            var textId = Guid.NewGuid();
            TextRaised?.RaiseOnUIThread(this, new TranslatedEventArgs(textId, text, textType, ChatTextChangeKind.Add));
            return textId;
        }

        public void AppendText(Guid textId, string text)
        {
            TextRaised?.RaiseOnUIThread(this, new TranslatedEventArgs(textId, text, TextTypes.Translation, ChatTextChangeKind.Append));
        }

        public void ReplaceText(Guid textId, string text, TextTypes textType)
        {
            TextRaised?.RaiseOnUIThread(this, new TranslatedEventArgs(textId, text, textType, ChatTextChangeKind.Replace));
        }

        public void ClearTexts()
        {
            ClearTextsRaised?.RaiseOnUIThread(this);
        }
    }
}
