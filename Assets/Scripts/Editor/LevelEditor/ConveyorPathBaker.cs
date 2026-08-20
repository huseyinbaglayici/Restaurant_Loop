using System.Collections.Generic;
using System.Linq;
using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace Editor.LevelEditor
{
    /// <summary>
    /// Turns the designer's unordered ConveyorCells (Shape+Rotation per cell) into the belt's
    /// actual travel order, run once when the level is saved. Gameplay never sees Shape/Rotation —
    /// only the resulting <see cref="ConveyorPathData.OrderedPath"/>.
    ///
    /// Method: derive which two orthogonal edges each cell's belt touches from its Shape+Rotation
    /// (the same geometry the editor's two-arrow corner rendering already uses), link cells whose
    /// edges face each other, and require every cell end up with exactly two neighbours — i.e. a
    /// single closed loop. From the Gate cell, walk the loop both ways and keep whichever direction
    /// is clockwise on screen (negative shoelace signed area on the grid coordinates).
    /// </summary>
    internal static class ConveyorPathBaker
    {
        public static bool TryBake(ConveyorLevelDataSO data, out List<Vector2Int> orderedPath, out string error)
        {
            orderedPath = null;

            var cellsByCoord = new Dictionary<Vector2Int, ConveyorCellData>();
            foreach (var c in data.ConveyorCells) cellsByCoord[c.Coord] = c;

            if (cellsByCoord.Count == 0)
            {
                error = "No conveyor cells painted.";
                return false;
            }

            var gateCell = data.ConveyorCells.FirstOrDefault(c => c.IsGate);
            if (!gateCell.IsGate)
            {
                error = "No Gate set. Ctrl+Click a conveyor cell in Path mode to mark it.";
                return false;
            }

            var edgesByCoord = new Dictionary<Vector2Int, List<Vector2Int>>();
            foreach (var cell in data.ConveyorCells)
                edgesByCoord[cell.Coord] = ConnectedDirections(cell.Shape, cell.Rotation);

            var neighbors = new Dictionary<Vector2Int, List<Vector2Int>>();
            foreach (var cell in data.ConveyorCells)
            {
                var list = new List<Vector2Int>();
                foreach (var dir in edgesByCoord[cell.Coord])
                {
                    var n = cell.Coord + dir;
                    if (cellsByCoord.ContainsKey(n) && edgesByCoord[n].Contains(-dir))
                        list.Add(n);
                }
                neighbors[cell.Coord] = list;
            }

            foreach (var kv in neighbors)
            {
                if (kv.Value.Count != 2)
                {
                    error = $"Conveyor isn't a single closed loop at {kv.Key} ({kv.Value.Count} connection(s), needs 2).";
                    return false;
                }
            }

            var pathA = WalkCycle(gateCell.Coord, neighbors[gateCell.Coord][0], neighbors);
            var pathB = WalkCycle(gateCell.Coord, neighbors[gateCell.Coord][1], neighbors);
            var clockwise = SignedArea(pathA) < 0f ? pathA : pathB;

            int gateIdx = clockwise.IndexOf(gateCell.Coord);
            orderedPath = clockwise.Skip(gateIdx + 1).Concat(clockwise.Take(gateIdx)).ToList();
            error = null;
            return true;
        }

        private static List<Vector2Int> WalkCycle(Vector2Int start, Vector2Int second, Dictionary<Vector2Int, List<Vector2Int>> neighbors)
        {
            var path = new List<Vector2Int> { start };
            var prev = start;
            var current = second;
            while (current != start)
            {
                path.Add(current);
                var options = neighbors[current];
                var next = options[0] == prev ? options[1] : options[0];
                prev = current;
                current = next;
            }
            return path;
        }

        private static float SignedArea(List<Vector2Int> path)
        {
            float area = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                var a = path[i];
                var b = path[(i + 1) % path.Count];
                area += a.x * b.y - b.x * a.y;
            }
            return area * 0.5f;
        }

        /// <summary>
        /// Which orthogonal grid directions this cell's belt touches, before rotation. Straight
        /// connects two opposite edges (its rotation = the facing direction). Corner connects Left
        /// and Up at rotation 0, matching the editor's DrawCornerTile pivot.
        /// </summary>
        private static List<Vector2Int> ConnectedDirections(ConveyorTileShape shape, float rotation)
        {
            if (shape == ConveyorTileShape.Straight)
            {
                var dir = Rotate(Vector2Int.right, rotation);
                return new List<Vector2Int> { dir, -dir };
            }

            return new List<Vector2Int> { Rotate(Vector2Int.left, rotation), Rotate(Vector2Int.up, rotation) };
        }

        /// <summary>
        /// Standard counter-clockwise rotation in grid space (+Y = up), which is what reproduces the
        /// editor canvas's screen-space rotation once you account for the canvas being Y-down.
        /// </summary>
        private static Vector2Int Rotate(Vector2Int dir, float degrees)
        {
            int steps = Mathf.RoundToInt(degrees / 90f) % 4;
            if (steps < 0) steps += 4;
            for (int i = 0; i < steps; i++) dir = new Vector2Int(-dir.y, dir.x);
            return dir;
        }
    }
}
