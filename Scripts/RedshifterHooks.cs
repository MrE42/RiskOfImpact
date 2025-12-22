using System;
using System.Collections.Generic;
using HG;
using LookingGlass.ItemStatsNameSpace;
using EntityStates;
using RoR2;
using RoR2.Orbs;
using RoR2.Items;
using System.Reflection;
using RoR2.Projectile;
using UnityEngine;
using static RiskOfImpact.RiskOfImpactMain;

namespace RiskOfImpact
{
    /// <summary>
    /// Hooks that implement Redshifter:
    ///  - Scales the radius of BlastAttacks (Gasoline, Wisp, Behemoth, etc.)
    ///  - Scales the range of LightningOrb chains (Ukulele / Tesla style)
    ///  - Extends Focus Crystal's effective radius
    /// </summary>
    public static class RedshifterHooks
    {
        
        [ThreadStatic] private static bool _pendingIceRingExplosionScale;
        [ThreadStatic] private static float _pendingIceRingExplosionMult;

        private class FocusIndicatorTracker : MonoBehaviour
        {
            public Vector3 baseScale;
        }
        
        private class MiredUrnRadiusTracker : MonoBehaviour
        {
            public float baseRadius;
        }
        
        private sealed class RedshifterScaleTracker : MonoBehaviour
        {
            public Vector3 baseScale;
        }


        // +50% radius per Redshifter stack. Tweak to taste.
        private const float RadiusBonusPerItem = 0.5f;

        public static void Init()
        {
            LogInfo("[Redshifter] Initializing hooks...");

            On.RoR2.BlastAttack.Fire += BlastAttack_Fire;
            On.RoR2.GlobalEventManager.ProcIgniteOnKill += GlobalEventManager_ProcIgniteOnKill;
            On.RoR2.Orbs.LightningOrb.Begin += LightningOrb_Begin;
            On.RoR2.GlobalEventManager.OnHitEnemy += GlobalEventManager_OnHitEnemy;
            On.RoR2.Items.MushroomBodyBehavior.FixedUpdate += MushroomBodyBehavior_FixedUpdate;
            // Focus Crystal
            On.RoR2.Items.NearbyDamageBonusBodyBehavior.OnEnable += NearbyDamageBonusBodyBehavior_OnEnable;
            CharacterBody.onBodyInventoryChangedGlobal += OnBodyInventoryChanged;
            // Bolstering Lantern
            On.RoR2.AttackSpeedPerNearbyCollider.UpdateValues += AttackSpeedPerNearbyCollider_UpdateValues;
            // Warbanner
            On.RoR2.Items.WardOnLevelManager.OnCharacterLevelUp += WardOnLevelManager_OnCharacterLevelUp;
            // Interstellar Desk Plant
            On.RoR2.DeskPlantController.MainState.OnEnter += DeskPlantMainState_OnEnter;
            // Frost Relic
            On.RoR2.IcicleAuraController.UpdateRadius += IcicleAuraController_UpdateRadius;
            // Mired Urn
            On.RoR2.Items.SiphonOnLowHealthItemBodyBehavior.OnEnable += SiphonOnLowHealthItemBodyBehavior_OnEnable;
            // Noxious Thorn
            On.RoR2.CharacterBody.TriggerEnemyDebuffs += CharacterBody_TriggerEnemyDebuffs;
            // Faraday Spur
            On.RoR2.Items.JumpDamageStrikeBodyBehavior.GetRadius += JumpDamageStrikeBodyBehavior_GetRadius;
            // Kinetic Dampener (effect only)
            On.RoR2.EffectManager.SpawnEffect_GameObject_EffectData_bool += EffectManager_SpawnEffect_GameObject;
            // Faulty Conductor
            On.RoR2.Items.DroneShockDamageBodyBehavior.Start          += DroneShockDamageBodyBehavior_Start;
            On.RoR2.Items.DroneShockDamageBodyBehavior.TriggerEnergize+= DroneShockDamageBodyBehavior_TriggerEnergize;
            // Bands
            On.RoR2.Projectile.ProjectileController.Start += ProjectileController_Start;
            On.RoR2.Projectile.ProjectileGhostController.Start += ProjectileGhostController_Start;
            // Mercurial Rachis
            On.RoR2.Items.RandomDamageZoneBodyBehavior.FixedUpdate += RandomDamageZoneBodyBehavior_FixedUpdate;
            // Teleporter / holdout zones
            On.RoR2.HoldoutZoneController.Awake += HoldoutZoneController_Awake;

            // Other holdout-style zones that also expose calcRadius
            On.RoR2.KillZoneController.Awake += KillZoneController_Awake;
        }

        private static ItemDef RedshifterItemDef => RiskOfImpactContent.GetRedshifterItemDef();

        /// <summary>
        /// Returns the radius multiplier for a body with Redshifter.
        /// 1.0 = no change, 1.5 = +50%, 2.0 = +100%, etc.
        /// </summary>
        private static float GetRadiusMultiplier(CharacterBody body)
        {
            if (!body || body.inventory == null) return 1f;

            int count = body.inventory.GetItemCountEffective(RedshifterItemDef);
            if (count <= 0) return 1f;

            float mult = 1f + RadiusBonusPerItem * count;
            //LogDebug($"[Redshifter] radius mult for {body.GetDisplayName()} (stacks={count}) = {mult}");
            return mult;
        }

        #region BlastAttack radius (Gasoline, Wisp, Behemoth, etc.)

