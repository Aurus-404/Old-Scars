using OldScars.Core.Actors;
using OldScars.Core.Items;
using UnityEngine;

namespace OldScars.Core.Visuals
{
    [DisallowMultipleComponent]
    public sealed class EquippedVisualPrefabBindings : MonoBehaviour
    {
        [SerializeField] private string visualProfileId;
        [SerializeField] private Transform attachmentRoot;
        [SerializeField] private Transform gripPrimary;
        [SerializeField] private Transform gripSecondary;
        [SerializeField] private Transform aimOrigin;
        [SerializeField] private Transform muzzle;

        public string VisualProfileId => visualProfileId;
        public Transform AttachmentRoot => attachmentRoot;
        public Transform GripPrimary => gripPrimary;
        public Transform GripSecondary => gripSecondary;
        public Transform AimOrigin => aimOrigin;
        public Transform Muzzle => muzzle;

        public void Configure(
            string profileId,
            Transform configuredAttachmentRoot,
            Transform configuredGripPrimary = null,
            Transform configuredGripSecondary = null,
            Transform configuredAimOrigin = null,
            Transform configuredMuzzle = null)
        {
            visualProfileId = profileId;
            attachmentRoot = configuredAttachmentRoot;
            gripPrimary = configuredGripPrimary;
            gripSecondary = configuredGripSecondary;
            aimOrigin = configuredAimOrigin;
            muzzle = configuredMuzzle;
        }
    }

    [DisallowMultipleComponent]
    public sealed class EquippedVisualInstanceMarker : MonoBehaviour
    {
        public string InstanceId { get; private set; }
        public string DefinitionId { get; private set; }
        public string VisualProfileId { get; private set; }
        public string RigProfileId { get; private set; }
        public string SocketId { get; private set; }
        public string SocketRole { get; private set; }

        public void Configure(
            string instanceId,
            string definitionId,
            string visualProfileId,
            string rigProfileId,
            string socketId,
            string socketRole)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            VisualProfileId = visualProfileId;
            RigProfileId = rigProfileId;
            SocketId = socketId;
            SocketRole = socketRole;
        }
    }

    public static class EquippedVisualPrefabContract
    {
        public static bool TryValidate(GameObject prefab, out string error)
        {
            error = null;
            if (prefab == null)
            {
                error = "Equipped visual prefab is null.";
                return false;
            }

            EquippedVisualPrefabBindings bindings = prefab.GetComponent<EquippedVisualPrefabBindings>();
            if (bindings == null || bindings.AttachmentRoot == null)
            {
                error = $"Equipped visual prefab '{prefab.name}' requires EquippedVisualPrefabBindings and AttachmentRoot.";
                return false;
            }
            if (prefab.GetComponentInChildren<Collider>(true) != null ||
                prefab.GetComponentInChildren<Rigidbody>(true) != null ||
                prefab.GetComponentInChildren<Joint>(true) != null ||
                prefab.GetComponentInChildren<CharacterController>(true) != null)
            {
                error = $"Equipped visual prefab '{prefab.name}' contains physics or collision components.";
                return false;
            }
            if (prefab.GetComponentInChildren<InventoryComponent>(true) != null ||
                prefab.GetComponentInChildren<ActorEquipmentComponent>(true) != null ||
                prefab.GetComponentInChildren<ActorItemOwnershipComponent>(true) != null ||
                prefab.GetComponentInChildren<WorldItemPickup>(true) != null)
            {
                error = $"Equipped visual prefab '{prefab.name}' contains gameplay components.";
                return false;
            }

            MonoBehaviour[] behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour != null && !(behaviour is EquippedVisualPrefabBindings) && !(behaviour is EquippedVisualInstanceMarker))
                {
                    error = $"Equipped visual prefab '{prefab.name}' contains unsupported behaviour '{behaviour.GetType().FullName}'.";
                    return false;
                }
            }
            return true;
        }
    }
}
