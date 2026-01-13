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

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace QuickSelectEditor
{
    /// <summary>
    /// A Unity Editor window for quickly selecting and organizing project assets into categories.
    /// Supports personal and shared (cloud) layouts, drag-and-drop organization, and keyboard shortcuts.
    /// 
    /// This is the main partial class containing core window setup and state.
    /// Other functionality is split into:
    /// - QuickSelectWindow.Persistence.cs - Save/Load operations
    /// - QuickSelectWindow.Categories.cs - Category list and management
    /// - QuickSelectWindow.Subcategories.cs - Subcategory tabs and management  
    /// - QuickSelectWindow.FileButtons.cs - File button drawing and clicks
    /// - QuickSelectWindow.Selection.cs - Rectangle selection logic
    /// - QuickSelectWindow.Helpers.cs - Utility methods
    /// </summary>
    public partial class QuickSelectWindow : EditorWindow
    {
        // Core state
        private bool compactMode;
        private List<Category> categories;
        private string activeCategoryId;
        private string activeSubcategoryId;
        private string draggedCategoryId;
        private string draggedSubCategoryId;
        private bool lastDockState;

        // Category List Variables
        private Vector2 scrollPosition;
        private bool isMouseButtonDown = false;
        private bool categoryDroppedSuccessfully;
        private Dictionary<Category, Rect> categoryButtonRects = new Dictionary<Category, Rect>();
        private int indentWidth = 20;
        private Vector2 initialMousePosition;

        // Subcategory Variables
        private bool potentialDragOperation;
        private Vector2 dragStartPosition;
        private const float dragStartThreshold = 5f;

        // File Button Variables
        private Vector2 fileButtonsScrollPosition;
        private Rect fileButtonsArea;
        private bool rightClickOnFileButton;
        private float lastClickTime;
        private string m_lastClickedFileGuid;
        private bool isLeftMouseDown;
        private bool dragStarted;
        
        // Captured modifier state from MouseDown (because MouseUp may not reliably report modifiers)
        private bool m_clickHadShift;
        private bool m_clickHadCtrlOrCmd;
        
        // Rectangle Selection Variables
        private bool m_isRectSelecting;
        private Vector2 m_rectSelectStart;
        private Vector2 m_rectSelectEnd;
        private Dictionary<string, Rect> m_fileButtonRects = new Dictionary<string, Rect>();
        private Dictionary<string, UnityEngine.Object> m_fileButtonObjects = new Dictionary<string, UnityEngine.Object>();
        private HashSet<string> m_rectSelectionGuids = new HashSet<string>();
        private HashSet<string> m_lastRectSelectionGuids = new HashSet<string>();
        private Dictionary<int, UnityEngine.Object> m_rectBaseSelectionById = new Dictionary<int, UnityEngine.Object>();
        private HashSet<int> m_lastAppliedSelectionIds = new HashSet<int>();
        private HashSet<int> m_cachedSelectionInstanceIds = new HashSet<int>();

        // Scratch buffers to avoid allocations during drag
        private HashSet<int> m_rectIdsScratch = new HashSet<int>();
        private Dictionary<int, UnityEngine.Object> m_rectByIdScratch = new Dictionary<int, UnityEngine.Object>();
        private HashSet<int> m_desiredIdsScratch = new HashSet<int>();
        private List<UnityEngine.Object> m_desiredObjectsScratch = new List<UnityEngine.Object>();

        // Throttle how often we push into Unity's Selection while dragging (Selection updates can be expensive)
        private double m_lastSelectionApplyTime;
        private const double c_selectionApplyIntervalSeconds = 0.08; // ~12 updates/sec (smoother feel)
        
        // Cached grouped/sorted file data (avoid recomputing every frame)
        private Dictionary<string, List<string>> m_cachedGroupedFiles;
        private string m_cachedGroupingCategoryId;
        private bool m_cachedGroupByFolder;
        private bool m_cachedGroupByFileType;
        private bool m_cachedGroupAlphabetically;
        private bool m_cachedGroupItems;
        private int m_cachedFileCount;
        
        private void InvalidateFileGroupingCache()
        {
            m_cachedGroupedFiles = null;
        }

        // Cached GUIStyles (to avoid allocations in OnGUI)
        private GUIStyle cachedToggleButtonStyle;
        private GUIStyle cachedCategoryButtonStyle;
        private GUIStyle cachedSubCategoryButtonStyle;
        private GUIStyle cachedFileButtonStyle;
        private GUIStyle cachedTextWidthStyle;

        // Windows / Modes
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

            // Set Min Size
            this.minSize = new Vector2(200, 200);

            // Load the state
            LoadCategories();
            LoadWindowState();
            
            // Migration helper: older saved layouts may not have IDs. If IDs are missing, regenerate them once.
            CheckAndUpdateCategoryIDs();
            
            // Start watching for SharedLayout.json file changes
            SharedLayoutFileWatcher.StartWatching();
            
            // Subscribe to selection changes to highlight selected assets
            Selection.selectionChanged += OnSelectionChanged;
            UpdateCachedUnitySelection();
        }
        
        private void OnSelectionChanged()
        {
            UpdateCachedUnitySelection();
            Repaint();
        }

        private void OnDisable()
        {
            // Save the state
            SaveWindowState();

            // Save the layout mode for the instance
            EditorPrefs.SetBool(docked ? DockedLayoutModeKey : FloatingLayoutModeKey, SharedLayoutMode);

            // Update the Open Instances list
            openInstances.Remove(this);
            
            // Stop watching for SharedLayout.json file changes
            SharedLayoutFileWatcher.StopWatching();
            
            // Unsubscribe from selection changes
            Selection.selectionChanged -= OnSelectionChanged;
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
            
            if (changedDockState)
            {
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
        /// Used when shared layout changes are detected externally (e.g., by another user).
        /// </summary>
        public static void RequestRepaintAllInstances()
        {
            // Skip if we're in the middle of saving (prevents duplicate reload)
            if (s_isSaving)
            {
                return;
            }
            
            EditorApplication.delayCall += () =>
            {
                foreach (var instance in openInstances)
                {
                    instance.RefreshAndRepaint();
                }
            };
        }

        /// <summary>
        /// Requests other instances to reload from disk, while only repainting the current instance.
        /// Used after local changes that have already been saved.
        /// </summary>
        private void RequestRepaintOtherInstances()
        {
            // Current instance already has correct data - just repaint
            Repaint();
            
            // Other instances need to reload from disk
            EditorApplication.delayCall += () =>
            {
                foreach (var instance in openInstances)
                {
                    if (instance != this)
                    {
                        instance.RefreshAndRepaint();
                    }
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
        
        
        // Toolbar
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.ExpandWidth(true));

            // Personal and Shared Layout Toggle
            bool isSharedLayoutMode = SharedLayoutMode;

            // Use cached GUIStyle for the toggle button
            if (cachedToggleButtonStyle == null)
            {
                cachedToggleButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
                cachedToggleButtonStyle.alignment = TextAnchor.MiddleLeft;
            }

            // Shared Layout Button
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

            // Settings Button
            GUIContent settingsButtonContent = EditorGUIUtility.IconContent("_Popup");
            if (GUILayout.Button(settingsButtonContent, EditorStyles.toolbarButton))
            {
                QuickSelectEditorSettings.ShowWindow();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
