using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;
using System.Collections.Generic;

namespace NoSnow
{
    [FileLocation(nameof(NoSnow))]

    // Not referenced groups do not have to be commented out, but better to make the code cleaner
    [SettingsUIGroupOrder(kToggleGroupPrecipitation, kSliderGroupTemperature)]
    // [SettingsUIGroupOrder(kButtonGroup, kToggleGroup, kSliderGroup, kDropdownGroup)]

    // Not referenced groups do not have to be commented out, but better to make the code cleaner
    [SettingsUIShowGroupName(kToggleGroupPrecipitation, kSliderGroupTemperature)]
    // [SettingsUIShowGroupName(kButtonGroup, kToggleGroup, kSliderGroup, kDropdownGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";

        public Setting(IMod mod) : base(mod)
        {

        }

        // -----------------------------------------------------------------
        // Group definitions

        public const string kToggleGroupPrecipitation = "Toggle";
        public const string kSliderGroupTemperature = "Slider";

        // Not referenced groups do not have to be commented out, but better to make the code cleaner
        /*
        public const string kButtonGroup = "Button";
        public const string kDropdownGroup = "Dropdown";
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
        // Disable snow toggle control definition

        [SettingsUISection(kSection, kToggleGroupPrecipitation)]
        public bool ToggleDisableSnow { get; set; }

        // -----------------------------------------------------------------
        // Set minimum temparature integer slider control

        [SettingsUISlider(min = -50, max = 50, step = 1, scalarMultiplier = 1, unit = Unit.kTemperature)]
        [SettingsUISection(kSection, kSliderGroupTemperature)]
        public int IntSliderMinimumTemperature { get; set; }

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
                // -----------------------------------------------------------------
                // Tab title localization

                // This is the name of the tab in the Options menu. Make it the same as the name of the mod.
                { m_Setting.GetSettingsLocaleID(), "No Snow" },

                // I am not sure what the effect of this setting
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },

                // -----------------------------------------------------------------
                // Group localization

                { m_Setting.GetOptionGroupLocaleID(Setting.kToggleGroupPrecipitation), "Precipitation" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kSliderGroupTemperature), "Temperature" },

                // Not referenced groups do not have to be commented out, but better to make the code cleaner
                /*
                { m_Setting.GetOptionGroupLocaleID(Setting.kButtonGroup), "Buttons" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kDropdownGroup), "Dropdowns" },
                */

                // -----------------------------------------------------------------
                // Button control localization

                // Commented out as the control in Setting above is not displayed
                /*
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Button)), "Button" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Button)), $"Simple single button. It should be bool property with only setter or use [{nameof(SettingsUIButtonAttribute)}] to make button from bool property with setter and getter" },
                */

                // -----------------------------------------------------------------
                // Buton with confirmation control localization

                // Commented out as the control in Setting above is not displayed
                /*
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ButtonWithConfirmation)), "Button with confirmation" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ButtonWithConfirmation)), $"Button can show confirmation message. Use [{nameof(SettingsUIConfirmationAttribute)}]" },
                { m_Setting.GetOptionWarningLocaleID(nameof(Setting.ButtonWithConfirmation)), "is it confirmation text which you want to show here?" },
                */

                // -----------------------------------------------------------------
                // Disable snow toggle control localization

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ToggleDisableSnow)), "Disable snow" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ToggleDisableSnow)), $"Check to disable snow fall" },

                // -----------------------------------------------------------------
                // Set minimum temparature integer slider control localization

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.IntSliderMinimumTemperature)), "The minimum temperature" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.IntSliderMinimumTemperature)), $"Set the minimum temperature value" },

                // -----------------------------------------------------------------
                // Integer dropdown control localization

                // Commented out as the control in Setting above is not displayed
                /*
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.IntDropdown)), "Int dropdown" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.IntDropdown)), $"Use int property with getter and setter and [{nameof(SettingsUIDropdownAttribute)}(typeof(SomeType), nameof(SomeMethod))] to get int dropdown: Method must be static or instance of your setting class with 0 parameters and returns {typeof(DropdownItem<int>).Name}" },
                */

                // -----------------------------------------------------------------
                // Enum dropdown control localization

                // Commented out as the control in Setting above is not displayed
                /*
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnumDropdown)), "Simple enum dropdown" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnumDropdown)), $"Use any enum property with getter and setter to get enum dropdown" },

                { m_Setting.GetEnumValueLocaleID(Setting.SomeEnum.Value1), "Value 1" },
                { m_Setting.GetEnumValueLocaleID(Setting.SomeEnum.Value2), "Value 2" },
                { m_Setting.GetEnumValueLocaleID(Setting.SomeEnum.Value3), "Value 3" },
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
