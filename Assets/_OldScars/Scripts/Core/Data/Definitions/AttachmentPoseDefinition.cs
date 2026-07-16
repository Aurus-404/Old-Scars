namespace OldScars.Core.Data.Definitions
{
    [System.Serializable]
    public sealed class AttachmentPoseDefinition
    {
        public string type; // must be "attachment_pose"
        public string id;
        public string visual_profile_id;
        public string rig_profile_id;
        public string rig_family_id;
        public string socket_id;
        public string socket_role;
        public Float3Definition local_position;
        public Float3Definition local_rotation;
        public Float3Definition local_scale;
    }

    [System.Serializable]
    public sealed class Float3Definition
    {
        public float x;
        public float y;
        public float z;
    }
}
