using EnvDTE;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeMetrics;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VisualStudioMetricsOnCodeLens
{
    /// <summary>
    /// Visual Studio file save hook.
    /// </summary>
    [Export(typeof(ICommandHandler))]
    [ContentType("CSharp")]
    [Name("MetricsSaveCommandHandler")]
    internal class SaveCommandHandler : ICommandHandler<SaveCommandArgs>
    {
        private bool _isExecuting = false;

        /// <summary>
        /// Gets the display name of the command handler.
        /// </summary>
        public string DisplayName => nameof(SaveCommandHandler);

        /// <summary>
        /// Determines the state of the save command.
        /// </summary>
        /// <param name="args">The arguments associated with the save command.</param>
        /// <returns>A <see cref="CommandState"/> indicating that the save command is available.</returns>
        public CommandState GetCommandState(SaveCommandArgs args)
            => CommandState.Available;

        /// <summary>
        /// Executes the specified save command within the given execution context.
        /// </summary>
        /// <remarks>
        /// This method retrieves solution information from the Visual Studio environment and performs an analysis based on the solution directory and file.
        /// It also broadcasts a reload token to notify listeners of the operation.
        /// </remarks>
        /// <param name="args">The arguments for the save command, containing details about the operation to be performed.</param>
        /// <param name="context">The context in which the command is executed, providing additional information about the execution
        /// environment.</param>
        /// <returns><see langword="false"/> to indicate that the command execution does not require further processing.</returns>
        public bool ExecuteCommand(SaveCommandArgs args, CommandExecutionContext context)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            var vssolution = (IVsSolution)ServiceProvider.GlobalProvider.GetService(typeof(SVsSolution));
            vssolution.GetSolutionInfo(out string _, out string slnFile, out string _);
            _ = AnalyzeMetricsAsync(slnFile);

            return false;
        }

        private async Task AnalyzeMetricsAsync(string slnFile)
        {
            if (string.IsNullOrEmpty(slnFile))
            {
                return;
            }

            if (_isExecuting)
            {
                return; // duplicate execution guard
            }

            _isExecuting = true;

            // Analyze metrics
            var workspace = MSBuildWorkspace.Create();
            var solution = await workspace.OpenSolutionAsync(slnFile);

            Logger.LogInfo($">>> Analyzing Start >>>");
            foreach (var projectId in solution.ProjectIds)
            {
                await AnalyzeProjectMetricsAsync(solution, projectId);
            }

            var dt = DateTime.Now;
            Logger.LogInfo($"<<< Analyzing Completed {dt.ToLocalTime()} <<<");
            PipeServerHost.Broadcast(PipeServerHost.ReloadToken);

            _isExecuting = false;
        }

        private async Task AnalyzeProjectMetricsAsync(Microsoft.CodeAnalysis.Solution solution, ProjectId projectId)
        {
            var project = solution?.GetProject(projectId);

            // Skip non-compilable projects or null projects
            if (project == null || project.SupportsCompilation == false) return;

            Logger.LogInfo($"--- Analyzing Project: {project.Name} ---");

            try
            {
                var compilation = await project.GetCompilationAsync();

                if (compilation.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error))
                {
                    Logger.LogInfo($"Warning: Project {project.Name} has compilation errors.");
                }

                var context = new CodeMetricsAnalysisContext(compilation, CancellationToken.None);
                var metricData = await CodeAnalysisMetricData.ComputeAsync(context);

                var slnDir = Path.GetDirectoryName(solution.FilePath);
                Metrics.SaveToFile(metricData, Path.Combine(slnDir, ".Metrics", $"{project.Name}.json"));
            }
            catch (Exception ex)
            {
                Logger.LogInfo($"Failed to analyze project {project.Name}: {ex.Message}");
            }
        }
    }
}
