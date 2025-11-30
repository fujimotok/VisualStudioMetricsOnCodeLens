using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using VisualStudioMetricsOnCodeLens.Properties;

namespace VisualStudioMetricsOnCodeLens
{
    /// <summary>
    /// CodeLens entry point.
    /// Dependency injection refer <see cref="MetricsCodeLensDocumentParser"/>
    /// </summary>
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name("MetricsCodeLensProvider")]
    [LocalizedName(typeof(Resources), "MetricsCodeLensProvider")]
    [ContentType("CSharp")]
    [Priority(200)]
    public class MetricsCodeLensProvider : IAsyncCodeLensDataPointProvider
    {
        private readonly Lazy<ICodeLensCallbackService> _callbackService;

        /// <summary>
        /// CodeLens Constructor
        /// </summary>
        /// <param name="callbackService"></param>
        [ImportingConstructor]
        public MetricsCodeLensProvider(Lazy<ICodeLensCallbackService> callbackService)
        {
            _callbackService = callbackService;
        }

        /// <summary>
        /// IAsyncCodeLensDataPointProvider.CanCreateDataPointAsync implementation
        /// </summary>
        /// <param name="descriptor"></param>
        /// <param name="descriptorContext"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public Task<bool> CanCreateDataPointAsync(
            CodeLensDescriptor descriptor, 
            CodeLensDescriptorContext descriptorContext,
            CancellationToken token)
        {
            return Task.FromResult<bool>(
                descriptor.Kind is CodeElementKinds.Method
                || descriptor.Kind is CodeElementKinds.Property
                || descriptor.Kind is CodeElementKinds.Type
                );
        }

        /// <summary>
        /// IAsyncCodeLensDataPointProvider.CreateDataPointAsync implementation
        /// </summary>
        /// <param name="descriptor"></param>
        /// <param name="descriptorContext"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public Task<IAsyncCodeLensDataPoint> CreateDataPointAsync(
            CodeLensDescriptor descriptor,
            CodeLensDescriptorContext descriptorContext,
            CancellationToken token)
        {
            return Task.FromResult<IAsyncCodeLensDataPoint>(
                new MetricsCodeLensDataPoint(_callbackService.Value, descriptor));
        }
    }
}
