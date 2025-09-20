using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ariko
{
    [InitializeOnLoad]
    public static class ArikoInfo
    {
        public static readonly string Version;

        static ArikoInfo()
        {
            var packageJsonPath = "Packages/io.github.pixel-clover.ariko/package.json";
            if (!File.Exists(packageJsonPath))
            {
                Version = "N/A";
                return;
            }

            var packageJson = File.ReadAllText(packageJsonPath);
            var package = JsonUtility.FromJson<Package>(packageJson);
            Version = package.version;
        }

        private class Package
        {
            public string version;
        }
    }
}
