using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Translumo.Configuration;

namespace Translumo.Services
{
    public sealed class ApplicationThemeService
    {
        private static readonly string[] AppBrushKeys =
        {
            "PrimaryHueLightBrush",
            "PrimaryHueLightForegroundBrush",
            "PrimaryHueMidBrush",
            "PrimaryHueMidForegroundBrush",
            "PrimaryHueDarkBrush",
            "PrimaryHueDarkForegroundBrush",
            "SecondaryHueLightBrush",
            "SecondaryHueLightForegroundBrush",
            "SecondaryHueMidBrush",
            "SecondaryHueMidForegroundBrush",
            "SecondaryHueDarkBrush",
            "SecondaryHueDarkForegroundBrush",
            "ErrorBrush",
            "HoverBackgroundBrush",
            "WarningBrush"
        };

        private readonly SystemConfiguration _systemConfiguration;
        private readonly ConfigurationStorage _configurationStorage;
        private readonly ILogger<ApplicationThemeService> _logger;
        private bool _initialThemeApplied;

        public ApplicationThemeService(
            SystemConfiguration systemConfiguration,
            ConfigurationStorage configurationStorage,
            ILogger<ApplicationThemeService> logger)
        {
            _systemConfiguration = systemConfiguration;
            _configurationStorage = configurationStorage;
            _logger = logger;
            _systemConfiguration.PropertyChanged += OnSystemConfigurationPropertyChanged;
        }

        public void ApplyCurrentTheme()
        {
            ApplyTheme(_systemConfiguration.DarkModeEnabled);
            _initialThemeApplied = true;
        }

        private void OnSystemConfigurationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(SystemConfiguration.DarkModeEnabled))
            {
                return;
            }

            ApplyTheme(_systemConfiguration.DarkModeEnabled);
            if (_initialThemeApplied)
            {
                _configurationStorage.SaveConfiguration();
            }
        }

        private void ApplyTheme(bool darkModeEnabled)
        {
            try
            {
                ApplyMaterialDesignTheme(darkModeEnabled);
                ApplyAppThemeDictionary(darkModeEnabled);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply application theme");
            }
        }

        private static void ApplyMaterialDesignTheme(bool darkModeEnabled)
        {
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(darkModeEnabled ? Theme.Dark : Theme.Light);
            paletteHelper.SetTheme(theme);
        }

        private static void ApplyAppThemeDictionary(bool darkModeEnabled)
        {
            var themeSource = new Uri(
                darkModeEnabled ? "/Themes/AppTheme.Dark.xaml" : "/Themes/AppTheme.Light.xaml",
                UriKind.Relative);
            var targetThemeDictionary = new ResourceDictionary { Source = themeSource };

            UpdateExistingBrushes(targetThemeDictionary);
            ReplaceThemeDictionary(themeSource, targetThemeDictionary);
        }

        private static void UpdateExistingBrushes(ResourceDictionary targetThemeDictionary)
        {
            foreach (var key in AppBrushKeys)
            {
                if (!targetThemeDictionary.Contains(key))
                {
                    continue;
                }

                var targetBrush = targetThemeDictionary[key] as SolidColorBrush;
                var existingBrush = Application.Current.TryFindResource(key) as SolidColorBrush;
                if (targetBrush == null || existingBrush == null || existingBrush.IsFrozen)
                {
                    continue;
                }

                existingBrush.Color = targetBrush.Color;
                existingBrush.Opacity = targetBrush.Opacity;
            }
        }

        private static void ReplaceThemeDictionary(Uri themeSource, ResourceDictionary targetThemeDictionary)
        {
            var mergedDictionaries = Application.Current.Resources.MergedDictionaries;
            var currentThemeDictionary = mergedDictionaries.FirstOrDefault(dictionary =>
                dictionary.Source?.OriginalString.Contains("/Themes/AppTheme.", StringComparison.OrdinalIgnoreCase) == true
                || dictionary.Source?.OriginalString.Contains("Themes/AppTheme.", StringComparison.OrdinalIgnoreCase) == true);

            if (currentThemeDictionary == null)
            {
                mergedDictionaries.Add(targetThemeDictionary);
                return;
            }

            var currentIndex = mergedDictionaries.IndexOf(currentThemeDictionary);
            mergedDictionaries.RemoveAt(currentIndex);
            targetThemeDictionary.Source = themeSource;
            mergedDictionaries.Insert(currentIndex, targetThemeDictionary);
        }
    }
}
