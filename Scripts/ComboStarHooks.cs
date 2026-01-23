using System.Collections.Generic;
using RoR2;
using UnityEngine;
using R2API;
using RoR2.Skills;
using static RiskOfImpact.RiskOfImpactMain;

namespace RiskOfImpact
{
    public static class ComboStarHooks
    {
        public const int BaseMaxStacks = 20;       // 20 at 1 item
        public const int ExtraStacksPerItem = 10;  // +10 max stacks per extra item
        public const float SkillHitTimeout = 5f;

        public static void Init()
        {
            LogInfo("[ComboStar] Init: registering hooks.");

            CharacterBody.onBodyStartGlobal += OnBodyStart;
            On.RoR2.GlobalEventManager.OnHitEnemy += GlobalEventManager_OnHitEnemy;
            On.RoR2.GenericSkill.OnExecute += GenericSkill_OnExecute;
        }

        private static bool SkillCountsForCombo(GenericSkill skill)
        {
            if (!skill || skill.skillDef == null)
            {
                LogDebug("[ComboStar] SkillCountsForCombo: skill or skillDef is null -> false");
                return false;
            }

            SkillDef def = skill.skillDef;
            string token = def.skillNameToken;

            // Hard blacklist for setup / stance skills that shouldn't affect Combo Star
            switch (token)
            {
                case "RAILGUNNER_SECONDARY_NAME":
                    LogDebug($"[ComboStar] SkillCountsForCombo: {token} is blacklisted (setup skill) -> false");
                    return false;
                case "RAILGUNNER_SPECIAL_NAME":
                    LogDebug($"[ComboStar] SkillCountsForCombo: {token} is blacklisted (setup skill) -> false");
                    return false;
            }
            
            if (token != null && token.StartsWith("CHEESEWITHHOLES_BASICTANK_BODY_UTILITY"))
            {
                LogDebug($"[ComboStar] SkillCountsForCombo: {token} is exempt (tank utility) -> false");
                return false;
            }

            if (!def.isCombatSkill)
            {
                LogDebug($"[ComboStar] SkillCountsForCombo: {token} isCombatSkill=false -> false");
                return false;
            }

            // For now, any combat skill not blacklisted counts
            LogDebug($"[ComboStar] SkillCountsForCombo: {token} isCombatSkill=true -> true");
            return true;
        }


        private static void OnBodyStart(CharacterBody body)
        {
            if (!body.GetComponent<ComboStarTracker>())
            {
                body.gameObject.AddComponent<ComboStarTracker>();
            }
        }

        private static void GlobalEventManager_OnHitEnemy(
            On.RoR2.GlobalEventManager.orig_OnHitEnemy orig,
            GlobalEventManager self,
            DamageInfo damageInfo,
            GameObject victim)
        {
            orig(self, damageInfo, victim);

            if (!damageInfo.attacker)
            {
                LogDebug("[ComboStar] OnHitEnemy: attacker is null, ignoring.");
                return;
            }

            if (damageInfo.rejected || damageInfo.damage <= 0f)
            {
                LogDebug($"[ComboStar] OnHitEnemy: hit rejected or zero damage (rejected={damageInfo.rejected}, damage={damageInfo.damage}), ignoring.");
                return;
            }

            if ((damageInfo.damageType & DamageType.DoT) != 0)
            {
                LogDebug("[ComboStar] OnHitEnemy: DoT damage type, ignoring for Combo Star.");
                return;
            }

            var attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
            if (!attackerBody)
            {
                LogDebug("[ComboStar] OnHitEnemy: attacker has no CharacterBody, ignoring.");
                return;
            }

            var tracker = attackerBody.GetComponent<ComboStarTracker>();
            if (tracker != null)
            {
                LogDebug($"[ComboStar] OnHitEnemy: registering hit for body={attackerBody.GetDisplayName()}, damage={damageInfo.damage}");
                tracker.RegisterHit();
            }
            else
            {
                LogDebug($"[ComboStar] OnHitEnemy: no ComboStarTracker found on {attackerBody.GetDisplayName()}");
            }
        }

        private static void GenericSkill_OnExecute(
            On.RoR2.GenericSkill.orig_OnExecute orig,
            GenericSkill self)
        {
            var body = self.characterBody;

            if (body && SkillCountsForCombo(self))
            {
                var tracker = body.GetComponent<ComboStarTracker>();
                if (tracker != null)
                {
                    LogDebug($"[ComboStar] GenericSkill_OnExecute: eligible skill executed. body={body.GetDisplayName()}, skill={self.skillDef.skillNameToken}");
                    tracker.OnEligibleSkillExecuted(self);
                }
                else
                {
                    LogDebug($"[ComboStar] GenericSkill_OnExecute: no ComboStarTracker on body={body.GetDisplayName()}");
                }
            }

            orig(self);
        }
    }

    public class ComboStarTracker : MonoBehaviour
    {
        private CharacterBody body;

