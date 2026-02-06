// DoomedMoonHooks.cs
using System;
using System.Collections.Generic;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using static RiskOfImpact.RiskOfImpactMain;

namespace RiskOfImpact
{
    internal static class DoomedMoonHooks
    {
        private const int ItemsToBreak = 5;
        private const float PctToBreak = 0.10f;

        // Pearl-stat values (matches your pearl table)
        private const float PctPerToken = 0.10f; // health/damage/movespeed/attackspeed
        private const float CritPerToken = 10f;  // crit chance
        private const float ArmorPerToken = 10f; // armor display
        private const float RegenBase = 0.10f;
        private const float RegenAddPerExtra = 0.02f;

        private static ItemIndex _doomedMoonIndex = ItemIndex.None;
        private static ItemDef _doomedMoonDef;

        private static ItemDef _doomedMoonConsumedDef;
        private static ItemIndex _doomedMoonConsumedIndex = ItemIndex.None;

        private static ItemDef _doomedMoonStatTokenDef;
        private static ItemIndex _doomedMoonStatTokenIndex = ItemIndex.None;
        
        private static BuffDef _doomedMoonBuffDef;
        private static BuffIndex _doomedMoonBuffIndex = BuffIndex.None;

        private static bool _initialized;
        private static bool _resolvedOnceLogged;

        internal static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            Stage.onServerStageBegin += OnServerStageBegin;

            // Correct revive priority hook
            On.RoR2.CharacterMaster.TryReviveOnBodyDeath += CharacterMaster_TryReviveOnBodyDeath;

            // YOU WERE MISSING THIS: apply stat tokens to stats
            RecalculateStatsAPI.GetStatCoefficients += ApplyStatBonuses;

            LogInfo("[DoomedMoon] Hooks initialized.");
        }

        // --------------------------------------------------------------------
        // Resolve indices from your content provider outputs
        // --------------------------------------------------------------------
        private static bool EnsureResolved()
        {
            // Cache: only resolve once successfully
            if (_doomedMoonIndex != ItemIndex.None &&
                _doomedMoonConsumedIndex != ItemIndex.None &&
                _doomedMoonStatTokenIndex != ItemIndex.None &&
                _doomedMoonBuffIndex != BuffIndex.None &&
                _doomedMoonDef && _doomedMoonConsumedDef && _doomedMoonStatTokenDef && _doomedMoonBuffDef)
                return true;

            try
            {
                _doomedMoonDef = RiskOfImpactContent.GetDoomedMoonItemDef();
                _doomedMoonConsumedDef = RiskOfImpactContent.GetDoomedMoonConsumedItemDef();
                _doomedMoonStatTokenDef = RiskOfImpactContent.GetDoomedMoonStatTokenItemDef();
                _doomedMoonBuffDef =  RiskOfImpactContent.GetDoomedMoonBuffDef();
            }
            catch (Exception e)
            {
                if (!_resolvedOnceLogged)
                {
                    _resolvedOnceLogged = true;
                    LogWarning($"[DoomedMoon] EnsureResolved failed to fetch defs from content provider: {e}");
                }
                return false;
            }

            if (!_doomedMoonDef || !_doomedMoonConsumedDef || !_doomedMoonStatTokenDef || !_doomedMoonBuffDef)
                return false;

            _doomedMoonIndex = _doomedMoonDef.itemIndex;
            _doomedMoonConsumedIndex = _doomedMoonConsumedDef.itemIndex;
            _doomedMoonStatTokenIndex = _doomedMoonStatTokenDef.itemIndex;
            _doomedMoonBuffIndex = _doomedMoonBuffDef.buffIndex;

            // Expansion gate follows main item
            if (_doomedMoonConsumedDef.requiredExpansion == null)
                _doomedMoonConsumedDef.requiredExpansion = _doomedMoonDef.requiredExpansion;
            if (_doomedMoonStatTokenDef.requiredExpansion == null)
                _doomedMoonStatTokenDef.requiredExpansion = _doomedMoonDef.requiredExpansion;

            return _doomedMoonIndex != ItemIndex.None
                   && _doomedMoonConsumedIndex != ItemIndex.None
                   && _doomedMoonStatTokenIndex != ItemIndex.None
                   && _doomedMoonBuffIndex != BuffIndex.None;
        }

