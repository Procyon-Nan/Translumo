using System.Globalization;
using Translumo.Utils;

namespace Translumo.Configuration
{
    public class SystemConfiguration : BindableBase
    {
        public static SystemConfiguration Default => new SystemConfiguration()
        {
            ApplicationCulture = LocalizationManager.GetDefaultCultureName(),
            DarkModeEnabled = false
        };

        public string ApplicationCulture
        {
            get => _applicationCulture;
            set
            {
                SetProperty(ref _applicationCulture, value);
                UpdateSelectedLanguage();
            }
        }

        private string _applicationCulture;

        public bool DarkModeEnabled
        {
            get => _darkModeEnabled;
            set => SetProperty(ref _darkModeEnabled, value);
        }

        private bool _darkModeEnabled;

        private void UpdateSelectedLanguage()
        {
            LocalizationManager.ChangeAppCulture(new CultureInfo(ApplicationCulture));
        }
    }
}
