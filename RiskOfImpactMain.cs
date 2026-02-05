using BepInEx;
using System.IO;
using UnityEngine;
using System.Security.Permissions;
using System.Security;
using BepInEx.Bootstrap;
using System.Runtime.CompilerServices;


[module: UnverifiableCode]
#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace RiskOfImpact
{
    #region Dependencies
    [BepInDependency("___riskofthunder.RoR2BepInExPack")]
    [BepInDependency("com.bepis.r2api")]
    [BepInDependency("com.bepis.r2api.items")]
    [BepInDependency("com.bepis.r2api.language")]
    [BepInDependency("com.bepis.r2api.recalculatestats")]
    [BepInDependency("droppod.lookingglass", BepInDependency.DependencyFlags.SoftDependency)]
    #endregion
    [BepInPlugin(GUID, MODNAME, VERSION)]
    public class RiskOfImpactMain : BaseUnityPlugin
    {
        public const string GUID = "com.MrE42.RiskOfImpact";
        public const string MODNAME = "Risk Of Impact";
        public const string VERSION = "0.2.0";

        public static PluginInfo pluginInfo { get; private set; }
        public static RiskOfImpactMain instance { get; private set; }
        internal static AssetBundle assetBundle { get; private set; }
        internal static string assetBundleDir => Path.Combine(Path.GetDirectoryName(pluginInfo.Location), "RiskOfImpactAssets");
        
        public static bool IsLookingGlassInstalled =>
            Chainloader.PluginInfos.ContainsKey("droppod.lookingglass");

        private void Awake()
        {
            instance = this;
            pluginInfo = Info;
            Debug.Log("[RiskOfImpactMain] Awake: Initializing mod...");
            new RiskOfImpactContent();
            Debug.Log("[RiskOfImpactMain] Content loaded. Initializing Equipment Hook...");
            if (IsLookingGlassInstalled)
            {
                LookingGlassCompat.Init();
            }
            LanceOfLonginusEquipmentHook.Init();
            ComboStarHooks.Init();
            RedshifterHooks.Init();
            BioticShellHooks.Init();
            DoomedMoonHooks.Init();
            //StartItemTester.Init();
        }
        internal static void LogFatal(object data) { instance.Logger.LogFatal(data); }
        internal static void LogError(object data) { instance.Logger.LogError(data); }
        internal static void LogWarning(object data) { instance.Logger.LogWarning(data); }
        internal static void LogMessage(object data) { instance.Logger.LogMessage(data); }
        internal static void LogInfo(object data) { instance.Logger.LogInfo(data); }
        internal static void LogDebug(object data) { instance.Logger.LogDebug(data); }
    }
}
