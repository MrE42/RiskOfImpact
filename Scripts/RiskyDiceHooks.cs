// RiskyDiceHooks.cs

using R2API;
using UnityEngine;
using UnityEngine.Networking;
using RoR2;

namespace RiskOfImpact
{
    /// <summary>
    /// Risky Dice (Chests + Shrine of Chance only)
    ///
    /// Rolls-per-interact stacking (processed as a batch):
    /// - N stacks => N rolls on each eligible interaction.
    /// - Each non-misfortune roll counts as a success.
    /// - If ANY roll is misfortune:
    ///     * punish based on Risk at the moment misfortune occurs (Risk + successes before that roll)
    ///     * reset Risk to 0 (counter + buff)
    ///     * cancel the purchase (do not call orig)
    ///
    /// Bonus items:
    /// - 1 bonus item per successful roll (so +N on full success).
    ///
    /// Sales Star compatibility:
    /// - We do NOT touch dropCount during PurchaseInteraction (Sales Star sets it there).
    /// - We stash a pending bonus on the chest object and apply it inside ChestBehavior.Open.
    ///
    /// Shrine of Chance:
    /// - If no misfortune, shrine auto-succeeds for that activation (temporary failureChance = -1f).
    /// - On success, spawn pending bonus extra drops from shrine dropTable/rng/dropletOrigin (additive).
    /// </summary>
    public static class RiskyDiceHooks
    {
        public static bool DebugLogs = true;

        // 1/20 misfortune
        public static int MisfortuneRollOutOf = 20;
        public static int MisfortuneRollHit = 0;

        // Risk bands (punishment)
        public const int DeathRisk = 20;

        // Punishment: TonicAffliction is an ITEM (permanent)
        public static int Band1TonicAfflictionStacks = 2;
        public static int Band2TonicAfflictionStacks = 4;

        private static bool _initialized;

        // Pending bonus items for a specific chest open
        private sealed class PendingChestBonus : MonoBehaviour
        {
            public int bonus;
        }

        // Pending bonus items for the next Shrine of Chance SUCCESS for a player
        private sealed class RiskyDiceTracker : MonoBehaviour
        {
            public int pendingChanceShrineBonus;
        }

        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            CharacterBody.onBodyStartGlobal += OnBodyStartGlobal;
            
            On.RoR2.CharacterBody.OnInventoryChanged += CharacterBody_OnInventoryChanged;

            On.RoR2.PurchaseInteraction.OnInteractionBegin += PurchaseInteraction_OnInteractionBegin;

            // IMPORTANT: In your assembly, ChestBehavior.Open takes NO Interactor parameter.
            On.RoR2.ChestBehavior.Open += ChestBehavior_Open;

            On.RoR2.ShrineChanceBehavior.AddShrineStack += ShrineChanceBehavior_AddShrineStack;
            
            RecalculateStatsAPI.GetStatCoefficients += ApplyMisfortuneAffliction;

            LogI("[RiskyDice] Hooks initialized.");
        }

        public static void Cleanup()
        {
            if (!_initialized) return;
            _initialized = false;

            CharacterBody.onBodyStartGlobal -= OnBodyStartGlobal;
            On.RoR2.CharacterBody.OnInventoryChanged -= CharacterBody_OnInventoryChanged;
            On.RoR2.PurchaseInteraction.OnInteractionBegin -= PurchaseInteraction_OnInteractionBegin;
            On.RoR2.ChestBehavior.Open -= ChestBehavior_Open;
            On.RoR2.ShrineChanceBehavior.AddShrineStack -= ShrineChanceBehavior_AddShrineStack;
            RecalculateStatsAPI.GetStatCoefficients -= ApplyMisfortuneAffliction;

            LogI("[RiskyDice] Hooks cleaned up.");
        }

        private static void OnBodyStartGlobal(CharacterBody body)
        {
            if (!NetworkServer.active) return;
            if (!body) return;

            var inv = body.inventory;
            if (!inv) return;

            SyncRiskBuff(body, inv);
        }
        
        private static void CharacterBody_OnInventoryChanged(On.RoR2.CharacterBody.orig_OnInventoryChanged orig, CharacterBody self)
        {
            orig(self);

            if (!NetworkServer.active) return;
            if (!self || !self.inventory) return;

            SyncRiskBuff(self, self.inventory);
        }


