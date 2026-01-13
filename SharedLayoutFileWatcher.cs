// Quick Select Editor - A Unity Editor tool for organizing project assets
// Copyright (C) 2025  Thomas Mahler
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.IO;
using UnityEditor;
using UnityEngine;

namespace QuickSelectEditor
{
    /// <summary>
    /// Watches for changes to the SharedLayout.json file and triggers UI updates
    /// when the shared layout is modified externally.
    /// </summary>
    public static class SharedLayoutFileWatcher
    {
        private static FileSystemWatcher layoutFileWatcher;

        public static void StartWatching()
        {
            if (layoutFileWatcher != null) return;

            string projectRootPath = Path.GetDirectoryName(Application.dataPath);
            string sharedLayoutFilePath = Path.Combine(projectRootPath, "SharedLayout.json");

            layoutFileWatcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(sharedLayoutFilePath),
                Filter = Path.GetFileName(sharedLayoutFilePath),
                NotifyFilter = NotifyFilters.LastWrite
            };

            layoutFileWatcher.Changed += OnSharedLayoutFileChanged;
            layoutFileWatcher.Created += OnSharedLayoutFileChanged;
            layoutFileWatcher.EnableRaisingEvents = true;
        }

        public static void StopWatching()
        {
            if (layoutFileWatcher == null) return;

            layoutFileWatcher.Changed -= OnSharedLayoutFileChanged;
            layoutFileWatcher.Created -= OnSharedLayoutFileChanged;
            layoutFileWatcher.Dispose();
            layoutFileWatcher = null;
        }

        private static void OnSharedLayoutFileChanged(object sender, FileSystemEventArgs e)
        {
            // This will run on a separate thread, so make sure to use EditorApplication.delayCall
            // to schedule the method to be run on the main Unity thread.
            EditorApplication.delayCall += QuickSelectWindow.RequestRepaintAllInstances;
        }
    }
}