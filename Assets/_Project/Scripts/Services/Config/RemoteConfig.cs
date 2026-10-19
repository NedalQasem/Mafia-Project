using System;
using System.Collections.Generic;
using MafiaNight.Core;
using MafiaNight.Services.Telemetry;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace MafiaNight.Services.Config
{
    /// <summary>
    /// Settings changed from the PlayFab dashboard (Title Data, key "AppConfig") without shipping a new build:
    /// minimum app version, maintenance mode, per-feature kill switches and the PlayFab call limits.
    /// </summary>
    [Serializable]
    public sealed class AppConfigData
    {
        public string minVersion = "";                 // e.g. "0.3.0" - older builds are asked to update
        public bool maintenance;
        public string maintenanceMessage = "";
        public string[] disabled = new string[0];      // feature keys switched off, e.g. "chat", "leaderboards", "inbox", "groups", "store"
        public int readsPerMinute;                     // 0 = keep the default
        public int writesPerMinute;
        public bool freezeWrites;                      // emergency: refuse every PlayFab write from clients
    }

    public interface IRemoteConfig
    {
        AppConfigData Data { get; }
        /// <summary>False when the app version is older than the configured minimum.</summary>
        bool IsVersionSupported { get; }
        bool IsEnabled(string feature);
        /// <summary>Downloads the config. Never fails the caller: on error the cached (or default) config is kept.</summary>
        void Fetch(Action<bool> done);
        event Action OnUpdated;
    }

    public sealed class RemoteConfigService : IRemoteConfig
    {
        private const string Tag = "RemoteConfig";
        private const string Key = "AppConfig";
        private const string CachePref = "MafiaNight.AppConfig";

        public AppConfigData Data { get; private set; } = new AppConfigData();
        public event Action OnUpdated;

        public RemoteConfigService()
        {
            string cached = PlayerPrefs.GetString(CachePref, "");
            if (!string.IsNullOrEmpty(cached)) Apply(cached, false);
        }

        public bool IsVersionSupported => string.IsNullOrEmpty(Data.minVersion) || Compare(Application.version, Data.minVersion) >= 0;

        public bool IsEnabled(string feature)
        {
            if (Data.disabled == null) return true;
            return Array.IndexOf(Data.disabled, feature) < 0;
        }

        public void Fetch(Action<bool> done)
        {
            PlayFabMeter.Allow("GetTitleData", false);
            PlayFabClientAPI.GetTitleData(new GetTitleDataRequest { Keys = new List<string> { Key } },
                result =>
                {
                    if (result.Data != null && result.Data.TryGetValue(Key, out string json) && !string.IsNullOrEmpty(json))
                    {
                        PlayerPrefs.SetString(CachePref, json);
                        Apply(json, true);
                    }
                    done?.Invoke(true);
                },
                error =>
                {
                    Log.Warning("Could not fetch app config: " + error.ErrorMessage + " (keeping the cached one)", Tag);
                    done?.Invoke(false);
                });
        }

        private void Apply(string json, bool notify)
        {
            try
            {
                var parsed = JsonUtility.FromJson<AppConfigData>(json);
                if (parsed == null) return;
                Data = parsed;
                if (parsed.readsPerMinute > 0) PlayFabMeter.ReadLimitPerMinute = parsed.readsPerMinute;
                if (parsed.writesPerMinute > 0) PlayFabMeter.WriteLimitPerMinute = parsed.writesPerMinute;
                PlayFabMeter.WritesFrozen = parsed.freezeWrites;
                if (notify) OnUpdated?.Invoke();
            }
            catch (Exception e)
            {
                Log.Warning("Bad AppConfig JSON: " + e.Message, Tag);
            }
        }

        /// <summary>Compares "1.2.3" style versions; missing parts count as 0.</summary>
        public static int Compare(string a, string b)
        {
            var pa = (a ?? "0").Split('.'); var pb = (b ?? "0").Split('.');
            int n = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                int.TryParse(i < pa.Length ? pa[i] : "0", out int x);
                int.TryParse(i < pb.Length ? pb[i] : "0", out int y);
                if (x != y) return x.CompareTo(y);
            }
            return 0;
        }
    }
}
