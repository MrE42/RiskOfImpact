using RoR2.ContentManagement;
using UnityEngine;
using RoR2;
using System.Collections;
using RoR2.ExpansionManagement;
using R2API;
using RoR2.Projectile;

namespace RiskOfImpact
{
    public class RiskOfImpactContent : IContentPackProvider
    {
        public string identifier => RiskOfImpactMain.GUID;
        public static ReadOnlyContentPack readOnlyContentPack => new ReadOnlyContentPack(RiskOfImpactContentPack);
        internal static ContentPack RiskOfImpactContentPack { get; } = new ContentPack();

        private static ItemDef _mug;
        private static EquipmentDef _lanceEquipmentDef;
        private static AssetBundle _myBundle;
        private static GameObject _lanceProjectilePrefab;
        private static GameObject _lanceGhost;

                
        private static ItemDef _comboStarItem;
        private static BuffDef _comboStarBuff;
        private static BuffDef _comboStarMaxBuff;
        private static GameObject _comboStarDisplayPrefab;
        private static GameObject _mugDisplayPrefab;
        
        private static ItemDef _redshifter;
        private static GameObject _redshifterDisplayPrefab;
        private static GameObject _redshifterDisplayFollowerPrefab;
        
        private static ItemDef _bioticShell;
        private static GameObject _bioticShellDisplayPrefab;

        private static ItemDef _doomedMoon;
        private static GameObject _doomedMoonDisplayPrefab;
        private static GameObject _doomedMoonDisplayFollowerPrefab;
        private static ItemDef _doomedMoonConsumed;
        private static ItemDef _doomedMoonStatToken;
        private static BuffDef _doomedMoonBuff;

        
        private static ItemDef _riskyDice;
        private static ItemDef _riskyDiceCount;
        private static ItemDef _riskyDiceAffliction;
        private static BuffDef _riskyDiceBuff;
        
        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            // Load the asset bundle from disk
            var asyncOperation = AssetBundle.LoadFromFileAsync(RiskOfImpactMain.assetBundleDir);
            while (!asyncOperation.isDone)
            {
                args.ReportProgress(asyncOperation.progress);
                yield return null;
            }

            _myBundle = asyncOperation.assetBundle;

            // Load your custom assets from the bundle
            _mug = _myBundle.LoadAsset<ItemDef>("Mug");
            _mugDisplayPrefab = _myBundle.LoadAsset<GameObject>("PickupDD");
            _comboStarItem = _myBundle.LoadAsset<ItemDef>("ComboStar");
            _comboStarBuff = _myBundle.LoadAsset<BuffDef>("ComboStarBuff");
            _comboStarMaxBuff = _myBundle.LoadAsset<BuffDef>("MaxStarBuff");
            _comboStarDisplayPrefab = _myBundle.LoadAsset<GameObject>("PickupCS");
            _redshifter = _myBundle.LoadAsset<ItemDef>("Redshifter");
            _redshifterDisplayPrefab = _myBundle.LoadAsset<GameObject>("PlayerDisplayRS");
            _redshifterDisplayFollowerPrefab = _myBundle.LoadAsset<GameObject>("DisplayFollowerRS");
            _bioticShell = _myBundle.LoadAsset<ItemDef>("BioticShell");
            _bioticShell.requiredExpansion =
                UnityEngine.AddressableAssets.Addressables
                    .LoadAssetAsync<RoR2.ExpansionManagement.ExpansionDef>("RoR2/DLC1/Common/DLC1.asset")
                    .WaitForCompletion();
            _bioticShellDisplayPrefab = _myBundle.LoadAsset<GameObject>("PickupBS");
            
            _doomedMoon = _myBundle.LoadAsset<ItemDef>("DoomedMoon");
            _doomedMoonDisplayPrefab = _myBundle.LoadAsset<GameObject>("PlayerDisplayDM");
            _doomedMoonDisplayFollowerPrefab = _myBundle.LoadAsset<GameObject>("DisplayFollowerDM");
            _doomedMoonConsumed = _myBundle.LoadAsset<ItemDef>("DoomedMoonConsumed");
            _doomedMoonStatToken = _myBundle.LoadAsset<ItemDef>("DoomedMoonStatToken");
            _doomedMoonBuff = _myBundle.LoadAsset<BuffDef>("DoomedMoonBuff");
            
            _riskyDice = _myBundle.LoadAsset<ItemDef>("RiskyDice");
            _riskyDiceCount = _myBundle.LoadAsset<ItemDef>("RiskyDiceCount");
            _riskyDiceAffliction = _myBundle.LoadAsset<ItemDef>("RiskyDiceAffliction");
            _riskyDiceBuff = _myBundle.LoadAsset<BuffDef>("RiskyDiceBuff");

            _lanceEquipmentDef = _myBundle.LoadAsset<EquipmentDef>("LanceOfLonginusEquipmentDef");
            _lanceProjectilePrefab = _myBundle.LoadAsset<GameObject>("LanceProjectilePrefab");
            var expansionDef = _myBundle.LoadAsset<ExpansionDef>("RiskOfImpactExpansion");
            var extraDef = _myBundle.LoadAsset<ExpansionDef>("ExtraExpansion");
            
            _lanceGhost = _myBundle.LoadAsset<GameObject>("LanceGhost");
            _lanceProjectilePrefab.GetComponent<ProjectileController>().ghostPrefab = _lanceGhost;


