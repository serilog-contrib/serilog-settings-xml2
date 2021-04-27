using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Serilog.Settings.XML
{
    class XmlReader : ILoggerSettings
    {
        private const string LevelSwitchNameRegex = @"^\${0,1}[A-Za-z]+[A-Za-z0-9]*$";

        readonly XElement _section;

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
            //if (_section == null) throw new
        }


        public XmlReader(XElement section)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            _section = section;
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
        }

        private void ProcessFilterSwitchDeclarations()
        {

        }

        private void ApplyEnrichment(LoggerConfiguration loggerConfiguration)
        {

        }

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
                string switchInitialLevel = levelSwitch.Attribute("Level")?.Value;

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


        internal static bool IsValidSwitchName(string input) =>
            Regex.IsMatch(input, LevelSwitchNameRegex);

        static LogEventLevel ParseLogEventLevel(string value)
        {
            if (!Enum.TryParse(value, out LogEventLevel parsedLevel))
                throw new InvalidOperationException($"The value {value} is not a valid Serilog level.");
            return parsedLevel;
        }

    }
}
