// <copyright file="KeySequence.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace NearestEnemyHighlight
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a sequence of key actions to be executed in order
    /// </summary>
    public class KeySequence
    {
        private volatile bool isExecuting = false;
        private volatile bool isCancelled = false;

        /// <summary>
        /// List of key actions to execute in sequence
        /// </summary>
        [JsonProperty]
        public List<KeyAction> Actions { get; set; }

        /// <summary>
        /// Gets a value indicating whether the sequence is currently executing
        /// </summary>
        [JsonIgnore]
        public bool IsExecuting => isExecuting;

        /// <summary>
        /// Gets a value indicating whether this sequence has any actions
        /// </summary>
        [JsonIgnore]
        public bool HasActions => Actions != null && Actions.Count > 0;

        /// <summary>
        /// Gets a value indicating whether the sequence execution was cancelled
        /// </summary>
        [JsonIgnore]
        public bool IsCancelled => isCancelled;

        /// <summary>
        /// Initializes a new instance of the <see cref="KeySequence"/> class.
        /// </summary>
        public KeySequence()
        {
            Actions = new List<KeyAction>();
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="other">KeySequence to copy from</param>
        public KeySequence(KeySequence other)
        {
            Actions = new List<KeyAction>();
            if (other?.Actions != null)
            {
                foreach (var action in other.Actions)
                {
                    Actions.Add(new KeyAction(action));
                }
            }
        }

        /// <summary>
        /// Gets a summary string of the sequence
        /// </summary>
        /// <returns>String describing the sequence</returns>
        public string GetSummary()
        {
            if (!HasActions)
                return "No actions";

            if (Actions.Count == 1)
                return Actions[0].GetDisplayString();

            return $"{Actions.Count} actions: {string.Join(" → ", Actions.Take(3).Select(a => a.GetDisplayString()))}{(Actions.Count > 3 ? "..." : "")}";
        }

        /// <summary>
        /// Cancels the currently executing sequence
        /// </summary>
        public void Cancel()
        {
            isCancelled = true;
        }

        /// <summary>
        /// Executes the key sequence asynchronously
        /// </summary>
        /// <param name="logger">Optional logger for debugging</param>
        /// <param name="onCompleted">Callback to execute when sequence completes</param>
        public async Task Execute(Action<string> logger = null, Action onCompleted = null)
        {
            if (isExecuting || !HasActions)
                return;

            isExecuting = true;

            try
            {
                isCancelled = false; // Reset cancellation flag

                foreach (var action in Actions)
                {
                    // Check for cancellation before each action
                    if (isCancelled)
                    {
                        if (logger != null)
                        {
                            logger("Sequence execution cancelled");
                        }
                        break;
                    }

                    // Apply delay before executing this action
                    if (action.DelayMs > 0)
                    {
                        await Task.Delay(action.DelayMs);

                        // Check cancellation after delay
                        if (isCancelled)
                        {
                            if (logger != null)
                            {
                                logger("Sequence execution cancelled during delay");
                            }
                            break;
                        }
                    }

                    // Execute the key action
                    bool success = action.Execute();

                    if (logger != null)
                    {
                        logger($"Executed {action.GetDisplayString()}: {(success ? "Success" : "Failed")}");
                    }

                    // Small delay between actions to ensure they register properly
                    await Task.Delay(10);
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger($"KeySequence execution error: {ex.Message}");
                }
            }
            finally
            {
                isExecuting = false;

                // Call completion callback to signal sequence is done
                onCompleted?.Invoke();
            }
        }

        /// <summary>
        /// Adds a new key action to the sequence
        /// </summary>
        /// <param name="action">KeyAction to add</param>
        public void AddAction(KeyAction action)
        {
            if (Actions == null)
                Actions = new List<KeyAction>();

            Actions.Add(action);
        }

        /// <summary>
        /// Removes a key action at the specified index
        /// </summary>
        /// <param name="index">Index to remove</param>
        public void RemoveAction(int index)
        {
            if (Actions != null && index >= 0 && index < Actions.Count)
            {
                Actions.RemoveAt(index);
            }
        }

        /// <summary>
        /// Clears all actions from the sequence
        /// </summary>
        public void Clear()
        {
            Actions?.Clear();
        }
    }
}