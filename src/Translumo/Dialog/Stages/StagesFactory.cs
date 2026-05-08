using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Translumo.Utils;

namespace Translumo.Dialog.Stages
{
    public static class StagesFactory
    {
        public static InteractionStage CreateLanguageChangeStages(DialogService dialogService, Action changeLangAction, ILogger logger)
        {
            return new ActionInteractionStage(dialogService, () =>
                        Task.Factory.StartNew(changeLangAction), LocalizationManager.GetValue($"Str.Stages.SwitchLanguage"))
                .AddException(new ExceptionInteractionStage(dialogService, (ex) => logger.LogError(ex, "Language change error"), LocalizationManager.GetValue("Str.Stages.SwitchLanguageError")));
        }

        public static InteractionStage CreateWindowsTtsCheckingStages(
            DialogService dialogService,
            string languageCode,
            InteractionStage enableFlagStage,
            ILogger logger)
            {
            return new ConditionalInteractionStage(
                    dialogService,
                    () => Task.FromResult(TTS.WindowsTTSHelper.IsLanguageTTSCapabilityInstalled(languageCode)),
                    LocalizationManager.GetValue("Str.Stages.CheckLangPack"))
                .AddNextFalse(
                    new DialogQuestionInteractionStage(
                        dialogService,
                        string.Format(LocalizationManager.GetValue("Str.Stages.TTSLangPackQuestion", true), languageCode))
                    .AddNextStage(
                        new ConditionalInteractionStage(
                                dialogService,
                                async () => await TTS.WindowsTTSHelper.InstallTTSLanguageCapatibility(languageCode),
                                LocalizationManager.GetValue("Str.Stages.InstallationLangPack"))
                            .AddNextStage(
                                new DialogInteractionStage(
                                        dialogService,
                                        LocalizationManager.GetValue("Str.Stages.LangPackInstalledRestart"))
                                    .AddNextStage(enableFlagStage))
                            .AddException(
                                new ExceptionInteractionStage(
                                    dialogService,
                                    ex => logger.LogError(ex, "Language windows pack install error"),
                                    LocalizationManager.GetValue("Str.Stages.InstallationLangError")))))
                .AddNextStage(enableFlagStage)
                .AddException(
                    new ExceptionInteractionStage(
                        dialogService,
                        ex => logger.LogError(ex, "Checking language pack error"),
                        LocalizationManager.GetValue("Str.Stages.CheckLangPackError", true)));
        }
    }
}
