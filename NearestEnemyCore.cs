// <copyright file="NearestEnemyCore.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace NearestEnemyHighlight
{
    using ClickableTransparentOverlay.Win32;
    using GameHelper;
    using GameHelper.Plugin;
    using GameHelper.RemoteEnums;
    using GameHelper.RemoteEnums.Entity;
    using GameHelper.RemoteObjects.Components;
    using GameHelper.RemoteObjects.States.InGameStateObjects;
    using GameHelper.Utils;
    using ImGuiNET;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;

    using GameHelper.RemoteObjects.Components;

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

        private string newProfileName = string.Empty;

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

            // Ensure default profile exists
            if (this.Settings.Profiles == null || this.Settings.Profiles.Count == 0)
            {
                this.Settings.Profiles = new Dictionary<string, Profile>();
                this.CreateDefaultProfile();
            }

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

            // Main enable checkbox
            bool enable = this.Settings.Enable;
            if (ImGui.Checkbox("Enable Plugin", ref enable))
                this.Settings.Enable = enable;

            ImGui.Separator();

            // Tabbed interface
            if (ImGui.BeginTabBar("MainSettingsTabs"))
            {
                // Visual Settings Tab
                if (ImGui.BeginTabItem("Targeting"))
                {
                    this.DrawVisualSettings();
                    ImGui.EndTabItem();
                }

                // Automation & Profiles Tab
                if (ImGui.BeginTabItem("Automation"))
                {
                    this.DrawAutomationSettings();
                    ImGui.EndTabItem();
                }

                // Debug Tab
                if (ImGui.BeginTabItem("Debug"))
                {
                    this.DrawDebugSettings();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }

        /// <summary>
        /// Draws the visual settings tab content.
        /// </summary>
        private void DrawVisualSettings()
        {
            if (ImGui.CollapsingHeader("Enemy Highlighting", ImGuiTreeNodeFlags.DefaultOpen))
            {
                float maxDistance = this.Settings.MaxSearchDistance;
                if (ImGui.DragFloat("Max Search Distance", ref maxDistance, 1.0f, 10.0f, 500.0f, "%.1f"))
                {
                    this.Settings.MaxSearchDistance = Math.Max(10.0f, Math.Min(500.0f, maxDistance));
                }
                ImGuiHelper.ToolTip("Maximum distance to search for enemies. Click to type exact value.");

                float circleRadius = this.Settings.CircleRadius;
                if (ImGui.DragFloat("Circle Radius", ref circleRadius, 0.5f, 5.0f, 200.0f, "%.1f"))
                {
                    this.Settings.CircleRadius = Math.Max(5.0f, Math.Min(200.0f, circleRadius));
                }
                ImGuiHelper.ToolTip("Size of circle drawn around nearest enemy. Click to type exact value.");

                float circleThickness = this.Settings.CircleThickness;
                if (ImGui.DragFloat("Circle Thickness", ref circleThickness, 0.1f, 1.0f, 20.0f, "%.1f"))
                {
                    this.Settings.CircleThickness = Math.Max(1.0f, Math.Min(20.0f, circleThickness));
                }
                ImGuiHelper.ToolTip("Thickness of the circle line. Click to type exact value.");

                ImGui.Text("Circle Color:");
                // Convert byte values (0-255) to normalized float values (0-1) for ImGui
                Vector4 colorVec = new Vector4(
                    this.Settings.CircleColorR / 255f,
                    this.Settings.CircleColorG / 255f,
                    this.Settings.CircleColorB / 255f,
                    this.Settings.CircleColorA / 255f);

                // Display color picker with alpha channel
                if (ImGui.ColorEdit4("##CircleColor", ref colorVec, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreviewHalf))
                {
                    // Convert back from normalized float (0-1) to byte values (0-255)
                    this.Settings.CircleColorR = (byte)(colorVec.X * 255f);
                    this.Settings.CircleColorG = (byte)(colorVec.Y * 255f);
                    this.Settings.CircleColorB = (byte)(colorVec.Z * 255f);
                    this.Settings.CircleColorA = (byte)(colorVec.W * 255f);
                }
                ImGuiHelper.ToolTip("Click to open color picker. Right-click for more options.");
            }

            // NEW SECTION: Rarity Priority Weights
            if (ImGui.CollapsingHeader("Rarity Priority Weights", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), "Configure targeting priority by monster rarity:");
                ImGui.Text("Weight 0 = Ignore rarity | Higher weight = Higher priority");
                ImGui.Text("Score = Weight / Distance (higher score wins)");
                ImGui.Spacing();

                float normalWeight = this.Settings.NormalMonsterWeight;
                if (ImGui.DragFloat("Normal Monster Weight", ref normalWeight, 1.0f, 0.0f, 100.0f, "%.0f"))
                {
                    this.Settings.NormalMonsterWeight = Math.Max(0.0f, Math.Min(100.0f, normalWeight));
                }
                ImGuiHelper.ToolTip("Priority weight for Normal (white) monsters. Set to 0 to ignore. Click to type exact value.");

                float magicWeight = this.Settings.MagicMonsterWeight;
                if (ImGui.DragFloat("Magic Monster Weight", ref magicWeight, 1.0f, 0.0f, 100.0f, "%.0f"))
                {
                    this.Settings.MagicMonsterWeight = Math.Max(0.0f, Math.Min(100.0f, magicWeight));
                }
                ImGuiHelper.ToolTip("Priority weight for Magic (blue) monsters. Set to 0 to ignore. Click to type exact value.");

                float rareWeight = this.Settings.RareMonsterWeight;
                if (ImGui.DragFloat("Rare Monster Weight", ref rareWeight, 1.0f, 0.0f, 100.0f, "%.0f"))
                {
                    this.Settings.RareMonsterWeight = Math.Max(0.0f, Math.Min(100.0f, rareWeight));
                }
                ImGuiHelper.ToolTip("Priority weight for Rare (yellow) monsters. Set to 0 to ignore. Click to type exact value.");

                float uniqueWeight = this.Settings.UniqueMonsterWeight;
                if (ImGui.DragFloat("Unique Monster Weight", ref uniqueWeight, 1.0f, 0.0f, 100.0f, "%.0f"))
                {
                    this.Settings.UniqueMonsterWeight = Math.Max(0.0f, Math.Min(100.0f, uniqueWeight));
                }
                ImGuiHelper.ToolTip("Priority weight for Unique (golden) monsters. Set to 0 to ignore. Click to type exact value.");

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Examples:");
                ImGui.BulletText("All weights = 50: Targets closest monster (distance only)");
                ImGui.BulletText("Unique = 100, others = 10: Strongly prefer uniques");
                ImGui.BulletText("Rare = 0: Completely ignore rare monsters");
            }

            if (ImGui.CollapsingHeader("Line of Sight"))
            {
                bool enableLOS = this.Settings.EnableLineOfSight;
                if (ImGui.Checkbox("Enable Line of Sight Check", ref enableLOS))
                    this.Settings.EnableLineOfSight = enableLOS;
                ImGuiHelper.ToolTip("Only target enemies with clear line of sight");

                float losTolerance = this.Settings.LineOfSightTolerance;
                if (ImGui.DragFloat("LOS Tolerance", ref losTolerance, 0.1f, 0.0f, 10.0f, "%.1f"))
                {
                    this.Settings.LineOfSightTolerance = Math.Max(0.0f, Math.Min(10.0f, losTolerance));
                }
                ImGuiHelper.ToolTip("Number of obstacles to tolerate (0 = perfect LOS, higher = more forgiving)");

                bool losDebug = this.Settings.ShowLineOfSightDebug;
                if (ImGui.Checkbox("Show LOS Debug Lines", ref losDebug))
                    this.Settings.ShowLineOfSightDebug = losDebug;
                ImGuiHelper.ToolTip("Draw debug lines showing line-of-sight paths");
            }
        }

        /// <summary>
        /// Draws the automation settings tab content including profile management.
        /// </summary>
        private void DrawAutomationSettings()
        {
            var currentProfile = this.GetCurrentProfile();

            // Profile Management Section
            if (ImGui.CollapsingHeader("Profile Management", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Text("Active Profile:");
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0, 1, 0, 1), currentProfile.Name);

                ImGui.Spacing();

                // Profile selector dropdown
                if (ImGui.BeginCombo("Select Profile", this.Settings.CurrentProfile))
                {
                    foreach (var profileName in this.Settings.Profiles.Keys)
                    {
                        bool isSelected = (this.Settings.CurrentProfile == profileName);
                        if (ImGui.Selectable(profileName, isSelected))
                        {
                            this.SetCurrentProfile(profileName);
                        }

                        if (isSelected)
                        {
                            ImGui.SetItemDefaultFocus();
                        }
                    }
                    ImGui.EndCombo();
                }

                ImGui.Spacing();

                // Profile management buttons
                if (ImGui.Button("Create New Profile"))
                {
                    ImGui.OpenPopup("CreateProfilePopup");
                }
                ImGui.SameLine();

                if (ImGui.Button("Clone Current"))
                {
                    string newName = $"{currentProfile.Name} Copy";
                    int counter = 1;
                    while (this.Settings.Profiles.ContainsKey(newName))
                    {
                        newName = $"{currentProfile.Name} Copy {counter}";
                        counter++;
                    }
                    this.CloneProfile(this.Settings.CurrentProfile, newName);
                }
                ImGui.SameLine();

                if (ImGui.Button("Delete Current"))
                {
                    if (this.Settings.Profiles.Count > 1)
                    {
                        ImGui.OpenPopup("DeleteConfirmPopup");
                    }
                }

                // Create profile popup
                if (ImGui.BeginPopup("CreateProfilePopup"))
                {
                    ImGui.Text("Create New Profile");
                    ImGui.Separator();

                    ImGui.InputText("Profile Name", ref this.newProfileName, 100);

                    ImGui.Spacing();

                    if (ImGui.Button("Create"))
                    {
                        if (!string.IsNullOrWhiteSpace(this.newProfileName) &&
                            !this.Settings.Profiles.ContainsKey(this.newProfileName))
                        {
                            this.CreateProfile(this.newProfileName);
                            this.SetCurrentProfile(this.newProfileName);
                            this.newProfileName = string.Empty; // Clear for next use
                            ImGui.CloseCurrentPopup();
                        }
                    }

                    ImGui.SameLine();

                    if (ImGui.Button("Cancel"))
                    {
                        this.newProfileName = string.Empty; // Clear on cancel
                        ImGui.CloseCurrentPopup();
                    }

                    // Show validation message
                    if (!string.IsNullOrWhiteSpace(this.newProfileName) &&
                        this.Settings.Profiles.ContainsKey(this.newProfileName))
                    {
                        ImGui.TextColored(new Vector4(1, 0, 0, 1), "Profile name already exists!");
                    }

                    ImGui.EndPopup();
                }

                // Delete confirmation popup
                if (ImGui.BeginPopup("DeleteConfirmPopup"))
                {
                    ImGui.Text($"Delete profile '{this.Settings.CurrentProfile}'?");
                    ImGui.Text("This action cannot be undone!");
                    ImGui.Separator();

                    if (ImGui.Button("Yes, Delete"))
                    {
                        string toDelete = this.Settings.CurrentProfile;
                        this.DeleteProfile(toDelete);
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel"))
                    {
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndPopup();
                }
            }

            ImGui.Separator();

            // Cursor Tracking Settings
            if (ImGui.CollapsingHeader("Cursor Tracking", ImGuiTreeNodeFlags.DefaultOpen))
            {
                bool enableTracking = currentProfile.EnableCursorTracking;
                if (ImGui.Checkbox("Enable Cursor Tracking", ref enableTracking))
                    currentProfile.EnableCursorTracking = enableTracking;
                ImGuiHelper.ToolTip("Automatically move cursor to nearest enemy");

                bool pauseUI = this.Settings.PauseDuringUI;
                if (ImGui.Checkbox("Pause During UI", ref pauseUI))
                    this.Settings.PauseDuringUI = pauseUI;

                VK toggleKey = currentProfile.ToggleHotKey;
                ImGui.Text("Toggle Hotkey: ");
                ImGui.SameLine();
                if (ImGuiHelper.NonContinuousEnumComboBox("##ToggleHotKey", ref toggleKey))
                {
                    currentProfile.ToggleHotKey = toggleKey;
                }
                ImGuiHelper.ToolTip("Press this key in-game to toggle cursor tracking on/off");
            }

            ImGui.Separator();

            // Cursor Rules Section
            if (ImGui.CollapsingHeader("Cursor Detection Rules", ImGuiTreeNodeFlags.DefaultOpen))
            {
                // Add new rule button
                if (ImGui.Button("Add New Rule"))
                {
                    currentProfile.CursorRules.Add(CursorRule.CreateDefault());
                }
                ImGui.SameLine();

                // Show rule count
                ImGui.Text($"({currentProfile.CursorRules.Count} rules in this profile)");

                ImGui.Separator();

                // Rules list
                for (int i = 0; i < currentProfile.CursorRules.Count; i++)
                {
                    var rule = currentProfile.CursorRules[i];
                    ImGui.PushID(i);

                    // Rule header
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

                    int priority = rule.Priority;
                    ImGui.SetNextItemWidth(80);
                    if (ImGui.DragInt("Priority", ref priority, 1.0f, 1, 999))
                    {
                        rule.Priority = Math.Max(1, Math.Min(999, priority));
                    }
                    ImGuiHelper.ToolTip("Higher number = higher priority");
                    ImGui.SameLine();

                    if (ImGui.Button("Delete"))
                    {
                        currentProfile.CursorRules.RemoveAt(i);
                        ImGui.PopID();
                        break;
                    }

                    ImGui.Indent();

                    // Show configuration based on mode
                    bool useKeySequence = rule.UseKeySequence;
                    if (ImGui.Checkbox("Use Key Sequence (Combo)", ref useKeySequence))
                    {
                        rule.UseKeySequence = useKeySequence;
                    }
                    ImGuiHelper.ToolTip("Enable for multi-key combinations instead of single key");

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

                        // Add new action button
                        if (ImGui.Button($"Add Action##rule_{i}"))
                        {
                            rule.KeySequence.AddAction(new KeyAction(VK.KEY_Q, 100));
                        }

                        ImGui.Unindent();
                        ImGui.Separator();

                        // Sequence cooldown setting
                        float seqCooldown = rule.SequenceCooldownMs;
                        if (ImGui.DragFloat("Sequence Cooldown (ms)", ref seqCooldown, 10.0f, 0.0f, 10000.0f, "%.0f"))
                        {
                            rule.SequenceCooldownMs = Math.Max(0.0f, Math.Min(10000.0f, seqCooldown));
                        }
                        ImGuiHelper.ToolTip("Time to wait after sequence completes before it can trigger again. Click to type exact value.");
                    }
                    else
                    {
                        // Single key configuration
                        VK key = rule.Key;
                        ImGui.Text("Key: ");
                        ImGui.SameLine();
                        if (ImGuiHelper.NonContinuousEnumComboBox("##Key", ref key))
                        {
                            rule.Key = key;
                        }

                        float spamDelay = rule.SpamDelayMs;
                        if (ImGui.DragFloat("Spam Delay (ms)", ref spamDelay, 5.0f, 50.0f, 2000.0f, "%.0f"))
                        {
                            rule.SpamDelayMs = Math.Max(50.0f, Math.Min(2000.0f, spamDelay));
                        }
                    }

                    // Common settings
                    float radius = rule.DetectionRadiusPixels;
                    if (ImGui.DragFloat("Detection Radius", ref radius, 5.0f, 50.0f, 800.0f, "%.0f"))
                    {
                        rule.DetectionRadiusPixels = Math.Max(50.0f, Math.Min(800.0f, radius));
                    }

                    bool showRadius = rule.ShowVisualRadius;
                    if (ImGui.Checkbox("Show Visual Radius", ref showRadius))
                    {
                        rule.ShowVisualRadius = showRadius;
                    }

                    ImGui.Unindent();
                    ImGui.Separator();
                    ImGui.PopID();
                }
            }

            if (ImGui.Button("Save Settings"))
            {
                this.SaveSettings();
            }
        }

        /// <summary>
        /// Draws the debug settings tab content.
        /// </summary>
        private void DrawDebugSettings()
        {
            bool showDebug = this.Settings.ShowDebugInfo;
            if (ImGui.Checkbox("Show Debug Information", ref showDebug))
                this.Settings.ShowDebugInfo = showDebug;
            ImGuiHelper.ToolTip("Display real-time debug information overlay");

            ImGui.Spacing();
            ImGui.TextColored(new Vector4(1, 1, 0, 1), "Debug Info Features:");
            ImGui.BulletText("Nearest enemy distance and position");
            ImGui.BulletText("Active rule execution status");
            ImGui.BulletText("Cursor position tracking");
            ImGui.BulletText("Rule detection counts");
            ImGui.BulletText("Priority and timing information");
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
            var currentProfile = this.GetCurrentProfile();
            if (Utils.IsKeyPressedAndNotTimeout(currentProfile.ToggleHotKey))
            {
                currentProfile.EnableCursorTracking = !currentProfile.EnableCursorTracking;
            }

            // Perform cursor tracking if enabled and UI not active (existing functionality)
            if (currentProfile.EnableCursorTracking && !this.IsUIActive())
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
        /// Gets the priority weight for a monster based on its rarity.
        /// </summary>
        /// <param name="entity">The entity to get weight for.</param>
        /// <returns>Weight value (0 = ignore, higher = more priority), or 50 if rarity cannot be determined.</returns>
        private float GetMonsterWeight(Entity entity)
        {
            // Try to get ObjectMagicProperties component for rarity
            if (!entity.TryGetComponent<ObjectMagicProperties>(out var magicProps))
            {
                // If no magic properties, assume Normal rarity
                return this.Settings.NormalMonsterWeight;
            }

            // Return weight based on rarity
            return magicProps.Rarity switch
            {
                Rarity.Normal => this.Settings.NormalMonsterWeight,
                Rarity.Magic => this.Settings.MagicMonsterWeight,
                Rarity.Rare => this.Settings.RareMonsterWeight,
                Rarity.Unique => this.Settings.UniqueMonsterWeight,
                _ => 50f // Default fallback
            };
        }

        /// <summary>
        /// Finds the nearest enemy within the search distance using weighted priority scoring.
        /// </summary>
        private void FindNearestEnemy()
        {
            this.nearestEnemy = null;
            this.nearestDistance = float.MaxValue;
            this.lastLOSDebugInfo = null; // Clear debug info when no enemy found

            // Variables for weighted scoring
            float bestScore = 0f;
            Entity bestEnemy = null;
            float bestDistance = float.MaxValue;

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

                    // Get weight for this monster's rarity
                    var weight = this.GetMonsterWeight(entity);

                    // Skip monsters with weight of 0 (user wants to ignore this rarity)
                    if (weight <= 0f)
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

                    // Calculate weighted score: higher weight and closer distance = higher score
                    // Use max(distance, 0.1f) to prevent division by zero
                    var score = weight / Math.Max(distance, 0.1f);

                    // Check if this has a better score than current best
                    if (score > bestScore)
                    {
                        // Check line of sight if enabled
                        if (this.Settings.EnableLineOfSight)
                        {
                            LineOfSightDebugInfo debugInfo;
                            if (LineOfSightChecker.HasLineOfSight(player, entity, this.Settings.LineOfSightTolerance, out debugInfo))
                            {
                                bestScore = score;
                                bestEnemy = entity;
                                bestDistance = distance;

                                // Store debug info for the best enemy (for visualization)
                                if (this.Settings.ShowLineOfSightDebug)
                                {
                                    this.lastLOSDebugInfo = debugInfo;
                                }
                            }
                            // If no line of sight, skip this enemy even if it has better score
                        }
                        else
                        {
                            // Line of sight disabled, use score-based selection
                            bestScore = score;
                            bestEnemy = entity;
                            bestDistance = distance;
                        }
                    }
                }

                // Set the results
                this.nearestEnemy = bestEnemy;
                this.nearestDistance = bestDistance;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error finding nearest enemy: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds enemies within the specified radius around the mouse cursor using weighted priority scoring.
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

                // Temporary list to store candidates with their scores
                var candidates = new List<(Entity entity, float score, float distance)>();

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

                    // Get weight for this monster's rarity
                    var weight = this.GetMonsterWeight(entity);

                    // Skip monsters with weight of 0 (user wants to ignore this rarity)
                    if (weight <= 0f)
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

                    // Calculate screen-space distance from cursor
                    var dx = screenPos.X - cursorPos.X;
                    var dy = screenPos.Y - cursorPos.Y;
                    var screenDistance = (float)Math.Sqrt(dx * dx + dy * dy);

                    // Check if within radius
                    if (screenDistance <= radiusPixels)
                    {
                        // Calculate weighted score for prioritization
                        var score = weight / Math.Max(screenDistance, 0.1f);
                        candidates.Add((entity, score, screenDistance));
                    }
                }

                // Sort candidates by score (highest first) and return the sorted list
                enemiesInRadius = candidates
                    .OrderByDescending(c => c.score)
                    .Select(c => c.entity)
                    .ToList();
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
                var currentProfile = this.GetCurrentProfile();
                if (currentProfile.CursorRules == null || currentProfile.CursorRules.Count == 0)
                {
                    return;
                }

                // Process each enabled rule, sorted by priority (highest first)
                foreach (var rule in currentProfile.CursorRules.Where(r => r.Enabled).OrderByDescending(r => r.Priority))
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
                var currentProfile = this.GetCurrentProfile();
                if (currentProfile.CursorRules == null || currentProfile.CursorRules.Count == 0)
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
                for (int i = 0; i < currentProfile.CursorRules.Count; i++)
                {
                    var rule = currentProfile.CursorRules[i];
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
                var currentProfile = this.GetCurrentProfile();
                ImGui.Text($"Active Profile: {currentProfile.Name}");
                ImGui.Text($"Total Rules: {currentProfile.CursorRules?.Count ?? 0}");
                ImGui.Text($"Active Rules: {currentProfile.CursorRules?.Count(r => r.Enabled) ?? 0}");

                var cursorPos = MouseCompatibilityHelper.GetCursorPosition();
                ImGui.Text($"Cursor Position: ({cursorPos.X}, {cursorPos.Y})");

                if (currentProfile.CursorRules != null && currentProfile.CursorRules.Count > 0)
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
                    foreach (var rule in currentProfile.CursorRules)
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

        /// <summary>
        /// Gets the currently active profile, creating default if none exists.
        /// </summary>
        /// <returns>The active profile.</returns>
        private Profile GetCurrentProfile()
        {
            // Ensure profiles dictionary exists
            if (this.Settings.Profiles == null)
            {
                this.Settings.Profiles = new Dictionary<string, Profile>();
            }

            // If no profiles exist, create default
            if (this.Settings.Profiles.Count == 0)
            {
                this.CreateDefaultProfile();
            }

            // If current profile is empty or doesn't exist, use first available
            if (string.IsNullOrEmpty(this.Settings.CurrentProfile) ||
                !this.Settings.Profiles.ContainsKey(this.Settings.CurrentProfile))
            {
                this.Settings.CurrentProfile = this.Settings.Profiles.Keys.First();
            }

            return this.Settings.Profiles[this.Settings.CurrentProfile];
        }

        /// <summary>
        /// Sets the current active profile by name.
        /// </summary>
        /// <param name="profileName">Name of the profile to activate.</param>
        private void SetCurrentProfile(string profileName)
        {
            if (this.Settings.Profiles.ContainsKey(profileName))
            {
                // Interrupt any executing rules before switching
                this.InterruptCurrentRule();
                this.ClearExecutingRule();

                this.Settings.CurrentProfile = profileName;

                Console.WriteLine($"Switched to profile: {profileName}");
            }
        }

        /// <summary>
        /// Creates a new profile with the given name.
        /// </summary>
        /// <param name="name">Name for the new profile.</param>
        /// <param name="description">Optional description.</param>
        private void CreateProfile(string name, string description = "")
        {
            if (!this.Settings.Profiles.ContainsKey(name))
            {
                var profile = new Profile
                {
                    Name = name,
                    Description = description,
                    CursorRules = new List<CursorRule>(),
                    EnableCursorTracking = false,
                    ToggleHotKey = VK.F8
                };

                this.Settings.Profiles.Add(name, profile);
                Console.WriteLine($"Created profile: {name}");
            }
        }

        /// <summary>
        /// Clones an existing profile with a new name.
        /// </summary>
        /// <param name="sourceName">Name of profile to clone.</param>
        /// <param name="newName">Name for the cloned profile.</param>
        private void CloneProfile(string sourceName, string newName)
        {
            if (this.Settings.Profiles.ContainsKey(sourceName) &&
                !this.Settings.Profiles.ContainsKey(newName))
            {
                var sourceProfile = this.Settings.Profiles[sourceName];
                var clonedProfile = new Profile(sourceProfile)
                {
                    Name = newName
                };

                this.Settings.Profiles.Add(newName, clonedProfile);
                Console.WriteLine($"Cloned profile '{sourceName}' to '{newName}'");
            }
        }

        /// <summary>
        /// Deletes a profile by name.
        /// </summary>
        /// <param name="name">Name of profile to delete.</param>
        private void DeleteProfile(string name)
        {
            // Don't allow deleting the last profile
            if (this.Settings.Profiles.Count <= 1)
            {
                Console.WriteLine("Cannot delete the last profile");
                return;
            }

            if (this.Settings.Profiles.ContainsKey(name))
            {
                this.Settings.Profiles.Remove(name);

                // If we deleted the current profile, switch to another
                if (this.Settings.CurrentProfile == name)
                {
                    this.Settings.CurrentProfile = this.Settings.Profiles.Keys.First();
                }

                Console.WriteLine($"Deleted profile: {name}");
            }
        }

        /// <summary>
        /// Creates the default profile if it doesn't exist.
        /// </summary>
        private void CreateDefaultProfile()
        {
            if (!this.Settings.Profiles.ContainsKey("Default"))
            {
                var defaultProfile = Profile.CreateDefault();
                this.Settings.Profiles.Add("Default", defaultProfile);
                this.Settings.CurrentProfile = "Default";
                Console.WriteLine("Created default profile");
            }
        }
    }
}