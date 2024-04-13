using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;
using System.Collections.Generic;

namespace NoSnow
{
    // The name and location of the .coc file where the setting values are stored
    // The .coc extension is automatically added
    // Default location is the C:\Users\YOUR_USER_NAME\AppData\LocalLow\Colossal Order\Cities Skylines II directory
    // This location is above the Mods directory, it preserves the data even if the mod directory is deleted 
    [FileLocation(nameof(NoSnowSystem))]

    // The display order of the groups

    // TODO: *** Enable groups if needed ***
    [SettingsUIGroupOrder(kPrecipitationGroup /* , kTemperatureGroup, kDayNightGroup */)]

    // Repeat the same group names to show them

    // TODO: *** Enable groups if needed ***
    [SettingsUIShowGroupName(kPrecipitationGroup /* , kTemperatureGroup, kDayNightGroup */)]

    public class Setting : ModSetting
    {

        public Setting(IMod mod) : base(mod)
        {

        }

        // =================================================================
        // =================================================================
        // Tab 1 definition

        public const string kWeatherSection = "Weather";

        // =================================================================
        // Precipitation group definition on tab 1

        public const string kPrecipitationGroup = "Precipitation";

        // -----------------------------------------------------------------
        // Disable rain toggle control definition on tab 1

        [SettingsUISection(kWeatherSection, kPrecipitationGroup)]
        public bool DisableRainToggle { get; set; }

        // -----------------------------------------------------------------
        // Disable snow toggle control definition

        [SettingsUISection(kWeatherSection, kPrecipitationGroup)]
        public bool DisableSnowToggle { get; set; }

        /*
        // =================================================================
        // Temperature group definition on tab 1

        public const string kTemperatureGroup = "Temperature";

        // -----------------------------------------------------------------
        // Set minimum temperature toggle control

        [SettingsUISection(kWeatherSection, kTemperatureGroup)]
        public bool SetMinimumTemperatureToggle { get; set; }

        // -----------------------------------------------------------------
        // Minimum temperature integer slider control

        [SettingsUISlider(min = -50, max = 50, step = 1, scalarMultiplier = 1, unit = Unit.kTemperature)]
        [SettingsUISection(kWeatherSection, kTemperatureGroup)]
        public int MinimumTemperatureIntSlider { get; set; }

        // -----------------------------------------------------------------
        // Set maximum temperature toggle control

        [SettingsUISection(kWeatherSection, kTemperatureGroup)]
        public bool SetMaximumTemperatureToggle { get; set; }

        // -----------------------------------------------------------------
        // Maximum temperature integer slider control

        [SettingsUISlider(min = -50, max = 50, step = 1, scalarMultiplier = 1, unit = Unit.kTemperature)]
        [SettingsUISection(kWeatherSection, kTemperatureGroup)]
        public int MaximumTemperatureIntSlider { get; set; }
        */
        // =================================================================
        // =================================================================
        /*
        // Tab 2 definition
        public const string kTimeSection = "Time";

        // =================================================================
        // Day and night group definition on tab 2

        public const string kDayNightGroup = "Day and night";

        // -----------------------------------------------------------------
        // Disable night toggle control definition on tab 2

        [SettingsUISection(kTimeSection, kDayNightGroup)]
        public bool DisableNigthToggle { get; set; }
        */

        // -----------------------------------------------------------------
        // Button control definition

        // Commented out here to disable the display of the control
        // Also, commented out the references in ReadEntries below
        /*
        [SettingsUISection(kSection, kButtonGroup)]
        public bool Button { set { Mod.log.Info("Button clicked"); } }
        */

        // -----------------------------------------------------------------
        // Buton with confirmation control definition

        // Commented out here to disable the display of the control
        // Also, commented out the references in ReadEntries below
        /*
        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUISection(kSection, kButtonGroup)]
        public bool ButtonWithConfirmation { set { Mod.log.Info("ButtonWithConfirmation clicked"); } }
        */

        // -----------------------------------------------------------------
        // Integer dropdown control definition

        // Commented out here to disable the display of the control
        // Also, commented out the references in ReadEntries below
        /*
        [SettingsUIDropdown(typeof(Setting), nameof(GetIntDropdownItems))]
        [SettingsUISection(kSection, kDropdownGroup)]
        public int IntDropdown { get; set; }

        // Load the integer values into the dropdown
        public DropdownItem<int>[] GetIntDropdownItems()
        {
            var items = new List<DropdownItem<int>>();

            for (var i = 0; i < 3; i += 1)
            {
                items.Add(new DropdownItem<int>()
                {
                    value = i,
                    displayName = i.ToString(),
                });
            }

            return items.ToArray();
        }
        */

        // -----------------------------------------------------------------
        // Enum dropdown control definition

