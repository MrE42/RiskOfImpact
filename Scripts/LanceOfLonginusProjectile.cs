using RoR2;
using RoR2.Projectile;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace RiskOfImpact
{
    [RequireComponent(typeof(ProjectileController))]
    [RequireComponent(typeof(ProjectileDamage))]
    [RequireComponent(typeof(TeamFilter))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class LanceOfLonginusProjectile : MonoBehaviour
    {
        [Header("Behavior")]
        public bool pierceEnemies = true;
        public int maxEnemyHits = 999;
        public float pickupDelaySeconds = 0.35f;
        public float lifetimeSeconds = 600f;

        [Tooltip("If the lance spawns intersecting ground/walls, it can instantly stick. This gives it a grace period.")]
        public float stickArmTime = 0.15f;

        [Header("Reliability (anti-tunneling)")]
        [Tooltip("Layer mask used by the per-tick sweep. Leave as Everything unless you know what you're doing.")]
        public LayerMask sweepMask = ~0;

        [Tooltip("SphereCast radius is derived from the projectile collider bounds extents, multiplied by this.")]
        public float sweepRadiusScale = 0.55f;

        [Tooltip("Minimum sphere sweep radius (in meters).")]
        public float minSweepRadius = 0.18f;

        [Header("Debug")]
        public bool debugHits = true;
        public bool verboseDamageDebug = true;
        public float debugInterval = 0.25f;
        
        [Header("Sweep radii")]
        public float enemySweepRadiusScale = 0.75f; // bigger hit detection
        public float worldSweepRadiusScale = 0.35f; // smaller wall/ground detection
        public float minEnemySweepRadius = 0.30f;
        public float minWorldSweepRadius = 0.12f;


        private ProjectileController controller;
        private ProjectileDamage projectileDamage;
        private TeamFilter teamFilter;
        private Rigidbody rb;
        private Collider hitCollider;

        private bool stuck;
        private int hitCount;
        private readonly HashSet<HealthComponent> hitHealthComponents = new HashSet<HealthComponent>();

        private float spawnTime;
        private Vector3 spawnPos;
        private Vector3 lastPos;
        private float nextDebugTime;

        private const float FallbackSpeed = 200f;

        private void Awake()
        {
            spawnTime = Time.time;
            spawnPos = transform.position;

            controller = GetComponent<ProjectileController>();
            projectileDamage = GetComponent<ProjectileDamage>();
            teamFilter = GetComponent<TeamFilter>();
            rb = GetComponent<Rigidbody>();
            hitCollider = GetComponent<Collider>();

            // HurtBoxes are typically triggers; using a trigger collider avoids a bunch of missed hits.
            // But triggers can still tunnel at high speeds, so we ALSO do a SphereCast sweep each FixedUpdate.
            hitCollider.isTrigger = true;

            rb.useGravity = false;
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.None;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            lastPos = rb.position;

            if (debugHits)
            {
                Debug.Log($"[LanceProj] Awake netActive={NetworkServer.active} layer={gameObject.layer} " +
                          $"sweepMask=0x{(int)sweepMask.value:X} trigger={hitCollider.isTrigger} pos={transform.position}");
            }

            Destroy(gameObject, lifetimeSeconds);
        }

        private void Start()
        {
            // If ProjectileSimple is on the prefab, it can override velocity each tick.
            var ps = GetComponent<ProjectileSimple>();
            if (ps) ps.enabled = false;

            // IMPORTANT: your spawn rotation makes UP = aim direction in your setup.
            if (rb && rb.velocity.sqrMagnitude <= 0.01f)
                rb.velocity = transform.up * FallbackSpeed;

            if (debugHits)
            {
                Debug.Log($"[LanceProj] Start vel={rb.velocity} speed={rb.velocity.magnitude:F1} " +
                          $"up={transform.up} owner={(controller && controller.owner ? controller.owner.name : "null")}");
            }
        }

        private void FixedUpdate()
        {
            if (NetworkServer.active)
                SweepForMissedHits();

            if (debugHits && Time.time >= nextDebugTime)
            {
                nextDebugTime = Time.time + debugInterval;
                Debug.Log($"[LanceProj] Tick pos={transform.position} rbPos={(rb ? rb.position.ToString() : "noRB")} " +
                          $"vel={(rb ? rb.velocity.ToString() : "noRB")} stuck={stuck}");
            }

            if (!stuck && rb && rb.velocity.sqrMagnitude > 0.01f)
            {
                // Your model is oriented "up", so add +90deg pitch after LookRotation.
                transform.rotation = Quaternion.LookRotation(rb.velocity.normalized) * Quaternion.Euler(90f, 0f, 0f);
            }

            lastPos = rb ? rb.position : transform.position;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (stuck) return;
            if (!other || other == hitCollider) return;

            // Ignore overlaps with the owner (capsule, feet, etc.)
            if (controller && controller.owner && other.transform.IsChildOf(controller.owner.transform))
                return;

            // No reliable contact point for triggers; using current position is fine for VFX/proc events.
            Vector3 hitPos = rb ? rb.position : transform.position;

            // Try damage first
            if (TryDealDamage(other, hitPos, "TRIGGER"))
            {
                if (!pierceEnemies)
                    Destroy(gameObject);
                return;
            }

            // Stick only to world geometry (not characters)
            bool isCharacterCollider = other.GetComponentInParent<CharacterBody>() || other.GetComponentInParent<HealthComponent>();
            if (!other.isTrigger && !isCharacterCollider)
            {
                if (Time.time - spawnTime < stickArmTime) return;
                if ((rb.position - spawnPos).sqrMagnitude < 0.25f * 0.25f) return;

                if (debugHits) Debug.Log($"[LanceProj] TRIGGER_STICK other={other.name}");
                StickIntoWorld();
            }
        }

        /// <summary>
        /// Trigger callbacks can miss at high velocity. This per-tick sweep bridges the gaps by checking
        /// the segment traveled since last FixedUpdate.
        /// 
        /// Modified: enemy sweep radius can be larger than world sweep radius.
        /// </summary>
        private void SweepForMissedHits()
        {
            if (stuck || rb == null) return;

            Vector3 from = lastPos;
            Vector3 to = rb.position;
            Vector3 delta = to - from;

            float dist = delta.magnitude;
            if (dist < 0.001f) return;

            // Base extent from our collider bounds (used to derive both radii)
            float baseExtent = 0.25f;
            if (hitCollider)
            {
                var e = hitCollider.bounds.extents;
                baseExtent = Mathf.Max(e.x, Mathf.Max(e.y, e.z));
            }

            float enemyRadius = Mathf.Max(minEnemySweepRadius, baseExtent * enemySweepRadiusScale);
            float worldRadius = Mathf.Max(minWorldSweepRadius, baseExtent * worldSweepRadiusScale);

            Vector3 dir = delta / dist;

            // ---------- PASS 1: ENEMY HIT SWEEP (larger) ----------
            RaycastHit[] enemyHits = Physics.SphereCastAll(from, enemyRadius, dir, dist, sweepMask, QueryTriggerInteraction.Collide);
            if (enemyHits != null && enemyHits.Length > 0)
            {
                Array.Sort(enemyHits, (a, b) => a.distance.CompareTo(b.distance));

                if (debugHits)
                    Debug.Log($"[LanceProj] SWEEP_ENEMY from={from} to={to} dist={dist:F3} r={enemyRadius:F3} hits={enemyHits.Length}");

                foreach (var h in enemyHits)
                {
                    Collider other = h.collider;
                    if (!other) continue;
                    if (other == hitCollider) continue;

                    // Same owner ignore
                    if (controller && controller.owner && other.transform.IsChildOf(controller.owner.transform))
                        continue;

                    // Damage only (this is your "bigger enemy hit collider")
                    if (TryDealDamage(other, h.point, "SWEEP_ENEMY"))
                    {
                        if (debugHits) Debug.Log($"[LanceProj] SWEEP_ENEMY_DAMAGE other={other.name} point={h.point} dist={h.distance:F3}");
                        if (!pierceEnemies)
                        {
                            Destroy(gameObject);
                            return;
                        }
                    }
                }
            }

            // ---------- PASS 2: WORLD STICK SWEEP (smaller) ----------
            RaycastHit[] worldHits = Physics.SphereCastAll(from, worldRadius, dir, dist, sweepMask, QueryTriggerInteraction.Collide);
            if (worldHits == null || worldHits.Length == 0) return;

            Array.Sort(worldHits, (a, b) => a.distance.CompareTo(b.distance));

            if (debugHits)
                Debug.Log($"[LanceProj] SWEEP_WORLD from={from} to={to} dist={dist:F3} r={worldRadius:F3} hits={worldHits.Length}");

            foreach (var h in worldHits)
            {
                Collider other = h.collider;
                if (!other) continue;
                if (other == hitCollider) continue;

                // Same owner ignore
                if (controller && controller.owner && other.transform.IsChildOf(controller.owner.transform))
                    continue;

                // Stick only to world geometry (not characters)
                bool isCharacterCollider = other.GetComponentInParent<CharacterBody>() || other.GetComponentInParent<HealthComponent>();
                if (!other.isTrigger && !isCharacterCollider)
                {
                    if (Time.time - spawnTime < stickArmTime) continue;
                    if ((rb.position - spawnPos).sqrMagnitude < 0.25f * 0.25f) continue;

                    if (debugHits) Debug.Log($"[LanceProj] SWEEP_WORLD_STICK other={other.name} point={h.point} dist={h.distance:F3}");
                    StickIntoWorld();
                    return;
                }
            }
        }


        private bool TryDealDamage(Collider other, Vector3 hitPosition, string srcTag)
        {
            if (!NetworkServer.active) return false;

            // IMPORTANT: HurtBox -> HurtBox.healthComponent is the reliable way.
            HurtBox hurtBox = other.GetComponent<HurtBox>() ?? other.GetComponentInParent<HurtBox>();
            HealthComponent hc = hurtBox ? hurtBox.healthComponent : other.GetComponentInParent<HealthComponent>();

            if (!hc)
            {
                if (debugHits && verboseDamageDebug)
                    Debug.Log($"[LanceProj] {srcTag}_NO_HC other={other.name} layer={other.gameObject.layer} trig={other.isTrigger}");
                return false;
            }

            // Don't hit owner
            if (controller && controller.owner && hc.gameObject == controller.owner)
            {
                if (debugHits && verboseDamageDebug)
                    Debug.Log($"[LanceProj] {srcTag}_SKIP_OWNER hc={hc.name} other={other.name}");
                return false;
            }

            // Team check
            TeamIndex victimTeam = hurtBox
                ? hurtBox.teamIndex
                : (hc.body && hc.body.teamComponent ? hc.body.teamComponent.teamIndex : TeamComponent.GetObjectTeam(hc.gameObject));

            TeamIndex myTeam = teamFilter
                ? teamFilter.teamIndex
                : (controller && controller.owner ? TeamComponent.GetObjectTeam(controller.owner) : TeamIndex.None);

            if (victimTeam == myTeam)
            {
                if (debugHits && verboseDamageDebug)
                    Debug.Log($"[LanceProj] {srcTag}_SKIP_SAME_TEAM victimTeam={victimTeam} myTeam={myTeam} hc={hc.name}");
                return false;
            }

            if (hitCount >= maxEnemyHits)
            {
                if (debugHits && verboseDamageDebug)
                    Debug.Log($"[LanceProj] {srcTag}_MAX_HITS_REACHED hc={hc.name}");
                return true; // treat as handled so we don't stick on enemies when capped
            }

            // Per-target dedupe (prevents multi-hit spam from multiple colliders/hurtboxes)
            HealthComponent key = hurtBox && hurtBox.healthComponent ? hurtBox.healthComponent : hc;
            if (key && hitHealthComponents.Contains(key))
            {
                if (debugHits && verboseDamageDebug)
                    Debug.Log($"[LanceProj] {srcTag}_ALREADY_HIT hc={key.name} other={other.name}");
                return true;
            }
            if (key) hitHealthComponents.Add(key);

            hitCount++;

            DamageInfo di = new DamageInfo
            {
                attacker = controller ? controller.owner : null,
                inflictor = gameObject,
                damage = projectileDamage ? projectileDamage.damage : 0f,
                crit = projectileDamage && projectileDamage.crit,
                position = hitPosition,
                force = rb ? rb.velocity : Vector3.zero,
                procCoefficient = controller ? controller.procCoefficient : 1f,
                damageType = DamageType.Generic,
                damageColorIndex = DamageColorIndex.Default
            };

            if (debugHits && verboseDamageDebug)
            {
                Debug.Log($"[LanceProj] {srcTag}_DAMAGE hc={hc.name} body={(hc.body ? hc.body.name : "none")} " +
                          $"victimTeam={victimTeam} myTeam={myTeam} dmg={di.damage:F1} pos={hitPosition} vel={(rb ? rb.velocity.ToString() : "noRB")}");
            }

            hc.TakeDamage(di);
            GlobalEventManager.instance.OnHitEnemy(di, hc.gameObject);
            GlobalEventManager.instance.OnHitAll(di, hc.gameObject);

            return true;
        }

        private void StickIntoWorld()
        {
            if (stuck) return;
            stuck = true;

            if (debugHits)
                Debug.Log($"[LanceProj] STUCK pos={transform.position} vel={(rb ? rb.velocity.ToString() : "noRB")}");

            if (rb)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            // Stop any further projectile-damage semantics
            if (projectileDamage) projectileDamage.enabled = false;

            // Make pickup easier: enlarge collider a bit once stuck
            if (hitCollider is BoxCollider bc)
            {
                bc.size *= 3f;
            }

            // Add pickup trigger
            if (!GetComponent<LancePickupTrigger>())
            {
                var pickup = gameObject.AddComponent<LancePickupTrigger>();
                pickup.lanceEquipmentDef = RiskOfImpactContent.GetLanceEquipmentDef();
                pickup.pickupActivationTime = Time.time + pickupDelaySeconds;
            }
        }
    }
}
