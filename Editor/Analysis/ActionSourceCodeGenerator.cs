using UnityEngine;
using Yarn.Unity;
using UnityEditor;
using System.Linq;

namespace Yarn.Unity.Editor
{
    public static class ActionSourceCodeGenerator
    {

        /// <summary>
        /// Get a path in the current project that can be used for storing
        /// manually-generated Yarn Action registration code.
        /// </summary>
        /// <remarks>
        /// This property checks to see if a file exists in the Assets folder
        /// that is both named "YarnActionRegistration.cs", and contains a
        /// marker indicating that it was generated by Yarn Spinner's code
        /// generation systems. If this is found, the path to the file is
        /// returned. Otherwise, the path
        /// <c>Assets/YarnActionRegistration.cs</c> is returned.
        /// </remarks>
        public static string GeneratedSourcePath
        {
            get
            {
                const string YarnRegistrationFileName = "YarnActionRegistration.cs";
                const string DefaultOutputFilePath = "Assets/" + YarnRegistrationFileName;

                // Note the lack of a closing parenthesis in this string - we
                // only want to check to see if it was generated by
                // "YarnActionAnalyzer", not any specific version of that
                // analyzer
                const string YarnGeneratedCodeSignature = "GeneratedCode(\"YarnActionAnalyzer\"";

                var existingFile = System.IO.Directory.EnumerateFiles(System.Environment.CurrentDirectory, YarnRegistrationFileName, System.IO.SearchOption.AllDirectories).FirstOrDefault();

                if (existingFile == null)
                {
                    return DefaultOutputFilePath;
                }
                else
                {
                    try
                    {
                        var text = System.IO.File.ReadAllText(existingFile);
                        return text.Contains(YarnGeneratedCodeSignature) 
                            ? existingFile
                            : DefaultOutputFilePath;
                    }
                    catch (System.Exception e)
                    {
                        // Something happened while checking the file. Return
                        // our default, and log that we encountered a problem.
                        Debug.LogWarning($"Can't check to see if {existingFile} is a valid action registration script, using {DefaultOutputFilePath} instead: {e}");
                        return DefaultOutputFilePath;
                    }
                }

            }
        }

        /// <summary>
        /// Generates and imports a C# source code file in the project
        /// containing Yarn Action registration code at the path indicated by
        /// <see cref="GeneratedSourcePath"/>.
        /// </summary>
        /// <remarks>
        /// This method should not be called in projects where Unity has support
        /// for source generators (i.e. Unity 2021.2 and later).
        /// </remarks>
        public static void GenerateYarnActionSourceCode()
        {
            var analysis = new Yarn.Unity.ActionAnalyser.Analyser("Assets");
            try
            {
                var assemblyPaths = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Player).Select(assembly => assembly.outputPath);
                
                var actions = analysis.GetActions(assemblyPaths);
                var source = Yarn.Unity.ActionAnalyser.Analyser.GenerateRegistrationFileSource(actions);

                var path = GeneratedSourcePath;

                System.IO.File.WriteAllText(path, source);
                UnityEditor.AssetDatabase.ImportAsset(path);

                Debug.Log($"Generated Yarn command and function registration code at {path}");
            }
            catch (Yarn.Unity.ActionAnalyser.AnalyserException e)
            {
                Debug.LogError($"Error generating source code: " + e.InnerException.ToString());
            }
        }

    }
}
