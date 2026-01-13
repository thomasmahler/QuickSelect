<img width="1936" height="1262" alt="quickSelect" src="https://github.com/user-attachments/assets/d1c8a91e-da1f-4bec-8f74-2e099cb1fed2" />

# Quick Select Editor

A Unity Editor tool for quickly organizing and accessing your project assets. Create categories and subcategories to group your frequently used assets for fast access.

## Features

- **Quick Access** - Press `Shift+Q` to toggle the Quick Select window
- **Categories & Subcategories** - Organize assets into nested categories
- **Drag & Drop** - Easily add assets by dragging them into the window
- **Personal & Cloud Layouts** - Switch between personal layouts (stored locally) and shared layouts (stored in a JSON file that can be synced across team members)
- **Compact Mode** - Window automatically adapts to narrow widths
- **Grouping Options** - Group files by type, alphabetically, or by folder
- **File Operations** - Single-click to select, double-click to open, middle-click to remove

## Installation

1. Copy the `QuickSelectWindow` folder into any `Editor` folder in your Unity project (for example: `Assets/Editor/QuickSelectWindow/`)
2. Open Unity and wait for scripts to compile
3. Access via `Window > Quick Select` or press `Shift+Q`

## Usage

### Opening the Window
- Use the menu: `Window > Quick Select`
- Or press `Shift+Q` to toggle the floating window

### Creating Categories
1. Click the `[+] Add Category` button in the left panel
2. Enter a name for your category
3. Categories are automatically sorted alphabetically

### Adding Assets
- **Drag & Drop**: Drag assets from the Project window into the Quick Select window
- Assets are added to the currently selected subcategory
- If no subcategory exists, one is automatically created

### Organizing
- **Left-click** a category to select it
- **Right-click** a category to rename it
- **Middle-click** a category to delete it
- **Drag** categories onto other categories to nest them
- **Drag** subcategories to move them between categories

### Layout Modes
- **Personal**: Stored in Unity's EditorPrefs (local to your machine)
- **Cloud**: Stored in `SharedLayout.json` at your project root (can be shared via version control)

Toggle between modes using the toolbar button or context menu.

### Settings
Click the settings icon in the toolbar to configure:
- Show subcategory count on category buttons
- Show file count on subcategories
- Adjust button fonts based on editor size
- Context menu display options

## Requirements

- Unity 2019.4 or later
- Editor-only (no runtime dependencies)

## License

This project is licensed under the GNU General Public License v3.0 - see the license headers in each source file for details.

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.
