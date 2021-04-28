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

        readonly XElement _section;

        readonly IReadOnlyCollection<Assembly> _configurationAssemblies;

        readonly ResolutionContext _resolutionContext = new();

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
        }


        public XmlReader(XElement section)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            _section = section;
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
        private XElement GetSection(XElement doc, string sectionName) => doc.Elements(sectionName).First();

        public void Configure(LoggerConfiguration loggerConfiguration)
        {
            ProcessLevelSwitchDeclarations();
            ProcessFilterSwitchDeclarations();

            ApplyMinimumLevel(loggerConfiguration);
            ApplyEnrichment(loggerConfiguration);
            ApplyFilters(loggerConfiguration);
            ApplyDestructuring(loggerConfiguration);
        }

        #region Destructures

        private void ApplyDestructuring(LoggerConfiguration loggerConfiguration)
        {
            var destructureElements = _section.Elements("Destructure").ToList();
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
            var filterSwitchesElement = _section.Elements("FilterSwitches").FirstOrDefault();
            var filterSwitches = filterSwitchesElement?.Elements("Switch");
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

                var switchName = filterSwitchElement.Attribute("Name")?.Value;
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
                        : filterSwitchElement.Attribute("Expression")?.Value;
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
            var filterDirective = _section.Elements("Filter").ToList();
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
            //var methodCalls = GetMethodCalls(_section);
            //CallConfigurationMethods(methodCalls, FindSinkConfigurationMethods(_configurationAssemblies), loggerSinkConfiguration);
        }

        #endregion

        #region Enrichment

        void IXmlReader.ApplyEnrichment(LoggerEnrichmentConfiguration loggerEnrichmentConfiguration)
        {
            //var methodCalls = GetMethodCalls(_section);
            //CallConfigurationMethods(methodCalls, FindEventEnricherConfigurationMethods(_configurationAssemblies), loggerEnrichmentConfiguration);
        }

        private void ApplyEnrichment(LoggerConfiguration loggerConfiguration)
        {
            var enricherElements = _section.Elements("Enricher").ToList();
            if (enricherElements.Count > 0)
            {
                var methodCalls = GetMethodCalls(enricherElements);
                CallConfigurationMethods(methodCalls, FindEventEnricherConfigurationMethods(_configurationAssemblies), loggerConfiguration.Enrich);
            }

            var propertyElements = _section.Elements("Property").ToList();
            if (propertyElements.Count > 0)
            {
                foreach (XElement propertyElement in propertyElements)
                {
                    string name = propertyElement.Attribute("Name")?.Value;
                    string value = propertyElement.FirstNode?.NodeType == System.Xml.XmlNodeType.Text
                        ? propertyElement.Value
                        : propertyElement.Attribute("Value")?.Value;

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
                    Name = element.Attribute("Name")?.Value,
                    Args = element.HasElements
                        ? element.Elements()
                                 .Select(args => new
                                 {
                                     Name = args.Name.LocalName,
                                     Value = GetArgumentValue(args.FirstNode, _configurationAssemblies)
                                 })
                                 .ToDictionary(a => a.Name, a => a.Value)
                        : new Dictionary<string, IConfigurationArgumentValue>()
                })
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .ToList();

            // Convert ControlledBy attribute to method call
            var controllByCalls = elements.Where(element => !string.IsNullOrWhiteSpace(element.Attribute("ControlledBy")?.Value))
                        .Select(element => new
                        {
                            Name = "ControlledBy",
                            Args = new Dictionary<string, IConfigurationArgumentValue>()
                            {
                                { "Switch", new StringArgumentValue(element.Attribute("ControlledBy").Value) }
                            }
                        })
                        .ToList();

            return methodCalls.Concat(controllByCalls).ToLookup(p => p.Name, p => p.Args);
        }

        internal static IConfigurationArgumentValue GetArgumentValue(XNode argumentNode, IReadOnlyCollection<Assembly> configurationAssemblies)
        {
            if (argumentNode == null)
            {
                throw new InvalidOperationException("Invalid argument");
            }

            if (argumentNode is XText text)
            {
                return new StringArgumentValue(text.Value);
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
            XElement minimumLevel = _section.Elements("MinimumLevel").FirstOrDefault();
            if (minimumLevel == null)
            {
                return;
            }

            string defaultMinLevel = minimumLevel.FirstNode?.NodeType == System.Xml.XmlNodeType.Text
                ? minimumLevel.Value
                : minimumLevel.Attribute("Default")?.Value;
            if (!string.IsNullOrWhiteSpace(defaultMinLevel))
            {
                ApplyMinimumLevel(defaultMinLevel, (configuration, levelSwitch) => configuration.ControlledBy(levelSwitch));
            }

            string controlledBy = minimumLevel.Attribute("ControlledBy")?.Value;
            if (!string.IsNullOrWhiteSpace(controlledBy))
            {
                var globalMinimumLevelSwitch = _resolutionContext.LookUpLevelSwitchByName(controlledBy);
                // not calling ApplyMinimumLevel local function because here we have a reference to a LogLevelSwitch already
                loggerConfiguration.MinimumLevel.ControlledBy(globalMinimumLevelSwitch);
            }

            foreach (XElement @override in minimumLevel.Elements("Override"))
            {
                string source = @override?.Attribute("Source")?.Value;
                controlledBy = @override?.Attribute("ControlledBy")?.Value;

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
            var levelSwitchesElement = _section.Elements("LevelSwitches").FirstOrDefault();
            var levelSwitches = levelSwitchesElement?.Elements("Switch");
            if (!(levelSwitches?.Any() ?? false))
            {
                return;
            }

            foreach (var levelSwitch in levelSwitches)
            {
                string switchName = levelSwitch.Attribute("Name")?.Value;
                string switchInitialLevel = levelSwitch.FirstNode?.NodeType == System.Xml.XmlNodeType.Text
                    ? levelSwitch.Value
                    : levelSwitch.Attribute("Level")?.Value;

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

            foreach (var usingElement in _section.Elements("Using"))
            {
                var assemblyName = usingElement.FirstNode?.NodeType == System.Xml.XmlNodeType.Text
                    ? usingElement.Value
                    : usingElement.Attribute("Asm")?.Value;

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
    }
}
