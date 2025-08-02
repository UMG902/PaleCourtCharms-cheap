using System;
using System.Collections.Generic;
using ItemChanger;
using ItemChanger.Locations;
using ItemChanger.UIDefs;
using ItemChanger.Tags;
using UnityEngine;
using Newtonsoft.Json;
using Modding;
namespace PaleCourtCharms
{
    internal static class ICShiny
    {
        public static bool placementsDone = false;

        
        public static readonly string[] CharmScenes = {
            "Fungus3_48", "Room_Mansion", "Fungus2_21", "Abyss_09"
        };
        public static readonly Vector2[] CharmPositions = {
            new(49.1f, 94.4f), new(27f, 6.4f), new(114.8f, 12.4f), new(210.2f, 50.4f)
        };

        
        public const string HonourName = "Kings_Honour";
        public static readonly string HonourScene = "Waterways_13";
        public static readonly Vector2 HonourPos = new(89.1f, 18.4f);

        public static void Hook()
        {
            On.UIManager.ContinueGame += (orig, self) =>
            {
                ItemChangerMod.CreateSettingsProfile(false, false);
                orig(self);
                TryPlaceCharms();
                
            };
            On.UIManager.StartNewGame += (orig, self, perma, bossRush) =>
            {
                ItemChangerMod.CreateSettingsProfile(false, false);
                orig(self, perma, bossRush);
                TryPlaceCharms();
                placementsDone = false;
            };
        }
        public static void ResetPlacementsFlag()
        {
            placementsDone = false;
        }

        private static bool GetIsRando()
        {
           
            return RandomizerMod.RandomizerMod.RS?.GenerationSettings != null;
        }

        public static void TryPlaceCharms()
        {
            if (placementsDone || PaleCourtCharms.CharmIDs.Count == 0)
                return;

            bool randoLoaded = ModHooks.GetMod("Randomizer 4") is Mod;
            bool isRando = false;

            if (randoLoaded)
            {
                
                isRando = GetIsRando();
            }

            bool addCharms = PaleCourtCharms.GlobalSettings.AddCharms;
            if (isRando && addCharms)
                return;

    var placements = new List<AbstractPlacement>();

  
    for (int i = 0; i < PaleCourtCharms.CharmIDs.Count && i < 4; i++)
    {
        int id = PaleCourtCharms.CharmIDs[i];
        string key = PaleCourtCharms.CharmKeys[i];

        // Only define item if not already defined
        if (Finder.GetItemInternal(key) == null)
        {
            var item = new ItemChanger.Items.CharmItem
            {
                charmNum = id,
                name     = key,
                UIDef    = new MsgUIDef
                {
                    name     = new LanguageString("UI", $"CHARM_NAME_{id}"),
                     shopDesc = new LanguageString("RANDO", $"SHOP_DESCRIPTION_{PaleCourtCharms.Charms[i].InternalName}"),
                    sprite   = new EmbeddedSprite { key = key }
                }
            };
            var tag = item.AddTag<InteropTag>();
            tag.Message = "RandoSupplementalMetadata";
            tag.Properties["ModSource"] = PaleCourtCharms.Instance.GetName();
            tag.Properties["PoolGroup"] = "Charms";

            Finder.DefineCustomItem(item);
        }

        // Only define location if not already defined
        if (Finder.GetLocationInternal(key) == null)
        {
            var loc = new CoordinateLocation
            {
                name      = key,
                sceneName = CharmScenes[i],
                x         = CharmPositions[i].x,
                y         = CharmPositions[i].y,
                elevation = 0
            };
            Finder.DefineCustomLocation(loc);
        }

        
        placements.Add(Finder.GetLocationInternal(key).Wrap().Add(Finder.GetItemInternal(key)));
    }

   
    if (Finder.GetItemInternal(HonourName) == null)
        Finder.DefineCustomItem(new HonourUpgradeItem());

    
    if (Finder.GetLocationInternal(HonourName) == null)
    {
        var honourLoc = new CoordinateLocation
        {
            name      = HonourName,
            sceneName = HonourScene,
            x         = HonourPos.x,
            y         = HonourPos.y,
            elevation = 0
        };
        Finder.DefineCustomLocation(honourLoc);
    }

   
    placements.Add(Finder.GetLocationInternal(HonourName).Wrap().Add(Finder.GetItemInternal(HonourName)));

   
    ItemChangerMod.AddPlacements(placements, PlacementConflictResolution.Ignore);

    placementsDone = true;
    
}


         public class EmbeddedSprite : ISprite {
        
        public string key;

        [JsonIgnore]
        public Sprite Value => EmbeddedSprites.Get(key);

        public ISprite Clone() => (ISprite)MemberwiseClone();
    }
    }
}
}