        // -------------------------------
        // PurchaseInteraction hook
        // -------------------------------
        private static void PurchaseInteraction_OnInteractionBegin(
            On.RoR2.PurchaseInteraction.orig_OnInteractionBegin orig,
            PurchaseInteraction self,
            Interactor activator)
        {
            if (!NetworkServer.active)
            {
                orig(self, activator);
                return;
            }

            if (!self || !activator)
            {
                orig(self, activator);
                return;
            }

            var body = activator.GetComponent<CharacterBody>();
            if (!body || !body.master)
            {
                orig(self, activator);
                return;
            }

            var inv = body.inventory;
            if (!inv)
            {
                orig(self, activator);
                return;
            }

            // Only apply to chests or shrine of chance
            var chest = self.GetComponent<ChestBehavior>();
            var shrineChance = self.GetComponent<ShrineChanceBehavior>();

            bool isChest = chest != null;
            bool isChanceShrine = shrineChance != null;

            if (!isChest && !isChanceShrine)
            {
                orig(self, activator);
                return;
            }

            // Pull defs from RiskOfImpactContent
            ItemDef riskyDiceDef = RiskOfImpactContent.GetRiskyDiceItemDef();
            ItemDef riskCountDef = RiskOfImpactContent.GetRiskyDiceCountItemDef();
            BuffDef riskBuffDef = RiskOfImpactContent.GetRiskyDiceBuffDef();

            if (riskyDiceDef == null || riskCountDef == null || riskBuffDef == null)
            {
                orig(self, activator);
                return;
            }

            int diceStacks = inv.GetItemCountEffective(riskyDiceDef);
            if (diceStacks <= 0)
            {
                orig(self, activator);
                return;
            }

            // Sync UI before rolling
            SyncRiskBuff(body, inv, riskCountDef, riskBuffDef);

            // Batch roll processing
            int riskNow = inv.GetItemCountEffective(riskCountDef);
            int successes = 0;

            bool hitMisfortune = false;
            int riskAtMisfortuneMoment = riskNow;

            for (int i = 0; i < diceStacks; i++)
            {
                int roll = (Run.instance != null)
                    ? Run.instance.treasureRng.RangeInt(0, MisfortuneRollOutOf)
                    : UnityEngine.Random.Range(0, MisfortuneRollOutOf);

                bool misfortune = (roll == MisfortuneRollHit);

                if (DebugLogs)
                    LogI($"[RiskyDice] {self.name} user={body.GetDisplayName()} rollIdx={i + 1}/{diceStacks} riskNow={riskNow} roll={roll}/{MisfortuneRollOutOf - 1} misfortune={misfortune}");

                if (misfortune)
                {
                    riskAtMisfortuneMoment = riskNow; // includes all prior successes
                    hitMisfortune = true;
                    break;
                }

                successes++;
                riskNow++; // risk increases at the moment of each success roll reminder
            }

            if (hitMisfortune)
            {
                // Charge the cost even though we're cancelling the activation.
                SpendCostOnly(self, activator);

                // Feedback (sound + effect)
                Vector3 fxPos = body ? body.corePosition : self.transform.position;
                PlayMisfortuneFeedback(self.gameObject, fxPos);

                ApplyMisfortune(body, inv, riskAtMisfortuneMoment);
                ResetRisk(inv, body, riskCountDef, riskBuffDef);
                ClearPendingChanceBonus(body.master);

                // Cancel purchase (no chest open / no shrine roll)
                return;
            }


            // Commit risk gain (+successes)
            if (successes > 0)
                inv.GiveItemPermanent(riskCountDef, successes);

            // Bonus items = 1 per success roll
            int bonusItems = successes;

            if (bonusItems > 0)
            {
                if (isChest)
                {
                    // Sales Star compatibility: stash and apply during ChestBehavior.Open
                    AddOrSetPendingChestBonus(chest.gameObject, bonusItems);

                    if (DebugLogs)
                        LogI($"[RiskyDice] Pending chest bonus refreshed => +{bonusItems} on {chest.name}");
                }
                else if (isChanceShrine)
                {
                    // Refresh every attempt: overwrite pending
                    SetPendingChanceBonus(body.master, bonusItems);

                    if (DebugLogs)
                        LogI($"[RiskyDice] ShrineChance pending bonus refreshed => +{bonusItems} for {body.GetDisplayName()}");
                }
            }

            // Shrine auto-succeeds if no misfortune
            if (isChanceShrine)
            {
                float oldFailureChance = shrineChance.failureChance;
                shrineChance.failureChance = -1f;

                try
                {
                    orig(self, activator);
                }
                finally
                {
                    shrineChance.failureChance = oldFailureChance;
                }
            }
            else
            {
                orig(self, activator);
            }

            SyncRiskBuff(body, inv, riskCountDef, riskBuffDef);
        }

