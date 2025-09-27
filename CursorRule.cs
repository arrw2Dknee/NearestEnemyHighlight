// <copyright file="CursorRule.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace NearestEnemyHighlight
{
    using System;
    using System.Diagnostics;
    using ClickableTransparentOverlay.Win32;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a single cursor-based enemy detection rule.
    /// </summary>
    public class CursorRule
    {
        private readonly Stopwatch spamTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="CursorRule"/> class.
        /// </summary>
        public CursorRule()
        {
            this.spamTimer = Stopwatch.StartNew();
            this.Name = "New Rule";
            this.Key = VK.KEY_Q;
            this.SpamDelayMs = 100.0f;
            this.DetectionRadiusPixels = 150.0f;
            this.ShowVisualRadius = false;
            this.Enabled = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CursorRule"/> class.
        /// Copy constructor for cloning rules.
        /// </summary>
        /// <param name="other">Rule to copy from.</param>
        public CursorRule(CursorRule other)
        {
            this.spamTimer = Stopwatch.StartNew();
            this.Name = $"{other.Name} Copy";
            this.Key = other.Key;
            this.SpamDelayMs = other.SpamDelayMs;
            this.DetectionRadiusPixels = other.DetectionRadiusPixels;
            this.ShowVisualRadius = other.ShowVisualRadius;
            this.Enabled = false; // New rule starts disabled
        }

        /// <summary>
        /// Gets or sets a value indicating whether this rule is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the user-friendly name for this rule.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the key to spam when enemies are detected.
        /// </summary>
        public VK Key { get; set; }

        /// <summary>
        /// Gets or sets the delay in milliseconds between key presses.
        /// </summary>
        public float SpamDelayMs { get; set; }

        /// <summary>
        /// Gets or sets the detection radius around the cursor in screen pixels.
        /// </summary>
        public float DetectionRadiusPixels { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to draw the detection radius around the cursor.
        /// </summary>
        public bool ShowVisualRadius { get; set; }

        /// <summary>
        /// Gets a value indicating whether enough time has passed to allow another key press.
        /// </summary>
        [JsonIgnore]
        public bool CanSpamKey => this.spamTimer.ElapsedMilliseconds >= this.SpamDelayMs;

        /// <summary>
        /// Marks that a key was just pressed, resetting the spam timer.
        /// </summary>
        public void MarkKeyPressed()
        {
            this.spamTimer.Restart();
        }

        /// <summary>
        /// Creates a default rule setup for testing.
        /// </summary>
        /// <returns>A preconfigured cursor rule.</returns>
        public static CursorRule CreateDefault()
        {
            return new CursorRule
            {
                Name = "New Rule",
                Key = VK.KEY_Q,
                SpamDelayMs = 200.0f,        // More reasonable default delay
                DetectionRadiusPixels = 120.0f,  // Reasonable default radius
                ShowVisualRadius = true,     // Show radius by default for new users
                Enabled = false              // Start disabled so user can configure first
            };
        }
    }
}