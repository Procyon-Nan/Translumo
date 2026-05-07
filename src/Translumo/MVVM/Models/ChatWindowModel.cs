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

        public void AddChatItem(string text, TextTypes textType)
        {
            ChatItemAdded?.Invoke(this, new ChatItemAddedEventArgs(text, textType));
            _chatItemsCount++;
            if (CHAT_MAX_ITEMS < _chatItemsCount)
            {
                RemoveFirstChatItems(_chatItemsCount - CHAT_MAX_ITEMS + CHAT_ITEMS_BUFFER);
            }
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