        private static BlastAttack.Result BlastAttack_Fire(On.RoR2.BlastAttack.orig_Fire orig, BlastAttack self)
        {
            if (self.attacker)
            {
                var body = self.attacker.GetComponent<CharacterBody>();
                if (body)
                {
                    // Skip Frost Relic aura blasts – their radius is already scaled in IcicleAuraController_UpdateRadius
                    bool isFrostRelicAura = self.inflictor &&
                                            self.inflictor.GetComponent<IcicleAuraController>() != null;
                    
                    // Skip Faraday Spur discharge blast: its radius already comes from GetRadius(), which we scale.
                    bool isFaradayDischarge = false;
                    var faraday = body.GetComponent<RoR2.Items.JumpDamageStrikeBodyBehavior>();
                    if (faraday)
                    {
                        int charge = body.GetBuffCount(DLC3Content.Buffs.JumpDamageStrikeCharge);
                        if (charge >= 25)
                        {
                            // If you've hooked GetRadius, this returns the *already scaled* radius (perfect).
                            float expectedRadius = faraday.GetRadius(charge, faraday.stack);

                            // Match the "signature" of the Faraday discharge blast.
                            if (Mathf.Abs(self.radius - expectedRadius) < 0.01f &&
                                self.procCoefficient == 1f &&
                                self.damageColorIndex == DamageColorIndex.Item &&
                                self.attackerFiltering == AttackerFiltering.NeverHitSelf &&
                                self.falloffModel == BlastAttack.FalloffModel.None &&
                                (self.damageType.damageTypeExtended & DamageTypeExtended.Electrical) != 0)
                            {
                                isFaradayDischarge = true;
                            }
                        }
                    }

                    if (!isFrostRelicAura && !isFaradayDischarge && self.radius > 0f)
                    {
                        float mult = GetRadiusMultiplier(body);
                        if (mult != 1f)
                        {
                            float oldRadius = self.radius;
                            self.radius *= mult;
                            LogDebug($"[Redshifter] BlastAttack: radius {oldRadius:F1} -> {self.radius:F1} on {body.GetDisplayName()}");
                        }
                    }
                }
            }

            return orig(self);
        }

        private static void GlobalEventManager_ProcIgniteOnKill(
            On.RoR2.GlobalEventManager.orig_ProcIgniteOnKill orig,
            DamageReport damageReport,
            int igniteOnKillCount,
            CharacterBody victimBody,
            TeamIndex attackerTeamIndex)
        {
            var attackerBody = damageReport.attackerBody;
            if (!attackerBody || !victimBody)
            {
                // Fallback to vanilla if something is weird
                orig(damageReport, igniteOnKillCount, victimBody, attackerTeamIndex);
                return;
            }

            float mult = GetRadiusMultiplier(attackerBody);

            // If no Redshifter, just run vanilla.
            if (mult == 1f)
            {
                orig(damageReport, igniteOnKillCount, victimBody, attackerTeamIndex);
                return;
            }

            // === Vanilla logic, adapted for Redshifter ===

            // Base (vanilla) radius.
            float baseRadius = 8f + 4f * igniteOnKillCount + victimBody.radius;

            float explosionDamageCoef = 1.5f;
            float explosionDamage = attackerBody.damage * explosionDamageCoef;
            Vector3 center = victimBody.corePosition;

            // Use an EXPANDED radius for the burn search.
            float searchRadius = baseRadius * mult;

            GlobalEventManager.igniteOnKillSphereSearch.origin = center;
            GlobalEventManager.igniteOnKillSphereSearch.mask = LayerIndex.entityPrecise.mask;
            GlobalEventManager.igniteOnKillSphereSearch.radius = searchRadius;
            GlobalEventManager.igniteOnKillSphereSearch.RefreshCandidates();
            GlobalEventManager.igniteOnKillSphereSearch
                .FilterCandidatesByHurtBoxTeam(TeamMask.GetUnprotectedTeams(attackerTeamIndex));
            GlobalEventManager.igniteOnKillSphereSearch.FilterCandidatesByDistinctHurtBoxEntities();
            GlobalEventManager.igniteOnKillSphereSearch.OrderCandidatesByDistance();
            GlobalEventManager.igniteOnKillSphereSearch
                .GetHurtBoxes(GlobalEventManager.igniteOnKillHurtBoxBuffer);
            GlobalEventManager.igniteOnKillSphereSearch.ClearCandidates();

            // Vanilla total burn damage.
            float totalBurnDamage = (1f + igniteOnKillCount) * 0.75f * attackerBody.damage;

            for (int i = 0; i < GlobalEventManager.igniteOnKillHurtBoxBuffer.Count; i++)
            {
                HurtBox hurtBox = GlobalEventManager.igniteOnKillHurtBoxBuffer[i];
                if (!hurtBox || !hurtBox.healthComponent) continue;

                var inflictInfo = new InflictDotInfo
                {
                    victimObject      = hurtBox.healthComponent.gameObject,
                    attackerObject    = damageReport.attacker,
                    totalDamage       = totalBurnDamage,
                    dotIndex          = DotController.DotIndex.Burn,
                    damageMultiplier  = 1f,
                    hitHurtBox        = hurtBox
                };

                if (damageReport?.attackerMaster?.inventory)
                {
                    StrengthenBurnUtils.CheckDotForUpgrade(
                        damageReport.attackerMaster.inventory,
                        ref inflictInfo
                    );
                }

                DotController.InflictDot(ref inflictInfo);
            }

            GlobalEventManager.igniteOnKillHurtBoxBuffer.Clear();

            // Explosion: keep baseRadius here.
            // Your global BlastAttack hook will multiply it by `mult`
            // so the explosion radius matches the extended burn radius.
            bool sureProc = damageReport.damageInfo.procChainMask.HasProc(ProcType.SureProc);

            new BlastAttack
            {
                radius           = baseRadius, // <- vanilla radius; Redshifter scales via BlastAttack_Fire
                baseDamage       = explosionDamage,
                procCoefficient  = 0f,
                crit             = sureProc || Util.CheckRoll(attackerBody.crit, damageReport.attackerMaster),
                damageColorIndex = DamageColorIndex.Item,
                attackerFiltering= AttackerFiltering.Default,
                falloffModel     = BlastAttack.FalloffModel.None,
                attacker         = damageReport.attacker,
                teamIndex        = attackerTeamIndex,
                position         = center
            }.Fire();

            // Visual: scale to match the extended radius.
            EffectManager.SpawnEffect(
                GlobalEventManager.CommonAssets.igniteOnKillExplosionEffectPrefab,
                new EffectData
                {
                    origin   = center,
                    scale    = searchRadius, // baseRadius * mult
                    rotation = Util.QuaternionSafeLookRotation(damageReport.damageInfo.force)
                },
                transmit: true
            );

            LogDebug($"[Redshifter] ProcIgniteOnKill: baseRadius={baseRadius:F1}, searchRadius={searchRadius:F1}, mult={mult:F2}");
        }



        #endregion

        #region LightningOrb range (Ukulele / Tesla-like effects)

