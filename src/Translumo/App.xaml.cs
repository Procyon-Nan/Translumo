using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using SharpDX.XInput;
using System;
using System.Windows;
using System.Windows.Threading;
using Translumo.Configuration;
using Translumo.Dialog;
using Translumo.HotKeys;
using Translumo.Infrastructure.Dispatching;
using Translumo.Infrastructure.Encryption;
using Translumo.Infrastructure.Language;
using Translumo.Logging;
using Translumo.MVVM.Models;
using Translumo.MVVM.ViewModels;
using Translumo.Processing;
using Translumo.Processing.Configuration;
using Translumo.Processing.Interfaces;
using Translumo.Services;
using Translumo.Translation;
using Translumo.Translation.Configuration;
using Translumo.TTS;
using Translumo.Update;
using Translumo.Utils;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Translumo
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly ILogger _logger;

        public App()
        {
            Log.Logger = CreateLogger();

            ServiceCollection services = new ServiceCollection();
            ConfigureServices(services);
            this._serviceProvider = services.BuildServiceProvider();
            this._logger = _serviceProvider.GetService<ILogger<App>>();

            this.DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
        }

        private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _logger.LogCritical(e.ExceptionObject as Exception, "Unhandled app exception");
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            _logger.LogCritical(e.Exception, "Unhandled app exception");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            var configurationStorage = _serviceProvider.GetService<ConfigurationStorage>();
            configurationStorage.SaveConfiguration();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var configurationStorage = _serviceProvider.GetService<ConfigurationStorage>();
            configurationStorage.LoadConfiguration();

            _serviceProvider.GetService<ApplicationThemeService>()?.ApplyCurrentTheme();

            var chatViewModel = _serviceProvider.GetService<ChatWindowViewModel>();
            var dialogService = _serviceProvider.GetService<DialogService>();
            dialogService.ShowWindowAsync(chatViewModel);

            _serviceProvider.RegisterUIInputController();
        }

        private void ConfigureServices(ServiceCollection services)
        {
            services.AddLogging(builder => builder.AddSerilog(/*Log.Logger,*/ dispose: true));

            services.AddScoped<SettingsViewModel>();
            services.AddScoped<AppearanceSettingsViewModel>();
            services.AddScoped<HotkeysSettingsViewModel>();
            services.AddScoped<LanguagesSettingsViewModel>();
            services.AddScoped<LogSettingsViewModel>();

            var chatWindowConfiguration = ChatWindowConfiguration.Default;
            services.AddSingleton<TranslationConfiguration>(TranslationConfiguration.Default);
            services.AddSingleton<TtsConfiguration>(TtsConfiguration.Default);
            services.AddSingleton<ChatWindowConfiguration>(chatWindowConfiguration);
            services.AddSingleton<HotKeysConfiguration>(HotKeysConfiguration.Default);
            services.AddSingleton<SystemConfiguration>(SystemConfiguration.Default);
            services.AddSingleton<TextProcessingConfiguration>(chatWindowConfiguration.TextProcessing);
            services.AddSingleton(RuntimeLogStore.Instance);
            services.AddSingleton<ApplicationThemeService>();

            var chatMediatorInstance = new ChatUITextMediator();
            services.AddSingleton<IChatTextMediator, ChatUITextMediator>(provider => chatMediatorInstance);
            services.AddSingleton<ChatUITextMediator>(chatMediatorInstance);
            services.AddSingleton<ChatWindowViewModel>();
            services.AddSingleton<ChatWindowModel>();
            services.AddSingleton<HotKeysServiceManager>();
            services.AddSingleton<ScreenCaptureConfiguration>();
            services.AddSingleton<DialogService>();
            services.AddSingleton<LanguageService>();
            services.AddSingleton<IActionDispatcher, InteractionActionDispatcher>();
            services.AddSingleton<IControllerService, GamepadService>();
            services.AddSingleton<IControllerInputProvider, ControllerInputProvider>();
            services.AddSingleton<ObservablePipe<Keystroke>>(new ObservablePipe<Keystroke>(Application.Current.Dispatcher));
            services.AddSingleton<UpdateManager>();
            services.AddSingleton<IReleasesClient, GithubApiClient>(provider => new GithubApiClient("Procyon-Nan", "Translumo"));
            services.AddSingleton<ICapturerFactory, ScreenCapturerFactory>();
            services.AddTransient<IProcessingService, TranslationProcessingService>();
            services.AddTransient<AiTextRecognitionService>();
            services.AddTransient<ScreenshotArchiveService>();
            services.AddTransient<FrozenScreenCaptureService>();
            services.AddTransient<TranslatorFactory>();
            services.AddSingleton<IProcessingTextLocalizer, ProcessingTextLocalizer>();
            services.AddTransient<IEncryptionService, AesEncryptionService>();
            services.AddTransient<LanguageDescriptorFactory>();
            services.AddTransient<TtsFactory>();


            services.AddConfigurationStorage();
        }

        private Logger CreateLogger()
        {
            var configuration = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Verbose()
                .WriteTo.Sink(new RuntimeLogSink(RuntimeLogStore.Instance))
                .WriteTo.File("Logs/log.txt", LogEventLevel.Warning, rollingInterval: RollingInterval.Day, outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}", retainedFileCountLimit: 10);

#if DEBUG
            configuration = configuration.WriteTo.File("Logs/trace.txt", LogEventLevel.Verbose, rollingInterval: RollingInterval.Day, outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}");
#endif

            return configuration.CreateLogger();
        }

    }

}
