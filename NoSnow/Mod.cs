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
        public ClimateSystem _climateSystem;
        private DateTime _lastUpdateTime;
        private TimeSpan _lastUpdateTimeInterval = new TimeSpan(0,0,30); // 30 seconds
        private float _priorAverageTemperature;
        private float _priorFreezingTemperature;
        private bool _priorDisableRainToggle;
        private bool _priorDisableSnowToggle;

        // Constructor
        public NoSnowSystem(Mod mod)
        {
            _mod = mod;
        }

        protected override void OnCreate()
        {
            base.OnCreate();

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

            // Instantiate the Climate System to control the weather
            _climateSystem = World.GetExistingSystemManaged<ClimateSystem>();
            Mod.log.Info("Climate System found");

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

            if (_climateSystem == null)
            {
                // Climate system is not yet initialized, return 
                return;
            }


            Mod.log.Info($"Now: {now}, Last update time: {_lastUpdateTime}, Elapsed time: {elapsedTime}, _lastUpdateTimeInterval: {_lastUpdateTimeInterval}");

            // Get the config values
            bool disableRainToggle = _mod.m_Setting.DisableRainToggle;
            bool disableSnowToggle = _mod.m_Setting.DisableSnowToggle;

            // Get the freezing and average temperatures
            float freezingTemperature = _climateSystem.freezingTemperature;
            Mod.log.Info($"The freezing temperature is {freezingTemperature}");
            float averageTemperature = _climateSystem.averageTemperature;
            Mod.log.Info($"The averageTemperature temperature is {averageTemperature}");

            // Control the precipitation
            ControlPrecipitation(disableRainToggle, disableSnowToggle, freezingTemperature, averageTemperature);

            // Update the last update time
            _lastUpdateTime = now;
            Mod.log.Info($"Now: {now}, Last update time: {_lastUpdateTime}");
        }

        // Control the precipitation based on the config settings
        private void ControlPrecipitation(bool disableRainToggle, bool disableSnowToggle, float freezingTemperature, float averageTemperature)
        {


            if (_priorDisableRainToggle == disableRainToggle
                && _priorDisableSnowToggle == disableSnowToggle
                && _priorFreezingTemperature == freezingTemperature
                && _priorAverageTemperature == averageTemperature)
            {
                // No change in settings and temperature
                return;
            }

            Mod.log.Info("Updating the precipitation");

            // Initialize the Climate System state
            _climateSystem.precipitation.overrideState = false;

            if (disableRainToggle == true && averageTemperature > freezingTemperature)
            {
                // The average temperature is above freezing, disable the rain
                Mod.log.Info("Disable the rain");
                _climateSystem.precipitation.overrideState = true;
                _climateSystem.precipitation.overrideValue = 0;
            }

            if (disableSnowToggle == true && averageTemperature <= freezingTemperature)
            {
                // The average temperature is at or below freezing, disable the snow
                Mod.log.Info("Disable the snow");
                _climateSystem.precipitation.overrideState = true;
                _climateSystem.precipitation.overrideValue = 0;
            }

            // Update the "prior" values
            _priorDisableRainToggle = disableRainToggle;
            _priorDisableSnowToggle = disableSnowToggle;
            _priorFreezingTemperature = freezingTemperature;
            _priorAverageTemperature = averageTemperature;

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