        private static void LightningOrb_Begin(On.RoR2.Orbs.LightningOrb.orig_Begin orig, LightningOrb self)
        {
            CharacterBody body = null;
            if (self.attacker)
                body = self.attacker.GetComponent<CharacterBody>();

            float mult = GetRadiusMultiplier(body);
            if (mult != 1f)
            {
                float oldRange = self.range;
                self.range *= mult;
                LogDebug($"[Redshifter] LightningOrb: range {oldRange:F1} -> {self.range:F1} on {body?.GetDisplayName() ?? "<no body>"}");
            }

            orig(self);
        }

        #endregion

        #region Focus Crystal “virtual radius” extension

        private static void GlobalEventManager_OnHitEnemy(
            On.RoR2.GlobalEventManager.orig_OnHitEnemy orig,
            GlobalEventManager self,
            DamageInfo damageInfo,
            GameObject victim)
        {
            // Keep this exactly as-is.
            TryApplyExtendedFocusCrystal(ref damageInfo, victim);

            bool prevPending = _pendingIceRingExplosionScale;
            float prevMult = _pendingIceRingExplosionMult;

            try
            {
                var attackerBody = damageInfo.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
                float mult = GetRadiusMultiplier(attackerBody);

                if (mult > 1f && attackerBody && attackerBody.inventory)
                {
                    // Mirror the game’s ring gate as closely as possible
                    bool notAlreadyRings = !damageInfo.procChainMask.HasProc(ProcType.Rings);
                    bool meetsThreshold = attackerBody.damage > 0f && (damageInfo.damage / attackerBody.damage) >= 4f;
                    bool ringsReady = attackerBody.HasBuff(RoR2Content.Buffs.ElementalRingsReady);

                    if (notAlreadyRings && meetsThreshold && ringsReady &&
                        attackerBody.inventory.GetItemCountEffective(RoR2Content.Items.IceRing) > 0)
                    {
                        _pendingIceRingExplosionScale = true;
                        _pendingIceRingExplosionMult = mult;
                    }
                }

                orig(self, damageInfo, victim);
            }
            finally
            {
                _pendingIceRingExplosionScale = prevPending;
                _pendingIceRingExplosionMult = prevMult;
            }
        }


        /// <summary>
        /// Vanilla Focus Crystal: +20% damage per stack if within 13m.
        /// We extend the “inside radius” from 13m to 13m * RedshifterMult.
        /// For hits in the extra ring (between 13m and extendedRadius),
        /// we apply the same Focus Crystal bonus manually.
        /// </summary>
        private static void TryApplyExtendedFocusCrystal(ref DamageInfo damageInfo, GameObject victim)
        {
            if (!damageInfo.attacker || !victim)
                return;

            var attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
            var victimBody   = victim.GetComponent<CharacterBody>();
            if (!attackerBody || !victimBody || attackerBody.inventory == null)
                return;

            int redCount   = attackerBody.inventory.GetItemCountEffective(RedshifterItemDef);
            int focusCount = attackerBody.inventory.GetItemCountEffective(RoR2Content.Items.NearbyDamageBonus);

            if (redCount <= 0 || focusCount <= 0)
                return;

            const float baseRadius = 13f;
            float radiusMult  = GetRadiusMultiplier(attackerBody);  // 1 + 0.5 * redCount
            float maxRadius   = baseRadius * radiusMult;

            Vector3 a = attackerBody.corePosition;
            Vector3 b = victimBody.corePosition;
            float distSqr = (a - b).sqrMagnitude;

            float baseRadiusSqr = baseRadius * baseRadius;
            float maxRadiusSqr  = maxRadius * maxRadius;

            // If we're already inside vanilla radius, vanilla Focus Crystal has already applied.
            if (distSqr <= baseRadiusSqr || distSqr > maxRadiusSqr)
                return;

            // We're in the “extended” ring: apply the same damage bonus Focus Crystal would.
            float bonusPerStack = 0.20f; // +20% per Focus Crystal stack
            float focusBonus    = 1f + bonusPerStack * focusCount;

            float oldDamage = damageInfo.damage;
            damageInfo.damage *= focusBonus;

            LogDebug(
                $"[Redshifter] Extended Focus Crystal: dist={Mathf.Sqrt(distSqr):F1}m, " +
                $"damage {oldDamage:F1} -> {damageInfo.damage:F1}, " +
                $"focusStacks={focusCount}, redStacks={redCount}");
        }
        
        private static void NearbyDamageBonusBodyBehavior_OnEnable(
            On.RoR2.Items.NearbyDamageBonusBodyBehavior.orig_OnEnable orig,
            RoR2.Items.NearbyDamageBonusBodyBehavior self)
        {
            // Let vanilla spawn the indicator first
            orig(self);
            UpdateFocusIndicatorScale(self);
        }

        private static void OnBodyInventoryChanged(CharacterBody body)
        {
            if (!body)
                return;

            // Focus Crystal indicator(s)
            var behaviors = body.GetComponents<RoR2.Items.NearbyDamageBonusBodyBehavior>();
            if (behaviors != null && behaviors.Length > 0)
            {
                foreach (var beh in behaviors)
                {
                    UpdateFocusIndicatorScale(beh);
                }
            }

            // Mired Urn radius
            UpdateMiredUrnRadiusForBody(body);
            
            // Faulty Conductor aura visual
            var faulty = body.GetComponent<RoR2.Items.DroneShockDamageBodyBehavior>();
            if (faulty) UpdateFaultyConductorAuraVisual(faulty);

        }


        private static void UpdateFocusIndicatorScale(RoR2.Items.NearbyDamageBonusBodyBehavior self)
        {
            if (self == null || self.body == null || self.nearbyDamageBonusIndicator == null)
                return;

            var body = self.body;
            float mult = GetRadiusMultiplier(body);
            if (mult == 1f)
                return;

            var indicator = self.nearbyDamageBonusIndicator;
            var t = indicator.transform;

            // Remember the original scale so we can recompute instead of compounding
            var tracker = indicator.GetComponent<FocusIndicatorTracker>();
            if (!tracker)
            {
                tracker = indicator.gameObject.AddComponent<FocusIndicatorTracker>();
                tracker.baseScale = t.localScale;
            }

            t.localScale = tracker.baseScale * mult;

            LogDebug($"[Redshifter] Focus Crystal indicator scaled by {mult:F2} on {body.GetDisplayName()}");
        }


        #endregion
        
