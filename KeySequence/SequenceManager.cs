// <copyright file="SequenceManager.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace NearestEnemyHighlight.KeySequence
{
    using System;
    using System.Threading;

    /// <summary>
    /// Manages priority-based execution of key sequences with interruption logic.
    /// </summary>
    public class SequenceManager
    {
        private readonly object executionLock = new object();
        private CursorRule currentlyExecutingRule = null;
        private CancellationTokenSource currentCancellationTokenSource = null;

        /// <summary>
        /// Gets the currently executing rule, if any.
        /// </summary>
        public CursorRule CurrentlyExecutingRule
        {
            get
            {
                lock (this.executionLock)
                {
                    return this.currentlyExecutingRule;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether any sequence is currently executing.
        /// </summary>
        public bool IsExecutingSequence
        {
            get
            {
                lock (this.executionLock)
                {
                    return this.currentlyExecutingRule != null;
                }
            }
        }

        /// <summary>
        /// Attempts to execute a rule's sequence based on priority logic.
        /// </summary>
        /// <param name="rule">The rule to execute.</param>
        /// <param name="logger">Logging action for feedback.</param>
        /// <returns>True if execution started, false if cancelled due to lower priority.</returns>
        public bool TryExecuteRule(CursorRule rule, Action<string> logger)
        {
            if (rule == null || !rule.Enabled)
            {
                return false;
            }

            lock (this.executionLock)
            {
                // No sequence currently running - execute immediately
                if (this.currentlyExecutingRule == null)
                {
                    this.StartExecution(rule, logger);
                    return true;
                }

                // Compare priorities (higher number = higher priority)
                if (rule.Priority > this.currentlyExecutingRule.Priority)
                {
                    // New rule has higher priority - interrupt current
                    this.InterruptCurrentSequence($"Interrupted by higher priority rule: {rule.Name} (Priority {rule.Priority})");
                    this.StartExecution(rule, logger);
                    return true;
                }
                else if (rule.Priority == this.currentlyExecutingRule.Priority)
                {
                    // Same priority - interrupt current (user's preference)
                    this.InterruptCurrentSequence($"Interrupted by same priority rule: {rule.Name} (Priority {rule.Priority})");
                    this.StartExecution(rule, logger);
                    return true;
                }
                else
                {
                    // New rule has lower priority - cancel it
                    logger?.Invoke($"Rule '{rule.Name}' (Priority {rule.Priority}) cancelled - lower priority than '{this.currentlyExecutingRule.Name}' (Priority {this.currentlyExecutingRule.Priority})");
                    return false;
                }
            }
        }

        /// <summary>
        /// Forces cancellation of any currently executing sequence.
        /// </summary>
        /// <param name="reason">Reason for cancellation.</param>
        public void CancelCurrentSequence(string reason = "Manual cancellation")
        {
            lock (this.executionLock)
            {
                if (this.currentlyExecutingRule != null)
                {
                    this.InterruptCurrentSequence(reason);
                }
            }
        }

        /// <summary>
        /// Gets status information about current execution state.
        /// </summary>
        /// <returns>Status string for debugging.</returns>
        public string GetExecutionStatus()
        {
            lock (this.executionLock)
            {
                if (this.currentlyExecutingRule == null)
                {
                    return "No sequence executing";
                }

                var rule = this.currentlyExecutingRule;
                var timeInfo = rule.UseKeySequence && rule.KeySequence != null
                    ? $" (Est. {rule.KeySequence.EstimatedExecutionTime}ms)"
                    : "";

                return $"Executing: '{rule.Name}' (Priority {rule.Priority}){timeInfo}";
            }
        }

        /// <summary>
        /// Starts execution of a rule's sequence.
        /// </summary>
        /// <param name="rule">The rule to execute.</param>
        /// <param name="logger">Logging action.</param>
        private void StartExecution(CursorRule rule, Action<string> logger)
        {
            this.currentlyExecutingRule = rule;
            this.currentCancellationTokenSource = new CancellationTokenSource();

            try
            {
                if (rule.UseKeySequence && rule.KeySequence != null && rule.KeySequence.HasActions)
                {
                    // Execute key sequence
                    logger?.Invoke($"Starting sequence execution: {rule.Name} (Priority {rule.Priority})");

                    // Start sequence execution with completion callback
                    rule.KeySequence.Execute(logger, this.currentCancellationTokenSource.Token);

                    // Monitor for completion
                    this.MonitorSequenceCompletion(rule, logger);
                }
                else
                {
                    // This shouldn't happen in sequence manager, but handle gracefully
                    logger?.Invoke($"Warning: Rule '{rule.Name}' has no valid sequence to execute");
                    this.CleanupExecution();
                }
            }
            catch (Exception ex)
            {
                logger?.Invoke($"Error starting sequence execution: {ex.Message}");
                this.CleanupExecution();
            }
        }

        /// <summary>
        /// Interrupts the currently executing sequence.
        /// </summary>
        /// <param name="reason">Reason for interruption.</param>
        private void InterruptCurrentSequence(string reason)
        {
            if (this.currentlyExecutingRule == null) return;

            try
            {
                // Cancel the sequence
                if (this.currentlyExecutingRule.UseKeySequence && this.currentlyExecutingRule.KeySequence != null)
                {
                    this.currentlyExecutingRule.KeySequence.Cancel();
                }

                // Cancel via token source as well
                this.currentCancellationTokenSource?.Cancel();

                Console.WriteLine($"Sequence interrupted: {reason}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error interrupting sequence: {ex.Message}");
            }
            finally
            {
                this.CleanupExecution();
            }
        }

        /// <summary>
        /// Monitors sequence completion and cleans up when done.
        /// </summary>
        /// <param name="rule">The rule being executed.</param>
        /// <param name="logger">Logging action.</param>
        private void MonitorSequenceCompletion(CursorRule rule, Action<string> logger)
        {
            // Use a background task to monitor completion
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    // Poll for completion (simple approach)
                    while (rule.KeySequence != null && rule.KeySequence.IsExecuting)
                    {
                        await System.Threading.Tasks.Task.Delay(50); // Check every 50ms

                        // Also check if the rule is still the current one
                        lock (this.executionLock)
                        {
                            if (this.currentlyExecutingRule != rule)
                            {
                                // Rule was replaced, stop monitoring
                                return;
                            }
                        }
                    }

                    // Sequence completed
                    lock (this.executionLock)
                    {
                        if (this.currentlyExecutingRule == rule)
                        {
                            logger?.Invoke($"Sequence completed: {rule.Name}");
                            this.CleanupExecution();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error monitoring sequence completion: {ex.Message}");
                    lock (this.executionLock)
                    {
                        if (this.currentlyExecutingRule == rule)
                        {
                            this.CleanupExecution();
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Cleans up execution state.
        /// </summary>
        private void CleanupExecution()
        {
            this.currentlyExecutingRule = null;

            try
            {
                this.currentCancellationTokenSource?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }

            this.currentCancellationTokenSource = null;
        }
    }
}