using System;
using Translumo.Infrastructure;

namespace Translumo.Services
{
    public class TranslatedEventArgs : EventArgs
    {
        public Guid TextId { get; set; }

        public string Text { get; set; }

        public TextTypes TextType { get; set; }

        public ChatTextChangeKind ChangeKind { get; set; }

        public TranslatedEventArgs(Guid textId, string text, TextTypes textType, ChatTextChangeKind changeKind)
        {
            TextId = textId;
            Text = text;
            TextType = textType;
            ChangeKind = changeKind;
        }
    }
}
