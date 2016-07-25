//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SqlTools.EditorServices.Utility;
using Microsoft.SqlTools.ServiceLayer.Hosting;
using Microsoft.SqlTools.ServiceLayer.Hosting.Protocol;
using Microsoft.SqlTools.ServiceLayer.LanguageServices.Contracts;
using Microsoft.SqlTools.ServiceLayer.SqlContext;
using Microsoft.SqlTools.ServiceLayer.WorkspaceServices;
using Microsoft.SqlTools.ServiceLayer.WorkspaceServices.Contracts;
using System.Linq;

namespace Microsoft.SqlTools.ServiceLayer.LanguageServices
{
    /// <summary>
    /// Main class for Language Service functionality
    /// </summary>
    public sealed class LanguageService
    {

        #region Singleton Instance Implementation

        private static readonly Lazy<LanguageService> instance = new Lazy<LanguageService>(() => new LanguageService());

        public static LanguageService Instance
        {
            get { return instance.Value; }
        }

        /// <summary>
        /// Default, parameterless constructor.
        /// </summary>
        private LanguageService()
        {
        }

        #endregion

        #region Properties

        private static CancellationTokenSource ExistingRequestCancellation { get; set; }

        private SqlToolsSettings CurrentSettings
        {
            get { return WorkspaceService<SqlToolsSettings>.Instance.CurrentSettings; }
        }

        private Workspace CurrentWorkspace
        {
            get { return WorkspaceService<SqlToolsSettings>.Instance.Workspace; }
        }

        /// <summary>
        /// Gets or sets the current SQL Tools context
        /// </summary>
        /// <returns></returns>
        private SqlToolsContext Context { get; set; }

        #endregion

        public void InitializeService(ServiceHost serviceHost, SqlToolsContext context)
        {
            // Register the requests that this service will handle
            serviceHost.SetRequestHandler(DefinitionRequest.Type, HandleDefinitionRequest);
            serviceHost.SetRequestHandler(ReferencesRequest.Type, HandleReferencesRequest);
            serviceHost.SetRequestHandler(CompletionRequest.Type, HandleCompletionRequest);
            serviceHost.SetRequestHandler(CompletionResolveRequest.Type, HandleCompletionResolveRequest);
            serviceHost.SetRequestHandler(SignatureHelpRequest.Type, HandleSignatureHelpRequest);
            serviceHost.SetRequestHandler(DocumentHighlightRequest.Type, HandleDocumentHighlightRequest);
            serviceHost.SetRequestHandler(HoverRequest.Type, HandleHoverRequest);
            serviceHost.SetRequestHandler(DocumentSymbolRequest.Type, HandleDocumentSymbolRequest);
            serviceHost.SetRequestHandler(WorkspaceSymbolRequest.Type, HandleWorkspaceSymbolRequest);

            // Register a no-op shutdown task for validation of the shutdown logic
            serviceHost.RegisterShutdownTask(async (shutdownParams, shutdownRequestContext) =>
            {
                Logger.Write(LogLevel.Verbose, "Shutting down language service");
                await Task.FromResult(0);
            });

            // Register the configuration update handler
            WorkspaceService<SqlToolsSettings>.Instance.RegisterConfigChangeCallback(HandleDidChangeConfigurationNotification);

            // Store the SqlToolsContext for future use
            Context = context;
        }

        #region Request Handlers

        private static async Task HandleDefinitionRequest(
            TextDocumentPosition textDocumentPosition,
            RequestContext<Location[]> requestContext)
        {
            Logger.Write(LogLevel.Verbose, "HandleDefinitionRequest");
            await Task.FromResult(true);
        }

        private static async Task HandleReferencesRequest(
            ReferencesParams referencesParams,
            RequestContext<Location[]> requestContext)
        {
            Logger.Write(LogLevel.Verbose, "HandleReferencesRequest");
            await Task.FromResult(true);
        }

        private static async Task HandleCompletionRequest(
            TextDocumentPosition textDocumentPosition,
            RequestContext<CompletionItem[]> requestContext)
        {
            Logger.Write(LogLevel.Verbose, "HandleCompletionRequest");
            await Task.FromResult(true);
        }

        private static async Task HandleCompletionResolveRequest(
            CompletionItem completionItem,
            RequestContext<CompletionItem> requestContext)
        {
            Logger.Write(LogLevel.Verbose, "HandleCompletionResolveRequest");
            await Task.FromResult(true);
        }

        private static async Task HandleSignatureHelpRequest(
            TextDocumentPosition textDocumentPosition,
            RequestContext<SignatureHelp> requestContext)
        {
            Logger.Write(LogLevel.Verbose, "HandleSignatureHelpRequest");
            await Task.FromResult(true);
        }

