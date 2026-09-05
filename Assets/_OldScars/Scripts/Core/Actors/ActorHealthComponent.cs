using OldScars.Core.Data.Definitions;
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
        private float vitalDamageScale = 1f;
        private float bluntVitalDamageFactor = 0.35f;
        private float punctureVitalDamageFactor = 1f;
        private float lacerationVitalDamageFactor = 0.6f;
        private float headVitalDamageFactor = 1.8f;
        private float torsoVitalDamageFactor = 1f;
        private float limbVitalDamageFactor = 0.25f;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float MaxVitalIntegrity => maxHealth;
        public float VitalIntegrity => currentHealth;
        public float LowHealthThreshold => lowHealthThreshold;
        public bool IsDead => currentHealth <= 0f;

        private void Awake()
        {
            if (GetComponent<ActorMedicalStateComponent>() == null)
                gameObject.AddComponent<ActorMedicalStateComponent>();
            if (GetComponent<ActorBloodTrailEmitter>() == null)
                gameObject.AddComponent<ActorBloodTrailEmitter>();
            if (GetComponent<ActorConditionComponent>() == null)
                gameObject.AddComponent<ActorConditionComponent>();
            if (GetComponent<ActorWoundTreatmentController>() == null)
                gameObject.AddComponent<ActorWoundTreatmentController>();
            ResolveWorldObjectTags();
            ClampHealth();

            if (IsDead)
                ProcessDeath();
            else
                SyncLivingTags();
        }

        public bool ApplyDamage(float amount) => ApplyVitalDamage(amount);

        public bool ApplyVitalDamage(float amount)
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

        public bool TryConfigureVitalIntegrity(ActorProfileVitalIntegrity profile, out string failure)
        {
            if (!TryValidateVitalIntegrity(profile, out failure))
                return false;

            vitalDamageScale = profile.damage_scale;
            bluntVitalDamageFactor = profile.blunt_factor;
            punctureVitalDamageFactor = profile.puncture_factor;
            lacerationVitalDamageFactor = profile.laceration_factor;
            headVitalDamageFactor = profile.head_factor;
            torsoVitalDamageFactor = profile.torso_factor;
            limbVitalDamageFactor = profile.limb_factor;
            return true;
        }

        public float CalculateVitalDamage(BodyRegion region, WoundType woundType, float finalSeverity)
        {
            if (!Finite(finalSeverity) || finalSeverity <= 0f)
                return 0f;

            float woundFactor = woundType switch
            {
                WoundType.Blunt => bluntVitalDamageFactor,
                WoundType.Puncture => punctureVitalDamageFactor,
                WoundType.Laceration => lacerationVitalDamageFactor,
                _ => 0f
            };
            float regionFactor = region switch
            {
                BodyRegion.Head => headVitalDamageFactor,
                BodyRegion.Torso => torsoVitalDamageFactor,
                _ => limbVitalDamageFactor
            };
            return Mathf.Max(0f, Mathf.Clamp01(finalSeverity) * woundFactor * regionFactor * vitalDamageScale * maxHealth);
        }

        public static bool TryValidateVitalIntegrity(ActorProfileVitalIntegrity profile, out string failure)
        {
            if (profile == null ||
                !FinitePositive(profile.damage_scale) ||
                !FinitePositive(profile.blunt_factor) ||
                !FinitePositive(profile.puncture_factor) ||
                !FinitePositive(profile.laceration_factor) ||
                !FinitePositive(profile.head_factor) ||
                !FinitePositive(profile.torso_factor) ||
                !FinitePositive(profile.limb_factor))
            {
                failure = "Vital Integrity tuning must use finite positive damage scale, wound, and region factors.";
                return false;
            }

            failure = null;
            return true;
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
            {
                SyncLivingTags();
                GetComponent<ActorConditionComponent>()?.ResetForHealthInitialization(true);
            }
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

        private static bool FinitePositive(float value) => Finite(value) && value > 0f;

        private void ProcessDeath()
        {
            GetComponent<ActorRuntimeIdentity>()?.SetLifecycle(ActorLifecycleState.Dead);
            GetComponent<ActorConditionComponent>()?.ResetForHealthInitialization(false);
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
