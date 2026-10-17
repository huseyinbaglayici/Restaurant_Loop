using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    [CreateAssetMenu(fileName = "TableSettings", menuName = "RestaurantLoop/Table Settings")]
    public class TableSettings : ScriptableObject
    {
        [Header("Prefab")]
        [Tooltip("The table prefab under the queue food items (e.g., yemek_alti_masa).")]
        public GameObject TablePrefab;

        [Header("Transform")]
        [Tooltip("Base position of the table in the scene.")]
        public Vector3 BasePosition = new Vector3(3.2f, -4.19f, -5.4f);

        [Tooltip("Base rotation of the table in Euler angles.")]
        public Vector3 BaseRotation = new Vector3(90f, 0f, -90f);

        [Tooltip("Base scale of the table authored for the default reference grid.")]
        public Vector3 BaseScale = new Vector3(420f, 239f, 239f);

        [Header("Exit Rack & Table Spacing")]
        [Tooltip("Z gap between the exit rack and the table.")]
        public float GapFromExitRack = 1.8f;

        [Tooltip("Table X offset (0 = centered on the exit rack and world origin).")]
        public float TableXOffset = 0f;

        [Tooltip("Z offset of the food queue on the table.")]
        public float QueueFoodZOffsetOnTable = 0f;

        [Tooltip("Extra X offset of the food queue on the table (0 = centered).")]
        public float QueueFoodXOffsetOnTable = 0f;

        [Tooltip("Local offset added to table position relative to the queue lanes center.")]
        public Vector3 QueueCenterOffset = Vector3.zero;

        [Header("Queue 2D Display")]
        [Tooltip("When enabled, food items in the queue are displayed as 2D icons with amount text, instead of 3D models.")]
        public bool Enable2DQueueVisuals = true;

        [Tooltip("Scale multiplier for the 2D sprite icon in the queue.")]
        public float Queue2DIconScale = 1.5f;

        [Tooltip("Local offset for the 2D sprite icon above the queue slot.")]
        public Vector3 Queue2DOffset = new Vector3(0f, 0.5f, 0f);

        [Header("Camera Zoom Scaling")]
        [Tooltip("If enabled, scales the table and queue food items proportionally when the camera zooms out on larger levels.")]
        public bool ScaleWithCamera = true;

        [Tooltip("Extra multiplier applied on top of the camera scale factor for the table.")]
        public float TableScaleMultiplier = 1f;

        [Tooltip("Offset compensation vector applied as the table scales with camera zoom, to prevent pivot drift on Z/Y/X.")]
        public Vector3 ZoomOffsetCompensation = Vector3.zero;

        [Tooltip("Extra multiplier applied on top of the camera scale factor for queue food items.")]
        public float FoodQueueScaleMultiplier = 1f;
    }
}
