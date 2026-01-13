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

namespace QuickSelectEditor
{
    /// <summary>
    /// Represents a category or subcategory in the Quick Select window.
    /// Categories can contain file references (GUIDs), subcategories, and child categories.
    /// </summary>
    [System.Serializable]
    public class Category
    {
        public string id;
        public string name;
        public List<string> fileGUIDs;
        public List<Category> subCategories;
        public List<Category> children;

        public Category()
        {
            subCategories = new List<Category>();
            children = new List<Category>();
        }
    }
}