        private static void AddOrSetPendingChestBonus(GameObject go, int bonus)
        {
            var pcb = go.GetComponent<PendingChestBonus>();
            if (!pcb) pcb = go.AddComponent<PendingChestBonus>();

            // Refresh every attempt: overwrite
            pcb.bonus = bonus;
        }

        private static int ConsumePendingChestBonus(Component obj)
        {
            if (!obj) return 0;
            var pcb = obj.GetComponent<PendingChestBonus>();
            if (!pcb) return 0;

            int b = pcb.bonus;
            pcb.bonus = 0;
            return b;
        }

        // -------------------------------
        // ChestBehavior.Open hook (signature in your assembly: Open(orig_Open, ChestBehavior))
        // -------------------------------
        private static void ChestBehavior_Open(
            On.RoR2.ChestBehavior.orig_Open orig,
            ChestBehavior self)
        {
            if (!NetworkServer.active)
            {
                orig(self);
                return;
            }

            int bonus = ConsumePendingChestBonus(self);
            if (bonus > 0)
            {
                int old = self.dropCount;
                self.dropCount = old + bonus;

                if (DebugLogs)
                    LogI($"[RiskyDice] ChestBehavior.Open: dropCount {old} -> {self.dropCount} (+{bonus}) on {self.name}");

                // IMPORTANT: do NOT restore dropCount.
                // Open() often defers drop spawning; restoring early can erase the bonus.
            }

            orig(self);
        }


        // -------------------------------
        // ShrineChance success hook (additive with Chance Doll)
        // -------------------------------
        private static void ShrineChanceBehavior_AddShrineStack(
            On.RoR2.ShrineChanceBehavior.orig_AddShrineStack orig,
            ShrineChanceBehavior self,
            Interactor activator)
        {
            if (!NetworkServer.active)
            {
                orig(self, activator);
                return;
            }

            int successBefore = self.successfulPurchaseCount;

            CharacterBody body = activator ? activator.GetComponent<CharacterBody>() : null;
            CharacterMaster master = body ? body.master : null;

            orig(self, activator);

            if (!master) return;

            bool succeeded = self.successfulPurchaseCount > successBefore;
            if (!succeeded) return;

            int pending = GetPendingChanceBonus(master);
            if (pending <= 0) return;

            // Spawn extra drops using shrine's exact vanilla droplet logic
            if (self.dropTable != null && self.rng != null && self.dropletOrigin != null)
            {
                Vector3 pos = self.dropletOrigin.position;
                Vector3 vel = self.dropletOrigin.forward * 20f;

                for (int i = 0; i < pending; i++)
                {
                    UniquePickup pickup = self.dropTable.GeneratePickup(self.rng);
                    PickupDropletController.CreatePickupDroplet(pickup, pos, vel);
                }

                if (DebugLogs)
                    LogI($"[RiskyDice] ShrineChance SUCCESS: spawned +{pending} extra drops for {master.GetBody().GetDisplayName()}");
            }

            ClearPendingChanceBonus(master);
        }

        // -------------------------------
        // Misfortune + risk reset
        // -------------------------------
        private static void ApplyMisfortune(CharacterBody body, Inventory inv, int riskAtMoment)
        {
            if (!body || !inv) return;

            if (DebugLogs)
                LogI($"[RiskyDice] MISFORTUNE triggered at risk={riskAtMoment} user={body.GetDisplayName()}");

            inv.GiveItemPermanent(RiskOfImpactContent.GetRiskyDiceAfflictionItemDef(), riskAtMoment);
            
            if (riskAtMoment >= DeathRisk)
            {
                ForceKill(body);
            }
        }
        
