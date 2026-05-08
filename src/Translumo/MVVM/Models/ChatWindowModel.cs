using System;
using System.Drawing;
using Translumo.Configuration;
using Translumo.Infrastructure;
using Translumo.Processing;

namespace Translumo.MVVM.Models
{
    public class ChatWindowModel
    {
        public ChatWindowConfiguration Configuration { get; set; }
        public ScreenCaptureConfiguration CaptureConfiguration { get; set; }

        public event EventHandler<ChatItemAddedEventArgs> ChatItemAdded;
        public event EventHandler<ChatFirstItemsRemovedEventArgs> ChatFirstItemsRemoved;

        private const int CHAT_MAX_ITEMS = 50;
        private const int CHAT_ITEMS_BUFFER = 20;

        private int _chatItemsCount;

        private readonly IProcessingService _translationProcessingService;

        public ChatWindowModel(ChatWindowConfiguration configuration, ScreenCaptureConfiguration captureConfiguration,
            IProcessingService translationProcessingService)
        {
            this.Configuration = configuration;
            this.CaptureConfiguration = captureConfiguration;
            this._translationProcessingService = translationProcessingService;
            this._chatItemsCount = 0;
        }

        public Guid AddChatItem(string text, TextTypes textType, Guid? textId = null)
        {
            var actualTextId = textId ?? Guid.NewGuid();
            ChatItemAdded?.Invoke(this, new ChatItemAddedEventArgs(actualTextId, text, textType, ChatTextChangeKind.Add));
            _chatItemsCount++;
            if (CHAT_MAX_ITEMS < _chatItemsCount)
            {
                RemoveFirstChatItems(_chatItemsCount - CHAT_MAX_ITEMS + CHAT_ITEMS_BUFFER);
            }

            return actualTextId;
        }

        public void AppendChatItem(Guid textId, string text)
        {
            ChatItemAdded?.Invoke(this, new ChatItemAddedEventArgs(textId, text, TextTypes.Translation, ChatTextChangeKind.Append));
        }

        public void ReplaceChatItem(Guid textId, string text, TextTypes textType)
        {
            ChatItemAdded?.Invoke(this, new ChatItemAddedEventArgs(textId, text, textType, ChatTextChangeKind.Replace));
        }

        public void ClearAllChatItems()
        {
            RemoveFirstChatItems(_chatItemsCount);
        }

        public void RemoveFirstChatItems(int count)
        {
            _chatItemsCount -= count;
            ChatFirstItemsRemoved?.Invoke(this, new ChatFirstItemsRemovedEventArgs(count));
        }

        public void OnceTranslation(FrozenScreenCapture frozenCapture, RectangleF selectedArea)
        {
            _translationProcessingService.ProcessOnce(frozenCapture, selectedArea);
        }
    }
}