        private static async Task HandleDocumentHighlightRequest(
            TextDocumentPosition textDocumentPosition,
            RequestContext<DocumentHighlight[]> requestContext)
        {
            Logger.Write(LogLevel.Verbose, "HandleDocumentHighlightRequest");
            await Task.FromResult(true);
        }

        private static async Task HandleHoverRequest(
            TextDocumentPosition textDocumentPosition,
            RequestContext<Hover> requestContext)
        {
            Logger.Write(LogLevel.Verbose, "HandleHoverRequest");
            await Task.FromResult(true);
        }

        private static async Task HandleDocumentSymbolRequest(
            TextDocumentIdentifier textDocumentIdentifier,
            RequestContext<SymbolInformation[]> requestContext)
        {
            Logger.Write(LogLevel.Verbose, "HandleDocumentSymbolRequest");
            await Task.FromResult(true);
        }

        private static async Task HandleWorkspaceSymbolRequest(
            WorkspaceSymbolParams workspaceSymbolParams,
            RequestContext<SymbolInformation[]> requestContext)
        {
            Logger.Write(LogLevel.Verbose, "HandleWorkspaceSymbolRequest");
            await Task.FromResult(true);
        }

        #endregion

        #region Handlers for Events from Other Services

        public async Task HandleDidChangeConfigurationNotification(
            SqlToolsSettings newSettings, 
            SqlToolsSettings oldSettings, 
            EventContext eventContext)
        {
            // If script analysis settings have changed we need to clear & possibly update the current diagnostic records.
            bool oldScriptAnalysisEnabled = oldSettings.ScriptAnalysis.Enable.HasValue;
            if ((oldScriptAnalysisEnabled != newSettings.ScriptAnalysis.Enable))
            {
                // If the user just turned off script analysis or changed the settings path, send a diagnostics
                // event to clear the analysis markers that they already have.
                if (!newSettings.ScriptAnalysis.Enable.Value)
                {
                    ScriptFileMarker[] emptyAnalysisDiagnostics = new ScriptFileMarker[0];

                    foreach (var scriptFile in WorkspaceService<SqlToolsSettings>.Instance.Workspace.GetOpenedFiles())
                    {
                        await PublishScriptDiagnostics(scriptFile, emptyAnalysisDiagnostics, eventContext);
                    }
                }
                else
                {
                    await this.RunScriptDiagnostics(CurrentWorkspace.GetOpenedFiles(), eventContext);
                }
            }

            // Update the settings in the current 
            CurrentSettings.EnableProfileLoading = newSettings.EnableProfileLoading;
            CurrentSettings.ScriptAnalysis.Update(newSettings.ScriptAnalysis, CurrentWorkspace.WorkspacePath);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Gets a list of semantic diagnostic marks for the provided script file
        /// </summary>
        /// <param name="scriptFile"></param>
        private ScriptFileMarker[] GetSemanticMarkers(ScriptFile scriptFile)
        {
            // the commented out snippet is an example of how to create a error marker
            // semanticMarkers = new ScriptFileMarker[1];
            // semanticMarkers[0] = new ScriptFileMarker()
            // {
            //     Message = "Error message",
            //     Level = ScriptFileMarkerLevel.Error,
            //     ScriptRegion = new ScriptRegion()
            //     {
            //         File = scriptFile.FilePath,
            //         StartLineNumber = 2,
            //         StartColumnNumber = 2,  
            //         StartOffset = 0,
            //         EndLineNumber = 4,
            //         EndColumnNumber = 10,
            //         EndOffset = 0
            //     }
            // };
            return new ScriptFileMarker[0];
        }

        /// <summary>
        /// Runs script diagnostics on changed files
        /// </summary>
        /// <param name="filesToAnalyze"></param>
        /// <param name="eventContext"></param>
        private Task RunScriptDiagnostics(ScriptFile[] filesToAnalyze, EventContext eventContext)
        {
            if (!CurrentSettings.ScriptAnalysis.Enable.Value)
            {
                // If the user has disabled script analysis, skip it entirely
                return Task.FromResult(true);
            }

            // If there's an existing task, attempt to cancel it
            try
            {
                if (ExistingRequestCancellation != null)
                {
                    // Try to cancel the request
                    ExistingRequestCancellation.Cancel();

                    // If cancellation didn't throw an exception,
                    // clean up the existing token
                    ExistingRequestCancellation.Dispose();
                    ExistingRequestCancellation = null;
                }
            }
            catch (Exception e)
            {
                Logger.Write(
                    LogLevel.Error,
                    String.Format(
                        "Exception while cancelling analysis task:\n\n{0}",
                        e.ToString()));

                TaskCompletionSource<bool> cancelTask = new TaskCompletionSource<bool>();
                cancelTask.SetCanceled();
                return cancelTask.Task;
            }

            // Create a fresh cancellation token and then start the task.
            // We create this on a different TaskScheduler so that we
            // don't block the main message loop thread.
            ExistingRequestCancellation = new CancellationTokenSource();
            Task.Factory.StartNew(
                () =>
                    DelayThenInvokeDiagnostics(
                        750,
                        filesToAnalyze,
                        eventContext,
                        ExistingRequestCancellation.Token),
                CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.Default);

            return Task.FromResult(true);
        }

        /// <summary>
        /// Actually run the script diagnostics after waiting for some small delay
        /// </summary>
        /// <param name="delayMilliseconds"></param>
        /// <param name="filesToAnalyze"></param>
        /// <param name="eventContext"></param>
        /// <param name="cancellationToken"></param>
        private async Task DelayThenInvokeDiagnostics(
            int delayMilliseconds,
            ScriptFile[] filesToAnalyze,
            EventContext eventContext,
            CancellationToken cancellationToken)
        {
            // First of all, wait for the desired delay period before
            // analyzing the provided list of files
            try
            {
                await Task.Delay(delayMilliseconds, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // If the task is cancelled, exit directly
                return;
            }

            // If we've made it past the delay period then we don't care
            // about the cancellation token anymore.  This could happen
            // when the user stops typing for long enough that the delay
            // period ends but then starts typing while analysis is going
            // on.  It makes sense to send back the results from the first
            // delay period while the second one is ticking away.

            // Get the requested files
            foreach (ScriptFile scriptFile in filesToAnalyze)
            {
                Logger.Write(LogLevel.Verbose, "Analyzing script file: " + scriptFile.FilePath);
                ScriptFileMarker[] semanticMarkers = GetSemanticMarkers(scriptFile);
                Logger.Write(LogLevel.Verbose, "Analysis complete.");

                await PublishScriptDiagnostics(scriptFile, semanticMarkers, eventContext);
            }
        }

        /// <summary>
        /// Send the diagnostic results back to the host application
        /// </summary>
        /// <param name="scriptFile"></param>
        /// <param name="semanticMarkers"></param>
        /// <param name="eventContext"></param>
        private static async Task PublishScriptDiagnostics(
            ScriptFile scriptFile,
            ScriptFileMarker[] semanticMarkers,
            EventContext eventContext)
        {
            var allMarkers = scriptFile.SyntaxMarkers != null
                    ? scriptFile.SyntaxMarkers.Concat(semanticMarkers)
                    : semanticMarkers;

            // Always send syntax and semantic errors.  We want to 
            // make sure no out-of-date markers are being displayed.
            await eventContext.SendEvent(
                PublishDiagnosticsNotification.Type,
                new PublishDiagnosticsNotification
                {
                    Uri = scriptFile.ClientFilePath,
                    Diagnostics =
                       allMarkers
                            .Select(GetDiagnosticFromMarker)
                            .ToArray()
                });
        }

        /// <summary>
        /// Convert a ScriptFileMarker to a Diagnostic that is Language Service compatible
        /// </summary>
        /// <param name="scriptFileMarker"></param>
        /// <returns></returns>
        private static Diagnostic GetDiagnosticFromMarker(ScriptFileMarker scriptFileMarker)
        {
            return new Diagnostic
            {
                Severity = MapDiagnosticSeverity(scriptFileMarker.Level),
                Message = scriptFileMarker.Message,
                Range = new Range
                {
                    // TODO: What offsets should I use?
                    Start = new Position
                    {
                        Line = scriptFileMarker.ScriptRegion.StartLineNumber - 1,
                        Character = scriptFileMarker.ScriptRegion.StartColumnNumber - 1
                    },
                    End = new Position
                    {
                        Line = scriptFileMarker.ScriptRegion.EndLineNumber - 1,
                        Character = scriptFileMarker.ScriptRegion.EndColumnNumber - 1
                    }
                }
            };
        }

        /// <summary>
        /// Map ScriptFileMarker severity to Diagnostic severity
        /// </summary>
        /// <param name="markerLevel"></param>        
        private static DiagnosticSeverity MapDiagnosticSeverity(ScriptFileMarkerLevel markerLevel)
        {
            switch (markerLevel)
            {
                case ScriptFileMarkerLevel.Error:
                    return DiagnosticSeverity.Error;

                case ScriptFileMarkerLevel.Warning:
                    return DiagnosticSeverity.Warning;

                case ScriptFileMarkerLevel.Information:
                    return DiagnosticSeverity.Information;

                default:
                    return DiagnosticSeverity.Error;
            }
        }

        #endregion
    }
}
