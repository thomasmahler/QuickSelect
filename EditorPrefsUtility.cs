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

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace QuickSelectEditor
{
    /// <summary>
    /// Utility class for saving and loading Quick Select categories and preferences.
    /// Supports both personal (EditorPrefs) and shared (JSON file) storage modes.
    /// </summary>
    public static class EditorPrefsUtility
    {
        // Personal (EditorPrefs) storage key. Project-specific to avoid layouts bleeding across projects.
        private static string CategoriesKey => GetProjectSpecificKey("QuickSelectCategories");

        public static void SavePersonalCategories(List<Category> categories)
        {
            string json = JsonUtility.ToJson(new SerializableList<Category> { List = categories });
            EditorPrefs.SetString(CategoriesKey, json);
        }

        public static void SaveSharedCategories(List<Category> categories)
        {
            string json = JsonUtility.ToJson(new SerializationWrapper<List<Category>> { Value = categories }, prettyPrint: true);
            string projectRootPath = Path.GetDirectoryName(Application.dataPath);
            File.WriteAllText(Path.Combine(projectRootPath, "SharedLayout.json"), json);
        }

        public static List<Category> LoadPersonalCategories()
        {
            string json = EditorPrefs.GetString(CategoriesKey, "");
            if (string.IsNullOrEmpty(json))
            {
                return new List<Category>();
            }

            SerializableList<Category> serializedCategories = JsonUtility.FromJson<SerializableList<Category>>(json);
            return serializedCategories.List;
        }

        public static List<Category> LoadSharedCategories()
        {
            string projectRootPath = Path.GetDirectoryName(Application.dataPath);
            string sharedLayoutFilePath = Path.Combine(projectRootPath, "SharedLayout.json");

            if (File.Exists(sharedLayoutFilePath))
            {
                string json = File.ReadAllText(sharedLayoutFilePath);
                SerializationWrapper<List<Category>> wrapper = JsonUtility.FromJson<SerializationWrapper<List<Category>>>(json);
                return wrapper.Value;
            }

            return new List<Category>();
        }

        [System.Serializable]
        public class SerializableList<T>
        {
            public List<T> List;
        }

        [System.Serializable]
        public class SerializationWrapper<T>
        {
            [SerializeField]
            private T _value;

            public T Value
            {
                get => _value;
                set => _value = value;
            }
        }

        //Get Project / Prefs Keys:
        public static string GetProjectSpecificKey(string key)
        {
            return $"{PlayerSettings.productName}-{key}";
        }

        //Category Keys:
        public static string GetActiveCategoryKey(bool sharedLayout, bool floatingInstance)
        {
            string key = "QuickSelectWindow_ActiveCategory_" + (sharedLayout ? "Shared" : "Personal") +
                         (floatingInstance ? "_Floating" : "_Docked");
            return GetProjectSpecificKey(key);
        }

        public static void SaveActiveCategory(bool sharedLayout, bool floatingInstance, string activeCategoryId)
        {
            string key = GetActiveCategoryKey(sharedLayout, floatingInstance);
            EditorPrefs.SetString(key, activeCategoryId);
        }

        public static string LoadActiveCategory(bool sharedLayout, bool floatingInstance)
        {
            string key = GetActiveCategoryKey(sharedLayout, floatingInstance);
            return EditorPrefs.GetString(key, "");
        }

        //Expanded State Keys:
        public static string GetExpandedStateKey(bool sharedLayout, bool floatingInstance, string categoryGuid)
        {
            string key = "QuickSelectWindow_ExpandedState_" + (sharedLayout ? "Shared" : "Personal") +
                         (floatingInstance ? "_Floating" : "_Docked") + "_" + categoryGuid;
            return GetProjectSpecificKey(key);
        }

        public static void SaveExpandedState(bool sharedLayout, bool floatingInstance, string categoryGuid, bool isExpanded)
        {
            EditorPrefs.SetBool(GetExpandedStateKey(sharedLayout, floatingInstance, categoryGuid), isExpanded);
        }

        public static bool LoadExpandedState(bool sharedLayout, bool floatingInstance, string categoryGuid)
        {
            return EditorPrefs.GetBool(GetExpandedStateKey(sharedLayout, floatingInstance, categoryGuid), false);
        }

        //Subcategory Keys:
        public static string GetActiveSubcategoryKey(bool sharedLayout, bool floatingInstance, string categoryGuid)
        {
            string key = "QuickSelectWindow_ActiveSubcategory_" + (sharedLayout ? "Shared" : "Personal") +
                         (floatingInstance ? "_Floating" : "_Docked") + "_" + categoryGuid;
            return GetProjectSpecificKey(key);
        }

        public static void SaveActiveSubcategory(bool sharedLayout, bool floatingInstance, string categoryGuid, string activeSubcategoryId)
        {
            EditorPrefs.SetString(GetActiveSubcategoryKey(sharedLayout, floatingInstance, categoryGuid), activeSubcategoryId ?? "");
        }

        public static string LoadActiveSubcategory(bool sharedLayout, bool floatingInstance, string categoryGuid)
        {
            return EditorPrefs.GetString(GetActiveSubcategoryKey(sharedLayout, floatingInstance, categoryGuid), "");
        }
    }
}
