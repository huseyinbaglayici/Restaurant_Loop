using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    /// <summary>The dish types customers can order — what rides the conveyor and fills the queue.</summary>
    public enum FoodType
    {
        Hamburger,
        Fries,
        Meat,
        Drink,
        Donut,
        Sushi,
        Pizza,
        Croissant,
        Kebab
    }

    public static class FoodTypeUtil
    {
        public static Color ToColor(FoodType t) => t switch
        {
            FoodType.Hamburger => new Color(0.72f, 0.45f, 0.20f),
            FoodType.Fries => new Color(0.95f, 0.75f, 0.20f),
            FoodType.Meat => new Color(0.65f, 0.20f, 0.18f),
            FoodType.Drink => new Color(0.20f, 0.55f, 0.90f),
            FoodType.Donut => new Color(0.90f, 0.55f, 0.70f),
            FoodType.Sushi => new Color(0.85f, 0.85f, 0.85f),
            FoodType.Pizza => new Color(0.85f, 0.35f, 0.15f),
            FoodType.Croissant => new Color(0.80f, 0.65f, 0.30f),
            FoodType.Kebab => new Color(0.55f, 0.30f, 0.15f),
            _ => Color.gray
        };

        public static string ToIcon(FoodType t) => t switch
        {
            FoodType.Hamburger => "\U0001F354",
            FoodType.Fries => "\U0001F35F",
            FoodType.Meat => "\U0001F969",
            FoodType.Drink => "\U0001F964",
            FoodType.Donut => "\U0001F369",
            FoodType.Sushi => "\U0001F363",
            FoodType.Pizza => "\U0001F355",
            FoodType.Croissant => "\U0001F950",
            FoodType.Kebab => "\U0001F959",
            _ => "?"
        };
    }

    [System.Serializable]
    public struct FoodStackData
    {
        public FoodType Type;
        public int Amount;
    }

    /// <summary>An entity (food stack) riding a specific cell of the conveyor.</summary>
    [System.Serializable]
    public struct ConveyorEntityPlacement
    {
        public Vector2Int Coord;
        public FoodStackData Stack;
    }

    /// <summary>A table sitting inside the conveyor loop, with its own order queue.</summary>
    [System.Serializable]
    public class TableData
    {
        public Vector2Int Coord;

        // Index 0 = the order currently being requested — the only one servable in-game;
        // the rest are waiting behind it.
        public List<FoodStackData> Orders = new();
    }

    /// <summary>A supply lane feeding food stacks onto the belt — separate from table demand.</summary>
    [System.Serializable]
    public class QueueLaneData
    {
        // Index 0 = front of the queue (closest to the belt) — the only entry that's
        // clickable/servable in-game; the rest are waiting behind it.
        public List<FoodStackData> Entries = new();
    }

    /// <summary>
    /// The belt's actual travel order, baked from ConveyorCells + the Gate cell when the level is
    /// saved. Index 0 = entry (fed by the Queue), last = exit (served/removed here). Gameplay code
    /// only ever reads this — it never re-derives connectivity from Shape/Rotation itself.
    /// </summary>
    [System.Serializable]
    public class ConveyorPathData
    {
        public List<Vector2Int> OrderedPath = new();
    }

    [CreateAssetMenu(menuName = "RestaurantLoop/Conveyor Level Data", fileName = "ConveyorLevel")]
    public class ConveyorLevelDataSO : ScriptableObject
    {
        public int LevelId;

        [Header("Conveyor Grid")]
        public int GridWidth = 8;
        public int GridHeight = 8;
        public List<ConveyorCellData> ConveyorCells = new();
        public List<ConveyorEntityPlacement> ConveyorEntities = new();

        [Header("Baked at Save — do not hand-edit")]
        public ConveyorPathData BakedPath = new();

        [Header("Tables — sit inside the loop, each with its own order queue (demand)")]
        public List<TableData> Tables = new();

        [Header("Queue — fixed supply lanes feeding the belt (always present)")]
        public List<QueueLaneData> Queues = new();
    }
}
