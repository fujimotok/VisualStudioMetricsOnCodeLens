using Microsoft.CodeAnalysis.CodeMetrics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VisualStudioMetricsOnCodeLens
{
    /// <summary>
    /// Visual Studio Code Metrics data model.
    /// </summary>
    internal class Metrics
    {
        public string Name { get; set; }
        public double MaintainabilityIndex { get; set; }
        public int CyclomaticComplexity { get; set; }
        public int ClassCoupling { get; set; }
        public int DepthOfInheritance { get; set; }
        public long SourceLines { get; set; }
        public long ExecutableLines { get; set; }

        /// <summary>
        /// To string with format.
        /// </summary>
        /// <remarks>
        /// The format string can contain the following placeholders:<br/>
        /// %MI%: Maintainability Index<br/>
        /// %CY%: Cyclomatic Complexity<br/>
        /// %CC%: Class Coupling<br/>
        /// %DI%: Depth Of Inheritance<br/>
        /// %SL%: Source Lines<br/>
        /// %EL%: Executable Lines<br/>
        /// </remarks>
        /// <param name="format">format string</param>
        /// <returns>formatted string</returns>
        public string ToString(string format)
        {
            if (string.IsNullOrEmpty(format))
            {
                return string.Empty;
            }

            return format.Replace("%MI%", MaintainabilityIndex.ToString())
                         .Replace("%CY%", CyclomaticComplexity.ToString())
                         .Replace("%CC%", ClassCoupling.ToString())
                         .Replace("%DI%", DepthOfInheritance.ToString())
                         .Replace("%SL%", SourceLines.ToString())
                         .Replace("%EL%", ExecutableLines.ToString());
        }

        /// <summary>
        /// Saves the specified code analysis metric data to a file in JSON format.
        /// </summary>
        /// <remarks>This method serializes the provided <see cref="CodeAnalysisMetricData"/> object into
        /// a JSON representation  and writes it to the specified file. If an error occurs during the process, the
        /// method returns <see langword="false"/>.</remarks>
        /// <param name="root">The root <see cref="CodeAnalysisMetricData"/> object to be serialized and saved.</param>
        /// <param name="filePath">The full path of the file where the data will be saved. If the directory does not exist, it will be created.</param>
        /// <returns><see langword="true"/> if the data was successfully saved to the file; otherwise, <see langword="false"/>.</returns>
        public static bool SaveToFile(CodeAnalysisMetricData root, string filePath)
        {
            if (root == null || string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            try
            {
                string dir = Path.GetDirectoryName(filePath);
                Directory.CreateDirectory(dir);
                
                var list = ConvertToList(root);
                var json = JsonConvert.SerializeObject(list, Formatting.Indented);
                File.WriteAllText(filePath, json, Encoding.UTF8);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static List<Metrics> ConvertToList(CodeAnalysisMetricData root)
        {
            var list = new List<Metrics>();
            Traverse(root, list);
            return list;
        }

        private static void Traverse(CodeAnalysisMetricData node, List<Metrics> list)
        {
            var metrics = new Metrics
            {
                Name = node.Symbol.ToDisplayString() ?? string.Empty,
                MaintainabilityIndex = node.MaintainabilityIndex,
                CyclomaticComplexity = node.CyclomaticComplexity,
                ClassCoupling = node.CoupledNamedTypes.Count,
                DepthOfInheritance = node.DepthOfInheritance ?? 0,
                SourceLines = node.SourceLines,
                ExecutableLines = node.ExecutableLines
            };

            list.Add(metrics);

            foreach (var child in node.Children)
            {
                Traverse(child, list);
            }
        }
    }
}
