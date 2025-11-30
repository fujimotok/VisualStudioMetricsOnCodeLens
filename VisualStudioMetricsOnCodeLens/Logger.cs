using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;

namespace VisualStudioMetricsOnCodeLens
{
    internal static class Logger
    {
        private static readonly string PaneGuidString = "D2A1B0F2-1234-4C56-ABCD-9876543210AB"; // Output pane GUID
        private static readonly string PaneTitle = "Metrics on CodeLens";
        private static IVsOutputWindowPane _pane;

        /// <summary>
        /// Gets the output pane for logging.
        /// </summary>
        public static void StartLogger()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var outputWindow = (IVsOutputWindow)ServiceProvider.GlobalProvider.GetService(typeof(SVsOutputWindow));

            Guid paneGuid = new Guid(PaneGuidString);
            outputWindow.CreatePane(ref paneGuid, PaneTitle, 1, 1);
            outputWindow.GetPane(ref paneGuid, out _pane);
        }

        /// <summary>
        /// Logs the details of the specified exception as an error.
        /// </summary>
        /// <param name="ex">The exception to log. Must not be <see langword="null"/>.</param>
        public static void LogError(System.Exception ex)
        {
            WriteLine($"{ex.GetType()}: {ex.Message}\n{ex.StackTrace}");
        }

        /// <summary>
        /// Logs an informational message to the output.
        /// </summary>
        /// <param name="message">The message to log. Cannot be null or empty.</param>
        public static void LogInfo(string message)
        {
            WriteLine(message);
        }

        private static void WriteLine(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _pane?.OutputString(message + Environment.NewLine);
        }
    }
}