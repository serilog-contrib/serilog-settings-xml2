using Serilog.Core;
using System;
using System.Collections.Generic;

namespace Serilog.Settings.XML
{
    /// <summary>
    /// Keeps track of available elements that are useful when resolving values in the settings system.
    /// </summary>
    internal sealed class ResolutionContext
    {
        readonly IDictionary<string, LoggingLevelSwitch> _declaredLevelSwitches;
        readonly IDictionary<string, LoggingFilterSwitchProxy> _declaredFilterSwitches;

        public ResolutionContext()
        {
            _declaredLevelSwitches = new Dictionary<string, LoggingLevelSwitch>();
            _declaredFilterSwitches = new Dictionary<string, LoggingFilterSwitchProxy>();
        }

        /// <summary>
        /// Looks up a switch in the declared LoggingLevelSwitches
        /// </summary>
        /// <param name="switchName">the name of a switch to look up</param>
        /// <returns>the LoggingLevelSwitch registered with the name</returns>
        /// <exception cref="InvalidOperationException">if no switch has been registered with <paramref name="switchName"/></exception>
        public LoggingLevelSwitch LookUpLevelSwitchByName(string switchName)
        {
            if (_declaredLevelSwitches.TryGetValue(switchName, out var levelSwitch))
            {
                return levelSwitch;
            }

            throw new InvalidOperationException($"No LoggingLevelSwitch has been declared with name \"{switchName}\". You might be missing a section <LevelSwitches> <Switch Name=\"$switchName\" Level=\"InitialLevel\" /> </LevelSwitches>");
        }

        public LoggingFilterSwitchProxy LookUpFilterSwitchByName(string switchName)
        {
            if (_declaredFilterSwitches.TryGetValue(switchName, out var filterSwitch))
            {
                return filterSwitch;
            }

            throw new InvalidOperationException($"No LoggingFilterSwitch has been declared with name \"{switchName}\". You might be missing a section \"FilterSwitches\":{{\"{switchName}\":\"{{FilterExpression}}\"}}");
        }

        public void AddLevelSwitch(string levelSwitchName, LoggingLevelSwitch levelSwitch)
        {
            if (levelSwitchName == null) throw new ArgumentNullException(nameof(levelSwitchName));
            _declaredLevelSwitches[levelSwitchName] = levelSwitch ?? throw new ArgumentNullException(nameof(levelSwitch));
        }

        public void AddFilterSwitch(string filterSwitchName, LoggingFilterSwitchProxy filterSwitch)
        {
            if (filterSwitchName == null) throw new ArgumentNullException(nameof(filterSwitchName));
            if (filterSwitch == null) throw new ArgumentNullException(nameof(filterSwitch));
            _declaredFilterSwitches[filterSwitchName] = filterSwitch;
        }
    }
}