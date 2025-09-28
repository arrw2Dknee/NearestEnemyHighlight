// <copyright file="LineOfSightChecker.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace NearestEnemyHighlight
{
    using GameHelper;
    using GameHelper.RemoteObjects.Components;
    using GameHelper.RemoteObjects.States.InGameStateObjects;
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>
    /// Contains debug information about line-of-sight calculation.
    /// </summary>
    public class LineOfSightDebugInfo
    {
        /// <summary>
        /// Gets or sets a value indicating whether line of sight exists.
        /// </summary>
        public bool HasLineOfSight { get; set; }

        /// <summary>
        /// Gets or sets the positions of blocked tiles along the path.
        /// </summary>
        public List<Vector2> BlockedPositions { get; set; } = new List<Vector2>();

        /// <summary>
        /// Gets or sets all positions checked along the path.
        /// </summary>
        public List<Vector2> PathPositions { get; set; } = new List<Vector2>();

        /// <summary>
        /// Gets or sets the number of blocked tiles encountered.
        /// </summary>
        public int BlockedTileCount { get; set; }

        /// <summary>
        /// Gets or sets the tolerance value used for the check.
        /// </summary>
        public float ToleranceUsed { get; set; }

        /// <summary>
        /// Gets or sets the start position in grid coordinates.
        /// </summary>
        public Vector2 StartGridPos { get; set; }

        /// <summary>
        /// Gets or sets the end position in grid coordinates.
        /// </summary>
        public Vector2 EndGridPos { get; set; }
    }

    /// <summary>
    /// Utility class for checking line-of-sight between two points using terrain data.
    /// </summary>
    public static class LineOfSightChecker
    {
        /// <summary>
        /// Checks if there is a clear line of sight between player and target entity.
        /// </summary>
        /// <param name="playerEntity">The player entity.</param>
        /// <param name="targetEntity">The target entity.</param>
        /// <param name="tolerance">Number of non-walkable tiles to tolerate (0 = perfect LOS).</param>
        /// <returns>True if line of sight exists within tolerance, false otherwise.</returns>
        public static bool HasLineOfSight(Entity playerEntity, Entity targetEntity, float tolerance)
        {
            try
            {
                // Get area instance for terrain data
                var areaInstance = Core.States.InGameStateObject?.CurrentAreaInstance;
                if (areaInstance == null)
                {
                    return true; // Default to allowing targeting if no terrain data
                }

                // Get player and target render components for positions
                if (!playerEntity.TryGetComponent<Render>(out var playerRender) ||
                    !targetEntity.TryGetComponent<Render>(out var targetRender))
                {
                    return true; // Default to allowing targeting if no position data
                }

                // Get world positions
                var playerWorldPos = new Vector2(playerRender.WorldPosition.X, playerRender.WorldPosition.Y);
                var targetWorldPos = new Vector2(targetRender.WorldPosition.X, targetRender.WorldPosition.Y);

                // Convert to grid coordinates
                var gridConverter = areaInstance.WorldToGridConvertor;
                var playerGridPos = new Vector2(
                    playerWorldPos.X / gridConverter,
                    playerWorldPos.Y / gridConverter);
                var targetGridPos = new Vector2(
                    targetWorldPos.X / gridConverter,
                    targetWorldPos.Y / gridConverter);

                // Check line of sight using terrain data
                return HasLineOfSightGrid(
                    playerGridPos,
                    targetGridPos,
                    areaInstance.GridWalkableData,
                    areaInstance.TerrainMetadata.BytesPerRow,
                    tolerance);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking line of sight: {ex.Message}");
                return true; // Default to allowing targeting on error
            }
        }

        /// <summary>
        /// Checks if there is a clear line of sight between player and target entity, with debug information.
        /// </summary>
        /// <param name="playerEntity">The player entity.</param>
        /// <param name="targetEntity">The target entity.</param>
        /// <param name="tolerance">Number of non-walkable tiles to tolerate (0 = perfect LOS).</param>
        /// <param name="debugInfo">Debug information about the line-of-sight check.</param>
        /// <returns>True if line of sight exists within tolerance, false otherwise.</returns>
        public static bool HasLineOfSight(Entity playerEntity, Entity targetEntity, float tolerance, out LineOfSightDebugInfo debugInfo)
        {
            debugInfo = new LineOfSightDebugInfo { ToleranceUsed = tolerance };

            try
            {
                // Get area instance for terrain data
                var areaInstance = Core.States.InGameStateObject?.CurrentAreaInstance;
                if (areaInstance == null)
                {
                    debugInfo.HasLineOfSight = true;
                    return true; // Default to allowing targeting if no terrain data
                }

                // Get player and target render components for positions
                if (!playerEntity.TryGetComponent<Render>(out var playerRender) ||
                    !targetEntity.TryGetComponent<Render>(out var targetRender))
                {
                    debugInfo.HasLineOfSight = true;
                    return true; // Default to allowing targeting if no position data
                }

                // Get world positions
                var playerWorldPos = new Vector2(playerRender.WorldPosition.X, playerRender.WorldPosition.Y);
                var targetWorldPos = new Vector2(targetRender.WorldPosition.X, targetRender.WorldPosition.Y);

                // Convert to grid coordinates
                var gridConverter = areaInstance.WorldToGridConvertor;
                var playerGridPos = new Vector2(
                    playerWorldPos.X / gridConverter,
                    playerWorldPos.Y / gridConverter);
                var targetGridPos = new Vector2(
                    targetWorldPos.X / gridConverter,
                    targetWorldPos.Y / gridConverter);

                debugInfo.StartGridPos = playerGridPos;
                debugInfo.EndGridPos = targetGridPos;

                // Check line of sight using terrain data with debug collection
                debugInfo.HasLineOfSight = HasLineOfSightGridWithDebug(
                    playerGridPos,
                    targetGridPos,
                    areaInstance.GridWalkableData,
                    areaInstance.TerrainMetadata.BytesPerRow,
                    tolerance,
                    debugInfo);

                return debugInfo.HasLineOfSight;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking line of sight: {ex.Message}");
                debugInfo.HasLineOfSight = true;
                return true; // Default to allowing targeting on error
            }
        }

        /// <summary>
        /// Checks line of sight between two grid positions using walkable data.
        /// </summary>
        /// <param name="start">Start grid position.</param>
        /// <param name="end">End grid position.</param>
        /// <param name="walkableData">Terrain walkable data array.</param>
        /// <param name="bytesPerRow">Bytes per row in walkable data.</param>
        /// <param name="tolerance">Number of non-walkable tiles to tolerate.</param>
        /// <returns>True if line of sight exists within tolerance.</returns>
        private static bool HasLineOfSightGrid(Vector2 start, Vector2 end, byte[] walkableData, int bytesPerRow, float tolerance)
        {
            if (walkableData == null || bytesPerRow <= 0)
            {
                return true; // No terrain data, allow targeting
            }

            // Use simple line traversal algorithm (DDA-like)
            var startX = (int)start.X;
            var startY = (int)start.Y;
            var endX = (int)end.X;
            var endY = (int)end.Y;

            var deltaX = Math.Abs(endX - startX);
            var deltaY = Math.Abs(endY - startY);
            var stepX = startX < endX ? 1 : -1;
            var stepY = startY < endY ? 1 : -1;
            var error = deltaX - deltaY;

            var currentX = startX;
            var currentY = startY;
            var blockedTiles = 0;

            // Traverse line from start to end
            while (true)
            {
                // Check if current tile is walkable
                if (!IsWalkable(currentX, currentY, walkableData, bytesPerRow))
                {
                    blockedTiles++;
                    if (blockedTiles > tolerance)
                    {
                        return false; // Too many obstacles
                    }
                }

                // Check if we've reached the end
                if (currentX == endX && currentY == endY)
                {
                    break;
                }

                // Move to next position
                var error2 = 2 * error;
                if (error2 > -deltaY)
                {
                    error -= deltaY;
                    currentX += stepX;
                }
                if (error2 < deltaX)
                {
                    error += deltaX;
                    currentY += stepY;
                }
            }

            return true; // Line of sight exists within tolerance
        }

        /// <summary>
        /// Checks line of sight between two grid positions using walkable data, collecting debug information.
        /// </summary>
        /// <param name="start">Start grid position.</param>
        /// <param name="end">End grid position.</param>
        /// <param name="walkableData">Terrain walkable data array.</param>
        /// <param name="bytesPerRow">Bytes per row in walkable data.</param>
        /// <param name="tolerance">Number of non-walkable tiles to tolerate.</param>
        /// <param name="debugInfo">Debug information to populate.</param>
        /// <returns>True if line of sight exists within tolerance.</returns>
        private static bool HasLineOfSightGridWithDebug(Vector2 start, Vector2 end, byte[] walkableData, int bytesPerRow, float tolerance, LineOfSightDebugInfo debugInfo)
        {
            if (walkableData == null || bytesPerRow <= 0)
            {
                return true; // No terrain data, allow targeting
            }

            // Use simple line traversal algorithm (DDA-like)
            var startX = (int)start.X;
            var startY = (int)start.Y;
            var endX = (int)end.X;
            var endY = (int)end.Y;

            var deltaX = Math.Abs(endX - startX);
            var deltaY = Math.Abs(endY - startY);
            var stepX = startX < endX ? 1 : -1;
            var stepY = startY < endY ? 1 : -1;
            var error = deltaX - deltaY;

            var currentX = startX;
            var currentY = startY;
            var blockedTiles = 0;

            // Traverse line from start to end
            while (true)
            {
                var currentPos = new Vector2(currentX, currentY);
                debugInfo.PathPositions.Add(currentPos);

                // Check if current tile is walkable
                if (!IsWalkable(currentX, currentY, walkableData, bytesPerRow))
                {
                    blockedTiles++;
                    debugInfo.BlockedPositions.Add(currentPos);
                    if (blockedTiles > tolerance)
                    {
                        debugInfo.BlockedTileCount = blockedTiles;
                        return false; // Too many obstacles
                    }
                }

                // Check if we've reached the end
                if (currentX == endX && currentY == endY)
                {
                    break;
                }

                // Move to next position
                var error2 = 2 * error;
                if (error2 > -deltaY)
                {
                    error -= deltaY;
                    currentX += stepX;
                }
                if (error2 < deltaX)
                {
                    error += deltaX;
                    currentY += stepY;
                }
            }

            debugInfo.BlockedTileCount = blockedTiles;
            return true; // Line of sight exists within tolerance
        }

        /// <summary>
        /// Checks if a specific grid tile is walkable.
        /// </summary>
        /// <param name="gridX">Grid X coordinate.</param>
        /// <param name="gridY">Grid Y coordinate.</param>
        /// <param name="walkableData">Terrain walkable data array.</param>
        /// <param name="bytesPerRow">Bytes per row in walkable data.</param>
        /// <returns>True if the tile is walkable, false otherwise.</returns>
        private static bool IsWalkable(int gridX, int gridY, byte[] walkableData, int bytesPerRow)
        {
            try
            {
                // Calculate array index (each byte contains 2 tiles in nibbles)
                var index = (gridY * bytesPerRow) + (gridX / 2);

                // Bounds checking
                if (index < 0 || index >= walkableData.Length)
                {
                    return false; // Outside map bounds = not walkable
                }

                // Determine which nibble to read (first or second half of byte)
                var shiftAmount = (gridX % 2 == 0) ? 0 : 4;

                // Extract tile value (0 = not walkable, 1+ = walkable)
                var tileValue = (walkableData[index] >> shiftAmount) & 0xF;

                // Following MapEdgeDetector.CanWalk logic: 0 = not walkable
                return tileValue != 0;
            }
            catch
            {
                return false; // Error = not walkable
            }
        }
    }
}