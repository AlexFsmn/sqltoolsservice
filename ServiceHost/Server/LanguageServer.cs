//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using Microsoft.SqlTools.EditorServices.Protocol.LanguageServer;
using Microsoft.SqlTools.EditorServices.Protocol.MessageProtocol;
using Microsoft.SqlTools.EditorServices.Protocol.MessageProtocol.Channel;
using Microsoft.SqlTools.EditorServices.Session;
using System.Threading.Tasks;
using Microsoft.SqlTools.EditorServices.Utility;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Linq;

namespace Microsoft.SqlTools.EditorServices.Protocol.Server
{
    /// <summary>
    /// SQL Tools VS Code Language Server request handler
    /// </summary>
    public class LanguageServer : LanguageServerBase
    {
        private static CancellationTokenSource existingRequestCancellation;
        
        private EditorSession editorSession;

        /// <param name="hostDetails">
        /// Provides details about the host application.
        /// </param>
        public LanguageServer(HostDetails hostDetails, ProfilePaths profilePaths)
            : base(new StdioServerChannel())
        {
            this.editorSession = new EditorSession();
            this.editorSession.StartSession(hostDetails, profilePaths);
        }

        protected override void Initialize()
        {
            // Register all supported message types
            this.SetRequestHandler(InitializeRequest.Type, this.HandleInitializeRequest);
            this.SetEventHandler(DidChangeTextDocumentNotification.Type, this.HandleDidChangeTextDocumentNotification);
            this.SetEventHandler(DidOpenTextDocumentNotification.Type, this.HandleDidOpenTextDocumentNotification);
            this.SetEventHandler(DidCloseTextDocumentNotification.Type, this.HandleDidCloseTextDocumentNotification);
            this.SetEventHandler(DidChangeConfigurationNotification<LanguageServerSettingsWrapper>.Type, this.HandleDidChangeConfigurationNotification);

            this.SetRequestHandler(DefinitionRequest.Type, this.HandleDefinitionRequest);
            this.SetRequestHandler(ReferencesRequest.Type, this.HandleReferencesRequest);
            this.SetRequestHandler(CompletionRequest.Type, this.HandleCompletionRequest);
            this.SetRequestHandler(CompletionResolveRequest.Type, this.HandleCompletionResolveRequest);
            this.SetRequestHandler(SignatureHelpRequest.Type, this.HandleSignatureHelpRequest);
            this.SetRequestHandler(DocumentHighlightRequest.Type, this.HandleDocumentHighlightRequest);
            this.SetRequestHandler(HoverRequest.Type, this.HandleHoverRequest);
            this.SetRequestHandler(DocumentSymbolRequest.Type, this.HandleDocumentSymbolRequest);
            this.SetRequestHandler(WorkspaceSymbolRequest.Type, this.HandleWorkspaceSymbolRequest);               
        }

        protected override async Task Shutdown()
        {
            Logger.Write(LogLevel.Normal, "Language service is shutting down...");

            if (this.editorSession != null)
            {
                this.editorSession.Dispose();
                this.editorSession = null;
            }

            await Task.FromResult(true);
        }

        protected async Task HandleInitializeRequest(
            InitializeRequest initializeParams,
            RequestContext<InitializeResult> requestContext)
        {
            Logger.Write(LogLevel.Normal, "HandleDidChangeTextDocumentNotification");

            // Grab the workspace path from the parameters
           //editorSession.Workspace.WorkspacePath = initializeParams.RootPath;

            await requestContext.SendResult(
                new InitializeResult
                {
                    Capabilities = new ServerCapabilities
                    {
                        TextDocumentSync = TextDocumentSyncKind.Incremental,
                        DefinitionProvider = true,
                        ReferencesProvider = true,
                        DocumentHighlightProvider = true,
                        DocumentSymbolProvider = true,
                        WorkspaceSymbolProvider = true,
                        HoverProvider = true,
                        CompletionProvider = new CompletionOptions
                        {
                            ResolveProvider = true,
                            TriggerCharacters = new string[] { ".", "-", ":", "\\" }
                        },
                        SignatureHelpProvider = new SignatureHelpOptions
                        {
                            TriggerCharacters = new string[] { " " } // TODO: Other characters here?
                        }
                    }
                });
        }

