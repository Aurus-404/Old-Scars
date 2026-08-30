using System;
using UnityEngine;
using UnityEngine.AI;

namespace OldScars.Core.Actors
{
    public enum ActorNavigationState
    {
        Idle,
        Moving,
        Reached,
        Failed
    }

    public enum ActorNavigationFailure
    {
        None,
        NotConfigured,
        InvalidRequest,
        MissingIdentity,
        Dead,
        Incapacitated,
        AgentDisabled,
        NotOnNavMesh,
        DestinationOffNavMesh,
        PathIncomplete,
        AgentRejected
    }

    public readonly struct ActorNavigationResult
    {
        public ActorNavigationResult(
            ActorNavigationState state,
            ActorNavigationFailure failure,
            string actorInstanceId,
            Vector3 requestedDestination,
            Vector3 resolvedDestination,
            string detail)
        {
            State = state;
            Failure = failure;
            ActorInstanceId = actorInstanceId;
            RequestedDestination = requestedDestination;
            ResolvedDestination = resolvedDestination;
            Detail = detail;
        }

        public ActorNavigationState State { get; }
        public ActorNavigationFailure Failure { get; }
        public string ActorInstanceId { get; }
        public Vector3 RequestedDestination { get; }
        public Vector3 ResolvedDestination { get; }
        public string Detail { get; }
        public bool Accepted => State == ActorNavigationState.Moving;
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class ActorNavigationController : MonoBehaviour
    {
        private const float MinimumArrivalTolerance = 0.05f;
        private const float DestinationValidationEpsilon = 0.05f;

        private NavMeshAgent agent;
        private ActorRuntimeIdentity identity;
        private ActorConditionComponent condition;
        private bool configured;
        private Vector3 requestedDestination;
        private Vector3 resolvedDestination;

        public ActorNavigationState State { get; private set; } = ActorNavigationState.Idle;
        public ActorNavigationFailure Failure { get; private set; } = ActorNavigationFailure.None;
        public bool IsConfigured => configured;
        public bool HasDestination { get; private set; }
        public Vector3 Destination => resolvedDestination;
        public NavMeshAgent Agent => agent != null ? agent : GetComponent<NavMeshAgent>();
        public ActorNavigationResult LastResult => Result(null);

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (State != ActorNavigationState.Moving)
                return;

            ResolveReferences();
            if (identity == null || !identity.IsRegistered)
            {
                Fail(ActorNavigationFailure.MissingIdentity, "Actor runtime identity is unavailable.");
                return;
            }
            if (identity.LifecycleState == ActorLifecycleState.Dead)
            {
                Fail(ActorNavigationFailure.Dead, "Actor became Dead while navigating.");
                return;
            }
            if (condition != null && !condition.CanPerformActiveActions)
            {
                Fail(ActorNavigationFailure.Incapacitated, "Actor became functionally incapacitated while navigating.");
                return;
            }
            if (agent == null || !agent.enabled)
            {
                Fail(ActorNavigationFailure.AgentDisabled, "NavMeshAgent became unavailable.");
                return;
            }
            if (!agent.isOnNavMesh)
            {
                Fail(ActorNavigationFailure.NotOnNavMesh, "Actor is no longer on a NavMesh.");
                return;
            }
            if (agent.pathPending)
                return;
            if (agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                Fail(ActorNavigationFailure.PathIncomplete, $"Path status changed to {agent.pathStatus}.");
                return;
            }

            float tolerance = Mathf.Max(agent.stoppingDistance, MinimumArrivalTolerance) + MinimumArrivalTolerance;
            if (float.IsInfinity(agent.remainingDistance) || agent.remainingDistance > tolerance)
                return;
            if (agent.velocity.sqrMagnitude > 0.01f)
                return;

            ResetAgentPath();
            State = ActorNavigationState.Reached;
            Failure = ActorNavigationFailure.None;
            LogTransition("REACHED", $"Destination: {Format(resolvedDestination)}");
        }

        public bool TryConfigure(float speed, float acceleration, float angularSpeed, float stoppingDistance, out string error)
        {
            error = null;
            if (!FinitePositive(speed) || !FinitePositive(acceleration) ||
                !FinitePositive(angularSpeed) || !FinitePositive(stoppingDistance))
            {
                error = "Navigation speed, acceleration, angular speed and stopping distance must be finite and positive.";
                return false;
            }

            ResolveReferences();
            if (agent == null)
            {
                error = "NavMeshAgent is missing.";
                return false;
            }

            agent.speed = speed;
            agent.acceleration = acceleration;
            agent.angularSpeed = angularSpeed;
            agent.stoppingDistance = stoppingDistance;
            agent.autoBraking = true;
            configured = true;
            Stop();
            return true;
        }

        public bool TryNavigate(Vector3 destination, out ActorNavigationResult result)
        {
            requestedDestination = destination;
            resolvedDestination = destination;
            HasDestination = false;
            ResolveReferences();

            if (!configured)
                return Reject(ActorNavigationFailure.NotConfigured, "Navigation capability was not configured by the actor profile.", out result);
            if (!Finite(destination))
                return Reject(ActorNavigationFailure.InvalidRequest, "Destination must contain finite coordinates.", out result);
            if (identity == null || !identity.IsRegistered)
                return Reject(ActorNavigationFailure.MissingIdentity, "Actor runtime identity is unavailable.", out result);
            if (identity.LifecycleState == ActorLifecycleState.Dead)
                return Reject(ActorNavigationFailure.Dead, "Dead actors cannot begin navigation.", out result);
            if (condition != null && !condition.CanPerformActiveActions)
                return Reject(ActorNavigationFailure.Incapacitated, "Functionally incapacitated actors cannot begin navigation.", out result);
            if (agent == null || !agent.enabled)
                return Reject(ActorNavigationFailure.AgentDisabled, "NavMeshAgent is disabled or missing.", out result);
            if (!agent.isOnNavMesh)
                return Reject(ActorNavigationFailure.NotOnNavMesh, "Actor is not positioned on a NavMesh.", out result);

            if (!NavMesh.SamplePosition(destination, out NavMeshHit destinationHit, DestinationValidationEpsilon, agent.areaMask))
                return Reject(ActorNavigationFailure.DestinationOffNavMesh, $"Destination is not on the NavMesh within the {DestinationValidationEpsilon:0.###}m validation epsilon.", out result);

            var path = new NavMeshPath();
            if (!agent.CalculatePath(destinationHit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                return Reject(ActorNavigationFailure.PathIncomplete, $"Calculated path is {path.status}.", out result);

            ResetAgentPath();
            resolvedDestination = destinationHit.position;
            if (!agent.SetPath(path))
                return Reject(ActorNavigationFailure.AgentRejected, "NavMeshAgent rejected the complete path.", out result);

            agent.isStopped = false;
            HasDestination = true;
            State = ActorNavigationState.Moving;
            Failure = ActorNavigationFailure.None;
            result = Result("Navigation order accepted.");
            LogTransition("MOVING", $"Requested: {Format(destination)}\n  Resolved: {Format(resolvedDestination)}");
            return true;
        }

        public void Stop()
        {
            ResolveReferences();
            ResetAgentPath();
            State = ActorNavigationState.Idle;
            Failure = ActorNavigationFailure.None;
            HasDestination = false;
            requestedDestination = transform.position;
            resolvedDestination = transform.position;
        }

        public void ApplyPersistencePose(Vector3 position, Quaternion rotation)
        {
            Stop();
            bool wasEnabled = agent != null && agent.enabled;
            if (wasEnabled)
                agent.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            if (wasEnabled)
                agent.enabled = true;
            State = ActorNavigationState.Idle;
            Failure = ActorNavigationFailure.None;
            HasDestination = false;
        }

        private bool Reject(ActorNavigationFailure failure, string detail, out ActorNavigationResult result)
        {
            Fail(failure, detail);
            result = Result(detail);
            return false;
        }

        private void Fail(ActorNavigationFailure failure, string detail)
        {
            ResetAgentPath();
            State = ActorNavigationState.Failed;
            Failure = failure;
            HasDestination = false;
            LogTransition("FAILED", $"Reason: {failure}\n  Detail: {detail}", true);
        }

        private void ResolveReferences()
        {
            if (agent == null)
                agent = GetComponent<NavMeshAgent>();
            if (identity == null)
                identity = GetComponent<ActorRuntimeIdentity>();
            if (condition == null)
                condition = GetComponent<ActorConditionComponent>();
        }

        private void ResetAgentPath()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                return;
            agent.ResetPath();
            agent.isStopped = true;
        }

        private ActorNavigationResult Result(string detail)
        {
            return new ActorNavigationResult(
                State, Failure, identity != null ? identity.ActorInstanceId : null,
                requestedDestination, resolvedDestination, detail);
        }

        private void LogTransition(string outcome, string detail, bool failure = false)
        {
            string message = "[Actors][NAVIGATION_" + outcome + "]" +
                $"\n  ActorInstanceId: {identity?.ActorInstanceId ?? "<NONE>"}" +
                $"\n  State: {State}" +
                $"\n  {detail}";
            if (failure)
                Debug.LogWarning(message);
            else
                Debug.Log(message);
        }

        private static bool FinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static string Format(Vector3 value) => $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
    }
}
