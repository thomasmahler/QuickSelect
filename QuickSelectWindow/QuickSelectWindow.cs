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
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace QuickSelectEditor
{
    /// <summary>
    /// A Unity Editor window for quickly selecting and organizing project assets into categories.
    /// Supports personal and shared (cloud) layouts, drag-and-drop organization, and keyboard shortcuts.
    /// </summary>
    public class QuickSelectWindow : EditorWindow
{
    private bool compactMode;
    private List<Category> categories;
    private string activeCategoryId;
    private string activeSubcategoryId;
    private string draggedCategoryId;
    private string draggedSubCategoryId;
    private bool lastDockState;

    //Category List Variables:
    private Vector2 scrollPosition;
    private bool isMouseButtonDown = false;
    private bool categoryDroppedSuccessfully;
    private Dictionary<Category, Rect> categoryButtonRects = new Dictionary<Category, Rect>();
    private int indentWidth = 20;
    private Vector2 initialMousePosition;

    //Subcategory Variables:
    private bool potentialDragOperation;
    private Vector2 dragStartPosition;
    private const float dragStartThreshold = 5f;

    //File Button Variables:
    private Vector2 fileButtonsScrollPosition;
    private Rect fileButtonsArea;
    private bool rightClickOnFileButton;
    private float lastClickTime;
    private bool isLeftMouseDown;
    private bool dragStarted;

    //Cached GUIStyles (to avoid allocations in OnGUI):
    private GUIStyle cachedToggleButtonStyle;
    private GUIStyle cachedCategoryButtonStyle;
    private GUIStyle cachedSubCategoryButtonStyle;
    private GUIStyle cachedFileButtonStyle;
    private GUIStyle cachedTextWidthStyle;

    //Windows / Modes:
    private static QuickSelectWindow floatingWindow;
    private static List<QuickSelectWindow> openInstances = new List<QuickSelectWindow>();
    
    private bool SharedLayoutMode
    {
        get => EditorPrefs.GetBool(EditorPrefsUtility.GetProjectSpecificKey(docked ? DockedLayoutModeKey : FloatingLayoutModeKey), false);
        set => EditorPrefs.SetBool(EditorPrefsUtility.GetProjectSpecificKey(docked ? DockedLayoutModeKey : FloatingLayoutModeKey), value);
    }
    
    /// <summary>
    /// Opens or closes the Quick Select floating window. Shortcut: Shift+Q
    /// </summary>
    [MenuItem("Window/Quick Select #q")]
    public static void OpenQuickSelectWindow()
    {
	    // Check if the game is in play mode and the Game View is focused
	    if (EditorApplication.isPlaying && focusedWindow != null && focusedWindow.titleContent.text == "Game")
	    {
		    return;
	    }
	    
        // Get all open instances of QuickSelectWindow
        QuickSelectWindow[] openWindows = Resources.FindObjectsOfTypeAll<QuickSelectWindow>();

        // Check if there's a floating window
        bool hasFloatingWindow = false;
        foreach (QuickSelectWindow window in openWindows)
        {
            if (!window.IsDocked())
            {
                hasFloatingWindow = true;
                floatingWindow = window;
                break;
            }
        }

        if (hasFloatingWindow)
        {
            // If a floating window exists, close it
            if (floatingWindow != null)
            {
                floatingWindow.Close();
                floatingWindow = null;
            }
        }
        else
        {
            // If no floating window was found, create a new one
            floatingWindow = CreateInstance<QuickSelectWindow>();
            floatingWindow.titleContent = new GUIContent("Quick Select");
            floatingWindow.Show();
            floatingWindow.Focus();
        }
    }

    private void OnEnable()
    {
        // Populate the openInstances list
        openInstances.Add(this);

        //Set Min Size
        this.minSize = new Vector2(200, 200);

        // Load the state
        LoadCategories();
        LoadWindowState();
        
        // Migration helper: older saved layouts may not have IDs. If IDs are missing, regenerate them once.
        CheckAndUpdateCategoryIDs();
        
        //Start watching for SharedLayout.json file changes:
        SharedLayoutFileWatcher.StartWatching();
    }

    private void OnDisable()
    {
        // Save the state
        SaveWindowState();

        // Save the layout mode for the instance
        EditorPrefs.SetBool(docked ? DockedLayoutModeKey : FloatingLayoutModeKey, SharedLayoutMode);

        // Update the Open Instances list:
        openInstances.Remove(this);
        
        //Stop watching for SharedLayout.json file changes:
        SharedLayoutFileWatcher.StopWatching();
    }

    private void OnDestroy()
    {
        if (floatingWindow == this)
        {
            // Save the state
            SaveWindowState();

            floatingWindow = null;
        }
    }
    
    private void OnGUI()
    {
        var changedDockState = docked != lastDockState;
        lastDockState = docked;
        
        if (changedDockState) {
	        LoadCategories();
        }
	    
        // Get the width of the editor window
        float editorWidth = position.width;

        // Set a threshold width below which the categories list should be hidden
        float thresholdWidth = 465f;

        // Update the compactMode based on the comparison
        compactMode = editorWidth <= thresholdWidth;

        if (!compactMode)
        {
            DrawToolbar();
        }

        EditorGUILayout.BeginHorizontal();

        // Draw the UI elements
        if (!compactMode)
        {
            DrawCategoriesList();
        }

        DrawCategoryContent();

        EditorGUILayout.EndHorizontal();
    }
    
    /// <summary>
    /// Requests a repaint of all open QuickSelectWindow instances.
    /// Used when shared layout changes are detected.
    /// </summary>
    public static void RequestRepaintAllInstances()
    {
        EditorApplication.delayCall += () =>
        {
            foreach (var instance in openInstances)
            {
                instance.RefreshAndRepaint();
            }
        };
    }

    private void RefreshAndRepaint()
    {
        LoadCategories();
        Repaint();
    }

    private bool IsDocked()
    {
        return docked;
    }
    
    
    //Saving / Loading:
    private const string FloatingLayoutModeKey = "QuickSelectWindow_SharedLayoutMode_Floating";
    private const string DockedLayoutModeKey = "QuickSelectWindow_SharedLayoutMode_Docked";

    private void SaveCategories()
    {
        if (SharedLayoutMode)
        {
            EditorPrefsUtility.SaveSharedCategories(categories);
        }
        else
        {
            EditorPrefsUtility.SavePersonalCategories(categories);
        }

        SortCategoriesAlphabetically();
        SaveWindowState();
        RequestRepaintAllInstances();
    }

    private void LoadCategories()
    {
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
                activeSubcategoryId =
                    EditorPrefsUtility.LoadActiveSubcategory(sharedLayout, floatingInstance, activeCategory.id);
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
                groupKey = Directory.Exists(filePath) ? "Folder" : fileObj.GetType().Name;
            }

            if (!fileTypeGroups.ContainsKey(groupKey))
            {
                fileTypeGroups[groupKey] = new List<string>();
            }

            fileTypeGroups[groupKey].Add(guid);
        }

        return fileTypeGroups;
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
    
    
    //Toolbar:
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.ExpandWidth(true));

        // Personal and Shared Layout Toggle:
        bool isSharedLayoutMode = SharedLayoutMode;

        // Use cached GUIStyle for the toggle button
        if (cachedToggleButtonStyle == null)
        {
            cachedToggleButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
            cachedToggleButtonStyle.alignment = TextAnchor.MiddleLeft;
        }

        // Shared Layout Button:
        GUIContent sharedLayoutButtonContent = EditorGUIUtility.IconContent("CloudConnect");
        sharedLayoutButtonContent.text = isSharedLayoutMode ? " Cloud" : " Personal";

        // Set the background color based on the toggle state
        Color originalBackgroundColor = GUI.backgroundColor;
        GUI.backgroundColor = isSharedLayoutMode ? new Color(0, 1, 1, 1) : originalBackgroundColor;
        bool newSharedLayoutMode = GUILayout.Toggle(isSharedLayoutMode, sharedLayoutButtonContent, cachedToggleButtonStyle,
            GUILayout.Width(80));

        // Reset the background color
        GUI.backgroundColor = originalBackgroundColor;

        if (isSharedLayoutMode != newSharedLayoutMode)
        {
            ToggleSharedLayoutMode();
        }

        // Add space between buttons
        GUILayout.FlexibleSpace();

        // Settings Button:
        GUIContent settingsButtonContent = EditorGUIUtility.IconContent("_Popup");
        if (GUILayout.Button(settingsButtonContent, EditorStyles.toolbarButton))
        {
            QuickSelectEditorSettings.ShowWindow();
        }

        EditorGUILayout.EndHorizontal();
    }
    
    
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

    private int CalculateVisibleMaxIndentLevel(List<Category> categories, int currentIndentLevel = 0)
    {
        int maxIndentLevel = currentIndentLevel;
        foreach (var category in categories)
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
            $"Left-Click to select, Right-Click to rename or Middle-Click to delete the {category.name} category.");

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
                    HandleMouseDown(currentEvent, id, targetCategory, passedParentCategory);
                    break;
                case EventType.MouseDrag:
                    HandleMouseDrag(currentEvent, id, targetCategory);
                    break;
                case EventType.MouseUp:
                    HandleMouseUp(currentEvent, id, targetCategory);
                    break;
            }
        }

        if (currentEvent.type == EventType.DragUpdated)
        {
            HandleDragUpdated(currentEvent, id);
        }

        if (currentEvent.type == EventType.DragPerform && categoryButtonRects.ContainsKey(targetCategory) &&
            categoryButtonRects[targetCategory].Contains(currentEvent.mousePosition))
        {
            HandleDragPerform(currentEvent, id, targetCategory);
        }

        if (currentEvent.type == EventType.DragExited)
        {
            HandleDragExited(currentEvent, id);
        }

        Repaint();
    }

    private void HandleMouseDown(Event currentEvent, string id, Category targetCategory, Category passedParentCategory)
    {
        if (currentEvent.button == 0) // Left mouse button
        {
            isMouseButtonDown = true;
            draggedCategoryId = id;
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData("draggedCategory", targetCategory);
            initialMousePosition = currentEvent.mousePosition;
        }
        else if (currentEvent.button == 2) // Middle mouse button
        {
            DeleteCategory(targetCategory);
        }
        else if (currentEvent.button == 1) // Right mouse button
        {
            foreach (Category topLevelCategory in categories)
            {
                if (topLevelCategory.children.Contains(targetCategory))
                {
                    EditCategory(targetCategory, topLevelCategory);
                    return;
                }
            }

            EditCategory(targetCategory, passedParentCategory);
        }

        currentEvent.Use();
    }

    private void HandleMouseDrag(Event currentEvent, string id, Category targetCategory)
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

    private void HandleMouseUp(Event currentEvent, string id, Category targetCategory)
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

    private void HandleDragUpdated(Event currentEvent, string id)
    {
        if (draggedCategoryId != null)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Move;
            currentEvent.Use();
        }
    }

    private void HandleDragPerform(Event currentEvent, string id, Category targetCategory)
    {
        DragAndDrop.AcceptDrag();
        Category draggedCategory = (Category)DragAndDrop.GetGenericData("draggedCategory");
        string draggedSubCategoryId = (string)DragAndDrop.GetGenericData("SubCategoryId");
        string draggedCategoryId = (string)DragAndDrop.GetGenericData("ParentCategoryId");

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
        else if (draggedSubCategoryId != null && draggedCategoryId != null)
        {
            Category subParentCategory = FindCategoryById(draggedCategoryId, categories);
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

    private void HandleDragExited(Event currentEvent, string id)
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
            EditorWindowUtility.ShowWindow("Add Category", EditorWindowUtility.WindowMode.AddCategory, "",
                (newName) => { AddNewCategory(newName); });
        }
        GUI.backgroundColor = Color.white;
    }

    private void AddNewCategory(string categoryName)
    {
        Category newCategory = new Category
        {
            id = Guid.NewGuid().ToString(),
            name = categoryName,
            fileGUIDs = new List<string>(),
            children = new List<Category>()
        };
        categories.Add(newCategory);
        SortCategoriesAlphabetically();
        activeCategoryId = newCategory.id; // Find the ID of the newly added category after sorting
        activeSubcategoryId = null; // Reset the activeSubcategoryId
        SaveCategories();
        Repaint();
    }

    private void SelectCategory(string id, Category category)
    {
        if (category != null)
        {
            activeCategoryId = id;
            activeSubcategoryId = EditorPrefsUtility.LoadActiveSubcategory(SharedLayoutMode, !IsDocked(), category.id);
            Repaint();
        }
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
            $"Left-Click to select, Right-Click to rename or Middle-Click to delete the {subCategory.name} subcategory.");
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
                    case 2: // Middle mouse button
                        DeleteSubCategory(category, subCategoryId);
                        currentEvent.Use();
                        break;
                    case 1: // Right mouse button
                        EditSubCategory(category, subCategoryId);
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
        
        if (currentEvent.type == EventType.MouseDrag && potentialDragOperation) {
			// If the mouse has moved a certain distance, start a drag operation
			if ((currentEvent.mousePosition - dragStartPosition).sqrMagnitude >
				dragStartThreshold * dragStartThreshold) {
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

	    fileButtonsArea = EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));
	    fileButtonsScrollPosition =
		    EditorGUILayout.BeginScrollView(fileButtonsScrollPosition, GUILayout.ExpandHeight(true));

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
			    // Default case, should not happen if the other flags are properly set
			    groupedFileGuids = new Dictionary<string, List<string>> { { "Files", category.fileGUIDs } };
		    }
	    }
	    else {
		    groupedFileGuids = new Dictionary<string, List<string>> { { "Files", category.fileGUIDs } };
	    }

	    groupedFileGuids = groupedFileGuids.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);

	    foreach (var fileGroup in groupedFileGuids) {
		    if(GroupItems) {
			    EditorGUILayout.BeginHorizontal();
			    EditorGUILayout.LabelField(fileGroup.Key, EditorStyles.boldLabel, GUILayout.Width(buttonWidth - 10));
			    EditorGUILayout.EndHorizontal();
		    }

		    fileGroup.Value.Sort((guid1, guid2) =>
			    string.Compare(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid1)),
				    Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid2)),
				    StringComparison.OrdinalIgnoreCase));

		    DrawFileButtonsGroup(fileGroup.Value, category);

		    if(GroupItems) {
			    GUILayout.Space(10); // Add space between groups
		    }
	    }

	    EditorGUILayout.EndScrollView();

	    EditorGUILayout.EndVertical();

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

                    if (timeSinceLastClick <= 0.3f) // Double-click
                    {
                        AssetDatabase.OpenAsset(fileObj);
                    }
                    else // Single-click
                    {
                        EditorGUIUtility.PingObject(fileObj);
                        Selection.activeObject = fileObj;
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
            GUI.Button(buttonRect, buttonContent, fileButtonStyle);
        }
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