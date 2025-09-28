// <copyright file="KeybindCapture.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace NearestEnemyHighlight
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using ClickableTransparentOverlay.Win32;
    using GameHelper.Utils;
    using ImGuiNET;

    /// <summary>
    /// Helper class for capturing key combinations from user input
    /// </summary>
    public static class KeybindCapture
    {
        private static readonly Dictionary<int, KeyCaptureState> captureStates = new();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        /// <summary>
        /// Represents the state of a key capture session
        /// </summary>
        private class KeyCaptureState
        {
            public bool IsCapturing { get; set; }
            public bool CapturedCtrl { get; set; }
            public bool CapturedAlt { get; set; }
            public bool CapturedShift { get; set; }
            public bool CapturedWin { get; set; }
            public VK CapturedKey { get; set; } = VK.KEY_1;
            public bool HasCapturedKey { get; set; }
            public DateTime LastCaptureTime { get; set; }
        }

        /// <summary>
        /// Draws a keybind capture interface
        /// </summary>
        /// <param name="id">Unique identifier for this capture session</param>
        /// <param name="currentAction">Current KeyAction to modify</param>
        /// <param name="buttonText">Text to show on the capture button</param>
        /// <returns>True if the keybind was updated</returns>
        public static bool DrawKeybindCapture(int id, KeyAction currentAction, string buttonText = null)
        {
            if (buttonText == null)
                buttonText = $"Set Keybind: {currentAction.GetDisplayString()}";

            bool updated = false;

            // Initialize capture state if needed
            if (!captureStates.ContainsKey(id))
            {
                captureStates[id] = new KeyCaptureState();
            }

            var state = captureStates[id];

            // Capture button
            if (ImGui.Button($"{buttonText}##capture{id}"))
            {
                ImGui.OpenPopup($"KeybindCapture{id}");
                state.IsCapturing = false;
            }

            // Keybind capture popup
            if (ImGui.BeginPopup($"KeybindCapture{id}"))
            {
                ImGui.Text("Key Binding Setup");
                ImGui.Separator();

                // Capture mode toggle
                if (ImGui.Button(state.IsCapturing ? "Stop Capturing" : "Start Capturing"))
                {
                    state.IsCapturing = !state.IsCapturing;
                    if (state.IsCapturing)
                    {
                        state.HasCapturedKey = false;
                        state.LastCaptureTime = DateTime.Now;
                    }
                }

                if (state.IsCapturing)
                {
                    ImGui.Text("Press any key combination...");

                    // Check for key presses
                    bool foundKey = false;
                    for (int vKey = 8; vKey <= 255; vKey++)
                    {
                        // Skip modifier keys themselves
                        if (vKey == 16 || vKey == 17 || vKey == 18 || vKey == 91 || vKey == 92)
                            continue;

                        short keyState = GetAsyncKeyState(vKey);
                        if ((keyState & 0x8000) != 0 && !foundKey)
                        {
                            // Capture modifiers
                            state.CapturedCtrl = (GetAsyncKeyState(17) & 0x8000) != 0;
                            state.CapturedAlt = (GetAsyncKeyState(18) & 0x8000) != 0;
                            state.CapturedShift = (GetAsyncKeyState(16) & 0x8000) != 0;
                            state.CapturedWin = (GetAsyncKeyState(91) & 0x8000) != 0 || (GetAsyncKeyState(92) & 0x8000) != 0;

                            // Try to convert to VK enum
                            if (Enum.IsDefined(typeof(VK), vKey))
                            {
                                state.CapturedKey = (VK)vKey;
                                state.HasCapturedKey = true;
                                state.IsCapturing = false;
                                foundKey = true;
                            }
                        }
                    }
                }

                if (state.HasCapturedKey)
                {
                    ImGui.Separator();
                    ImGui.Text("Captured:");
                    string capturedStr = "";
                    if (state.CapturedCtrl) capturedStr += "CTRL+";
                    if (state.CapturedAlt) capturedStr += "ALT+";
                    if (state.CapturedShift) capturedStr += "SHIFT+";
                    if (state.CapturedWin) capturedStr += "WIN+";
                    capturedStr += state.CapturedKey.ToString().Replace("KEY_", "");

                    ImGui.Text(capturedStr);

                    if (ImGui.Button("Apply"))
                    {
                        currentAction.Key = state.CapturedKey;
                        currentAction.UseCtrl = state.CapturedCtrl;
                        currentAction.UseAlt = state.CapturedAlt;
                        currentAction.UseShift = state.CapturedShift;
                        currentAction.UseWin = state.CapturedWin;
                        updated = true;
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SameLine();
                }

                if (ImGui.Button("Cancel"))
                {
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            return updated;
        }
    }
}