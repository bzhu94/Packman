﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Packman;

namespace PackmanVsix
{
    public static class ProjectHelpers
    {
        static DTE2 _dte = VSPackage.DTE;

        public static string GetConfigFile(this Project project)
        {
            string folder = project.GetRootFolder();

            if (string.IsNullOrEmpty(folder))
                return null;

            return Path.Combine(folder, Defaults.ManifestFileName);
        }

        public static void CheckFileOutOfSourceControl(string file)
        {
            if (!File.Exists(file) || _dte.Solution.FindProjectItem(file) == null)
                return;

            if (_dte.SourceControl.IsItemUnderSCC(file) && !_dte.SourceControl.IsItemCheckedOut(file))
                _dte.SourceControl.CheckOutItem(file);

            var info = new FileInfo(file);
            info.IsReadOnly = false;
        }

        public static string GetFullPath(this ProjectItem item)
        {
            return item.Properties?.Item("FullPath")?.Value.ToString();
        }

        public static IEnumerable<ProjectItem> GetSelectedItems()
        {
            var items = (Array)_dte.ToolWindows.SolutionExplorer.SelectedItems;

            foreach (UIHierarchyItem selItem in items)
            {
                var item = selItem.Object as ProjectItem;

                if (item != null)
                    yield return item;
            }
        }

        public static IEnumerable<string> GetSelectedItemPaths()
        {
            foreach (ProjectItem item in GetSelectedItems())
            {
                if (item != null && item.Properties != null)
                    yield return item.Properties.Item("FullPath").Value.ToString();
            }
        }

        public static string GetRootFolder(this Project project)
        {
            if (project == null || string.IsNullOrEmpty(project.FullName))
                return null;

            string fullPath;

            try
            {
                fullPath = project.Properties.Item("FullPath").Value as string;
            }
            catch (ArgumentException)
            {
                try
                {
                    // MFC projects don't have FullPath, and there seems to be no way to query existence
                    fullPath = project.Properties.Item("ProjectDirectory").Value as string;
                }
                catch (ArgumentException)
                {
                    // Installer projects have a ProjectPath.
                    fullPath = project.Properties.Item("ProjectPath").Value as string;
                }
            }

            if (string.IsNullOrEmpty(fullPath))
                return File.Exists(project.FullName) ? Path.GetDirectoryName(project.FullName) : null;

            if (Directory.Exists(fullPath))
                return fullPath;

            if (File.Exists(fullPath))
                return Path.GetDirectoryName(fullPath);

            return null;
        }

        public static async Task AddFileToProjectAsync(this Project project, string file, string itemType = null)
        {
            if (project.IsKind(ProjectTypes.ASPNET_5))
                return;

            await Task.Run(() =>
            {
                //if (_dte.Solution.FindProjectItem(file) == null)
                //{
                ProjectItem item = project.ProjectItems.AddFromFile(file);
                item.SetItemType(itemType);
                //}
            });
        }

        public static void SetItemType(this ProjectItem item, string itemType)
        {
            try
            {
                if (item == null || item.ContainingProject == null)
                    return;

                if (string.IsNullOrEmpty(itemType)
                    || item.ContainingProject.IsKind(ProjectTypes.WEBSITE_PROJECT)
                    || item.ContainingProject.IsKind(ProjectTypes.UNIVERSAL_APP))
                    return;

                item.Properties.Item("ItemType").Value = itemType;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public static async Task AddNestedFileAsync(string parentFile, string newFile, string itemType = null)
        {
            ProjectItem item = _dte.Solution.FindProjectItem(parentFile);

            try
            {
                if (item == null
                    || item.ContainingProject == null
                    || item.ContainingProject.IsKind(ProjectTypes.ASPNET_5))
                    return;

                if (item.ProjectItems == null || item.ContainingProject.IsKind(ProjectTypes.UNIVERSAL_APP))
                {
                    await item.ContainingProject.AddFileToProjectAsync(newFile);
                }
                else if (_dte.Solution.FindProjectItem(newFile) == null)
                {
                    item.ProjectItems.AddFromFile(newFile);
                }

                ProjectItem newItem = _dte.Solution.FindProjectItem(newFile);
                newItem.SetItemType(itemType);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public static bool IsKind(this Project project, string kindGuid)
        {
            return project.Kind.Equals(kindGuid, StringComparison.OrdinalIgnoreCase);
        }

        public static void DeleteFileFromProject(string file)
        {
            ProjectItem item = _dte.Solution.FindProjectItem(file);

            if (item == null)
                return;
            try
            {
                item.Delete();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public static IEnumerable<Project> GetAllProjects()
        {
            return _dte.Solution.Projects
                  .Cast<Project>()
                  .SelectMany(GetChildProjects)
                  .Union(_dte.Solution.Projects.Cast<Project>())
                  .Where(p => { try { return !string.IsNullOrEmpty(p.FullName); } catch { return false; } });
        }

        private static IEnumerable<Project> GetChildProjects(Project parent)
        {
            try
            {
                if (!parent.IsKind(ProjectKinds.vsProjectKindSolutionFolder) && parent.Collection == null)  // Unloaded
                    return Enumerable.Empty<Project>();

                if (!string.IsNullOrEmpty(parent.FullName))
                    return new[] { parent };
            }
            catch (COMException)
            {
                return Enumerable.Empty<Project>();
            }

            return parent.ProjectItems
                    .Cast<ProjectItem>()
                    .Where(p => p.SubProject != null)
                    .SelectMany(p => GetChildProjects(p.SubProject));
        }

        public static bool IsSolutionLoaded()
        {
            if (_dte.Solution == null)
                return false;

            return GetAllProjects().Any();
        }

        public static Project GetActiveProject()
        {
            try
            {
                var activeSolutionProjects = _dte.ActiveSolutionProjects as Array;

                if (activeSolutionProjects != null && activeSolutionProjects.Length > 0)
                    return activeSolutionProjects.GetValue(0) as Project;

                var doc = _dte.ActiveDocument;

                if (doc != null && !string.IsNullOrEmpty(doc.FullName))
                {
                    var item = _dte.Solution?.FindProjectItem(doc.FullName);

                    if (item != null)
                        return item.ContainingProject;
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Error getting the active project" + ex);
            }

            return null;
        }

        public static bool IsConfigFile(this ProjectItem item)
        {
            if (item == null || item.Properties == null || !item.IsKind(Constants.vsProjectItemKindPhysicalFile) || item.ContainingProject == null)
                return false;

            string fileName = Path.GetFileName(item.GetFullPath());

            return fileName.Equals(Defaults.ManifestFileName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsKind(this ProjectItem item, string kindGuid)
        {
            if (item == null)
                return false;

            return item.Kind.Equals(kindGuid, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static class ProjectTypes
    {
        public const string ASPNET_5 = "{8BB2217D-0F2D-49D1-97BC-3654ED321F3B}";
        public const string WEBSITE_PROJECT = "{E24C65DC-7377-472B-9ABA-BC803B73C61A}";
        public const string UNIVERSAL_APP = "{262852C6-CD72-467D-83FE-5EEB1973A190}";
        public const string NODE_JS = "{9092AA53-FB77-4645-B42D-1CCCA6BD08BD}";
    }
}
