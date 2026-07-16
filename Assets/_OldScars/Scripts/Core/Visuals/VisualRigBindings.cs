using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace OldScars.Core.Visuals
{
    [Serializable]
    public sealed class VisualPartBinding
    {
        [SerializeField] private string partId;
        [SerializeField] private Transform target;

        public VisualPartBinding(string partId, Transform target)
        {
            this.partId = partId;
            this.target = target;
        }

        public string PartId => partId;
        public Transform Target => target;
    }

    [Serializable]
    public sealed class VisualSocketBinding
    {
        [SerializeField] private string socketId;
        [SerializeField] private Transform target;

        public VisualSocketBinding(string socketId, Transform target)
        {
            this.socketId = socketId;
            this.target = target;
        }

        public string SocketId => socketId;
        public Transform Target => target;
    }

    public readonly struct VisualSocketResolution
    {
        public VisualSocketResolution(string socketId, string role, string partId, Transform target)
        {
            SocketId = socketId;
            Role = role;
            PartId = partId;
            Target = target;
        }

        public string SocketId { get; }
        public string Role { get; }
        public string PartId { get; }
        public Transform Target { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(SocketId) && Target != null;
    }

    public sealed class VisualRigAvailabilityChangedEventArgs : EventArgs
    {
        private readonly ReadOnlyCollection<string> affectedSocketIds;

        public VisualRigAvailabilityChangedEventArgs(string partId, IReadOnlyList<string> socketIds)
        {
            PartId = partId;
            var copy = new string[socketIds != null ? socketIds.Count : 0];
            for (int index = 0; index < copy.Length; index++)
                copy[index] = socketIds[index];
            affectedSocketIds = Array.AsReadOnly(copy);
        }

        public string PartId { get; }
        public IReadOnlyList<string> AffectedSocketIds => affectedSocketIds;
    }
}
