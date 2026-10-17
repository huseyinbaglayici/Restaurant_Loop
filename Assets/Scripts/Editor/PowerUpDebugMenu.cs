using System;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Save;
using UnityEditor;
using UnityEngine;

namespace RestaurantLoop.Editor
{
    public static class PowerUpDebugMenu
    {
        private const string InfinitePowerUpsPrefKey = "RestaurantLoop_InfinitePowerUps";
        private const string MenuItemPath = "RestaurantLoop/Test Mode/Infinite Power-Ups";

        [MenuItem(MenuItemPath, false, 50)]
        public static void ToggleInfinitePowerUps()
        {
            bool current = EditorPrefs.GetBool(InfinitePowerUpsPrefKey, false);
            bool newState = !current;
            EditorPrefs.SetBool(InfinitePowerUpsPrefKey, newState);
            Debug.Log($"[RestaurantLoop] Infinite Power-Ups test mode {(newState ? "ENABLED (ON)" : "DISABLED (OFF)")}.");
        }

        [MenuItem(MenuItemPath, true)]
        public static bool ToggleInfinitePowerUpsValidate()
        {
            bool isChecked = EditorPrefs.GetBool(InfinitePowerUpsPrefKey, false);
            Menu.SetChecked(MenuItemPath, isChecked);
            return true;
        }

        [MenuItem("RestaurantLoop/Test Mode/Quick Jump/Level 1", false, 10)]
        public static void SetLevel1() => SelectLevelWindow.ApplyLevel(1);

        [MenuItem("RestaurantLoop/Test Mode/Quick Jump/Level 2", false, 11)]
        public static void SetLevel2() => SelectLevelWindow.ApplyLevel(2);

        [MenuItem("RestaurantLoop/Test Mode/Quick Jump/Level 3", false, 12)]
        public static void SetLevel3() => SelectLevelWindow.ApplyLevel(3);

        [MenuItem("RestaurantLoop/Test Mode/Quick Jump/Level 4", false, 13)]
        public static void SetLevel4() => SelectLevelWindow.ApplyLevel(4);

        [MenuItem("RestaurantLoop/Test Mode/Quick Jump/Level 5", false, 14)]
        public static void SetLevel5() => SelectLevelWindow.ApplyLevel(5);

        [MenuItem("RestaurantLoop/Test Mode/Grant +10 to All Power-Ups", false, 51)]
        public static void GrantTenToAll()
        {
            var save = new SaveService();
            foreach (PowerUpButtonId id in Enum.GetValues(typeof(PowerUpButtonId)))
            {
                int updated = save.LoadPowerUpCount(id) + 10;
                save.SavePowerUpCount(id, updated);
                save.MarkPowerUpUnlockGranted(id);
                Debug.Log($"[RestaurantLoop] Granted +10 to {id}. New count: {updated}");
            }
        }

        [MenuItem("RestaurantLoop/Test Mode/Reset Power-Up Save Data", false, 52)]
        public static void ResetPowerUpSaveData()
        {
            var save = new SaveService();
            foreach (PowerUpButtonId id in Enum.GetValues(typeof(PowerUpButtonId)))
                save.ResetPowerUp(id);
            Debug.Log("[RestaurantLoop] Power-Up save data has been reset.");
        }
    }
}
