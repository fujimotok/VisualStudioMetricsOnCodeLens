using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using Microsoft.VisualStudio.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace VisualStudioMetricsOnCodeLens
{
    /// <summary>
    /// Parse the element from CodeLens and load corresponding metrics from JSON file.
    /// </summary>
    [Export(typeof(ICodeLensCallbackListener))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    [ContentType("CSharp")]
    internal class MetricsCodeLensDocumentParser : ICodeLensCallbackListener
    {
        private readonly VisualStudioWorkspace _workspace;
        private readonly string _metricsDir;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsCodeLensDocumentParser"/> class.
        /// </summary>
        /// <remarks>The constructor requires a valid <see cref="VisualStudioWorkspace"/> instance. The
        /// solution directory is derived from the  <see cref="VisualStudioWorkspace.CurrentSolution"/> property. Ensure
        /// that the workspace is properly initialized before  passing it to this constructor.</remarks>
        /// <param name="workspace">The <see cref="VisualStudioWorkspace"/> instance representing the current Visual Studio workspace.  This is
        /// used to determine the solution directory and manage workspace-related operations.</param>
        [ImportingConstructor]
        public MetricsCodeLensDocumentParser(VisualStudioWorkspace workspace)
        {
            _workspace = workspace;

            try
            {
                var solutionDir = Path.GetDirectoryName(workspace.CurrentSolution.FilePath);
                _metricsDir = Path.Combine(solutionDir, ".Metrics");
            }
            catch (Exception)
            {
                _metricsDir = string.Empty;
            }
        }

        /// <summary>
        /// CodeLens description string from user settings.
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetCodeLensDescriptionAsync()
        {
            var descriptionDefault = "MI(%)";
            var collection = "MetricsOnCodeLens";
            var property = "CodeLensDescription";

            var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
            var store = settingsManager.GetReadOnlySettingsStore(SettingsScope.UserSettings);

            if (store.PropertyExists(collection, property) == false)
            {
                return descriptionDefault;
            }

            try
            {
                return await Task.FromResult<string>(store.GetString(collection, property));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                return descriptionDefault;
            }
        }

        /// <summary>
        /// Asynchronously loads code metrics for the specified code element.
        /// </summary>
        /// <remarks>This method retrieves the syntax node corresponding to the specified code element,
        /// extracts its identifier, and calculates the associated metrics. If an error occurs during processing, the
        /// returned <see cref="Metrics"/> object will include the error details.</remarks>
        /// <param name="descriptor">The descriptor containing information about the code element, such as its file path and project GUID.</param>
        /// <param name="descriptorContext">The context providing additional details about the code element, including its applicable span.</param>
        /// <returns>A <see cref="Metrics"/> object containing the calculated metrics for the specified code element. If an error
        /// occurs, the returned object will contain error details.</returns>
        public async Task<Metrics> LoadCodeMetricsAsync(
            CodeLensDescriptor descriptor,
            CodeLensDescriptorContext descriptorContext
            )
        {
            if (descriptor == null || descriptorContext == null)
            {
                return new Metrics();
            }

            try
            {
                var document = _workspace.GetDocument(descriptor.FilePath, descriptor.ProjectGuid);
                var span = descriptorContext.ApplicableSpan.Value;
                var node = await GetSyntaxNodeAsync(document, span);
                var identifier = await GetIdentifierAsync(document, node);

                return GetMetrics(document, identifier);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                return new Metrics();
            }
        }

        private async Task<SyntaxNode> GetSyntaxNodeAsync(Document document, Microsoft.VisualStudio.Text.Span span)
        {
            if (document == null || span == null)
            {
                return null;
            }

            var root = await document.GetSyntaxRootAsync();

            return root?.DescendantNodes()
                .FirstOrDefault(node => node?.Span.Start == span.Start && node?.Span.Length == span.Length);
        }

        private async Task<string> GetIdentifierAsync(Document document, SyntaxNode node)
        {
            if (document == null || node == null)
            {
                return "unknown";
            }

            var semanticModel = await document.GetSemanticModelAsync();
            var symbol = semanticModel.GetDeclaredSymbol(node);
            return symbol?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? "unknown";
        }

        private Metrics GetMetrics(Document document, string identifier)
        {
            if (document == null || string.IsNullOrEmpty(identifier))
            {
                return null;
            }

            var filePath = GetMetricsFilePath(document);
            return GetMetrics(filePath, identifier);
        }

        private Metrics GetMetrics(string metricsFile, string identifier)
        {
            if (File.Exists(metricsFile) == false)
            {
                return new Metrics() { Name = $"{identifier}" };
            }

            try
            {
                using (var fs = File.OpenRead(metricsFile))
                using (var sr = new StreamReader(fs))
                using (var reader = new JsonTextReader(sr))
                {
                    var serializer = new JsonSerializer();

                    // Expects [{Metrics}, ... ]
                    if (reader.Read() == false || reader.TokenType != JsonToken.StartArray)
                    {
                        return new Metrics() { Name = $"{identifier}" };
                    }

                    // Read each object in the array
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonToken.StartObject)
                        {
                            var obj = JObject.Load(reader);

                            var name = (string)obj["Name"];
                            if (string.IsNullOrEmpty(name))
                            {
                                continue;
                            }

                            if (string.Equals(name, identifier, StringComparison.Ordinal))
                            {
                                return obj.ToObject<Metrics>(serializer);
                            }
                        }
                        else if (reader.TokenType == JsonToken.EndArray)
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }

            return new Metrics() { Name = $"{identifier}" };
        }

        private string GetMetricsFilePath(Document document)
        {
            try
            {
                var projectName = document?.Project?.Name ?? string.Empty;
                if (string.IsNullOrEmpty(_metricsDir) || string.IsNullOrEmpty(projectName))
                {
                    return string.Empty;
                }

                return Path.Combine(_metricsDir, $"{projectName}.json");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                return string.Empty;
            }
        }
    }
}
