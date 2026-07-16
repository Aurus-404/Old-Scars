using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using UnityEngine;

namespace OldScars.Core.Visuals
{
    public readonly struct AttachmentPoseValue
    {
        public AttachmentPoseValue(Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale)
        {
            LocalPosition = localPosition;
            LocalEulerAngles = localEulerAngles;
            LocalScale = localScale;
        }

        public Vector3 LocalPosition { get; }
        public Vector3 LocalEulerAngles { get; }
        public Vector3 LocalScale { get; }
        public static AttachmentPoseValue Identity => new AttachmentPoseValue(Vector3.zero, Vector3.zero, Vector3.one);
    }

    public static class AttachmentPoseResolver
    {
        public static AttachmentPoseValue Resolve(
            GameDatabase database,
            ItemVisualProfileDefinition visualProfile,
            string rigProfileId,
            string rigFamilyId,
            string socketId,
            string socketRole)
        {
            if (database == null || visualProfile == null)
                return AttachmentPoseValue.Identity;

            AttachmentPoseDefinition best = null;
            int bestScore = int.MinValue;
            foreach (AttachmentPoseDefinition pose in database.GetAllAttachmentPoses())
            {
                int score = Score(
                    pose,
                    visualProfile.id,
                    visualProfile.persistent_pose_id,
                    rigProfileId,
                    rigFamilyId,
                    socketId,
                    socketRole);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = pose;
                }
            }

            if (best == null || bestScore < 0)
                return AttachmentPoseValue.Identity;

            return new AttachmentPoseValue(
                ToVector(best.local_position, Vector3.zero),
                ToVector(best.local_rotation, Vector3.zero),
                ToVector(best.local_scale, Vector3.one));
        }

        private static int Score(
            AttachmentPoseDefinition pose,
            string visualProfileId,
            string preferredPoseId,
            string rigProfileId,
            string rigFamilyId,
            string socketId,
            string socketRole)
        {
            if (pose == null || pose.visual_profile_id != visualProfileId)
                return -1;

            int score;
            if (!string.IsNullOrWhiteSpace(pose.rig_profile_id))
            {
                if (pose.rig_profile_id != rigProfileId)
                    return -1;
                score = 300;
            }
            else if (!string.IsNullOrWhiteSpace(pose.rig_family_id))
            {
                if (pose.rig_family_id != rigFamilyId)
                    return -1;
                score = 200;
            }
            else
            {
                score = 100;
            }

            if (!string.IsNullOrWhiteSpace(pose.socket_id))
            {
                if (pose.socket_id != socketId)
                    return -1;
                score += 20;
            }
            if (!string.IsNullOrWhiteSpace(pose.socket_role))
            {
                if (pose.socket_role != socketRole)
                    return -1;
                score += 10;
            }
            if (!string.IsNullOrWhiteSpace(preferredPoseId) && pose.id == preferredPoseId)
                score += 1000;
            return score;
        }

        private static Vector3 ToVector(Float3Definition value, Vector3 fallback)
        {
            return value != null ? new Vector3(value.x, value.y, value.z) : fallback;
        }
    }
}
