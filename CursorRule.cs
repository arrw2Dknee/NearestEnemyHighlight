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
        private readonly Stopwatch sequenceCooldownTimer;

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
            this.UseKeySequence = false;
            this.KeySequence = new KeySequence();
            this.sequenceCooldownTimer = Stopwatch.StartNew();
            this.SequenceCooldownMs = 0.0f;
            this.Priority = 1;
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
            this.UseKeySequence = other.UseKeySequence;
            this.KeySequence = new KeySequence(other.KeySequence);
            this.sequenceCooldownTimer = Stopwatch.StartNew();
            this.SequenceCooldownMs = other.SequenceCooldownMs;
            this.Priority = other.Priority;
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
        /// Gets or sets a value indicating whether to use key sequence instead of single key.
        /// </summary>
        [JsonProperty]
        public bool UseKeySequence { get; set; }

        /// <summary>
        /// Gets or sets the cooldown in milliseconds for sequence execution.
        /// </summary>
        public float SequenceCooldownMs { get; set; }

        /// <summary>
        /// Gets or sets the priority of this rule (1-999, higher number = higher priority).
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Gets or sets the key sequence to execute when enemies are detected.
        /// </summary>
        [JsonProperty]
        public KeySequence KeySequence { get; set; }

        /// <summary>
        /// Gets a value indicating whether enough time has passed to allow another key press.
        /// </summary>
        [JsonIgnore]
        public bool CanSpamKey => this.spamTimer.ElapsedMilliseconds >= this.SpamDelayMs;

        /// <summary>
        /// Gets a value indicating whether enough time has passed to allow sequence execution.
        /// </summary>
        [JsonIgnore]
        public bool CanExecuteSequence => this.sequenceCooldownTimer.ElapsedMilliseconds >= this.SequenceCooldownMs;

        /// <summary>
        /// Gets the remaining sequence cooldown time in milliseconds for debug display.
        /// </summary>
        [JsonIgnore]
        public float SequenceCooldownRemainingMs => Math.Max(0, this.SequenceCooldownMs - this.sequenceCooldownTimer.ElapsedMilliseconds);

        /// <summary>
        /// Marks that a key was just pressed, resetting the spam timer.
        /// </summary>
        public void MarkKeyPressed()
        {
            this.spamTimer.Restart();
        }

        /// <summary>
        /// Marks that a sequence execution has completed, resetting the sequence cooldown timer.
        /// </summary>
        public void MarkSequenceCompleted()
        {
            this.sequenceCooldownTimer.Restart();
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
                Enabled = false,              // Start disabled so user can configure first
                UseKeySequence = false,              // Start with single key mode
                KeySequence = new KeySequence(),     // Initialize empty sequence
                SequenceCooldownMs = 0.0f,       // Default 1 second sequence cooldown
                Priority = 1,                     // Default medium priority
            };
        }
    }
}