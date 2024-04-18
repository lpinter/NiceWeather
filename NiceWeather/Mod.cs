﻿using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Game.Areas;
using Game.Modding;
using Game.SceneFlow;
using Game.Simulation;
using System;
using System.Runtime.InteropServices;
using System.Threading;
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
            log.Info($"{nameof(OnLoad)} started");

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

        /// <summary>
        /// Set up triggers when there is an update in the game 
        /// </summary>
        /// <param name="updateSystem"></param>
        public void OnCreateWorld(UpdateSystem updateSystem)
        {
            log.Info($"{nameof(OnCreateWorld)} started");

            // updateSystem.UpdateBefore<MySystem1>(SystemUpdatePhase.Modification2); // Before

            // Trigger the NiceWeatherSystem in the Main Loop of the game every 25 ms
            updateSystem.UpdateAt<NiceWeatherSystem>(SystemUpdatePhase.MainLoop); // At

            // updateSystem.UpdateAfter<MySystem3>(SystemUpdatePhase.Modification4); // After
        }

        /// <summary>
        /// Triggered when the mod is disposed because we exit to Desktop, happens before OnDestroy
        /// </summary>
        public void OnDispose()
        {
            log.Info($"{nameof(OnDispose)} started");

            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }

    }

    public partial class NiceWeatherSystem : GameSystemBase
    {

        /// <summary>
        /// We read the config values from the Settings file into this class
        /// </summary>
        private class ConfigValues
        {
            private bool _disableRainToggle;
            private bool _disableSnowToggle;
            private bool _disableCloudsToggle;
            private bool _disableFogToggle;

            public bool DisableRainToggle { get => _disableRainToggle; set => _disableRainToggle = value; }
            public bool DisableSnowToggle { get => _disableSnowToggle; set => _disableSnowToggle = value; }
            public bool DisableCloudsToggle { get => _disableCloudsToggle; set => _disableCloudsToggle = value; }
            public bool DisableFogToggle { get => _disableFogToggle; set => _disableFogToggle = value; }
        }

        /// <summary>
        /// We store the environment values in this class
        /// </summary>
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

        /// <summary>
        /// We store the previus values in this class
        /// </summary>
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

        private bool _debug = true;

        public Mod _mod;
        public bool isInitialized = false;
        //public ClimateSystem _OLDclimateSystem;
        public ClimateSystem _climateSystem;

        private bool _inGame;
        private bool _inEditor;
        private bool _isModInGoodState;

        private DateTime _lastUpdateTime;
        private int _updateCount;
        private TimeSpan _lastUpdateTimeInterval = new TimeSpan(0,0,5); // 5 seconds

        private ConfigValues _configValues;
        private EnvironmentValues _environmentValues;
        private PriorValues _priorValues;
 
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mod"></param>
        public NiceWeatherSystem(Mod mod)
        {
            _mod = mod;
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void OnCreate()
        {

            LogInfo($"{nameof(OnCreate)} started");

            base.OnCreate();

            //_OLDclimateSystem = World.GetExistingSystemManaged<ClimateSystem>();
            //Mod.log.Info("OLD Climate System found");

            // Instantiate the Climate System to control the weather
            _climateSystem = World.DefaultGameObjectInjectionWorld?.GetOrCreateSystemManaged<ClimateSystem>();
            LogInfo("Climate System found");

            LogInfo($"*** {nameof(OnCreate)} ran successfully");
        }

        /// <summary>
        /// Triggered when we open the Main Menu, Game and Editor
        /// </summary>
        /// <param name="purpose"></param>
        /// <param name="mode"></param>
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {

            LogInfo($"{nameof(OnGameLoadingComplete)} started - We are in the main menu, or in the game or editor");

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

            // Enable everything to be able to apply the settings when we get into the game of the editor
            EnableEverything();

            if (_inGame)
            {
                LogInfo("********************************");
                LogInfo("         In the game");
                LogInfo("********************************");
            }
            else if (_inEditor)
            {
                LogInfo("********************************");
                LogInfo("        In the editor");
                LogInfo("********************************");
            }
            else
            {
                LogInfo("********************************");
                LogInfo("Not in the game or in the editor");
                LogInfo("********************************");
            }


            if ( (!_inGame && !_inEditor ) || !_isModInGoodState)
            {
                // We are not in the game or in the editor, or the mod is not correctly initialized, return

                LogInfo($"*** Returning, {nameof(_inGame)} = {_inGame}, {nameof(_inEditor)} = {_inEditor}, {nameof(_isModInGoodState)} = {_isModInGoodState}");

                return;
            }

            //Clear the state values for a fresh start
            _updateCount = 0;
            _lastUpdateTime = DateTime.Now;
            _configValues = new ConfigValues();
            _environmentValues = new EnvironmentValues();
            _priorValues = new PriorValues();

            // Get the config and environment values
            GetConfigAndGameValues(_configValues, _environmentValues);

            // Set the map based on the config settings
            // EnforceDisableToggles(_configValues, _environmentValues);

            // Run the update process, do not wait for the first delay
            // RunUpdateProcess();

            LogInfo("*** OnGameLoadingComplete ran successfull");
        }

        /// <summary>
        /// As set up in OnCreateWorld() this function is triggered in the main loop of the game continuously
        /// </summary>
        protected override void OnUpdate()
        {

            if ((!_inGame && !_inEditor) || !_isModInGoodState)
            {
                // We are not in the game or in the editor, or the mod is not correctly initialized, return

                // LogDebug($"*** Returning, {nameof(_inGame)} = {_inGame}, {nameof(_inEditor)} = {_inEditor}, {nameof(_isModInGoodState)} = {_isModInGoodState}");

                return;
            }

            DateTime now = DateTime.Now;
            TimeSpan elapsedTime = now - _lastUpdateTime;

            if (elapsedTime < _lastUpdateTimeInterval)
            {
                // The last update was less than the specified timespan ago, or the last update time is not yet saved in the instance variable, return

                if (_lastUpdateTime == DateTime.MinValue)
                {
                    // Try to update the last update time instance variable to the first value
                    // This has to be repeated until the system stabilizes in around 30 seconds
                    _lastUpdateTime = now;
                    LogInfo($"First time initializing Last update time: {_lastUpdateTime} to Now: {now}");
                }

                return;
            }

            if (_climateSystem == null)
            {
                // Climate system has not yet been initialized, return 
                LogInfo($"*** {nameof(_climateSystem)} is null, returning");
                return;
            }

            // Get the config and environment values
            // GetConfigAndGameValues(_configValues, _environmentValues);

            // Set the map based on the config settings
            // EnforceDisableToggles(_configValues, _environmentValues);

            // Get the config and environment values, and update the game if necessary
            RunUpdateProcess();

            // Update the last update time
            _lastUpdateTime = now;
            _updateCount++;
            // LogDebug($"Now: {now}, Last update time: {_lastUpdateTime}");
        }

        /// <summary>
        /// Execute all updates
        /// </summary>
        private void RunUpdateProcess()
        {

            LogDebug($"*** {nameof(RunUpdateProcess)} started");

            if (_climateSystem == null)
            {
                // Climate system has not yet been initialized, return 
                LogInfo($"*** {nameof(_climateSystem)} is null, returning");
                return;
            }

            // Get the config and environment values
            GetConfigAndGameValues(_configValues, _environmentValues);

            // Control the precipitation
            ControlPrecipitation(_configValues, _environmentValues, _priorValues);

            // Control the cloud and fog
            ControlCloudAndFog(_configValues, _environmentValues, _priorValues);

            LogDebug("*** RunUpdateProcess ran successfully");

        }

        /// <summary>
        /// Read the config values from the Settings file and the environment values from the Climate System
        /// </summary>
        /// <param name="configValues"></param>
        /// <param name="environmentValues"></param>
        private void GetConfigAndGameValues(ConfigValues configValues, EnvironmentValues environmentValues)
        {

            LogDebug($"*** {nameof(GetConfigAndGameValues)} started");

            // -----------------------------------------------------------------
            // Get the config values

            configValues.DisableRainToggle = _mod.m_Setting.DisableRainToggle;
            configValues.DisableSnowToggle = _mod.m_Setting.DisableSnowToggle;
            configValues.DisableCloudsToggle = _mod.m_Setting.DisableCloudsToggle;
            configValues.DisableFogToggle = _mod.m_Setting.DisableFogToggle;

            // -----------------------------------------------------------------
            // Get the weather state

            environmentValues.IsRaining = _climateSystem.isRaining;
            environmentValues.IsSnowing = _climateSystem.isSnowing;
            environmentValues.IsCloudy = _climateSystem.cloudiness.value > 0;
            environmentValues.IsFoggy = _climateSystem.fog.value > 0;

            // -----------------------------------------------------------------
            // Find out if it is freezing anywhere on the map

            // Get the freezing temperature
            float freezingTemperature = _climateSystem.freezingTemperature;

            // Get the current temperature
            float temperature;
            bool isTemperatureOverride = _climateSystem.temperature.overrideState;

            if (isTemperatureOverride)
            {
                // The temperature is overridden, use the override value
                temperature = _climateSystem.temperature.overrideValue;
            } else
            {
                // Get the current temperarture
                temperature = _climateSystem.temperature.value;
            }

            // Check if the temperature is above freezing. On high mountains the snow starts to accummulate at around 7 degrees celsius
            environmentValues.IsAboveFreezing = temperature > freezingTemperature + 10;

            LogDebug("*** GetConfigAndGameValues ran successfully");
        }

        /// <summary>
        /// Run this when a new map is loaded
        /// </summary>
        /// <param name="configValues"></param>
        /// <param name="environmentValues"></param>
        private void EnforceDisableToggles(ConfigValues configValues, EnvironmentValues environmentValues)
        {

            // Set the precipitation, remove the snow, clouds and fog at the start of the map if the toggles are set

            LogDebug($"*** {nameof(EnforceDisableToggles)} started - Set the environment based on the config settings");

            //if (configValues.DisableRainToggle || configValues.DisableSnowToggle)
            //{
            //    LogDebug($"The 'Disable Rain' or the 'Disable Snow' toggle is set, disable the precipitation");
            //    DisablePrecipitation();
            //}

            //if (configValues.DisableSnowToggle)
            //{
            //    LogDebug($"The 'Disable Snow' toggle is set, remove the snow");
            //    RemoveSnow();
            //}


            if (configValues.DisableCloudsToggle)
            {
                LogDebug($"The 'Disable Cloud' toggle is set, disable the clouds");
                DisableClouds();
            }

            if (configValues.DisableFogToggle)
            {
                LogDebug($"The 'Disable Fog' toggle is set, disable the fog");
                DisableFog();
            }

            LogDebug($"{nameof(EnforceDisableToggles)} ran successfully");
        }

        /// <summary>
        /// Control the precipitation based on the config settings
        /// </summary>
        /// <param name="configValues"></param>
        /// <param name="environmentValues"></param>
        /// <param name="priorValues"></param>
        private void ControlPrecipitation(ConfigValues configValues, EnvironmentValues environmentValues, PriorValues priorValues)
        {
            LogDebug($"*** {nameof(ControlPrecipitation)} started");

            bool isThereChange = false;

            // Check for change

            if (priorValues.PriorDisableRainToggle != configValues.DisableRainToggle)
            {
                LogInfo($"=== {nameof(configValues.DisableRainToggle)} changed from {priorValues.PriorDisableRainToggle} to {configValues.DisableRainToggle}");
                isThereChange = true;
            }

            if (priorValues.PriorDisableSnowToggle != configValues.DisableSnowToggle)
            {
                LogInfo($"=== {nameof(configValues.DisableSnowToggle)} changed from {priorValues.PriorDisableSnowToggle} to {configValues.DisableSnowToggle}");
                isThereChange = true;
            }

            if (priorValues.PriorIsAboveFreezing != environmentValues.IsAboveFreezing)
            {
                LogInfo($"--- {nameof(environmentValues.IsAboveFreezing)} changed from {priorValues.PriorIsAboveFreezing} to {environmentValues.IsAboveFreezing}");
                isThereChange = true;
            }

            if (priorValues.PriorIsRaining != environmentValues.IsRaining)
            {
                LogInfo($"--- {nameof(environmentValues.IsRaining)} changed from {priorValues.PriorIsRaining} to {environmentValues.IsRaining}");
                isThereChange = true;
            }

            if (priorValues.PriorIsSnowing != environmentValues.IsSnowing)
            {
                LogInfo($"--- {nameof(environmentValues.IsSnowing)} changed from {priorValues.PriorIsSnowing} to {environmentValues.IsSnowing}");
                isThereChange = true;
            }

            //if (!isThereChange)
            //{
            //    // No change in settings and temperature
            //    return;
            //}

            //LogDebug("------------------------------");
            //LogDebug("*** Updating the precipitation");
            //LogDebug("------------------------------");
            //LogDebug($"The {nameof(environmentValues.IsAboveFreezing)} is {environmentValues.IsAboveFreezing}");
            //LogDebug($"The {nameof(environmentValues.IsRaining)} is {environmentValues.IsRaining}");
            //LogDebug($"The {nameof(environmentValues.IsSnowing)} is {environmentValues.IsSnowing}");
            //LogDebug($"The {nameof(configValues.DisableRainToggle)} is {configValues.DisableRainToggle}");
            //LogDebug($"The {nameof(configValues.DisableSnowToggle)} is {configValues.DisableSnowToggle}");

            // -----------------------------------------------------------------
            // Enable the rain or snow if the user just unchecked the toggle

            //if (!configValues.DisableRainToggle && priorValues.PriorDisableRainToggle && environmentValues.IsAboveFreezing)
            //{
            //    // This is above freezing, and enabling rain
            //    LogDebug("=== It is above freezing, and the user just unchecked the 'Disable Rain' toggle, enable the rain");
            //    EnablePrecipitation();
            //}

            //if (! configValues.DisableSnowToggle && priorValues.PriorDisableSnowToggle && !environmentValues.IsAboveFreezing)
            //{
            //    // This is below freezing, and enabling snow
            //    LogDebug("=== It is below freezing, and the user just unchecked the 'Disable Snow' toggle, enable the snow");
            //    EnablePrecipitation();
            //}
            //else if (configValues.DisableSnowToggle && !priorValues.PriorDisableSnowToggle)
            //{
            //    // The user just checked the 'Disable Snow toggle', remove the snow
            //    LogDebug("=== The user just checked the 'Disable Snow' toggle, remove the snow");
            //    RemoveSnow();
            //}

            // -----------------------------------------------------------------

            if (!configValues.DisableRainToggle && environmentValues.IsAboveFreezing)
            {
                // This is above freezing, and enabling rain
                LogDebug("=== It is above freezing, and the 'Disable Rain' toggle is false, enable the rain");
                EnablePrecipitation();
            }

            if (!configValues.DisableSnowToggle && !environmentValues.IsAboveFreezing)
            {
                // This is below freezing, and enabling snow
                LogDebug("=== It is below freezing, and the 'Disable Snow' toggle is false, enable the snow");
                EnablePrecipitation();
            }
            
            if (configValues.DisableSnowToggle)
            {
                // The user just checked the 'Disable Snow toggle', remove the snow
                LogDebug("=== The 'Disable Snow' toggle is true, remove the snow");
                RemoveSnow();
            }

            // -----------------------------------------------------------------
            // Control the rain

            if (configValues.DisableRainToggle && environmentValues.IsRaining)
            {
                // It is rainig disable the rain
                LogDebug("--- It is raining, disable the rain");
                DisablePrecipitation();
            }

            // -----------------------------------------------------------------
            // Control the snow

            if (configValues.DisableSnowToggle && environmentValues.IsSnowing)
            {
                // It is snowing, disable the snow
                LogDebug("--- It is snowing, disable the snow and remove it");
                DisablePrecipitation();

            }

            // -----------------------------------------------------------------
            // THIS IS  NOT NECESSARY, AS WE DIRECTLY CHECK FOR RAIN AND SNOW

            /*
            // Check if the precipitation changed from snow to rain
            if (isTemperatureChangedToPositive && !disableRainToggle)
            {
                // The temperature just changed positive, and the rain is not disabled, enable the precipitation
                LogDebug("The temperature just changed positive, and the rain is not disabled, enable the rain");
                EnablePrecipitation();
            }

            // Check if the precipitation changed from rain to snow
            if (isTemperatureChangedToNegative && !configValues.DisableSnow)
            {
                // The temperature just changed negative, and the snow is not disabled, enable the precipitation
                LogDebug("The temperature just changed negative, and the snow is not disabled, enable the snow");
                EnablePrecipitation();
            }
            */

            // -----------------------------------------------------------------
            // Update the "prior" values

            priorValues.PriorDisableRainToggle = configValues.DisableRainToggle;
            priorValues.PriorDisableSnowToggle = configValues.DisableSnowToggle;
            priorValues.PriorIsAboveFreezing = environmentValues.IsAboveFreezing;
            priorValues.PriorIsRaining = environmentValues.IsRaining;
            priorValues.PriorIsSnowing = environmentValues.IsSnowing;

            LogDebug($"*** {nameof(ControlPrecipitation)} ran successfully");

        }

        /// <summary>
        /// Control the cloud and fog based on the config values
        /// </summary>
        /// <param name="configValues"></param>
        /// <param name="environmentValues"></param>
        /// <param name="priorValues"></param>
        private void ControlCloudAndFog(ConfigValues configValues, EnvironmentValues environmentValues, PriorValues priorValues)
        {
            LogDebug($"*** {nameof(ControlCloudAndFog)} started");

            bool isThereChange = false;

            // Check for change

            if (priorValues.PriorDisableCloudToggle != configValues.DisableCloudsToggle)
            {
                LogInfo($"=== {nameof(configValues.DisableCloudsToggle)} changed from {priorValues.PriorDisableCloudToggle} to {configValues.DisableCloudsToggle}");
                isThereChange = true;
            }

            if (priorValues.PriorDisableFogToggle != configValues.DisableFogToggle)
            {
                LogInfo($"=== {nameof(configValues.DisableFogToggle)} changed from {priorValues.PriorDisableFogToggle} to {configValues.DisableFogToggle}");
                isThereChange = true;
            }

            if (priorValues.PriorIsCloudy != environmentValues.IsCloudy)
            {
                LogInfo($"--- {nameof(environmentValues.IsCloudy)} changed from {priorValues.PriorIsCloudy} to {environmentValues.IsCloudy}");
                isThereChange = true;
            }

            if (priorValues.PriorIsFoggy != environmentValues.IsFoggy)
            {
                LogInfo($"--- {nameof(environmentValues.IsFoggy)} changed from {priorValues.PriorIsFoggy} to {environmentValues.IsFoggy}");
                isThereChange = true;
            }

            //if (!isThereChange)
            //{
            //    // No change in settings and cloudiness and fogginess
            //    return;
            //}

            //LogDebug("------------------------------");
            //LogDebug("Updating Cloud and Fog");
            //LogDebug("------------------------------");
            //LogDebug($"The {nameof(environmentValues.IsCloudy)} is {environmentValues.IsCloudy}");
            //LogDebug($"The {nameof(environmentValues.IsFoggy)} is {environmentValues.IsFoggy}");
            //LogDebug($"The {nameof(configValues.DisableCloudsToggle)} is {configValues.DisableCloudsToggle}");
            //LogDebug($"The {nameof(configValues.DisableFogToggle)} is {configValues.DisableFogToggle}");

            // -----------------------------------------------------------------
            // Enable the clouds or fog if the user just unchecked the toggle

            //if (configValues.DisableCloudsToggle && !priorValues.PriorDisableCloudToggle)
            //{
            //    // The user unchecked the 'Disable Clouds' toggle, enable the clouds
            //    LogDebug("=== The user just checked the 'Disable Clouds' toggle, remove and disable the clouds");
            //    DisableClouds();
            //}

            //if (!configValues.DisableCloudsToggle && priorValues.PriorDisableCloudToggle)
            //{
            //    // The user unchecked the 'Disable Clouds' toggle, enable the clouds
            //    LogDebug("=== The user just unchecked the 'Disable Clouds' toggle, enable the clouds");
            //    EnableClouds();
            //}

            //if (configValues.DisableFogToggle && !priorValues.PriorDisableFogToggle)
            //{
            //    // The user unchecked the 'Disable Fog' toggle, enable the fog
            //    LogDebug("=== The user just checked the 'Disable Fog' toggle, remove and disable the fog");
            //    DisableFog();
            //}

            //if (!configValues.DisableFogToggle && priorValues.PriorDisableFogToggle)
            //{
            //    // The user unchecked the 'Disable Fog' toggle, enable the fog
            //    LogDebug("=== The user just unchecked the 'Disable Fog' toggle, enable the fog");
            //    EnableFog();
            //}

            // -----------------------------------------------------------------
            if (configValues.DisableCloudsToggle)
            {
                // The user unchecked the 'Disable Clouds' toggle, enable the clouds
                LogDebug("=== The 'Disable Clouds' toggle is true, remove and disable the clouds");
                DisableClouds();
            }

            if (!configValues.DisableCloudsToggle)
            {
                // The user unchecked the 'Disable Clouds' toggle, enable the clouds
                LogDebug("=== The 'Disable Clouds' toggle is false, enable the clouds");
                EnableClouds();
            }

            if (configValues.DisableFogToggle)
            {
                // The user unchecked the 'Disable Fog' toggle, enable the fog
                LogDebug("=== The 'Disable Fog' toggle is true, remove and disable the fog");
                DisableFog();
            }

            if (!configValues.DisableFogToggle)
            {
                // The user unchecked the 'Disable Fog' toggle, enable the fog
                LogDebug("=== The 'Disable Fog' toggle is false, enable the fog");
                EnableFog();
            }

            // -----------------------------------------------------------------

            // Control the clouds

            //// if (configValues.DisableCloudsToggle && environmentValues.IsCloudy)
            //if (configValues.DisableCloudsToggle)
            //    {
            //        // It is cloudy, disable the Clouds
            //        LogDebug("--- It is cloudy or not, disable the Clouds");
            //    DisableClouds();
            //}

            //// -----------------------------------------------------------------
            //// Control the fog

            //// if (configValues.DisableFogToggle && environmentValues.IsFoggy)
            //if (configValues.DisableFogToggle)
            //{
            //    // It is foggy, disable the Fog
            //    LogDebug("--- It is foggy or not, disable the Fog");
            //    DisableFog();
            //}

            // -----------------------------------------------------------------
            // Update the "prior" values

            priorValues.PriorDisableCloudToggle = configValues.DisableCloudsToggle;
            priorValues.PriorDisableFogToggle = configValues.DisableFogToggle;
            priorValues.PriorIsCloudy = environmentValues.IsCloudy;
            priorValues.PriorIsFoggy = environmentValues.IsFoggy;

            LogDebug($"*** {nameof(ControlCloudAndFog)} ran successfully");

        }


        // -----------------------------------------------------------------
        /// <summary>
        /// Enable everything
        /// </summary>
        private void EnableEverything()
        {
            EnablePrecipitation();
            EnableClouds();
            EnableFog();
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// Enable the precipitation
        /// </summary>
        private void EnablePrecipitation()
        {
            LogDebug("Enable Precipitation");
            _climateSystem.precipitation.overrideState = false;
        }

        /// <summary>
        /// Disable the precipitation
        /// </summary>
        private void DisablePrecipitation()
        {
            LogDebug("Disable Precipitation");
            _climateSystem.precipitation.overrideState = true;
            _climateSystem.precipitation.overrideValue = 0;
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// Remove existing snow
        /// </summary>
        private void RemoveSnow()
        {
            LogDebug("Remove existing snow");
            base.World.GetOrCreateSystemManaged<SnowSystem>().DebugReset();
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// Maximize the clouds
        /// </summary>
        private void MaximizeClouds()
        {
            LogDebug("Maximize Clouds");
            _climateSystem.cloudiness.overrideState = true;
            _climateSystem.cloudiness.overrideValue = 1;
        }

        /// <summary>
        /// Enable the clouds
        /// </summary>
        private void EnableClouds()
        {
            LogDebug("Enable Clouds");
            _climateSystem.cloudiness.overrideState = false;
        }

        /// <summary>
        /// Disable the clouds
        /// </summary>
        private void DisableClouds()
        {
            LogDebug("Disable Clouds");
            _climateSystem.cloudiness.overrideState = true;
            _climateSystem.cloudiness.overrideValue = 0;
        }

        // -----------------------------------------------------------------

        // Maximize the fog
        private void MaximizeFog()
        {
            LogDebug("Maximize Fog");
            _climateSystem.fog.overrideState = true;
            _climateSystem.fog.overrideValue = 1;
        }

        /// <summary>
        /// Enabl the fog 
        /// </summary>
        private void EnableFog()
        {
            LogDebug("Enable Fog");
            _climateSystem.fog.overrideState = false;
        }

        /// <summary>
        /// Disable the fog
        /// </summary>
        private void DisableFog()
        {
            LogDebug("Disable Fog");
            _climateSystem.fog.overrideState = true;
            _climateSystem.fog.overrideValue = 0;
            //_OLDclimateSystem.fog.overrideState = true;
            //_OLDclimateSystem.fog.overrideValue= 0;
        }

        // -----------------------------------------------------------------
        // -----------------------------------------------------------------
        // These methods are tiggered when we exit to the Desktop

        /// <summary>
        /// After OnDispose this is the last one to be triggered when exiting to Desktop
        /// </summary>
        protected override void OnDestroy()
        {
            LogInfo($"{nameof(OnDestroy)}");

            base.OnDestroy();
        }

        /// <summary>
        /// I cannot find this triggered in the log, it may happens when the mod is already destroyed
        /// </summary>
        public void OnGameExit()
        {
            LogInfo($"{nameof(OnGameExit)}");

            isInitialized = false;
        }

        // -----------------------------------------------------------------

        private void LogDebug(string msg)
        {
            if (_debug) {
                Mod.log.Debug(msg); 
            }
        }

        private void LogInfo(string msg)
        {
            Mod.log.Info(msg);
        }
    }

}
