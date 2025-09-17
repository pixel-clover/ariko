using System.IO;
using UnityEngine;

namespace Ariko.Editor.Agent
{
    public static class PathUtility
    {
        public static bool IsPathSafe(string path, out string fullPath)
        {
            fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, path));
            var assetsPath = Path.GetFullPath(Application.dataPath);

            return fullPath.StartsWith(assetsPath);
        }
    }
}
