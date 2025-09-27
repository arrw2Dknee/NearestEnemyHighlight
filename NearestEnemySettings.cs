// <copyright file="NearestEnemySettings.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace NearestEnemyHighlight
{
    using ClickableTransparentOverlay.Win32;
    using GameHelper.Plugin;

    using System.Collections.Generic;

    /// <summary>
    /// Settings for the Nearest Enemy Highlight plugin.
    /// </summary>
    public class NearestEnemySettings : IPSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether the plugin is enabled.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum distance to search for enemies.
        /// </summary>
        public float MaxSearchDistance { get; set; } = 100.0f;

        /// <summary>
        /// Gets or sets the radius of the circle drawn around the nearest enemy.
        /// </summary>
        public float CircleRadius { get; set; } = 30.0f;

        /// <summary>
        /// Gets or sets the thickness of the circle line.
        /// </summary>
        public float CircleThickness { get; set; } = 2.0f;

        /// <summary>
        /// Gets or sets the red component of the circle color (0-255).
        /// </summary>
        public byte CircleColorR { get; set; } = 255;

        /// <summary>
        /// Gets or sets the green component of the circle color (0-255).
        /// </summary>
        public byte CircleColorG { get; set; } = 0;

        /// <summary>
        /// Gets or sets the blue component of the circle color (0-255).
        /// </summary>
        public byte CircleColorB { get; set; } = 0;

        /// <summary>
        /// Gets or sets the alpha component of the circle color (0-255).
        /// </summary>
        public byte CircleColorA { get; set; } = 200;

        /// <summary>
        /// Gets or sets a value indicating whether to show debug information.
        /// </summary>
        public bool ShowDebugInfo { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether cursor tracking is enabled.
        /// </summary>
        public bool EnableCursorTracking { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to pause cursor tracking during UI interactions.
        /// </summary>
        public bool PauseDuringUI { get; set; } = true;

        /// <summary>
        /// Gets or sets the hotkey for toggling cursor tracking.
        /// </summary>
        public VK ToggleHotKey { get; set; } = VK.F8;

        /// <summary>
        /// Gets or sets the list of cursor-based detection rules.
        /// </summary>
        public List<CursorRule> CursorRules { get; set; } = new List<CursorRule>();
    }
}