        private static void ApplyMisfortuneAffliction(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!body || !body.inventory) return;

            int tokens = body.inventory.GetItemCountPermanent(RiskOfImpactContent.GetRiskyDiceAfflictionItemDef()._itemIndex);
            if (tokens <= 0) return;

            float strength = 0.4f; // Relative to Tonic Affliction (1 = 5%)

            float perStackStatPenalty = 0.05f * strength; // vanilla 5%
            float cursePerStack       = 0.1f  * strength; // vanilla 0.1

            float m = Mathf.Pow(1f - perStackStatPenalty, tokens);

            args.damageTotalMult       *= m;
            args.moveSpeedTotalMult    *= m;
            args.attackSpeedTotalMult  *= m;
            args.regenTotalMult        *= m;
            args.armorTotalMult        *= m;

            args.baseCurseAdd += cursePerStack * tokens;
        }

        private static void ResetRisk(Inventory inv, CharacterBody body, ItemDef riskCountDef, BuffDef riskBuffDef)
        {
            if (riskCountDef != null)
            {
                int cur = inv.GetItemCountPermanent(riskCountDef);
                if (cur > 0) inv.RemoveItemPermanent(riskCountDef, cur);
            }
            SyncRiskBuff(body, inv, riskCountDef, riskBuffDef);
        }

        private static void SyncRiskBuff(CharacterBody body, Inventory inv)
        {
            ItemDef riskItemDef = RiskOfImpactContent.GetRiskyDiceItemDef();
            ItemDef riskCountDef = RiskOfImpactContent.GetRiskyDiceCountItemDef();
            BuffDef riskBuffDef = RiskOfImpactContent.GetRiskyDiceBuffDef();
            SyncRiskBuff(body, inv, riskItemDef, riskCountDef, riskBuffDef);
        }

        private static void SyncRiskBuff(CharacterBody body, Inventory inv, ItemDef riskItemDef, ItemDef riskCountDef,
            BuffDef riskBuffDef)
        {
            if (!NetworkServer.active || !body || !inv) return;
            if (riskItemDef == null || riskCountDef == null || riskBuffDef == null) return;

            int itemCount = inv.GetItemCountEffective(riskItemDef);
            if (itemCount <= 0)
            {
                int risk = inv.GetItemCountPermanent(riskCountDef);
                body.inventory.RemoveItemPermanent(riskCountDef, risk);
                int buff = body.GetBuffCount(riskBuffDef);
                for (int i = 0; i < buff; i++){
                    body.RemoveBuff(riskBuffDef);
                }
            }

            SyncRiskBuff(body, inv, riskCountDef, riskBuffDef);
        }

        private static void SyncRiskBuff(CharacterBody body, Inventory inv, ItemDef riskCountDef, BuffDef riskBuffDef)
        {
            if (!NetworkServer.active || !body || !inv) return;
            if (riskCountDef == null || riskBuffDef == null) return;
            
            
            int risk = inv.GetItemCountEffective(riskCountDef);
            int shown = body.GetBuffCount(riskBuffDef);

            while (shown < risk) { body.AddBuff(riskBuffDef); shown++; }
            while (shown > risk) { body.RemoveBuff(riskBuffDef); shown--; }
        }

        private static void ForceKill(CharacterBody body)
        {
            if (!body || !body.healthComponent) return;

            var hc = body.healthComponent;
            var di = new DamageInfo
            {
                attacker = null,
                inflictor = null,
                crit = false,
                damage = hc.fullCombinedHealth * 1000f,
                position = body.corePosition,
                force = Vector3.zero,
                procCoefficient = 0f,
                damageColorIndex = DamageColorIndex.Void,
                damageType = DamageType.BypassArmor | DamageType.BypassBlock | DamageType.BypassOneShotProtection
            };

            hc.TakeDamage(di);
            hc.Suicide(null, null, DamageType.BypassArmor);
        }

        // -------------------------------
        // Pending Shrine bonus storage (refresh every attempt)
        // -------------------------------
        private static RiskyDiceTracker GetOrAddTracker(CharacterMaster master)
        {
            if (!master) return null;
            var go = master.gameObject;
            var t = go.GetComponent<RiskyDiceTracker>();
            if (!t) t = go.AddComponent<RiskyDiceTracker>();
            return t;
        }

        private static void SetPendingChanceBonus(CharacterMaster master, int bonus)
        {
            var t = GetOrAddTracker(master);
            if (!t) return;

            // Refresh every attempt (overwrite)
            t.pendingChanceShrineBonus = bonus;
        }

        private static int GetPendingChanceBonus(CharacterMaster master)
        {
            var t = GetOrAddTracker(master);
            return t ? t.pendingChanceShrineBonus : 0;
        }

        private static void ClearPendingChanceBonus(CharacterMaster master)
        {
            var t = GetOrAddTracker(master);
            if (!t) return;
            t.pendingChanceShrineBonus = 0;
        }

        // -------------------------------
        // Logging
        // -------------------------------
        private static void LogI(string msg)
        {
            if (RiskOfImpactMain.instance != null) RiskOfImpactMain.LogInfo(msg);
            else Debug.Log(msg);
        }
        
        // Misfortune feedback (safe defaults; change to taste)
        private const string MisfortuneSoundEvent = "Play_UI_menuCancel"; // easy-to-hear, always exists in RoR2
        private const string MisfortuneEffectPrefabPath = "Prefabs/Effects/OmniEffect/OmniImpactVFX"; // if null, we'll skip

        private static void PlayMisfortuneFeedback(GameObject target, Vector3 pos)
        {
            if (target)
                Util.PlaySound(MisfortuneSoundEvent, target);

            var fxPrefab = LegacyResourcesAPI.Load<GameObject>(MisfortuneEffectPrefabPath);
            if (fxPrefab)
            {
                var ed = new EffectData
                {
                    origin = pos,
                    scale = 1.2f
                };
                EffectManager.SpawnEffect(fxPrefab, ed, true);
            }
        }

        /// <summary>
        /// Deduct the cost for this PurchaseInteraction WITHOUT firing the purchase events (so the chest/shrine doesn't proceed).
        /// This mirrors the PayCost block inside PurchaseInteraction.OnInteractionBegin, but stops before onPurchase.
        /// </summary>
        private static void SpendCostOnly(PurchaseInteraction pi, Interactor activator)
        {
            if (!NetworkServer.active || !pi || !activator) return;

            // If they can't afford it, do nothing (vanilla would not let interaction proceed anyway).
            if (!pi.CanBeAffordedByInteractor(activator)) return;

            CharacterBody body = activator.GetComponent<CharacterBody>();
            CostTypeDef costTypeDef = CostTypeCatalog.GetCostTypeDef(pi.costType);

            int adjustedCost = TeamManager.AdjustCostForLongstandingSolitude(pi.costType, pi.cost, body);

            CostTypeDef.PayCostContext ctx;
            using (CostTypeDef.PayCostContext.pool.Request(out ctx))
            {
                ctx.activator = activator;
                ctx.activatorBody = body;
                ctx.activatorMaster = body ? body.master : null;
                ctx.activatorInventory = body ? body.inventory : null;
                ctx.purchasedObject = pi.gameObject;
                ctx.purchaseInteraction = pi;
                ctx.costTypeDef = costTypeDef;
                ctx.cost = adjustedCost;
                ctx.rng = pi.rng; // should be set in PurchaseInteraction.Awake on server
                ctx.avoidedItemIndex = ItemIndex.None; // we’re not a shop terminal, ignore

                CostTypeDef.PayCostResults results;
                using (CostTypeDef.PayCostResults.pool.Request(out results))
                {
                    // This actually removes money/items/etc.
                    costTypeDef.PayCost(ctx, results);

                    // IMPORTANT: Do NOT invoke pi.onPurchase / pi.onDetailedPurchaseServer / global purchase hooks.
                    // We want the player to pay, but the interactable NOT to activate.
                    pi.lastActivator = activator;
                }
            }
        }

    }
}