        // --------------------------------------------------------------------
        // Stage refresh: consumed -> active
        // --------------------------------------------------------------------
        private static void OnServerStageBegin(Stage stage)
        {
            if (!NetworkServer.active) return;
            if (!EnsureResolved()) return;

            foreach (var master in CharacterMaster.readOnlyInstancesList)
            {
                var inv = master?.inventory;
                if (!inv) continue;

                int consumed = inv.GetItemCountPermanent(_doomedMoonConsumedIndex);
                if (consumed <= 0) continue;

                new Inventory.ItemTransformation
                {
                    originalItemIndex = _doomedMoonConsumedIndex,
                    newItemIndex = _doomedMoonIndex,
                    minToTransform = consumed,
                    maxToTransform = consumed,
                    transformationType = (ItemTransformationTypeIndex)7
                }.TryTransform(inv, out _);
            }
        }

        // --------------------------------------------------------------------
        // Revive priority hook: vanilla revives first
        // --------------------------------------------------------------------
        private static bool CharacterMaster_TryReviveOnBodyDeath(
            On.RoR2.CharacterMaster.orig_TryReviveOnBodyDeath orig,
            CharacterMaster self,
            CharacterBody body)
        {
            // Vanilla first (shrine, dio, void dio, heal&revive, etc.)
            if (orig(self, body))
                return true;

            if (!NetworkServer.active) return false;
            if (!self || !body) return false;
            if (!EnsureResolved()) return false;

            if (self.IsExtraLifePendingServer())
                return true;

            var inv = self.inventory;
            if (!inv) return false;

            int activeStacks = inv.GetItemCountPermanent(_doomedMoonIndex);
            if (activeStacks <= 0)
                return false;

            // Break items
            if (!BreakRandomItems(inv, ItemsToBreak, PctToBreak))
                return false;

            // IMPORTANT: capture total stacks BEFORE consuming (stable + matches design)
            // Consume one
            Inventory.ItemTransformation.TakeResult takeResult;
            if (!new Inventory.ItemTransformation
            {
                originalItemIndex = _doomedMoonIndex,
                newItemIndex = _doomedMoonConsumedIndex,
                minToTransform = 1,
                maxToTransform = 1,
                transformationType = (~ItemTransformationTypeIndex.None)
            }.TryTake(inv, out takeResult))
            {
                return false;
            }

            // Grant permanent stat token

            inv.GiveItemPermanent(_doomedMoonStatTokenIndex);
            
            // Schedule respawn like Dio’s via ExtraLifeServerBehavior
            var life = self.gameObject.AddComponent<CharacterMaster.ExtraLifeServerBehavior>();
            life.pendingTransformation = takeResult;
            life.consumedItemIndex = _doomedMoonConsumedIndex;
            life.completionTime = Run.FixedTimeStamp.now + 2f;
            life.completionCallback = () => RespawnDoomedMoon(self);
            life.soundTime = life.completionTime - 1f;
            life.soundCallback = () => PlayDoomedMoonSfx(self);

            return true;
        }

        private static void RespawnDoomedMoon(CharacterMaster master)
        {
            if (!master) return;

            Vector3 footPos = master.deathFootPosition;

            if (master.killedByUnsafeArea)
            {
                var bodyPrefabBody = master.bodyPrefab ? master.bodyPrefab.GetComponent<CharacterBody>() : null;
                if (bodyPrefabBody)
                    footPos = TeleportHelper.FindSafeTeleportDestination(master.deathFootPosition, bodyPrefabBody, RoR2Application.rng)
                              ?? master.deathFootPosition;
            }

            master.Respawn(footPos, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), true);

            var newBody = master.GetBody();
            if (newBody)
            {
                newBody.AddTimedBuff(RoR2Content.Buffs.Immune, 3f);
                foreach (var esm in newBody.GetComponents<EntityStateMachine>())
                    esm.initialStateType = esm.mainStateType;
                SyncDoomedMoonBuff(newBody);
            }

