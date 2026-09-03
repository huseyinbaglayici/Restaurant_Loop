using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
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
        Kebab,
        Steak
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
            FoodType.Steak => new Color(0.50f, 0.22f, 0.20f),
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
            FoodType.Steak => "\U0001F969",
            _ => "?"
        };
    }

    [System.Serializable]
    public struct FoodStackData
    {
        public FoodType Type;
        public int Amount;

        // 0 = unlinked
        public int LinkId;
    }

    [System.Serializable]
    public struct ConveyorEntityPlacement
    {
        public Vector2Int Coord;
        public FoodStackData Stack;
    }

    [System.Serializable]
    public struct CustomerEntity
    {
        public Vector2Int Coord;
        public FoodStackData Order;
        public bool IsHidden;
        public bool HasSecondOrder;
        public FoodStackData SecondOrder;
    }

    [System.Serializable]
    public class QueueLaneData
    {
        // index 0 = front
        public List<FoodStackData> Entries = new();
    }

    [System.Serializable]
    public class ConveyorPathData
    {
        public List<Vector2Int> OrderedPath = new();
    }

    public enum LevelDifficulty { None, Hard, SuperHard, Bonus }

    [CreateAssetMenu(menuName = "RestaurantLoop/Conveyor Level Data", fileName = "ConveyorLevel")]
    public class ConveyorLevelDataSO : ScriptableObject
    {
        public int LevelId;
        public LevelDifficulty Difficulty;
        public ThemeType Theme = ThemeType.Asian;

        [Header("Conveyor Grid")]
        public int GridWidth = 8;
        public int GridHeight = 8;
        public List<ConveyorCellData> ConveyorCells = new();
        public List<ConveyorEntityPlacement> ConveyorEntities = new();

        [Header("Baked at Save — do not hand-edit")]
        public ConveyorPathData BakedPath = new();

        [Header("Customers — sit inside the loop, each wanting one food stack (demand)")]
        public List<CustomerEntity> Customers = new();

        [Header("Queue — fixed supply lanes feeding the belt (always present)")]
        public List<QueueLaneData> Queues = new();

        [Header("Tutorial")]
        public bool IsTutorial;
        public List<TutorialStepData> TutorialSteps = new();
    }
}
