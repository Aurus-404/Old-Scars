using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Runtime-session registry for active durable item instance identities.
    /// It intentionally knows nothing about definitions, owners, storages or save data.
    /// </summary>
    public sealed class ItemInstanceIdRegistry
    {
        private const string ItemIdPrefix = "item_";
        private static readonly ItemInstanceIdRegistry instance = new ItemInstanceIdRegistry();

        [ThreadStatic] private static ItemInstanceIdReservationScope currentScope;

        private readonly HashSet<string> activeIds = new HashSet<string>(StringComparer.Ordinal);
        private int sessionThreadId;

        public static ItemInstanceIdRegistry Instance => instance;

        private ItemInstanceIdRegistry()
        {
        }

        public static bool IsValidFormat(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) || !instanceId.StartsWith(ItemIdPrefix, StringComparison.Ordinal) ||
                instanceId.Length != ItemIdPrefix.Length + 32)
            {
                return false;
            }

            for (int index = ItemIdPrefix.Length; index < instanceId.Length; index++)
            {
                char value = instanceId[index];
                bool isLowerHex = value >= '0' && value <= '9' || value >= 'a' && value <= 'f';
                if (!isLowerHex)
                    return false;
            }

            return true;
        }

        internal string ReserveNewId()
        {
            EnsureSessionThread();
            string instanceId;
            do
            {
                instanceId = ItemIdPrefix + Guid.NewGuid().ToString("N");
            }
            while (!activeIds.Add(instanceId));

            currentScope?.TrackCreated(instanceId);
            return instanceId;
        }

        internal void ReserveExact(string instanceId)
        {
            EnsureSessionThread();
            if (!IsValidFormat(instanceId))
                throw new ArgumentException($"Item instance id '{instanceId}' does not match the durable item ID format.", nameof(instanceId));
            if (!activeIds.Add(instanceId))
                throw new InvalidOperationException($"Item instance id '{instanceId}' is already active.");

            currentScope?.TrackCreated(instanceId);
        }

        internal void ReleaseFailedReservation(string instanceId)
        {
            EnsureSessionThread();
            if (string.IsNullOrWhiteSpace(instanceId))
                return;

            activeIds.Remove(instanceId);
            for (ItemInstanceIdReservationScope scope = currentScope; scope != null; scope = scope.Parent)
                scope.ForgetCreated(instanceId);
        }

        internal void RetireAfterCommit(string instanceId)
        {
            EnsureSessionThread();
            if (string.IsNullOrWhiteSpace(instanceId) || !activeIds.Contains(instanceId))
                throw new InvalidOperationException($"Cannot retire inactive item instance id '{instanceId}'.");

            if (currentScope != null)
                currentScope.TrackRetirement(instanceId);
            else
                RetireNow(instanceId);
        }

        internal bool IsActive(string instanceId)
        {
            return !string.IsNullOrWhiteSpace(instanceId) && activeIds.Contains(instanceId);
        }

        internal int ActiveCount => activeIds.Count;

        internal ItemInstanceIdReservationScope BeginReservationScope()
        {
            EnsureSessionThread();
            return new ItemInstanceIdReservationScope(this, currentScope);
        }

        internal static void ResetRuntimeSession()
        {
            currentScope = null;
            instance.sessionThreadId = Thread.CurrentThread.ManagedThreadId;
            instance.activeIds.Clear();
            ItemOwnedStorageRegistry.Instance.ResetRuntimeSession();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitializeRuntimeSession()
        {
            ResetRuntimeSession();
        }

        private void RetireNow(string instanceId)
        {
            ItemOwnedStorageRegistry.Instance.RemoveRuntimeStateForInstance(instanceId);
            if (!activeIds.Remove(instanceId))
                throw new InvalidOperationException($"Item instance id '{instanceId}' was not active at retirement.");
        }

        private void EnsureSessionThread()
        {
            int currentThreadId = Thread.CurrentThread.ManagedThreadId;
            if (sessionThreadId == 0)
                sessionThreadId = currentThreadId;
            if (currentThreadId != sessionThreadId)
                throw new InvalidOperationException("Item identity registry mutations must run on the runtime session thread.");
        }

        internal sealed class ItemInstanceIdReservationScope : IDisposable
        {
            private readonly ItemInstanceIdRegistry registry;
            private readonly List<string> createdIds = new List<string>();
            private readonly HashSet<string> retirements = new HashSet<string>(StringComparer.Ordinal);
            private readonly int threadId;
            private bool completed;

            internal ItemInstanceIdReservationScope(ItemInstanceIdRegistry registry, ItemInstanceIdReservationScope parent)
            {
                this.registry = registry;
                Parent = parent;
                threadId = Thread.CurrentThread.ManagedThreadId;
                currentScope = this;
            }

            internal ItemInstanceIdReservationScope Parent { get; }

            internal void TrackCreated(string instanceId)
            {
                createdIds.Add(instanceId);
            }

            internal void ForgetCreated(string instanceId)
            {
                createdIds.Remove(instanceId);
                retirements.Remove(instanceId);
            }

            internal void TrackRetirement(string instanceId)
            {
                retirements.Add(instanceId);
            }

            internal void Commit()
            {
                EnsureCurrent();

                if (Parent != null)
                {
                    Parent.createdIds.AddRange(createdIds);
                    Parent.retirements.UnionWith(retirements);
                }
                else
                {
                    foreach (string instanceId in retirements)
                        registry.RetireNow(instanceId);
                }

                completed = true;
                currentScope = Parent;
            }

            public void Dispose()
            {
                if (completed)
                    return;

                EnsureCurrent();
                for (int index = createdIds.Count - 1; index >= 0; index--)
                {
                    string instanceId = createdIds[index];
                    ItemOwnedStorageRegistry.Instance.RemoveRuntimeStateForInstance(instanceId);
                    registry.activeIds.Remove(instanceId);
                }

                completed = true;
                currentScope = Parent;
            }

            private void EnsureCurrent()
            {
                if (threadId != Thread.CurrentThread.ManagedThreadId)
                    throw new InvalidOperationException("Item identity reservation scopes must stay on their creating thread.");
                if (!ReferenceEquals(currentScope, this))
                    throw new InvalidOperationException("Item identity reservation scopes must complete in LIFO order.");
            }
        }
    }
}
