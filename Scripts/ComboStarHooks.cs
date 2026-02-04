﻿using System.Collections.Generic;
using RoR2;
using UnityEngine;
using R2API;
using RoR2.Skills;
using UnityEngine.Networking;
using static RiskOfImpact.RiskOfImpactMain;

// R2API Networking
using R2API.Networking;
using R2API.Networking.Interfaces;

namespace RiskOfImpact
{
    public static class ComboStarHooks
    {
        public const int BaseMaxStacks = 20;       // 20 at 1 item
        public const int ExtraStacksPerItem = 10;  // +10 max stacks per extra item
        public const float SkillHitTimeout = 5f;

        // Toggle to spam extra logs
        public static bool VerboseLogs = true;

        public static void Init()
        {
            LogInfo("[ComboStar] Init: registering hooks + net messages.");

            // Net message registration (client -> server)
            NetworkingAPI.RegisterMessageType<ComboStarSkillExecuteMessage>();

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

            LogDebug($"[ComboStar] SkillCountsForCombo: {token} isCombatSkill=true -> true");
            return true;
        }

        private static void OnBodyStart(CharacterBody body)
        {
            // Only the server should own combo state + timers
            if (!NetworkServer.active) return;

            if (!body) return;

            if (!body.GetComponent<ComboStarTracker>())
            {
                body.gameObject.AddComponent<ComboStarTracker>();

                if (VerboseLogs)
                {
                    var ni = body.GetComponent<NetworkIdentity>();
                    LogInfo($"[ComboStar] OnBodyStart: Added tracker on SERVER. body={body.GetDisplayName()} netId={(ni ? ni.netId.ToString() : "noNetId")}");
                }
            }
        }

        private static void GlobalEventManager_OnHitEnemy(
            On.RoR2.GlobalEventManager.orig_OnHitEnemy orig,
            GlobalEventManager self,
            DamageInfo damageInfo,
            GameObject victim)
        {
            orig(self, damageInfo, victim);

            if (!NetworkServer.active) return;

            if (!damageInfo.attacker)
            {
                if (VerboseLogs) LogDebug("[ComboStar] OnHitEnemy(SERVER): attacker is null, ignoring.");
                return;
            }

            if (damageInfo.rejected || damageInfo.damage <= 0f)
            {
                if (VerboseLogs) LogDebug($"[ComboStar] OnHitEnemy(SERVER): rejected or zero damage (rejected={damageInfo.rejected}, damage={damageInfo.damage}), ignoring.");
                return;
            }

            if ((damageInfo.damageType & DamageType.DoT) != 0)
            {
                if (VerboseLogs) LogDebug("[ComboStar] OnHitEnemy(SERVER): DoT damage type, ignoring.");
                return;
            }

            var attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
            if (!attackerBody)
            {
                if (VerboseLogs) LogDebug("[ComboStar] OnHitEnemy(SERVER): attacker has no CharacterBody, ignoring.");
                return;
            }

            var tracker = attackerBody.GetComponent<ComboStarTracker>();
            if (tracker != null)
            {
                if (VerboseLogs)
                {
                    var ni = attackerBody.GetComponent<NetworkIdentity>();
                    LogDebug($"[ComboStar] OnHitEnemy(SERVER): RegisterHit body={attackerBody.GetDisplayName()} netId={(ni ? ni.netId.ToString() : "noNetId")} dmg={damageInfo.damage:0.0}");
                }
                tracker.RegisterHit();
            }
            else
            {
                if (VerboseLogs) LogDebug($"[ComboStar] OnHitEnemy(SERVER): NO tracker found on {attackerBody.GetDisplayName()}");
            }
        }

        private static void GenericSkill_OnExecute(
            On.RoR2.GenericSkill.orig_OnExecute orig,
            GenericSkill self)
        {
            orig(self);

            if (!self) return;

            var body = self.characterBody;
            if (!body) return;

            // Only consider eligible skills
            if (!SkillCountsForCombo(self)) return;

            // Resolve token for logging/message
            string token = self.skillDef ? self.skillDef.skillNameToken : "<null_token>";

            var ni = body.GetComponent<NetworkIdentity>();
            string netIdStr = ni ? ni.netId.ToString() : "noNetId";

            // SERVER PATH: host/server runs state directly
            if (NetworkServer.active)
            {
                var tracker = body.GetComponent<ComboStarTracker>();
                if (tracker != null)
                {
                    if (VerboseLogs) LogDebug($"[ComboStar] GenericSkill_OnExecute(SERVER): body={body.GetDisplayName()} netId={netIdStr} token={token}");
                    tracker.OnEligibleSkillExecutedToken(token);
                }
                else
                {
                    if (VerboseLogs) LogDebug($"[ComboStar] GenericSkill_OnExecute(SERVER): NO tracker on body={body.GetDisplayName()} netId={netIdStr}");
                }

                return;
            }

            // CLIENT PATH: for remote players, this often fires ONLY on the owning client.
            // If we have authority, notify server so it can run the bucket logic.
            if (NetworkClient.active && body.hasAuthority && ni)
            {
                if (VerboseLogs) LogDebug($"[ComboStar] GenericSkill_OnExecute(CLIENT->SERVER SEND): body={body.GetDisplayName()} netId={netIdStr} token={token}");

                new ComboStarSkillExecuteMessage(ni.netId, token).Send(NetworkDestination.Server);
            }
            else
            {
                if (VerboseLogs)
                {
                    LogDebug($"[ComboStar] GenericSkill_OnExecute(CLIENT ignored): body={body.GetDisplayName()} netId={netIdStr} " +
                             $"NetworkClient.active={NetworkClient.active} hasAuthority={body.hasAuthority} hasNetId={(ni != null)} token={token}");
                }
            }
        }
    }

