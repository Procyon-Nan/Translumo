using System.Windows.Input;
using SharpDX.XInput;
using Translumo.Utils;

namespace Translumo.HotKeys
{
    public class HotKeysConfiguration : BindableBase
    {
        public static HotKeysConfiguration Default => new HotKeysConfiguration()
        {
            ChatVisibilityKey = new HotKeyInfo(Key.T, KeyModifier.Alt),
            SettingVisibilityKey = new HotKeyInfo(Key.G, KeyModifier.Alt),
            OnceTranslateKey = new HotKeyInfo(Key.F, KeyModifier.Shift),
            WindowStyleChangeKey = new HotKeyInfo(Key.T, KeyModifier.Ctrl),

            ChatVisibilityGamepadKey = new GamepadHotKeyInfo(GamepadKeyCode.None),
            SettingVisibilityGamepadKey = new GamepadHotKeyInfo(GamepadKeyCode.None),
            OnceTranslateGamepadKey = new GamepadHotKeyInfo(GamepadKeyCode.None),
            WindowStyleChangeGamepadKey = new GamepadHotKeyInfo(GamepadKeyCode.None)
        };

        public HotKeyInfo ChatVisibilityKey
        {
            get => _chatVisibilityKey;
            set
            {
                SetProperty(ref _chatVisibilityKey, value);
            }
        }

        public HotKeyInfo SettingVisibilityKey
        {
            get => _settingVisibilityKey;
            set 
            {
                SetProperty(ref _settingVisibilityKey, value);
            }
        }

        public HotKeyInfo OnceTranslateKey
        {
            get => _onceTranslateKey;
            set
            {
                SetProperty(ref _onceTranslateKey, value);
            }
        }

        public HotKeyInfo WindowStyleChangeKey
        {
            get => _windowStyleChangeKey;
            set
            {
                SetProperty(ref _windowStyleChangeKey, value);
            }
        }



        public GamepadHotKeyInfo ChatVisibilityGamepadKey
        {
            get => _chatVisibilityGamepadKey;
            set
            {
                SetProperty(ref _chatVisibilityGamepadKey, value);
            }
        }

        public GamepadHotKeyInfo SettingVisibilityGamepadKey
        {
            get => _settingVisibilityGamepadKey;
            set
            {
                SetProperty(ref _settingVisibilityGamepadKey, value);
            }
        }

        public GamepadHotKeyInfo OnceTranslateGamepadKey
        {
            get => _onceTranslateGamepadKey;
            set
            {
                SetProperty(ref _onceTranslateGamepadKey, value);
            }
        }

        public GamepadHotKeyInfo WindowStyleChangeGamepadKey
        {
            get => _windowStyleChangeGamepadKey;
            set
            {
                SetProperty(ref _windowStyleChangeGamepadKey, value);
            }
        }

        private HotKeyInfo _chatVisibilityKey;
        private HotKeyInfo _settingVisibilityKey;
        private HotKeyInfo _onceTranslateKey;
        private HotKeyInfo _windowStyleChangeKey = new HotKeyInfo(Key.T, KeyModifier.Ctrl);

        private GamepadHotKeyInfo _chatVisibilityGamepadKey;
        private GamepadHotKeyInfo _settingVisibilityGamepadKey;
        private GamepadHotKeyInfo _onceTranslateGamepadKey;
        private GamepadHotKeyInfo _windowStyleChangeGamepadKey = new GamepadHotKeyInfo(GamepadKeyCode.None);
    }
}
