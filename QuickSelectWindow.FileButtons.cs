// Quick Select Editor - A Unity Editor tool for organizing project assets
// Copyright (C) 2025  Thomas Mahler
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace QuickSelectEditor
{
    /// <summary>
    /// Partial class containing file button drawing, click handling, and context menus.
    /// </summary>
    public partial class QuickSelectWindow
    {
        private Texture2D GetAssetPreviewTexture(UnityEngine.Object obj)
        {
            string assetPath = AssetDatabase.GetAssetPath(obj);
            Texture2D previewTexture;

            if (obj is GameObject && PrefabUtility.GetPrefabAssetType(obj) != PrefabAssetType.NotAPrefab)
            {
                // The object is a prefab, get the default prefab icon
                previewTexture = (Texture2D)AssetDatabase.GetCachedIcon(assetPath);
            }
            else
            {
                // The object is not a prefab, generate a preview
                previewTexture = AssetPreview.GetAssetPreview(obj);
                if (previewTexture == null)
                {
                    previewTexture = AssetPreview.GetMiniThumbnail(obj);
                }
            }

            return previewTexture;
        }


        //The following methods are about the FileButtons inside Subcategories:
        private void DrawFileButtons(Category category)
        {
            float defaultButtonWidth = 240;
            float availableWidth = EditorGUIUtility.currentViewWidth - 25;
            int columnCount = Mathf.Max(1, Mathf.FloorToInt(availableWidth / defaultButtonWidth));
            float buttonWidth = compactMode
                ? Mathf.Min(availableWidth / columnCount, defaultButtonWidth)
                : defaultButtonWidth;

            // Cache button rects from the last repaint. MouseUp events are not Repaint events,
            // so clearing every frame would empty the cache right before we need it.
            if (Event.current.type == EventType.Repaint)
            {
                m_fileButtonRects.Clear();
                m_fileButtonObjects.Clear();
            }

            fileButtonsArea = EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));
            fileButtonsScrollPosition =
                EditorGUILayout.BeginScrollView(fileButtonsScrollPosition, GUILayout.ExpandHeight(true));

            // Check if we need to rebuild the grouped/sorted cache
            bool needsRebuild = m_cachedGroupedFiles == null ||
                                m_cachedGroupingCategoryId != category.id ||
                                m_cachedGroupByFolder != GroupByFolder ||
                                m_cachedGroupByFileType != GroupByFileType ||
                                m_cachedGroupAlphabetically != GroupAlphabetically ||
                                m_cachedGroupItems != GroupItems ||
                                m_cachedFileCount != category.fileGUIDs.Count;
            
            if (needsRebuild)
            {
                // Cache the current settings
                m_cachedGroupingCategoryId = category.id;
                m_cachedGroupByFolder = GroupByFolder;
                m_cachedGroupByFileType = GroupByFileType;
                m_cachedGroupAlphabetically = GroupAlphabetically;
                m_cachedGroupItems = GroupItems;
                m_cachedFileCount = category.fileGUIDs.Count;
                
                // Build grouped data
                Dictionary<string, List<string>> groupedFileGuids;
                if (GroupItems)
                {
                    if (GroupByFolder) {
                        groupedFileGuids = GroupFilesByFolder(category);
                    }
                    else if (GroupByFileType) {
                        groupedFileGuids = GroupFilesByType(category);
                    }
                    else if (GroupAlphabetically) {
                        groupedFileGuids = GroupFilesAlphabetically(GroupFilesByType(category));
                    }
                    else {
                        groupedFileGuids = new Dictionary<string, List<string>> { { "Files", new List<string>(category.fileGUIDs) } };
                    }
                }
                else {
                    groupedFileGuids = new Dictionary<string, List<string>> { { "Files", new List<string>(category.fileGUIDs) } };
                }
                
                // Sort the dictionary keys and sort each group's files
                m_cachedGroupedFiles = new Dictionary<string, List<string>>();
                var sortedKeys = new List<string>(groupedFileGuids.Keys);
                sortedKeys.Sort(System.StringComparer.OrdinalIgnoreCase);
                
                foreach (var key in sortedKeys)
                {
                    var files = groupedFileGuids[key];
                    // Sort files by name (cache the paths to avoid repeated lookups)
                    files.Sort((guid1, guid2) =>
                        string.Compare(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid1)),
                            Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid2)),
                            System.StringComparison.OrdinalIgnoreCase));
                    m_cachedGroupedFiles[key] = files;
                }
            }

            foreach (var fileGroup in m_cachedGroupedFiles) {
                if(GroupItems) {
                    EditorGUILayout.LabelField(fileGroup.Key, EditorStyles.boldLabel);
                }

                DrawFileButtonsGroup(fileGroup.Value, category);

                if(GroupItems) {
                    GUILayout.Space(10); // Add space between groups
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();

            // Handle rectangle selection
            HandleRectangleSelection();

            // Check for a right-click within the fileButtons area
            if (Event.current.type == EventType.ContextClick && fileButtonsArea.Contains(Event.current.mousePosition)) {
                // Only show the settings menu if the right-click is not on a file button
                if (!rightClickOnFileButton) {
                    Event.current.Use();
                    ShowFileButtonsSettingsContextMenu();
                }

                // Reset the right-click flag
                rightClickOnFileButton = false;
            }
        }

        private void DrawFileButtonsGroup(List<string> fileGuids, Category category)
        {
            float defaultButtonWidth = 240;
            float availableWidth = EditorGUIUtility.currentViewWidth - 30;
            int columnCount = Mathf.Max(1, Mathf.FloorToInt(availableWidth / defaultButtonWidth));

            // Calculate button width based on available width and column count
            float buttonWidth = compactMode
                ? Mathf.Min(availableWidth / columnCount, defaultButtonWidth)
                : defaultButtonWidth;

            if (!compactMode)
            {
                availableWidth = EditorGUIUtility.currentViewWidth - 225; // Reset availableWidth for non-compact mode
                columnCount = Mathf.Max(1, Mathf.FloorToInt(availableWidth / defaultButtonWidth));
            }

            int i = 0;
            while (i < fileGuids.Count)
            {
                EditorGUILayout.BeginHorizontal();

                int col = 0;
                while (col < columnCount && i < fileGuids.Count)
                {
                    DrawFileButton(fileGuids[i], category, buttonWidth);

                    i++;
                    col++;
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawFileButton(string guid, Category category, float buttonWidth)
        {
            string filePath = AssetDatabase.GUIDToAssetPath(guid);
            UnityEngine.Object fileObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(filePath);

            // Skip if the asset no longer exists
            if (fileObj == null)
            {
                return;
            }

            Texture2D filePreview = GetAssetPreviewTexture(fileObj);
            int adjustedFontSize = AdjustFontSizeToFitWidth(fileObj.name, buttonWidth - 24); // Reserve space for the icon
            GUIContent buttonContent = new GUIContent(fileObj.name, filePreview);
            
            if (cachedFileButtonStyle == null)
            {
                cachedFileButtonStyle = new GUIStyle(GUI.skin.button);
                cachedFileButtonStyle.imagePosition = ImagePosition.ImageLeft;
                cachedFileButtonStyle.fixedHeight = 20;
                cachedFileButtonStyle.margin = new RectOffset(0, 0, 2, 2);
                cachedFileButtonStyle.alignment = TextAnchor.MiddleLeft;
            }
            cachedFileButtonStyle.fontSize = adjustedFontSize;

            Rect buttonRect = GUILayoutUtility.GetRect(buttonContent, cachedFileButtonStyle, GUILayout.Width(buttonWidth),
                GUILayout.Height(20));
            
            // Store button rect for rectangle selection. Store in screen space so we don't need to
            // manually compensate for scroll views / clipping.
            if (Event.current.type == EventType.Repaint)
            {
                m_fileButtonRects[guid] = GUIUtility.GUIToScreenRect(buttonRect);
                m_fileButtonObjects[guid] = fileObj;
            }
            
            HandleFileButtonClickEvents(buttonRect, guid, category, buttonContent, cachedFileButtonStyle);
        }

        private void HandleFileButtonClickEvents(Rect buttonRect, string guid, Category category, GUIContent buttonContent,
            GUIStyle fileButtonStyle)
        {
            string filePath = AssetDatabase.GUIDToAssetPath(guid);
            UnityEngine.Object fileObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(filePath);

            if (Event.current.type == EventType.MouseDown && buttonRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.button == 0) // Left-click
                {
                    isLeftMouseDown = true;
                    dragStarted = false;
                    // Capture modifiers on MouseDown (MouseUp may not reliably report them)
                    m_clickHadShift = Event.current.shift;
                    m_clickHadCtrlOrCmd = Event.current.control || Event.current.command;
                }
                else if (Event.current.button == 2) // Middle-click
                {
                    int fileToRemove = category.fileGUIDs.IndexOf(guid);
                    Event.current.Use();

                    if (fileToRemove >= 0 &&
                        fileToRemove < category.fileGUIDs.Count) // Check if the index is valid
                    {
                        category.fileGUIDs.RemoveAt(fileToRemove); // Remove the file from the list
                        SaveCategories(); // Save the categories after deleting a fileButton
                    }
                }
                else if (Event.current.button == 1) // Right-click
                {
                    ShowFileButtonContextMenu(category, guid, fileObj, filePath);
                    Event.current.Use();
                }

                if (Event.current.type == EventType.MouseDown && Event.current.button == 1 &&
                    buttonRect.Contains(Event.current.mousePosition))
                {
                    rightClickOnFileButton = true;
                }
            }

            if (Event.current.type == EventType.MouseUp && buttonRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.button == 0 && isLeftMouseDown) // Left-click
                {
                    isLeftMouseDown = false;

                    if (!dragStarted)
                    {
                        float currentTime = (float)EditorApplication.timeSinceStartup;
                        float timeSinceLastClick = currentTime - lastClickTime;
                        lastClickTime = currentTime;

                        // Use captured modifiers from MouseDown (more reliable than checking on MouseUp)
                        bool hasModifier = m_clickHadShift || m_clickHadCtrlOrCmd;
                        bool isSameButtonAsLastTime = !string.IsNullOrEmpty(m_lastClickedFileGuid) && m_lastClickedFileGuid == guid;
                        m_lastClickedFileGuid = guid;

                        // Only treat as double-click when clicking the same button twice quickly,
                        // and never when modifiers are held (so Shift/Ctrl/Cmd multi-select works as expected).
                        if (!hasModifier && isSameButtonAsLastTime && timeSinceLastClick <= 0.3f)
                        {
                            AssetDatabase.OpenAsset(fileObj);
                        }
                        else // Single-click
                        {
                            ApplyUnitySelectionForFileButtonClick(fileObj, m_clickHadShift, m_clickHadCtrlOrCmd);
                            // Only ping when not multi-selecting (PingObject resets Selection.objects!)
                            if (!m_clickHadShift && !m_clickHadCtrlOrCmd)
                            {
                                EditorGUIUtility.PingObject(fileObj);
                            }
                        }
                    }
                }
            }

            if (Event.current.type == EventType.MouseDrag && isLeftMouseDown && !dragStarted &&
                buttonRect.Contains(Event.current.mousePosition))
            {
                dragStarted = true;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.paths = new string[] { filePath };
                DragAndDrop.objectReferences = new UnityEngine.Object[] { fileObj };
                DragAndDrop.StartDrag(fileObj.name);
                Event.current.Use();
            }

            if (Event.current.type == EventType.Repaint)
            {
                bool shouldHighlight = false;
                
                if (m_isRectSelecting)
                {
                    // During rect selection: compute highlight based on modifiers and rectangle
                    bool isInRectSelection = m_rectSelectionGuids.Contains(guid);
                    int instanceId = fileObj != null ? fileObj.GetInstanceID() : 0;
                    bool wasInBaseSelection = instanceId != 0 && m_rectBaseSelectionById.ContainsKey(instanceId);
                    
                    // Cache modifier state (avoid repeated Event.current access)
                    Event evt = Event.current;
                    if (evt.control || evt.command)
                    {
                        // Toggle mode: highlight if (was selected XOR in rectangle)
                        shouldHighlight = wasInBaseSelection != isInRectSelection;
                    }
                    else if (evt.shift)
                    {
                        // Shift mode: highlight if was selected OR in rectangle
                        shouldHighlight = wasInBaseSelection || isInRectSelection;
                    }
                    else
                    {
                        // No modifiers: highlight only what's in rectangle
                        shouldHighlight = isInRectSelection;
                    }
                }
                else
                {
                    // Normal mode: highlight if selected in Unity
                    shouldHighlight = fileObj != null && m_cachedSelectionInstanceIds.Contains(fileObj.GetInstanceID());
                }
                
                if (shouldHighlight)
                {
                    GUI.backgroundColor = Color.cyan;
                }
                
                GUI.Button(buttonRect, buttonContent, fileButtonStyle);
                
                if (shouldHighlight)
                {
                    GUI.backgroundColor = Color.white;
                }
            }
        }

        private void ApplyUnitySelectionForFileButtonClick(UnityEngine.Object fileObj, bool isAdd, bool isToggle)
        {
            if (fileObj == null)
            {
                return;
            }

            // Default click: select only this object
            if (!isToggle && !isAdd)
            {
                Selection.objects = new[] { fileObj };
                Selection.activeObject = fileObj;
                UpdateCachedUnitySelection();
                return;
            }

            // Build a modifiable list of current selection
            UnityEngine.Object[] currentSelection = Selection.objects;
            List<UnityEngine.Object> list = new List<UnityEngine.Object>(currentSelection.Length + 1);
            for (int i = 0; i < currentSelection.Length; i++)
            {
                if (currentSelection[i] != null)
                {
                    list.Add(currentSelection[i]);
                }
            }

            int fileId = fileObj.GetInstanceID();
            int existingIndex = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].GetInstanceID() == fileId)
                {
                    existingIndex = i;
                    break;
                }
            }

            if (isToggle)
            {
                // Ctrl/Cmd-click: toggle membership
                if (existingIndex >= 0)
                {
                    list.RemoveAt(existingIndex);
                }
                else
                {
                    list.Add(fileObj);
                }
            }
            else if (isAdd)
            {
                // Shift-click: add to selection
                if (existingIndex < 0)
                {
                    list.Add(fileObj);
                }
            }

            // IMPORTANT: Only set Selection.objects, NOT Selection.activeObject for multi-select.
            // Setting activeObject separately can cause Unity to internally reset Selection.objects.
            Selection.objects = list.ToArray();

            UpdateCachedUnitySelection();
        }
        
        private void ShowFileButtonContextMenu(Category category, string guid, UnityEngine.Object fileObj, string filePath)
        {
            GenericMenu menu = new GenericMenu();
            
            // Add "Move To" submenu for moving to other subcategories
            AddFileButtonMoveToSubmenu(menu, category, guid);
            
            menu.AddItem(new GUIContent("Rename"), false, () =>
            {
                int index = category.fileGUIDs.IndexOf(guid);
                EditorWindowUtility.ShowWindow("Rename File", EditorWindowUtility.WindowMode.RenameItem, fileObj.name,
                    (newName) =>
                    {
                        if (newName != fileObj.name)
                        {
                            string newPath = Path.Combine(Path.GetDirectoryName(filePath),
                                newName + Path.GetExtension(filePath));
                            AssetDatabase.RenameAsset(filePath, newName);
                            string newGuid = AssetDatabase.AssetPathToGUID(newPath);
                            category.fileGUIDs[index] = newGuid;
                        }
                    });
            });
            
            menu.AddItem(new GUIContent("Remove"), false, () =>
            {
                int fileToRemove = category.fileGUIDs.IndexOf(guid);
                if (fileToRemove >= 0 && fileToRemove < category.fileGUIDs.Count)
                {
                    category.fileGUIDs.RemoveAt(fileToRemove);
                    SaveCategories();
                }
            });
            
            menu.ShowAsContext();
        }
        
        private void AddFileButtonMoveToSubmenu(GenericMenu menu, Category sourceCategory, string fileGuid)
        {
            // Add all categories as nested destinations (same style as category Move To)
            for (int i = 0; i < categories.Count; i++)
            {
                AddFileButtonMoveToDestination(menu, sourceCategory, fileGuid, categories[i], "Move To/");
            }
        }
        
        private void AddFileButtonMoveToDestination(GenericMenu menu, Category sourceCategory, string fileGuid, Category category, string menuPath)
        {
            bool hasSubcategories = category.subCategories != null && category.subCategories.Count > 0;
            bool hasChildren = category.children != null && category.children.Count > 0;
            
            // Check if this category has any valid destinations (subcategories or children with subcategories)
            bool hasValidDestinations = hasSubcategories || (hasChildren && HasValidFileDestinations(category.children));
            
            if (hasValidDestinations)
            {
                // Category has subcategories and/or children - create a submenu
                if (hasSubcategories)
                {
                    // Add each subcategory as a destination
                    for (int i = 0; i < category.subCategories.Count; i++)
                    {
                        Category subCat = category.subCategories[i];
                        bool isCurrentSubcategory = subCat.id == sourceCategory.id;
                        
                        if (isCurrentSubcategory)
                        {
                            menu.AddDisabledItem(new GUIContent(menuPath + category.name + "/" + subCat.name + " (current)"));
                        }
                        else
                        {
                            menu.AddItem(new GUIContent(menuPath + category.name + "/" + subCat.name), false, () =>
                            {
                                MoveFileToSubcategory(sourceCategory, subCat, fileGuid);
                            });
                        }
                    }
                }
                
                // Add child categories recursively
                if (hasChildren)
                {
                    if (hasSubcategories)
                    {
                        menu.AddSeparator(menuPath + category.name + "/");
                    }
                    
                    for (int i = 0; i < category.children.Count; i++)
                    {
                        AddFileButtonMoveToDestination(menu, sourceCategory, fileGuid, category.children[i], menuPath + category.name + "/");
                    }
                }
            }
            else
            {
                // Category has no subcategories - show as disabled leaf
                menu.AddDisabledItem(new GUIContent(menuPath + category.name + " (no subcategories)"));
            }
        }
        
        private bool HasValidFileDestinations(List<Category> categoryList)
        {
            for (int i = 0; i < categoryList.Count; i++)
            {
                Category cat = categoryList[i];
                if (cat.subCategories != null && cat.subCategories.Count > 0)
                {
                    return true;
                }
                if (cat.children != null && cat.children.Count > 0 && HasValidFileDestinations(cat.children))
                {
                    return true;
                }
            }
            return false;
        }
        
        private void MoveFileToSubcategory(Category sourceCategory, Category targetCategory, string fileGuid)
        {
            // Remove from source
            sourceCategory.fileGUIDs.Remove(fileGuid);
            
            // Add to target (if not already there)
            if (!targetCategory.fileGUIDs.Contains(fileGuid))
            {
                targetCategory.fileGUIDs.Add(fileGuid);
            }
            
            SaveCategories();
        }
    }
}
