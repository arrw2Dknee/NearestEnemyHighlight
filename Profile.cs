// <copyright file="Profile.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace NearestEnemyHighlight
{
    using ClickableTransparentOverlay.Win32;
    using Newtonsoft.Json;
    using System.Collections.Generic;

    /// <summary>
    /// Represents an automation profile containing cursor rules and related settings.
    /// </summary>
    public class Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Profile"/> class.
        /// </summary>
        public Profile()
        {
            this.Name = "New Profile";
            this.Description = string.Empty;
            this.CursorRules = new List<CursorRule>();
            this.EnableCursorTracking = false;
            this.ToggleHotKey = VK.F8;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Profile"/> class (copy constructor).
        /// </summary>
        /// <param name="other">Profile to copy from.</param>
        public Profile(Profile other)
        {
            this.Name = $"{other.Name} Copy";
            this.Description = other.Description;
            this.CursorRules = new List<CursorRule>();

            // Deep copy all rules
            foreach (var rule in other.CursorRules)
            {
                this.CursorRules.Add(new CursorRule(rule));
            }

            this.EnableCursorTracking = other.EnableCursorTracking;
            this.ToggleHotKey = other.ToggleHotKey;
        }

        /// <summary>
        /// Gets or sets the profile name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the profile description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the list of cursor-based detection rules.
        /// </summary>
        public List<CursorRule> CursorRules { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether cursor tracking is enabled.
        /// </summary>
        public bool EnableCursorTracking { get; set; }

        /// <summary>
        /// Gets or sets the hotkey for toggling cursor tracking.
        /// </summary>
        public VK ToggleHotKey { get; set; }

        /// <summary>
        /// Creates a default profile with no rules.
        /// </summary>
        /// <returns>A new default profile.</returns>
        public static Profile CreateDefault()
        {
            return new Profile
            {
                Name = "Default",
                Description = "Default automation profile",
                CursorRules = new List<CursorRule>(),
                EnableCursorTracking = false,
                ToggleHotKey = VK.F8
            };
        }
    }
}