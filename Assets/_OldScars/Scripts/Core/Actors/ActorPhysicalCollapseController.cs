using UnityEngine;
using UnityEngine.AI;

namespace OldScars.Core.Actors
{
    /// <summary>
    /// Physical representation of an actor that is Dead or Alive+Unconscious.
    /// Lifecycle, condition and navigation remain owned by their existing components.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ActorPhysicalCollapseController : MonoBehaviour
    {
        private const float RecoverySampleDistance = 1.5f;
        private const float CollapseAngularVelocityChange = 0.35f;

        private ActorRuntimeIdentity identity;
        private ActorHealthComponent health;
        private ActorConditionComponent condition;
        private ActorNavigationController navigation;
        private NavMeshAgent agent;
        private Rigidbody body;
        private bool collapsed;
        private bool recoveryBlockedLogged;
        private float uprightYaw;

        public bool IsCollapsed => collapsed;
        public bool IsDynamic => body != null && !body.isKinematic && body.useGravity;
        public Rigidbody Body => body != null ? body : GetComponent<Rigidbody>();

        private void Awake()
        {
            ResolveReferences();
            ConfigureBody();
            SyncRepresentation();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (condition != null)
            {
                condition.FunctionalStateChanged -= OnFunctionalStateChanged;
                condition.FunctionalStateChanged += OnFunctionalStateChanged;
            }
            SyncRepresentation();
        }

        private void Start()
        {
            SyncRepresentation();
        }

        private void Update()
        {
            SyncRepresentation();
        }

        private void OnDisable()
        {
            if (condition != null)
                condition.FunctionalStateChanged -= OnFunctionalStateChanged;
        }

        private void OnFunctionalStateChanged(ActorFunctionalState previous, ActorFunctionalState next)
        {
            SyncRepresentation();
        }

        private void SyncRepresentation()
        {
            ResolveReferences();
            if (ShouldCollapse())
            {
                if (!collapsed)
                    EnterCollapse();
                return;
            }

            if (collapsed)
                TryRecoverUpright();
            else if (body != null && (!body.isKinematic || body.useGravity))
                ConfigureNormalBody();
        }

        private bool ShouldCollapse()
        {
            bool dead = identity != null
                ? identity.LifecycleState == ActorLifecycleState.Dead
                : health != null && health.IsDead;
            return dead || condition != null && condition.IsUnconscious;
        }

        private void EnterCollapse()
        {
            if (body == null || agent == null || navigation == null)
                return;

            uprightYaw = ResolveHorizontalYaw();
            navigation.Stop();
            if (agent.enabled)
                agent.enabled = false;

            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = true;
            body.detectCollisions = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.WakeUp();

            Vector3 direction = DeterministicHorizontalDirection(identity?.ActorInstanceId);
            Vector3 torqueAxis = Vector3.Cross(Vector3.up, direction).normalized;
            body.AddTorque(torqueAxis * CollapseAngularVelocityChange, ForceMode.VelocityChange);

            collapsed = true;
            recoveryBlockedLogged = false;
            Debug.Log(
                "[Actors][PHYSICAL_COLLAPSE_ENTERED]" +
                $"\n  ActorInstanceId: {identity?.ActorInstanceId ?? "<NONE>"}" +
                $"\n  Lifecycle: {identity?.LifecycleState.ToString() ?? "<NONE>"}" +
                $"\n  FunctionalState: {condition?.FunctionalState.ToString() ?? "<NONE>"}");
        }

        private bool TryRecoverUpright()
        {
            if (body == null || agent == null || navigation == null)
                return false;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;

            transform.rotation = Quaternion.Euler(0f, uprightYaw, 0f);
            Physics.SyncTransforms();

            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, RecoverySampleDistance, agent.areaMask))
            {
                LogRecoveryBlockedOnce("No nearby NavMesh position was found.");
                return false;
            }

            transform.position = hit.position;
            agent.enabled = true;
            if (!agent.Warp(hit.position))
            {
                agent.enabled = false;
                LogRecoveryBlockedOnce("NavMeshAgent rejected the sampled recovery position.");
                return false;
            }

            navigation.Stop();
            Physics.SyncTransforms();
            collapsed = false;
            recoveryBlockedLogged = false;
            Debug.Log(
                "[Actors][PHYSICAL_COLLAPSE_RECOVERED]" +
                $"\n  ActorInstanceId: {identity?.ActorInstanceId ?? "<NONE>"}" +
                $"\n  Position: ({transform.position.x:0.###}, {transform.position.y:0.###}, {transform.position.z:0.###})");
            return true;
        }

        private void ConfigureBody()
        {
            if (body == null)
                return;
            body.mass = 1f;
            body.linearDamping = 0.2f;
            body.angularDamping = 1f;
            body.detectCollisions = true;
            ConfigureNormalBody();
        }

        private void ConfigureNormalBody()
        {
            if (body == null || collapsed)
                return;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            body.detectCollisions = true;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }

        private void ResolveReferences()
        {
            if (identity == null) identity = GetComponent<ActorRuntimeIdentity>();
            if (health == null) health = GetComponent<ActorHealthComponent>();
            if (condition == null) condition = GetComponent<ActorConditionComponent>();
            if (navigation == null) navigation = GetComponent<ActorNavigationController>();
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (body == null) body = GetComponent<Rigidbody>();
        }

        private void LogRecoveryBlockedOnce(string reason)
        {
            if (recoveryBlockedLogged)
                return;
            recoveryBlockedLogged = true;
            Debug.LogWarning(
                "[Actors][PHYSICAL_COLLAPSE_RECOVERY_BLOCKED]" +
                $"\n  ActorInstanceId: {identity?.ActorInstanceId ?? "<NONE>"}" +
                $"\n  Reason: {reason}");
        }

        private static Vector3 DeterministicHorizontalDirection(string actorInstanceId)
        {
            uint hash = 2166136261u;
            string value = actorInstanceId ?? string.Empty;
            for (int index = 0; index < value.Length; index++)
            {
                hash ^= value[index];
                hash *= 16777619u;
            }

            float angle = (hash % 360u) * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            return direction.sqrMagnitude > 0.001f ? direction : Vector3.right;
        }

        private float ResolveHorizontalYaw()
        {
            Vector3 horizontalForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (horizontalForward.sqrMagnitude < 0.001f)
                horizontalForward = Vector3.ProjectOnPlane(transform.right, Vector3.up);
            return horizontalForward.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(horizontalForward.normalized, Vector3.up).eulerAngles.y
                : transform.eulerAngles.y;
        }
    }
}
