namespace OldScars.Core.Items
{
    public interface IItemOwnedStorageResolver
    {
        bool TryResolveOwnedStorage(string containerInstanceId, out ItemOwnedStorageRuntime storage);
        bool TryResolveRootOwner(string instanceId, out object rootOwner, out string error);
    }

    internal interface IGridStorageIncomingGuard
    {
        bool CanAcceptIncoming(ItemStorageEntry entry, int quantity, out string reason);
    }
}
