// <copyright file="NearestEnemyCore.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace NearestEnemyHighlight
{
    using ClickableTransparentOverlay.Win32;
    using GameHelper;
    using GameHelper.Plugin;
    using GameHelper.RemoteEnums.Entity;
    using GameHelper.RemoteObjects.Components;
    using GameHelper.RemoteObjects.States.InGameStateObjects;
    using GameHelper.Utils;
    using ImGuiNET;
    using Newtonsoft.Json;
    using System;
    using System.IO;
    using System.Linq;
    using System.Numerics;

    using System.Collections.Generic;

    /// <summary>
    /// Nearest Enemy Highlight plugin that draws a circle around the closest enemy.
    /// </summary>
    public sealed class NearestEnemyCore : PCore<NearestEnemySettings>
    {
        private Entity nearestEnemy;
        private float nearestDistance;
        private string settingsPath;

        private int selectedRuleIndex = -1; // For UI rule management

        /// <summary>
        /// Initializes a new instance of the <see cref="NearestEnemyCore"/> class.
        /// </summary>
        public NearestEnemyCore()
        {
            this.nearestEnemy = null;
            this.nearestDistance = float.MaxValue;
        }

        /// <inheritdoc/>
        public override void OnEnable(bool isGameOpened)
        {
            // Set up settings file path
            this.settingsPath = Path.Join(this.DllDirectory, "config", "settings.json");

            // Load settings if they exist
            this.LoadSettings();

            // Initialize mouse compatibility helper
            //MouseCompatibilityHelper.Initialize();
        }

        /// <inheritdoc/>
        public override void OnDisable()
        {
            // Save settings when disabling
            this.SaveSettings();

            // Clear references
            this.nearestEnemy = null;
        }

        /// <inheritdoc/>
        public override void DrawSettings()
        {
            ImGui.Text("Nearest Enemy Highlight Settings");
            ImGui.Separator();

            // Create temporary variables for the sliders
            bool enable = this.Settings.Enable;
            float maxDistance = this.Settings.MaxSearchDistance;
            float circleRadius = this.Settings.CircleRadius;
            float circleThickness = this.Settings.CircleThickness;
            int r = this.Settings.CircleColorR;
            int g = this.Settings.CircleColorG;
            int b = this.Settings.CircleColorB;
            int a = this.Settings.CircleColorA;
            bool showDebug = this.Settings.ShowDebugInfo;

            if (ImGui.Checkbox("Enable Plugin", ref enable))
                this.Settings.Enable = enable;

            // UPDATED: Max search distance with precise input
            if (ImGui.DragFloat("Max Search Distance", ref maxDistance, 1.0f, 10.0f, 500.0f, "%.1f"))
            {
                this.Settings.MaxSearchDistance = Math.Max(10.0f, Math.Min(500.0f, maxDistance));
            }
            ImGuiHelper.ToolTip("Maximum distance to search for enemies. Click to type exact value.");

            // UPDATED: Circle radius with precise input  
            if (ImGui.DragFloat("Circle Radius", ref circleRadius, 0.5f, 5.0f, 200.0f, "%.1f"))
            {
                this.Settings.CircleRadius = Math.Max(5.0f, Math.Min(200.0f, circleRadius));
            }
            ImGuiHelper.ToolTip("Size of circle drawn around nearest enemy. Click to type exact value.");

            // UPDATED: Circle thickness with precise input
            if (ImGui.DragFloat("Circle Thickness", ref circleThickness, 0.1f, 1.0f, 20.0f, "%.1f"))
            {
                this.Settings.CircleThickness = Math.Max(1.0f, Math.Min(20.0f, circleThickness));
            }
            ImGuiHelper.ToolTip("Line thickness of the circle. Click to type exact value.");

            ImGui.Text("Circle Color:");
            if (ImGui.SliderInt("Red", ref r, 0, 255))
                this.Settings.CircleColorR = (byte)r;
            if (ImGui.SliderInt("Green", ref g, 0, 255))
                this.Settings.CircleColorG = (byte)g;
            if (ImGui.SliderInt("Blue", ref b, 0, 255))
                this.Settings.CircleColorB = (byte)b;
            if (ImGui.SliderInt("Alpha", ref a, 0, 255))
                this.Settings.CircleColorA = (byte)a;

            if (ImGui.Checkbox("Show Debug Info", ref showDebug))
                this.Settings.ShowDebugInfo = showDebug;

            ImGui.Separator();
            ImGui.Text("Cursor Tracking:");

            bool enableTracking = this.Settings.EnableCursorTracking;
            if (ImGui.Checkbox("Enable Cursor Tracking", ref enableTracking))
                this.Settings.EnableCursorTracking = enableTracking;

            bool pauseUI = this.Settings.PauseDuringUI;
            if (ImGui.Checkbox("Pause During UI", ref pauseUI))
                this.Settings.PauseDuringUI = pauseUI;

            VK toggleKey = this.Settings.ToggleHotKey;
            ImGui.Text("Toggle Hotkey: ");
            ImGui.SameLine();
            if (ImGuiHelper.NonContinuousEnumComboBox("##ToggleHotKey", ref toggleKey))
            {
                this.Settings.ToggleHotKey = toggleKey;
            }
            ImGuiHelper.ToolTip("Press this key in-game to toggle cursor tracking on/off");

            if (ImGui.Button("Save Settings"))
            {
                this.SaveSettings();
            }

            // CORRECTED: Always-visible cursor rules configuration
            ImGui.Separator();
            ImGui.Text("Cursor Detection Rules:");

            // Add new rule button
            if (ImGui.Button("Add New Rule"))
            {
                this.Settings.CursorRules.Add(CursorRule.CreateDefault());
            }
            ImGui.SameLine();

            // Show rule count
            ImGui.Text($"({this.Settings.CursorRules.Count} rules)");

            // Rules list with always-visible configuration
            for (int i = 0; i < this.Settings.CursorRules.Count; i++)
            {
                var rule = this.Settings.CursorRules[i];
                ImGui.PushID(i);

                ImGui.Separator();

                // Rule header: Enable checkbox + Name input + Delete button
                bool enabled = rule.Enabled;
                if (ImGui.Checkbox("Enable", ref enabled))
                {
                    rule.Enabled = enabled;
                }
                ImGui.SameLine();

                string name = rule.Name;
                ImGui.SetNextItemWidth(150);
                if (ImGui.InputText("Name", ref name, 100))
                {
                    rule.Name = name;
                }
                ImGui.SameLine();

                // Delete button
                if (ImGui.Button("Delete"))
                {
                    this.Settings.CursorRules.RemoveAt(i);
                    ImGui.PopID();
                    break; // Break to avoid iterator issues
                }

                // FIXED: Always show configuration controls (removed if condition)
                ImGui.Indent();

                // Key selection
                VK key = rule.Key;
                if (ImGuiHelper.NonContinuousEnumComboBox("Key to Spam", ref key))
                {
                    rule.Key = key;
                }
                ImGuiHelper.ToolTip("Key that will be pressed when enemies are detected within radius");

                // UPDATED: Spam delay with precise input
                float delay = rule.SpamDelayMs;
                if (ImGui.DragFloat("Spam Delay (ms)", ref delay, 1.0f, 50.0f, 2000.0f, "%.0f"))
                {
                    rule.SpamDelayMs = Math.Max(50.0f, Math.Min(2000.0f, delay));
                }
                ImGuiHelper.ToolTip("Time to wait between key presses (lower = faster spam). Click to type exact value.");

                // UPDATED: Detection radius with precise input
                float radius = rule.DetectionRadiusPixels;
                if (ImGui.DragFloat("Detection Radius (pixels)", ref radius, 2.0f, 50.0f, 800.0f, "%.0f"))
                {
                    rule.DetectionRadiusPixels = Math.Max(50.0f, Math.Min(800.0f, radius));
                }
                ImGuiHelper.ToolTip("Screen distance around cursor to detect enemies. Click to type exact value.");

                // Show visual radius checkbox (unchanged)
                bool showVisual = rule.ShowVisualRadius;
                if (ImGui.Checkbox("Show Visual Radius", ref showVisual))
                {
                    rule.ShowVisualRadius = showVisual;
                }
                ImGuiHelper.ToolTip("Draw colored circle around cursor showing detection area");

                // Rule status indicator
                if (rule.Enabled)
                {
                    ImGui.TextColored(new Vector4(0, 1, 0, 1), "ACTIVE");
                }
                else
                {
                    ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "Inactive");
                }

                ImGui.Unindent();
                ImGui.PopID();
            }
        }

        /// <inheritdoc/>
        public override void DrawUI()
        {
            // Only run if plugin is enabled and game is running
            if (!this.Settings.Enable || !this.IsGameRunning())
            {
                return;
            }

            // Find the nearest enemy (existing functionality)
            this.FindNearestEnemy();

            // Draw circle around nearest enemy if found (existing functionality)
            if (this.nearestEnemy != null)
            {
                this.DrawCircleAroundEnemy();
            }

            // Execute cursor-based rules (NEW)
            this.ExecuteCursorRules();

            // Draw visual radius circles for active rules (NEW)
            this.DrawCursorRuleRadii();

            // Show debug information if enabled (existing functionality)
            if (this.Settings.ShowDebugInfo)
            {
                this.DrawDebugInfo();
            }

            // Handle hotkey toggle for cursor tracking (existing functionality)
            if (Utils.IsKeyPressedAndNotTimeout(this.Settings.ToggleHotKey))
            {
                this.Settings.EnableCursorTracking = !this.Settings.EnableCursorTracking;
            }

            // Perform cursor tracking if enabled and UI not active (existing functionality)
            if (this.Settings.EnableCursorTracking && !this.IsUIActive())
            {
                this.SnapCursorToNearestEnemy();
            }
        }

        /// <inheritdoc/>
        public override void SaveSettings()
        {
            try
            {
                // Ensure config directory exists
                var configDir = Path.GetDirectoryName(this.settingsPath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                // Serialize and save settings
                var json = JsonConvert.SerializeObject(this.Settings, Formatting.Indented);
                File.WriteAllText(this.settingsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads settings from file if it exists.
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(this.settingsPath))
                {
                    var json = File.ReadAllText(this.settingsPath);
                    this.Settings = JsonConvert.DeserializeObject<NearestEnemySettings>(json) ?? new NearestEnemySettings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load settings: {ex.Message}");
                this.Settings = new NearestEnemySettings();
            }
        }

        /// <summary>
        /// Checks if the game is running and player exists.
        /// </summary>
        /// <returns>True if game is running with valid player.</returns>
        private bool IsGameRunning()
        {
            return Core.Process.Pid != 0 &&
                   Core.States.InGameStateObject?.CurrentAreaInstance?.Player != null;
        }

        /// <summary>
        /// Finds the nearest enemy within the search distance.
        /// </summary>
        private void FindNearestEnemy()
        {
            this.nearestEnemy = null;
            this.nearestDistance = float.MaxValue;

            try
            {
                var player = Core.States.InGameStateObject.CurrentAreaInstance.Player;
                if (player == null || !player.IsValid)
                {
                    return;
                }

                // Get all awakened entities
                var entities = Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities;
                if (entities == null)
                {
                    return;
                }

                // Search through all monster entities
                foreach (var entity in entities.Values.Where(e => e != null && e.IsValid))
                {
                    // Only consider monsters
                    if (entity.EntityType != EntityTypes.Monster)
                    {
                        continue;
                    }

                    // BUG FIX: Skip dead or useless entities
                    if (entity.EntityState == EntityStates.Useless)
                    {
                        continue;
                    }

                    // BUG FIX: Verify entity has Life component and is alive
                    if (!entity.TryGetComponent<Life>(out var lifeComp) || !lifeComp.IsAlive)
                    {
                        continue;
                    }

                    // BUG FIX: Skip friendly monsters (non-attackable)
                    if (entity.EntityState == EntityStates.MonsterFriendly)
                    {
                        continue;
                    }

                    // BUG FIX: Skip hidden pinnacle bosses (invincible)
                    if (entity.EntityState == EntityStates.PinnacleBossHidden)
                    {
                        continue;
                    }

                    // Calculate distance from player
                    var distance = player.DistanceFrom(entity);

                    // Skip if too far away
                    if (distance > this.Settings.MaxSearchDistance)
                    {
                        continue;
                    }

                    // Check if this is the nearest enemy found so far
                    if (distance < this.nearestDistance)
                    {
                        this.nearestDistance = distance;
                        this.nearestEnemy = entity;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error finding nearest enemy: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds enemies within the specified radius around the mouse cursor.
        /// </summary>
        /// <param name="radiusPixels">Detection radius in screen pixels.</param>
        /// <returns>List of entities within the cursor radius.</returns>
        private List<Entity> FindEnemiesAroundCursor(float radiusPixels)
        {
            var enemiesInRadius = new List<Entity>();

            try
            {
                // Get current cursor position
                var cursorPos = MouseCompatibilityHelper.GetCursorPosition();
                if (cursorPos.X == 0 && cursorPos.Y == 0)
                {
                    return enemiesInRadius; // Invalid cursor position
                }

                // Get all awakened entities
                var entities = Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities;
                if (entities == null)
                {
                    return enemiesInRadius;
                }

                // Search through all monster entities
                foreach (var entity in entities.Values.Where(e => e != null && e.IsValid))
                {
                    // Only consider monsters (same filtering as existing method)
                    if (entity.EntityType != EntityTypes.Monster)
                    {
                        continue;
                    }

                    // Skip dead or useless entities
                    if (entity.EntityState == EntityStates.Useless)
                    {
                        continue;
                    }

                    // Verify entity has Life component and is alive
                    if (!entity.TryGetComponent<Life>(out var lifeComp) || !lifeComp.IsAlive)
                    {
                        continue;
                    }

                    // Skip friendly monsters (non-attackable)
                    if (entity.EntityState == EntityStates.MonsterFriendly)
                    {
                        continue;
                    }

                    // Skip hidden pinnacle bosses (invincible)
                    if (entity.EntityState == EntityStates.PinnacleBossHidden)
                    {
                        continue;
                    }

                    // Get enemy's screen position
                    if (!entity.TryGetComponent<Render>(out var renderComponent))
                    {
                        continue;
                    }

                    var worldPos = renderComponent.WorldPosition;
                    var screenPos = Core.States.InGameStateObject.CurrentWorldInstance.WorldToScreen(
                        new Vector2(worldPos.X, worldPos.Y), worldPos.Z);

                    // Check if position is valid (on screen)
                    if (screenPos == Vector2.Zero)
                    {
                        continue;
                    }

                    // Calculate screen distance from cursor to enemy
                    float deltaX = screenPos.X - cursorPos.X;
                    float deltaY = screenPos.Y - cursorPos.Y;
                    float distance = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                    // Add to list if within radius
                    if (distance <= radiusPixels)
                    {
                        enemiesInRadius.Add(entity);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error finding enemies around cursor: {ex.Message}");
            }

            return enemiesInRadius;
        }

        /// <summary>
        /// Executes all active cursor rules, spamming keys when enemies are detected.
        /// </summary>
        private void ExecuteCursorRules()
        {
            try
            {
                if (this.Settings.CursorRules == null || this.Settings.CursorRules.Count == 0)
                {
                    return;
                }

                // Process each enabled rule
                foreach (var rule in this.Settings.CursorRules.Where(r => r.Enabled))
                {
                    // Find enemies within this rule's radius
                    var enemiesInRadius = this.FindEnemiesAroundCursor(rule.DetectionRadiusPixels);

                    // If enemies found and enough time has passed, spam the key
                    if (enemiesInRadius.Count > 0 && rule.CanSpamKey)
                    {
                        // Use GameHelper's key spamming with built-in protection
                        if (MiscHelper.KeyUp(rule.Key))
                        {
                            rule.MarkKeyPressed();

                            // Optional: Add debug output
                            if (this.Settings.ShowDebugInfo)
                            {
                                Console.WriteLine($"Rule '{rule.Name}' pressed {rule.Key} - {enemiesInRadius.Count} enemies in radius");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing cursor rules: {ex.Message}");
            }
        }

        /// <summary>
        /// Draws visual radius circles around the cursor for enabled rules.
        /// </summary>
        private void DrawCursorRuleRadii()
        {
            try
            {
                if (this.Settings.CursorRules == null || this.Settings.CursorRules.Count == 0)
                {
                    return;
                }

                // Get cursor position
                var cursorPos = MouseCompatibilityHelper.GetCursorPosition();
                if (cursorPos.X == 0 && cursorPos.Y == 0)
                {
                    return; // Invalid cursor position
                }

                var drawList = ImGui.GetForegroundDrawList();
                var cursorScreenPos = new Vector2(cursorPos.X, cursorPos.Y);

                // Draw radius circle for each enabled rule that has visual radius enabled
                for (int i = 0; i < this.Settings.CursorRules.Count; i++)
                {
                    var rule = this.Settings.CursorRules[i];
                    if (!rule.Enabled || !rule.ShowVisualRadius)
                    {
                        continue;
                    }

                    // Create a unique color for each rule (cycle through colors)
                    uint color;
                    switch (i % 6)
                    {
                        case 0: color = ImGuiHelper.Color(255, 100, 100, 128); break; // Red
                        case 1: color = ImGuiHelper.Color(100, 255, 100, 128); break; // Green
                        case 2: color = ImGuiHelper.Color(100, 100, 255, 128); break; // Blue
                        case 3: color = ImGuiHelper.Color(255, 255, 100, 128); break; // Yellow
                        case 4: color = ImGuiHelper.Color(255, 100, 255, 128); break; // Magenta
                        case 5: color = ImGuiHelper.Color(100, 255, 255, 128); break; // Cyan
                        default: color = ImGuiHelper.Color(200, 200, 200, 128); break;
                    }

                    // Draw the radius circle
                    drawList.AddCircle(
                        cursorScreenPos,
                        rule.DetectionRadiusPixels,
                        color,
                        0, // num_segments (0 = auto)
                        2.0f); // thickness

                    // Optional: Draw rule name near the circle
                    if (rule.DetectionRadiusPixels > 100) // Only for larger circles to avoid clutter
                    {
                        var textPos = new Vector2(
                            cursorScreenPos.X + rule.DetectionRadiusPixels * 0.7f,
                            cursorScreenPos.Y - rule.DetectionRadiusPixels * 0.7f);

                        drawList.AddText(textPos, color, rule.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error drawing cursor rule radii: {ex.Message}");
            }
        }

        /// <summary>
        /// Draws a circle around the nearest enemy on screen.
        /// </summary>
        private void DrawCircleAroundEnemy()
        {
            try
            {
                if (this.nearestEnemy == null || !this.nearestEnemy.IsValid)
                {
                    return;
                }

                // Get the enemy's render component for position
                if (!this.nearestEnemy.TryGetComponent<Render>(out var renderComponent))
                {
                    return;
                }

                // Convert world position to screen coordinates
                var worldPos = renderComponent.WorldPosition;
                var screenPos = Core.States.InGameStateObject.CurrentWorldInstance.WorldToScreen(
                    new Vector2(worldPos.X, worldPos.Y), worldPos.Z);

                // Check if position is valid (on screen)
                if (screenPos == Vector2.Zero)
                {
                    return;
                }

                // Create color from settings
                var color = ImGuiHelper.Color(this.Settings.CircleColorR, this.Settings.CircleColorG,
                                            this.Settings.CircleColorB, this.Settings.CircleColorA);

                // Draw the circle
                ImGui.GetForegroundDrawList().AddCircle(
                    screenPos,
                    this.Settings.CircleRadius,
                    color,
                    0, // num_segments (0 = auto)
                    this.Settings.CircleThickness);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error drawing circle: {ex.Message}");
            }
        }

        /// <summary>
        /// Draws debug information on screen.
        /// </summary>
        private void DrawDebugInfo()
        {
            try
            {
                ImGui.Begin("Nearest Enemy Debug");

                if (this.nearestEnemy != null)
                {
                    ImGui.Text($"Nearest Enemy Found: YES");
                    ImGui.Text($"Distance: {this.nearestDistance:F1}");
                    ImGui.Text($"Entity ID: {this.nearestEnemy.Id}");
                    ImGui.Text($"Entity Path: {this.nearestEnemy.Path}");

                    // Show entity state information
                    ImGui.Separator();
                    ImGui.Text("Entity State Information:");
                    ImGui.Text($"Entity State: {this.nearestEnemy.EntityState}");
                    ImGui.Text($"Entity Type: {this.nearestEnemy.EntityType}");
                    ImGui.Text($"Entity Subtype: {this.nearestEnemy.EntitySubtype}");
                    ImGui.Text($"Is Valid: {this.nearestEnemy.IsValid}");

                    // Show life information
                    if (this.nearestEnemy.TryGetComponent<Life>(out var lifeComp))
                    {
                        ImGui.Text($"Is Alive: {lifeComp.IsAlive}");
                        ImGui.Text($"Health: {lifeComp.Health.Current}/{lifeComp.Health.Total}");
                    }
                    else
                    {
                        ImGui.Text("No Life Component Found!");
                    }

                    // Show positioning information
                    if (this.nearestEnemy.TryGetComponent<Positioned>(out var posComp))
                    {
                        ImGui.Text($"Is Friendly: {posComp.IsFriendly}");
                    }

                    if (this.nearestEnemy.TryGetComponent<Render>(out var render))
                    {
                        ImGui.Separator();
                        ImGui.Text("Position Information:");
                        ImGui.Text($"World Pos: ({render.WorldPosition.X:F1}, {render.WorldPosition.Y:F1}, {render.WorldPosition.Z:F1})");
                        var screenPos = Core.States.InGameStateObject.CurrentWorldInstance.WorldToScreen(
                            new Vector2(render.WorldPosition.X, render.WorldPosition.Y), render.WorldPosition.Z);
                        ImGui.Text($"Screen Pos: ({screenPos.X:F1}, {screenPos.Y:F1})");
                    }
                }
                else
                {
                    ImGui.Text("Nearest Enemy Found: NO");
                    ImGui.Text($"Search Distance: {this.Settings.MaxSearchDistance:F1}");
                }

                // FIXED: Single entityCount declaration with comprehensive entity analysis
                var entityCount = Core.States.InGameStateObject?.CurrentAreaInstance?.AwakeEntities?.Count ?? 0;
                var monsterCount = 0;
                var deadMonsterCount = 0;
                var friendlyMonsterCount = 0;
                var hiddenBossCount = 0;

                // Count different entity types for debugging
                if (Core.States.InGameStateObject?.CurrentAreaInstance?.AwakeEntities != null)
                {
                    foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
                    {
                        if (entity?.EntityType == EntityTypes.Monster)
                        {
                            monsterCount++;
                            if (entity.EntityState == EntityStates.Useless)
                                deadMonsterCount++;
                            else if (entity.EntityState == EntityStates.MonsterFriendly)
                                friendlyMonsterCount++;
                            else if (entity.EntityState == EntityStates.PinnacleBossHidden)
                                hiddenBossCount++;
                        }
                    }
                }

                ImGui.Separator();
                ImGui.Text("Entity Statistics:");
                ImGui.Text($"Total Entities: {entityCount}");
                ImGui.Text($"Total Monsters: {monsterCount}");
                ImGui.Text($"Dead Monsters (Filtered): {deadMonsterCount}");
                ImGui.Text($"Friendly Monsters (Filtered): {friendlyMonsterCount}");
                ImGui.Text($"Hidden Bosses (Filtered): {hiddenBossCount}");
                ImGui.Text($"Targetable Monsters: {monsterCount - deadMonsterCount - friendlyMonsterCount - hiddenBossCount}");

                // Cursor rules debug information
                ImGui.Separator();
                ImGui.Text("Cursor Rules Debug:");
                ImGui.Text($"Total Rules: {this.Settings.CursorRules?.Count ?? 0}");
                ImGui.Text($"Active Rules: {this.Settings.CursorRules?.Count(r => r.Enabled) ?? 0}");

                var cursorPos = MouseCompatibilityHelper.GetCursorPosition();
                ImGui.Text($"Cursor Position: ({cursorPos.X}, {cursorPos.Y})");

                if (this.Settings.CursorRules != null && this.Settings.CursorRules.Count > 0)
                {
                    ImGui.Separator();
                    ImGui.Text("Rule Status:");
                    foreach (var rule in this.Settings.CursorRules)
                    {
                        if (rule.Enabled)
                        {
                            var enemiesInRadius = this.FindEnemiesAroundCursor(rule.DetectionRadiusPixels);
                            ImGui.TextColored(
                                new Vector4(0, 1, 0, 1),
                                $"{rule.Name} ({rule.Key}): {enemiesInRadius.Count} enemies, Ready: {rule.CanSpamKey}");
                        }
                        else
                        {
                            ImGui.TextColored(
                                new Vector4(0.5f, 0.5f, 0.5f, 1),
                                $"{rule.Name} ({rule.Key}): Disabled");
                        }
                    }
                }

                ImGui.End();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error drawing debug info: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if the UI is currently active and cursor tracking should be paused.
        /// </summary>
        /// <returns>True if UI is active and tracking should pause.</returns>
        private bool IsUIActive()
        {
            if (!this.Settings.PauseDuringUI)
            {
                return false;
            }

            try
            {
                // Check if chat is active
                return Core.States.InGameStateObject?.GameUi?.ChatParent?.IsChatActive ?? false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking UI state: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Snaps the cursor to the nearest enemy's screen position.
        /// </summary>
        private void SnapCursorToNearestEnemy()
        {
            try
            {
                if (this.nearestEnemy == null || !this.nearestEnemy.IsValid)
                {
                    return;
                }

                // Get the enemy's render component for position
                if (!this.nearestEnemy.TryGetComponent<Render>(out var renderComponent))
                {
                    return;
                }

                // Convert world position to screen coordinates
                var worldPos = renderComponent.WorldPosition;
                var screenPos = Core.States.InGameStateObject.CurrentWorldInstance.WorldToScreen(
                    new Vector2(worldPos.X, worldPos.Y), worldPos.Z);

                // Check if position is valid (on screen)
                if (screenPos == Vector2.Zero)
                {
                    return;
                }

                // Snap cursor to enemy position using compatibility wrapper
                MouseCompatibilityHelper.SnapCursorTo((int)screenPos.X, (int)screenPos.Y);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error snapping cursor to enemy: {ex.Message}");
            }
        }
    }
}