using UnityEngine;

namespace OldScars.Core
{
    public sealed class GameDataDebugTester : MonoBehaviour
    {
        private const string TestItemId = "rusted_crowbar_01";

        private void Start()
        {
            if (GameDataManager.Instance == null)
            {
                Debug.LogError("[GameDataDebugTester] GameDataManager.Instance was not found in the scene.");
                return;
            }

            if (!GameDataManager.Instance.IsReady)
            {
                Debug.LogError("[GameDataDebugTester] GameDataManager is not ready. CoreDataSystem did not finish loading successfully.");
                return;
            }

            var item = GameDataManager.Instance.Database.GetItem(TestItemId);
            if (item == null)
            {
                Debug.LogError($"[GameDataDebugTester] Item '{TestItemId}' was not found in GameDatabase.");
                return;
            }

            string itemName = item.display != null ? item.display.name : "(no display name)";
            string description = item.display != null ? item.display.description : "(no description)";
            string weight = item.physical != null ? $"{item.physical.weight_kg} kg" : "(no physical data)";
            string tags = item.tags != null && item.tags.Length > 0 ? string.Join(", ", item.tags) : "(no tags)";
            string weaponProfile = item.combat != null ? item.combat.weapon_profile : "(no combat profile)";

            Debug.Log(
                "[GameDataDebugTester] Item loaded:" +
                $"\n  Name: {itemName}" +
                $"\n  Description: {description}" +
                $"\n  Weight: {weight}" +
                $"\n  Tags: {tags}" +
                $"\n  Weapon Profile: {weaponProfile}");
        }
    }
}
