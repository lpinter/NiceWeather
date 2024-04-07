using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.Simulation;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace NoSnow
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(NoSnow)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        public Setting m_Setting;
        public NoSnow _noSnow;

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");


            if (_noSnow == null)
            {
                // Instantiate NoSnow
                _noSnow = new NoSnow(this);
            }

            World.DefaultGameObjectInjectionWorld.AddSystemManaged(_noSnow);



            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));

            AssetDatabase.global.LoadSettings(nameof(NoSnow), m_Setting, new Setting(this));
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

    public partial class NoSnow : GameSystemBase
    {
        public bool isInitialized = false;
        public Mod _mod;

        // Constructor
        public NoSnow(Mod mod)
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

            if (_mod.m_Setting.DisableRainToggle == true)
            {
                Mod.log.Info("Disable rain toggle = true");
            }
            else
            {
                Mod.log.Info("Disable rain toggle = false");
            }
        }

        protected override void OnUpdate()
        {

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
