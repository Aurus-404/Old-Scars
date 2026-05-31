namespace OldScars.Core.Items
{
    public readonly struct InventoryItemUseResult
    {
        public bool Success { get; }
        public string Message { get; }

        private InventoryItemUseResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static InventoryItemUseResult Succeeded(string message)
        {
            return new InventoryItemUseResult(true, message);
        }

        public static InventoryItemUseResult Failed(string message)
        {
            return new InventoryItemUseResult(false, message);
        }
    }
}
