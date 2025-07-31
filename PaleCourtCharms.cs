using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Modding;
using UnityEngine;
using SFCore;
using SFCore.Utils;
using ItemChanger;
using ItemChanger.Items;
using ItemChanger.Locations;
using ItemChanger.Placements;
using ItemChanger.Tags;
using ItemChanger.UIDefs;
using UnityEngine.SceneManagement;
using PaleCourtCharms.Rando;
using System.Linq;
using RandomizerMod.IC;
using PaleCourtCharms.Interop;
namespace PaleCourtCharms
{
    public class PaleCourtCharms : Mod,
    ILocalSettings<SaveModSettings>,
    IGlobalSettings<GlobalSettings>
    {
        public static PaleCourtCharms Instance;

        public static readonly List<CharmDefinition> Charms = new();
        public static readonly string[] CharmKeys = { "Mark_of_Purity", "Vessels_Lament", "Boon_of_Hallownest", "Abyssal_Bloom" };
        public static readonly int[] CharmCosts = { 3, 2, 4, 5 };

        public static List<int> CharmIDs = new();
        public static Dictionary<int, int> CharmCostsByID = new();

        public static Dictionary<string, AudioClip> Clips { get; } = new();
        public static Dictionary<string, AnimationClip> AnimClips { get; } = new();
        public static Dictionary<string, GameObject> preloadedGO { get; } = new();
        public static readonly Dictionary<string, Sprite> SPRITES = new();

        private SaveModSettings localSettings = new();
        public SaveModSettings OnSaveLocal()
{
   
      if (ModHooks.GetMod("Randomizer 4") is Mod)
    {
        localSettings.notchCosts = PaleCourtCharms.CharmCostsByID
            .OrderBy(kvp => Array.IndexOf(
                PaleCourtCharms.CharmKeys,
                PaleCourtCharms.CharmKeys[
                    PaleCourtCharms.CharmIDs.IndexOf(kvp.Key)
                ]))
            .Select(kvp => kvp.Value)
            .ToList();
    }

    return localSettings;
}

public void OnLoadLocal(SaveModSettings s)
{
    localSettings = s;

            if (ModHooks.GetMod("Randomizer 4") is Mod)
            {
                // load rando notch costs
                if (PaleCourtCharms.IsRandoSave() && localSettings.notchCosts.Count == PaleCourtCharms.CharmKeys.Length)
                {
                    for (int i = 0; i < PaleCourtCharms.CharmKeys.Length; i++)
                    {
                        int charmId = PaleCourtCharms.CharmIDs[i];
                        PaleCourtCharms.CharmCostsByID[charmId] = localSettings.notchCosts[i];
                    }
                }
            }
}

        public static SaveModSettings Settings => Instance?.localSettings;

        public override string GetVersion() => "1.0.1";

        public PaleCourtCharms() : base("PaleCourtCharms")
        {
            if (ModHooks.GetMod("FiveKnights") is Mod)
            {
                Log("[Warning]Detected PaleCourt,disabling mod.Note:you'll see a few more errors,that's normal.");
                return;
            }

            Instance = this;
            Log("PaleCourtCharms initialized.");

            LoadEmbeddedSprites();

            Charms.Add(new CharmDefinition { InternalName = "MarkOfPurity", DisplayName = "Mark of Purity", Description = "The Vessel was gifted a Pure Nail forged <br>through arcane means as a reflection of itself.<br>This Charm embodies the echoes of that <br>ancient craftmanship.<br><br>Hold ATTACK to concentrate and swing the <br>nail in a frenzy. Gradually increases the <br>bearer's rate of attack as they land nail strikes <br>in quick succession.",ShopDesc = "I see you wield a nail,and this charm looks to be made out of nails.\nIt will be yours... for a price.", Icon = SPRITES["Mark_of_Purity"], NotchCost = CharmCosts[0] });
            Charms.Add(new CharmDefinition { InternalName = "VesselsLament", DisplayName = "Vessel's Lament", Description = "Contains the hardships and grievances forced<br>upon the chained Vessel. Energy desperately <br>seeps out from within the constraints.<br><br>Focus to twist the SOUL of enemies marked<br>by the nail and unleash devastating blasts of <br>energy upon them.", ShopDesc = "I heard this charm is very good,but never risked to try myself.\nPay up,and you can find out if what they say is true.",Icon = SPRITES["Vessels_Lament"], NotchCost = CharmCosts[1] });
            Charms.Add(new CharmDefinition { InternalName = "BoonOfHallownest", DisplayName = "Boon of Hallownest", Description = "The Pale Wyrm's beacon shines brighty <br>through this Charm. The blessing bestowed<br>pon the land known as Hallownest was able<br>to elevate the lesser bugs that resided in the domain.<br><br>Transforms all spells to take on a purified form.",ShopDesc = "Found this lying around in fungal.I get shivers just by looking at it,and it looks very important.\n\nDo you want it?", Icon = SPRITES["Boon_of_Hallownest"], NotchCost = CharmCosts[2] });
            Charms.Add(new CharmDefinition { InternalName = "AbyssalBloom", DisplayName = "Abyssal Bloom", Description = "A dark blossom that was cursed to restrain an<br> uncontrollable power. Even though the <br>arkness within may be repressed, it writhes<br>in a volatile manner threatening to break <br>free.<br><br>The bearer gains overwhelming power as <br>they draw nearer to death.", ShopDesc = "I bought this from some wierd-looking bug for a handful of geo.\n\nI'll sell it to you if you pay enough.",Icon = SPRITES["Abyssal_Bloom"], NotchCost = CharmCosts[3] });

            CharmIDs = SFCore.CharmHelper.AddSprites(
                SPRITES["Mark_of_Purity"],
                SPRITES["Vessels_Lament"],
                SPRITES["Boon_of_Hallownest"],
                SPRITES["Abyssal_Bloom"]
            );

            InitializeCharmCosts();
        }

        private void InitializeCharmCosts()
        {
            CharmCostsByID.Clear();
            for (int i = 0; i < CharmIDs.Count; i++)
                CharmCostsByID[CharmIDs[i]] = Charms[i].NotchCost;
        }

        private void LoadEmbeddedSprites()
        {
            var asm = Assembly.GetExecutingAssembly();
            foreach (var res in asm.GetManifestResourceNames())
            {
                if (!res.EndsWith(".png")) continue;
                using var stream = asm.GetManifestResourceStream(res);
                if (stream == null) continue;

                var buffer = new byte[stream.Length];
                stream.Read(buffer, 0, buffer.Length);

                var tex = new Texture2D(1, 1);
                tex.LoadImage(buffer);

                var split = res.Split('.');
                var resName = split.Length >= 3
                    ? split[split.Length - 2] : Path.GetFileNameWithoutExtension(res);

                SPRITES[resName] = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f
                );

                
            }
        }