    // Client -> Server message: "I executed an eligible combat skill"
    public class ComboStarSkillExecuteMessage : INetMessage
    {
        private NetworkInstanceId bodyNetId;
        private string skillToken;

        // Empty ctor required
        public ComboStarSkillExecuteMessage() { }

        public ComboStarSkillExecuteMessage(NetworkInstanceId bodyNetId, string skillToken)
        {
            this.bodyNetId = bodyNetId;
            this.skillToken = skillToken;
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(bodyNetId);
            writer.Write(skillToken ?? "");
        }

        public void Deserialize(NetworkReader reader)
        {
            bodyNetId = reader.ReadNetworkId();
            skillToken = reader.ReadString();
        }

        public void OnReceived()
        {
            // Server should be the only place this message is applied
            if (!NetworkServer.active) return;

            var go = Util.FindNetworkObject(bodyNetId);
            if (!go)
            {
                RiskOfImpactMain.LogDebug($"[ComboStarNet] OnReceived(SERVER): Could not find body object for netId={bodyNetId}");
                return;
            }

            var body = go.GetComponent<CharacterBody>();
            if (!body)
            {
                RiskOfImpactMain.LogDebug($"[ComboStarNet] OnReceived(SERVER): netId={bodyNetId} has no CharacterBody");
                return;
            }

            var tracker = body.GetComponent<ComboStarTracker>();
            if (!tracker)
            {
                RiskOfImpactMain.LogDebug($"[ComboStarNet] OnReceived(SERVER): netId={bodyNetId} body={body.GetDisplayName()} has NO tracker");
                return;
            }

            if (ComboStarHooks.VerboseLogs)
            {
                RiskOfImpactMain.LogDebug($"[ComboStarNet] OnReceived(SERVER): Apply skill execute. body={body.GetDisplayName()} netId={bodyNetId} token={skillToken}");
            }

            tracker.OnEligibleSkillExecutedToken(skillToken);
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

        private static readonly HashSet<string> DelayedSkillTokens = new HashSet<string>
        {
            "ENGI_PRIMARY_NAME",
            "CHEESEWITHHOLES_BASICTANK_BODY_SECONDARY_OBLITERATOR_CANNON_NAME",
        };

        private static bool SkillIsDelayedHitToken(string token)
        {
            return !string.IsNullOrEmpty(token) && DelayedSkillTokens.Contains(token);
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
                return ComboStarHooks.SkillHitTimeout;

            float t = ActiveBaseTimeout + ActiveIntervalMultiplier * ewmaExecuteInterval;
            t = Mathf.Clamp(t, ActiveMinTimeout, Mathf.Min(ActiveMaxTimeout, ComboStarHooks.SkillHitTimeout));
            return t;
        }

        // New: server-side entry point that doesn’t require a GenericSkill reference
        public void OnEligibleSkillExecutedToken(string token)
        {
            if (!NetworkServer.active) return;
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
                if (dt > 0f && dt < 1.0f)
                    ewmaExecuteInterval = Mathf.Lerp(ewmaExecuteInterval, dt, ExecuteIntervalAlpha);
            }
            lastEligibleExecuteTime = now;

            // Delayed-latency skill window
            if (SkillIsDelayedHitToken(token))
            {
                if (stacks > 0)
                {
                    delayedPending = true;
                    delayedPendingExpiresAt = now + DelayedSkillHitTimeout;
                    delayedPendingToken = token;

                    if (ComboStarHooks.VerboseLogs)
                        RiskOfImpactMain.LogDebug($"[ComboStarTracker] Delayed pending started. body={body.GetDisplayName()}, token={token}, expiresAt={delayedPendingExpiresAt:0.00}");
                }

                stackGrantedThisSkill = false;
                return;
            }

            // Bucket create / reuse
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

                if (ComboStarHooks.VerboseLogs)
                    RiskOfImpactMain.LogDebug($"[ComboStarTracker] New bucket. body={body.GetDisplayName()}, token={token}, stacks={stacks}, buckets={buckets.Count}, expiresAt={(now + timeout):0.00}");
            }
            else
            {
                if (ComboStarHooks.VerboseLogs)
                    RiskOfImpactMain.LogDebug($"[ComboStarTracker] Reuse bucket. body={body.GetDisplayName()}, token={token}, stacks={stacks}, buckets={buckets.Count}");
            }

