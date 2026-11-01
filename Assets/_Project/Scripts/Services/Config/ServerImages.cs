using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MafiaNight.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace MafiaNight.Services.Config
{
    /// <summary>
    /// The pictures the admin board uploads (private messages, offers, events): /v1/img/&lt;id&gt; of our server. A picture's id is random and never
    /// changes its picture, so it is kept for the rest of the run in memory and on the device (persistentDataPath/serverimg): the second time it
    /// costs no download, and it still shows with no connection.
    /// </summary>
    public static class ServerImages
    {
        private static readonly Regex IdRe = new Regex("^[a-f0-9]{24}$");
        private static readonly Dictionary<string, Texture2D> memory = new Dictionary<string, Texture2D>();
        private static readonly HashSet<string> loading = new HashSet<string>();

        private static string Base => (GameConstants.ApiUrl ?? "").TrimEnd('/');
        private static string Dir => Path.Combine(Application.persistentDataPath, "serverimg");

        public static string Url(string id) => IdRe.IsMatch(id ?? "") && Base.Length > 0 ? Base + "/v1/img/" + id : null;

        /// <summary>The callback gets the picture, or null when the id is wrong or the picture cannot be had.</summary>
        public static IEnumerator Load(string id, Action<Texture2D> done)
        {
            if (!IdRe.IsMatch(id ?? "")) { done?.Invoke(null); yield break; }
            if (memory.TryGetValue(id, out var have) && have != null) { done?.Invoke(have); yield break; }
            while (loading.Contains(id)) yield return null;                      // another place asked for the same picture a moment ago
            if (memory.TryGetValue(id, out have) && have != null) { done?.Invoke(have); yield break; }
            loading.Add(id);
            Texture2D tex = null;
            try
            {
                string file = Path.Combine(Dir, id);
                if (File.Exists(file)) { tex = Decode(File.ReadAllBytes(file)); if (tex == null) File.Delete(file); }
                if (tex == null)
                {
                    string url = Url(id);
                    if (url != null)
                    {
                        using (var req = UnityWebRequest.Get(url))
                        {
                            req.timeout = 15;
                            yield return req.SendWebRequest();
                            if (req.result == UnityWebRequest.Result.Success)
                            {
                                tex = Decode(req.downloadHandler.data);
                                if (tex != null) { try { Directory.CreateDirectory(Dir); File.WriteAllBytes(file, req.downloadHandler.data); } catch (Exception) { /* the picture still shows */ } }
                            }
                        }
                    }
                }
            }
            finally { loading.Remove(id); }
            if (tex != null) memory[id] = tex;
            done?.Invoke(tex);
        }

        private static Texture2D Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            if (!tex.LoadImage(bytes, true)) { UnityEngine.Object.Destroy(tex); return null; }
            return tex;
        }
    }
}
