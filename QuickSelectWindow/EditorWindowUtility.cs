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
        public OnActionDelegate OnAction;

        private string oldName;
        private string newName = "";
        private string controlName = "CategoryNameTextField";

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
            EditorGUILayout.BeginHorizontal();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                if (!string.IsNullOrEmpty(newName) && !newName.Equals(oldName))
                {
                    OnAction?.Invoke(newName);
                    Close();
                }
                Event.current.Use();
            }

            if (Event.current.type == EventType.Layout)
            {
                EditorGUI.FocusTextInControl(controlName);
            }

            GUI.SetNextControlName(controlName);
            newName = EditorGUILayout.TextField(newName);

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button(buttonText))
            {
                if (!string.IsNullOrEmpty(newName) && !newName.Equals(oldName))
                {
                    OnAction?.Invoke(newName);
                    Close();
                }
            }
        }
    }
}
