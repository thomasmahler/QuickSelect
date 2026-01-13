// Quick Select Editor - A Unity Editor tool for organizing project assets
// Copyright (C) 2025  Thomas Mahler
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace QuickSelectEditor
{
    /// <summary>
    /// Partial class containing subcategory tabs and management.
    /// </summary>
    public partial class QuickSelectWindow
    {
        //The following methods are about the Subcategories on the right side of the Editor:
        private void DrawCategoryContent()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            Category selectedCategory = FindCategoryById(activeCategoryId, categories);
            if (selectedCategory != null)
            {
                DrawSubCategoryTitle();
                DrawSubcategoriesList();

                Category categoryToDrawFiles;
                Category activeSubcategory = selectedCategory.subCategories.Find(sc => sc.id == activeSubcategoryId);
                if (activeSubcategory != null)
                {
                    categoryToDrawFiles = activeSubcategory;
                }
                else
                {
                    categoryToDrawFiles = selectedCategory;
                }

                HandleDragAndDrop(selectedCategory);
                DrawFileButtons(categoryToDrawFiles);
                DrawAddSubCategoryButton();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSubCategoryTitle()
        {
            Category selectedCategory = FindCategoryById(activeCategoryId, categories);
            if (!compactMode && selectedCategory != null)
            {
                EditorGUILayout.LabelField(selectedCategory.name, EditorStyles.boldLabel);
            }
        }

        private void DrawSubcategoriesList()
        {
            Category selectedCategory = FindCategoryById(activeCategoryId, categories);

            if (selectedCategory != null)
            {
                int subCategoriesCount = selectedCategory.subCategories.Count;
                int col = 0;

                for (int i = 0; i < subCategoriesCount; i++)
                {
                    if (col == 0)
                    {
                        EditorGUILayout.BeginHorizontal();
                    }

                    DrawSubCategoryButton(selectedCategory, i, ref col);

                    if (col * 120 > EditorGUIUtility.currentViewWidth - 130 - (compactMode ? 0 : 210) ||
                        i == subCategoriesCount - 1)
                    {
                        EditorGUILayout.EndHorizontal();
                        col = 0;
                    }
                }
            }
        }

        private void DrawSubCategoryButton(Category category, int subCategoryIndex, ref int col)
        {
            string draggedGUID = null;
            Category subCategory = category.subCategories[subCategoryIndex];
            GUIContent subCategoryButtonContent = CreateSubCategoryButtonContent(subCategory);

            if (cachedSubCategoryButtonStyle == null)
            {
                cachedSubCategoryButtonStyle = new GUIStyle(GUI.skin.button);
                cachedSubCategoryButtonStyle.fixedHeight = 20;
                cachedSubCategoryButtonStyle.alignment = TextAnchor.MiddleCenter;
            }
            
            int adjustedFontSize = AdjustFontSizeToFitWidth($"{subCategory.name} ({subCategory.fileGUIDs.Count})", 120);
            cachedSubCategoryButtonStyle.fontSize = adjustedFontSize;

            Rect subCategoryButtonRect =
                GUILayoutUtility.GetRect(subCategoryButtonContent, cachedSubCategoryButtonStyle, GUILayout.Width(120));
            HandleSubCategoryButtonClicks(category, subCategory.id, subCategoryButtonRect);

            // Add Drag and Drop handling here.
            if (Event.current.type == EventType.DragUpdated && subCategoryButtonRect.Contains(Event.current.mousePosition))
            {
                if (DragAndDrop.objectReferences.Length > 0)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                }
            }
            else if (Event.current.type == EventType.DragPerform && subCategoryButtonRect.Contains(Event.current.mousePosition))
            {
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    string path = AssetDatabase.GetAssetPath(obj);
                    string guid = AssetDatabase.AssetPathToGUID(path);

                    if (!subCategory.fileGUIDs.Contains(guid))
                    {
                        draggedGUID = guid;
                    }
                    
                    if (draggedGUID != null)
                    {
                        RemoveFileFromOriginalLocation(draggedGUID, categories);
                        subCategory.fileGUIDs.Add(draggedGUID);
                        SaveCategories();
                    }
                }

                Event.current.Use();
            }

            // Set background color based on selection
            GUI.backgroundColor = activeSubcategoryId == subCategory.id ? Color.cyan : Color.white;
            GUI.Button(subCategoryButtonRect, subCategoryButtonContent, cachedSubCategoryButtonStyle);
            GUI.backgroundColor = Color.white;

            col++;
        }

        private GUIContent CreateSubCategoryButtonContent(Category subCategory)
        {
            bool showFileCountOnSubcategories =
                EditorPrefs.GetBool(QuickSelectEditorSettings.ShowFileCountOnSubcategoriesKey, false);
            string buttonText = showFileCountOnSubcategories
                ? $"{subCategory.name} ({subCategory.fileGUIDs.Count})"
                : subCategory.name;

            return new GUIContent(buttonText,
                $"Left-Click to select, Right-Click for options.");
        }

        private void HandleSubCategoryButtonClicks(Category category, string subCategoryId, Rect subCategoryButtonRect)
        {
            Event currentEvent = Event.current;

            if (subCategoryButtonRect.Contains(currentEvent.mousePosition))
            {
                if (currentEvent.type == EventType.MouseDown)
                {
                    switch (currentEvent.button)
                    {
                        case 0: // Left mouse button
                            // Don't immediately select the subcategory on mouse down
                            // Instead, set a flag to potentially start a drag operation
                            potentialDragOperation = true;
                            draggedSubCategoryId = subCategoryId;
                            draggedCategoryId = category.id;
                            dragStartPosition = currentEvent.mousePosition;
                            currentEvent.Use();
                            break;
                        case 1: // Right mouse button - show context menu
                            ShowSubCategoryContextMenu(category, subCategoryId);
                            currentEvent.Use();
                            break;
                    }
                }
                else if (currentEvent.type == EventType.MouseUp)
                {
                    if (potentialDragOperation)
                    {
                        // The user has released the mouse button without starting a drag operation, so select the category
                        SelectSubCategory(subCategoryId);
                        currentEvent.Use();
                    }

                    // Reset the potential drag operation flag
                    potentialDragOperation = false;
                }
            }
            
            if (currentEvent.type == EventType.MouseDrag && potentialDragOperation)
            {
                // If the mouse has moved a certain distance, start a drag operation
                if ((currentEvent.mousePosition - dragStartPosition).sqrMagnitude >
                    dragStartThreshold * dragStartThreshold)
                {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = new UnityEngine.Object[] { }; // No actual objects need to be dragged
                    DragAndDrop.SetGenericData("SubCategoryId", draggedSubCategoryId);
                    DragAndDrop.SetGenericData("ParentCategoryId", draggedCategoryId);
                    DragAndDrop.StartDrag("Dragging Subcategory");
                    potentialDragOperation = false;
                    currentEvent.Use();
                }
            }
        }
        
        private void HandleDragAndDrop(Category category)
        {
            EventType eventType = Event.current.type;

            if (eventType == EventType.DragUpdated || eventType == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (eventType == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    foreach (UnityEngine.Object droppedObj in DragAndDrop.objectReferences)
                    {
                        string assetPath = AssetDatabase.GetAssetPath(droppedObj);
                        string assetGUID = AssetDatabase.AssetPathToGUID(assetPath);

                        // Check if the category has no subcategories and create a new one if necessary
                        if (category.subCategories.Count == 0)
                        {
                            activeSubcategoryId = AddNewSubCategory(category, category.name).id;
                        }

                        // Use FindCategoryById to find activeSubcategory
                        Category activeSubcategory = FindCategoryById(activeSubcategoryId, category.subCategories);
                        if (activeSubcategory != null && !activeSubcategory.fileGUIDs.Contains(assetGUID))
                        {
                            activeSubcategory.fileGUIDs.Add(assetGUID);
                        }
                    }

                    SaveCategories();
                    SaveWindowState();
                }

                Event.current.Use();
            }
        }

        private void SelectSubCategory(string subCategoryId)
        {
            Category selectedCategory = FindCategoryById(activeCategoryId, categories);
            if (selectedCategory != null && selectedCategory.subCategories.Exists(sc => sc.id == subCategoryId))
            {
                activeSubcategoryId = subCategoryId;
            }
            else
            {
                activeSubcategoryId = null;
            }

            SaveWindowState();
        }

        private void ShowSubCategoryContextMenu(Category parentCategory, string subCategoryId)
        {
            GenericMenu menu = new GenericMenu();
            
            menu.AddItem(new GUIContent("Rename"), false, () =>
            {
                EditSubCategory(parentCategory, subCategoryId);
            });
            
            menu.AddItem(new GUIContent("Delete"), false, () =>
            {
                DeleteSubCategory(parentCategory, subCategoryId);
            });
            
            menu.ShowAsContext();
        }

        private void DeleteSubCategory(Category category, string subCategoryId)
        {
            Category subCategory = category.subCategories.Find(sc => sc.id == subCategoryId);

            if (EditorUtility.DisplayDialog("Delete Subcategory",
                    $"Are you sure you want to delete the {subCategory.name} subcategory?",
                    "Yes", "No"))
            {
                category.subCategories.RemoveAll(sc => sc.id == subCategoryId);

                if (category.subCategories.Count > 0)
                {
                    activeSubcategoryId = category.subCategories[0].id;
                }
                else
                {
                    activeSubcategoryId = null;
                }

                SaveCategories();
                EditorPrefsUtility.SaveActiveSubcategory(SharedLayoutMode, !IsDocked(),
                    category.id, activeSubcategoryId);
            }
        }

        private void EditSubCategory(Category parentCategory, string subCategoryId)
        {
            Category subCategory = parentCategory.subCategories.Find(sc => sc.id == subCategoryId);
            if (subCategory != null)
            {
                string oldName = subCategory.name;
                string oldId = subCategory.id;
                EditorWindowUtility.ShowWindow("Rename Sub-category", EditorWindowUtility.WindowMode.RenameItem, oldName,
                    (string newName) =>
                    {
                        subCategory.name = newName;
                        // Sort the subcategories alphabetically after renaming
                        SortSubCategoriesAlphabetically(parentCategory);
                        // Set the active subcategory to the renamed subcategory
                        activeSubcategoryId = oldId;

                        SaveCategories();
                        EditorPrefsUtility.SaveActiveSubcategory(SharedLayoutMode, !IsDocked(),
                            FindCategoryById(activeCategoryId, categories).id, activeSubcategoryId);
                    });
            }
        }

        private void DrawAddSubCategoryButton()
        {
            EditorGUILayout.BeginHorizontal();
            if (!compactMode)
            {
                GUILayout.FlexibleSpace();

                GUI.backgroundColor = new Color(0.0f, 1.0f, 0.0f, 0.5f);
                if (GUILayout.Button("[+] Add Sub-Category"))
                {
                    Category activeCategory = FindCategoryById(activeCategoryId, categories);
                    EditorWindowUtility.ShowWindow("Add Sub-Category", EditorWindowUtility.WindowMode.AddCategory, "",
                        (newName) => { AddNewSubCategory(activeCategory, newName); });
                }
                GUI.backgroundColor = Color.white;

                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.EndHorizontal();
        }

        private Category AddNewSubCategory(Category parentCategory, string subCategoryName)
        {
            Category newSubCategory = new Category
            {
                name = subCategoryName,
                id = Guid.NewGuid().ToString(),
                fileGUIDs = new List<string>(),
                subCategories = new List<Category>()
            };

            parentCategory.subCategories.Add(newSubCategory);
            SortSubCategoriesAlphabetically(parentCategory);
            activeSubcategoryId = newSubCategory.id;
            SaveCategories();
            EditorPrefsUtility.SaveActiveSubcategory(SharedLayoutMode, !IsDocked(), parentCategory.id, activeSubcategoryId);

            return newSubCategory; // return the new subcategory
        }
    }
}