        private static void MushroomBodyBehavior_FixedUpdate(
            On.RoR2.Items.MushroomBodyBehavior.orig_FixedUpdate orig,
            MushroomBodyBehavior self)
        {
            // Let vanilla logic run first (spawns ward, sets base radius, heal, etc.)
            orig(self);

            // If there's no body or no ward yet, nothing to do.
            if (!self.body || self.body.inventory == null || self.mushroomHealingWard == null)
                return;

            // How much should we scale by based on Redshifter stacks?
            var body = self.body;
            float mult = GetRadiusMultiplier(body);  // 1.0 if no Redshifter
            if (mult == 1f)
                return;

            // Scale the heal radius.
            float oldRadius = self.mushroomHealingWard.radius;
            float newRadius = oldRadius * mult;

            self.mushroomHealingWard.radius = newRadius;
            self.mushroomHealingWard.Networkradius = newRadius;

            LogDebug(
                $"[Redshifter] Bustling Fungus: radius {oldRadius:F1} -> {newRadius:F1} " +
                $"for {body.GetDisplayName()}");
        }
        
        #region Bolstering Lantern radius (AttackSpeedPerNearbyAllyOrEnemy)

        private static void AttackSpeedPerNearbyCollider_UpdateValues(
            On.RoR2.AttackSpeedPerNearbyCollider.orig_UpdateValues orig,
            AttackSpeedPerNearbyCollider self,
            int itemCount,
            out float diameter)
        {
            // Let vanilla compute maxCharacterCount, radiusSizeGrowth, and base diameter (40f)
            orig(self, itemCount, out diameter);

            if (!self || !self.body)
                return;

            // Scale the effective radius by Redshifter
            float mult = GetRadiusMultiplier(self.body);   // 1.0 if no Redshifter
            if (mult <= 1f || diameter <= 0f)
                return;

            float oldDiameter = diameter;
            diameter *= mult;

            LogDebug(
                $"[Redshifter] Bolstering Lantern: diameter {oldDiameter:F1} -> {diameter:F1} " +
                $"for {self.body.GetDisplayName()}");
        }

        #endregion
        
        #region Warbanner radius (WardOnLevel / WarbannerWard)

        private static void WardOnLevelManager_OnCharacterLevelUp(
            On.RoR2.Items.WardOnLevelManager.orig_OnCharacterLevelUp orig,
            CharacterBody characterBody)
        {
            // Let vanilla handle non-server weirdness
            if (!UnityEngine.Networking.NetworkServer.active || characterBody == null)
            {
                orig(characterBody);
                return;
            }

            var inventory = characterBody.inventory;
            if (!inventory)
            {
                orig(characterBody);
                return;
            }

            int wardOnLevelCount = inventory.GetItemCountEffective(RoR2Content.Items.WardOnLevel);
            if (wardOnLevelCount <= 0)
            {
                // No Warbanner – nothing to do
                orig(characterBody);
                return;
            }

            // How much Redshifter does this body have?
            float mult = GetRadiusMultiplier(characterBody); // 1.0 if no Redshifter
            if (mult == 1f)
            {
                // No Redshifter – just use vanilla behavior
                orig(characterBody);
                return;
            }

            // === Custom version of WardOnLevelManager.OnCharacterLevelUp with scaled radius ===

            // Make sure the prefab is loaded
            if (!RoR2.Items.WardOnLevelManager.wardPrefab)
            {
                // Fallback to vanilla if prefab somehow isn't ready
                orig(characterBody);
                return;
            }

            // Spawn the Warbanner ward at the body position
            GameObject wardObj = UnityEngine.Object.Instantiate(
                RoR2.Items.WardOnLevelManager.wardPrefab,
                characterBody.transform.position,
                UnityEngine.Quaternion.identity
            );

            // Set team
            var teamFilter = wardObj.GetComponent<TeamFilter>();
            if (teamFilter && characterBody.teamComponent)
            {
                teamFilter.teamIndex = characterBody.teamComponent.teamIndex;
            }

            // Set radius: base 8 + 8 per WardOnLevel stack, then scale by Redshifter
            var buffWard = wardObj.GetComponent<BuffWard>();
            if (buffWard)
            {
                float baseRadius = 8f + 8f * wardOnLevelCount;
                float newRadius  = baseRadius * mult;

                buffWard.Networkradius = newRadius;

                LogDebug(
                    $"[Redshifter] Warbanner: radius {baseRadius:F1} -> {newRadius:F1} " +
                    $"for {characterBody.GetDisplayName()} (warbanners={wardOnLevelCount}, mult={mult:F2})");
            }

            UnityEngine.Networking.NetworkServer.Spawn(wardObj);
        }

        #endregion
        
        #region Interstellar Desk Plant radius

        private static void DeskPlantMainState_OnEnter(
            On.RoR2.DeskPlantController.MainState.orig_OnEnter orig,
            BaseState baseState)
        {
            // The On hook is declared against BaseState, so we downcast here.
            var self = baseState as DeskPlantController.MainState;
            if (self == null)
            {
                orig(baseState);
                return;
            }

            // Get the controller directly from the same GameObject
            var controller = self.GetComponent<DeskPlantController>();
            if (!controller)
            {
                orig(baseState);
                return;
            }

            var teamFilter = controller.teamFilter;
            if (!teamFilter)
            {
                orig(baseState);
                return;
            }

            TeamIndex teamIndex = teamFilter.teamIndex;
            if (teamIndex == TeamIndex.None)
            {
                orig(baseState);
                return;
            }

            // Find the most likely "owner" on this team that actually has Interstellar Desk Plant
            CharacterBody ownerBody = null;
            int bestPlantCount = 0;

            foreach (var body in CharacterBody.readOnlyInstancesList)
            {
                if (!body || body.teamComponent == null || body.inventory == null)
                    continue;

                if (body.teamComponent.teamIndex != teamIndex)
                    continue;

                int plantCount = body.inventory.GetItemCountEffective(RoR2Content.Items.Plant);
                if (plantCount > 0 && plantCount > bestPlantCount)
                {
                    bestPlantCount = plantCount;
                    ownerBody = body;
                }
            }

            // If we found an owner with Interstellar Desk Plant, scale this Desk Plant
            if (ownerBody != null)
            {
                float mult = GetRadiusMultiplier(ownerBody); // 1.0 if no Redshifter
                if (mult != 1f)
                {
                    float oldBaseRadius = controller.healingRadius;
                    float oldPerStack   = controller.radiusIncreasePerStack;

                    controller.healingRadius       *= mult;
                    controller.radiusIncreasePerStack *= mult;

                    LogDebug(
                        $"[Redshifter] Desk Plant: baseRadius {oldBaseRadius:F1} -> {controller.healingRadius:F1}, " +
                        $"perStack {oldPerStack:F1} -> {controller.radiusIncreasePerStack:F1} for team {teamIndex} " +
                        $"(owner {ownerBody.GetDisplayName()})");
                }
            }

            // Run the original OnEnter, which will now use the scaled radii
            orig(baseState);
        }