        public override List<(string, string)> GetPreloadNames()
        {
            return new List<(string, string)>
    {
        ("GG_Hollow_Knight", "Battle Scene/HK Prime"), ("GG_Hollow_Knight", "Battle Scene/Focus Blasts/HK Prime Blast/Blast"),  ("Dream_Final_Boss", "Boss Control/Radiance/Death/Knight Split/Knight Ball"), ("Tutorial_01", "_Props/Tut_tablet_top/Glows"), ("Ruins1_23", "Mage"), ("Dream_Final_Boss", "Boss Control/Radiance")
    };
        }

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            ModHooks.AfterSavegameLoadHook += _ => ICShiny.ResetPlacementsFlag();
            On.UIManager.StartNewGame += HandleNewGame;
            ModHooks.LanguageGetHook += LangGet;
            ModHooks.GetPlayerIntHook += OnGetPlayerIntHook;
            ModHooks.GetPlayerBoolHook += ModHooks_GetPlayerBool;
            ModHooks.SetPlayerBoolHook += OnSetPlayerBool;
            On.GameManager.StartNewGame += GameManager_StartNewGame;
            ModHooks.AfterSavegameLoadHook += OnAfterSave;
            On.HeroController.Awake += HeroController_Awake;
            preloadedGO["PV"] = preloadedObjects["GG_Hollow_Knight"]["Battle Scene/HK Prime"];
            preloadedGO["Blast"] = preloadedObjects["GG_Hollow_Knight"]["Battle Scene/Focus Blasts/HK Prime Blast/Blast"];
            preloadedGO["Knight Ball"] = preloadedObjects["Dream_Final_Boss"]["Boss Control/Radiance/Death/Knight Split/Knight Ball"];
            preloadedGO["Radiance"] = preloadedObjects["Dream_Final_Boss"]["Boss Control/Radiance"];
            preloadedGO["SoulTwister"] = preloadedObjects["Ruins1_23"]["Mage"];
            preloadedGO["SoulEffect"] = preloadedObjects["Tutorial_01"]["_Props/Tut_tablet_top/Glows"];
            ABManager.LoadAll();
            var bloomAnim = ABManager.LoadFromCharms<GameObject>("BloomAnim");
            if (bloomAnim != null) preloadedGO["Bloom Anim Prefab"] = bloomAnim;
            else Log("Warning: BloomAnim not found in AssetBundle.");
            var abyssalBloom = ABManager.LoadFromCharms<GameObject>("AbyssalBloom");
            if (abyssalBloom != null) preloadedGO["Bloom Sprite Prefab"] = abyssalBloom;
            else Log("Warning: AbyssalBloom not found in AssetBundle.");
            Instance = this;
            if (ModHooks.GetMod("DebugMod") is Mod)
            {
                DebugModHook.GiveAllCharms(() =>
                {
                    ToggleAllCharms(true);
                    localSettings.upgradedCharm_10 = true;
                    PlayerData.instance.SetBool("upgradedCharm_10", true);
                    PlayerData.instance.CountCharms();
                });
                DebugModHook.RemoveAllCharms(() =>
                {
                    ToggleAllCharms(false);
                    localSettings.upgradedCharm_10 = false;
                    PlayerData.instance.SetBool("upgradedCharm_10", false);
                    PlayerData.instance.CountCharms();
                });
            }


