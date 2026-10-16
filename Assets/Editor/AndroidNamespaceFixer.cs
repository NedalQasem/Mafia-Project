using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

class AndroidNamespaceFixer : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 0;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        // Path to the eos_dependencies library build.gradle
        // Note: The structure might vary, we search recursively or target specific path
        string eosLibPath = Path.Combine(path, @"unityLibrary\eos_dependencies.androidlib\build.gradle");
        
        // Sometimes the path is different based on Unity version export, let's try to find it
        if (!File.Exists(eosLibPath))
        {
            // Try to find it in the generated project
            var files = Directory.GetFiles(path, "build.gradle", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (file.Contains("eos_dependencies"))
                {
                    eosLibPath = file;
                    break;
                }
            }
        }

        if (File.Exists(eosLibPath))
        {
            Debug.Log($"[AndroidNamespaceFixer] Fixing namespace in: {eosLibPath}");
            string content = File.ReadAllText(eosLibPath);

            // Check if namespace is already there
            if (!content.Contains("namespace '"))
            {
                // We inject the namespace block inside 'android { ... }'
                // The namespace usually matches the package name from manifest, or we can use a generic one
                string namespaceLine = "\n    namespace 'com.epicgames.mobile.auth'\n"; // EOS Auth namespace
                
                // Insert after "android {"
                if (content.Contains("android {"))
                {
                    content = content.Replace("android {", "android {" + namespaceLine);
                    File.WriteAllText(eosLibPath, content);
                    Debug.Log("[AndroidNamespaceFixer] Fixed Successfully!");
                }
            }
        }
        else
        {
             // Fallback for general libraries missing namespace
             Debug.LogWarning("[AndroidNamespaceFixer] Could not find specific EOS lib, checking all.");
        }
    }
}