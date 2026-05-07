using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Serilog.Events;
using Translumo.Logging;
using Translumo.MVVM.Common;
using Translumo.Utils;

namespace Translumo.MVVM.ViewModels
{
    public sealed class LogSettingsViewModel : BindableBase, IDisposable
    {
        public ObservableCollection<RuntimeLogEntry> VisibleLogs
        {
            get => _visibleLogs;
            set => SetProperty(ref _visibleLogs, value);
        }

        public ObservableCollection<LogLevelFilterOption> LevelFilters
        {
            get => _levelFilters;
            set => SetProperty(ref _levelFilters, value);
        }

        public LogLevelFilterOption SelectedLevelFilter
        {
            get => _selectedLevelFilter;
            set
            {
                SetProperty(ref _selectedLevelFilter, value);
                RefreshLogs();
            }
        }

        public RuntimeLogEntry SelectedLogEntry
        {
            get => _selectedLogEntry;
            set => SetProperty(ref _selectedLogEntry, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                SetProperty(ref _searchText, value);
                RefreshLogs();
            }
        }

        public ICommand ClearLogsCommand => new RelayCommand(ClearLogs);

        public ICommand CopySelectedLogCommand => new RelayCommand(CopySelectedLog);

        private readonly RuntimeLogStore _logStore;
        private ObservableCollection<RuntimeLogEntry> _visibleLogs;
        private ObservableCollection<LogLevelFilterOption> _levelFilters;
        private LogLevelFilterOption _selectedLevelFilter;
        private RuntimeLogEntry _selectedLogEntry;
        private string _searchText = string.Empty;

        public LogSettingsViewModel(RuntimeLogStore logStore)
        {
            _logStore = logStore;
            VisibleLogs = new ObservableCollection<RuntimeLogEntry>();
            InitializeLevelFilters();
            RefreshLogs();

            _logStore.EntryAdded += LogStoreOnEntryAdded;
            _logStore.Cleared += LogStoreOnCleared;
        }

        private void InitializeLevelFilters()
        {
            var selectedLevel = SelectedLevelFilter?.Level;
            LevelFilters = new ObservableCollection<LogLevelFilterOption>()
            {
                new LogLevelFilterOption(LocalizationManager.GetValue("Str.LogSettings.LevelAll", false, OnLocalizedValueChanged, this), null),
                new LogLevelFilterOption("Verbose", LogEventLevel.Verbose),
                new LogLevelFilterOption("Debug", LogEventLevel.Debug),
                new LogLevelFilterOption("Information", LogEventLevel.Information),
                new LogLevelFilterOption("Warning", LogEventLevel.Warning),
                new LogLevelFilterOption("Error", LogEventLevel.Error),
                new LogLevelFilterOption("Fatal", LogEventLevel.Fatal)
            };

            SelectedLevelFilter = LevelFilters.FirstOrDefault(item => item.Level == selectedLevel)
                ?? LevelFilters[0];
        }

        private void RefreshLogs()
        {
            if (_logStore == null)
            {
                return;
            }

            VisibleLogs = new ObservableCollection<RuntimeLogEntry>(_logStore.Snapshot().Where(MatchesFilter));
        }

        private bool MatchesFilter(RuntimeLogEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            if (SelectedLevelFilter?.Level != null && entry.Level != SelectedLevelFilter.Level.Value)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return true;
            }

            var searchText = SearchText.Trim();
            return Contains(entry.Source, searchText)
                || Contains(entry.LevelName, searchText)
                || Contains(entry.Message, searchText)
                || Contains(entry.Details, searchText);
        }

        private static bool Contains(string value, string searchText)
        {
            return value?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void LogStoreOnEntryAdded(object sender, RuntimeLogEntry entry)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                AddVisibleEntry(entry);
                return;
            }

            dispatcher.BeginInvoke((Action)(() => AddVisibleEntry(entry)));
        }

        private void AddVisibleEntry(RuntimeLogEntry entry)
        {
            if (!MatchesFilter(entry))
            {
                return;
            }

            VisibleLogs.Add(entry);
            while (VisibleLogs.Count > 1000)
            {
                VisibleLogs.RemoveAt(0);
            }
        }

        private void LogStoreOnCleared(object sender, EventArgs e)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                VisibleLogs.Clear();
                return;
            }

            dispatcher.BeginInvoke((Action)(() => VisibleLogs.Clear()));
        }

        private void ClearLogs()
        {
            _logStore.Clear();
        }

        private void CopySelectedLog()
        {
            if (SelectedLogEntry == null)
            {
                return;
            }

            Clipboard.SetText(SelectedLogEntry.FullText);
        }

        private void OnLocalizedValueChanged(string key, string oldValue)
        {
            InitializeLevelFilters();
        }

        public void Dispose()
        {
            _logStore.EntryAdded -= LogStoreOnEntryAdded;
            _logStore.Cleared -= LogStoreOnCleared;
            LocalizationManager.ReleaseChangedValuesCallbacks(this);
        }
    }

    public sealed class LogLevelFilterOption
    {
        public LogLevelFilterOption(string displayName, LogEventLevel? level)
        {
            DisplayName = displayName;
            Level = level;
        }

        public string DisplayName { get; }

        public LogEventLevel? Level { get; }
    }
}
