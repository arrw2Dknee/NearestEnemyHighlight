// <copyright file="KeyAction.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace NearestEnemyHighlight.KeySequence
{
    using ClickableTransparentOverlay.Win32;
    using GameHelper.Utils;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a single key action in a sequence with timing control.
    /// </summary>
    public class KeyAction
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KeyAction"/> class.
        /// </summary>
        public KeyAction()
        {
            this.Key = VK.KEY_Q;
            this.DelayMs = 50; // Default 50ms delay
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyAction"/> class.
        /// </summary>
        /// <param name="key">The key to press.</param>
        /// <param name="delayMs">Delay in milliseconds before pressing this key.</param>
        public KeyAction(VK key, int delayMs = 50)
        {
            this.Key = key;
            this.DelayMs = delayMs;
        }

        /// <summary>
        /// Copy constructor for cloning key actions.
        /// </summary>
        /// <param name="other">KeyAction to copy from.</param>
        public KeyAction(KeyAction other)
        {
            this.Key = other.Key;
            this.DelayMs = other.DelayMs;
        }

        /// <summary>
        /// Gets or sets the key to press.
        /// </summary>
        public VK Key { get; set; }

        /// <summary>
        /// Gets or sets the delay in milliseconds before pressing this key.
        /// Valid range: 25-5000ms.
        /// </summary>
        public int DelayMs { get; set; }

        /// <summary>
        /// Gets a display string representation of this key action.
        /// </summary>
        /// <returns>String like "Q" or "SPACE" for display purposes.</returns>
        [JsonIgnore]
        public string DisplayString => this.Key.ToString().Replace("KEY_", "");

        /// <summary>
        /// Executes this key action using GameHelper's safe key method.
        /// </summary>
        /// <returns>True if the key was successfully pressed.</returns>
        public bool Execute()
        {
            try
            {
                return MiscHelper.KeyUp(this.Key);
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Error executing key {this.Key}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates that the delay is within acceptable range.
        /// </summary>
        /// <returns>True if delay is valid.</returns>
        public bool IsValidDelay()
        {
            return this.DelayMs >= 25 && this.DelayMs <= 5000;
        }

        /// <summary>
        /// Clamps the delay to valid range.
        /// </summary>
        public void ClampDelay()
        {
            if (this.DelayMs < 25) this.DelayMs = 25;
            if (this.DelayMs > 5000) this.DelayMs = 5000;
        }

        /// <summary>
        /// Gets a formatted string for sequence preview.
        /// </summary>
        /// <returns>String like "Q(50ms)" for sequence display.</returns>
        public string GetPreviewString()
        {
            if (this.DelayMs > 0)
            {
                return $"{this.DisplayString}({this.DelayMs}ms)";
            }
            return this.DisplayString;
        }

        /// <summary>
        /// Returns a string representation of the key action.
        /// </summary>
        /// <returns>String representation.</returns>
        public override string ToString()
        {
            return $"KeyAction: {this.Key} (Delay: {this.DelayMs}ms)";
        }
    }
}