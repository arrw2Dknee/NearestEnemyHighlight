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

    using System.Threading.Tasks;

    /// <summary>
    /// Nearest Enemy Highlight plugin that draws a circle around the closest enemy.
    /// </summary>
    public sealed class NearestEnemyCore : PCore<NearestEnemySettings>
    {
        private Entity nearestEnemy;
        private float nearestDistance;
        private string settingsPath;

        private int selectedRuleIndex = -1; // For UI rule management
        private CursorRule currentExecutingRule = null;
        private int currentExecutingPriority = 0;

        private LineOfSightDebugInfo lastLOSDebugInfo = null; // Debug info for visualization

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
            ImGui.Text("Line-of-Sight Settings");

            // Line-of-sight enable/disable
            bool enableLOS = this.Settings.EnableLineOfSight;
            if (ImGui.Checkbox("Enable Line-of-Sight Checking", ref enableLOS))
                this.Settings.EnableLineOfSight = enableLOS;
            ImGuiHelper.ToolTip("Only target enemies with clear line of sight (no walls blocking)");

            // Tolerance setting (only show if LOS is enabled)
            if (this.Settings.EnableLineOfSight)
            {
                float tolerance = this.Settings.LineOfSightTolerance;
                if (ImGui.DragFloat("LOS Tolerance", ref tolerance, 0.1f, 0.0f, 5.0f, "%.1f"))
                {
                    this.Settings.LineOfSightTolerance = Math.Max(0.0f, Math.Min(5.0f, tolerance));
                }
                ImGuiHelper.ToolTip("Number of obstacles to tolerate (0 = perfect LOS, higher = more forgiving)");

                // Debug visualization toggle
                bool losDebug = this.Settings.ShowLineOfSightDebug;
                if (ImGui.Checkbox("Show LOS Debug Lines", ref losDebug))
                    this.Settings.ShowLineOfSightDebug = losDebug;
                ImGuiHelper.ToolTip("Draw debug lines showing line-of-sight paths");
            }

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

                // Priority input
                int priority = rule.Priority;
                ImGui.SetNextItemWidth(80);
                if (ImGui.DragInt("Priority", ref priority, 1.0f, 1, 999))
                {
                    rule.Priority = Math.Max(1, Math.Min(999, priority));
                }
                ImGuiHelper.ToolTip("Rule priority (1-999). Higher number = higher priority. Higher priority rules interrupt lower priority ones.");
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

                // Key mode selection
                bool useKeySequence = rule.UseKeySequence;
                if (ImGui.Checkbox("Use Key Sequence (Combo)", ref useKeySequence))
                {
                    rule.UseKeySequence = useKeySequence;
                }
                ImGuiHelper.ToolTip("Enable this to use a sequence of keys with delays instead of a single key");

                if (rule.UseKeySequence)
                {
                    // Key sequence configuration
                    ImGui.Text("Key Sequence:");
                    ImGui.Indent();

                    // Show current sequence
                    if (rule.KeySequence.HasActions)
                    {
                        for (int j = 0; j < rule.KeySequence.Actions.Count; j++)
                        {
                            var action = rule.KeySequence.Actions[j];
                            ImGui.PushID($"action_{i}_{j}");

                            // Action display and controls
                            ImGui.Text($"{j + 1}.");
                            ImGui.SameLine();

                            // Use keybind capture for setting key
                            if (KeybindCapture.DrawKeybindCapture(i * 1000 + j, action, action.GetDisplayString()))
                            {
                                // Key was updated by capture
                            }

                            ImGui.SameLine();

                            // Delay input
                            int actionDelay = action.DelayMs;
                            ImGui.SetNextItemWidth(80);
                            if (ImGui.DragInt("ms", ref actionDelay, 1.0f, 0, 5000))
                            {
                                action.DelayMs = Math.Max(0, Math.Min(5000, actionDelay));
                            }

                            ImGui.SameLine();

                            // Remove button
                            if (ImGui.Button($"Remove##action_{j}"))
                            {
                                rule.KeySequence.RemoveAction(j);
                                ImGui.PopID();
                                break;
                            }

                            ImGui.PopID();
                        }
                    }
                    else
                    {
                        ImGui.Text("No actions in sequence");
                    }

                    // Sequence cooldown setting
                    ImGui.Separator();
                    float sequenceCooldown = rule.SequenceCooldownMs;
                    if (ImGui.DragFloat("Sequence Cooldown (ms)", ref sequenceCooldown, 10.0f, 0.0f, 25000.0f, "%.0f"))
                    {
                        rule.SequenceCooldownMs = Math.Max(0.0f, Math.Min(25000.0f, sequenceCooldown));
                    }
                    ImGuiHelper.ToolTip("Time to wait after sequence completes before it can trigger again. 1000ms = 1 second. Click to type exact value.");

                    // Add new action button
                    if (ImGui.Button($"Add Action##rule_{i}"))
                    {
                        rule.KeySequence.AddAction(new KeyAction(VK.KEY_Q, 100));
                    }
                }
                else
                {
                    // Single key configuration (existing behavior)
                    VK key = rule.Key;
                    if (ImGuiHelper.NonContinuousEnumComboBox("Key to Spam", ref key))
                    {
                        rule.Key = key;
                    }
                    ImGuiHelper.ToolTip("Key that will be pressed when enemies are detected within radius");
                }

                // Show spam delay only for single key mode
                if (!rule.UseKeySequence)
                {
                    // UPDATED: Spam delay with precise input
                    float delay = rule.SpamDelayMs;
                    if (ImGui.DragFloat("Spam Delay (ms)", ref delay, 1.0f, 50.0f, 2000.0f, "%.0f"))
                    {
                        rule.SpamDelayMs = Math.Max(50.0f, Math.Min(2000.0f, delay));
                    }
                    ImGuiHelper.ToolTip("Time to wait between key presses (lower = faster spam). Click to type exact value.");
                }

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

            // Draw line-of-sight debug visualization if enabled
            if (this.Settings.ShowLineOfSightDebug && this.Settings.EnableLineOfSight)
            {
                this.DrawLineOfSightDebug();
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
            this.lastLOSDebugInfo = null; // Clear debug info when no enemy found

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
                        // Check line of sight if enabled
                        if (this.Settings.EnableLineOfSight)
                        {
                            LineOfSightDebugInfo debugInfo;
                            if (LineOfSightChecker.HasLineOfSight(player, entity, this.Settings.LineOfSightTolerance, out debugInfo))
                            {
                                this.nearestDistance = distance;
                                this.nearestEnemy = entity;
                                // Store debug info for the nearest enemy (for visualization)
                                if (this.Settings.ShowLineOfSightDebug)
                                {
                                    this.lastLOSDebugInfo = debugInfo;
                                }
                            }
                            // If no line of sight, skip this enemy even if it's closer
                        }
                        else
                        {
                            // Line of sight disabled, use original behavior
                            this.nearestDistance = distance;
                            this.nearestEnemy = entity;
                        }
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

                // Process each enabled rule, sorted by priority (highest first)
                foreach (var rule in this.Settings.CursorRules.Where(r => r.Enabled).OrderByDescending(r => r.Priority))
                {
                    // Find enemies within this rule's radius
                    var enemiesInRadius = this.FindEnemiesAroundCursor(rule.DetectionRadiusPixels);

                    // If enemies found, check if rule should execute based on priority
                    if (enemiesInRadius.Count > 0)
                    {
                        if (rule.UseKeySequence && rule.KeySequence.HasActions)
                        {
                            // Execute key sequence if not executing, cooldown has elapsed, and priority allows
                            if (!rule.KeySequence.IsExecuting && rule.CanExecuteSequence && this.ShouldExecuteRule(rule))
                            {
                                // Set this rule as executing
                                this.SetExecutingRule(rule);

                                if (this.Settings.ShowDebugInfo)
                                {
                                    Console.WriteLine($"Starting sequence for rule '{rule.Name}' (priority {rule.Priority})");
                                }

                                // Execute in a separate task to not block the main thread
                                Task.Run(() => rule.KeySequence.Execute(
                                    msg =>
                                    {
                                        if (this.Settings.ShowDebugInfo)
                                        {
                                            Console.WriteLine($"Rule '{rule.Name}' sequence: {msg}");
                                        }
                                    },
                                    () =>
                                    {
                                        // Called when sequence completes
                                        rule.MarkSequenceCompleted();
                                        this.ClearExecutingRule();
                                        if (this.Settings.ShowDebugInfo)
                                        {
                                            Console.WriteLine($"Rule '{rule.Name}' sequence completed, cooldown started");
                                        }
                                    }));

                                // Only execute one rule per frame
                                break;
                            }
                        }
                        else
                        {
                            // Execute single key with spam delay and priority check
                            if (rule.CanSpamKey && this.ShouldExecuteRule(rule))
                            {
                                if (MiscHelper.KeyUp(rule.Key))
                                {
                                    // Set this rule as executing (for tracking, single keys complete immediately)
                                    this.SetExecutingRule(rule);
                                    rule.MarkKeyPressed();

                                    // Clear execution tracking immediately for single keys
                                    this.ClearExecutingRule();

                                    // Optional: Add debug output
                                    if (this.Settings.ShowDebugInfo)
                                    {
                                        Console.WriteLine($"Rule '{rule.Name}' pressed {rule.Key} (priority {rule.Priority}) - {enemiesInRadius.Count} enemies in radius");
                                    }

                                    // Only execute one rule per frame
                                    break;
                                }
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
        /// Draws line-of-sight debug visualization.
        /// </summary>
        private void DrawLineOfSightDebug()
        {
            try
            {
                if (this.lastLOSDebugInfo == null || this.nearestEnemy == null || !this.nearestEnemy.IsValid)
                {
                    return;
                }

                var areaInstance = Core.States.InGameStateObject?.CurrentAreaInstance;
                if (areaInstance == null)
                {
                    return;
                }

                // Get player render component for position
                var player = areaInstance.Player;
                if (!player.TryGetComponent<Render>(out var playerRender))
                {
                    return;
                }

                // Get target render component for position
                if (!this.nearestEnemy.TryGetComponent<Render>(out var targetRender))
                {
                    return;
                }

                // Convert world positions to screen coordinates
                var playerWorldPos = playerRender.WorldPosition;
                var targetWorldPos = targetRender.WorldPosition;

                var playerScreenPos = Core.States.InGameStateObject.CurrentWorldInstance.WorldToScreen(
                    new Vector2(playerWorldPos.X, playerWorldPos.Y), playerWorldPos.Z);
                var targetScreenPos = Core.States.InGameStateObject.CurrentWorldInstance.WorldToScreen(
                    new Vector2(targetWorldPos.X, targetWorldPos.Y), targetWorldPos.Z);

                // Check if positions are valid (on screen)
                if (playerScreenPos == Vector2.Zero || targetScreenPos == Vector2.Zero)
                {
                    return;
                }

                var drawList = ImGui.GetBackgroundDrawList();

                // Determine line color based on LOS status
                uint lineColor;
                if (this.lastLOSDebugInfo.HasLineOfSight)
                {
                    if (this.lastLOSDebugInfo.BlockedTileCount > 0)
                    {
                        // Yellow for tolerance-passed (some obstacles but within tolerance)
                        lineColor = ImGuiHelper.Color(255, 255, 0, 200);
                    }
                    else
                    {
                        // Green for clear line of sight
                        lineColor = ImGuiHelper.Color(0, 255, 0, 200);
                    }
                }
                else
                {
                    // Red for blocked line of sight
                    lineColor = ImGuiHelper.Color(255, 0, 0, 200);
                }

                // Draw main line from player to target
                drawList.AddLine(playerScreenPos, targetScreenPos, lineColor, 2.0f);

                // Draw blocked positions as small red dots
                if (this.lastLOSDebugInfo.BlockedPositions.Count > 0)
                {
                    var gridConverter = areaInstance.WorldToGridConvertor;
                    var blockedColor = ImGuiHelper.Color(255, 0, 0, 255);

                    foreach (var blockedGridPos in this.lastLOSDebugInfo.BlockedPositions)
                    {
                        // Convert grid position back to world position
                        var blockedWorldPos = new Vector2(
                            blockedGridPos.X * gridConverter,
                            blockedGridPos.Y * gridConverter);

                        // Convert world position to screen position
                        var blockedScreenPos = Core.States.InGameStateObject.CurrentWorldInstance.WorldToScreen(
                            blockedWorldPos, playerWorldPos.Z); // Use player height as reference

                        if (blockedScreenPos != Vector2.Zero)
                        {
                            // Draw small filled circle at blocked position
                            drawList.AddCircleFilled(blockedScreenPos, 3.0f, blockedColor);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error drawing LOS debug: {ex.Message}");
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
                    // Show current execution info
                    if (this.currentExecutingRule != null)
                    {
                        ImGui.TextColored(new Vector4(1, 1, 0, 1), $"Currently Executing: {this.currentExecutingRule.Name} (Priority {this.currentExecutingPriority})");
                    }
                    else
                    {
                        ImGui.Text("Currently Executing: None");
                    }
                    ImGui.Text("Rule Status:");
                    foreach (var rule in this.Settings.CursorRules)
                    {
                        if (rule.Enabled)
                        {
                            var enemiesInRadius = this.FindEnemiesAroundCursor(rule.DetectionRadiusPixels);

                            // Determine if this rule is currently executing
                            bool isCurrentlyExecuting = this.currentExecutingRule == rule;
                            var textColor = isCurrentlyExecuting ? new Vector4(1, 1, 0, 1) : new Vector4(0, 1, 0, 1); // Yellow if executing, green if not

                            if (rule.UseKeySequence)
                            {
                                string sequenceStatus = rule.CanExecuteSequence ? "Ready" :
                                    $"Cooldown: {rule.SequenceCooldownRemainingMs:F0}ms";

                                string executingStatus = isCurrentlyExecuting ? " [EXECUTING]" : "";

                                ImGui.TextColored(
                                    textColor,
                                    $"{rule.Name} (Sequence, P{rule.Priority}): {enemiesInRadius.Count} enemies, {sequenceStatus}{executingStatus}");
                            }
                            else
                            {
                                string readyStatus = rule.CanSpamKey ? "Ready" : "Spam Cooldown";
                                string executingStatus = isCurrentlyExecuting ? " [EXECUTING]" : "";

                                ImGui.TextColored(
                                    textColor,
                                    $"{rule.Name} ({rule.Key}, P{rule.Priority}): {enemiesInRadius.Count} enemies, {readyStatus}{executingStatus}");
                            }
                        }
                        else
                        {
                            ImGui.TextColored(
                                new Vector4(0.5f, 0.5f, 0.5f, 1),
                                $"{rule.Name} ({rule.Key}): Disabled");
                        }
                    }
                }

                // Line-of-sight debug information
                if (this.Settings.EnableLineOfSight)
                {
                    ImGui.Separator();
                    ImGui.Text("Line-of-Sight Debug:");

                    if (this.lastLOSDebugInfo != null)
                    {
                        ImGui.Text($"LOS Status: {(this.lastLOSDebugInfo.HasLineOfSight ? "CLEAR" : "BLOCKED")}");
                        ImGui.Text($"Blocked Tiles: {this.lastLOSDebugInfo.BlockedTileCount}");
                        ImGui.Text($"Tolerance: {this.lastLOSDebugInfo.ToleranceUsed:F1}");
                        ImGui.Text($"Path Length: {this.lastLOSDebugInfo.PathPositions.Count} tiles");

                        // Color-coded status
                        if (this.lastLOSDebugInfo.HasLineOfSight)
                        {
                            if (this.lastLOSDebugInfo.BlockedTileCount > 0)
                            {
                                ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), "Status: TOLERANCE PASS");
                            }
                            else
                            {
                                ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), "Status: CLEAR PATH");
                            }
                        }
                        else
                        {
                            ImGui.TextColored(new Vector4(1.0f, 0.0f, 0.0f, 1.0f), "Status: PATH BLOCKED");
                        }
                    }
                    else
                    {
                        ImGui.Text("No LOS data available");
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

        /// <summary>
        /// Clears the currently executing rule tracking.
        /// </summary>
        private void ClearExecutingRule()
        {
            this.currentExecutingRule = null;
            this.currentExecutingPriority = 0;
        }

        /// <summary>
        /// Determines if a rule should execute based on priority conflicts.
        /// </summary>
        /// <param name="rule">The rule that wants to execute</param>
        /// <returns>True if the rule should execute, false if it should be skipped</returns>
        private bool ShouldExecuteRule(CursorRule rule)
        {
            // If no rule is currently executing, new rule can execute
            if (this.currentExecutingRule == null)
            {
                return true;
            }

            // If new rule has higher priority OR same priority, it should interrupt current rule
            if (rule.Priority >= this.currentExecutingPriority)
            {
                // Interrupt current rule
                this.InterruptCurrentRule();
                return true;
            }

            // New rule has lower priority, skip it
            return false;
        }

        /// <summary>
        /// Interrupts the currently executing rule.
        /// </summary>
        private void InterruptCurrentRule()
        {
            if (this.currentExecutingRule == null)
                return;

            if (this.Settings.ShowDebugInfo)
            {
                Console.WriteLine($"Interrupting rule '{this.currentExecutingRule.Name}' (priority {this.currentExecutingPriority})");
            }

            // Cancel sequence if it's running
            if (this.currentExecutingRule.UseKeySequence && this.currentExecutingRule.KeySequence.IsExecuting)
            {
                this.currentExecutingRule.KeySequence.Cancel();
            }

            // Clear tracking immediately
            this.ClearExecutingRule();
        }

        /// <summary>
        /// Sets the currently executing rule for tracking.
        /// </summary>
        /// <param name="rule">The rule that is starting execution</param>
        private void SetExecutingRule(CursorRule rule)
        {
            this.currentExecutingRule = rule;
            this.currentExecutingPriority = rule.Priority;
        }
    }
}