            // Important: this is what was missing for remote players on server
            stackGrantedThisSkill = false;
        }

        public void RegisterHit()
        {
            if (!NetworkServer.active) return;

            if (!body || !body.inventory) return;

            int itemCount = body.inventory.GetItemCountEffective(RiskOfImpactContent.GetComboStarItemDef());
            if (itemCount <= 0) return;

            // Any hit satisfies delayed window
            if (delayedPending)
            {
                delayedPending = false;

                if (ComboStarHooks.VerboseLogs)
                    RiskOfImpactMain.LogDebug($"[ComboStarTracker] Delayed window satisfied by hit. body={body.GetDisplayName()}, token={delayedPendingToken}");
            }

            // Mark newest bucket hit
            if (buckets.Count > 0)
            {
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
            else
            {
                if (ComboStarHooks.VerboseLogs)
                    RiskOfImpactMain.LogDebug($"[ComboStarTracker] RegisterHit: NO buckets (hit happened without execute tracking). body={body.GetDisplayName()}");
            }

            BuffDef buff = RiskOfImpactContent.GetComboStarBuffDef();
            int currentStacks = body.GetBuffCount(buff);

            if (stackGrantedThisSkill)
            {
                if (ComboStarHooks.VerboseLogs)
                    RiskOfImpactMain.LogDebug($"[ComboStarTracker] RegisterHit: stack already granted this skill. body={body.GetDisplayName()} stacks={currentStacks}");
                return;
            }

            int maxStacks = ComboStarHooks.BaseMaxStacks +
                            ComboStarHooks.ExtraStacksPerItem * (itemCount - 1);

            BuffDef maxBuff = RiskOfImpactContent.GetComboStarMaxBuffDef();

            if (currentStacks < maxStacks)
            {
                body.AddBuff(buff);
                currentStacks += 1;
                stackGrantedThisSkill = true;

                if (ComboStarHooks.VerboseLogs)
                    RiskOfImpactMain.LogDebug($"[ComboStarTracker] +STACK. body={body.GetDisplayName()} stacksNow={currentStacks}/{maxStacks}");

                if (currentStacks == maxStacks)
                {
                    body.AddBuff(maxBuff);

                    if (ComboStarHooks.VerboseLogs)
                        RiskOfImpactMain.LogDebug($"[ComboStarTracker] MAX reached. body={body.GetDisplayName()} added max buff.");
                }
            }
        }

        private void FixedUpdate()
        {
            // This component only exists server-side because you add it only on server.
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

            // Expire delayed expectation
            if (delayedPending && stacks > 0 && Time.time >= delayedPendingExpiresAt)
            {
                RiskOfImpactMain.LogInfo($"[ComboStarTracker] Delayed expired -> reset. body={body.GetDisplayName()}, token={delayedPendingToken}");
                ResetCombo("Delayed skill hit window expired");
                return;
            }

            // Bucket expiration
            while (buckets.Count > 0 && Time.time >= buckets.Peek().expiresAt)
            {
                var expired = buckets.Dequeue();

                if (stacks > 0 && !expired.hadHit)
                {
                    RiskOfImpactMain.LogInfo($"[ComboStarTracker] Bucket expired without hit -> reset. body={body.GetDisplayName()}");
                    ResetCombo("Bucket expired without hit");
                    return;
                }
            }

            BuffDef maxBuff = RiskOfImpactContent.GetComboStarMaxBuffDef();
            int maxBuffs = body.GetBuffCount(maxBuff);

            if (stacks < maxStacks && maxBuffs > 0)
            {
                body.RemoveBuff(maxBuff);

                if (ComboStarHooks.VerboseLogs)
                    RiskOfImpactMain.LogDebug($"[ComboStarTracker] Removed max buff because stacks dropped. body={body.GetDisplayName()} stacks={stacks}/{maxStacks}");
            }
        }

        private void ResetCombo(string reason)
        {
            if (!body) return;

            BuffDef buff = RiskOfImpactContent.GetComboStarBuffDef();
            int stacks = body.GetBuffCount(buff);

            if (stacks > 0)
            {
                RiskOfImpactMain.LogInfo($"[ComboStarTracker] ResetCombo: reason={reason}, body={body.GetDisplayName()}, removing {stacks} stacks.");
                for (int i = 0; i < stacks; i++)
                    body.RemoveBuff(buff);

                BuffDef maxBuff = RiskOfImpactContent.GetComboStarMaxBuffDef();
                int maxBuffs = body.GetBuffCount(maxBuff);
                if (maxBuffs > 0)
                    body.RemoveBuff(maxBuff);
            }
            else
            {
                if (ComboStarHooks.VerboseLogs)
                    RiskOfImpactMain.LogDebug($"[ComboStarTracker] ResetCombo: reason={reason}, body={body.GetDisplayName()}, no stacks to remove.");
            }

            buckets.Clear();
            currentBucketStartTime = -999f;

            delayedPending = false;
            delayedPendingExpiresAt = 0f;
            delayedPendingToken = null;

            stackGrantedThisSkill = false;
        }
    }
}