        /// <summary>
        /// Handles text document change events
        /// </summary>
        /// <param name="textChangeParams"></param>
        /// <param name="eventContext"></param>
        /// <returns></returns>
        protected Task HandleDidChangeTextDocumentNotification(
            DidChangeTextDocumentParams textChangeParams,
            EventContext eventContext)
        {
            StringBuilder msg = new StringBuilder();
            msg.Append("HandleDidChangeTextDocumentNotification"); 
            List<ScriptFile> changedFiles = new List<ScriptFile>();

            // A text change notification can batch multiple change requests
            foreach (var textChange in textChangeParams.ContentChanges)
            {
                string fileUri = textChangeParams.TextDocument.Uri;
                msg.AppendLine();
                msg.Append("  File: ");
                msg.Append(fileUri);

                ScriptFile changedFile = editorSession.Workspace.GetFile(fileUri);

                changedFile.ApplyChange(
                    GetFileChangeDetails(
                        textChange.Range.Value,
                        textChange.Text));

                changedFiles.Add(changedFile);
            }

            Logger.Write(LogLevel.Normal, msg.ToString());

            this.RunScriptDiagnostics(
                changedFiles.ToArray(),
                editorSession,
                eventContext);

            return Task.FromResult(true);
        }

        protected Task HandleDidOpenTextDocumentNotification(
            DidOpenTextDocumentNotification openParams,
            EventContext eventContext)
        {
            Logger.Write(LogLevel.Normal, "HandleDidOpenTextDocumentNotification");
            return Task.FromResult(true);
        }

         protected Task HandleDidCloseTextDocumentNotification(
            TextDocumentIdentifier closeParams,
            EventContext eventContext)
        {
            Logger.Write(LogLevel.Normal, "HandleDidCloseTextDocumentNotification");
            return Task.FromResult(true);
        }

        protected async Task HandleDidChangeConfigurationNotification(
            DidChangeConfigurationParams<LanguageServerSettingsWrapper> configChangeParams,
            EventContext eventContext)
        {
            Logger.Write(LogLevel.Normal, "HandleDidChangeConfigurationNotification");
            await Task.FromResult(true);
        }

        protected async Task HandleDefinitionRequest(
            TextDocumentPosition textDocumentPosition,
            RequestContext<Location[]> requestContext)
        {
            Logger.Write(LogLevel.Normal, "HandleDefinitionRequest");
            await Task.FromResult(true);
        }

        protected async Task HandleReferencesRequest(
            ReferencesParams referencesParams,
            RequestContext<Location[]> requestContext)
        {
            Logger.Write(LogLevel.Normal, "HandleReferencesRequest");
            await Task.FromResult(true);
        }

        protected async Task HandleCompletionRequest(
            TextDocumentPosition textDocumentPosition,
            RequestContext<CompletionItem[]> requestContext)
        {
            Logger.Write(LogLevel.Normal, "HandleCompletionRequest");
            await Task.FromResult(true);
        }

        protected async Task HandleCompletionResolveRequest(
            CompletionItem completionItem,
            RequestContext<CompletionItem> requestContext)
        {
            Logger.Write(LogLevel.Normal, "HandleCompletionResolveRequest");
            await Task.FromResult(true);
        }

        protected async Task HandleSignatureHelpRequest(
            TextDocumentPosition textDocumentPosition,
            RequestContext<SignatureHelp> requestContext)
        {
            Logger.Write(LogLevel.Normal, "HandleSignatureHelpRequest");
            await Task.FromResult(true);
        }

        protected async Task HandleDocumentHighlightRequest(
            TextDocumentPosition textDocumentPosition,
            RequestContext<DocumentHighlight[]> requestContext)
        {
            Logger.Write(LogLevel.Normal, "HandleDocumentHighlightRequest");
            await Task.FromResult(true);
        }

