namespace TrafficManager {
    using ColossalFramework.UI;
    using ColossalFramework;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using System.Reflection;
    using System;
    using TrafficManager.RedirectionFramework;
    using HarmonyLib;

    // must not be refrenced before cities harmony is installed.
    public class Patcher {
        public static Patcher Instance;

        // Must not call this before harmony cities harmony is installed.
        public static Patcher Create() => Instance = new Patcher();

        private const string HARMONY_ID = "de.viathinksoft.tmpe";

        public static bool DetourInited { get; set; }

        /// <summary>
        /// Method redirection states for attribute-driven detours
        /// </summary>
        public static IDictionary<MethodInfo, RedirectCallsState> DetouredMethodStates {
            get;
            private set;
        } = new Dictionary<MethodInfo, RedirectCallsState>();

        public void RevertDetours() {
            if (!DetourInited) {
                return;
            }

            Log.Info("Reverting attribute-driven detours");
            AssemblyRedirector.Revert();

            Log.Info("Reverting Harmony detours");
            var harmony = new Harmony(HARMONY_ID);
            harmony.UnpatchAll();

            DetourInited = false;
            Log.Info("Reverting detours finished.");
        }

        public void InitDetours() {
            // TODO realize detouring with annotations
            if (DetourInited) {
                return;
            }

            Log.Info("Init detours");
            bool detourFailed = false;

            try {
                Log.Info("Deploying Harmony patches");
#if DEBUG
                Harmony.DEBUG = true;
#endif
                Assembly assembly = Assembly.GetExecutingAssembly();

                // Harmony attribute-driven patching
                Log.Info($"Performing Harmony attribute-driven patching");
                var harmony = new Harmony(HARMONY_ID);
                harmony.PatchAll(assembly);

                // Harmony manual patching
                Log.Info($"Performing Harmony manual patching");
            }
            catch (Exception e) {
                Log.Error("Could not deploy Harmony patches");
                Log.Info(e.ToString());
                Log.Info(e.StackTrace);
                detourFailed = true;
            }

            try {
                Log.Info("Deploying attribute-driven detours");
                DetouredMethodStates = AssemblyRedirector.Deploy();
            }
            catch (Exception e) {
                Log.Error("Could not deploy attribute-driven detours");
                Log.Info(e.ToString());
                Log.Info(e.StackTrace);
                detourFailed = true;
            }

            if (detourFailed) {
                Log.Info("Detours failed");
                Singleton<SimulationManager>.instance.m_ThreadingWrapper.QueueMainThread(
                    () => {
                        UIView.library
                              .ShowModal<ExceptionPanel>("ExceptionPanel")
                              .SetMessage(
                                "TM:PE failed to load",
                                "Traffic Manager: President Edition failed to load. You can " +
                                "continue playing but it's NOT recommended. Traffic Manager will " +
                                "not work as expected.",
                                true);
                    });
            } else {
                Log.Info("Detours successful");
            }

            DetourInited = true;
        }
    }
}
