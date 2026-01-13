// Quick Select Editor - A Unity Editor tool for organizing project assets
// Copyright (C) 2025  Thomas Mahler
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace QuickSelectEditor
{
    /// <summary>
    /// Partial class containing utility methods, sorting, grouping, and migration helpers.
    /// </summary>
    public partial class QuickSelectWindow
    {
        //Helper Methods:
        private void SortCategoriesAlphabetically()
        {
            categories = categories.OrderBy(c => c.name).ToList();
            
            // Also sort all children recursively
            foreach (var category in categories)
            {
                SortChildCategoriesAlphabetically(category);
            }
        }
        
        private void SortSubCategoriesAlphabetically(Category category)
        {
            category.subCategories = category.subCategories.OrderBy(c => c.name).ToList();
        }

        private void SortChildCategoriesAlphabetically(Category parentCategory)
        {
            parentCategory.children.Sort((c1, c2) => string.Compare(c1.name, c2.name, StringComparison.Ordinal));
            foreach (var childCategory in parentCategory.children)
            {
                if (childCategory.children.Count > 0)
                {
                    SortChildCategoriesAlphabetically(childCategory);
                }
            }
        }
        
        private Dictionary<string, List<string>> GroupFilesByType(Category category)
        {
            Dictionary<string, List<string>> fileTypeGroups = new Dictionary<string, List<string>>();

            for (int i = category.fileGUIDs.Count - 1; i >= 0; i--)
            {
                string guid = category.fileGUIDs[i];
                string filePath = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Object fileObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(filePath);

                if (fileObj == null)
                {
                    category.fileGUIDs.RemoveAt(i);
                    continue;
                }

                string groupKey;
                if (GroupAlphabetically)
                {
                    char firstChar = char.ToUpperInvariant(fileObj.name[0]);
                    groupKey = char.IsLetterOrDigit(firstChar) ? firstChar.ToString() : "Other";
                }
                else
                {
                    groupKey = Directory.Exists(filePath) ? "Folder" : SplitPascalCase(fileObj.GetType().Name);
                }

                if (!fileTypeGroups.ContainsKey(groupKey))
                {
                    fileTypeGroups[groupKey] = new List<string>();
                }

                fileTypeGroups[groupKey].Add(guid);
            }

            return fileTypeGroups;
        }

        /// <summary>
        /// Converts PascalCase to Title Case with spaces (e.g., "EntityViewDataAsset" -> "Entity View Data Asset").
        /// </summary>
        private string SplitPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var result = new StringBuilder(input.Length + 10);
            result.Append(input[0]);

            for (int i = 1; i < input.Length; i++)
            {
                if (char.IsUpper(input[i]))
                    result.Append(' ');
                result.Append(input[i]);
            }

            return result.ToString();
        }

        private Dictionary<string, List<string>> GroupFilesAlphabetically(Dictionary<string, List<string>> fileTypeGroups)
        {
            Dictionary<string, List<string>> alphabeticalGroups = new Dictionary<string, List<string>>();

            foreach (var fileTypeGroup in fileTypeGroups)
            {
                foreach (var guid in fileTypeGroup.Value)
                {
                    string fileName = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                    string firstChar = fileName.Substring(0, 1).ToUpper();

                    if (!char.IsLetter(firstChar[0]))
                    {
                        firstChar = "0-9";
                    }

                    if (!alphabeticalGroups.ContainsKey(firstChar))
                    {
                        alphabeticalGroups[firstChar] = new List<string>();
                    }

                    alphabeticalGroups[firstChar].Add(guid);
                }
            }

            return alphabeticalGroups;
        }
        
        private Dictionary<string, List<string>> GroupFilesByFolder(Category category)
        {
            Dictionary<string, List<string>> folderGroups = new Dictionary<string, List<string>>();

            for (int i = category.fileGUIDs.Count - 1; i >= 0; i--)
            {
                string guid = category.fileGUIDs[i];
                string filePath = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Object fileObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(filePath);

                if (fileObj == null)
                {
                    category.fileGUIDs.RemoveAt(i);
                    continue;
                }

                // Get the parent directory name
                string parentDirectoryPath = Path.GetDirectoryName(filePath);
                string parentDirectoryName = parentDirectoryPath != null ? ConvertCamelCaseToProperCase(Path.GetFileName(parentDirectoryPath)) : "No Folder";

                if (!folderGroups.ContainsKey(parentDirectoryName))
                {
                    folderGroups[parentDirectoryName] = new List<string>();
                }

                folderGroups[parentDirectoryName].Add(guid);
            }

            return folderGroups;
        }
        
        private string ConvertCamelCaseToProperCase(string input)
        {
            StringBuilder stringBuilder = new StringBuilder();

            foreach (char c in input)
            {
                if (char.IsUpper(c) && stringBuilder.Length > 0)
                    stringBuilder.Append(' ');

                stringBuilder.Append(c);
            }

            // Capitalize the first letter
            if (stringBuilder.Length > 0)
                stringBuilder[0] = char.ToUpperInvariant(stringBuilder[0]);

            return stringBuilder.ToString();
        }

        private bool GroupItems
        {
            get
            {
                string key = IsDocked()
                    ? "QuickSelectWindow_GroupItems_Docked"
                    : "QuickSelectWindow_GroupItems_Floating";
                return EditorPrefs.GetBool(key, true);
            }
            set
            {
                string key = IsDocked()
                    ? "QuickSelectWindow_GroupItems_Docked"
                    : "QuickSelectWindow_GroupItems_Floating";
                EditorPrefs.SetBool(key, value);
            }
        }
        
        private bool GroupByFileType
        {
            get
            {
                string key = IsDocked()
                    ? "QuickSelectWindow_GroupByFileType_Docked"
                    : "QuickSelectWindow_GroupByFileType_Floating";
                return EditorPrefs.GetBool(key, true);
            }
            set
            {
                string key = IsDocked()
                    ? "QuickSelectWindow_GroupByFileType_Docked"
                    : "QuickSelectWindow_GroupByFileType_Floating";
                EditorPrefs.SetBool(key, value);
            }
        }

        private bool GroupAlphabetically
        {
            get
            {
                string key = IsDocked()
                    ? "QuickSelectWindow_GroupAlphabetically_Docked"
                    : "QuickSelectWindow_GroupAlphabetically_Floating";
                return EditorPrefs.GetBool(key, false);
            }
            set
            {
                string key = IsDocked()
                    ? "QuickSelectWindow_GroupAlphabetically_Docked"
                    : "QuickSelectWindow_GroupAlphabetically_Floating";
                EditorPrefs.SetBool(key, value);
            }
        }
        
        private bool GroupByFolder
        {
            get
            {
                string key = IsDocked()
                    ? "QuickSelectWindow_GroupByFolder_Docked"
                    : "QuickSelectWindow_GroupByFolder_Floating";
                return EditorPrefs.GetBool(key, false);
            }
            set
            {
                string key = IsDocked()
                    ? "QuickSelectWindow_GroupByFolder_Docked"
                    : "QuickSelectWindow_GroupByFolder_Floating";
                EditorPrefs.SetBool(key, value);
            }
        }

        private float CalculateTextWidth(string text, int fontSize)
        {
            if (cachedTextWidthStyle == null)
            {
                cachedTextWidthStyle = new GUIStyle(GUI.skin.button);
            }
            cachedTextWidthStyle.fontSize = fontSize;
            Vector2 size = cachedTextWidthStyle.CalcSize(new GUIContent(text));
            return size.x;
        }

        private int AdjustFontSizeToFitWidth(string text, float availableWidth)
        {
            int fontSize = GUI.skin.button.fontSize;

            if (EditorPrefs.GetBool(QuickSelectEditorSettings.AdjustButtonFontsKey, true))
            {
                float textWidth = CalculateTextWidth(text, fontSize);

                while (textWidth > availableWidth && fontSize > 1)
                {
                    fontSize--;
                    textWidth = CalculateTextWidth(text, fontSize);
                }
            }

            return fontSize;
        }

        private void ShowFileButtonsSettingsContextMenu()
        {
            GenericMenu menu = new GenericMenu();

            // View submenu
            menu.AddItem(new GUIContent("View/Group Items"), GroupItems, () => GroupItems = !GroupItems);

            // Group By submenu
            if (GroupItems)
            {
                menu.AddItem(new GUIContent("Group By/File Type"), GroupByFileType, () =>
                {
                    GroupByFileType = true;
                    GroupAlphabetically = false;
                    GroupByFolder = false;
                });
                menu.AddItem(new GUIContent("Group By/Alphabetically"), GroupAlphabetically, () =>
                {
                    GroupByFileType = false;
                    GroupAlphabetically = true;
                    GroupByFolder = false;
                });
                menu.AddItem(new GUIContent("Group By/Folder"), GroupByFolder, () =>
                {
                    GroupByFileType = false;
                    GroupAlphabetically = false;
                    GroupByFolder = true;
                });
            }

            if (compactMode)
            {
                if (EditorPrefs.GetBool(QuickSelectEditorSettings.ShowLayoutModeKey, true))
                {
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Layout Mode/Personal"), !SharedLayoutMode, ToggleSharedLayoutMode);
                    menu.AddItem(new GUIContent("Layout Mode/Cloud"), SharedLayoutMode, ToggleSharedLayoutMode);
                }

                menu.AddSeparator("");
                for (int i = 0; i < categories.Count; i++)
                {
                    AddCategoryToMenu(menu, categories[i], "");
                }
            }
            menu.ShowAsContext();
        }

        private void AddCategoryToMenu(GenericMenu menu, Category category, string parentPath)
        {
            // Add a separator before each new top-level category
            if (string.IsNullOrEmpty(parentPath) && EditorPrefs.GetBool(QuickSelectEditorSettings.ShowCategoriesGroupKey, true))
            {
                menu.AddSeparator("Categories/");
            }

            bool isActiveCategory = (category.id == activeCategoryId);
            string categoryName = parentPath + category.name;

            if (EditorPrefs.GetBool(QuickSelectEditorSettings.ShowCategoriesGroupKey, true))
            {
                categoryName = "Categories/" + categoryName;
            }

            menu.AddItem(new GUIContent(categoryName), isActiveCategory,
                () => SelectCategory(category.id, category));

            if (category.children != null && category.children.Count > 0)
            {
                foreach (var child in category.children)
                {
                    // Call this method recursively for child categories, adding an indent to their name
                    AddCategoryToMenu(menu, child, parentPath + "  ");
                }
            }
        }

        private Category FindCategoryById(string id, List<Category> categoryList)
        {
            foreach (Category category in categoryList)
            {
                if (category.id == id)
                {
                    return category;
                }

                Category foundInSubcategories = FindCategoryById(id, category.subCategories);
                if (foundInSubcategories != null)
                {
                    return foundInSubcategories;
                }

                Category foundInChildren = FindCategoryById(id, category.children);
                if (foundInChildren != null)
                {
                    return foundInChildren;
                }
            }

            return null;
        }
        
        private void RemoveFileFromOriginalLocation(string guid, List<Category> categoryList)
        {
            foreach (Category category in categoryList)
            {
                if (category.fileGUIDs.Contains(guid))
                {
                    category.fileGUIDs.Remove(guid);
                    return;
                }

                RemoveFileFromOriginalLocation(guid, category.subCategories);
                RemoveFileFromOriginalLocation(guid, category.children);
            }
        }
        
        
        // Migration helper: regenerate IDs if legacy layout data is missing them.
        private void CheckAndUpdateCategoryIDs()
        {
            // Start with the root categories
            foreach (Category category in categories)
            {
                // If this category doesn't have an ID, update all IDs and return
                if (string.IsNullOrEmpty(category.id))
                {
                    UpdateCategoryIDs();
                    return;
                }

                // Check the subcategories
                foreach (Category subCategory in category.subCategories)
                {
                    if (string.IsNullOrEmpty(subCategory.id))
                    {
                        UpdateCategoryIDs();
                        return;
                    }

                    // Check the child categories
                    foreach (Category childCategory in subCategory.children)
                    {
                        if (string.IsNullOrEmpty(childCategory.id))
                        {
                            UpdateCategoryIDs();
                            return;
                        }
                    }
                }
            }
        }

        private void UpdateCategoryIDs()
        {
            foreach (Category category in categories)
            {
                UpdateIDRecursive(category);
            }

            // Don't forget to save the categories after updating the IDs
            SaveCategories();
        }

        private void UpdateIDRecursive(Category category)
        {
            category.id = Guid.NewGuid().ToString();

            foreach (Category subCategory in category.subCategories)
            {
                UpdateIDRecursive(subCategory);
            }

            foreach (Category child in category.children)
            {
                UpdateIDRecursive(child);
            }
        }
    }
}
