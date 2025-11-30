using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using System.ComponentModel;

namespace VisualStudioMetricsOnCodeLens
{
    public class OptionPage : DialogPage
    {
        private const string CollectionPath = "MetricsOnCodeLens";
        private const string CodeLensDescriptionDefault = "MI(%MI%)";

        #region Properties
        [Category("General")]
        [DisplayName("CodeLens description format")]
        [Description("Customize description with placeholders\n%MI%: Maintainability Index\n%CY%: Cyclomatic Complexity\n%CC%: Class Coupling\n%DI%: Depth Of Inheritance\n%SL%: Source Lines\n%EL%: Executable Lines")]
        public string CodeLensDescription { get; set; } = CodeLensDescriptionDefault;
        #endregion

        public override void SaveSettingsToStorage()
        {
            base.SaveSettingsToStorage();

            ThreadHelper.ThrowIfNotOnUIThread();
            var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
            var writableStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            if (!writableStore.CollectionExists(CollectionPath))
            {
                writableStore.CreateCollection(CollectionPath);
            }

            writableStore.SetString(CollectionPath, nameof(CodeLensDescription), CodeLensDescription);
            PipeServerHost.Broadcast(PipeServerHost.ReloadToken);
        }

        public override void LoadSettingsFromStorage()
        {
            base.LoadSettingsFromStorage();

            ThreadHelper.ThrowIfNotOnUIThread();
            var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
            var store = settingsManager.GetReadOnlySettingsStore(SettingsScope.UserSettings);

            if (store.CollectionExists(CollectionPath))
            {
                if (store.PropertyExists(CollectionPath, nameof(CodeLensDescription)))
                    CodeLensDescription = store.GetString(CollectionPath, nameof(CodeLensDescription));
            }
        }
    }
}
