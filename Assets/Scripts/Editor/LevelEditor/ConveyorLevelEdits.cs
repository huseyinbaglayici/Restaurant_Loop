using System.Linq;
using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace RestaurantLoop.Editor.LevelEditor
{
    // Undoable edits on a level draft's conveyor cells, entities and customers.
    // Methods returning bool report whether anything changed.
    internal static class ConveyorLevelEdits
    {
        public const int QueueLaneCount = 3;
        public const int DefaultGridSize = 8;

        public static ConveyorLevelDataSO CreateDraft()
        {
            var draft = ScriptableObject.CreateInstance<ConveyorLevelDataSO>();
            draft.LevelId = 1;
            for (int i = 0; i < QueueLaneCount; i++) draft.Queues.Add(new QueueLaneData());
            return draft;
        }

        public static void ResetDraft(ConveyorLevelDataSO data, int levelId)
        {
            Undo.RecordObject(data, "New Level");
            data.LevelId = levelId;
            data.Difficulty = LevelDifficulty.None;
            data.Theme = ThemeType.Asian;
            data.GridWidth = DefaultGridSize;
            data.GridHeight = DefaultGridSize;
            data.ConveyorCells.Clear();
            data.ConveyorEntities.Clear();
            data.Customers.Clear();
            data.Queues = Enumerable.Range(0, QueueLaneCount).Select(_ => new QueueLaneData()).ToList();
            data.IsTutorial = false;
            data.TutorialSteps.Clear();
        }

        public static void ResizeGrid(ConveyorLevelDataSO data, int width, int height)
        {
            Undo.RecordObject(data, "Resize Grid");
            data.GridWidth = Mathf.Max(1, width);
            data.GridHeight = Mathf.Max(1, height);
            data.ConveyorCells.RemoveAll(c => !InGrid(data, c.Coord));
            data.ConveyorEntities.RemoveAll(e => !InGrid(data, e.Coord));
            data.Customers.RemoveAll(c => !InGrid(data, c.Coord));
        }

        public static void ClearAll(ConveyorLevelDataSO data)
        {
            Undo.RecordObject(data, "Clear Conveyor");
            data.ConveyorCells.Clear();
            data.ConveyorEntities.Clear();
            data.Customers.Clear();
        }

        public static void ClearCustomers(ConveyorLevelDataSO data)
        {
            Undo.RecordObject(data, "Clear Customers");
            data.Customers.Clear();
        }

        public static void PaintCell(ConveyorLevelDataSO data, Vector2Int cell, ConveyorTileShape shape)
        {
            Undo.RecordObject(data, "Paint Conveyor Cell");
            int idx = FindCellIndex(data, cell);
            var entry = new ConveyorCellData { Coord = cell, Shape = shape, Rotation = 0f };
            if (idx >= 0) data.ConveyorCells[idx] = entry;
            else data.ConveyorCells.Add(entry);
        }

        // also drops any entity sitting on the erased belt cell
        public static void EraseCell(ConveyorLevelDataSO data, Vector2Int cell)
        {
            Undo.RecordObject(data, "Erase Conveyor Cell");
            data.ConveyorCells.RemoveAll(c => c.Coord == cell);
            data.ConveyorEntities.RemoveAll(e => e.Coord == cell);
        }

        public static bool RotateCell(ConveyorLevelDataSO data, Vector2Int cell)
        {
            int idx = FindCellIndex(data, cell);
            if (idx < 0) return false;

            Undo.RecordObject(data, "Rotate Conveyor Cell");
            var entry = data.ConveyorCells[idx];
            entry.Rotation = (entry.Rotation + 90f) % 360f;
            data.ConveyorCells[idx] = entry;
            return true;
        }

        // at most one gate per level: turning one on clears all others
        public static bool ToggleGate(ConveyorLevelDataSO data, Vector2Int cell, out bool isGateNow)
        {
            isGateNow = false;
            int idx = FindCellIndex(data, cell);
            if (idx < 0) return false;

            Undo.RecordObject(data, "Set Gate");
            isGateNow = !data.ConveyorCells[idx].IsGate;
            for (int i = 0; i < data.ConveyorCells.Count; i++)
            {
                var c = data.ConveyorCells[i];
                bool shouldBeGate = isGateNow && i == idx;
                if (c.IsGate != shouldBeGate)
                {
                    c.IsGate = shouldBeGate;
                    data.ConveyorCells[i] = c;
                }
            }
            return true;
        }

        public static void PaintEntity(ConveyorLevelDataSO data, Vector2Int cell, FoodStackData stack)
        {
            Undo.RecordObject(data, "Paint Entity");
            int idx = FindEntityIndex(data, cell);
            var entry = new ConveyorEntityPlacement { Coord = cell, Stack = stack };
            if (idx >= 0) data.ConveyorEntities[idx] = entry;
            else data.ConveyorEntities.Add(entry);
        }

        public static bool EraseEntity(ConveyorLevelDataSO data, Vector2Int cell)
        {
            if (FindEntityIndex(data, cell) < 0) return false;
            Undo.RecordObject(data, "Erase Entity");
            data.ConveyorEntities.RemoveAll(e => e.Coord == cell);
            return true;
        }

        public static void PaintCustomer(ConveyorLevelDataSO data, Vector2Int cell, FoodStackData order)
        {
            Undo.RecordObject(data, "Paint Customer");
            int idx = FindCustomerIndex(data, cell);
            if (idx >= 0)
            {
                var entry = data.Customers[idx];
                entry.Order = order;
                data.Customers[idx] = entry;
            }
            else
            {
                data.Customers.Add(new CustomerEntity { Coord = cell, Order = order });
            }
        }

        public static bool EraseCustomer(ConveyorLevelDataSO data, Vector2Int cell)
        {
            if (FindCustomerIndex(data, cell) < 0) return false;
            Undo.RecordObject(data, "Erase Customer");
            data.Customers.RemoveAll(c => c.Coord == cell);
            return true;
        }

        // an empty cell gets a new hidden customer with the given order
        public static void ToggleCustomerHidden(ConveyorLevelDataSO data, Vector2Int cell, FoodStackData orderIfNew)
        {
            int idx = FindCustomerIndex(data, cell);
            Undo.RecordObject(data, "Toggle Customer Hidden");

            if (idx < 0)
            {
                data.Customers.Add(new CustomerEntity { Coord = cell, Order = orderIfNew, IsHidden = true });
                return;
            }

            var entry = data.Customers[idx];
            entry.IsHidden = !entry.IsHidden;
            data.Customers[idx] = entry;
        }

        public static bool SetCustomerSecondOrder(ConveyorLevelDataSO data, Vector2Int cell, FoodStackData order)
        {
            int idx = FindCustomerIndex(data, cell);
            if (idx < 0) return false;

            Undo.RecordObject(data, "Set Customer 2nd Order");
            var entry = data.Customers[idx];
            entry.HasSecondOrder = true;
            entry.SecondOrder = order;
            data.Customers[idx] = entry;
            return true;
        }

        public static bool ClearCustomerSecondOrder(ConveyorLevelDataSO data, Vector2Int cell)
        {
            int idx = FindCustomerIndex(data, cell);
            if (idx < 0) return false;

            Undo.RecordObject(data, "Clear Customer 2nd Order");
            var entry = data.Customers[idx];
            entry.HasSecondOrder = false;
            entry.SecondOrder = default;
            data.Customers[idx] = entry;
            return true;
        }

        private static bool InGrid(ConveyorLevelDataSO data, Vector2Int c) =>
            c.x >= 0 && c.x < data.GridWidth && c.y >= 0 && c.y < data.GridHeight;

        private static int FindCellIndex(ConveyorLevelDataSO data, Vector2Int coord) =>
            data.ConveyorCells.FindIndex(c => c.Coord == coord);

        private static int FindEntityIndex(ConveyorLevelDataSO data, Vector2Int coord) =>
            data.ConveyorEntities.FindIndex(e => e.Coord == coord);

        private static int FindCustomerIndex(ConveyorLevelDataSO data, Vector2Int coord) =>
            data.Customers.FindIndex(c => c.Coord == coord);
    }
}