            var dm = new ItemDisplayRuleDict();
            var dc = new ItemDisplayRuleDict();
            var drs = new ItemDisplayRuleDict();
            var dbs = new ItemDisplayRuleDict();
            var dl = new ItemDisplayRuleDict();
            var ddm = new ItemDisplayRuleDict();

            // Commando
            dc.Add("mdlCommandoDualies", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(0.20239F, -0.06471F, -0.01451F),
                    localAngles = new Vector3(7.55425F, 100.9208F, 181.4531F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
            
            dm.Add("mdlCommandoDualies", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(-0.22258F, -0.06758F, 0.03077F),
                    localAngles = new Vector3(353.157F, 193.3047F, 206.7511F),
                    localScale = new Vector3(0.1F, 0.1F, 0.1F)
                }
            });
            
            drs.Add("mdlCommandoDualies", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(0.659F, 0.394F, -0.787F),
                    localAngles = new Vector3(90.0003F, 180.079F, 180.4018F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlCommandoDualies", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(-0.92478F, -0.45384F, -1.30414F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(0.8F, 0.8F, 0.8F)

                }
            });
            
            dbs.Add("mdlCommandoDualies", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _bioticShellDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(-0.00003F, 0.32895F, 0.17171F),
                    localAngles = new Vector3(344.5746F, 359.8877F, 359.99F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
            
            // Huntress
            dc.Add("mdlHuntress", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(0.18116F, -0.05787F, 0.01043F),
                    localAngles = new Vector3(32.08101F, 101.53F, 186.5844F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
            
            dm.Add("mdlHuntress", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(-0.18304F, -0.07066F, -0.03466F),
                    localAngles = new Vector3(1.01352F, 196.2477F, 177.5963F),
                    localScale = new Vector3(0.1F, 0.1F, 0.1F)
                }
            });
            
            drs.Add("mdlHuntress", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(0.659F, 0.394F, -0.787F),
                    localAngles = new Vector3(90.0003F, 180.079F, 180.4018F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlHuntress", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(-0.92478F, -0.45384F, -1.30414F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(0.8F, 0.8F, 0.8F)

                }
            });
            
            dbs.Add("mdlHuntress", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _bioticShellDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(-0.00947F, 0.15638F, 0.17891F),
                    localAngles = new Vector3(337.9482F, 358.7204F, 52.48233F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });

            // Bandit
            dc.Add("mdlBandit2", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(0.20239F, -0.06471F, -0.01451F),
                    localAngles = new Vector3(7.55425F, 100.9208F, 181.4531F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
            
            dm.Add("mdlBandit2", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(-0.24089F, -0.03475F, -0.08941F),
                    localAngles = new Vector3(351.2207F, 180.0096F, 189.8164F),
                    localScale = new Vector3(0.1F, 0.1F, 0.1F)
                }
            });
            
            drs.Add("mdlBandit2", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "ROOT",
                    localPos = new Vector3(-0.36643F, 1.76863F, 0.55818F),
                    localAngles = new Vector3(0.0713F, 344.7929F, 0F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlBandit2", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(0.76099F, 0.41951F, -1.30499F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(0.8F, 0.8F, 0.8F)

                }
            });
            
