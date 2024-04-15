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
using static NiceWeather.NiceWeatherSystem;

// Thanks for the help from yenyang, who pointed me to Water Features to get the current temperature based on
// https://github.com/yenyang/Water_Features/blob/38770174dd26e2e6ceef6e1b2959d0677f83f2ba/Water_Features/Systems/RetentionBasinSystem.cs#L99

namespace NiceWeather
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(NiceWeatherSystem)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        public Setting m_Setting;
        public NiceWeatherSystem _niceWeather;

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            if (_niceWeather == null)
            {
                // Instantiate NiceWeather
                _niceWeather = new NiceWeatherSystem(this);
            }

            World.DefaultGameObjectInjectionWorld.AddSystemManaged(_niceWeather);

            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));

            AssetDatabase.global.LoadSettings(nameof(NiceWeatherSystem), m_Setting, new Setting(this));

            // Set up triggers when thee is an update in the game 
            OnCreateWorld(updateSystem);
        }

        // Set up triggers when there is an update in the game 
        public void OnCreateWorld(UpdateSystem updateSystem)
        {
            // updateSystem.UpdateBefore<MySystem1>(SystemUpdatePhase.Modification2); // Before

            // Trigger the NiceWeatherSystem in the Main Loop of the game every 25 ms
            updateSystem.UpdateAt<NiceWeatherSystem>(SystemUpdatePhase.MainLoop); // At

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

    public partial class NiceWeatherSystem : GameSystemBase
    {

        private class ConfigValues
        {
            private bool _disableRain;
            private bool _disableSnow;
            private bool _disableClouds;
            private bool _disableFog;

            public bool DisableRain { get => _disableRain; set => _disableRain = value; }
            public bool DisableSnow { get => _disableSnow; set => _disableSnow = value; }
            public bool DisableClouds { get => _disableClouds; set => _disableClouds = value; }
            public bool DisableFog { get => _disableFog; set => _disableFog = value; }
        }

        public class EnvironmentValues
        {
            private bool _isRaining;
            private bool _isSnowing;
            private bool _isCloudy;
            private bool _isFoggy;
            private bool _isAboveFreezing;

            public bool IsRaining { get => _isRaining; set => _isRaining = value; }
            public bool IsSnowing { get => _isSnowing; set => _isSnowing = value; }
            public bool IsCloudy { get => _isCloudy; set => _isCloudy = value; }
            public bool IsFoggy { get => _isFoggy; set => _isFoggy = value; }
            public bool IsAboveFreezing { get => _isAboveFreezing; set => _isAboveFreezing = value; }
        }

        public class PriorValues
        {
            private bool _priorIsAboveFreezing;
            private bool _priorIsRaining;
            private bool _priorIsSnowing;
            private bool _priorIsCloudy;
            private bool _priorIsFoggy;
            private bool _priorDisableRainToggle;
            private bool _priorDisableSnowToggle;
            private bool _priorDisableCloudToggle;
            private bool _priorDisableFogToggle;

            public bool PriorIsAboveFreezing { get => _priorIsAboveFreezing; set => _priorIsAboveFreezing = value; }
            public bool PriorIsRaining { get => _priorIsRaining; set => _priorIsRaining = value; }
            public bool PriorIsSnowing { get => _priorIsSnowing; set => _priorIsSnowing = value; }
            public bool PriorIsCloudy { get => _priorIsCloudy; set => _priorIsCloudy = value; }
            public bool PriorIsFoggy { get => _priorIsFoggy; set => _priorIsFoggy = value; }
            public bool PriorDisableRainToggle { get => _priorDisableRainToggle; set => _priorDisableRainToggle = value; }
            public bool PriorDisableSnowToggle { get => _priorDisableSnowToggle; set => _priorDisableSnowToggle = value; }
            public bool PriorDisableCloudToggle { get => _priorDisableCloudToggle; set => _priorDisableCloudToggle = value; }
            public bool PriorDisableFogToggle { get => _priorDisableFogToggle; set => _priorDisableFogToggle = value; }
        }

        public bool isInitialized = false;
        public Mod _mod;
        // public ClimateSystem _OLDclimateSystem;
        public ClimateSystem _climateSystem;

        private bool _inGame;
        private bool _inEditor;
        private bool _isModInGoodState;

        private DateTime _lastUpdateTime;
        private TimeSpan _lastUpdateTimeInterval = new TimeSpan(0,0,5); // 5 seconds

        private ConfigValues _configValues = new ConfigValues();
        private EnvironmentValues _environmentValues = new EnvironmentValues();
        private PriorValues _priorValues = new PriorValues();
 
        // Constructor
        public NiceWeatherSystem(Mod mod)
        {
            _mod = mod;
        }

        protected override void OnCreate()
        {

            Mod.log.Info("*** OnCreate started");

            base.OnCreate();

            // Instantiate the Climate System to control the weather
            // _OLDclimateSystem = World.GetExistingSystemManaged<ClimateSystem>();
            // Mod.log.Info("OLD Climate System found");
            _climateSystem = World.DefaultGameObjectInjectionWorld?.GetOrCreateSystemManaged<ClimateSystem>();
            Mod.log.Info("*** Climate System found");

            Mod.log.Info("*** OnCreate ran successfully");
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {

            Mod.log.Info("*** We are in the main menu, or in the game or editor, OnGameLoadingComplete started");

            // Execute OnGameLoadingComplete in the base class
            base.OnGameLoadingComplete(purpose, mode);

            _inGame = mode.IsGame();
            _inEditor = mode.IsEditor();

            _isModInGoodState = true;
            if (_mod == null)
            {
                Mod.log.Warn("Failed to instatiate the mod: _mod is null.");
                _isModInGoodState = false;
            }
            else if (_mod.m_Setting == null)
            {
                Mod.log.Warn("Failed to access settings: _mod.m_Setting is null.");
                _isModInGoodState = false;
            }

            if (_inGame)
            {
                Mod.log.Info("* * * * In the game");
            } else if (_inEditor)
            {
                Mod.log.Info("* * * * In the editor");
            } else
            {
                Mod.log.Info("* * * * Not in the game or in the editor");
            }


            if ( (!_inGame && !_inEditor ) || !_isModInGoodState)
            {
                // We are not in the game or in the editor, or the mod is not correctly initialized, return

                Mod.log.Info($"*** Returning, {nameof(_inGame)} = {_inGame}, {nameof(_inEditor)} = {_inEditor}, {nameof(_isModInGoodState)} = {_isModInGoodState}");

                return;
            }

            // Get the config and environment values
            GetConfigAndGameValues(_configValues, _environmentValues);

            // Set the map based on the config settings
            RunAtMapLoading(_configValues, _environmentValues);

            // Run the update process, do not wait for the first delay
            RunUpdateProcess();

            Mod.log.Info("*** OnGameLoadingComplete ran successfull");
        }

        // As set up in OnCreateWorld() this function is triggered in the main loop of the game continuously
        protected override void OnUpdate()
        {

            if (!_inGame || !_isModInGoodState)
            {
                // We are not in the game or in the editor, or the mod is not correctly initialized, return

                // Mod.log.Info("*** Returning, _inGameOrEditor = " + _inGameOrEditor + " _isModInGoodState = " + _isModInGoodState);

                return;
            }

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

            // Mod.log.Info("*** RunUpdateProcess started");

            if (_climateSystem == null)
            {
                // Climate system has not yet been initialized, return 
                Mod.log.Info("*** _climateSystem is null, returning");
                return;
            }

            // Mod.log.Info($"Now: {now}, Last update time: {_lastUpdateTime}, Elapsed time: {elapsedTime}, _lastUpdateTimeInterval: {_lastUpdateTimeInterval}");
            GetConfigAndGameValues(_configValues, _environmentValues);

            // Control the precipitation
            ControlPrecipitation(_configValues, _environmentValues, _priorValues);

            // Control the cloud and fog
            ControlCloudAndFog(_configValues, _environmentValues, _priorValues);

            // Mod.log.Info("*** RunUpdateProcess ran successfully");

        }

        // Read the config values from the Settings file and the environment values from the Climate System
        private void GetConfigAndGameValues(ConfigValues configValues, EnvironmentValues environmentValues)
        {

            // Mod.log.Info("*** GetConfigAndGameValues started");

            // Get the config values
            configValues.DisableRain = _mod.m_Setting.DisableRainToggle;
            configValues.DisableSnow = _mod.m_Setting.DisableSnowToggle;
            configValues.DisableClouds = _mod.m_Setting.DisableCloudsToggle;
            configValues.DisableFog = _mod.m_Setting.DisableFogToggle;

            // Get the weather state
            environmentValues.IsRaining = _climateSystem.isRaining;
            environmentValues.IsSnowing = _climateSystem.isSnowing;
            environmentValues.IsCloudy = _climateSystem.cloudiness.value > 0;
            environmentValues.IsFoggy = _climateSystem.fog.value > 0;

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

            // Check if the temperature is above freezing. On high mountains the snow starts to accummulate at around 7 degrees celsius
            environmentValues.IsAboveFreezing = temperature > freezingTemperature + 10;

            // Mod.log.Info("*** GetConfigAndGameValues ran successfully");
        }

        // Run this when a new map is loaded
        private void RunAtMapLoading(ConfigValues configValues, EnvironmentValues environmentValues)
        {
            // Set the precipitation, remove the snow, clouds and fog at the start of the map if the toggles are set

            Mod.log.Info("*** RunAtMapLoading started. Set the environment based on the config settings");

            if (configValues.DisableRain || configValues.DisableSnow)
            {
                // The disable rain or the disable snow toggle is set, disable the precipitation
                DisablePrecipitation();
            }

            if (configValues.DisableSnow)
            {
                // The Disable Snow toggle is set, remove the snow
                RemoveSnow();
            }

            if (configValues.DisableClouds)
            {
                // The Disable Cloud toggle is set, cycle the clouds ansd disable the clouds
                MaximizeClouds();
                DisableClouds();
            }

            if (configValues.DisableFog)
            {
                // Disable Fog toggle is set, cycle the fog and disable the fog
                MaximizeFog();
                DisableFog();
            }

            Mod.log.Info("*** RunAtMapLoading ran successfully");
        }

        // Control the precipitation based on the config settings
        private void ControlPrecipitation(ConfigValues configValues, EnvironmentValues environmentValues, PriorValues priorValues)
        {
            // Mod.log.Info("*** ControlPrecipitation started");

            bool isThereChange = false;

            // Check for change

            if (priorValues.PriorDisableRainToggle != configValues.DisableRain)
            {
                Mod.log.Info($"=== {nameof(configValues.DisableRain)} changed from {priorValues.PriorDisableRainToggle} to {configValues.DisableRain}");
                isThereChange = true;
            }

            if (priorValues.PriorDisableSnowToggle != configValues.DisableSnow)
            {
                Mod.log.Info($"=== {nameof(configValues.DisableSnow)} changed from {priorValues.PriorDisableSnowToggle} to {configValues.DisableSnow}");
                isThereChange = true;
            }

            if (priorValues.PriorIsAboveFreezing != environmentValues.IsAboveFreezing)
            {
                Mod.log.Info($"=== {nameof(environmentValues.IsAboveFreezing)} changed from {priorValues.PriorIsAboveFreezing} to {environmentValues.IsAboveFreezing}");
                isThereChange = true;
            }

            if (priorValues.PriorIsRaining != environmentValues.IsRaining)
            {
                Mod.log.Info($"=== {nameof(environmentValues.IsRaining)} changed from {priorValues.PriorIsRaining} to {environmentValues.IsRaining}");
                isThereChange = true;
            }

            if (priorValues.PriorIsSnowing != environmentValues.IsSnowing)
            {
                Mod.log.Info($"=== {nameof(environmentValues.IsSnowing)} changed from {priorValues.PriorIsSnowing} to {environmentValues.IsSnowing}");
                isThereChange = true;
            }

            if (!isThereChange)
            {
                // No change in settings and temperature
                return;
            }

            Mod.log.Info("*** Updating the precipitation");
            
            Mod.log.Info($"The {nameof(environmentValues.IsAboveFreezing)} is {environmentValues.IsAboveFreezing}");
            Mod.log.Info($"The {nameof(environmentValues.IsRaining)} is {environmentValues.IsRaining}");
            Mod.log.Info($"The {nameof(environmentValues.IsSnowing)} is {environmentValues.IsSnowing}");
            Mod.log.Info($"The {nameof(configValues.DisableRain)} is {configValues.DisableRain}");
            Mod.log.Info($"The {nameof(configValues.DisableSnow)} is {configValues.DisableSnow}");

            // -----------------------------------------------------------------
            // Enable the rain or snow if the user just unchecked the toggle

            if (!configValues.DisableRain && priorValues.PriorDisableRainToggle && environmentValues.IsAboveFreezing)
            {
                // This is above freezing, and enabling rain
                Mod.log.Info("=== It is above freezing, and the user unchecked the 'Disable Rain' toggle, enable the rain");
                _climateSystem.precipitation.overrideState = false;
            }

            if (! configValues.DisableSnow && priorValues.PriorDisableSnowToggle && !environmentValues.IsAboveFreezing)
            {
                // This is below freezing, and enabling snow
                Mod.log.Info("=== It is below freezing, and the user unchecked the Disable 'Snow toggle', enable the snow");
                _climateSystem.precipitation.overrideState = false;
            }
            else if (configValues.DisableSnow && !priorValues.PriorDisableSnowToggle)
            {
                // The user just checked the 'Disable Snow toggle', remove the snow
                Mod.log.Info("=== The user just checked the 'Disable Snow' toggle, remove the snow");
                RemoveSnow();
            }

            // -----------------------------------------------------------------
            // Control the rain

            if (configValues.DisableRain && environmentValues.IsRaining)
            {
                // It is raini disable the rain
                Mod.log.Info("--- It is raining, disable the rain");
                DisablePrecipitation();
            }

            // -----------------------------------------------------------------
            // Control the snow

            if (configValues.DisableSnow && environmentValues.IsSnowing)
            {
                // It is snowing, disable the snow
                Mod.log.Info("--- It is snowing, disable the snow and remove it");
                DisablePrecipitation();

            }

            // -----------------------------------------------------------------
            // THIS IS  NOT NECESSARY, AS WE DIRECTLY CHECK FOR RAIN AND SNOW

            /*
            // Check if the precipitation changed from snow to rain
            if (isTemperatureChangedToPositive && !disableRainToggle)
            {
                // The temperature just changed positive, and the rain is not disabled, enable the precipitation
                Mod.log.Info("The temperature just changed positive, and the rain is not disabled, enable the rain");
                _climateSystem.precipitation.overrideState = false;
            }

            // Check if the precipitation changed from rain to snow
            if (isTemperatureChangedToNegative && !configValues.DisableSnow)
            {
                // The temperature just changed negative, and the snow is not disabled, enable the precipitation
                Mod.log.Info("The temperature just changed negative, and the snow is not disabled, enable the snow");
                _climateSystem.precipitation.overrideState = false;
            }
            */

            // -----------------------------------------------------------------
            // Update the "prior" values

            priorValues.PriorDisableRainToggle = configValues.DisableRain;
            priorValues.PriorDisableSnowToggle = configValues.DisableSnow;
            priorValues.PriorIsAboveFreezing = environmentValues.IsAboveFreezing;
            priorValues.PriorIsRaining = environmentValues.IsRaining;
            priorValues.PriorIsSnowing = environmentValues.IsSnowing;

            Mod.log.Info("*** ControlPrecipitation ran successfully");

        }

        // Control the cloud and fog based on the config values
        private void ControlCloudAndFog(ConfigValues configValues, EnvironmentValues environmentValues, PriorValues priorValues)
        {
            // Mod.log.Info("*** ControlCloudAndFog started");

            bool isThereChange = false;

            // Check for change

            if (priorValues.PriorDisableCloudToggle != configValues.DisableClouds)
            {
                Mod.log.Info($"=== {nameof(configValues.DisableClouds)} changed from {priorValues.PriorDisableCloudToggle} to {configValues.DisableClouds}");
                isThereChange = true;
            }

            if (priorValues.PriorDisableFogToggle != configValues.DisableFog)
            {
                Mod.log.Info($"=== {nameof(configValues.DisableFog)} changed from {priorValues.PriorDisableFogToggle} to {configValues.DisableFog}");
                isThereChange = true;
            }

            if (priorValues.PriorIsCloudy != environmentValues.IsCloudy)
            {
                Mod.log.Info($"=== {nameof(environmentValues.IsCloudy)} changed from {priorValues.PriorIsCloudy} to {environmentValues.IsCloudy}");
                isThereChange = true;
            }

            if (priorValues.PriorIsFoggy != environmentValues.IsFoggy)
            {
                Mod.log.Info($"=== {nameof(environmentValues.IsFoggy)} changed from {priorValues.PriorIsFoggy} to {environmentValues.IsFoggy}");
                isThereChange = true;
            }

            if (!isThereChange)
            {
                // No change in settings and cloudiness and fogginess
                return;
            }

            Mod.log.Info("Updating Cloud and fog");

            Mod.log.Info($"The {nameof(environmentValues.IsCloudy)} is {environmentValues.IsCloudy}");
            Mod.log.Info($"The {nameof(environmentValues.IsFoggy)} is {environmentValues.IsFoggy}");
            Mod.log.Info($"The {nameof(configValues.DisableClouds)} is {configValues.DisableClouds}");
            Mod.log.Info($"The {nameof(configValues.DisableFog)} is {configValues.DisableFog}");

            // -----------------------------------------------------------------
            // Enable the clouds or fog if the user just unchecked the toggle

            if (configValues.DisableClouds && !priorValues.PriorDisableCloudToggle)
            {
                // The user unchecked the 'Disable Clouds' toggle, enable the clouds
                Mod.log.Info("=== The user just checked the 'Disable Clouds' toggle, remove and disable the clouds");
                _climateSystem.cloudiness.overrideState = false;
            }

            if (!configValues.DisableClouds && priorValues.PriorDisableCloudToggle)
            {
                // The user unchecked the 'Disable Clouds' toggle, enable the clouds
                Mod.log.Info("=== The user just unchecked the 'Disable Clouds' toggle, enable the clouds");
                MaximizeClouds();
                DisableClouds();
            }

            if (configValues.DisableFog && !priorValues.PriorDisableFogToggle)
            {
                // The user unchecked the 'Disable Fog' toggle, enable the fog
                Mod.log.Info("=== The user just checked the 'Disable Fog' toggle, remove and disable the fog");
                MaximizeFog();
                DisableFog();
            }

            if (!configValues.DisableFog && priorValues.PriorDisableFogToggle)
            {
                // The user unchecked the 'Disable Fog' toggle, enable the fog
                Mod.log.Info("=== The user just unchecked the 'Disable Fog' toggle, enable the fog");
                _climateSystem.fog.overrideState = false;
            }

            // -----------------------------------------------------------------
            // Control the clouds

            if (configValues.DisableClouds && environmentValues.IsCloudy)
            {
                // It is cloudy, disable the Clouds
                Mod.log.Info("--- It is cloudy, disable the Clouds");
                DisableClouds();
            }

            // -----------------------------------------------------------------
            // Control the fog

            if (configValues.DisableFog && environmentValues.IsFoggy)
            {
                // It is foggy, disable the Fog
                Mod.log.Info("--- It is foggy, disable the Fog");
                DisableFog();
            }

            // -----------------------------------------------------------------
            // Update the "prior" values

            priorValues.PriorDisableCloudToggle = configValues.DisableClouds;
            priorValues.PriorDisableFogToggle = configValues.DisableFog;
            priorValues.PriorIsCloudy = environmentValues.IsCloudy;
            priorValues.PriorIsFoggy = environmentValues.IsFoggy;

            Mod.log.Info("*** ControlCloudAndFog ran successfully");

        }

        // -----------------------------------------------------------------
        // Disable the precipitation
        private void DisablePrecipitation()
        {
            _climateSystem.precipitation.overrideState = true;
            _climateSystem.precipitation.overrideValue = 0;
        }

        // -----------------------------------------------------------------
        // Remove existing snow
        private void RemoveSnow()
        {
            Mod.log.Info("Remove existing snow");
            base.World.GetOrCreateSystemManaged<SnowSystem>().DebugReset();
        }

        // -----------------------------------------------------------------
        // Maximize the clouds
        private void MaximizeClouds()
        {
            Mod.log.Info("MaximizeClouds");
            _climateSystem.cloudiness.overrideState = true;
            _climateSystem.cloudiness.overrideValue = 1;
        }

        // Disable the clouds
        private void DisableClouds()
        {
            Mod.log.Info("DisableClouds");
            _climateSystem.cloudiness.overrideState = true;
            _climateSystem.cloudiness.overrideValue = 0;
        }

        // -----------------------------------------------------------------

        // Maximize the fog
        private void MaximizeFog()
        {
            Mod.log.Info("MaximizeFog");
            _climateSystem.fog.overrideState = true;
            _climateSystem.fog.overrideValue = 1;
        }

        // Disable the fog
        private void DisableFog()
        {
            Mod.log.Info("DisableFog");
            _climateSystem.fog.overrideState = true;
            _climateSystem.fog.overrideValue = 0;
        }

        // -----------------------------------------------------------------

        public void OnGameExit()
        {
            Mod.log.Info("OnGameExit started");

            isInitialized = false;
        }

        // -----------------------------------------------------------------

        protected override void OnDestroy()
        {
            Mod.log.Info("OnDestroy started");

            base.OnDestroy();
        }

    }

}
