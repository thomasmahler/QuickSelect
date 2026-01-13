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
    /// Partial class containing category list drawing and management.
    /// </summary>
    public partial class QuickSelectWindow
    {
        //The following methods are about the Category List on the left side of the editor:
        private void DrawCategoriesList()
        {
            // Calculate the maximum indentation level of the currently visible categories
            int maxIndentLevel = CalculateVisibleMaxIndentLevel(categories);

            // Calculate the maximum width required by the visible buttons
            float maxButtonWidth = 200 + maxIndentLevel * indentWidth;

            EditorGUILayout.BeginVertical(GUILayout.Width(maxButtonWidth));
            DrawCategoryTitle();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Width(maxButtonWidth + 10),
                GUILayout.ExpandHeight(true));

            for (int i = 0; i < categories.Count; i++)
            {
                DrawCategoryButton(categories[i].id, categories[i], 0);

                // If the category has children, draw the child categories
                if (categories[i].children != null && categories[i].children.Count > 0)
                {
                    DrawChildCategories(categories[i], 1);
                }
            }
            
            // Handle drag and drop events within the scrollView
            if (Event.current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();

                // Get dragged data
                Category draggedCategory = (Category)DragAndDrop.GetGenericData("draggedCategory");

                if (draggedCategory != null)
                {
                    // Check if dragged category is not the target category and dragged category is not already a child of the target category
                    if (!categories.Contains(draggedCategory))
                    {
                        MakeChildCategoryTopLevel(draggedCategory);
                        SaveCategories();
                        Repaint();
                    }
                }

                Event.current.Use();
            }

            DrawAddCategoryButton();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private int CalculateVisibleMaxIndentLevel(List<Category> categoryList, int currentIndentLevel = 0)
        {
            int maxIndentLevel = currentIndentLevel;
            foreach (var category in categoryList)
            {
                bool isExpanded = EditorPrefsUtility.LoadExpandedState(SharedLayoutMode, !IsDocked(), category.id);
                if (category.children != null && category.children.Count > 0 && isExpanded)
                {
                    maxIndentLevel = Math.Max(maxIndentLevel,
                        CalculateVisibleMaxIndentLevel(category.children, currentIndentLevel + 1));
                }
            }

            return maxIndentLevel;
        }

        private void DrawChildCategories(Category parentCategory, int indentLevel)
        {
            bool isExpanded = EditorPrefsUtility.LoadExpandedState(SharedLayoutMode, !IsDocked(), parentCategory.id);
            if (parentCategory.children != null && parentCategory.children.Count > 0 && isExpanded)
            {
                // Children are already sorted when saved/loaded
                for (int i = 0; i < parentCategory.children.Count; i++)
                {
                    Category childCategory = parentCategory.children[i];
                    DrawCategoryButton(childCategory.id, childCategory, indentLevel);
                    DrawChildCategories(childCategory, indentLevel + 1);
                }
            }
        }

        private void DrawCategoryTitle()
        {
            if (SharedLayoutMode)
            {
                EditorGUILayout.LabelField("Cloud Categories", EditorStyles.boldLabel);
            }
            else
            {
                EditorGUILayout.LabelField("Personal Categories", EditorStyles.boldLabel);
            }
        }

        private void DrawCategoryButton(string id, Category category, int indentLevel)
        {
            EditorGUILayout.BeginHorizontal();

            Category parentCategory = GetParentCategory(category, categories);

            bool isParent = category.children != null && category.children.Count > 0;
            bool isExpanded = EditorPrefsUtility.LoadExpandedState(SharedLayoutMode, !IsDocked(), category.id);
            string arrow = isParent ? (isExpanded ? "▼ " : "► ") : "   ";

            bool showSubcategoryCountOnCategoryButtons =
                EditorPrefs.GetBool(QuickSelectEditorSettings.ShowSubcategoryCountOnCategoryButtonsKey, false);
            string buttonText = showSubcategoryCountOnCategoryButtons
                ? $"{arrow}{category.name} ({category.subCategories.Count})"
                : $"{arrow}{category.name}";

            GUIContent buttonContent = new GUIContent(buttonText,
                $"Left-Click to select, Right-Click for options.");

            GUI.backgroundColor = activeCategoryId == id ? Color.cyan : Color.white;
            GUIStyle categoryButtonStyle = GetCategoryButtonStyle(category.name);

            // Align the text to the left
            categoryButtonStyle.alignment = TextAnchor.MiddleLeft;

            // Calculate the button width based on the indent level
            float buttonWidth = 200;
            Rect buttonRect = GUILayoutUtility.GetRect(buttonContent, categoryButtonStyle, GUILayout.Width(buttonWidth));
            buttonRect.x += indentWidth * indentLevel;
            categoryButtonRects[category] = buttonRect;

            HandleCategoryButtonClick(id, category, parentCategory);
            GUI.Button(buttonRect, buttonContent, categoryButtonStyle);

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(1);

            // New code to handle a subcategory being dropped
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.DragUpdated &&
                GUILayoutUtility.GetLastRect().Contains(currentEvent.mousePosition))
            {
                // If a subcategory is being dragged, show a drag visual
                if (DragAndDrop.GetGenericData("SubCategoryId") != null)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                }
            }
            else if (currentEvent.type == EventType.DragPerform &&
                     GUILayoutUtility.GetLastRect().Contains(currentEvent.mousePosition))
            {
                // Perform the drop operation
                string draggedSubCategoryId = (string)DragAndDrop.GetGenericData("SubCategoryId");
                string draggedCategoryId = (string)DragAndDrop.GetGenericData("ParentCategoryId");

                if (draggedSubCategoryId != null && draggedCategoryId != null)
                {
                    Category subParentCategory = FindCategoryById(draggedCategoryId, categories);
                    Category subCategory = subParentCategory?.subCategories.Find(sc => sc.id == draggedSubCategoryId);

                    if (subCategory != null)
                    {
                        // Remove the subcategory from the old category
                        subParentCategory.subCategories.Remove(subCategory);

                        // Add the subcategory to the new category
                        category.subCategories.Add(subCategory);

                        // Sort the subcategories of the target category
                        SortSubCategoriesAlphabetically(category);
                        SaveCategories();

                        // Clear the drag data
                        DragAndDrop.AcceptDrag();
                        DragAndDrop.PrepareStartDrag();

                        currentEvent.Use();
                    }
                }
            }
        }

        private Category GetParentCategory(Category childCategory, List<Category> potentialParents)
        {
            foreach (Category parent in potentialParents)
            {
                if (parent.children.Contains(childCategory))
                {
                    return parent;
                }

                Category parentCategory = GetParentCategory(childCategory, parent.children);

                if (parentCategory != null)
                {
                    return parentCategory;
                }
            }

            return null;
        }

        private GUIStyle GetCategoryButtonStyle(string categoryName)
        {
            if (cachedCategoryButtonStyle == null)
            {
                cachedCategoryButtonStyle = new GUIStyle(GUI.skin.button);
                cachedCategoryButtonStyle.fixedHeight = 20;
                cachedCategoryButtonStyle.alignment = TextAnchor.MiddleCenter;
            }
            
            int adjustedFontSize = AdjustFontSizeToFitWidth(categoryName, 200);
            cachedCategoryButtonStyle.fontSize = adjustedFontSize;

            return cachedCategoryButtonStyle;
        }

        private void HandleCategoryButtonClick(string id, Category category, Category passedParentCategory)
        {
            Event currentEvent = Event.current;
            Category targetCategory = FindCategoryById(id, categories);

            if (targetCategory == null)
            {
                return;
            }

            if (categoryButtonRects.ContainsKey(targetCategory) &&
                categoryButtonRects[targetCategory].Contains(currentEvent.mousePosition))
            {
                switch (currentEvent.type)
                {
                    case EventType.MouseDown:
                        HandleCategoryMouseDown(currentEvent, id, targetCategory, passedParentCategory);
                        break;
                    case EventType.MouseDrag:
                        HandleCategoryMouseDrag(currentEvent, id, targetCategory);
                        break;
                    case EventType.MouseUp:
                        HandleCategoryMouseUp(currentEvent, id, targetCategory);
                        break;
                }
            }

            if (currentEvent.type == EventType.DragUpdated)
            {
                HandleCategoryDragUpdated(currentEvent, id);
            }

            if (currentEvent.type == EventType.DragPerform && categoryButtonRects.ContainsKey(targetCategory) &&
                categoryButtonRects[targetCategory].Contains(currentEvent.mousePosition))
            {
                HandleCategoryDragPerform(currentEvent, id, targetCategory);
            }

            if (currentEvent.type == EventType.DragExited)
            {
                HandleCategoryDragExited(currentEvent, id);
            }

            Repaint();
        }

        private void HandleCategoryMouseDown(Event currentEvent, string id, Category targetCategory, Category passedParentCategory)
        {
            if (currentEvent.button == 0) // Left mouse button
            {
                isMouseButtonDown = true;
                draggedCategoryId = id;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData("draggedCategory", targetCategory);
                initialMousePosition = currentEvent.mousePosition;
            }
            else if (currentEvent.button == 1) // Right mouse button - show context menu
            {
                Category parentCategory = passedParentCategory;
                foreach (Category topLevelCategory in categories)
                {
                    if (topLevelCategory.children.Contains(targetCategory))
                    {
                        parentCategory = topLevelCategory;
                        break;
                    }
                }
                ShowCategoryContextMenu(targetCategory, parentCategory);
            }

            currentEvent.Use();
        }

        private void HandleCategoryMouseDrag(Event currentEvent, string id, Category targetCategory)
        {
            if (currentEvent.button == 0 && currentEvent.type == EventType.MouseDrag)
            {
                // Only start dragging if the mouse has moved more than the threshold
                if (draggedCategoryId != null && (currentEvent.mousePosition - initialMousePosition).sqrMagnitude > dragStartThreshold * dragStartThreshold)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                    DragAndDrop.StartDrag(targetCategory.name);
                    currentEvent.Use();
                }
            }
        }

        private void HandleCategoryMouseUp(Event currentEvent, string id, Category targetCategory)
        {
            if (currentEvent.button == 0)
            {
                // If the active category is clicked, toggle its expanded state
                if (id == activeCategoryId)
                {
                    bool isExpanded = EditorPrefsUtility.LoadExpandedState(SharedLayoutMode, !IsDocked(), targetCategory.id);
                    EditorPrefsUtility.SaveExpandedState(SharedLayoutMode, !IsDocked(), id, !isExpanded);
                }

                Category draggedCategory = (Category)DragAndDrop.GetGenericData("draggedCategory");

                // Check if dragged category is not the target category and dragged category is not already a child of the target category
                // and the target category is not a child of the dragged category and target category is not itself
                if (draggedCategoryId != null && draggedCategory != null 
                                              && !targetCategory.children.Contains(draggedCategory)
                                              && !draggedCategory.children.Contains(targetCategory) 
                                              && targetCategory != draggedCategory)
                {
                    MoveCategoryToNewParent(targetCategory, draggedCategory);
                }

                // Reset the dragged category ID
                draggedCategoryId = null;
                isMouseButtonDown = false;

                // Select the category
                SelectCategory(id, targetCategory);

                currentEvent.Use();
            }
        }

        private void HandleCategoryDragUpdated(Event currentEvent, string id)
        {
            if (draggedCategoryId != null)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                currentEvent.Use();
            }
        }

        private void HandleCategoryDragPerform(Event currentEvent, string id, Category targetCategory)
        {
            DragAndDrop.AcceptDrag();
            Category draggedCategory = (Category)DragAndDrop.GetGenericData("draggedCategory");
            string draggedSubCategoryId = (string)DragAndDrop.GetGenericData("SubCategoryId");
            string draggedCatId = (string)DragAndDrop.GetGenericData("ParentCategoryId");

            // Case where a Category is being dragged
            if (draggedCategory != null)
            {
                // Check if dragged category is not the target category and dragged category is not already a child of the target category
                // and the target category is not a child of the dragged category and target category is not itself
                if (!targetCategory.children.Contains(draggedCategory)
                    && !draggedCategory.children.Contains(targetCategory) && targetCategory != draggedCategory)
                {
                    MoveCategoryToNewParent(targetCategory, draggedCategory);
                    SaveCategories();
            
                    // Indicate that a category has been dropped successfully
                    categoryDroppedSuccessfully = true;
                }
            }
            // Case where a Subcategory is being dragged
            else if (draggedSubCategoryId != null && draggedCatId != null)
            {
                Category subParentCategory = FindCategoryById(draggedCatId, categories);
                Category subCategory = subParentCategory?.subCategories.Find(sc => sc.id == draggedSubCategoryId);

                if (subCategory != null)
                {
                    // Remove the subcategory from the old category
                    subParentCategory.subCategories.Remove(subCategory);

                    // Add the subcategory to the new category
                    targetCategory.subCategories.Add(subCategory);

                    // Potentially sort the subcategories here
                    SortSubCategoriesAlphabetically(targetCategory);
                    SaveCategories();
                }
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.None;
            currentEvent.Use();
        }

        private void HandleCategoryDragExited(Event currentEvent, string id)
        {
            Category draggedCategory = (Category)DragAndDrop.GetGenericData("draggedCategory");

            // Check if the mouse button is currently not pressed before invoking MakeChildCategoryTopLevel
            if (draggedCategory != null && !categoryDroppedSuccessfully && !isMouseButtonDown)
            {
                MakeChildCategoryTopLevel(draggedCategory);
            }

            draggedCategoryId = null; // Reset the dragged category ID
            categoryDroppedSuccessfully = false; // Reset the drop status
            DragAndDrop.visualMode = DragAndDropVisualMode.None;
            currentEvent.Use();
        }
        
        private void MakeChildCategoryTopLevel(Category childCategory)
        {
            // Remove the child category from its current parent
            RemoveCategoryFromParent(childCategory, categories);

            // Add the child category to the top-level categories list
            categories.Add(childCategory);

            // Save categories
            SaveCategories();
        }

        private void MoveCategoryToNewParent(Category targetCategory, Category draggedCategory)
        {
            // Remove the dragged category from its current parent if it has one
            RemoveCategoryFromParent(draggedCategory, categories);

            // Add the dragged category as a child of the target category
            targetCategory.children.Add(draggedCategory);

            SortChildCategoriesAlphabetically(targetCategory);
            SaveCategories();
        }

        private void RemoveCategoryFromParent(Category childCategory, List<Category> potentialParents)
        {
            // If the child category is a top-level category, remove it from the categories list
            if (potentialParents == categories && categories.Contains(childCategory))
            {
                categories.Remove(childCategory);
                return;
            }

            foreach (Category parent in potentialParents)
            {
                if (parent.children.Contains(childCategory))
                {
                    parent.children.Remove(childCategory);
                    return; // If we've found and removed the category, we can exit the method early.
                }

                // If the category was not found on this level, continue the search in the next level.
                RemoveCategoryFromParent(childCategory, parent.children);
            }
        }

        private void RemoveCategoryFromChildren(Category category, List<Category> parentCategories)
        {
            foreach (Category parentCategory in parentCategories)
            {
                // Try to remove from current parent's children
                bool isRemoved = parentCategory.children.Remove(category);

                // If not removed, continue searching in the children of the current parent
                if (!isRemoved)
                {
                    RemoveCategoryFromChildren(category, parentCategory.children);
                }
                else
                {
                    // If removed, break the loop
                    break;
                }
            }
        }

        private void DrawAddCategoryButton()
        {
            GUI.backgroundColor = new Color(0.0f, 1.0f, 0.0f, 0.5f);
            if (GUILayout.Button("[+] Add Category", GUILayout.Width(200)))
            {
                EditorWindowUtility.ShowAddCategoryWindow("Add Category", categories, null,
                    (newName, parentCategory) => { AddNewCategoryWithParent(newName, parentCategory); });
            }
            GUI.backgroundColor = Color.white;
        }

        private void AddNewCategoryWithParent(string categoryName, Category parentCategory)
        {
            Category newCategory = new Category
            {
                id = Guid.NewGuid().ToString(),
                name = categoryName,
                fileGUIDs = new List<string>(),
                children = new List<Category>()
            };
            
            if (parentCategory != null)
            {
                parentCategory.children.Add(newCategory);
                SortChildCategoriesAlphabetically(parentCategory);
            }
            else
            {
                categories.Add(newCategory);
                SortCategoriesAlphabetically();
            }
            
            activeCategoryId = newCategory.id;
            activeSubcategoryId = null;
            SaveCategories();
            Repaint();
        }

        private void SelectCategory(string id, Category category)
        {
            if (category != null)
            {
                activeCategoryId = id;
                
                // Try to load the saved subcategory selection
                string savedSubcategoryId = EditorPrefsUtility.LoadActiveSubcategory(SharedLayoutMode, !IsDocked(), category.id);
                
                // Check if the saved subcategory is valid (exists in this category)
                bool isValidSavedSelection = !string.IsNullOrEmpty(savedSubcategoryId) && 
                    category.subCategories != null && 
                    category.subCategories.Exists(sc => sc.id == savedSubcategoryId);
                
                if (isValidSavedSelection)
                {
                    activeSubcategoryId = savedSubcategoryId;
                }
                else if (category.subCategories != null && category.subCategories.Count > 0)
                {
                    // Fall back to the first subcategory
                    activeSubcategoryId = category.subCategories[0].id;
                }
                else
                {
                    activeSubcategoryId = null;
                }
                
                Repaint();
            }
        }

        private void ShowCategoryContextMenu(Category category, Category parentCategory)
        {
            GenericMenu menu = new GenericMenu();
            
            menu.AddItem(new GUIContent("Add Child Category"), false, () =>
            {
                EditorWindowUtility.ShowWindow("Add Child Category", EditorWindowUtility.WindowMode.AddCategory, "",
                    (newName) => { AddChildCategory(category, newName); });
            });
            
            menu.AddSeparator("");
            
            // Add "Move To" submenu
            AddCategoryMoveToSubmenu(menu, category, parentCategory);
            
            menu.AddItem(new GUIContent("Rename"), false, () =>
            {
                EditCategory(category, parentCategory);
            });
            
            menu.AddItem(new GUIContent("Delete"), false, () =>
            {
                DeleteCategory(category);
            });
            
            menu.ShowAsContext();
        }

        private void AddCategoryMoveToSubmenu(GenericMenu menu, Category categoryToMove, Category currentParent)
        {
            // Option to move to root (only if not already at root)
            bool isAtRoot = currentParent == null;
            if (isAtRoot)
            {
                menu.AddDisabledItem(new GUIContent("Move To/Root (already here)"));
            }
            else
            {
                menu.AddItem(new GUIContent("Move To/Root"), false, () =>
                {
                    MoveCategoryTo(categoryToMove, currentParent, null);
                });
            }
            
            menu.AddSeparator("Move To/");
            
            // Add all categories as nested destinations
            for (int i = 0; i < categories.Count; i++)
            {
                AddCategoryMoveToDestination(menu, categoryToMove, currentParent, categories[i], "Move To/");
            }
        }

        private void AddCategoryMoveToDestination(GenericMenu menu, Category categoryToMove, Category currentParent, Category destination, string menuPath)
        {
            // Skip if destination is the category itself
            if (destination.id == categoryToMove.id)
            {
                return;
            }
            
            // Skip if destination is a descendant of the category (can't move into own children)
            if (IsCategoryDescendantOf(destination, categoryToMove))
            {
                return;
            }
            
            bool isCurrentParent = currentParent != null && currentParent.id == destination.id;
            bool hasValidChildren = destination.children != null && HasValidMoveDestinations(destination.children, categoryToMove);
            
            if (hasValidChildren)
            {
                // Category has children - create a submenu with "Move Here" option at top
                if (isCurrentParent)
                {
                    menu.AddDisabledItem(new GUIContent(menuPath + destination.name + "/• Current Location"));
                }
                else
                {
                    menu.AddItem(new GUIContent(menuPath + destination.name + "/• Move Here"), false, () =>
                    {
                        MoveCategoryTo(categoryToMove, currentParent, destination);
                    });
                }
                
                menu.AddSeparator(menuPath + destination.name + "/");
                
                // Add children
                for (int i = 0; i < destination.children.Count; i++)
                {
                    AddCategoryMoveToDestination(menu, categoryToMove, currentParent, destination.children[i], menuPath + destination.name + "/");
                }
            }
            else
            {
                // Category has no children - simple menu item
                if (isCurrentParent)
                {
                    menu.AddDisabledItem(new GUIContent(menuPath + destination.name + " (current)"));
                }
                else
                {
                    menu.AddItem(new GUIContent(menuPath + destination.name), false, () =>
                    {
                        MoveCategoryTo(categoryToMove, currentParent, destination);
                    });
                }
            }
        }

        private bool HasValidMoveDestinations(List<Category> categoryList, Category categoryToMove)
        {
            for (int i = 0; i < categoryList.Count; i++)
            {
                Category cat = categoryList[i];
                if (cat.id != categoryToMove.id && !IsCategoryDescendantOf(cat, categoryToMove))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsCategoryDescendantOf(Category potentialDescendant, Category ancestor)
        {
            if (ancestor.children == null)
            {
                return false;
            }
            
            for (int i = 0; i < ancestor.children.Count; i++)
            {
                if (ancestor.children[i].id == potentialDescendant.id)
                {
                    return true;
                }
                if (IsCategoryDescendantOf(potentialDescendant, ancestor.children[i]))
                {
                    return true;
                }
            }
            
            return false;
        }

        private void MoveCategoryTo(Category categoryToMove, Category oldParent, Category newParent)
        {
            // Remove from old location
            if (oldParent != null)
            {
                oldParent.children.Remove(categoryToMove);
            }
            else
            {
                categories.Remove(categoryToMove);
            }
            
            // Add to new location
            if (newParent != null)
            {
                newParent.children.Add(categoryToMove);
                SortChildCategoriesAlphabetically(newParent);
            }
            else
            {
                categories.Add(categoryToMove);
                SortCategoriesAlphabetically();
            }
            
            SaveCategories();
            Repaint();
        }

        private void AddChildCategory(Category parentCategory, string categoryName)
        {
            Category newCategory = new Category
            {
                id = Guid.NewGuid().ToString(),
                name = categoryName,
                fileGUIDs = new List<string>(),
                children = new List<Category>()
            };
            parentCategory.children.Add(newCategory);
            SortChildCategoriesAlphabetically(parentCategory);
            SaveCategories();
            Repaint();
        }

        private void DeleteCategory(Category category)
        {
            if (EditorUtility.DisplayDialog("Delete Category",
                    $"Are you sure you want to delete the {category.name} category?",
                    "Yes", "No"))
            {
                // Try to remove the category from top-level categories
                bool isRemoved = categories.Remove(category);

                // If it's not a top-level category, try to remove it from child categories
                if (!isRemoved)
                {
                    RemoveCategoryFromChildren(category, categories);
                }

                activeCategoryId = categories.Count > 0 ? categories[0].id : null;
                SaveCategories();
            }
        }

        private void EditCategory(Category category, Category parentCategory)
        {
            List<Category> categoryList = parentCategory != null ? parentCategory.children : categories;
            int index = categoryList.IndexOf(category);
            if (index >= 0 && index < categoryList.Count)
            {
                string oldName = category.name;
                EditorWindowUtility.ShowWindow("Rename Category", EditorWindowUtility.WindowMode.RenameCategory, oldName,
                    (string newName) =>
                    {
                        if (!string.IsNullOrEmpty(newName) && !newName.Equals(oldName))
                        {
                            category.name = newName;
                            if (parentCategory != null)
                            {
                                SortChildCategoriesAlphabetically(parentCategory);
                            }
                            else
                            {
                                SortCategoriesAlphabetically();
                            }

                            SaveCategories();
                        }
                    });
            }
        }
    }
}