        private struct Bucket
        {
            public float expiresAt;   // when this bucket “fails”
            public bool hadHit;       // whether any hit happened in this bucket
        }

        private readonly Queue<Bucket> buckets = new Queue<Bucket>();

        private float currentBucketStartTime = -999f;
        private bool stackGrantedThisSkill;
        private const float BucketSeconds = 0.20f; // your old MergeWindowSeconds

        // --- Adaptive timeout tracking ---
        private float lastEligibleExecuteTime = -1f;
        private float ewmaExecuteInterval = 0.15f;  // start near BucketSeconds
        private const float ExecuteIntervalAlpha = 0.20f; // EWMA smoothing (0..1)

        // --- Adaptive timeout tuning (active combo only) ---
        private const float ActiveMinTimeout = 0.35f;      // fastest reset floor
        private const float ActiveMaxTimeout = 2.0f;       // cap while active (still <= SkillHitTimeout)
        private const float ActiveBaseTimeout = 0.10f;     // baseline latency allowance
        private const float ActiveIntervalMultiplier = 4f; // allow ~4 execute intervals before breaking

        // --- Delayed hit expectation (for long-latency combat skills) ---
        private const float DelayedSkillHitTimeout = 10f; // buffer window
        private bool delayedPending;
        private float delayedPendingExpiresAt;
        private string delayedPendingToken;

        // Put delayed-latency skill tokens (or prefixes) here.
        // If you prefer to keep this in ComboStarHooks, you can move it there and call that instead.
        private static readonly HashSet<string> DelayedSkillTokens = new HashSet<string>
        {
            "ENGI_PRIMARY_NAME",
            "CHEESEWITHHOLES_BASICTANK_BODY_SECONDARY_OBLITERATOR_CANNON_NAME",
        };

        private static bool SkillIsDelayedHit(SkillDef def)
        {
            if (!def) return false;
            string token = def.skillNameToken;

            if (!string.IsNullOrEmpty(token) && DelayedSkillTokens.Contains(token))
                return true;

            return false;
        }

        private void Awake()
        {
            body = GetComponent<CharacterBody>();
        }

        private void OnEnable()
        {
            ResetCombo("OnEnable");
        }

        private void OnDisable()
        {
            ResetCombo("OnDisable");
        }

        private float ComputeBucketTimeoutSeconds(bool comboActive)
        {
            if (!comboActive)
            {
                // Keep original behavior for starting/low-stacks: don’t drop buckets too early.
                return ComboStarHooks.SkillHitTimeout;
            }

            // Dynamic timeout: base + multiplier * average execute interval
            float t = ActiveBaseTimeout + ActiveIntervalMultiplier * ewmaExecuteInterval;

            // Clamp so it’s snappy in streams but not absurdly short.
            t = Mathf.Clamp(t, ActiveMinTimeout, Mathf.Min(ActiveMaxTimeout, ComboStarHooks.SkillHitTimeout));
            return t;
        }

        public void OnEligibleSkillExecuted(GenericSkill skill)
        {
            if (!body || !body.inventory) return;

            int itemCount = body.inventory.GetItemCountEffective(RiskOfImpactContent.GetComboStarItemDef());
            if (itemCount <= 0) return;

            BuffDef buff = RiskOfImpactContent.GetComboStarBuffDef();
            int stacks = body.GetBuffCount(buff);

            float now = Time.time;

            // Update activation cadence EWMA
            if (lastEligibleExecuteTime > 0f)
            {
                float dt = now - lastEligibleExecuteTime;
                // Ignore ridiculous gaps/spikes (prevents one pause from blowing up the average)
                if (dt > 0f && dt < 1.0f)
                    ewmaExecuteInterval = Mathf.Lerp(ewmaExecuteInterval, dt, ExecuteIntervalAlpha);
            }
            lastEligibleExecuteTime = now;

            // If this is a delayed-latency combat skill, arm a "must see a hit within N seconds" window.
            // Do NOT enqueue a normal bucket for it, or that un-hit bucket can expire later and reset you incorrectly.
            if (skill && skill.skillDef && SkillIsDelayedHit(skill.skillDef))
            {
                if (stacks > 0) // only meaningful once combo is active
                {
                    delayedPending = true;
                    delayedPendingExpiresAt = now + DelayedSkillHitTimeout;
                    delayedPendingToken = skill.skillDef.skillNameToken;

                    LogDebug($"[ComboStarTracker] Delayed skill pending window started. body={body.GetDisplayName()}, token={delayedPendingToken}, expiresAt={delayedPendingExpiresAt:0.00}");
                }

                // still one stack per execute
                stackGrantedThisSkill = false;
                return;
            }

            // Start a new bucket if we’re outside the bucket window
            if (buckets.Count == 0 || (now - currentBucketStartTime) > BucketSeconds)
            {
                currentBucketStartTime = now;

                bool comboActive = stacks > 0;
                float timeout = ComputeBucketTimeoutSeconds(comboActive);

                buckets.Enqueue(new Bucket
                {
                    hadHit = false,
                    expiresAt = now + timeout
                });

                LogDebug($"[ComboStarTracker] New bucket started. body={body.GetDisplayName()}, stacks={stacks}, buckets={buckets.Count}");
            }
            else
            {
                LogDebug($"[ComboStarTracker] Using existing bucket. body={body.GetDisplayName()}, stacks={stacks}, buckets={buckets.Count}");
            }

            // still one stack per execute
            stackGrantedThisSkill = false;
        }