            ICShiny.Hook();
            var honourItem = new HonourUpgradeItem();
            Finder.DefineCustomItem(honourItem);

            if (ModHooks.GetMod("Randomizer 4") is Mod)
            {
                RandoManager.Hook();



                Rando.Interop.Setup(
                    PaleCourtCharms.Instance.globalSettings,
                    PaleCourtCharms.Instance

                );
            }
             if (ModHooks.GetMod("RandoSettingsManager") is Mod)
            {
                Interop.RSM_Interop.Hook();
            }
        }

        private static bool randoInitialized = false;



private static bool GetIsRando()
{
    return RandomizerMod.RandomizerMod.RS?.GenerationSettings != null;
}

private static void HandleNewGame(On.UIManager.orig_StartNewGame orig, UIManager self, bool permaDeath, bool bossRush)
{
   
    orig(self, permaDeath, bossRush);

    if (!randoInitialized)
    {
    
        bool randoLoaded = ModHooks.GetMod("Randomizer 4") is Mod;

        
        bool isRandoSave = randoLoaded && GetIsRando();

        if (isRandoSave)
        {
            randoInitialized = true;
            PaleCourtCharms.Instance.StartGame();
        }
    }
}

        private void GameManager_StartNewGame(On.GameManager.orig_StartNewGame orig, GameManager gm, bool perma, bool bossRush)
        {
            orig(gm, perma, bossRush);

            if (bossRush)
            {
                
                int count = CharmIDs.Count;
                localSettings.gotCharms = new bool[count];
                localSettings.newCharms = new bool[count];
                localSettings.equippedCharms = new bool[count];

                for (int i = 0; i < count; i++)
                    localSettings.gotCharms[i] = localSettings.newCharms[i] = true;

                localSettings.upgradedCharm_10 = true;
            }

            StartGame();
        }

        public void StartGame()
        {
            GameManager.instance.gameObject.AddComponent<Amulets>();
           

        }