        #endregion
        
        #region Frost Relic radius (IcicleAura)

        private static void IcicleAuraController_UpdateRadius(
            On.RoR2.IcicleAuraController.orig_UpdateRadius orig,
            IcicleAuraController self)
        {
            // First let vanilla compute base actualRadius.
            orig(self);

            if (self == null || !self.owner)
                return;

            // Owner of the aura is the Frost Relic holder.
            var body = self.owner.GetComponent<CharacterBody>();
            if (!body)
                return;

            float mult = GetRadiusMultiplier(body);   // 1.0 if no Redshifter
            if (mult == 1f || self.actualRadius <= 0f)
                return;

            float oldRadius = self.actualRadius;
            self.actualRadius *= mult;

            // Keep the ward radius synced immediately; FixedUpdate will also set it each tick.
            if (self.buffWard != null)
            {
                self.buffWard.radius        = self.actualRadius;
                self.buffWard.Networkradius = self.actualRadius;
            }

            LogDebug(
                $"[Redshifter] Frost Relic: radius {oldRadius:F1} -> {self.actualRadius:F1} " +
                $"for {body.GetDisplayName()}");
        }

        #endregion
        
        #region Mired Urn radius (SiphonNearbyController)

        private static void SiphonOnLowHealthItemBodyBehavior_OnEnable(
            On.RoR2.Items.SiphonOnLowHealthItemBodyBehavior.orig_OnEnable orig,
            RoR2.Items.SiphonOnLowHealthItemBodyBehavior self)
        {
            orig(self);

            // Only the server should drive radius; it's a SyncVar.
            if (!UnityEngine.Networking.NetworkServer.active)
                return;

            if (self?.body == null || self.siphonNearbyController == null)
                return;

            UpdateMiredUrnRadiusForBody(self.body, self.siphonNearbyController);
        }
        
        private static void UpdateMiredUrnRadiusForBody(CharacterBody body)
        {
            if (!UnityEngine.Networking.NetworkServer.active || body == null)
                return;

            // The item body behavior lives directly on the body GameObject.
            var behavior = body.GetComponent<RoR2.Items.SiphonOnLowHealthItemBodyBehavior>();
            if (behavior == null || behavior.siphonNearbyController == null)
                return;

            UpdateMiredUrnRadiusForBody(body, behavior.siphonNearbyController);
        }

        private static void UpdateMiredUrnRadiusForBody(CharacterBody body, SiphonNearbyController controller)
        {
            if (!UnityEngine.Networking.NetworkServer.active || body == null || controller == null)
                return;

            float mult = GetRadiusMultiplier(body); // 1.0 if no Redshifter

            // Grab / cache the base radius from the prefab
            var tracker = controller.GetComponent<MiredUrnRadiusTracker>();
            if (!tracker)
            {
                tracker = controller.gameObject.AddComponent<MiredUrnRadiusTracker>();
                tracker.baseRadius = controller.radius; // prefab radius
            }

            float newRadius = tracker.baseRadius * mult;
            if (Mathf.Approximately(controller.radius, newRadius))
                return;

            float oldRadius = controller.radius;
            controller.radius = newRadius;
            controller.Networkradius = newRadius;  // SyncVar setter

            LogDebug(
                $"[Redshifter] Mired Urn: radius {oldRadius:F1} -> {newRadius:F1} for {body.GetDisplayName()}");
        }

        #endregion
        
        #region Noxious Thorn (TriggerEnemyDebuffs radius)

        private static void CharacterBody_TriggerEnemyDebuffs(
            On.RoR2.CharacterBody.orig_TriggerEnemyDebuffs orig,
            CharacterBody self,
            DamageReport damageReport)
        {
            // If anything is weird, or we have no inventory, just do vanilla.
            if (!self || self.inventory == null || damageReport == null || damageReport.victimBody == null)
            {
                orig(self, damageReport);
                LogDebug("[Redshifter] Noxious Thorn: Original ran only");
                return;
            }

            // How many Noxious Thorns?
            int itemCountEffective = self.inventory.GetItemCountEffective(DLC2Content.Items.TriggerEnemyDebuffs);
            if (itemCountEffective == 0)
            {
                orig(self, damageReport);
                LogDebug("[Redshifter] Noxious Thorn: No thorn item detected, original ran");
                return;
            }

            // Redshifter multiplier for this body
            float mult = GetRadiusMultiplier(self); // 1.0f if no Redshifter
            if (Mathf.Approximately(mult, 1f))
            {
                // No Redshifter: just run vanilla logic.
                orig(self, damageReport);
                LogDebug("[Redshifter] Noxious Thorn: No redshifter detected, original ran");
                return;
            }

            // ====== Vanilla TriggerEnemyDebuffs, with ONLY the radius line modified ======

            List<VineOrb.SplitDebuffInformation> debuffInfoList = new List<VineOrb.SplitDebuffInformation>();
            DamageInfo damageInfo = damageReport.damageInfo;
            DotController dotController = DotController.FindDotController(damageReport.victimBody.gameObject);

            foreach (BuffIndex excludingNoxiousThorn in BuffCatalog.debuffAndDotsIndicesExcludingNoxiousThorns)
            {
                BuffDef buffDef = BuffCatalog.GetBuffDef(excludingNoxiousThorn);
                int buffCount = damageReport.victimBody.GetBuffCount(buffDef);
                if (buffCount > 0)
                {
                    int num = Mathf.CeilToInt(buffCount * 0.33f);
                    bool flag = false;
                    float totalDuration = 0f;

                    if (buffDef.isDOT && dotController != null)
                    {
                        DotController.DotIndex dotDefIndex = DotController.GetDotDefIndex(buffDef);
                        DotController.GetDotDef(dotDefIndex);
                        flag = dotController.GetDotStackTotalDurationForIndex(dotDefIndex, out totalDuration);
                    }
                    else if (buffDef.isDebuff)
                    {
                        flag = damageReport.victimBody.GetTimedBuffTotalDurationForIndex(excludingNoxiousThorn, out totalDuration);
                    }

                    VineOrb.SplitDebuffInformation debuffInformation = new VineOrb.SplitDebuffInformation
                    {
                        attacker       = self.gameObject,
                        attackerMaster = self.master,
                        index          = excludingNoxiousThorn,
                        isTimed        = flag,
                        duration       = totalDuration,
                        count          = num
                    };
                    debuffInfoList.Add(debuffInformation);
                }
            }

            if (debuffInfoList.Count == 0)
                return;

            SphereSearch sphereSearch = new SphereSearch();
            List<HurtBox> hurtBoxList = CollectionPool<HurtBox, List<HurtBox>>.RentCollection();

            sphereSearch.mask = LayerIndex.entityPrecise.mask;
            sphereSearch.origin = damageReport.victimBody.gameObject.transform.position;

            // --- THIS is the one line we change for Redshifter ---
            float baseRadius   = 20f + 5f * (itemCountEffective - 1);
            float scaledRadius = baseRadius * mult;
            sphereSearch.radius = scaledRadius;
            // ------------------------------------------------------

            sphereSearch.queryTriggerInteraction = QueryTriggerInteraction.UseGlobal;
            sphereSearch.RefreshCandidates();
            sphereSearch.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(self.teamComponent.teamIndex));
            sphereSearch.OrderCandidatesByDistance();
            sphereSearch.FilterCandidatesByDistinctHurtBoxEntities();
            sphereSearch.GetHurtBoxes(hurtBoxList);
            sphereSearch.ClearCandidates();