            dbs.Add("mdlBandit2", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _bioticShellDisplayPrefab,
                    childName = "UpperArmR",
                    localPos = new Vector3(-0.00694F, 0.18783F, -0.07376F),
                    localAngles = new Vector3(337.5303F, 183.404F, 317.6291F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
            
            // MUL-T
            dc.Add("mdlToolbot", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(2.66884F, 1.38091F, 1.85382F),
                    localAngles = new Vector3(0F, 90F, 0F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
                
            dm.Add("mdlToolbot", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "Hip",
                    localPos = new Vector3(2.45337F, 0.28995F, -0.52838F),
                    localAngles = new Vector3(353.157F, 15.00789F, 206.7511F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            drs.Add("mdlToolbot", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(5.86281F, 6.67238F, 1.90503F),
                    localAngles = new Vector3(0F, 90F, 0F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlToolbot", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(-4.74067F, 8.84387F, -6.63475F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(0.8F, 0.8F, 0.8F)

                }
            });
            
            dbs.Add("mdlToolbot", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _bioticShellDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(-0.06F, 1.16F, 3.24F),
                    localAngles = new Vector3(8.85492F, 346.6888F, 13.30589F),
                    localScale = new Vector3(0.5F, 0.5F, 0.5F)
                }
            });
            
            // Engineer
            dc.Add("mdlEngi", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(0.24459F, -0.04413F, -0.01453F),
                    localAngles = new Vector3(7.55425F, 100.9208F, 181.4531F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
                
            dm.Add("mdlEngi", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(-0.27511F, -0.00163F, 0.03076F),
                    localAngles = new Vector3(353.157F, 193.3047F, 206.7511F),
                    localScale = new Vector3(0.1F, 0.1F, 0.1F)
                }
            });
            
            drs.Add("mdlEngi", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(0.659F, 0.394F, -0.787F),
                    localAngles = new Vector3(90.0003F, 180.079F, 180.4018F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlEngi", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(-0.92478F, -0.45384F, -1.30414F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(0.8F, 0.8F, 0.8F)

                }
            });
            
            dbs.Add("mdlEngi", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _bioticShellDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(-0.0041F, 0.2951F, 0.2481F),
                    localAngles = new Vector3(337.6299F, 359.5753F, 0.39165F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
            
            // Artificer
            dc.Add("mdlMage", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(0.20239F, -0.06471F, -0.01451F),
                    localAngles = new Vector3(7.55425F, 100.9208F, 181.4531F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
                
            dm.Add("mdlMage", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(-0.22258F, -0.06758F, 0.03077F),
                    localAngles = new Vector3(353.157F, 193.3047F, 206.7511F),
                    localScale = new Vector3(0.1F, 0.1F, 0.1F)
                }
            });
            
            drs.Add("mdlMage", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(0.659F, 0.394F, -0.787F),
                    localAngles = new Vector3(90.0003F, 180.079F, 180.4018F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlMage", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(-0.92478F, -0.45384F, -1.30414F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(0.8F, 0.8F, 0.8F)

                }
            });
            
            dbs.Add("mdlMage", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _bioticShellDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(-0.006F, 0.202F, 0.135F),
                    localAngles = new Vector3(328.7007F, 21.62506F, 345.3727F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
            
            // Mercenary
            dc.Add("mdlMerc", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(0.21235F, 0.06133F, -0.02965F),
                    localAngles = new Vector3(15.33725F, 100.263F, 182.9367F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });

            dm.Add("mdlMerc", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(-0.23385F, 0.04143F, -0.05599F),
                    localAngles = new Vector3(353.3026F, 173.733F, 204.3656F),
                    localScale = new Vector3(0.1F, 0.1F, 0.1F)
                }
            });
            
            drs.Add("mdlMerc", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(0.659F, 0.394F, -0.787F),
                    localAngles = new Vector3(90.0003F, 180.079F, 180.4018F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlMerc", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(-0.92478F, -0.45384F, -1.30414F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(0.8F, 0.8F, 0.8F)

                }
            });
            
            dbs.Add("mdlMerc", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _bioticShellDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(0.024F, 0.206F, 0.174F),
                    localAngles = new Vector3(324.9625F, 6.28376F, 355.0841F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
            
            // Rex
            dc.Add("mdlTreebot", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "PlatformBase",
                    localPos = new Vector3(0.7219F, 0.13116F, 0.2195F),
                    localAngles = new Vector3(351.8981F, 73.25513F, 358.5338F),
                    localScale = new Vector3(0.4F, 0.4F, 0.4F)
                }
            });
                
            dm.Add("mdlTreebot", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "PlatformBase",
                    localPos = new Vector3(-0.75075F, 0.07669F, 0.03075F),
                    localAngles = new Vector3(5.2294F, 355.3522F, 20.07811F),
                    localScale = new Vector3(0.4F, 0.4F, 0.4F)
                }
            });
            
            drs.Add("mdlTreebot", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "PlatformBase",
                    localPos = new Vector3(2.24017F, 2.11404F, 0.21945F),
                    localAngles = new Vector3(351.8981F, 73.25513F, 358.5338F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlTreebot", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "PlatformBase",
                    localPos = new Vector3(-1.57404F, 3.24967F, -1.88479F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(1.5F, 1.5F, 1.5F)

                }
            });
            
            dbs.Add("mdlTreebot", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _bioticShellDisplayPrefab,
                    childName = "PlatformBase",
                    localPos = new Vector3(0.661F, -0.186F, 0.157F),
                    localAngles = new Vector3(352.855F, 89.99998F, 351.2419F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
            
            // Loader
            dc.Add("mdlLoader", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(0.23944F, 0.01208F, -0.05754F),
                    localAngles = new Vector3(0F, 90F, 0F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
                
            dm.Add("mdlLoader", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(-0.2722F, -0.06754F, 0.03045F),
                    localAngles = new Vector3(6.94817F, 3.35957F, 26.3395F),
                    localScale = new Vector3(0.1F, 0.1F, 0.1F)
                }
            });
            
            drs.Add("mdlLoader", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(0.79858F, 0.55478F, 0.42963F),
                    localAngles = new Vector3(6.94817F, 3.35957F, 26.3395F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlLoader", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(-0.9262F, 1.13019F, -1.1029F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(0.8F, 0.8F, 0.8F)

                }
            });
            
            dbs.Add("mdlLoader", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _bioticShellDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(-0.25771F, 0.01213F, -0.06079F),
                    localAngles = new Vector3(0F, 90F, 0F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
            
            // Acrid
            dc.Add("mdlCroco", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "Hip",
                    localPos = new Vector3(1.94228F, 0.39972F, -0.40409F),
                    localAngles = new Vector3(7.55425F, 100.9208F, 181.4531F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
                
            dm.Add("mdlCroco", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "Hip",
                    localPos = new Vector3(-2.45304F, 0.54139F, 0.25251F),
                    localAngles = new Vector3(19.53804F, 194.0312F, 213.1425F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            drs.Add("mdlCroco", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(-5.04F, -3.02F, 5.34F),
                    localAngles = new Vector3(84.9998F, 359.9714F, 359.584F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlCroco", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(-7.60114F, 1.59776F, 9.56897F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(1F, 1F, 1F)

                }
            });
            
            dbs.Add("mdlCroco", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _bioticShellDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(-2.55F, 2.94F, -0.59F),
                    localAngles = new Vector3(356.9059F, 69.76768F, 25.01778F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });

            
            // Captain
            dc.Add("mdlCaptain", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(0.2758F, -0.10375F, -0.02017F),
                    localAngles = new Vector3(7.55425F, 100.9208F, 181.4531F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
                
            dm.Add("mdlCaptain", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(-0.30334F, -0.12005F, 0.02085F),
                    localAngles = new Vector3(356.783F, 191.9943F, 191.5501F),
                    localScale = new Vector3(0.1F, 0.1F, 0.1F)
                }
            });
            
            drs.Add("mdlCaptain", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(0.659F, 0.394F, -0.787F),
                    localAngles = new Vector3(90.0003F, 180.079F, 180.4018F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlCaptain", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(-0.92478F, -0.45384F, -1.30414F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(0.8F, 0.8F, 0.8F)

                }
            });
            
            dbs.Add("mdlCaptain", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _bioticShellDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(-0.00288F, 0.22976F, 0.21501F),
                    localAngles = new Vector3(343.283F, 350.7494F, 320.7523F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
            
            // Heretic
            dc.Add("mdlHeretic", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(-0.16834F, 0.20744F, -0.3221F),
                    localAngles = new Vector3(321.7271F, 192.0575F, 270.6939F),
                    localScale = new Vector3(0.2F, 0.2F, 0.2F)
                }
            });
                
            dm.Add("mdlHeretic", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(-0.21054F, 0.08455F, 0.4505F),
                    localAngles = new Vector3(281.3847F, 102.9863F, 13.02637F),
                    localScale = new Vector3(0.2F, 0.2F, 0.2F)
                }
            });
            
            drs.Add("mdlHeretic", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(-0.68984F, -1.22926F, -1.79202F),
                    localAngles = new Vector3(295.4063F, 217.6076F, 232.8437F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlHeretic", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(-1.06155F, -0.90547F, 1.83211F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(1F, 1F, 1F)

                }
            });
            
            // Railgunner
            dc.Add("mdlRailGunner", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "GunRoot",
                    localPos = new Vector3(0F, -0.12961F, 0.06076F),
                    localAngles = new Vector3(0F, 0F, 180F),
                    localScale = new Vector3(0.05F, 0.05F, 0.05F)
                }
            });
                
