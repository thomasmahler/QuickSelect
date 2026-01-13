// Quick Select Editor - A Unity Editor tool for organizing project assets
// Copyright (C) 2025  Thomas Mahler
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace QuickSelectEditor
{
    /// <summary>
    /// Partial class containing rectangle selection and Unity Selection integration.
    /// </summary>
    public partial class QuickSelectWindow
    {
        private void HandleRectangleSelection()
        {
            Event currentEvent = Event.current;

            // Compare in screen space (handles scroll/clipping automatically).
            Vector2 mouseScreenPos = GUIUtility.GUIToScreenPoint(currentEvent.mousePosition);
            Rect currentSelectionRectScreen = default;
            if (m_isRectSelecting)
            {
                Rect currentSelectionRect = GetSelectionRect(m_rectSelectStart, m_rectSelectEnd);
                currentSelectionRectScreen = GUIUtility.GUIToScreenRect(currentSelectionRect);
            }
            
            // Start rectangle selection on mouse down in empty area
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && 
                fileButtonsArea.Contains(currentEvent.mousePosition))
            {
                // Check if click is on a file button (screen space)
                bool clickedOnButton = false;
                foreach (var kvp in m_fileButtonRects)
                {
                    if (kvp.Value.Contains(mouseScreenPos))
                    {
                        clickedOnButton = true;
                        break;
                    }
                }
                
                // Start rect selection only if not clicking on a button
                if (!clickedOnButton)
                {
                    m_isRectSelecting = true;
                    m_rectSelectStart = currentEvent.mousePosition;
                    m_rectSelectEnd = currentEvent.mousePosition;
                    m_rectSelectionGuids.Clear();
                    m_lastRectSelectionGuids.Clear();
                    m_lastAppliedSelectionIds.Clear();

                    // Cache selection at start of drag (used for Shift/Ctrl behaviors)
                    m_rectBaseSelectionById.Clear();
                    var baseSelection = Selection.objects;
                    for (int i = 0; i < baseSelection.Length; i++)
                    {
                        var obj = baseSelection[i];
                        if (obj == null) continue;
                        int id = obj.GetInstanceID();
                        if (!m_rectBaseSelectionById.ContainsKey(id))
                        {
                            m_rectBaseSelectionById.Add(id, obj);
                        }
                    }
                    
                    // If no modifiers held, immediately clear selection (OS-style behavior)
                    bool hasModifier = currentEvent.shift || currentEvent.control || currentEvent.command;
                    if (!hasModifier && Selection.objects.Length > 0)
                    {
                        Selection.objects = System.Array.Empty<UnityEngine.Object>();
                        UpdateCachedUnitySelection();
                    }
                    
                    currentEvent.Use();
                    Repaint();
                }
            }
            
            // Update rectangle during drag
            if (m_isRectSelecting && currentEvent.type == EventType.MouseDrag)
            {
                m_rectSelectEnd = currentEvent.mousePosition;
                UpdateLiveRectangleSelection(currentEvent);
                currentEvent.Use();
                Repaint();
            }
            
            // Complete selection on mouse up
            if (m_isRectSelecting && currentEvent.type == EventType.MouseUp)
            {
                m_isRectSelecting = false;
                
                Rect selectionRect = GetSelectionRect(m_rectSelectStart, m_rectSelectEnd);
                Rect selectionRectScreen = GUIUtility.GUIToScreenRect(selectionRect);
                
                // Only select if the rectangle has some size (not just a click)
                if (selectionRect.width > 5 && selectionRect.height > 5)
                {
                    List<UnityEngine.Object> selectedObjects = new List<UnityEngine.Object>();
                    
                    // Use cached objects instead of loading from disk (much faster)
                    foreach (var kvp in m_fileButtonRects)
                    {
                        if (selectionRectScreen.Overlaps(kvp.Value))
                        {
                            if (m_fileButtonObjects.TryGetValue(kvp.Key, out var obj) && obj != null)
                            {
                                selectedObjects.Add(obj);
                            }
                        }
                    }
                    
                    bool isCtrlHeld = currentEvent.control || currentEvent.command;
                    bool isShiftHeld = currentEvent.shift;
                    
                    if (isCtrlHeld)
                    {
                        // Ctrl: toggle items in rectangle relative to base selection
                        var result = new List<UnityEngine.Object>();
                        HashSet<int> rectangleIds = new HashSet<int>();
                        foreach (var obj in selectedObjects)
                        {
                            if (obj != null) rectangleIds.Add(obj.GetInstanceID());
                        }
                        
                        // Start with base selection
                        foreach (var kvp in m_rectBaseSelectionById)
                        {
                            if (kvp.Value != null && !rectangleIds.Contains(kvp.Key))
                            {
                                // Item was selected but NOT in rectangle - keep it
                                result.Add(kvp.Value);
                            }
                            // Item was selected AND in rectangle - toggle off (don't add)
                        }
                        
                        // Add items in rectangle that weren't in base selection
                        foreach (var obj in selectedObjects)
                        {
                            if (obj != null && !m_rectBaseSelectionById.ContainsKey(obj.GetInstanceID()))
                            {
                                result.Add(obj);
                            }
                        }
                        
                        Selection.objects = result.ToArray();
                    }
                    else if (isShiftHeld)
                    {
                        // Shift: add to base selection
                        var combined = new List<UnityEngine.Object>();
                        foreach (var kvp in m_rectBaseSelectionById)
                        {
                            if (kvp.Value != null) combined.Add(kvp.Value);
                        }
                        foreach (var obj in selectedObjects)
                        {
                            if (obj != null && !m_rectBaseSelectionById.ContainsKey(obj.GetInstanceID()))
                            {
                                combined.Add(obj);
                            }
                        }
                        Selection.objects = combined.ToArray();
                    }
                    else
                    {
                        // No modifiers: select only what's in rectangle
                        Selection.objects = selectedObjects.ToArray();
                    }
                }
                else
                {
                    // Single click on empty space should clear selection (OS-style),
                    // but keep modifier behavior (Shift/Ctrl/Cmd) unchanged.
                    bool isToggle = currentEvent.control || currentEvent.command;
                    bool isAdd = currentEvent.shift;
                    if (!isToggle && !isAdd)
                    {
                        Selection.objects = System.Array.Empty<UnityEngine.Object>();
                        UpdateCachedUnitySelection();
                    }
                }
                
                m_rectSelectionGuids.Clear();
                m_lastRectSelectionGuids.Clear();
                m_rectBaseSelectionById.Clear();
                m_lastAppliedSelectionIds.Clear();
                currentEvent.Use();
                Repaint();
            }
            
            // Draw selection rectangle
            if (m_isRectSelecting && currentEvent.type == EventType.Repaint)
            {
                // Ensure live highlight stays up to date even if Unity triggers repaint without drag event
                UpdateLiveRectangleSelection(currentEvent);

                Rect selectionRect = GetSelectionRect(m_rectSelectStart, m_rectSelectEnd);
                
                // Draw filled rectangle
                EditorGUI.DrawRect(selectionRect, new Color(0f, 0.5f, 1f, 0.25f));
                
                // Draw border
                Handles.BeginGUI();
                Handles.color = new Color(0f, 0.5f, 1f, 0.8f);
                Handles.DrawLine(new Vector3(selectionRect.xMin, selectionRect.yMin, 0), new Vector3(selectionRect.xMax, selectionRect.yMin, 0));
                Handles.DrawLine(new Vector3(selectionRect.xMax, selectionRect.yMin, 0), new Vector3(selectionRect.xMax, selectionRect.yMax, 0));
                Handles.DrawLine(new Vector3(selectionRect.xMax, selectionRect.yMax, 0), new Vector3(selectionRect.xMin, selectionRect.yMax, 0));
                Handles.DrawLine(new Vector3(selectionRect.xMin, selectionRect.yMax, 0), new Vector3(selectionRect.xMin, selectionRect.yMin, 0));
                Handles.EndGUI();
            }
        }
        
        private void UpdateLiveRectangleSelection(Event currentEvent)
        {
            // Build current set of GUIDs inside selection rectangle (screen space)
            Rect selectionRect = GetSelectionRect(m_rectSelectStart, m_rectSelectEnd);
            Rect selectionRectScreen = GUIUtility.GUIToScreenRect(selectionRect);

            m_rectSelectionGuids.Clear();
            foreach (var kvp in m_fileButtonRects)
            {
                if (selectionRectScreen.Overlaps(kvp.Value))
                {
                    m_rectSelectionGuids.Add(kvp.Key);
                }
            }

            // Update Unity selection during drag (only if something changed).
            if (currentEvent.type != EventType.MouseDrag)
            {
                return;
            }

            // Throttle Selection.objects updates (expensive)
            double now = EditorApplication.timeSinceStartup;
            if (now - m_lastSelectionApplyTime < c_selectionApplyIntervalSeconds)
            {
                return;
            }

            m_lastRectSelectionGuids.Clear();
            foreach (var guid in m_rectSelectionGuids)
            {
                m_lastRectSelectionGuids.Add(guid);
            }

            bool isToggle = currentEvent.control || currentEvent.command;
            bool isAdd = currentEvent.shift;

            // Build rect selection as instance IDs
            m_rectIdsScratch.Clear();
            m_rectByIdScratch.Clear();
            foreach (var guid in m_rectSelectionGuids)
            {
                if (m_fileButtonObjects.TryGetValue(guid, out var obj) && obj != null)
                {
                    int id = obj.GetInstanceID();
                    if (!m_rectIdsScratch.Contains(id))
                    {
                        m_rectIdsScratch.Add(id);
                        m_rectByIdScratch[id] = obj;
                    }
                }
            }

            // Compute desired selection (OS-style live selection)
            m_desiredIdsScratch.Clear();

            if (isToggle)
            {
                // Ctrl/Cmd: toggle relative to selection at drag start
                foreach (var kvp in m_rectBaseSelectionById)
                {
                    m_desiredIdsScratch.Add(kvp.Key);
                }

                foreach (var id in m_rectIdsScratch)
                {
                    if (m_desiredIdsScratch.Contains(id))
                    {
                        m_desiredIdsScratch.Remove(id);
                    }
                    else
                    {
                        m_desiredIdsScratch.Add(id);
                    }
                }
            }
            else if (isAdd)
            {
                // Shift: add rectangle selection to selection at drag start
                foreach (var kvp in m_rectBaseSelectionById)
                {
                    m_desiredIdsScratch.Add(kvp.Key);
                }
                foreach (var id in m_rectIdsScratch)
                {
                    m_desiredIdsScratch.Add(id);
                }
            }
            else
            {
                // No modifiers: selection is exactly what's inside the rectangle (and can become empty)
                foreach (var id in m_rectIdsScratch)
                {
                    m_desiredIdsScratch.Add(id);
                }
            }

            // Only apply if changed
            if (m_desiredIdsScratch.SetEquals(m_lastAppliedSelectionIds))
            {
                return;
            }

            m_lastAppliedSelectionIds.Clear();
            foreach (var id in m_desiredIdsScratch)
            {
                m_lastAppliedSelectionIds.Add(id);
            }

            // Convert instance IDs to objects
            m_desiredObjectsScratch.Clear();
            foreach (var id in m_desiredIdsScratch)
            {
                if (m_rectByIdScratch.TryGetValue(id, out var rectObj) && rectObj != null)
                {
                    m_desiredObjectsScratch.Add(rectObj);
                    continue;
                }
                if (m_rectBaseSelectionById.TryGetValue(id, out var baseObj) && baseObj != null)
                {
                    m_desiredObjectsScratch.Add(baseObj);
                }
            }

            Selection.objects = m_desiredObjectsScratch.ToArray();
            m_lastSelectionApplyTime = now;
            UpdateCachedUnitySelection();
        }

        private void UpdateCachedUnitySelection()
        {
            m_cachedSelectionInstanceIds.Clear();
            var selected = Selection.objects;
            for (int i = 0; i < selected.Length; i++)
            {
                var obj = selected[i];
                if (obj == null) continue;
                m_cachedSelectionInstanceIds.Add(obj.GetInstanceID());
            }
        }

        private Rect GetSelectionRect(Vector2 start, Vector2 end)
        {
            float x = Mathf.Min(start.x, end.x);
            float y = Mathf.Min(start.y, end.y);
            float width = Mathf.Abs(end.x - start.x);
            float height = Mathf.Abs(end.y - start.y);
            return new Rect(x, y, width, height);
        }
    }
}
