﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EnvDTE;
using EnvDTE80;
using Microsoft.Win32;
using Mono.Cecil;
using VSLangProj;
using Application = System.Windows.Forms.Application;
using Project = EnvDTE.Project;
using Thread = System.Threading.Thread;

namespace Xpand.VSIX.Extensions {

    public static class SolutionExtension {
        public static Boolean BuildSolution(this Solution solution) {
            var dte = DteExtensions.DTE;
            SolutionConfiguration previousSolutionConfiguration = solution.SolutionBuild.ActiveConfiguration;
            var solutionConfiguration = solution.SolutionBuild.SolutionConfigurations.Cast<SolutionConfiguration>().First(configuration => configuration.Name=="EasyTest") ??
                                                          solution.SolutionBuild.SolutionConfigurations.Cast<SolutionConfiguration>().First(configuration => configuration.Name == "Debug");
            solutionConfiguration.Activate();
            try {
                dte.WriteToOutput($@"Build the following configuration: ""{dte.Solution.SolutionBuild.ActiveConfiguration.Name}"".");
                dte.ExecuteCommand("Build.BuildSolution");
                
                while (dte.Solution.SolutionBuild.BuildState != vsBuildState.vsBuildStateDone) {
                    Thread.Sleep(100);
                    Application.DoEvents();
                }
            }
            finally {
                if (previousSolutionConfiguration != null) {
                    dte.WriteToOutput($@"Restore the previous active configuration: ""{previousSolutionConfiguration.Name}"".");

                    previousSolutionConfiguration.Activate();
                }
            }
            dte.WriteToOutput("Get build result.");
            Boolean isBuildSuccess = dte.Solution.SolutionBuild.LastBuildInfo == 0;
            dte.WriteToOutput(!isBuildSuccess ? "Solution build failed." : "BuildSuccess.");
            return isBuildSuccess;
        }

        public static Reference[] GetReferences(this UIHierarchy uiHierarchy, Func<Reference, bool> isFiltered) {
            DteExtensions.DTE.SuppressUI = true;
            var references = ((UIHierarchyItem[]) uiHierarchy.SelectedItems).GetItems<UIHierarchyItem>(item => {
                if (!(item.Object is Reference)) {
                    item.UIHierarchyItems.Expanded = true;
                }
                return item.UIHierarchyItems.Cast<UIHierarchyItem>();
            }).Select(item => item.Object).OfType<Reference>().Where(isFiltered).ToArray();
            DteExtensions.DTE.SuppressUI = false;
            return references;
        }

        public static bool VersionMatch(this AssemblyDefinition assemblyDefinition) {
            var dxVersion = DteExtensions.DTE.Solution.GetDXVersion();
            var dxAssemblies = assemblyDefinition.MainModule.AssemblyReferences.Where(name => name.Name.StartsWith("DevExpress")).ToArray();
            return !dxAssemblies.Any() || dxAssemblies.Any(name => name.Name.Contains(dxVersion));
        }

        public static string GetDXRootDirectory(this Solution solution) {
            var registryKey = Registry.LocalMachine.OpenSubKey(@"Software\WOW6432node\DevExpress\Components\" + solution.GetDXVersion());
            return (string)registryKey?.GetValue("RootDirectory");
        }

        public static Project[] Projects(this Solution solution) {
            var projects = solution.Projects;
            var list = new List<Project>();
            var item = projects.GetEnumerator();
            while (item.MoveNext()) {
                var project = item.Current as Project;
                if (project == null) {
                    continue;
                }

                if (project.Kind == ProjectKinds.vsProjectKindSolutionFolder) {
                    list.AddRange(GetSolutionFolderProjects(project));
                }
                else {
                    list.Add(project);
                }
            }

            return list.Where(project => project.Kind!=Constants.vsProjectKindMisc).ToArray();
        }

        private static IEnumerable<Project> GetSolutionFolderProjects(Project solutionFolder) {
            var list = new List<Project>();
            for (var i = 1; i <= solutionFolder.ProjectItems.Count; i++) {
                var subProject = solutionFolder.ProjectItems.Item(i).SubProject;
                if (subProject == null) {
                    continue;
                }
                if (subProject.Kind == ProjectKinds.vsProjectKindSolutionFolder) {
                    list.AddRange(GetSolutionFolderProjects(subProject));
                }
                else {
                    list.Add(subProject);
                }
            }
            return list;
        }
    

        public static bool HasAuthentication(this Solution solution){
            return solution.Projects().Any(project => project.IsApplicationProject() && project.HasAuthentication());
        }

        public static string GetDXVersion(this Solution solution) {
            return solution.DTE.Solution.Projects()
                .Select(project => project.Object)
                .OfType<VSProject>()
                .SelectMany(project => project.References.Cast<Reference>()).Select(reference => {
                    var matchResults = Regex.Match(reference.Name, @"DevExpress(.*)(v[^.]*\.[.\d])");
                    return matchResults.Success ? matchResults.Groups[2].Value : null;
                }).FirstOrDefault(version => version != null);
        }

        public static Project FindProjectFromUniqueName(this Solution solution, string projectName) {
            return DteExtensions.DTE.Solution.Projects().First(project1 => project1.UniqueName == projectName);
        }

        public static Project FindProject(this Solution solution, string projectName){
            return solution.Projects().FirstOrDefault(project => project.Name == projectName);
        }
        public static void CollapseAllFolders(this Solution solution) {
            var dte = DteExtensions.DTE;
            var uihSolutionExplorer = dte.Windows.Item(Constants.vsext_wk_SProjectWindow).Object as UIHierarchy;
            if (uihSolutionExplorer == null || uihSolutionExplorer.UIHierarchyItems.Count == 0)
                return;
            UIHierarchyItem hierarchyItem = uihSolutionExplorer.UIHierarchyItems.Item(1);
            hierarchyItem.DTE.SuppressUI = true;
            hierarchyItem.Expand(false);
            hierarchyItem.Select(vsUISelectionType.vsUISelectionTypeSelect);
            hierarchyItem.DTE.SuppressUI = false;
        }
        public static Project FindStartUpProject(this Solution solution){
            return ((IEnumerable) solution.SolutionBuild.StartupProjects)?.Cast<string>()
                .Select(s => DteExtensions.DTE.Solution.Projects().First(project => project.UniqueName == s))
                .FirstOrDefault();
        }

        public static void Expand(this UIHierarchyItem item, bool expand) {
            foreach (UIHierarchyItem hierarchyItem in item.UIHierarchyItems) {
                if (hierarchyItem.UIHierarchyItems.Count > 0) {
                    Expand(hierarchyItem, expand);
                    if (hierarchyItem.UIHierarchyItems.Expanded)
                        hierarchyItem.UIHierarchyItems.Expanded = expand;
                }
            }
        }
    }
}