            int remainingTargets = itemCountEffective;
            for (int i = 0; i < hurtBoxList.Count; ++i)
            {
                HurtBox targetHurtbox = hurtBoxList[i];
                CharacterBody body = targetHurtbox.healthComponent.body;
                if (targetHurtbox &&
                    targetHurtbox.healthComponent &&
                    targetHurtbox.healthComponent.alive &&
                    body != damageReport.victimBody &&
                    body != self)
                {
                    self.CreateVineOrbChain(damageReport.victimBody.gameObject, targetHurtbox, debuffInfoList);
                    --remainingTargets;
                    if (remainingTargets == 0)
                    {
                        CollectionPool<HurtBox, List<HurtBox>>.ReturnCollection(hurtBoxList);
                        
                        LogDebug(
                            $"[Redshifter] Noxious Thorn: radius {baseRadius:F1} -> {scaledRadius:F1} " +
                            $"for {self.GetDisplayName()} (thornStacks={itemCountEffective}, mult={mult:F2}) " +
                            "(early exit after hitting max targets)"
                        );
                        
                        return;
                    }
                }
            }

            CollectionPool<HurtBox, List<HurtBox>>.ReturnCollection(hurtBoxList);

            LogDebug(
                $"[Redshifter] Noxious Thorn: radius {baseRadius:F1} -> {scaledRadius:F1} " +
                $"for {self.GetDisplayName()} (thornStacks={itemCountEffective}, mult={mult:F2})");
        }

        #endregion
        
        #region Faraday Spur radius (JumpDamageStrike GetRadius)

        private static float JumpDamageStrikeBodyBehavior_GetRadius(
            On.RoR2.Items.JumpDamageStrikeBodyBehavior.orig_GetRadius orig,
            RoR2.Items.JumpDamageStrikeBodyBehavior self,
            int charge,
            int stacks)
        {
            float baseRadius = orig(self, charge, stacks);

            // Vanilla returns 0 if charge < 25
            if (baseRadius <= 0f || self == null || self.body == null)
                return baseRadius;

            float mult = GetRadiusMultiplier(self.body); // 1.0 if no Redshifter
            if (mult == 1f)
                return baseRadius;

            return baseRadius * mult;
        }

        #endregion
        
        #region Kinetic Dampener Visuals
        private static void EffectManager_SpawnEffect_GameObject(
            On.RoR2.EffectManager.orig_SpawnEffect_GameObject_EffectData_bool orig,
            GameObject effectPrefab,
            EffectData effectData,
            bool transmit)
        {
            try
            {
                if (effectPrefab && effectData != null)
                {
                    // Kinetic Dampener / ShieldBooster visuals
                    if (effectPrefab == HealthComponent.AssetReferences.shieldBoosterBreakPrefab ||
                        effectPrefab == HealthComponent.AssetReferences.shieldBoosterBreakVoidPrefab)
                    {
                        var body = FindClosestBody(effectData.origin, 3f);
                        if (body && body.inventory != null)
                        {
                            float mult = GetRadiusMultiplier(body);
                            if (mult > 1f)
                            {
                                float before = effectData.scale;
                                effectData.scale *= mult;

                                LogDebug($"[Redshifter] Kinetic Dampener visual: scale {before:0.###} -> {effectData.scale:0.###} for {body.GetDisplayName()} (mult={mult:0.00})");
                            }
                        }
                    }
                    
                    // Faulty Conductor pulse visual (SpawnEffect uses scale = body.radius) :contentReference[oaicite:10]{index=10}
                    if (effectPrefab == CharacterBody.CommonAssets.shockDamagePulseEffect)
                    {
                        var body = FindClosestBody(effectData.origin, 3f);
                        if (body && body.inventory != null)
                        {
                            float mult = GetRadiusMultiplier(body);
                            if (mult > 1f)
                            {
                                float before = effectData.scale;
                                effectData.scale *= mult;
                                LogDebug($"[Redshifter] Faulty Conductor pulse: scale {before:0.###} -> {effectData.scale:0.###} for {body.GetDisplayName()} (mult={mult:0.00})");
                            }
                        }
                    }
                    
                    if (_pendingIceRingExplosionScale && effectPrefab && effectPrefab.name == "IceRingExplosion")
                    {
                        effectData.scale *= _pendingIceRingExplosionMult;
                        LogDebug($"[Redshifter] IceRingExplosion visual scale *= {_pendingIceRingExplosionMult:0.##}");
                    }

                }
            }
            catch (Exception e)
            {
                LogWarning($"[Redshifter] EffectManager_SpawnEffect exception: {e}");
            }

            orig(effectPrefab, effectData, transmit);
        }

