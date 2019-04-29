﻿using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;
using Microsoft;
using EnvDTE80;
using System.Linq;

namespace NetPrintsVSIX
{
    [ProvideEditorExtension(typeof(NetPrintsEditorFactory), ".netpp", 32)]
    public class NetPrintsEditorFactory: IVsEditorFactory, IDisposable
    {
        private const string BinaryPathEnvVar = "NETPRINTSEDITOR";

        public int CreateEditorInstance(uint grfCreateDoc, string pszMkDocument, string pszPhysicalView, IVsHierarchy pvHier,
                                        uint itemid, IntPtr punkDocDataExisting, out IntPtr ppunkDocView, out IntPtr ppunkDocData,
                                        out string pbstrEditorCaption, out Guid pguidCmdUI, out int pgrfCDW)
        {
            ppunkDocView = IntPtr.Zero;
            ppunkDocData = IntPtr.Zero;
            pbstrEditorCaption = "";
            pguidCmdUI = Guid.Empty;
            pgrfCDW = 0;

            System.Diagnostics.Process.Start(Environment.GetEnvironmentVariable(BinaryPathEnvVar), pszMkDocument);

            return VSConstants.S_OK;
        }

        public int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider psp) => VSConstants.S_OK;

        public int Close() => VSConstants.S_OK;

        public int MapLogicalView(ref Guid rguidLogicalView, out string pbstrPhysicalView)
        {
            pbstrPhysicalView = "";
            return VSConstants.S_OK;
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(NetPrintsVSIXPackage.PackageGuidString)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideEditorFactory(typeof(NetPrintsEditorFactory), 106)]
    public sealed class NetPrintsVSIXPackage : AsyncPackage, IVsUpdateSolutionEvents, IVsSolutionEvents
    {
        /// <summary>
        /// NetPrintsVSIXPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "b5e5dd1f-f24f-44dd-b0ed-bbcce219af0c";

        private uint solutionCookie;
        private IVsSolution solution;

        private IVsSolutionBuildManager2 solutionBuildManager;
        private uint solutionBuildManagerCookie;

        private DTE2 dte;

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            solution = (IVsSolution)(await GetServiceAsync(typeof(SVsSolution)));
            solution.AdviseSolutionEvents(this, out solutionCookie);

            solutionBuildManager = (IVsSolutionBuildManager2)ServiceProvider.GlobalProvider.GetService(typeof(SVsSolutionBuildManager));
            Assumes.Present(solutionBuildManager);
            solutionBuildManager.AdviseUpdateSolutionEvents(this, out solutionBuildManagerCookie);

            dte = await ServiceProvider.GetGlobalServiceAsync(typeof(SDTE)) as DTE2;
            var solutionEvents = dte.Events.SolutionEvents;

            RegisterEditorFactory(new NetPrintsEditorFactory());
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            ThreadHelper.ThrowIfNotOnUIThread();
            solution?.UnadviseSolutionEvents(solutionCookie);
            solutionBuildManager?.UnadviseUpdateSolutionEvents(solutionBuildManagerCookie);
        }

        public int OnBeforeActiveSolutionCfgChange(IVsCfg pOldActiveSlnCfg, IVsCfg pNewActiveSlnCfg) => VSConstants.S_OK;

        public int OnAfterActiveSolutionCfgChange(IVsCfg pOldActiveSlnCfg, IVsCfg pNewActiveSlnCfg) => VSConstants.S_OK;

        public int UpdateSolution_Begin(ref int pfCancelUpdate)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            foreach (var project in dte.Solution.Projects.OfType<EnvDTE.Project>())
            {
                foreach (var projectItem in project.ProjectItems.OfType<EnvDTE.ProjectItem>())
                {
                    string fullPath = projectItem.Properties.Item("FullPath")?.Value as string;
                    if (fullPath != null && fullPath.EndsWith(".netpp"))
                    {
                        try
                        {
                            NetPrints.Core.Project netPrintsProject = NetPrints.Core.Project.LoadFromPath(fullPath);
                            netPrintsProject.CompileProject();
                        }
                        catch
                        {
                            pfCancelUpdate = 1;
                            return VSConstants.E_FAIL;
                        }
                    }
                }
            }

            return VSConstants.S_OK;
        }

        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded) => VSConstants.S_OK;

        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved) => VSConstants.S_OK;

        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy) => VSConstants.S_OK;

        public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) => VSConstants.S_OK;

        public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy) => VSConstants.S_OK;

        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution) => VSConstants.S_OK;

        public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) => VSConstants.S_OK;

        public int OnBeforeCloseSolution(object pUnkReserved) => VSConstants.S_OK;

        public int OnAfterCloseSolution(object pUnkReserved) => VSConstants.S_OK;

        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) => VSConstants.S_OK;

        public int UpdateSolution_Done(int fSucceeded, int fModified, int fCancelCommand) => VSConstants.S_OK;

        public int UpdateSolution_StartUpdate(ref int pfCancelUpdate) => VSConstants.S_OK;

        public int UpdateSolution_Cancel() => VSConstants.S_OK;

        public int OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy) => VSConstants.S_OK;
    }
}
