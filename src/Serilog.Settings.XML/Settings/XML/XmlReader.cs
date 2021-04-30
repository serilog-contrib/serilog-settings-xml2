using Serilog.Configuration;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Serilog.Settings.XML
{
    class XmlReader : IXmlReader
    {
        private const string LevelSwitchNameRegex = @"^\${0,1}[A-Za-z]+[A-Za-z0-9]*$";

        private readonly XElement _section;

        private readonly IReadOnlyCollection<Assembly> _configurationAssemblies;

        private readonly ResolutionContext _resolutionContext = new();

        /// <summary>
        /// Creates a new instance of <see cref="XmlReader"/>
        /// </summary>
        /// <param name="xmlFile">XML File path</param>
        /// <param name="sectionName">XML section to load</param>
        public XmlReader(string xmlFile, string sectionName)
        {
            XElement doc = LoadXMLFile(xmlFile);
            _section = GetSection(doc, sectionName);
            _configurationAssemblies = LoadAssemblies();
            if (_section == null)
            {
                throw new InvalidOperationException($"XML section {sectionName} not found in root");
            }
        }

        public XmlReader(XElement section)
        {
            _section = section ?? throw new ArgumentNullException(nameof(section));
            _configurationAssemblies = LoadAssemblies();
        }

        // Used internally for processing nested configuration sections -- see GetMethodCalls below.
        internal XmlReader(XElement configElement, IReadOnlyCollection<Assembly> configurationAssemblies, ResolutionContext resolutionContext)
        {
            _section = configElement ?? throw new ArgumentNullException(nameof(configElement));
            _configurationAssemblies = configurationAssemblies ?? throw new ArgumentNullException(nameof(configurationAssemblies));
            _resolutionContext = resolutionContext ?? throw new ArgumentNullException(nameof(resolutionContext));
        }

        /// <summary>
        /// Loads a XML file
        /// </summary>
        /// <param name="xmlFile">XML file path</param>
        /// <returns>the loaded XmlDocument instance</returns>
        private XElement LoadXMLFile(string xmlFile) => XElement.Load(xmlFile);

        /// <summary>
        /// Gets the section
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="sectionName"></param>
        /// <returns></returns>
        private XElement GetSection(XElement doc, string sectionName) =>
            doc.Name.LocalName.Equals(sectionName, StringComparison.OrdinalIgnoreCase)
            ? doc
            : doc.Elements().FirstOrDefault(element => element.Name.LocalName.Equals(sectionName, StringComparison.OrdinalIgnoreCase));

        public void Configure(LoggerConfiguration loggerConfiguration)
        {
            ProcessLevelSwitchDeclarations();
            ProcessFilterSwitchDeclarations();

            ApplyMinimumLevel(loggerConfiguration);
            ApplyEnrichment(loggerConfiguration);
            ApplyFilters(loggerConfiguration);
            ApplyDestructuring(loggerConfiguration);
            ApplySinks(loggerConfiguration);
            ApplyAuditSinks(loggerConfiguration);
        }

        #region Destructures

        private void ApplyDestructuring(LoggerConfiguration loggerConfiguration)
        {
            var destructureElements = GetElements(_section, "Destructure");
            if (destructureElements.Count > 0)
            {
                var methodCalls = GetMethodCalls(destructureElements);
                CallConfigurationMethods(methodCalls, FindDestructureConfigurationMethods(_configurationAssemblies), loggerConfiguration.Destructure);
            }
        }

        private static IList<MethodInfo> FindDestructureConfigurationMethods(IReadOnlyCollection<Assembly> configurationAssemblies)
        {
            var found = FindConfigurationExtensionMethods(configurationAssemblies, typeof(LoggerDestructuringConfiguration));
            if (configurationAssemblies.Contains(typeof(LoggerDestructuringConfiguration).GetTypeInfo().Assembly))
                found.AddRange(SurrogateConfigurationMethods.Destructure);

            return found;
        }

        #endregion

        #region Filter

        private void ProcessFilterSwitchDeclarations()
        {
            var filterSwitchesElement = GetElements(_section, "FilterSwitches").FirstOrDefault();
            var filterSwitches = GetElements(filterSwitchesElement, "Switch");
            if (!(filterSwitches?.Any() ?? false))
            {
                return;
            }

            foreach (var filterSwitchElement in filterSwitches)
            {
                var filterSwitch = LoggingFilterSwitchProxy.Create();
                if (filterSwitch == null)
                {
                    SelfLog.WriteLine("FilterSwitches element found, but neither Serilog.Expressions nor Serilog.Filters.Expressions is referenced.");
                    break;
                }

                var switchName = GetAttribute(filterSwitchElement, "Name")?.Value;
                // switchName must be something like $switch to avoid ambiguities
                if (!IsValidSwitchName(switchName))
                {
                    throw new FormatException(@$"""{switchName}"" is not a valid name for a Filter Switch declaration. The first character of the name must be a letter or '$' sign, like <FilterSwitches> <Switch Name=""$switchName"" Expression=""FilterExpression"" /> </FilterSwitches>");
                }

                SetFilterSwitch(throwOnError: true);

                _resolutionContext.AddFilterSwitch(switchName, filterSwitch);

                void SetFilterSwitch(bool throwOnError)
                {
                    var filterExpr = filterSwitchElement.FirstNode?.NodeType == System.Xml.XmlNodeType.Text
                        ? filterSwitchElement.Value
                        : GetAttribute(filterSwitchElement, "Expression")?.Value;
                    if (string.IsNullOrWhiteSpace(filterExpr))
                    {
                        filterSwitch.Expression = null;
                        return;
                    }

                    try
                    {
                        filterSwitch.Expression = filterExpr;
                    }
                    catch (Exception e)
                    {
                        var errMsg = $"The expression '{filterExpr}' is invalid filter expression: {e.Message}.";
                        if (throwOnError)
                        {
                            throw new InvalidOperationException(errMsg, e);
                        }

                        SelfLog.WriteLine(errMsg);
                    }
                }
            }
        }

        private void ApplyFilters(LoggerConfiguration loggerConfiguration)
        {
            var filterDirective = GetElements(_section, "Filter");
            if (filterDirective.Count > 0)
            {
                var methodCalls = GetMethodCalls(filterDirective);
                CallConfigurationMethods(methodCalls, FindFilterConfigurationMethods(_configurationAssemblies), loggerConfiguration.Filter);
            }
        }

        private static IList<MethodInfo> FindFilterConfigurationMethods(IReadOnlyCollection<Assembly> configurationAssemblies)
        {
            var found = FindConfigurationExtensionMethods(configurationAssemblies, typeof(LoggerFilterConfiguration));
            if (configurationAssemblies.Contains(typeof(LoggerFilterConfiguration).GetTypeInfo().Assembly))
                found.AddRange(SurrogateConfigurationMethods.Filter);

            return found;
        }

        #endregion

        #region Sinks

        void IXmlReader.ApplySinks(LoggerSinkConfiguration loggerSinkConfiguration)
        {
            throw new NotImplementedException("Not implemented. Please report use case!");
            //var methodCalls = GetMethodCalls(_section);
            //CallConfigurationMethods(methodCalls, FindSinkConfigurationMethods(_configurationAssemblies), loggerSinkConfiguration);
        }

        private void ApplySinks(LoggerConfiguration loggerConfiguration)
        {
            var writeToElements = GetElements(_section, "WriteTo");
            if (writeToElements.Count > 0)
            {
                var methodCalls = GetMethodCalls(writeToElements);
                CallConfigurationMethods(methodCalls, FindSinkConfigurationMethods(_configurationAssemblies), loggerConfiguration.WriteTo);
            }
        }

        private static IList<MethodInfo> FindSinkConfigurationMethods(IReadOnlyCollection<Assembly> configurationAssemblies)
        {
            var found = FindConfigurationExtensionMethods(configurationAssemblies, typeof(LoggerSinkConfiguration));
            if (configurationAssemblies.Contains(typeof(LoggerSinkConfiguration).GetTypeInfo().Assembly))
                found.AddRange(SurrogateConfigurationMethods.WriteTo);

            return found;
        }

        #endregion

        #region Audit-Sinks

        private void ApplyAuditSinks(LoggerConfiguration loggerConfiguration)
        {
            var auditToElements = GetElements(_section, "AuditTo");
            if (auditToElements.Count > 0)
            {
                var methodCalls = GetMethodCalls(auditToElements);
                CallConfigurationMethods(methodCalls, FindAuditSinkConfigurationMethods(_configurationAssemblies), loggerConfiguration.AuditTo);
            }
        }

        private static IList<MethodInfo> FindAuditSinkConfigurationMethods(IReadOnlyCollection<Assembly> configurationAssemblies)
        {
            var found = FindConfigurationExtensionMethods(configurationAssemblies, typeof(LoggerAuditSinkConfiguration));
            if (configurationAssemblies.Contains(typeof(LoggerAuditSinkConfiguration).GetTypeInfo().Assembly))
                found.AddRange(SurrogateConfigurationMethods.AuditTo);
            return found;
        }

        #endregion

        #region Enrichment

        void IXmlReader.ApplyEnrichment(LoggerEnrichmentConfiguration loggerEnrichmentConfiguration)
        {
            throw new NotImplementedException("Not implemented. Please report use case!");
            //var methodCalls = GetMethodCalls(_section);
            //CallConfigurationMethods(methodCalls, FindEventEnricherConfigurationMethods(_configurationAssemblies), loggerEnrichmentConfiguration);
        }

        private void ApplyEnrichment(LoggerConfiguration loggerConfiguration)
        {
            var enricherElements = GetElements(_section, "Enrich");
            if (enricherElements.Count > 0)
            {
                var methodCalls = GetMethodCalls(enricherElements);
                CallConfigurationMethods(methodCalls, FindEventEnricherConfigurationMethods(_configurationAssemblies), loggerConfiguration.Enrich);
            }

            var propertyElements = GetElements(_section, "Property");
            if (propertyElements.Count > 0)
            {
                foreach (XElement propertyElement in propertyElements)
                {
                    string name = GetAttribute(propertyElement, "Name")?.Value;
                    string value = propertyElement.FirstNode?.NodeType == System.Xml.XmlNodeType.Text
                        ? propertyElement.Value
                        : GetAttribute(propertyElement, "Value")?.Value;

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        throw new InvalidOperationException("Property has no name");
                    }
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        throw new InvalidOperationException($"Property {name} has no value");
                    }

                    loggerConfiguration.Enrich.WithProperty(name, value);
                }
            }
        }

        private static IList<MethodInfo> FindEventEnricherConfigurationMethods(IReadOnlyCollection<Assembly> configurationAssemblies)
        {
            var found = FindConfigurationExtensionMethods(configurationAssemblies, typeof(LoggerEnrichmentConfiguration));
            if (configurationAssemblies.Contains(typeof(LoggerEnrichmentConfiguration).GetTypeInfo().Assembly))
                found.AddRange(SurrogateConfigurationMethods.Enrich);

            return found;
        }

        #endregion

        #region Find Methods & Arguments

        private static List<MethodInfo> FindConfigurationExtensionMethods(IReadOnlyCollection<Assembly> configurationAssemblies, Type configType)
        {
            return configurationAssemblies
                .SelectMany(a => a.ExportedTypes
                    .Select(t => t.GetTypeInfo())
                    .Where(t => t.IsSealed && t.IsAbstract && !t.IsNested))
                .SelectMany(t => t.DeclaredMethods)
                .Where(m => m.IsStatic && m.IsPublic && m.IsDefined(typeof(ExtensionAttribute), false)
                         && m.GetParameters()[0].ParameterType == configType)
                .ToList();
        }

        private void CallConfigurationMethods(ILookup<string, Dictionary<string, IConfigurationArgumentValue>> methods, IList<MethodInfo> configurationMethods, object receiver)
        {
            foreach (var method in methods.SelectMany(g => g.Select(x => new { g.Key, Value = x })))
            {
                var methodInfo = SelectConfigurationMethod(configurationMethods, method.Key, method.Value.Keys);

                if (methodInfo != null)
                {
                    var call = (from p in methodInfo.GetParameters().Skip(1)
                                let directive = method.Value.FirstOrDefault(s => ParameterNameMatches(p.Name, s.Key))
                                select directive.Key == null
                                    ? GetImplicitValueForNotSpecifiedKey(p, methodInfo)
                                    : directive.Value.ConvertTo(p.ParameterType, _resolutionContext)).ToList();

                    call.Insert(0, receiver);
                    methodInfo.Invoke(null, call.ToArray());
                }
            }
        }

        private static bool ParameterNameMatches(string actualParameterName, string suppliedName) =>
            suppliedName.Equals(actualParameterName, StringComparison.OrdinalIgnoreCase);

        private static bool ParameterNameMatches(string actualParameterName, IEnumerable<string> suppliedNames) =>
            suppliedNames.Any(s => ParameterNameMatches(actualParameterName, s));

        private static bool HasImplicitValueWhenNotSpecified(ParameterInfo paramInfo) =>
            paramInfo.HasDefaultValue;

        private object GetImplicitValueForNotSpecifiedKey(ParameterInfo parameter, MethodInfo methodToInvoke)
        {
            if (!HasImplicitValueWhenNotSpecified(parameter))
            {
                throw new InvalidOperationException("GetImplicitValueForNotSpecifiedKey() should only be called for parameters for which HasImplicitValueWhenNotSpecified() is true. " +
                                                    "This means something is wrong in the Serilog.Settings.Xml code.");
            }

            return parameter.DefaultValue;
        }

        internal static MethodInfo SelectConfigurationMethod(IEnumerable<MethodInfo> candidateMethods, string name, IEnumerable<string> suppliedArgumentNames)
        {
            var selectedMethod = candidateMethods
                .Where(m => m.Name == name
                         && m.GetParameters()
                            .Skip(1)
                            .All(p => HasImplicitValueWhenNotSpecified(p) ||
                                      ParameterNameMatches(p.Name, suppliedArgumentNames)))
                .OrderByDescending(m =>
                {
                    var matchingArgs = m.GetParameters().Where(p => ParameterNameMatches(p.Name, suppliedArgumentNames)).ToList();

                    // Prefer the configuration method with most number of matching arguments and of those the ones with
                    // the most string type parameters to predict best match with least type casting
                    return new Tuple<int, int>(
                        matchingArgs.Count,
                        matchingArgs.Count(p => p.ParameterType == typeof(string)));
                })
                .FirstOrDefault();

            if (selectedMethod == null)
            {
                var methodsByName = candidateMethods
                    .Where(m => m.Name == name)
                    .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Skip(1).Select(p => p.Name))})")
                    .ToList();

                if (methodsByName.Count == 0)
                {
                    SelfLog.WriteLine($"Unable to find a method called {name}. Candidate methods are:{Environment.NewLine}{string.Join(Environment.NewLine, candidateMethods)}");
                }
                else
                {
                    SelfLog.WriteLine($"Unable to find a method called {name} "
                    + (suppliedArgumentNames.Any()
                        ? "for supplied arguments: " + string.Join(", ", suppliedArgumentNames)
                        : "with no supplied arguments")
                    + ". Candidate methods are:"
                    + Environment.NewLine
                    + string.Join(Environment.NewLine, methodsByName));
                }
            }

            return selectedMethod;
        }

        internal ILookup<string, Dictionary<string, IConfigurationArgumentValue>> GetMethodCalls(IList<XElement> elements)
        {
            var methodCalls = elements
                .Select(element => new
                {
                    Name = GetAttribute(element, "Name")?.Value,
                    Args = element.HasElements
                        ? element.Elements()
                                 .Select(args => new
                                 {
                                     Name = args.Name.LocalName,
                                     Value = GetArgumentValue(args, _configurationAssemblies)
                                 })
                                 .ToDictionary(a => a.Name, a => a.Value)
                        : new Dictionary<string, IConfigurationArgumentValue>()
                })
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .ToList();

            // Convert ControlledBy attribute to method call
            var controllByCalls = elements.Where(element => !string.IsNullOrWhiteSpace(GetAttribute(element, "ControlledBy")?.Value))
                        .Select(element => new
                        {
                            Name = "ControlledBy",
                            Args = new Dictionary<string, IConfigurationArgumentValue>()
                            {
                                { "Switch", new StringArgumentValue(GetAttribute(element, "ControlledBy").Value) }
                            }
                        })
                        .ToList();

            return methodCalls.Concat(controllByCalls).ToLookup(p => p.Name, p => p.Args);
        }

        internal static IConfigurationArgumentValue GetArgumentValue(XNode argumentNode, IReadOnlyCollection<Assembly> configurationAssemblies)
        {
            if (argumentNode is XText text)
            {
                return new StringArgumentValue(text.Value);
            }
            else if (argumentNode is XElement telement && telement.FirstNode is XText ttext)
            {
                return new StringArgumentValue(ttext.Value);
            }
            else if (argumentNode is XElement element)
            {
                return new ObjectArgumentValue(element, configurationAssemblies);
            }

            throw new InvalidOperationException($"Argument value type unkown: {argumentNode.GetType().FullName}");
        }

        #endregion

        #region MinimumLevel

        private void ApplyMinimumLevel(LoggerConfiguration loggerConfiguration)
        {
            XElement minimumLevel = GetElements(_section, "MinimumLevel").FirstOrDefault();
            if (minimumLevel == null)
            {
                return;
            }

            string defaultMinLevel = minimumLevel.FirstNode?.NodeType == System.Xml.XmlNodeType.Text
                ? minimumLevel.Value
                : GetAttribute(minimumLevel, "Default")?.Value;
            if (!string.IsNullOrWhiteSpace(defaultMinLevel))
            {
                ApplyMinimumLevel(defaultMinLevel, (configuration, levelSwitch) => configuration.ControlledBy(levelSwitch));
            }

            string controlledBy = GetAttribute(minimumLevel, "ControlledBy")?.Value;
            if (!string.IsNullOrWhiteSpace(controlledBy))
            {
                var globalMinimumLevelSwitch = _resolutionContext.LookUpLevelSwitchByName(controlledBy);
                // not calling ApplyMinimumLevel local function because here we have a reference to a LogLevelSwitch already
                loggerConfiguration.MinimumLevel.ControlledBy(globalMinimumLevelSwitch);
            }

            foreach (XElement @override in GetElements(minimumLevel, "Override"))
            {
                string source = GetAttribute(@override, "Source")?.Value;
                controlledBy = GetAttribute(@override, "ControlledBy")?.Value;

                if (!string.IsNullOrWhiteSpace(controlledBy))
                {
                    var overrideSwitch = _resolutionContext.LookUpLevelSwitchByName(controlledBy);
                    // not calling ApplyMinimumLevel local function because here we have a reference to a LogLevelSwitch already
                    loggerConfiguration.MinimumLevel.Override(source, overrideSwitch);
                }
                else if (@override?.FirstNode?.NodeType == System.Xml.XmlNodeType.Text
                    && Enum.TryParse(@override?.Value, out LogEventLevel _))
                {
                    ApplyMinimumLevel(@override?.Value, (configuration, levelSwitch) => configuration.Override(source, levelSwitch));
                }
            }

            void ApplyMinimumLevel(string minLevel, Action<LoggerMinimumLevelConfiguration, LoggingLevelSwitch> applyConfigAction)
            {
                var minimumLevel = ParseLogEventLevel(minLevel);

                var levelSwitch = new LoggingLevelSwitch(minimumLevel);
                applyConfigAction(loggerConfiguration.MinimumLevel, levelSwitch);
            }
        }

        static LogEventLevel ParseLogEventLevel(string value)
        {
            if (!Enum.TryParse(value, out LogEventLevel parsedLevel))
                throw new InvalidOperationException($"The value {value} is not a valid Serilog level.");
            return parsedLevel;
        }

        #endregion

        #region Switches

        internal static bool IsValidSwitchName(string input) =>
            !string.IsNullOrWhiteSpace(input) && Regex.IsMatch(input, LevelSwitchNameRegex);

        private void ProcessLevelSwitchDeclarations()
        {
            var levelSwitchesElement = GetElements(_section, "LevelSwitches").FirstOrDefault();
            var levelSwitches = GetElements(levelSwitchesElement, "Switch");
            if (!(levelSwitches?.Any() ?? false))
            {
                return;
            }

            foreach (var levelSwitch in levelSwitches)
            {
                string switchName = GetAttribute(levelSwitch, "Name")?.Value;
                string switchInitialLevel = levelSwitch.FirstNode?.NodeType == System.Xml.XmlNodeType.Text
                    ? levelSwitch.Value
                    : GetAttribute(levelSwitch, "Level")?.Value;

                // switchName must be something like $switch to avoid ambiguities
                if (!IsValidSwitchName(switchName))
                {
                    throw new FormatException($"\"{switchName}\" is not a valid name for a Level Switch declaration. The first character of the name must be a letter or '$' sign, like <LevelSwitches> <Switch Name=\"$switchName\" Level=\"InitialLevel\" /> </LevelSwitches>");
                }

                LoggingLevelSwitch newSwitch;
                if (string.IsNullOrEmpty(switchInitialLevel))
                {
                    newSwitch = new LoggingLevelSwitch();
                }
                else
                {
                    var initialLevel = ParseLogEventLevel(switchInitialLevel);
                    newSwitch = new LoggingLevelSwitch(initialLevel);
                }

                // make them available later on when resolving argument values
                _resolutionContext.AddLevelSwitch(switchName, newSwitch);
            }
        }

        #endregion

        #region Usings

        private IReadOnlyCollection<Assembly> LoadAssemblies()
        {
            var serilogAssembly = typeof(ILogger).Assembly;
            var assemblies = new Dictionary<string, Assembly> { [serilogAssembly.FullName] = serilogAssembly };

            foreach (var usingElement in GetElements(_section, "Using"))
            {
                var assemblyName = usingElement.FirstNode?.NodeType == System.Xml.XmlNodeType.Text
                    ? usingElement.Value
                    : GetAttribute(usingElement, "Asm")?.Value;

                if (string.IsNullOrWhiteSpace(assemblyName))
                {
                    throw new InvalidOperationException(
                        "A zero-length or whitespace assembly name was supplied to a Serilog.Using configuration statement.");
                }

                var assembly = Assembly.Load(new AssemblyName(assemblyName));
                if (!assemblies.ContainsKey(assembly.FullName))
                {
                    assemblies.Add(assembly.FullName, assembly);
                }
            }

            return assemblies.Values.ToList().AsReadOnly();
        }

        #endregion

        private static IList<XElement> GetElements(XElement element, string name) =>
            element?.Elements()?.Where(e => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.ToList();

        private static XAttribute GetAttribute(XElement element, string name) =>
           element?.Attributes()?.Where(e => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.FirstOrDefault();
    }
}
