using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.LanguageServices;

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace VisualStudioMetricsOnCodeLens
{
    /// <summary>
    /// https://github.com/tackme31/howmessy/blob/master/Howmessy.VSExtension/WorkspaceExtension.cs
    /// </summary>
    public static class WorkspaceExtension
    {
        private static readonly FieldInfo projectToGuidMapField = typeof(VisualStudioWorkspace).Assembly
            .GetType(
                "Microsoft.VisualStudio.LanguageServices.Implementation.ProjectSystem.VisualStudioWorkspaceImpl",
                throwOnError: true)
            .GetField("_projectToGuidMap", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly MethodInfo getDocumentIdInCurrentContextMethod = typeof(Workspace).GetMethod(
            "GetDocumentIdInCurrentContext",
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(DocumentId) },
            modifiers: null);

        /// <summary>
        /// Retrieves a <see cref="Document"/> from the specified <see cref="VisualStudioWorkspace"/>  based on the file
        /// path and project GUID.
        /// </summary>
        /// <remarks>This method assumes that the workspace may contain multiple projects with the same
        /// file path,  such as in multi-target framework scenarios. It resolves the document by matching the project
        /// GUID  to the corresponding project in the workspace.</remarks>
        /// <param name="workspace">The <see cref="VisualStudioWorkspace"/> to search for the document.</param>
        /// <param name="filePath">The file path of the document to retrieve. This must match the file path in the workspace.</param>
        /// <param name="projGuid">The GUID of the project containing the document. This is used to disambiguate projects with the same file
        /// path.</param>
        /// <returns>The <see cref="Document"/> corresponding to the specified file path and project GUID.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the document cannot be found in the workspace, either because the file path does not exist  or the
        /// project GUID does not match any project in the solution.</exception>
        public static Document GetDocument(this VisualStudioWorkspace workspace, string filePath, Guid projGuid)
        {
            var projectToGuidMap = (ImmutableDictionary<ProjectId, Guid>)projectToGuidMapField.GetValue(workspace);
            var sln = workspace.CurrentSolution;

            var candidateId = sln
                .GetDocumentIdsWithFilePath(filePath)
                // VS will create multiple `ProjectId`s for projects with multiple target frameworks.
                // We simply take the first one we find.
                .FirstOrDefault(cid => projectToGuidMap.GetValueOrDefault(cid.ProjectId) == projGuid)
                ?? throw new InvalidOperationException($"File {filePath} (project: {projGuid}) not found in solution {sln.FilePath}.");

            var currentContextId = workspace.GetDocumentIdInCurrentContext(candidateId);
            return sln.GetDocument(currentContextId)
                ?? throw new InvalidOperationException($"Document {currentContextId} not found in solution {sln.FilePath}.");
        }

        /// <summary>
        /// Retrieves the <see cref="DocumentId"/> associated with the current context in the specified workspace.
        /// </summary>
        /// <param name="workspace">The <see cref="Workspace"/> instance to search for the document context.</param>
        /// <param name="documentId">The <see cref="DocumentId"/> to use as a reference for the current context.</param>
        /// <returns>The <see cref="DocumentId"/> representing the document in the current context, or <c>null</c> if no context
        /// is found.</returns>
        public static DocumentId GetDocumentIdInCurrentContext(this Workspace workspace, DocumentId documentId)
            => (DocumentId)getDocumentIdInCurrentContextMethod.Invoke(workspace, new[] { documentId });
    }
}