            var fx = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/HippoRezEffect");
            if (fx && master.bodyInstanceObject)
            {
                EffectManager.SpawnEffect(fx, new EffectData
                {
                    origin = footPos,
                    rotation = master.bodyInstanceObject.transform.rotation
                }, true);
            }
        }

        private static void PlayDoomedMoonSfx(CharacterMaster master)
        {
            if (master && master.bodyInstanceObject)
                Util.PlaySound("Play_item_proc_extraLife", master.bodyInstanceObject);
        }

        // --------------------------------------------------------------------
        // APPLY STATS (you were missing this entirely)
        // --------------------------------------------------------------------
        private static void ApplyStatBonuses(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!body || !body.inventory) return;
            if (!EnsureResolved()) return;

            if (NetworkServer.active)
                SyncDoomedMoonBuff(body);

            int tokens = body.inventory.GetItemCountPermanent(_doomedMoonStatTokenIndex);
            if (tokens <= 0) return;

            float pct = PctPerToken * tokens;   // e.g. 0.10f * tokens
            float mult = 1f + pct;              // e.g. 1.10f, 1.20f, ...

            args.healthTotalMult      *= mult;
            args.damageTotalMult      *= mult;
            args.moveSpeedTotalMult   *= mult;
            args.attackSpeedTotalMult *= mult;

            // These are additive stats; leave as-is
            args.critAdd  += CritPerToken * tokens;
            args.armorAdd += ArmorPerToken * tokens;

            // Regen in your code is baseRegenAdd (flat), so keep as-is
            args.baseRegenAdd += RegenBase + RegenAddPerExtra * Mathf.Max(0, tokens - 1);
        }


        // --------------------------------------------------------------------
        // Item breaking logic
        // --------------------------------------------------------------------

        private static bool BreakRandomItems(Inventory inv, int toBreak, float pctBreak)
        {
            var bag = new List<ItemIndex>(256);

            foreach (ItemIndex idx in ItemCatalog.allItems)
            {
                if (!IsBreakable(inv, idx)) continue;

                int count = inv.GetItemCountPermanent(idx);
                for (int i = 0; i < count; i++)
                    bag.Add(idx);
            }

            if (bag.Count < toBreak) return false;
            int totalBreak = Mathf.Max(toBreak, Mathf.CeilToInt(pctBreak * bag.Count));

            for (int i = 0; i < totalBreak; i++)
            {
                int pick = UnityEngine.Random.Range(0, bag.Count);
                ItemIndex chosen = bag[pick];

                inv.RemoveItemPermanent(chosen);
                bag.RemoveAt(pick);
            }

            return true;
        }

        private static bool IsBreakable(Inventory inv, ItemIndex idx)
        {
            if (idx == ItemIndex.None) return false;
            if (!inv) return false;


            // Exclude our own items
            if (idx == _doomedMoonIndex) return false;
            if (idx == _doomedMoonConsumedIndex) return false;
            if (idx == _doomedMoonStatTokenIndex) return false;

            var def = ItemCatalog.GetItemDef(idx);
            if (!def) return false;

            // Exclude void/corrupted
            if (def.tier == ItemTier.VoidTier1 ||
                def.tier == ItemTier.VoidTier2 ||
                def.tier == ItemTier.VoidTier3 ||
                def.tier == ItemTier.VoidBoss)
                return false;

            // Exclude NoTier/unremovable
            if (def.tier == ItemTier.NoTier) return false;
            if (!def.canRemove) return false;
            
            

            return inv.GetItemCountPermanent(idx) > 0;
        }
        
        private static void SyncDoomedMoonBuff(CharacterBody body)
        {
            if (!NetworkServer.active) return;
            if (!body || !body.inventory) return;
            if (_doomedMoonBuffIndex == BuffIndex.None) return;

            int tokens = body.inventory.GetItemCountPermanent(_doomedMoonStatTokenIndex);
            tokens = Mathf.Clamp(tokens, 0, 255); // buff stacks are byte-ish in many contexts

            // Best-case: SetBuffCount exists and replicates cleanly
            body.SetBuffCount(_doomedMoonBuffIndex, tokens);
        }

    }
}
