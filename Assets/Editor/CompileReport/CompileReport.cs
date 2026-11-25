using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace SoulRequiem.EditorTools
{
    /// <summary>
    /// Writes the result of EVERY Unity script compilation to `_tools/compile_report.txt` (next to the Assets folder):
    /// first line = OK / FAILED + time, then each error (file:line: message) and each warning in the project's own code.
    /// Lets someone else (or Claude) check whether the code compiles without opening the Console.
    /// Own assembly with no reference to game code -> it still runs and writes the report when game code fails to compile.
    /// </summary>
    [InitializeOnLoad]
    internal static class CompileReport
    {
        private static readonly StringBuilder Errors = new StringBuilder();
        private static readonly StringBuilder Warnings = new StringBuilder();
        private static int errorCount, warningCount;

        private static string ReportPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../_tools/compile_report.txt"));

        static CompileReport()
        {
            CompilationPipeline.compilationStarted += _ => { Errors.Clear(); Warnings.Clear(); errorCount = warningCount = 0; };
            CompilationPipeline.assemblyCompilationFinished += OnAssembly;
            CompilationPipeline.compilationFinished += _ => Write();
        }

        private static void OnAssembly(string assembly, CompilerMessage[] messages)
        {
            foreach (var m in messages)
            {
                string line = $"{m.file}:{m.line}: {m.message}";
                if (m.type == CompilerMessageType.Error) { errorCount++; Errors.AppendLine(line); }
                else if (m.file != null && m.file.Replace('\\', '/').Contains("Assets/") && !m.file.Contains("GamePackage"))
                {
                    warningCount++;                                           // only warnings in the project's own code (bought packages skipped)
                    Warnings.AppendLine(line);
                }
            }
        }

        private static void Write()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine((errorCount == 0 ? "OK" : "FAILED") + " " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                              + $"  errors={errorCount} warnings={warningCount}");
                if (errorCount > 0) sb.AppendLine("== ERRORS").Append(Errors);
                if (warningCount > 0) sb.AppendLine("== WARNINGS").Append(Warnings);
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception e) { Debug.LogWarning("[CompileReport] could not write the report: " + e.Message); }
        }
    }
}