        public void RegisterHit()
        {
            if (!body || !body.inventory) return;

            int itemCount = body.inventory.GetItemCountEffective(RiskOfImpactContent.GetComboStarItemDef());
            if (itemCount <= 0) return;

            // Any hit satisfies a delayed window (the delayed skill doesn't need direct attribution).
            if (delayedPending)
            {
                delayedPending = false;
                LogDebug($"[ComboStarTracker] Delayed skill window satisfied by hit. body={body.GetDisplayName()}, token={delayedPendingToken}");
            }

            if (buckets.Count > 0)
            {
                // Mark the newest bucket as hit (we need to edit it in-place, so rotate queue)
                int n = buckets.Count;
                Bucket last = default;

                for (int i = 0; i < n; i++)
                {
                    var b = buckets.Dequeue();
                    if (i == n - 1) last = b;
                    else buckets.Enqueue(b);
                }

                last.hadHit = true;
                buckets.Enqueue(last);
            }

            BuffDef buff = RiskOfImpactContent.GetComboStarBuffDef();
            int currentStacks = body.GetBuffCount(buff);

            if (stackGrantedThisSkill) return;

            int maxStacks = ComboStarHooks.BaseMaxStacks +
                            ComboStarHooks.ExtraStacksPerItem * (itemCount - 1);

            BuffDef maxBuff = RiskOfImpactContent.GetComboStarMaxBuffDef();

            if (currentStacks < maxStacks)
            {
                body.AddBuff(buff);
                currentStacks += 1;
                stackGrantedThisSkill = true;
                if (currentStacks == maxStacks)
                {
                    body.AddBuff(maxBuff);
                }
            }
        }

        private void FixedUpdate()
        {
            if (!body || !body.inventory) return;

            int itemCount = body.inventory.GetItemCountEffective(RiskOfImpactContent.GetComboStarItemDef());
            BuffDef buff = RiskOfImpactContent.GetComboStarBuffDef();
            int stacks = body.GetBuffCount(buff);
            int maxStacks = ComboStarHooks.BaseMaxStacks +
                            ComboStarHooks.ExtraStacksPerItem * (itemCount - 1);

            if (itemCount <= 0)
            {
                if (stacks > 0) ResetCombo("Lost all items");
                return;
            }

            // Expire delayed expectation (only matters when combo is active)
            if (delayedPending && stacks > 0 && Time.time >= delayedPendingExpiresAt)
            {
                LogInfo($"[ComboStarTracker] Delayed skill window expired without hit; resetting combo. body={body.GetDisplayName()}, token={delayedPendingToken}");
                ResetCombo("Delayed skill hit window expired");
                return;
            }

            while (buckets.Count > 0 && Time.time >= buckets.Peek().expiresAt)
            {
                var expired = buckets.Dequeue();

                if (stacks > 0 && !expired.hadHit)
                {
                    LogInfo($"[ComboStarTracker] Bucket expired without hit, resetting combo. body={body.GetDisplayName()}");
                    ResetCombo("Bucket expired without hit");
                    return;
                }
            }

            BuffDef maxBuff = RiskOfImpactContent.GetComboStarMaxBuffDef();
            int maxBuffs = body.GetBuffCount(maxBuff);

            if (stacks < maxStacks && maxBuffs > 0)
            {
                body.RemoveBuff(maxBuff);
            }
        }

        private void ResetCombo(string reason)
        {
            if (!body) return;

            BuffDef buff = RiskOfImpactContent.GetComboStarBuffDef();
            int stacks = body.GetBuffCount(buff);

            if (stacks > 0)
            {
                LogInfo($"[ComboStarTracker] ResetCombo: reason={reason}, body={body.GetDisplayName()}, removing {stacks} stacks.");
                for (int i = 0; i < stacks; i++)
                    body.RemoveBuff(buff);

                BuffDef maxBuff = RiskOfImpactContent.GetComboStarMaxBuffDef();
                int maxBuffs = body.GetBuffCount(maxBuff);
                if (maxBuffs > 0)
                {
                    body.RemoveBuff(maxBuff);
                }
            }
            else
            {
                LogDebug($"[ComboStarTracker] ResetCombo: reason={reason}, body={body.GetDisplayName()}, no stacks to remove.");
            }

            buckets.Clear();
            currentBucketStartTime = -999f;

            // Clear delayed state too
            delayedPending = false;
            delayedPendingExpiresAt = 0f;
            delayedPendingToken = null;
        }
    }


}
