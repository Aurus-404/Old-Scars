namespace OldScars.Core.Items
{
    public sealed class GridPlacement
    {
        public string InstanceId { get; }
        public int X { get; }
        public int Y { get; }
        public bool IsRotated { get; }
        public int EffectiveWidth { get; }
        public int EffectiveHeight { get; }

        public GridPlacement(string instanceId, int x, int y, bool isRotated, int effectiveWidth, int effectiveHeight)
        {
            InstanceId = instanceId;
            X = x;
            Y = y;
            IsRotated = isRotated;
            EffectiveWidth = effectiveWidth;
            EffectiveHeight = effectiveHeight;
        }
    }
}