            dm.Add("mdlRailGunner", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "Backpack",
                    localPos = new Vector3(0.37979F, -0.509F, 0.0309F),
                    localAngles = new Vector3(24.20953F, 182.6858F, 49.19599F),
                    localScale = new Vector3(0.1F, 0.1F, 0.1F)
                }
            });
            
            drs.Add("mdlRailGunner", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(0.799F, 0.04025F, -0.28362F),
                    localAngles = new Vector3(90.00027F, 180.079F, 180.4018F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlRailGunner", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(-0.92478F, -0.45384F, -1.30414F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(0.8F, 0.8F, 0.8F)

                }
            });
            
            dbs.Add("mdlRailGunner", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _bioticShellDisplayPrefab,
                    childName = "UpperArmL",
                    localPos = new Vector3(0.00072F, 0.08052F, 0.06183F),
                    localAngles = new Vector3(5.06661F, 358.0723F, 116.7787F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
            
            // Void Fiend
            dc.Add("mdlVoidSurvivor", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(-0.00645F, 0.02082F, 0.15709F),
                    localAngles = new Vector3(11.41105F, 359.2993F, 165.3926F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
                
            dm.Add("mdlVoidSurvivor", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(0.03053F, 0.00479F, -0.20481F),
                    localAngles = new Vector3(10.27088F, 86.79462F, 192.5363F),
                    localScale = new Vector3(0.1F, 0.1F, 0.1F)
                }
            });
            
            drs.Add("mdlVoidSurvivor", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(0.81593F, 0.92553F, 0.42535F),
                    localAngles = new Vector3(88.27319F, 357.384F, 352.9297F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlVoidSurvivor", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(-0.92478F, 0.24515F, 0.64663F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(0.8F, 0.8F, 0.8F)

                }
            });
            
            dbs.Add("mdlVoidSurvivor", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _bioticShellDisplayPrefab,
                    childName = "UpperArmR",
                    localPos = new Vector3(0.21565F, -0.04918F, 0.00468F),
                    localAngles = new Vector3(10.95605F, 105.0443F, 191.8376F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
            
            // Seeker
            dc.Add("mdlSeeker", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(0.22783F, 0.02224F, -0.00465F),
                    localAngles = new Vector3(339.8495F, 78.9239F, 353.0269F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
                
            dm.Add("mdlSeeker", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(-0.26268F, 0.04164F, -0.02405F),
                    localAngles = new Vector3(354.4812F, 351.9723F, 342.0317F),
                    localScale = new Vector3(0.1F, 0.1F, 0.1F)
                }
            });
            
            drs.Add("mdlSeeker", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(0.649F, 1.727F, -0.109F),
                    localAngles = new Vector3(14.33531F, 0.39733F, 0.22404F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlSeeker", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(-0.91217F, 2.18543F, -0.30558F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(0.8F, 0.8F, 0.8F)

                }
            });
            
            dbs.Add("mdlSeeker", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _bioticShellDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(-0.0041F, 0.07456F, 0.1186F),
                    localAngles = new Vector3(352.4757F, 353.3727F, 4.8746F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
            
            // False Son
            dc.Add("mdlFalseSon", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(0.31534F, 0.11852F, -0.14177F),
                    localAngles = new Vector3(339.1165F, 125.6798F, 352.5457F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
                
            dm.Add("mdlFalseSon", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(-0.27259F, 0.11521F, -0.22216F),
                    localAngles = new Vector3(358.424F, 318.1652F, 346.5849F),
                    localScale = new Vector3(0.1F, 0.1F, 0.1F)
                }
            });
            
            drs.Add("mdlFalseSon", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "Root",
                    localPos = new Vector3(0.966F, 3.37F, -0.347F),
                    localAngles = new Vector3(11.01017F, 359.0091F, 0.39317F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlFalseSon", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "Root",
                    localPos = new Vector3(-1.25927F, 3.85099F, -1.37428F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(1F, 1F, 1F)

                }
            });
            
            dbs.Add("mdlFalseSon", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _bioticShellDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(0.02305F, 0.42224F, 0.16099F),
                    localAngles = new Vector3(308.0242F, 354.2237F, 354.7403F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
            
            // Chef
            dc.Add("mdlChef", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(0.1323F, 0.20992F, -0.3466F),
                    localAngles = new Vector3(352.6544F, 180.1904F, 314.3582F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
                
            dm.Add("mdlChef", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "OvenDoor",
                    localPos = new Vector3(-0.58416F, 0.06339F, 0.09305F),
                    localAngles = new Vector3(4.14493F, 2.58369F, 8.85832F),
                    localScale = new Vector3(0.1F, 0.1F, 0.1F)
                }
            });
            
            drs.Add("mdlChef", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "Root",
                    localPos = new Vector3(-0.787F, 1.34229F, 0.41002F),
                    localAngles = new Vector3(0.11209F, 89.41496F, 19.53264F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlChef", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "Root",
                    localPos = new Vector3(1.08621F, 2.5569F, -1.11563F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(0.8F, 0.8F, 0.8F)

                }
            });
            
            dbs.Add("mdlChef", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _bioticShellDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(-0.2804F, 0.1809F, 0.0058F),
                    localAngles = new Vector3(359.487F, 89.95702F, 183.1426F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
            
            // Operator
            dc.Add("mdlDroneTech", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(0.18679F, 0.07799F, 0.0409F),
                    localAngles = new Vector3(347.8549F, 103.8105F, 359.8152F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
                
            dm.Add("mdlDroneTech", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(-0.14239F, 0.08143F, 0.07492F),
                    localAngles = new Vector3(26.93316F, 359.0482F, 33.11788F),
                    localScale = new Vector3(0.1F, 0.1F, 0.1F)
                }
            });
            
            drs.Add("mdlDroneTech", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(-0.568F, 1.189F, 0F),
                    localAngles = new Vector3(350.118F, 180F, 180F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlDroneTech", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(1.80938F, 1.72888F, 0.44503F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(0.8F, 0.8F, 0.8F)

                }
            });
            
            dbs.Add("mdlDroneTech", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _bioticShellDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(-0.001F, -0.0999F, -0.0822F),
                    localAngles = new Vector3(286.3296F, 353.8997F, 22.19561F),
                    localScale = new Vector3(0.06F, 0.06F, 0.06F)
                }
            });
            
            // Drifter
            dc.Add("mdlDrifter", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(-0.1024F, 0.00187F, -0.37851F),
                    localAngles = new Vector3(357.0724F, 189.1636F, 280.7911F),
                    localScale = new Vector3(0.12F, 0.12F, 0.12F)
                }
            });
                
            dm.Add("mdlDrifter", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "Pelvis",
                    localPos = new Vector3(-0.18712F, -0.06656F, 0.3887F),
                    localAngles = new Vector3(275.4132F, 1.23565F, 85.85339F),
                    localScale = new Vector3(0.1F, 0.1F, 0.1F)
                }
            });
            
            drs.Add("mdlDrifter", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(0.39681F, -0.65566F, 0.66076F),
                    localAngles = new Vector3(318.5939F, 293.7787F, 169.5416F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlDrifter", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(-0.62317F, -1.03718F, -1.42457F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(0.8F, 0.8F, 0.8F)

                }
            });
            
            dbs.Add("mdlDrifter", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _bioticShellDisplayPrefab,
                    childName = "Chest",
                    localPos = new Vector3(-0.03096F, 0.27478F, -0.01272F),
                    localAngles = new Vector3(315.9871F, 247.1702F, 194.6353F),
                    localScale = new Vector3(0.06F, 0.06F, 0.06F)
                }
            });
            
            // Celestial War Tank
            dc.Add("mdlBasicTank", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "Bruiser_Hull",
                    localPos = new Vector3(-0.94709F, 0.66307F, 0.0552F),
                    localAngles = new Vector3(0F, 270F, 0F),
                    localScale = new Vector3(0.2F, 0.2F, 0.2F)
                }
            });
                
            dm.Add("mdlBasicTank", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "CannonMuzzle0",
                    localPos = new Vector3(0F, 0.16607F, -0.07901F),
                    localAngles = new Vector3(0F, 270F, 0F),
                    localScale = new Vector3(0.1F, 0.1F, 0.1F)
                }
            });
            
            drs.Add("mdlBasicTank", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "ROOT",
                    localPos = new Vector3(1.50346F, 2.04334F, -0.38996F),
                    localAngles = new Vector3(-0.00001F, 180F, 180F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlBasicTank", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "ROOT",
                    localPos = new Vector3(-1.71963F, 2.4493F, -1.30414F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(1F, 1F, 1F)

                }
            });
            
            // Best Buddy

            dm.Add("mdlDefectiveUnit (1)", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "Body",
                    localPos = new Vector3(0.53761F, 0.58974F, 0.39026F),
                    localAngles = new Vector3(307.4664F, 2.9877F, 336.0304F),
                    localScale = new Vector3(0.2F, 0.2F, 0.2F)
                }
            });
            
            // Engineer Turret
            dc.Add("mdlEngiTurret", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _comboStarDisplayPrefab,
                    childName = "Head",
                    localPos = new Vector3(0F, 0.83617F, -0.7562F),
                    localAngles = new Vector3(298.6673F, 0F, 0.00001F),
                    localScale = new Vector3(0.3F, 0.3F, 0.3F)
                }
            });
                
            dm.Add("mdlEngiTurret", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _mugDisplayPrefab,
                    childName = "Head",
                    localPos = new Vector3(0F, 0.84778F, -1.66701F),
                    localAngles = new Vector3(0F, 270F, 20.28025F),
                    localScale = new Vector3(0.3F, 0.3F, 0.3F)
                }
            });
            
            drs.Add("mdlEngiTurret", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _redshifterDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(2.07F, 3.72F, 0.41F),
                    localAngles = new Vector3(-0.0003F, 180.079F, 180.4018F),
                    localScale = new Vector3(1F, 1F, 1F)
                }
            });
            
            ddm.Add("mdlEngiTurret", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _doomedMoonDisplayPrefab,
                    childName = "Base",
                    localPos = new Vector3(-2.34182F, 3.93022F, -1.91096F),
                    localAngles = new Vector3(0F, 0F, 45F),
                    localScale = new Vector3(0.8F, 0.8F, 0.8F)

                }
            });
            
            dbs.Add("mdlEngiTurret", new[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = _bioticShellDisplayPrefab,
                    childName = "Head",
                    localPos = new Vector3(0F, 0F, -0.326F),
                    localAngles = new Vector3(358.6553F, 0F, 0F),
                    localScale = new Vector3(0.42F, 0.42F, 0.42F)
                }
            });
            
            _lanceEquipmentDef.requiredExpansion = expansionDef;
            _comboStarItem.requiredExpansion = expansionDef;
            _redshifter.requiredExpansion = expansionDef;
            _doomedMoon.requiredExpansion = expansionDef;
            _mug.requiredExpansion = expansionDef;
            _riskyDice.requiredExpansion = expansionDef;
            _riskyDiceCount.requiredExpansion = expansionDef;
            _riskyDiceAffliction.requiredExpansion = expansionDef;
            
            _riskyDiceCount.hidden = true;


            ItemAPI.Add(new CustomItem(_mug, dm));
            ItemAPI.Add(new CustomItem(_comboStarItem, dc));
            ItemAPI.Add(new CustomItem(_redshifter, drs));
            ItemAPI.Add(new CustomItem(_bioticShell, dbs));
            ItemAPI.Add(new CustomEquipment(_lanceEquipmentDef, dl));
            ItemAPI.Add(new CustomItem(_doomedMoon, ddm));
            ItemAPI.Add(new CustomItem(_doomedMoonConsumed, new ItemDisplayRuleDict()));
            ItemAPI.Add(new CustomItem(_doomedMoonStatToken, new ItemDisplayRuleDict()));
            ItemAPI.Add(new CustomItem(_riskyDice, new ItemDisplayRuleDict()));
            ItemAPI.Add(new CustomItem(_riskyDiceCount, new ItemDisplayRuleDict()));
            ItemAPI.Add(new CustomItem(_riskyDiceAffliction, new ItemDisplayRuleDict()));

            // Add them to the ContentPack
            RiskOfImpactContentPack.itemDefs.Add(new ItemDef[] { _mug });
            RiskOfImpactContentPack.itemDefs.Add(new ItemDef[] { _comboStarItem });
            RiskOfImpactContentPack.buffDefs.Add(new BuffDef[] { _comboStarBuff });
            RiskOfImpactContentPack.buffDefs.Add(new BuffDef[] { _comboStarMaxBuff });
            RiskOfImpactContentPack.itemDefs.Add(new ItemDef[] { _redshifter });
            RiskOfImpactContentPack.itemDefs.Add(new ItemDef[] { _doomedMoon });
            RiskOfImpactContentPack.itemDefs.Add(new ItemDef[] { _doomedMoonConsumed });
            RiskOfImpactContentPack.itemDefs.Add(new ItemDef[] { _doomedMoonStatToken });
            RiskOfImpactContentPack.buffDefs.Add(new BuffDef[] { _doomedMoonBuff });
            RiskOfImpactContentPack.itemDefs.Add( new ItemDef[] { _riskyDice });
            RiskOfImpactContentPack.itemDefs.Add( new ItemDef[] { _riskyDiceCount });
            RiskOfImpactContentPack.itemDefs.Add( new ItemDef[] { _riskyDiceAffliction });
            RiskOfImpactContentPack.buffDefs.Add(new BuffDef[] { _riskyDiceBuff });
            RiskOfImpactContentPack.itemDefs.Add(new ItemDef[] { _bioticShell });
            RiskOfImpactContentPack.equipmentDefs.Add(new EquipmentDef[] { _lanceEquipmentDef });
            RiskOfImpactContentPack.projectilePrefabs.Add(new GameObject[] { _lanceProjectilePrefab });
            RiskOfImpactContentPack.expansionDefs.Add(new ExpansionDef[] { expansionDef });
            //RiskOfImpactContentPack.expansionDefs.Add(new ExpansionDef[] { extraDef });


            //childName = "Pelvis",
            //localPos = new Vector3(-0.203F, -0.058F, 0.058F),
            //localAngles = new Vector3(63.11126F, 330.4519F, 181.0795F),
            //localScale = new Vector3(0.85025F, 0.85025F, 0.85025F)



            // Register language tokens for your custom equipment
            LanguageAPI.Add("EQUIPMENT_LANCEOFLONGINUS_NAME", "Lance of Longinus");
            LanguageAPI.Add("EQUIPMENT_LANCEOFLONGINUS_PICKUP", "Throw a mighty lance that impales enemies and can be retrieved, <style=cIsLunar>but at a cost</style>.");
            LanguageAPI.Add(
                "EQUIPMENT_LANCEOFLONGINUS_DESC",
                "Throw a spear for <style=cIsDamage>9000%</style> damage, " +
                "<style=cIsLunar>but</style> lose <style=cIsHealth>20%</style> maximum health."
            );
            LanguageAPI.Add("DD_NAME", "Cheerful Mug");
            LanguageAPI.Add("DD_PICKUP", "Wait, are those marshmallows or sugar cubes?");
            LanguageAPI.Add("DD_DESC", "Gain <style=cIsUtility>69%</style> (<style=cStack>+69% per stack</style>) movement and attack speed");
            LanguageAPI.Add("COMBOSTAR_NAME", "Combo Star");
            LanguageAPI.Add("COMBOSTAR_PICKUP", "Hitting enemies builds damage, missing a hit resets it.");
            LanguageAPI.Add("COMBOSTAR_DESC",
                "Hitting an enemy increases your damage by <style=cIsDamage>3%</style> " +
                "(<style=cStack>+0.5% per stack</style>), up to <style=cIsUtility>20</style> " +
                "(<style=cStack>+10 per stack</style>). Missing a skill hit <style=cDeath>resets all stacks</style>. " +
                "At maximum stacks, gain <style=cIsDamage>5%</style> (<style=cStack>+2.5% per stack</style>) crit chance.");
            LanguageAPI.Add("RS_NAME", "Redshifter");
            LanguageAPI.Add("RS_PICKUP", "Expand space to your benefit");
            LanguageAPI.Add("RS_DESC", "Gain <style=cIsUtility>50%</style> (<style=cStack>+50% per stack</style>) radius and range bonuses");
            LanguageAPI.Add("BIOTICSHELL_NAME", "Biotic Shell");
            LanguageAPI.Add("BIOTICSHELL_PICKUP",
                "<style=cIsVoid>Corrupts all Personal Shield Generators.</style> Slows the degradation of temporary barriers.");
            LanguageAPI.Add("BIOTICSHELL_DESC",
                "<style=cIsVoid>Corrupts all Personal Shield Generators.</style> " +
                "Temporary barriers decay <style=cIsUtility>12%</style> <style=cStack>(+12% per stack)</style> slower.");
            LanguageAPI.Add("DM_NAME", "Doomed Moon");
            LanguageAPI.Add("DM_PICKUP", "Revive each stage, but <style=cDeath>but lose items</style>");
            LanguageAPI.Add("DM_DESC", "Gain a <style=cIsHealing>revive</style> <style=cStack>(per stack)</style> each stage. " +
                                       "<style=cDeath>Breaks 10% of your items</style> <style=cStack>(5 items minimum)</style> on revive <style=cArtifact>(excluding Void items)</style>. " +
                                       "After reviving, permanently gain <style=cIsUtility>+10%</style> to <style=cShrine>all stats</style>. " +
                                       "<style=cDeath>Fails if fewer than 5 eligible items exist</style>." );

            LanguageAPI.Add("DMC_NAME", "Doomed Moon (Consumed)");
            LanguageAPI.Add("DMC_DESC", "Gain a <style=cIsHealing>revive</style> <style=cStack>(per stack)</style> each stage. " +
                                       "<style=cDeath>Breaks 10% of your items</style> <style=cStack>(5 items minimum)</style> on revive <style=cArtifact>(excluding Void items)</style>. " +
                                       "After reviving, permanently gain <style=cIsUtility>+10%</style> to <style=cShrine>all stats</style>. " +
                                       "<style=cDeath>Fails if fewer than 5 eligible items exist</style>." );
            
            LanguageAPI.Add("RD_NAME", "Risky Dice");
            LanguageAPI.Add("RD_PICKUP", "Roll fate at chests and Shrines of Chance for bonus rewards... or pay the price.");
            LanguageAPI.Add("RD_DESC",
                    "<style=cShrine>Chests</style> and <style=cShrine>Shrines of Chance</style> roll <style=cIsUtility>once</style> <style=cStack>(per stack)</style> per interaction.\n" +
                    "<style=cShrine>Success</style> (<style=cShrine>19 in 20</style>): Gain <style=cIsUtility>1 Risk</style> and the interactable drops <style=cIsDamage>+1 extra item</style> per successful roll.\n" +
                    "<style=cDeath>Misfortune</style> (<style=cDeath>1 in 20</style>): You still <style=cDeath>pay the cost</style>, the interaction is <style=cDeath>canceled</style>, and your <style=cIsUtility>Risk resets</style>.\n" +
                    "You'll receive <style=cWorldEvent>permanent</style> debuffs to <style=cDeath>all stats</style> equal to your total <style=cIsUtility>Risk</style> <style=cStack>(2% per Risk)</style>.\n" +
                    "If you have 20 or more Risk upon rolling Misfortune, you will <style=cDeath>die</style>.");
            
            LanguageAPI.Add("RDA_NAME", "Buyer's Remorse");
            LanguageAPI.Add("RDA_DESC",
                "<style=cWorldEvent>Permanent</style>.\n" +
                "<style=cDeath>Reduce all stats by 2%</style> <style=cStack>(+2% per stack)</style>.");
            
            
            // --- Void corruption setup (Personal Shield Generator -> Biotic Shell) ---
            var contagiousType = UnityEngine.AddressableAssets.Addressables
                .LoadAssetAsync<ItemRelationshipType>("RoR2/DLC1/Common/ContagiousItem.asset")
                .WaitForCompletion(); // wiki’s relationship type 

            // Load the vanilla ItemDef via Addressables (wiki warns NOT to use RoR2Content.Items statics)
            ItemDef personalShield = null;

            // Commonly used key (if this comes back null, the log will tell you and you can swap the key)
            personalShield = UnityEngine.AddressableAssets.Addressables
                .LoadAssetAsync<ItemDef>("RoR2/Base/PersonalShield/PersonalShield.asset")
                .WaitForCompletion();

            if (!personalShield || !_bioticShell)
            {
                Debug.LogError("[RiskOfImpact] BioticShell void corruption failed: missing PersonalShield or BioticShell ItemDef.");
            }
            else
            {
                var provider = ScriptableObject.CreateInstance<ItemRelationshipProvider>();
                provider.relationshipType = contagiousType;
                provider.relationships = new []
                {
                    new ItemDef.Pair { itemDef1 = personalShield, itemDef2 = _bioticShell }
                };
                
                RiskOfImpactContentPack.itemRelationshipProviders.Add(new ItemRelationshipProvider[] { provider });
            }

            Debug.Log("[RiskOfImpactContent] Assets and language tokens registered.");
            yield break;
        }

        public IEnumerator GenerateContentPackAsync(GetContentPackAsyncArgs args)
        {
            ContentPack.Copy(RiskOfImpactContentPack, args.output);
            args.ReportProgress(1f);
            yield break;
        }

        public IEnumerator FinalizeAsync(FinalizeAsyncArgs args)
        {
            R2API.RecalculateStatsAPI.GetStatCoefficients += (body, statArgs) =>
            {
                if (!body.inventory)
                    return;

                // Cheerful Mug
                int mugCount = body.inventory.GetItemCountEffective(_mug);
                if (mugCount > 0)
                {
                    statArgs.moveSpeedMultAdd += .69f * mugCount;
                    statArgs.attackSpeedMultAdd += .69f * mugCount;
                }

                // Combo Star – each visible buff stack increases damage
                int comboItemCount = body.inventory.GetItemCountEffective(_comboStarItem);
                if (comboItemCount > 0)
                {
                    int comboStacks = body.GetBuffCount(_comboStarBuff);
                    if (comboStacks > 0)
                    {
                        // 3% damage + 0.5% per item, all per stack
                        float bonusDamage = (0.03f + ((comboItemCount - 1) * 0.005f)) * comboStacks;
                        statArgs.damageMultAdd += bonusDamage;
                        int maxStacks = body.GetBuffCount(_comboStarMaxBuff);
                        if (maxStacks > 0)
                        {
                            // 5% crit chance + 2.5% per item at max stacks
                            statArgs.critAdd += 5f + ((comboItemCount - 1) * 2.5f);
                        }
                    }
                }
            };

            args.ReportProgress(1f);
            yield break;
        }


        private void AddSelf(ContentManager.AddContentPackProviderDelegate addContentPackProvider)
        {
            addContentPackProvider(this);
        }

        internal RiskOfImpactContent()
        {
            ContentManager.collectContentPackProviders += AddSelf;
        }

        public static EquipmentDef GetLanceEquipmentDef() => _lanceEquipmentDef;
        public static GameObject GetLanceProjectilePrefab() => _lanceProjectilePrefab;
        public static ItemDef GetComboStarItemDef() => _comboStarItem;
        public static BuffDef GetComboStarBuffDef() => _comboStarBuff;
        public static BuffDef GetComboStarMaxBuffDef() => _comboStarMaxBuff;
        public static ItemDef GetMugItemDef() => _mug;
        public static ItemDef GetRedshifterItemDef() => _redshifter;
        public static ItemDef GetDoomedMoonItemDef() => _doomedMoon;
        public static ItemDef GetDoomedMoonConsumedItemDef() => _doomedMoonConsumed;
        public static ItemDef GetDoomedMoonStatTokenItemDef() => _doomedMoonStatToken;
        public static BuffDef GetDoomedMoonBuffDef() => _doomedMoonBuff;
        public static ItemDef GetRiskyDiceItemDef() => _riskyDice;
        public static ItemDef GetRiskyDiceCountItemDef() => _riskyDiceCount;
        public static ItemDef GetRiskyDiceAfflictionItemDef() => _riskyDiceAffliction;
        public static BuffDef GetRiskyDiceBuffDef() => _riskyDiceBuff;

        public static ItemDef GetBioticShellItemDef() => _bioticShell;

    }
}
