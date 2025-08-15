using System;
using System.Linq;
using System.Reflection;
using Modding;
using UnityEngine;

namespace PaleCourtCharms
{   //Giving this one an individual file because it needs to be referenced in two different files
    internal static class FloristInterop
    {
        private static bool _initialized = false;
        private static bool _available = false;
        private static object _instance = null;
        private static int _num = -1;
        private static bool _cachedEquipped = false;

        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            TryFindFlorist();
            UpdateCache();
            ModHooks.CharmUpdateHook += OnCharmUpdate;
        }

        private static void TryFindFlorist()
        {
            try
            {
                var modObj = ModHooks.GetMod("Transcendence");
                Assembly asm = null;
                if (modObj is Mod modInstance) asm = modInstance.GetType().Assembly;
                else
                {
                    asm = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a =>
                        {
                            try { return a.GetType("Transcendence.FloristsBlessing") != null; }
                            catch { return false; }
                        });
                }
                if (asm == null) { _available = false; return; }

                var t = asm.GetType("Transcendence.FloristsBlessing");
                if (t == null) { _available = false; return; }

                var instanceProp = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp != null) _instance = instanceProp.GetValue(null);
                else
                {
                    var instanceField = t.GetField("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                    if (instanceField != null) _instance = instanceField.GetValue(null);
                }

                if (_instance == null) { _available = false; return; }

                var numProp = _instance.GetType().GetProperty("Num", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (numProp != null)
                {
                    _num = (int)numProp.GetValue(_instance);
                    _available = true;
                    return;
                }

                var numField = _instance.GetType().GetField("Num", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (numField != null)
                {
                    _num = (int)numField.GetValue(_instance);
                    _available = true;
                    return;
                }
            }
            catch (Exception e)
            {
                Modding.Logger.Log("[PC_Charms][FloristInterop] init failed: " + e.Message);
                _available = false;
            }
        }

        private static void OnCharmUpdate(PlayerData pd, HeroController hc)
        {
            UpdateCache();
        }

        private static void UpdateCache()
        {
            if (!_available || _num <= 0 || PlayerData.instance == null)
            {
                _cachedEquipped = false;
                return;
            }
            try
            {
                _cachedEquipped = PlayerData.instance.GetBool($"equippedCharm_{_num}");
            }
            catch
            {
                _cachedEquipped = false;
            }
        }

        public static bool IsEquipped()
        {
            if (!_initialized) Init();
            return _cachedEquipped;
        }
    }
}