public static bool IsRandoSave()
{
    
    if (ModHooks.GetMod("Randomizer 4") is Mod)
    {
        try
        {
            var rm = ItemChangerMod.Modules.Get<RandomizerModule>();
            return rm != null;
        }
        catch (NullReferenceException)
        {
            return false;
        }
    }
    return false;
}


       private int OnGetPlayerIntHook(string target, int orig)
        {
           
            if (target.StartsWith("charmCost_")
                && int.TryParse(target.Split('_')[1], out var charmNum))
            {
               
                if (!IsRandoSave())
                {
                   
                    int idx = PaleCourtCharms.CharmIDs.IndexOf(charmNum);
                    if (idx >= 0 && idx < PaleCourtCharms.CharmCosts.Length)
                        return PaleCourtCharms.CharmCosts[idx];
                }
                else if (PaleCourtCharms.CharmCostsByID.TryGetValue(charmNum, out var cost))
                {
                   
                    return cost;
                }
            }
            return orig;
        }


       private bool OnSetPlayerBool(string target, bool value)
{
    if (CharmIDs.Count == 0) return value;

    var parts = target.Split('_');
    if (parts.Length == 2 && int.TryParse(parts[1], out var charmNum))
    {
        var idx = CharmIDs.IndexOf(charmNum);
        if (idx >= 0)
        {
            switch (parts[0])
            {
                case "equippedCharm":
                    localSettings.equippedCharms[idx] = value;
                    return true;
                case "gotCharm":
                    localSettings.gotCharms[idx] = value;

                    if (value)
                    {
                        
                        GameManager.instance.SaveGame();
                    }

                    return true;
                case "newCharm":
                    localSettings.newCharms[idx] = value;
                    return true;
            }
        }
    }

    return value;
}

        private bool ModHooks_GetPlayerBool(string target, bool orig)
        {
            if (CharmIDs.Count == 0) return orig;
            var parts = target.Split('_');
            if (parts.Length == 2 && int.TryParse(parts[1], out var charmNum))
            {
                var idx = CharmIDs.IndexOf(charmNum);
                if (idx >= 0)
                {
                    bool Safe(int i, bool[] arr) => i >= 0 && i < arr.Length ? arr[i] : orig;
                    return parts[0] switch
                    {
                        "gotCharm" => Safe(idx, localSettings.gotCharms),
                        "newCharm" => Safe(idx, localSettings.newCharms),
                        "equippedCharm" => Safe(idx, localSettings.equippedCharms),
                        _ => orig
                    };
                }
            }
            return orig;
        }

        private string LangGet(string key, string sheet, string orig)
        {

            if (sheet == "UI" && key == "CHARM_NAME_10")
            {

                return localSettings.upgradedCharm_10
                    ? "King’s Honour"
                    : orig;
            }
            if (sheet == "UI" && key == "CHARM_DESC_10")
            {
                return localSettings.upgradedCharm_10
                    ? "Unique charm bestowed by the king to his<br>most loyal Knight. Refurbished and restored<br>to its original state.<br><br>Surrounds its bearer in a royal and heroic aura."
                    : orig;
            }


            if (key.StartsWith("CHARM_NAME_") || key.StartsWith("CHARM_DESC_"))
            {

                var parts = key.Split('_');
                if (parts.Length == 3 && int.TryParse(parts[2], out int id))
                {
                    int idx = CharmIDs.IndexOf(id);
                    if (idx >= 0)
                    {
                        if (key.StartsWith("CHARM_NAME_"))
                            return Charms[idx].DisplayName;
                        else
                            return Charms[idx].Description;
                    }
                }
            }

            return orig;
        }


        private static void ToggleAllCharms(bool give)
        {
            for (int i = 0; i < CharmIDs.Count; i++)
            {
                PaleCourtCharms.Instance.localSettings.gotCharms[i] = give;
                PaleCourtCharms.Instance.localSettings.newCharms[i] = give;
                PaleCourtCharms.Instance.localSettings.equippedCharms[i] = false;
            }
        }

        private void OnAfterSave(SaveGameData data)
        {
            if (CharmIDs.Count == 0 || localSettings.gotCharms == null || localSettings.gotCharms.Length != CharmIDs.Count)
                return;

            for (int i = 0; i < CharmIDs.Count; i++)
            {
                int charmNum = CharmIDs[i];
                PlayerData.instance.SetBool($"gotCharm_{charmNum}", localSettings.gotCharms[i]);
                PlayerData.instance.SetBool($"newCharm_{charmNum}", localSettings.newCharms[i]);
                PlayerData.instance.SetBool($"equippedCharm_{charmNum}", localSettings.equippedCharms[i]);
            }

            if (localSettings.upgradedCharm_10)
                PlayerData.instance.SetBool("upgradedCharm_10", true);
        }
        public static GlobalSettings GlobalSettings => Instance?.globalSettings;
        private GlobalSettings globalSettings = new GlobalSettings();

        public void OnLoadGlobal(GlobalSettings s)
        {
            globalSettings = s;
        }
        public GlobalSettings OnSaveGlobal()
        {
            return globalSettings;
        }

        private void HeroController_Awake(On.HeroController.orig_Awake orig, HeroController self)
        { ItemHandler.ModulesRegistered = false;
        
            orig(self);

            if (GameManager.instance != null && GameManager.instance.gameObject.GetComponent<Amulets>() == null)
            {
                GameManager.instance.gameObject.AddComponent<Amulets>();
          
            }

        }
        

        private new void Log(object msg) => Modding.Logger.Log("[PaleCourtCharms] " + msg);
    }

    public class CharmDefinition
    {
        public string InternalName;
        public int NotchCost;
        public string DisplayName;
        public string Description;
        public string ShopDesc; 
        public Sprite Icon;
    }
  public class GlobalSettings {
    public bool AddCharms { get; set; } = false;
    public bool RandomizeCosts { get; set; } = false;
    public string LogicSettings { get; set; } = "{}";
  }
} 
