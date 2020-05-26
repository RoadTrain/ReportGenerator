using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Palmmedia.ReportGenerator.Core.Logging;
using Palmmedia.ReportGenerator.Core.Parser.Analysis;
using Palmmedia.ReportGenerator.Core.Parser.Filtering;
using Palmmedia.ReportGenerator.Core.Properties;

namespace Palmmedia.ReportGenerator.Core.Parser
{
    /// <summary>
    /// Parser for XML reports generated by JaCoCo.
    /// </summary>
    internal class JaCoCoParser : ParserBase
    {
        /// <summary>
        /// The Logger.
        /// </summary>
        private static readonly ILogger Logger = LoggerFactory.GetLogger(typeof(JaCoCoParser));

        /// <summary>
        /// Regex to extract short method name.
        /// </summary>
        private static Regex methodRegex = new Regex(@"^(?<MethodName>.+)\((?<Arguments>.*)\).*$", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="JaCoCoParser" /> class.
        /// </summary>
        /// <param name="assemblyFilter">The assembly filter.</param>
        /// <param name="classFilter">The class filter.</param>
        /// <param name="fileFilter">The file filter.</param>
        internal JaCoCoParser(IFilter assemblyFilter, IFilter classFilter, IFilter fileFilter)
            : base(assemblyFilter, classFilter, fileFilter)
        {
        }

        /// <summary>
        /// Parses the given XML report.
        /// </summary>
        /// <param name="report">The XML report.</param>
        /// <returns>The parser result.</returns>
        public ParserResult Parse(XContainer report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var assemblies = new List<Assembly>();

            var modules = report.Descendants("package")
              .ToArray();

            var assemblyNames = modules
                .Select(m => m.Attribute("name").Value)
                .Distinct()
                .Where(a => this.AssemblyFilter.IsElementIncludedInReport(a))
                .OrderBy(a => a)
                .ToArray();

            foreach (var assemblyName in assemblyNames)
            {
                assemblies.Add(this.ProcessAssembly(modules, assemblyName));
            }

            var result = new ParserResult(assemblies.OrderBy(a => a.Name).ToList(), true, this.ToString());
            return result;
        }

        /// <summary>
        /// Processes the given assembly.
        /// </summary>
        /// <param name="modules">The modules.</param>
        /// <param name="assemblyName">Name of the assembly.</param>
        /// <returns>The <see cref="Assembly"/>.</returns>
        private Assembly ProcessAssembly(XElement[] modules, string assemblyName)
        {
            Logger.DebugFormat(Resources.CurrentAssembly, assemblyName);

            var classNames = modules
                .Where(m => m.Attribute("name").Value.Equals(assemblyName))
                .Elements("class")
                .Select(c => c.Attribute("name").Value)
                .Where(c => !c.Contains("$"))
                .Distinct()
                .Where(c => this.ClassFilter.IsElementIncludedInReport(c))
                .OrderBy(name => name)
                .ToArray();

            var assembly = new Assembly(assemblyName);

            Parallel.ForEach(classNames, className => this.ProcessClass(modules, assembly, className));

            return assembly;
        }

        /// <summary>
        /// Processes the given class.
        /// </summary>
        /// <param name="modules">The modules.</param>
        /// <param name="assembly">The assembly.</param>
        /// <param name="className">Name of the class.</param>
        private void ProcessClass(XElement[] modules, Assembly assembly, string className)
        {
            var classes = modules
                .Where(m => m.Attribute("name").Value.Equals(assembly.Name))
                .Elements("class")
                .Where(c => c.Attribute("name").Value.Equals(className)
                            || c.Attribute("name").Value.StartsWith(className + "$", StringComparison.Ordinal))
                .ToArray();

            var files = classes
                .Select(c => c.Attribute("sourcefilename")?.Value)
                .Where(f => f != null) // This attribute is not present in older JaCoCo versions
                .Distinct()
                .ToArray();

            var filteredFiles = files
                .Where(f => this.FileFilter.IsElementIncludedInReport(f))
                .ToArray();

            // If all files are removed by filters, then the whole class is omitted
            if ((files.Length == 0 && !this.FileFilter.HasCustomFilters) || filteredFiles.Length > 0)
            {
                var @class = new Class(className, assembly);

                foreach (var file in filteredFiles)
                {
                    var codeFile = ProcessFile(modules, @class, file, out int numberOrLines);

                    var methodsOfFile = classes
                        .Where(c => c.Attribute("sourcefilename") != null && c.Attribute("sourcefilename").Value.Equals(file))
                        .Elements("method")
                        .ToArray();

                    SetMethodMetrics(codeFile, methodsOfFile);
                    SetCodeElements(codeFile, methodsOfFile, numberOrLines);

                    @class.AddFile(codeFile);
                }

                assembly.AddClass(@class);
            }
        }

        /// <summary>
        /// Processes the file.
        /// </summary>
        /// <param name="modules">The modules.</param>
        /// <param name="class">The class.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="numberOrLines">The number of lines in the file.</param>
        /// <returns>The <see cref="CodeFile"/>.</returns>
        private static CodeFile ProcessFile(XElement[] modules, Class @class, string filePath, out int numberOrLines)
        {
            var linesOfFile = modules
                .Where(m => m.Attribute("name").Value.Equals(@class.Assembly.Name))
                .Elements("sourcefile")
                .Where(c => c.Attribute("name").Value.Equals(filePath))
                .Elements("line")
                .Select(line => new JaCoCoLineCoverage()
                {
                    LineNumber = int.Parse(line.Attribute("nr").Value, CultureInfo.InvariantCulture),
                    MissedInstructions = int.Parse(line.Attribute("mi")?.Value ?? "0", CultureInfo.InvariantCulture),
                    CoveredInstructions = int.Parse(line.Attribute("ci")?.Value ?? "0", CultureInfo.InvariantCulture),
                    MissedBranches = int.Parse(line.Attribute("mb")?.Value ?? "0", CultureInfo.InvariantCulture),
                    CoveredBranches = int.Parse(line.Attribute("cb")?.Value ?? "0", CultureInfo.InvariantCulture)
                })
                .OrderBy(seqpnt => seqpnt.LineNumber)
                .ToArray();

            var branches = GetBranches(linesOfFile);

            int[] coverage = new int[] { };
            LineVisitStatus[] lineVisitStatus = new LineVisitStatus[] { };

            if (linesOfFile.Length > 0)
            {
                coverage = new int[linesOfFile[linesOfFile.LongLength - 1].LineNumber + 1];
                lineVisitStatus = new LineVisitStatus[linesOfFile[linesOfFile.LongLength - 1].LineNumber + 1];

                for (int i = 0; i < coverage.Length; i++)
                {
                    coverage[i] = -1;
                }

                foreach (var line in linesOfFile)
                {
                    coverage[line.LineNumber] = line.CoveredInstructions > 0 ? 1 : 0;

                    bool partiallyCovered = line.MissedInstructions > 0;

                    LineVisitStatus statusOfLine = line.CoveredInstructions > 0 ? (partiallyCovered ? LineVisitStatus.PartiallyCovered : LineVisitStatus.Covered) : LineVisitStatus.NotCovered;
                    lineVisitStatus[line.LineNumber] = statusOfLine;
                }
            }

            numberOrLines = coverage.Length - 1;

            return new CodeFile(filePath, coverage, lineVisitStatus, branches);
        }

        /// <summary>
        /// Extracts the metrics from the given <see cref="XElement">XElements</see>.
        /// </summary>
        /// <param name="codeFile">The code file.</param>
        /// <param name="methodsOfFile">The methods of the file.</param>
        private static void SetMethodMetrics(CodeFile codeFile, IEnumerable<XElement> methodsOfFile)
        {
            foreach (var method in methodsOfFile)
            {
                string fullName = method.Attribute("name").Value + method.Attribute("desc").Value;

                if (fullName.StartsWith("lambda$"))
                {
                    continue;
                }

                string shortName = methodRegex.Replace(fullName, m => string.Format(CultureInfo.InvariantCulture, "{0}({1})", m.Groups["MethodName"].Value, m.Groups["Arguments"].Value.Length > 0 ? "..." : string.Empty));

                var metrics = new List<Metric>();

                var lineRate = method.Elements("counter")
                    .Where(e => e.Attribute("type") != null && e.Attribute("type").Value == "LINE")
                    .FirstOrDefault();

                if (lineRate != null)
                {
                    decimal missed = decimal.Parse(lineRate.Attribute("missed").Value, CultureInfo.InvariantCulture);
                    decimal covered = decimal.Parse(lineRate.Attribute("covered").Value, CultureInfo.InvariantCulture);
                    decimal total = missed + covered;

                    metrics.Add(new Metric(
                        ReportResources.Coverage,
                        ParserBase.CodeCoverageUri,
                        MetricType.CoveragePercentual,
                        total == 0 ? (decimal?)null : Math.Round((100 * covered) / total, 2, MidpointRounding.AwayFromZero)));
                }
                else
                {
                    // If no line rate available, do not add branch coverage too
                    continue;
                }

                var branchRate = method.Elements("counter")
                    .Where(e => e.Attribute("type") != null && e.Attribute("type").Value == "BRANCH")
                    .FirstOrDefault();

                if (branchRate != null)
                {
                    decimal missed = decimal.Parse(branchRate.Attribute("missed").Value, CultureInfo.InvariantCulture);
                    decimal covered = decimal.Parse(branchRate.Attribute("covered").Value, CultureInfo.InvariantCulture);
                    decimal total = missed + covered;

                    metrics.Add(new Metric(
                        ReportResources.BranchCoverage,
                        ParserBase.CodeCoverageUri,
                        MetricType.CoveragePercentual,
                        total == 0 ? (decimal?)null : Math.Round((100 * covered) / total, 2, MidpointRounding.AwayFromZero)));
                }
                else
                {
                    // If no branch coverage is available, add default to avoid empty columns
                    metrics.Add(new Metric(
                        ReportResources.BranchCoverage,
                        ParserBase.CodeCoverageUri,
                        MetricType.CoveragePercentual,
                        null));
                }

                var methodMetric = new MethodMetric(fullName, shortName, metrics);
                methodMetric.Line = method.Attribute("line") != null ? int.Parse(method.Attribute("line").Value, CultureInfo.InvariantCulture) : default(int?);

                codeFile.AddMethodMetric(methodMetric);
            }
        }

        /// <summary>
        /// Extracts the methods/properties of the given <see cref="XElement">XElements</see>.
        /// </summary>
        /// <param name="codeFile">The code file.</param>
        /// <param name="methodsOfFile">The methods of the file.</param>
        /// <param name="numberOrLines">The number of lines in the file.</param>
        private static void SetCodeElements(CodeFile codeFile, IEnumerable<XElement> methodsOfFile, int numberOrLines)
        {
            var codeElements = new List<CodeElementBase>();

            foreach (var method in methodsOfFile)
            {
                string methodName = method.Attribute("name").Value + method.Attribute("desc").Value;

                if (methodName.StartsWith("lambda$"))
                {
                    continue;
                }

                methodName = methodRegex.Replace(methodName, m => string.Format(CultureInfo.InvariantCulture, "{0}({1})", m.Groups["MethodName"].Value, m.Groups["Arguments"].Value));

                int lineNumber = int.Parse(method.Attribute("line")?.Value ?? "0", CultureInfo.InvariantCulture);

                codeElements.Add(new CodeElementBase(methodName, lineNumber));
            }

            codeElements.Sort((x, y) => x.FirstLine.CompareTo(y.FirstLine));
            for (int i = 0; i < codeElements.Count; i++)
            {
                var codeElement = codeElements[i];

                int lastLine = numberOrLines;
                if (i < codeElements.Count - 1)
                {
                    lastLine = codeElements[i + 1].FirstLine - 1;
                }

                codeFile.AddCodeElement(new CodeElement(
                    codeElement.Name,
                    CodeElementType.Method,
                    codeElement.FirstLine,
                    lastLine,
                    codeFile.CoverageQuota(codeElement.FirstLine, lastLine)));
            }
        }

        /// <summary>
        /// Gets the branches by line number.
        /// </summary>
        /// <param name="lines">The lines.</param>
        /// <returns>The branches by line number.</returns>
        private static Dictionary<int, ICollection<Branch>> GetBranches(IEnumerable<JaCoCoLineCoverage> lines)
        {
            var result = new Dictionary<int, ICollection<Branch>>();

            foreach (var line in lines)
            {
                if (line.MissedBranches == 0 && line.CoveredBranches == 0)
                {
                    continue;
                }

                int numberOfTotalBranches = line.MissedBranches + line.CoveredBranches;

                var branches = new HashSet<Branch>();

                for (int i = 0; i < numberOfTotalBranches; i++)
                {
                    string identifier = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}_{1}",
                        line.LineNumber,
                        i);

                    branches.Add(new Branch(i < line.CoveredBranches ? 1 : 0, identifier));
                }

                result.Add(line.LineNumber, branches);
            }

            return result;
        }

        /// <summary>
        /// Represents the coverage information of a line.
        /// </summary>
        private class JaCoCoLineCoverage
        {
            /// <summary>
            /// Gets or sets the line number.
            /// </summary>
            public int LineNumber { get; set; }

            /// <summary>
            /// Gets or sets the number of missed instructions.
            /// </summary>
            public int MissedInstructions { get; set; }

            /// <summary>
            /// Gets or sets the number of covered instructions.
            /// </summary>
            public int CoveredInstructions { get; set; }

            /// <summary>
            /// Gets or sets the number of missed branches.
            /// </summary>
            public int MissedBranches { get; set; }

            /// <summary>
            /// Gets or sets the number of covered branches.
            /// </summary>
            public int CoveredBranches { get; set; }
        }
    }
}
