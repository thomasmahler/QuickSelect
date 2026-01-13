// Quick Select Editor - A Unity Editor tool for organizing project assets
// Copyright (C) 2025  Thomas Mahler
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using UnityEditor;

namespace QuickSelectEditor
{
    /// <summary>
    /// Partial class containing save/load and persistence functionality.
    /// </summary>
    public partial class QuickSelectWindow
    {
        //Saving / Loading:
        private const string FloatingLayoutModeKey = "QuickSelectWindow_SharedLayoutMode_Floating";
        private const string DockedLayoutModeKey = "QuickSelectWindow_SharedLayoutMode_Docked";
        
        // Flag to suppress file watcher reload during saves (prevents race condition)
        public static bool s_isSaving;

        private void SaveCategories()
        {
            s_isSaving = true;
            InvalidateFileGroupingCache();
            
            if (SharedLayoutMode)
            {
                EditorPrefsUtility.SaveSharedCategories(categories);
            }
            else
            {
                EditorPrefsUtility.SavePersonalCategories(categories);
            }

            SaveWindowState();
            RequestRepaintOtherInstances();
            
            // Reset flag after a delay to allow file watcher events to be ignored
            EditorApplication.delayCall += () => { s_isSaving = false; };
        }

        private void LoadCategories()
        {
            InvalidateFileGroupingCache();
            
            if (SharedLayoutMode)
            {
                categories = EditorPrefsUtility.LoadSharedCategories();
            }
            else
            {
                categories = EditorPrefsUtility.LoadPersonalCategories();
            }

            SortCategoriesAlphabetically();
        }

        private void SaveWindowState()
        {
            bool sharedLayout = SharedLayoutMode;
            bool floatingInstance = !IsDocked();

            //Save the active categories / subcategories:
            if (!string.IsNullOrEmpty(activeCategoryId))
            {
                EditorPrefsUtility.SaveActiveCategory(sharedLayout, floatingInstance, activeCategoryId);
                Category activeCategory = FindCategoryById(activeCategoryId, categories);
                if (activeCategory != null)
                {
                    EditorPrefsUtility.SaveActiveSubcategory(sharedLayout, floatingInstance, activeCategory.id,
                        activeSubcategoryId);
                }
            }
            else
            {
                EditorPrefsUtility.SaveActiveCategory(sharedLayout, floatingInstance, Guid.Empty.ToString());
            }
            
            //Save the expanded state:
            foreach (var category in categories)
            {
                bool isExpanded = EditorPrefsUtility.LoadExpandedState(sharedLayout, floatingInstance, category.id);
                EditorPrefsUtility.SaveExpandedState(sharedLayout, floatingInstance, category.id, isExpanded);
            }
        }

        private void LoadWindowState()
        {
            bool sharedLayout = SharedLayoutMode;
            bool floatingInstance = !IsDocked();

            // Load the active categories / subcategories:
            string loadedActiveCategoryId = EditorPrefsUtility.LoadActiveCategory(sharedLayout, floatingInstance);

            if (!string.IsNullOrEmpty(loadedActiveCategoryId) && loadedActiveCategoryId != Guid.Empty.ToString())
            {
                activeCategoryId = loadedActiveCategoryId;

                Category activeCategory = FindCategoryById(activeCategoryId, categories);
                if (activeCategory != null)
                {
                    // Try to load the saved subcategory selection
                    string savedSubcategoryId = EditorPrefsUtility.LoadActiveSubcategory(sharedLayout, floatingInstance, activeCategory.id);
                    
                    // Check if the saved subcategory is valid (exists in this category)
                    bool isValidSavedSelection = !string.IsNullOrEmpty(savedSubcategoryId) && 
                        activeCategory.subCategories != null && 
                        activeCategory.subCategories.Exists(sc => sc.id == savedSubcategoryId);
                    
                    if (isValidSavedSelection)
                    {
                        activeSubcategoryId = savedSubcategoryId;
                    }
                    else if (activeCategory.subCategories != null && activeCategory.subCategories.Count > 0)
                    {
                        // Fall back to the first subcategory
                        activeSubcategoryId = activeCategory.subCategories[0].id;
                    }
                    else
                    {
                        activeSubcategoryId = null;
                    }
                }
            }
            else
            {
                activeCategoryId = null;
            }
        }

        private void ToggleSharedLayoutMode()
        {
            // Save the current layout before toggling
            SaveCategories();

            // Toggle the mode
            SharedLayoutMode = !SharedLayoutMode;

            // Load the new layout based on the updated mode
            LoadCategories();

            // Load the active indices for the new mode
            LoadWindowState();
        }
    }
}
