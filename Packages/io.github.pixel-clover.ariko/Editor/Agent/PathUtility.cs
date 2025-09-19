using System.IO;
using UnityEngine;

namespace Ariko.Editor.Agent
{
    /// <summary>
    ///     Provides utility methods for handling file paths in a safe manner.
    /// </summary>
    public static class PathUtility
    {
        /// <summary>
        ///     Checks if a given path is safe to use by ensuring it is within the project's Assets folder.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <param name="fullPath">The full, normalized path.</param>
        /// <returns>True if the path is within the Assets folder, false otherwise.</returns>
        public static bool IsPathSafe(string path, out string fullPath)
        {
            fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, path));
            var assetsPath = Path.GetFullPath(Application.dataPath);

            return fullPath.StartsWith(assetsPath);
        }
    }
}
