using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace VisualStudioMetricsOnCodeLens
{
    /// <summary>
    /// Defines a CodeLens data point that provides code metrics information.
    /// Uses named pipe to listen for reload requests from <see cref="SaveCommandHandler"/>.
    /// </summary>
    public class MetricsCodeLensDataPoint : IAsyncCodeLensDataPoint
    {
        // To get VisualStudioWorkspace object from ICodeLensCallbackService
        private readonly ICodeLensCallbackService _codeLensCallbackService;

        // CodeLens data provider to hold metrics data
        private Metrics _metrics = null;

        // Note: IAsyncCodeLensDataPoint interface requires Descriptor property
        public CodeLensDescriptor Descriptor { get; }

        // Note: IAsyncCodeLensDataPoint interface requires InvalidatedAsync property
        public event AsyncEventHandler InvalidatedAsync;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsCodeLensDataPoint"/> class.
        /// </summary>
        /// <remarks>
        /// This constructor starts a named pipe listener in the background to handle communication for the data point.
        /// Ensure that the provided <paramref name="callbackService"/> and <paramref name="descriptor"/> are not null.
        /// </remarks>
        /// <param name="callbackService">The service used to handle callbacks for CodeLens operations.</param>
        /// <param name="descriptor">The descriptor that provides metadata about the CodeLens data point.</param>
        public MetricsCodeLensDataPoint(
            ICodeLensCallbackService callbackService,
            CodeLensDescriptor descriptor)
        {
            _codeLensCallbackService = callbackService;
            Descriptor = descriptor;

            _ = Task.Run(() => StartNamedPipeListener());
        }

        /// <summary>
        /// IAsyncCodeLensDataPoint.GetDataAsync implementation
        /// </summary>
        /// <param name="descriptorContext"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<CodeLensDataPointDescriptor> GetDataAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            try
            {

                _metrics = await _codeLensCallbackService.InvokeAsync<Metrics>(
                    this,
                    nameof(MetricsCodeLensDocumentParser.LoadCodeMetricsAsync),
                    new object[] { Descriptor, descriptorContext },
                    token).ConfigureAwait(false);

                var format = await GetCodeLensDescriptionAsync();
                return new CodeLensDataPointDescriptor
                {
                    Description = _metrics.ToString(format)
                };
            }
            catch (Exception ex)
            {

                Logger.LogError(ex);
                return new CodeLensDataPointDescriptor
                {
                    Description = $"Error"
                };
            }

        }

        /// <summary>
        /// IAsyncCodeLensDataPoint.GetDetailsAsync implementation
        /// </summary>
        /// <param name="descriptorContext"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<CodeLensDetailsDescriptor> GetDetailsAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            return await Task.FromResult<CodeLensDetailsDescriptor>(
                new CodeLensDetailsDescriptor
                {
                    Headers = new List<CodeLensDetailHeaderDescriptor>()
                    {
                        new CodeLensDetailHeaderDescriptor
                        {
                            DisplayName = "Metric",
                            Width = 150
                        },
                        new CodeLensDetailHeaderDescriptor
                        {
                            DisplayName = "Value",
                            Width = 1.0
                        }
                    },
                    Entries = new List<CodeLensDetailEntryDescriptor>()
                    {
                        new CodeLensDetailEntryDescriptor
                        {
                            Fields = new List<CodeLensDetailEntryField>
                            {
                                new CodeLensDetailEntryField(){ Text="Name"},
                                new CodeLensDetailEntryField(){ Text=_metrics.Name }
                            }
                        },
                        new CodeLensDetailEntryDescriptor
                        {
                            Fields = new List<CodeLensDetailEntryField>
                            {
                                new CodeLensDetailEntryField(){ Text="Maintainability Index"},
                                new CodeLensDetailEntryField(){ Text=_metrics.MaintainabilityIndex.ToString() }
                            }
                        },
                        new CodeLensDetailEntryDescriptor
                        {
                            Fields = new List<CodeLensDetailEntryField>
                            {
                                new CodeLensDetailEntryField(){ Text="Cyclomatic Complexity"},
                                new CodeLensDetailEntryField(){ Text=_metrics.CyclomaticComplexity.ToString() }
                            }
                        },
                        new CodeLensDetailEntryDescriptor
                        {
                            Fields = new List<CodeLensDetailEntryField>
                            {
                                new CodeLensDetailEntryField(){ Text="Class Coupling"},
                                new CodeLensDetailEntryField(){ Text=_metrics.ClassCoupling.ToString() }
                            }
                        },
                        new CodeLensDetailEntryDescriptor
                        {
                            Fields = new List<CodeLensDetailEntryField>
                            {
                                new CodeLensDetailEntryField(){ Text="Depth Of Inheritance"},
                                new CodeLensDetailEntryField(){ Text=_metrics.DepthOfInheritance.ToString() }
                            }
                        },
                        new CodeLensDetailEntryDescriptor
                        {
                            Fields = new List<CodeLensDetailEntryField>
                            {
                                new CodeLensDetailEntryField(){ Text="Source Lines"},
                                new CodeLensDetailEntryField(){ Text=_metrics.SourceLines.ToString() }
                            }
                        },
                        new CodeLensDetailEntryDescriptor
                        {
                            Fields = new List<CodeLensDetailEntryField>
                            {
                                new CodeLensDetailEntryField(){ Text="Executable Lines"},
                                new CodeLensDetailEntryField(){ Text=_metrics.ExecutableLines.ToString() }
                            }
                        }
                    }
                });
        }

        private async Task<string> GetCodeLensDescriptionAsync()
        {
            return await _codeLensCallbackService.InvokeAsync<string>(
                this,
                nameof(MetricsCodeLensDocumentParser.GetCodeLensDescriptionAsync),
                null,
                CancellationToken.None).ConfigureAwait(false);
        }

        private void StartNamedPipeListener()
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", PipeServerHost.PipeName, PipeDirection.In))
                {
                    client.Connect();

                    using (var reader = new StreamReader(client))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.StartsWith(PipeServerHost.ReloadToken))
                            {
                                _ = InvalidatedAsync?.InvokeAsync(this, EventArgs.Empty);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }
    }
}