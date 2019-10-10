﻿using System;
using System.IO;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;

namespace Raml.Common
{
    public class VisualStudioAutomationHelper
    {
        public const string RamlVsToolsActivityLogSource = "RamlVsTools";

        public static Project GetActiveProject(_DTE dte)
        {
            Project activeProject = null;

            var activeSolutionProjects = dte.ActiveSolutionProjects as Array;
            if (activeSolutionProjects != null && activeSolutionProjects.Length > 0)
            {
                activeProject = activeSolutionProjects.GetValue(0) as Project;
            }

            return activeProject;
        }

        public static string GetDefaultNamespace(IServiceProvider serviceProvider)
        {
            var dte = serviceProvider.GetService(typeof(SDTE)) as DTE;
            var project = GetActiveProject(dte);

            var namespaceProperty = IsJsonOrXProj(project) ?  "RootNamespace" : "DefaultNamespace";
            return project.Properties.Item(namespaceProperty).Value.ToString();
        }

        public static string GetExceptionInfo(Exception ex)
        {
            return ex.Message + Environment.NewLine + ex.StackTrace +
                   (ex.InnerException != null
                       ? Environment.NewLine + ex.InnerException.Message + Environment.NewLine +
                         ex.InnerException.StackTrace
                       : string.Empty);
        }

        public static ProjectItem AddFolderIfNotExists(Project proj, string folderName)
        {
            var path = Path.GetDirectoryName(proj.FullName) + "\\" + folderName + "\\";
            var projectItem = proj.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == folderName);
            if (projectItem != null)
                return projectItem;

            if (!IsJsonOrXProj(proj) && Directory.Exists(path))
                projectItem = proj.ProjectItems.AddFromDirectory(path);
            else if(!Directory.Exists(path))
                projectItem = proj.ProjectItems.AddFolder(folderName);

            return projectItem;
        }

        public static ProjectItem AddFolderIfNotExists(ProjectItem projItem, string folderName)
        {
            var path = Path.GetDirectoryName(projItem.FileNames[0]) + "\\" + folderName + "\\";
            var projectItem = projItem.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == folderName);
            if (projectItem != null)
                return projectItem;

            if (!IsJsonOrXProj(projItem.ContainingProject) && Directory.Exists(path))
                projectItem = projItem.ProjectItems.AddFromDirectory(path);
            else if (!Directory.Exists(path))
                projectItem = projItem.ProjectItems.AddFolder(folderName);

            return projectItem;
        }


        public static ProjectItem AddFolderIfNotExists(ProjectItem projItem, string folderName, string folderPath)
        {
            
            var projectItem = projItem.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == folderName);
            if (projectItem != null)
                return projectItem;

            if (Directory.Exists(folderPath))
                projectItem = projItem.ProjectItems.AddFromDirectory(folderPath);
            else
                projectItem = projItem.ProjectItems.AddFolder(folderName);

            return projectItem;
        }

        public static bool IsANetCoreProject(Project proj)
        {
            if (proj.FileName.EndsWith("xproj") || proj.FileName.EndsWith("json"))
                return true;

            //.NETCoreApp,Version = v2.0
            //.NETCoreApp,Version = v1.1
            //.NETFramework,Version = v4.6.1
            Property targetFwkObj;
            try
            {
                targetFwkObj = proj.Properties.Item("TargetFrameworkMoniker"); //var targetFwkObj = proj.Properties.Item("TargetFramework");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Cannot determine .net framework. TargetFrameworkMoniker not present.", ex);
            }
            var targetFramework = (targetFwkObj.Value as string).ToLowerInvariant();
            if (targetFramework.Contains("netcore") || targetFramework.Contains("netstandard"))
                return true;

            if (targetFramework.Contains("netframework"))
                return false;

            throw new InvalidOperationException("Cannot determine .net framework. " + targetFramework);
        }

        public static bool IsJsonOrXProj(Project proj)
        {
            if (proj.FileName.EndsWith("xproj") || proj.FileName.EndsWith("json"))
                return true;

            return false;
        }


    }
}