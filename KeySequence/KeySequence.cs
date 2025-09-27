// <copyright file="KeySequence.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace NearestEnemyHighlight.KeySequence
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    /// <summary>
    /// Manages a sequence of key actions with execution control and cancellation support.
    /// </summary>
    public class KeySequence
    {
        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="KeySequence"/> class.
        /// </summary>
        public KeySequence()
        {
            this.Actions = new List<KeyAction>();
            this.IsExecuting = false;
        }

        /// <summary>
        /// Copy constructor for cloning sequences.
        /// </summary>
        /// <param name="other">KeySequence to copy from.</param>
        public KeySequence(KeySequence other)
        {
            this.Actions = new List<KeyAction>();
            foreach (var action in other.Actions)
            {
                this.Actions.Add(new KeyAction(action));
            }
            this.IsExecuting = false;
        }

        /// <summary>
        /// Gets or sets the list of key actions in this sequence.
        /// </summary>
        public List<KeyAction> Actions { get; set; }

        /// <summary>
        /// Gets a value indicating whether this sequence is currently executing.
        /// </summary>
        [JsonIgnore]
        public bool IsExecuting { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this sequence has valid actions to execute.
        /// </summary>
        [JsonIgnore]
        public bool HasActions => this.Actions?.Count > 0 && this.Actions.Count <= 8;

        /// <summary>
        /// Gets the total estimated execution time in milliseconds.
        /// </summary>
        [JsonIgnore]
        public int EstimatedExecutionTime
        {
            get
            {
                if (!this.HasActions) return 0;
                return this.Actions.Sum(a => a.DelayMs) + (this.Actions.Count * 20); // ~20ms per key press
            }
        }

        /// <summary>
        /// Gets a summary string for display purposes.
        /// </summary>
        /// <returns>String like "Q → W(100ms) → E(200ms)".</returns>
        public string GetSummary()
        {
            if (!this.HasActions)
            {
                return "Empty sequence";
            }

            var parts = new List<string>();
            for (int i = 0; i < this.Actions.Count; i++)
            {
                var action = this.Actions[i];
                if (i == 0)
                {
                    // First action shows no delay (executes immediately)
                    parts.Add(action.DisplayString);
                }
                else
                {
                    // Subsequent actions show their delay
                    parts.Add(action.GetPreviewString());
                }
            }

            return string.Join(" → ", parts);
        }

        /// <summary>
        /// Adds a new key action to the sequence.
        /// </summary>
        /// <param name="key">The key to add.</param>
        /// <param name="delayMs">Delay before this key (default 50ms).</param>
        /// <returns>True if added successfully, false if sequence is full.</returns>
        public bool AddAction(ClickableTransparentOverlay.Win32.VK key, int delayMs = 50)
        {
            if (this.Actions.Count >= 8)
            {
                return false; // Max sequence length reached
            }

            var action = new KeyAction(key, delayMs);
            action.ClampDelay(); // Ensure valid delay range
            this.Actions.Add(action);
            return true;
        }

        /// <summary>
        /// Removes an action at the specified index.
        /// </summary>
        /// <param name="index">Index of action to remove.</param>
        /// <returns>True if removed successfully.</returns>
        public bool RemoveAction(int index)
        {
            if (index >= 0 && index < this.Actions.Count)
            {
                this.Actions.RemoveAt(index);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clears all actions from the sequence.
        /// </summary>
        public void ClearActions()
        {
            this.Actions.Clear();
        }

        /// <summary>
        /// Validates all actions in the sequence.
        /// </summary>
        /// <returns>True if all actions are valid.</returns>
        public bool IsValid()
        {
            if (!this.HasActions) return false;
            return this.Actions.All(a => a.IsValidDelay());
        }

        /// <summary>
        /// Executes the key sequence asynchronously with cancellation support.
        /// </summary>
        /// <param name="logger">Logging action for feedback.</param>
        /// <param name="externalCancellationToken">External cancellation token for interruption.</param>
        public void Execute(Action<string> logger, CancellationToken externalCancellationToken = default)
        {
            if (this.IsExecuting)
            {
                logger?.Invoke("Sequence already executing");
                return;
            }

            if (!this.IsValid())
            {
                logger?.Invoke("Invalid sequence - cannot execute");
                return;
            }

            // Start execution in background task
            Task.Run(async () => await this.ExecuteSequenceAsync(logger, externalCancellationToken));
        }

        /// <summary>
        /// Cancels the currently executing sequence.
        /// </summary>
        public void Cancel()
        {
            try
            {
                this.cancellationTokenSource?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Token already disposed, sequence likely finished
            }
        }

        /// <summary>
        /// Internal async execution method.
        /// </summary>
        /// <param name="logger">Logging action.</param>
        /// <param name="externalCancellationToken">External cancellation token.</param>
        /// <returns>Task representing the async operation.</returns>
        private async Task ExecuteSequenceAsync(Action<string> logger, CancellationToken externalCancellationToken)
        {
            this.IsExecuting = true;
            this.cancellationTokenSource = new CancellationTokenSource();

            // Combine internal and external cancellation tokens
            using (var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                this.cancellationTokenSource.Token, externalCancellationToken))
            {
                try
                {
                    logger?.Invoke($"Executing sequence: {this.GetSummary()}");

                    for (int i = 0; i < this.Actions.Count; i++)
                    {
                        var action = this.Actions[i];

                        // Check for cancellation before each action
                        combinedTokenSource.Token.ThrowIfCancellationRequested();

                        // Apply delay before key press (except for first action)
                        if (i > 0 && action.DelayMs > 0)
                        {
                            await Task.Delay(action.DelayMs, combinedTokenSource.Token);
                        }

                        // Check cancellation again after delay
                        combinedTokenSource.Token.ThrowIfCancellationRequested();

                        // Execute the key press
                        if (action.Execute())
                        {
                            logger?.Invoke($"Sequence: Pressed {action.DisplayString}");
                        }
                        else
                        {
                            logger?.Invoke($"Sequence: Failed to press {action.DisplayString}");
                        }
                    }

                    logger?.Invoke("Sequence completed successfully");
                }
                catch (OperationCanceledException)
                {
                    logger?.Invoke("Sequence was interrupted");
                }
                catch (Exception ex)
                {
                    logger?.Invoke($"Sequence error: {ex.Message}");
                }
                finally
                {
                    this.IsExecuting = false;
                    this.cancellationTokenSource?.Dispose();
                    this.cancellationTokenSource = null;
                }
            }
        }

        /// <summary>
        /// Returns a string representation of the sequence.
        /// </summary>
        /// <returns>String representation.</returns>
        public override string ToString()
        {
            return $"KeySequence: {this.Actions.Count} actions, {this.GetSummary()}";
        }
    }
}