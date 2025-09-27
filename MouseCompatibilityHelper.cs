// <copyright file="MouseCompatibilityHelper.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace NearestEnemyHighlight
{
    using System;
    using System.Runtime.InteropServices;
    using GameHelper;

    /// <summary>
    /// Mouse cursor operations service for GameHelper plugins.
    /// Provides safe mouse cursor control using Win32 APIs.
    /// </summary>
    public static class MouseCompatibilityHelper
    {
        /// <summary>
        /// Snaps the mouse cursor to the specified screen coordinates.
        /// Includes safety checks for controller mode and process validation.
        /// </summary>
        /// <param name="x">X screen coordinate.</param>
        /// <param name="y">Y screen coordinate.</param>
        /// <returns>True if cursor was moved successfully.</returns>
        public static bool SnapCursorTo(int x, int y)
        {
            try
            {
                // Check if controller mode is enabled (safety check)
                if (Core.GHSettings?.EnableControllerMode == true)
                {
                    return false;
                }

                // Check if game process is valid
                if (Core.Process?.Pid == 0)
                {
                    return false;
                }

                // Move cursor using Win32 API
                return SetCursorPos(x, y);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MouseCompatibilityHelper: Error moving cursor: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the current mouse cursor position.
        /// </summary>
        /// <returns>Current cursor position, or zero if failed.</returns>
        public static POINT GetCursorPosition()
        {
            try
            {
                POINT point = new POINT();
                GetCursorPos(out point);
                return point;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MouseCompatibilityHelper: Error getting cursor position: {ex.Message}");
                return new POINT();
            }
        }

        #region Win32 API Declarations

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        #endregion
    }
}