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
    /// Settings window for configuring Quick Select Editor preferences.
    /// </summary>
    public class QuickSelectEditorSettings : EditorWindow
    {
        public const string ShowSubcategoryCountOnCategoryButtonsKey = "QuickSelectEditor.ShowSubcategoryCountOnCategoryButtons";
        public const string ShowFileCountOnSubcategoriesKey = "QuickSelectEditor.ShowFileCountOnSubcategories";
        public const string AdjustButtonFontsKey = "QuickSelectEditor.AdjustButtonFontsBasedOnEditorSize";
        public const string ShowLayoutModeKey = "QuickSelectEditor.ShowLayoutModeInContextMenu";
        public const string ShowCategoriesGroupKey = "QuickSelectEditor_ShowCategoriesGroupInContextMenu";

        public static void ShowWindow()
        {
            QuickSelectEditorSettings window = GetWindow<QuickSelectEditorSettings>("Quick Select Editor Settings");
            window.minSize = new Vector2(330, 300);
            window.maxSize = new Vector2(330, 500);
            window.Show();
        }

        private void OnGUI()
        {
            int labelWidth = 300;
            GUIStyle settingsBackgroundStyle = new GUIStyle();
            settingsBackgroundStyle.normal.background = MakeTexture(1, 1, new Color(0.2f, 0.2f, 0.2f, 1f));

            // General Settings:
            EditorGUILayout.LabelField("General Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal(settingsBackgroundStyle);
            {
                EditorGUILayout.LabelField("Show Subcategory Count on Category Buttons", GUILayout.Width(labelWidth));
                bool showSubcategoryCountOnCategoryButtons =
                    EditorPrefs.GetBool(ShowSubcategoryCountOnCategoryButtonsKey, false);
                bool newShowSubcategoryCountOnCategoryButtons =
                    EditorGUILayout.Toggle(showSubcategoryCountOnCategoryButtons);
                if (showSubcategoryCountOnCategoryButtons != newShowSubcategoryCountOnCategoryButtons)
                {
                    EditorPrefs.SetBool(ShowSubcategoryCountOnCategoryButtonsKey, newShowSubcategoryCountOnCategoryButtons);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal(settingsBackgroundStyle);
            {
                EditorGUILayout.LabelField("Show File Count on Subcategories", GUILayout.Width(labelWidth));
                bool showFileCountOnSubcategories = EditorPrefs.GetBool(ShowFileCountOnSubcategoriesKey, false);
                bool newShowFileCountOnSubcategories = EditorGUILayout.Toggle(showFileCountOnSubcategories);
                if (showFileCountOnSubcategories != newShowFileCountOnSubcategories)
                {
                    EditorPrefs.SetBool(ShowFileCountOnSubcategoriesKey, newShowFileCountOnSubcategories);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal(settingsBackgroundStyle);
            {
                EditorGUILayout.LabelField("Adjust Button Fonts based on Editor Size", GUILayout.Width(labelWidth));
                bool adjustButtonFonts = EditorPrefs.GetBool(AdjustButtonFontsKey, false); // Set the default value to false
                bool newAdjustButtonFonts = EditorGUILayout.Toggle(adjustButtonFonts);
                if (adjustButtonFonts != newAdjustButtonFonts)
                {
                    EditorPrefs.SetBool(AdjustButtonFontsKey, newAdjustButtonFonts);
                    QuickSelectWindow.RequestRepaintAllInstances();
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            //Context Menu Settings:
            EditorGUILayout.LabelField("Context Menu Settings", EditorStyles.boldLabel);

            bool showCategoriesGroupInContextMenu = EditorPrefs.GetBool(ShowCategoriesGroupKey, true);
            bool showLayoutModeInContextMenu = EditorPrefs.GetBool(ShowLayoutModeKey, true);

            EditorGUILayout.BeginHorizontal(settingsBackgroundStyle);
            {
                EditorGUILayout.LabelField("Show Layout Mode in Context Menu", GUILayout.Width(labelWidth));
                bool newShowLayoutModeInContextMenu = EditorGUILayout.Toggle(showLayoutModeInContextMenu);
                if (showLayoutModeInContextMenu != newShowLayoutModeInContextMenu)
                {
                    EditorPrefs.SetBool(ShowLayoutModeKey, newShowLayoutModeInContextMenu);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal(settingsBackgroundStyle);
            {
                EditorGUILayout.LabelField("Group Categories", GUILayout.Width(labelWidth));
                bool newShowCategoriesGroupInContextMenu = EditorGUILayout.Toggle(showCategoriesGroupInContextMenu);
                if (showCategoriesGroupInContextMenu != newShowCategoriesGroupInContextMenu)
                {
                    EditorPrefs.SetBool(ShowCategoriesGroupKey, newShowCategoriesGroupInContextMenu);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);

            // Add more settings and categories here
        }

        // Helper method to create a texture with a solid color
        private Texture2D MakeTexture(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];

            for (int i = 0; i < pix.Length; i++)
            {
                pix[i] = col;
            }

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();

            return result;
        }
    }
}