        protected async Task HandleHoverRequest(
            TextDocumentPosition textDocumentPosition,
            RequestContext<Hover> requestContext)
        {
            Logger.Write(LogLevel.Normal, "HandleHoverRequest");
            await Task.FromResult(true);
        }

        protected async Task HandleDocumentSymbolRequest(
            TextDocumentIdentifier textDocumentIdentifier,
            RequestContext<SymbolInformation[]> requestContext)
        {
            Logger.Write(LogLevel.Normal, "HandleDocumentSymbolRequest");
            await Task.FromResult(true);     
        }

        protected async Task HandleWorkspaceSymbolRequest(
            WorkspaceSymbolParams workspaceSymbolParams,
            RequestContext<SymbolInformation[]> requestContext)
        {
            Logger.Write(LogLevel.Normal, "HandleWorkspaceSymbolRequest");
            await Task.FromResult(true);
        }

        private Task RunScriptDiagnostics(
            ScriptFile[] filesToAnalyze,
            EditorSession editorSession,
            EventContext eventContext)
        {
            // if (!this.currentSettings.ScriptAnalysis.Enable.Value)
            // {
            //     // If the user has disabled script analysis, skip it entirely
            //     return Task.FromResult(true);
            // }

            // // If there's an existing task, attempt to cancel it
            // try
            // {
            //     if (existingRequestCancellation != null)
            //     {
            //         // Try to cancel the request
            //         existingRequestCancellation.Cancel();

            //         // If cancellation didn't throw an exception,
            //         // clean up the existing token
            //         existingRequestCancellation.Dispose();
            //         existingRequestCancellation = null;
            //     }
            // }
            // catch (Exception e)
            // {
            //     // TODO: Catch a more specific exception!
            //     Logger.Write(
            //         LogLevel.Error,
            //         string.Format(
            //             "Exception while cancelling analysis task:\n\n{0}",
            //             e.ToString()));

            //     TaskCompletionSource<bool> cancelTask = new TaskCompletionSource<bool>();
            //     cancelTask.SetCanceled();
            //     return cancelTask.Task;
            // }

            // Create a fresh cancellation token and then start the task.
            // We create this on a different TaskScheduler so that we
            // don't block the main message loop thread.
            // TODO: Is there a better way to do this?
            existingRequestCancellation = new CancellationTokenSource();
            Task.Factory.StartNew(
                () =>
                    DelayThenInvokeDiagnostics(
                        750,
                        filesToAnalyze,
                        editorSession,
                        eventContext,
                        existingRequestCancellation.Token),
                CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.Default);

            return Task.FromResult(true);
        }


        private static async Task DelayThenInvokeDiagnostics(
            int delayMilliseconds,
            ScriptFile[] filesToAnalyze,
            EditorSession editorSession,
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
                ScriptFileMarker[] semanticMarkers = null;
                // if (editorSession.AnalysisService != null)
                // {
                //     Logger.Write(LogLevel.Verbose, "Analyzing script file: " + scriptFile.FilePath);

                //     semanticMarkers =
                //         editorSession.AnalysisService.GetSemanticMarkers(
                //             scriptFile);

                //     Logger.Write(LogLevel.Verbose, "Analysis complete.");
                // }
                // else
                {
                    // Semantic markers aren't available if the AnalysisService
                    // isn't available
                    semanticMarkers = new ScriptFileMarker[0];
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
                }

                await PublishScriptDiagnostics(
                    scriptFile,
                    semanticMarkers,
                    eventContext);
            }
        }

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

        private static FileChange GetFileChangeDetails(Range changeRange, string insertString)
        {
            // The protocol's positions are zero-based so add 1 to all offsets

            return new FileChange
            {
                InsertString = insertString,
                Line = changeRange.Start.Line + 1,
                Offset = changeRange.Start.Character + 1,
                EndLine = changeRange.End.Line + 1,
                EndOffset = changeRange.End.Character + 1
            };
        }
    }
}
