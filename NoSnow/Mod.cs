using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.Simulation;
using System;
using System.Runtime.InteropServices;
using Unity.Entities;

// Thanks for the help from yenyang, who pointed me to Water Features to get the current temperature based on
// https://github.com/yenyang/Water_Features/blob/38770174dd26e2e6ceef6e1b2959d0677f83f2ba/Water_Features/Systems/RetentionBasinSystem.cs#L99

namespace NoSnow
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(NoSnowSystem)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        public Setting m_Setting;
        public NoSnowSystem _noSnow;

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            if (_noSnow == null)
            {
                // Instantiate NoSnow
                _noSnow = new NoSnowSystem(this);
            }

            World.DefaultGameObjectInjectionWorld.AddSystemManaged(_noSnow);

            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));

            AssetDatabase.global.LoadSettings(nameof(NoSnowSystem), m_Setting, new Setting(this));

            // Set up triggers when thee is an update in the game 
            OnCreateWorld(updateSystem);
        }

        // Set up triggers when thee is an update in the game 
        public void OnCreateWorld(UpdateSystem updateSystem)
        {
            // updateSystem.UpdateBefore<MySystem1>(SystemUpdatePhase.Modification2); // Before

            // Trigger the NoSnowSystem in the Main Loop of the game every 25 ms
            updateSystem.UpdateAt<NoSnowSystem>(SystemUpdatePhase.MainLoop); // At

            // updateSystem.UpdateAfter<MySystem3>(SystemUpdatePhase.Modification4); // After
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }

    }

    public partial class NoSnowSystem : GameSystemBase
    {
        public bool isInitialized = false;
        public Mod _mod;
        // public ClimateSystem _OLDclimateSystem;
        public ClimateSystem _climateSystem;
        private DateTime _lastUpdateTime;
        private TimeSpan _lastUpdateTimeInterval = new TimeSpan(0,0,5); // 5 seconds
        private bool _priorIsAboveFreezing;
        private bool _priorDisableRainToggle;
        private bool _priorDisableSnowToggle;
        private bool _priorDisableCloudToggle;
        private bool _priorDisableFogToggle;

        // Constructor
        public NoSnowSystem(Mod mod)
        {
            _mod = mod;
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            // Instantiate the Climate System to control the weather
            // _OLDclimateSystem = World.GetExistingSystemManaged<ClimateSystem>();
            Mod.log.Info("OLD Climate System found");
            _climateSystem = World.DefaultGameObjectInjectionWorld?.GetOrCreateSystemManaged<ClimateSystem>();
            Mod.log.Info("Climate System found");

            Mod.log.Info("OnCreate Ran Successfully");
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {

            // Execute OnGameLoadingComplete in the base class
            base.OnGameLoadingComplete(purpose, mode);


            bool isModInGoodState = true;
            if (_mod == null)
            {
                Mod.log.Warn("Failed to instatiate the mod: _mod is null.");
                isModInGoodState = false;
            }
            else if (_mod.m_Setting == null)
            {
                Mod.log.Warn("Failed to access settings: _mod.m_Setting is null.");
                isModInGoodState = false;
            }

            if (!mode.IsGameOrEditor() || !isModInGoodState)
            {
                // We are not in the game or in the editor, or the mod is not correctly initialized, return
                return;
            }


            // Run the update process, do not wait for the first delay
            RunUpdateProcess();
        }

        // As set up in OnCreateWorld() this function is triggered in the main loop of the game continuously
        protected override void OnUpdate()
        {

            DateTime now = DateTime.Now;
            TimeSpan elapsedTime = now - _lastUpdateTime;

            if (elapsedTime < _lastUpdateTimeInterval || _lastUpdateTime == DateTime.MinValue)
            {
                // The last update was less than the specified timespan ago, or the last update time is not yet saved in the instance variable, return

                if (_lastUpdateTime == DateTime.MinValue)
                {
                    // Try to update the last update time instance variable to the first value
                    // This has to be repeated until the system stabilizes in around 30 seconds
                    _lastUpdateTime = now;
                    Mod.log.Info($"First time initializing Last update time: {_lastUpdateTime} to Now: {now}");
                }

                return;
            }

            // Get the config and environment values, and update the game if necessary
            RunUpdateProcess();

            // Update the last update time
            _lastUpdateTime = now;
            // Mod.log.Info($"Now: {now}, Last update time: {_lastUpdateTime}");
        }

        private void RunUpdateProcess()
        {
            if (_climateSystem == null)
            {
                // Climate system has not yet been initialized, return 
                return;
            }

            // Mod.log.Info($"Now: {now}, Last update time: {_lastUpdateTime}, Elapsed time: {elapsedTime}, _lastUpdateTimeInterval: {_lastUpdateTimeInterval}");

            // Get the config values
            bool disableRainToggle = _mod.m_Setting.DisableRainToggle;
            bool disableSnowToggle = _mod.m_Setting.DisableSnowToggle;
            bool disableCloudToggle = _mod.m_Setting.DisableCloudsToggle;
            bool disableFogToggle = _mod.m_Setting.DisableFogToggle;

            // Get the freezing and average temperatures
            float freezingTemperature = _climateSystem.freezingTemperature;

            // Get the current temperature ***
            float temperature = _climateSystem.temperature.value;
            float temperatureOverrideValue = _climateSystem.temperature.overrideValue;
            bool temperatureState = _climateSystem.temperature.overrideState;
            if (temperatureState)
            {
                // The temperature is overridden, use the override value
                temperature = temperatureOverrideValue;
            }

            // Check if the temperature is above freezing
            bool isAboveFreezing = temperature > freezingTemperature;

            // Control the precipitation
            ControlPrecipitation(disableRainToggle, disableSnowToggle, isAboveFreezing);

            // Control the cloud and fog
            ControlCloudAndFog(disableCloudToggle, disableFogToggle);
 
        }

        // Control the cloud and fog based on the config values
        private void ControlCloudAndFog(bool disableCloudToggle, bool disableFogToggle)
        {
            bool isThereChange = false;
            bool isDisableCloudChange = false;
            bool isDisableFogChange = false;

            // Check for change

            if (_priorDisableCloudToggle != disableCloudToggle)
            {
                Mod.log.Info($"{nameof(disableCloudToggle)} changed from {_priorDisableCloudToggle} to {disableCloudToggle}");
                isDisableCloudChange = true;
                isThereChange = true;
            }

            if (_priorDisableFogToggle != disableFogToggle)
            {
                Mod.log.Info($"{nameof(disableFogToggle)} changed from {_priorDisableFogToggle} to {disableFogToggle}");
                isDisableFogChange = true;
                isThereChange = true;
            }

            if (!isThereChange)
            {
                // No change in settings and temperature
                return;
            }

            Mod.log.Info($"The {nameof(disableCloudToggle)} is {disableCloudToggle}");
            Mod.log.Info($"The {nameof(disableFogToggle)} is {disableFogToggle}");

            Mod.log.Info("Updating Cloud and fog");

            if (disableCloudToggle == true)
            {
                // The average temperature is above freezing, disable the Cloud
                Mod.log.Info("Disable clouds");
                _climateSystem.cloudiness.overrideState = true;
                _climateSystem.cloudiness.overrideValue = 0;
            }
            else if (isDisableCloudChange == true && _priorDisableCloudToggle == true)
            {
                // Enabling clouds
                Mod.log.Info("Enable clouds");
                _climateSystem.cloudiness.overrideState = false;
            }

            if (disableFogToggle == true)
            {
                // The average temperature is above freezing, disable the Fog
                Mod.log.Info("Disable fog");
                _climateSystem.fog.overrideState = true;
                _climateSystem.fog.overrideValue = 0;
            }
            else if (isDisableFogChange == true && _priorDisableFogToggle == true)
            {
                // Enabling Fog
                Mod.log.Info("Enable fog");
                _climateSystem.fog.overrideState = false;
            }

            // Update the "prior" values
            _priorDisableCloudToggle = disableCloudToggle;
            _priorDisableFogToggle = disableFogToggle;
        }


        // Control the precipitation based on the config settings
        private void ControlPrecipitation(bool disableRainToggle, bool disableSnowToggle, bool isAboveFreezing)
        {
            bool isThereChange = false;
            bool isDisableRainChange = false;
            bool isDisableSnowChange = false;
            bool isTemperatureChangedToPositive = false;
            bool isTemperatureChangedToNegative = false;

            // Check for change

            if (_priorDisableRainToggle != disableRainToggle)
            {
                Mod.log.Info($"{nameof(disableRainToggle)} changed from {_priorDisableRainToggle} to {disableRainToggle}");
                isDisableRainChange = true;
                isThereChange = true;
            }

            if (_priorDisableSnowToggle != disableSnowToggle)
            {
                Mod.log.Info($"{nameof(disableSnowToggle)} changed from {_priorDisableSnowToggle} to {disableSnowToggle}");
                isDisableSnowChange = true;
                isThereChange = true;
            }

            if (_priorIsAboveFreezing != isAboveFreezing)
            {
                Mod.log.Info($"{nameof(isAboveFreezing)} changed from {_priorIsAboveFreezing} to {isAboveFreezing}");
                isThereChange = true;

                if (!_priorIsAboveFreezing && isAboveFreezing)
                {
                    // The temperature just changed to positive
                    isTemperatureChangedToPositive = true;
                }
                else
                {
                    // The temperature just changed negative
                    isTemperatureChangedToNegative = true; 
                }

            }

            if (!isThereChange)
            {
                // No change in settings and temperature
                return;
            }

            Mod.log.Info($"The {nameof(isAboveFreezing)} is {isAboveFreezing}");
            Mod.log.Info($"The {nameof(disableRainToggle)} is {disableRainToggle}");
            Mod.log.Info($"The {nameof(disableSnowToggle)} is {disableSnowToggle}");

            Mod.log.Info("Updating the precipitation");

            // Control the rain
            if (disableRainToggle && isAboveFreezing)
            {
                // The temperature is above freezing, disable the rain
                Mod.log.Info("The temperature is above freezing, disable the rain");
                _climateSystem.precipitation.overrideState = true;
                _climateSystem.precipitation.overrideValue = 0;
            }
            else if (isDisableRainChange && _priorDisableRainToggle && isAboveFreezing)
            {
                // This is above freezing, and enabling rain
                Mod.log.Info("It is above freezing, and the user unchecked the 'Disable Rain' togge, enable the rain");
                _climateSystem.precipitation.overrideState = false;
            }

            // Control the snow
            if (disableSnowToggle && !isAboveFreezing)
            {
                // The temperature is at or below freezing, disable the snow
                Mod.log.Info("The temperature is at or below freezing, disable the snow");
                _climateSystem.precipitation.overrideState = true;
                _climateSystem.precipitation.overrideValue = 0;
            }
            else if (isDisableSnowChange && _priorDisableSnowToggle && !isAboveFreezing)
            {
                // This is below freezing, and enabling snow
                Mod.log.Info("It is below freezing, and the user unchecked the Disable 'Snow toggle', enable the snow");
                _climateSystem.precipitation.overrideState = false;
            }

            // Check if the precipitation changed from snow to rain
            if (isTemperatureChangedToPositive && !disableRainToggle)
            {
                // The temperature just changed positive, and the rain is not disabled, enable the precipitation
                Mod.log.Info("The temperature just changed positive, and the rain is not disabled, enable the rain");
                _climateSystem.precipitation.overrideState = false;
            }

            // Check if the precipitation changed from rain to snow
            if (isTemperatureChangedToNegative && !disableSnowToggle)
            {
                // The temperature just changed negative, and the snow is not disabled, enable the precipitation
                Mod.log.Info("The temperature just changed negative, and the snow is not disabled, enable the snow");
                _climateSystem.precipitation.overrideState = false;
            }

            // Update the "prior" values
            _priorDisableRainToggle = disableRainToggle;
            _priorDisableSnowToggle = disableSnowToggle;
            _priorIsAboveFreezing = isAboveFreezing;

        }

        public void OnGameExit()
        {
            isInitialized = false;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }

    }

}
