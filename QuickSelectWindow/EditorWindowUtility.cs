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
    /// A utility popup window for adding or renaming categories and items.
    /// </summary>
    public class EditorWindowUtility : EditorWindow
    {
        public enum WindowMode { AddCategory, RenameCategory, RenameItem }
        public WindowMode Mode;

        public delegate void OnActionDelegate(string input);
        public delegate void OnAddCategoryDelegate(string name, Category parentCategory);
        
        public OnActionDelegate OnAction;
        public OnAddCategoryDelegate OnAddCategory;

        private string oldName;
        private string newName = "";
        private string controlName = "CategoryNameTextField";
        
        // For parent category selection
        private List<Category> m_categories;
        private Category m_selectedParent;
        private string m_selectedParentDisplayName;

        private static EditorWindowUtility instance;

        private void Awake()
        {
            Vector2 fixedSize = new Vector2(300, 80);
            minSize = fixedSize;
            maxSize = fixedSize;
        }

        public static void ShowWindow(string title, WindowMode mode, string oldName, OnActionDelegate onAction)
        {
            if (instance == null)
            {
                instance = CreateInstance<EditorWindowUtility>();
            }
            instance.titleContent = new GUIContent(title);
            instance.Mode = mode;
            instance.oldName = oldName;
            instance.newName = oldName;
            instance.OnAction = onAction;
            instance.OnAddCategory = null;
            instance.m_categories = null;
            instance.m_selectedParent = null;
            instance.m_selectedParentDisplayName = null;
            
            instance.minSize = new Vector2(300, 80);
            instance.maxSize = new Vector2(300, 80);
            
            instance.ShowUtility();
            instance.Focus();
        }

        public static void ShowAddCategoryWindow(string title, List<Category> categories, Category preselectedParent, OnAddCategoryDelegate onAddCategory)
        {
            if (instance == null)
            {
                instance = CreateInstance<EditorWindowUtility>();
            }
            instance.titleContent = new GUIContent(title);
            instance.Mode = WindowMode.AddCategory;
            instance.oldName = "";
            instance.newName = "";
            instance.OnAction = null;
            instance.OnAddCategory = onAddCategory;
            instance.m_categories = categories;
            instance.m_selectedParent = preselectedParent;
            instance.m_selectedParentDisplayName = preselectedParent != null ? preselectedParent.name : "Root";
            
            instance.minSize = new Vector2(300, 105);
            instance.maxSize = new Vector2(300, 105);
            
            instance.ShowUtility();
            instance.Focus();
        }

        private void OnGUI()
        {
            string labelText = "";
            string buttonText = "";

            switch (Mode)
            {
                case WindowMode.AddCategory:
                    labelText = "Category Name:";
                    buttonText = "Add Category";
                    break;
                case WindowMode.RenameCategory:
                    labelText = $"Rename Category {oldName}:";
                    buttonText = "Rename";
                    break;
                case WindowMode.RenameItem:
                    labelText = $"Rename Item {oldName}:";
                    buttonText = "Rename";
                    break;
            }

            GUILayout.Label(labelText, EditorStyles.boldLabel);

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                TrySubmit();
                Event.current.Use();
            }

            if (Event.current.type == EventType.Layout)
            {
                EditorGUI.FocusTextInControl(controlName);
            }

            GUI.SetNextControlName(controlName);
            newName = EditorGUILayout.TextField(newName);

            // Show parent selector for AddCategory mode with categories
            if (Mode == WindowMode.AddCategory && m_categories != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Parent:", GUILayout.Width(45));
                if (GUILayout.Button(m_selectedParentDisplayName, EditorStyles.popup))
                {
                    ShowParentSelectionMenu();
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button(buttonText))
            {
                TrySubmit();
            }
        }

        private void TrySubmit()
        {
            if (string.IsNullOrEmpty(newName))
            {
                return;
            }
            
            if (OnAddCategory != null && m_categories != null)
            {
                OnAddCategory.Invoke(newName, m_selectedParent);
                Close();
            }
            else if (OnAction != null && !newName.Equals(oldName))
            {
                OnAction.Invoke(newName);
                Close();
            }
        }

        private void ShowParentSelectionMenu()
        {
            GenericMenu menu = new GenericMenu();
            
            // Root option
            bool isRoot = m_selectedParent == null;
            menu.AddItem(new GUIContent("Root"), isRoot, () => SelectParent(null, "Root"));
            
            menu.AddSeparator("");
            
            // Add all categories with nested submenus
            for (int i = 0; i < m_categories.Count; i++)
            {
                AddParentMenuItem(menu, m_categories[i], "");
            }
            
            menu.ShowAsContext();
        }

        private void AddParentMenuItem(GenericMenu menu, Category category, string menuPath)
        {
            bool isSelected = m_selectedParent != null && m_selectedParent.id == category.id;
            bool hasChildren = category.children != null && category.children.Count > 0;
            
            if (hasChildren)
            {
                // Category has children - create submenu with "Select" option
                menu.AddItem(new GUIContent(menuPath + category.name + "/• Select"), isSelected, () => SelectParent(category, category.name));
                menu.AddSeparator(menuPath + category.name + "/");
                
                // Add children
                for (int i = 0; i < category.children.Count; i++)
                {
                    AddParentMenuItem(menu, category.children[i], menuPath + category.name + "/");
                }
            }
            else
            {
                // Simple menu item
                menu.AddItem(new GUIContent(menuPath + category.name), isSelected, () => SelectParent(category, category.name));
            }
        }

        private void SelectParent(Category parent, string displayName)
        {
            m_selectedParent = parent;
            m_selectedParentDisplayName = displayName;
            Repaint();
        }
    }
}