        // Commented out here to disable the display of the control
        // Also, commented out the references in ReadEntries below
        /*
        [SettingsUISection(kSection, kDropdownGroup)]
        public SomeEnum EnumDropdown { get; set; } = SomeEnum.Value1;

        // Load the enum values into the dropdown
        public enum SomeEnum
        {
            Value1,
            Value2,
            Value3,
        }
        */

        // -----------------------------------------------------------------

        public override void SetDefaults()
        {
            throw new System.NotImplementedException();
        }

    }

    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;
        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {

                // This is the name of the item in the Options menu. Make it the same as the name of the mod.
                { m_Setting.GetSettingsLocaleID(), "No Snow" },

                // =================================================================
                // =================================================================
                // Weather tab title localization

                // This is the name of the first tab of the settings page
                { m_Setting.GetOptionTabLocaleID(Setting.kWeatherSection), "Weather" },

                // =================================================================
                // Precipitation group localization

                { m_Setting.GetOptionGroupLocaleID(Setting.kPrecipitationGroup), "Precipitation" },

                // =================================================================
                // Disable rain toggle control localization

                // TODO: *** Update toggle title and description
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DisableRainToggle)), "Disable rain" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.DisableRainToggle)), $"Check to disable rain" },

                // -----------------------------------------------------------------
                // Disable snow toggle control localization

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DisableSnowToggle)), "Disable snow" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.DisableSnowToggle)), $"Check to disable snow" },

                /*
                // =================================================================
                // Temperature group localization
                { m_Setting.GetOptionGroupLocaleID(Setting.kTemperatureGroup), "Temperature" },

                // =================================================================
                // Set minimum temperature toggle control localization

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SetMinimumTemperatureToggle)), "Set minimum temperature" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.SetMinimumTemperatureToggle)), $"Check to set minimum temperature" },

                // -----------------------------------------------------------------
                // Minimum temperature integer slider control localization

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.MinimumTemperatureIntSlider)), "The minimum temperature" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.MinimumTemperatureIntSlider)), $"Set the minimum temperature value" },

                // -----------------------------------------------------------------
                // Set maximum temperature toggle control localization

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SetMaximumTemperatureToggle)), "Set maximum temperature" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.SetMaximumTemperatureToggle)), $"Check to set maximum temperature" },

                // -----------------------------------------------------------------
                // Maximum temperature integer slider control localization

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.MaximumTemperatureIntSlider)), "The maximum temperature" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.MaximumTemperatureIntSlider)), $"Set the maximum temperature value" },
                */

                /*
                // =================================================================
                // =================================================================
                // Time tab title localization

                // This is the name of the second tab of the settings page
                { m_Setting.GetOptionTabLocaleID(Setting.kTimeSection), "Time" },

                // =================================================================
                // Day and night group localization

                { m_Setting.GetOptionGroupLocaleID(Setting.kDayNightGroup), "Day and night" },

                // =================================================================
                // Disable night toggle control localization

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DisableNigthToggle)), "Disable night" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.DisableNigthToggle)), $"Check to disable night" },
                */
            };

            // Original localization dictionary
            /*
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "No Snow" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },

                { m_Setting.GetOptionGroupLocaleID(Setting.kButtonGroup), "Buttons" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kToggleGroup), "Snow" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kSliderGroup), "Temperature" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kDropdownGroup), "Dropdowns" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Button)), "Button" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Button)), $"Simple single button. It should be bool property with only setter or use [{nameof(SettingsUIButtonAttribute)}] to make button from bool property with setter and getter" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ButtonWithConfirmation)), "Button with confirmation" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ButtonWithConfirmation)), $"Button can show confirmation message. Use [{nameof(SettingsUIConfirmationAttribute)}]" },
                { m_Setting.GetOptionWarningLocaleID(nameof(Setting.ButtonWithConfirmation)), "is it confirmation text which you want to show here?" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Toggle)), "Disable snow" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Toggle)), $"Check to disable snow fall" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.IntSlider)), "The minimum temperature" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.IntSlider)), $"Set the minimum temperature value" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.IntDropdown)), "Int dropdown" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.IntDropdown)), $"Use int property with getter and setter and [{nameof(SettingsUIDropdownAttribute)}(typeof(SomeType), nameof(SomeMethod))] to get int dropdown: Method must be static or instance of your setting class with 0 parameters and returns {typeof(DropdownItem<int>).Name}" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnumDropdown)), "Simple enum dropdown" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnumDropdown)), $"Use any enum property with getter and setter to get enum dropdown" },

                { m_Setting.GetEnumValueLocaleID(Setting.SomeEnum.Value1), "Value 1" },
                { m_Setting.GetEnumValueLocaleID(Setting.SomeEnum.Value2), "Value 2" },
                { m_Setting.GetEnumValueLocaleID(Setting.SomeEnum.Value3), "Value 3" },
            };
            */
        }

        public void Unload()
        {

        }
    }
}