        private static CharacterBody FindClosestBody(Vector3 pos, float maxDist)
        {
            CharacterBody best = null;
            float bestSqr = maxDist * maxDist;

            // CharacterBody has instancesList/readOnlyInstancesList in your dump.
            // Use readOnlyInstancesList to avoid accidental modifications.
            var list = CharacterBody.readOnlyInstancesList;
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (!b) continue;

                float sqr = (b.transform.position - pos).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = b;
                }
            }

            return best;
        }
        
        #endregion
        
        #region Faulty Conductor (Shock Damage Aura) radius + visuals

        private sealed class FaultyConductorAuraScaleTracker : MonoBehaviour
        {
            public Vector3 baseScale;
        }

        private static void DroneShockDamageBodyBehavior_Start(
            On.RoR2.Items.DroneShockDamageBodyBehavior.orig_Start orig,
            RoR2.Items.DroneShockDamageBodyBehavior self)
        {
            orig(self);

            // Aura prefab is created in Start() (auraInstance) :contentReference[oaicite:4]{index=4}
            UpdateFaultyConductorAuraVisual(self);
        }

        private static void DroneShockDamageBodyBehavior_TriggerEnergize(
            On.RoR2.Items.DroneShockDamageBodyBehavior.orig_TriggerEnergize orig,
            RoR2.Items.DroneShockDamageBodyBehavior self)
        {
            var body = self?.body;
            float mult = GetRadiusMultiplier(body);
            if (mult == 1f)
            {
                orig(self);
                return;
            }

            // TriggerEnergize uses DroneShockDamageBodyBehavior.radius for SphereSearch.radius :contentReference[oaicite:5]{index=5}
            float old = RoR2.Items.DroneShockDamageBodyBehavior.radius; // default 45f :contentReference[oaicite:6]{index=6}
            RoR2.Items.DroneShockDamageBodyBehavior.radius = old * mult;

            try
            {
                orig(self);
            }
            finally
            {
                RoR2.Items.DroneShockDamageBodyBehavior.radius = old;
            }

            LogDebug($"[Redshifter] Faulty Conductor: search radius {old:F1} -> {(old * mult):F1} for {body?.GetDisplayName() ?? "?"} (mult={mult:F2})");
        }

        private static void UpdateFaultyConductorAuraVisual(RoR2.Items.DroneShockDamageBodyBehavior beh)
        {
            if (beh == null || beh.body == null || !beh.auraInstance)
                return;

            float mult = GetRadiusMultiplier(beh.body);
            var t = beh.auraInstance.transform;

            var tracker = beh.auraInstance.GetComponent<FaultyConductorAuraScaleTracker>();
            if (!tracker)
            {
                tracker = beh.auraInstance.AddComponent<FaultyConductorAuraScaleTracker>();
                tracker.baseScale = t.localScale;
            }

            t.localScale = tracker.baseScale * mult;
        }

        #endregion
        
        #region Bands radius
        
        private static void ApplyScaledLocalScale(Transform t, float mult)
        {
            if (!t || mult == 1f) return;

            var tracker = t.GetComponent<RedshifterScaleTracker>();
            if (!tracker)
            {
                tracker = t.gameObject.AddComponent<RedshifterScaleTracker>();
                tracker.baseScale = t.localScale;
            }

            t.localScale = tracker.baseScale * mult;
        }

        private static void ProjectileController_Start(
            On.RoR2.Projectile.ProjectileController.orig_Start orig,
            ProjectileController self)
        {
            orig(self);

            if (!self || !self.owner) return;

            var ownerBody = self.owner.GetComponent<CharacterBody>();
            float mult = GetRadiusMultiplier(ownerBody);
            if (mult <= 1f) return;

            // Only touch the two ring projectiles we care about.
            string n = self.gameObject.name; // includes "(Clone)"
            bool isFireTornado = n.StartsWith("FireTornado");
            bool isVoidBlackHole = n.StartsWith("ElementalRingVoidBlackHole");

            if (!isFireTornado && !isVoidBlackHole) return;

            // 1) Scale transform (often affects overlap/hitboxes + visuals)
            ApplyScaledLocalScale(self.transform, mult);

            // 2) Also try to scale any radius-like fields on components (important for black hole pull logic)
            MultiplyRadiusLikeFields(self.gameObject, mult);

            LogDebug($"[Redshifter] Rings projectile scaled: {n} mult={mult:0.##}");
        }

        private static readonly string[] _radiusFieldNames =
        {
            "radius", "range", "maxDistance", "pullRadius", "suctionRadius", "searchRadius"
        };

        private static void MultiplyRadiusLikeFields(GameObject go, float mult)
        {
            // Only runs for the ring projectiles above, so reflection is safe-ish.
            var comps = go.GetComponentsInChildren<Component>(true);
            foreach (var c in comps)
            {
                if (!c) continue;
                var t = c.GetType();

                foreach (var fieldName in _radiusFieldNames)
                {
                    var f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f == null || f.FieldType != typeof(float)) continue;

                    float oldVal = (float)f.GetValue(c);
                    if (oldVal <= 0f) continue;

                    f.SetValue(c, oldVal * mult);
                    // Optional: log if you want it noisy
                    // LogDebug($"[Redshifter]   {t.Name}.{fieldName}: {oldVal} -> {oldVal * mult}");
                }
            }
        }
        
        private static void ProjectileGhostController_Start(
            On.RoR2.Projectile.ProjectileGhostController.orig_Start orig,
            RoR2.Projectile.ProjectileGhostController self)
        {
            orig(self);

            if (!self) return;

            string ghostName = self.gameObject.name;
            if (!ghostName.Contains("FireTornadoGhost") && !ghostName.Contains("ElementalRingVoidBlackHole"))
                return;

            // Use whichever link exists (prediction often means authorityTransform is null)
            Transform link = self.authorityTransform ? self.authorityTransform : self.predictionTransform;
            if (!link) return;

            // IMPORTANT: link might be ghostTransformAnchor (child), so use InParent
            var pc = link.GetComponentInParent<RoR2.Projectile.ProjectileController>();
            if (!pc || !pc.owner) return;

            var body = pc.owner.GetComponent<CharacterBody>();
            if (!body) return;

            float mult = GetRadiusMultiplier(body);
            if (mult <= 1f) return;

            // If you want the ghost to continue inheriting scale correctly:
            self.inheritScaleFromProjectile = true;

            ApplyScaledLocalScale(self.transform, mult);

            if (ghostName.Contains("FireTornadoGhost"))
                FixFireTornadoGhostVisuals(self.gameObject, mult);

            LogDebug($"[Redshifter] Ring ghost scaled: {ghostName} mult={mult:0.##} for {body.GetDisplayName()}");
        }

        private static void FixFireTornadoGhostVisuals(GameObject ghostRoot, float mult)
        {
            if (!ghostRoot || mult == 1f) return;

            // 1) Make ParticleSystems actually respect transform scaling
            var pss = ghostRoot.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < pss.Length; i++)
            {
                var ps = pss[i];
                if (!ps) continue;

                var main = ps.main;
                // This is the big one: makes parent scaling affect the particles.
                if (main.scalingMode != ParticleSystemScalingMode.Hierarchy)
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }

            // 2) LineRenderer widths usually ignore transform scale
            var lrs = ghostRoot.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lrs.Length; i++)
            {
                var lr = lrs[i];
                if (!lr) continue;
                lr.widthMultiplier *= mult;
            }

            // 3) TrailRenderer widths also usually ignore transform scale
            var trs = ghostRoot.GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trs.Length; i++)
            {
                var tr = trs[i];
                if (!tr) continue;
                tr.widthMultiplier *= mult;
            }

            // 4) If the ghost has any "radius" style fields (ex: ShakeEmitter.radius),
            // scale them too using your existing reflection helper.
            MultiplyRadiusLikeFields(ghostRoot, mult);
        }
        
        private static void RandomDamageZoneBodyBehavior_FixedUpdate(
            On.RoR2.Items.RandomDamageZoneBodyBehavior.orig_FixedUpdate orig,
            RoR2.Items.RandomDamageZoneBodyBehavior self)
        {
            if (self == null || self.body == null)
            {
                orig(self);
                return;
            }

            float mult = GetRadiusMultiplier(self.body); // your Redshifter scaling multiplier
            if (mult <= 1f)
            {
                orig(self);
                return;
            }

            // Optional: detect if this FixedUpdate actually resulted in a ward being spawned
            // PowerWard is limited; if it was available before but not after, we probably spawned one.
            var master = self.body.master;
            bool slotAvailBefore = master && master.IsDeployableSlotAvailable(DeployableSlot.PowerWard);

            float oldBase = RoR2.Items.RandomDamageZoneBodyBehavior.baseWardRadius;
            RoR2.Items.RandomDamageZoneBodyBehavior.baseWardRadius = oldBase * mult;

            try
            {
                orig(self);
            }
            finally
            {
                RoR2.Items.RandomDamageZoneBodyBehavior.baseWardRadius = oldBase;
            }

            bool slotAvailAfter = master && master.IsDeployableSlotAvailable(DeployableSlot.PowerWard);
            if (slotAvailBefore && !slotAvailAfter)
            {
                LogDebug(
                    $"[Redshifter] Mercurial Rachis: baseWardRadius {oldBase:F1} -> {(oldBase * mult):F1} " +
                    $"for {self.body.GetDisplayName()} (mult={mult:F2})"
                );
            }
        }
        
        #endregion
        
        private static void HoldoutZoneController_Awake(On.RoR2.HoldoutZoneController.orig_Awake orig, HoldoutZoneController self)
        {
            orig(self);

            if (!self) return;

            // Always add; our scaler will only do anything if the charging team has Redshifter.
            if (!self.GetComponent<RedshifterHoldoutZoneScaler>())
                self.gameObject.AddComponent<RedshifterHoldoutZoneScaler>();
        }

        private static void KillZoneController_Awake(On.RoR2.KillZoneController.orig_Awake orig, KillZoneController self)
        {
            orig(self);

            if (!self) return;

            if (!self.GetComponent<RedshifterKillZoneScaler>())
                self.gameObject.AddComponent<RedshifterKillZoneScaler>();
        }

        /// <summary>
        /// Teleporter / holdout zone radius scaling (uses HoldoutZoneController.calcRadius)
        /// </summary>
        private sealed class RedshifterHoldoutZoneScaler : MonoBehaviour
        {
            private HoldoutZoneController _zone;

            private void Awake() => _zone = GetComponent<HoldoutZoneController>();

            private void OnEnable()
            {
                if (_zone != null)
                    _zone.calcRadius += ApplyRadius;
            }

            private void OnDisable()
            {
                if (_zone != null)
                    _zone.calcRadius -= ApplyRadius;
            }

            private void ApplyRadius(ref float radius)
            {
                int stacks = GetMaxRedshifterStacksOnTeam(_zone.chargingTeam);
                if (stacks <= 0) return;

                float mult = 1f + RadiusBonusPerItem * stacks; // e.g. 0.5f per stack
                radius *= mult;
            }
        }

        /// <summary>
        /// KillZoneController also has calcRadius and scales a radius indicator the same way.
        /// </summary>
        private sealed class RedshifterKillZoneScaler : MonoBehaviour
        {
            private KillZoneController _zone;

            private void Awake() => _zone = GetComponent<KillZoneController>();

            private void OnEnable()
            {
                if (_zone != null)
                    _zone.calcRadius += ApplyRadius;
            }

            private void OnDisable()
            {
                if (_zone != null)
                    _zone.calcRadius -= ApplyRadius;
            }

            private void ApplyRadius(ref float radius)
            {
                int stacks = GetMaxRedshifterStacksOnTeam(_zone.chargingTeam);
                if (stacks <= 0) return;

                float mult = 1f + RadiusBonusPerItem * stacks;
                radius *= mult;
            }
        }

        /// <summary>
        /// I strongly recommend "max on team" (not sum) so multiplayer doesn't explode radii.
        /// Mirrors HoldoutZoneController's own idea of "players on a team" filtering.
        /// </summary>
        private static int GetMaxRedshifterStacksOnTeam(TeamIndex teamIndex)
        {
            int max = 0;

            var members = TeamComponent.GetTeamMembers(teamIndex);
            for (int i = 0; i < members.Count; i++)
            {
                var body = members[i].body;
                if (!body || !body.isPlayerControlled || body.isRemoteOp) continue;

                var inv = body.inventory;
                if (!inv) continue;

                int count = inv.GetItemCountEffective(RedshifterItemDef);
                if (count > max) max = count;
            }

            return max;
        }


    }
}
