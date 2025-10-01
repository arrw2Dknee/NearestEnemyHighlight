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
        /// Initializes a new instance of the <see cref="NearestEnemySettings"/> class.
        /// </summary>
        public NearestEnemySettings()
        {
            // Initialize profile system
            this.Profiles = new Dictionary<string, Profile>();
            this.CurrentProfile = string.Empty;
        }

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
        /// Gets or sets a value indicating whether to pause cursor tracking during UI interactions.
        /// </summary>
        public bool PauseDuringUI { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether line-of-sight checking is enabled.
        /// </summary>
        public bool EnableLineOfSight { get; set; } = true;

        /// <summary>
        /// Gets or sets the line-of-sight tolerance (0 = perfect LOS, higher = allows more obstacles).
        /// </summary>
        public float LineOfSightTolerance { get; set; } = 2.0f;

        /// <summary>
        /// Gets or sets a value indicating whether to show line-of-sight debug visualization.
        /// </summary>
        public bool ShowLineOfSightDebug { get; set; } = false;

        /// <summary>
        /// Gets or sets the priority weight for Normal rarity monsters (0 = ignore, 1-100 = priority level).
        /// </summary>
        public float NormalMonsterWeight { get; set; } = 50f;

        /// <summary>
        /// Gets or sets the priority weight for Magic rarity monsters (0 = ignore, 1-100 = priority level).
        /// </summary>
        public float MagicMonsterWeight { get; set; } = 50f;

        /// <summary>
        /// Gets or sets the priority weight for Rare rarity monsters (0 = ignore, 1-100 = priority level).
        /// </summary>
        public float RareMonsterWeight { get; set; } = 50f;

        /// <summary>
        /// Gets or sets the priority weight for Unique rarity monsters (0 = ignore, 1-100 = priority level).
        /// </summary>
        public float UniqueMonsterWeight { get; set; } = 50f;

        /// <summary>
        /// Gets or sets the dictionary of automation profiles.
        /// </summary>
        public Dictionary<string, Profile> Profiles { get; set; }

        /// <summary>
        /// Gets or sets the currently active profile name.
        /// </summary>
        public string CurrentProfile { get; set; }
    }
}