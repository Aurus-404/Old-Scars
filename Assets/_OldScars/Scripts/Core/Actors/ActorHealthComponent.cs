using OldScars.Core.Interactions;
using UnityEngine;

namespace OldScars.Core.Actors
{
    [RequireComponent(typeof(ActorMedicalStateComponent))]
    public sealed class ActorHealthComponent : MonoBehaviour
    {
        public const string AliveActorTag = "alive_actor";
        public const string DamagedActorTag = "damaged_actor";
        public const string LowHealthActorTag = "low_health_actor";
        public const string DeadActorTag = "dead_actor";
        public const string LootableActorTag = "lootable_actor";

        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;
        [SerializeField] private float lowHealthThreshold = 0.25f;
        [SerializeField] private WorldObjectTags worldObjectTags;
        [SerializeField] private bool becomesLootableOnDeath = true;
        [SerializeField] private bool canHealFromZero;

        private bool deathProcessed;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float LowHealthThreshold => lowHealthThreshold;
        public bool IsDead => currentHealth <= 0f;

        private void Awake()
        {
            if (GetComponent<ActorMedicalStateComponent>() == null)
                gameObject.AddComponent<ActorMedicalStateComponent>();
            ResolveWorldObjectTags();
            ClampHealth();

            if (IsDead)
                ProcessDeath();
            else
                SyncLivingTags();
        }

        public bool ApplyDamage(float amount)
        {
            if (!Finite(amount) || amount <= 0f || IsDead)
                return false;

            float previousHealth = currentHealth;
            currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);

            if (IsDead)
                ProcessDeath();
            else
                SyncLivingTags();

            return currentHealth < previousHealth;
        }

        public bool Heal(float amount)
        {
            if (!Finite(amount) || amount <= 0f || (IsDead && !canHealFromZero))
                return false;

            float previousHealth = currentHealth;
            currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);

            if (currentHealth > 0f)
                deathProcessed = false;

            SyncLivingTags();
            return currentHealth > previousHealth;
        }

        public bool CanHeal(float amount)
        {
            return Finite(amount) && amount > 0f && currentHealth < maxHealth && (!IsDead || canHealFromZero);
        }

        public void Kill()
        {
            currentHealth = 0f;
            ProcessDeath();
        }

        public void ApplyInitialHealth(float newMaxHealth, float newCurrentHealth)
        {
            maxHealth = Mathf.Max(1f, newMaxHealth);
            currentHealth = Mathf.Clamp(newCurrentHealth, 0f, maxHealth);
            deathProcessed = false;

            if (IsDead)
                ProcessDeath();
            else
                SyncLivingTags();
        }

        private void ResolveWorldObjectTags()
        {
            if (worldObjectTags == null)
                worldObjectTags = GetComponent<WorldObjectTags>();
        }

        private void ClampHealth()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            lowHealthThreshold = Mathf.Clamp01(lowHealthThreshold);
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }

        private void SyncLivingTags()
        {
            GetComponent<ActorRuntimeIdentity>()?.SetLifecycle(ActorLifecycleState.Alive);
            ResolveWorldObjectTags();
            if (worldObjectTags == null)
                return;

            worldObjectTags.RemoveTag(DeadActorTag);
            worldObjectTags.RemoveTag(LootableActorTag);
            worldObjectTags.AddTag(AliveActorTag);

            if (currentHealth < maxHealth)
                worldObjectTags.AddTag(DamagedActorTag);
            else
                worldObjectTags.RemoveTag(DamagedActorTag);

            if (IsLowHealth())
                worldObjectTags.AddTag(LowHealthActorTag);
            else
                worldObjectTags.RemoveTag(LowHealthActorTag);
        }

        private bool IsLowHealth()
        {
            return maxHealth > 0f && currentHealth / maxHealth <= lowHealthThreshold;
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void ProcessDeath()
        {
            GetComponent<ActorRuntimeIdentity>()?.SetLifecycle(ActorLifecycleState.Dead);
            ResolveWorldObjectTags();
            if (deathProcessed)
                return;

            deathProcessed = true;

            if (worldObjectTags == null)
                return;

            worldObjectTags.RemoveTag(AliveActorTag);
            worldObjectTags.RemoveTag(DamagedActorTag);
            worldObjectTags.RemoveTag(LowHealthActorTag);
            worldObjectTags.AddTag(DeadActorTag);

            if (becomesLootableOnDeath)
                worldObjectTags.AddTag(LootableActorTag);
            else
                worldObjectTags.RemoveTag(LootableActorTag);
        }
    }
}
