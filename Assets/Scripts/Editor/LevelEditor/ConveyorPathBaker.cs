using System.Collections.Generic;
using System.Linq;
using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace Editor.LevelEditor
{
    // cell graph -> closed loop -> ordered path, direction picked from painted arrows
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

            int arrowScore = ScoreAgainstArrows(pathA, cellsByCoord);
            var chosen = arrowScore > 0 ? pathA
                : arrowScore < 0 ? pathB
                : (SignedArea(pathA) < 0f ? pathA : pathB); // all-corner fallback

            int gateIdx = chosen.IndexOf(gateCell.Coord);
            orderedPath = chosen.Skip(gateIdx + 1).Concat(chosen.Take(gateIdx)).ToList();
            error = null;
            return true;
        }

        // +1/-1 per Straight cell vs its painted arrow; corners don't vote
        private static int ScoreAgainstArrows(List<Vector2Int> path, Dictionary<Vector2Int, ConveyorCellData> cellsByCoord)
        {
            int score = 0;
            for (int i = 0; i < path.Count; i++)
            {
                var cell = cellsByCoord[path[i]];
                if (cell.Shape != ConveyorTileShape.Straight) continue;

                var next = path[(i + 1) % path.Count];
                var actualDir = next - path[i];
                var arrowDir = Rotate(Vector2Int.right, cell.Rotation);

                if (actualDir == arrowDir) score++;
                else if (actualDir == -arrowDir) score--;
            }
            return score;
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

        // pre-rotation touched edges; corner = Left+Up at rotation 0 (matches DrawCornerTile)
        private static List<Vector2Int> ConnectedDirections(ConveyorTileShape shape, float rotation)
        {
            if (shape == ConveyorTileShape.Straight)
            {
                var dir = Rotate(Vector2Int.right, rotation);
                return new List<Vector2Int> { dir, -dir };
            }

            return new List<Vector2Int> { Rotate(Vector2Int.left, rotation), Rotate(Vector2Int.up, rotation) };
        }

        // CCW in grid space (+Y up) — matches canvas rotation once Y-down is accounted for
        private static Vector2Int Rotate(Vector2Int dir, float degrees)
        {
            int steps = Mathf.RoundToInt(degrees / 90f) % 4;
            if (steps < 0) steps += 4;
            for (int i = 0; i < steps; i++) dir = new Vector2Int(-dir.y, dir.x);
            return dir;
        }
    }
}
