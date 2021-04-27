using Serilog.Configuration;
using Serilog.Settings.XML;
using System;
using System.Xml.Linq;

namespace Serilog
{
    /// <summary>
    /// Extends <see cref="LoggerConfiguration"/> with support for XML settings.
    /// </summary>
    public static class ConfigurationLoggerXMLExtensions
    {
        /// <summary>
        /// Configuration section name required by this package.
        /// </summary>
        public const string DefaultSectionName = "Serilog";

        /// <summary>
        /// Loads logger settins from Linq XML Elements
        /// </summary>
        /// <param name="settingConfiguration">Logger setting configuration.</param>
        /// <param name="xElements">Linq XML</param>
        /// <returns></returns>
        public static LoggerConfiguration Xml(
           this LoggerSettingsConfiguration settingConfiguration,
           XElement xElements)
        {
            if (settingConfiguration == null) throw new ArgumentNullException(nameof(settingConfiguration));
            if (xElements == null) throw new ArgumentNullException(nameof(xElements));

            return settingConfiguration.Settings(new XmlReader(xElements));
        }

        /// <summary>
        /// Reads logger settings from XML settings
        /// </summary>
        /// <param name="settingConfiguration">Logger setting configuration.</param>
        /// <param name="xmlFile">XML File path to read.</param>
        /// <param name="sectionName">XML section name to load.</param>
        /// <returns></returns>
        public static LoggerConfiguration Xml(
           this LoggerSettingsConfiguration settingConfiguration,
           string xmlFile,
           string sectionName)
        {
            if (settingConfiguration == null) throw new ArgumentNullException(nameof(settingConfiguration));
            if (xmlFile == null) throw new ArgumentNullException(nameof(xmlFile));
            if (sectionName == null) throw new ArgumentNullException(nameof(sectionName));

            return settingConfiguration.Settings(new XmlReader(xmlFile, sectionName));
        }

        /// <summary>
        /// Reads logger settings from XML settings using the default section name
        /// </summary>
        /// <param name="settingConfiguration">Logger setting configuration.</param>
        /// <param name="xmlFile">XML File path to read.</param>
        /// <returns></returns>
        public static LoggerConfiguration Xml(
           this LoggerSettingsConfiguration settingConfiguration,
           string xmlFile) =>
            Xml(settingConfiguration, xmlFile, DefaultSectionName);
    }
}
