using System;
using System.IO;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using MuleSoft.RAML.Tools.Properties;
using Raml.Common;
using Microsoft.VisualStudio.ComponentModelHost;
using NuGet.VisualStudio;

namespace MuleSoft.RAML.Tools
{
    public class RamlScaffoldServiceAspNetCore : RamlScaffoldServiceBase
    {
        private readonly string newtonsoftJsonForCorePackageVersion = Settings.Default.NewtonsoftJsonForCorePackageVersion;

        public RamlScaffoldServiceAspNetCore(IT4Service t4Service, IServiceProvider serviceProvider): base(t4Service, serviceProvider){}

        public override string TemplateSubFolder
        {
            get { return "AspNet5"; }
        }

        public override void AddContract(RamlChooserActionParams parameters)
        {
            var dte = ServiceProvider.GetService(typeof(SDTE)) as DTE;
            var proj = VisualStudioAutomationHelper.GetActiveProject(dte);

            InstallDependencies(proj, newtonsoftJsonForCorePackageVersion);

            var folderItem = VisualStudioAutomationHelper.AddFolderIfNotExists(proj, ContractsFolderName);
            var contractsFolderPath = Path.GetDirectoryName(proj.FullName) + Path.DirectorySeparatorChar + ContractsFolderName + Path.DirectorySeparatorChar;

            var targetFolderPath = GetTargetFolderPath(contractsFolderPath, parameters.TargetFileName);
            if (!Directory.Exists(targetFolderPath))
                Directory.CreateDirectory(targetFolderPath);

            if (string.IsNullOrWhiteSpace(parameters.RamlSource) && !string.IsNullOrWhiteSpace(parameters.RamlTitle))
            {
                AddEmptyContract(folderItem, contractsFolderPath, parameters);
            }
            else
            {
                AddContractFromFile(folderItem, contractsFolderPath, parameters);
            }
        }

        private void InstallDependencies(Project proj, string newtonsoftJsonForCorePackageVersion)
        {
            var componentModel = (IComponentModel)ServiceProvider.GetService(typeof(SComponentModel));
            var installerServices = componentModel.GetService<IVsPackageInstallerServices>();
            var installer = componentModel.GetService<IVsPackageInstaller>();

            var packs = installerServices.GetInstalledPackages(proj).ToArray();

            InstallNugetDependencies(proj, newtonsoftJsonForCorePackageVersion);

            // RAML.NetCore.APICore
            var ramlNetCoreApiCorePackageId = "RAML.NetCore.APICore";
            var ramlNetCoreApiCorePackageVersion = "0.0.1";
            if (!installerServices.IsPackageInstalled(proj, ramlNetCoreApiCorePackageId))
            {
                installer.InstallPackage(nugetPackagesSource, proj, ramlNetCoreApiCorePackageId, ramlNetCoreApiCorePackageVersion, false);
            }
        }

        protected override string GetTargetFolderPath(string folderPath, string targetFilename)
        {
            return folderPath + Path.GetFileNameWithoutExtension(targetFilename) + Path.DirectorySeparatorChar;
        }

        protected override void ManageIncludes(ProjectItem folderItem, RamlIncludesManagerResult result)
        {
        }
    }
}