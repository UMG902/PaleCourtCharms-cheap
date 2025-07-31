using System.Linq;
using System.Collections.Generic;
using ItemChanger;
using ItemChanger.Locations;
using ItemChanger.Tags;
using RandomizerMod.RC;
using RandomizerMod.RandomizerData;
using RandomizerMod.Settings;
using RandomizerCore.Logic;
using RandomizerCore.Json;
using RandomizerCore.LogicItems;
using RandomizerCore;
using System;
using ItemChanger.Modules;

namespace PaleCourtCharms.Rando
{
   internal static class ItemHandler
{
    private const string HonourKey = "Kings_Honour";
  public static bool ModulesRegistered = false;
private static bool _objectsDefined = false;
private static bool _addedToPool = false;

    private static bool _chainTagged = false; 
public static void Hook()
{
   
    if (!_objectsDefined)
    {
        DefineObjects();
        Finder.GetItemOverride += args =>
        {
            if (!_chainTagged && args.ItemName == ItemNames.Defenders_Crest)
            {
                var crest = Finder.GetItemInternal(ItemNames.Defenders_Crest);
                args.Current = crest;
                crest.tags ??= new List<Tag>();
                crest.tags.Add(new ItemChainTag {
                    predecessor = ItemNames.Defenders_Crest,
                    successor   = HonourKey
                });
                
                _chainTagged = true;
            }
        };
        _objectsDefined = true;
    }

   
    RequestBuilder.OnUpdate.Subscribe(0f, rb =>
    {
        if (PaleCourtCharms.GlobalSettings.AddCharms)
            AddToPool(rb);
    });

 
    RCData.RuntimeLogicOverride.Subscribe(50f, (gs, lmb) =>
    {
        if (PaleCourtCharms.GlobalSettings.AddCharms)
            InjectLogic(gs, lmb);
    });

    RequestBuilder.OnUpdate.Subscribe(1f, rb =>
    {
        if (RandomizerMod.RandomizerMod.RS != null
            && PaleCourtCharms.GlobalSettings.RandomizeCosts)
            ScaleNotchBudget(rb);
    });
    RequestBuilder.OnUpdate.Subscribe(50f, rb =>
    {
        if (RandomizerMod.RandomizerMod.RS != null
            && PaleCourtCharms.GlobalSettings.RandomizeCosts)
            RandomizeNotchCosts(rb);
    });

  
    RandoController.OnExportCompleted += _ =>
    {
        _addedToPool    = false;
        _objectsDefined = false;
        _chainTagged    = false;

        DefineObjects();
        _objectsDefined = true;

        
        RequestBuilder.OnUpdate.Subscribe(0f, rb =>
        {
            if (PaleCourtCharms.GlobalSettings.AddCharms)
                AddToPool(rb);
        });
        RCData.RuntimeLogicOverride.Subscribe(50f, (gs, lmb) =>
        {
            if (PaleCourtCharms.GlobalSettings.AddCharms)
                InjectLogic(gs, lmb);
        });
        RequestBuilder.OnUpdate.Subscribe(1f, rb =>
        {
            if (RandomizerMod.RandomizerMod.RS != null
                && PaleCourtCharms.GlobalSettings.RandomizeCosts)
                ScaleNotchBudget(rb);
        });
        RequestBuilder.OnUpdate.Subscribe(50f, rb =>
        {
            if (RandomizerMod.RandomizerMod.RS != null
                && PaleCourtCharms.GlobalSettings.RandomizeCosts)
                RandomizeNotchCosts(rb);
        });

        bool randoDoesCost = RandomizerMod.RandomizerMod.RS?
                                .GenerationSettings?
                                .MiscSettings?
                                .RandomizeNotchCosts ?? false;

        if (PaleCourtCharms.GlobalSettings.RandomizeCosts
            && !randoDoesCost
            && !ModulesRegistered)
        {
            
            ItemChangerMod.Modules.Add<NotchCostUI>();
            ItemChangerMod.Modules.Add<ZeroCostCharmEquip>();
            ModulesRegistered = true;
        }
       
       
        var crest = Finder.GetItemInternal(ItemNames.Defenders_Crest);
        if (crest?.tags != null)
        {
            var removed = crest.tags.RemoveAll(t =>
                t is ItemChainTag ict && ict.successor == HonourKey);
            if (removed > 0)
                Modding.Logger.Log($"[PaleCourtCharms] Cleaned up {removed} leftover Crest chain tag(s).");
        }

        
    };
}




    private static void DefineObjects()
    {
      
        for (int i = 0; i < PaleCourtCharms.CharmKeys.Length; i++)
        {
            string key = PaleCourtCharms.CharmKeys[i];
            if (Finder.GetItemInternal(key) == null)
                Finder.DefineCustomItem(new PC_CharmItem(i));

            if (Finder.GetLocationInternal(key) == null)
            {
                var loc = new CoordinateLocation
                {
                    name = key,
                    sceneName = ICShiny.CharmScenes[i],
                    x = ICShiny.CharmPositions[i].x,
                    y = ICShiny.CharmPositions[i].y,
                    elevation = 0
                };
                var tag = loc.AddTag<InteropTag>();
                tag.Message = "RandoSupplementalMetadata";
                tag.Properties["ModSource"] = PaleCourtCharms.Instance.GetName();
                tag.Properties["PoolGroup"] = PoolNames.Charm;
                tag.Properties["VanillaItem"] = key;

                    if (key == "Boon_of_Hallownest")
                    {
                        tag.Properties["MapLocations"] = new (string scene, float x, float y)[]
                        {
                           ("Fungus2_21", 122.77f, 12.15f)
                        };
                        tag.Properties["HighlightScenes"] = new string[] { "Fungus2_21" };


                        tag.Properties["WorldMapLocation"] = ("Fungus2_21", 122.77f, 12.15f);

                    }


                Finder.DefineCustomLocation(loc);
            }
        }

        if (Finder.GetItemInternal(HonourKey) == null)
            Finder.DefineCustomItem(new HonourUpgradeItem());

        if (Finder.GetLocationInternal(HonourKey) == null)
        {
            var honourLoc = new CoordinateLocation
            {
                name = HonourKey,
                sceneName = ICShiny.HonourScene,
                x = ICShiny.HonourPos.x,
                y = ICShiny.HonourPos.y,
                elevation = 0
            };
            var tag = honourLoc.AddTag<InteropTag>();
            tag.Message = "RandoSupplementalMetadata";
            tag.Properties["ModSource"] = PaleCourtCharms.Instance.GetName();
            tag.Properties["PoolGroup"] = PoolNames.Charm;
            tag.Properties["VanillaItem"] = HonourKey;
            Finder.DefineCustomLocation(honourLoc);
        }
    }

private static void AddToPool(RequestBuilder rb)
{
    if (_addedToPool) return;
    _addedToPool = true;

    foreach (var key in PaleCourtCharms.CharmKeys.Concat(new[] { HonourKey }))
    {
        rb.RemoveLocationByName(key);
        rb.AddLocationByName(key);
        rb.AddItemByName(key);
        rb.EditItemRequest(key, info =>
        {
            info.getItemDef = () => new ItemDef {
                Name      = key,
                Pool      = PoolNames.Charm,
                MajorItem = false,
                PriceCap  = 2000
            };

          
            if (key == HonourKey
                && rb.gs.PoolSettings.Charms)
            {
                info.realItemCreator = (factory, placement) =>
                {
                    var item = factory.MakeItem(HonourKey);
                    item.tags ??= new List<Tag>();
                    item.tags.Add(new ItemChainTag {
                        predecessor = ItemNames.Defenders_Crest,
                        successor   = HonourKey
                    });
                    
                    return item;
                };
            }
        });
    }

   
}


        private static void ScaleNotchBudget(RequestBuilder rb)
        {
            const int addedNotchCost = 14;
            rb.gs.MiscSettings.MinRandomNotchTotal += addedNotchCost;
            rb.gs.MiscSettings.MaxRandomNotchTotal += addedNotchCost;
        }

        private static void RandomizeNotchCosts(RequestBuilder rb)
        {
            var rng          = rb.rng;
            int vanillaTotal = rb.ctx.notchCosts.Sum();
            int minTotal     = rb.gs.MiscSettings.MinRandomNotchTotal;
            int maxTotal     = rb.gs.MiscSettings.MaxRandomNotchTotal;
            int variance     = maxTotal - minTotal;
            int n            = PaleCourtCharms.CharmKeys.Length;

            int minCount = Math.Max(0, (vanillaTotal - variance) / 10);
            int maxCount = Math.Min(n * 6, (vanillaTotal + variance) / 10);
            int count    = rng.Next(minCount, maxCount + 1);

            var costs = new int[n];
            for (int i = 0; i < count; i++)
            {
                int idx;
                do { idx = rng.Next(n); }
                while (costs[idx] >= 6);
                costs[idx]++;
            }

            PaleCourtCharms.Settings.notchCosts = costs.ToList();
            for (int i = 0; i < n; i++)
                PaleCourtCharms.CharmCostsByID[PaleCourtCharms.CharmIDs[i]] = costs[i];

        }

        private static void InjectLogic(GenerationSettings gs, LogicManagerBuilder lmb)
        {
            foreach (var key in PaleCourtCharms.CharmKeys)
                lmb.AddItem(new SingleItem(key, new TermValue(lmb.GetOrAddTerm(key), 1)));
            lmb.AddItem(new SingleItem(HonourKey, new TermValue(lmb.GetOrAddTerm(HonourKey), 1)));

            LoadAdditionalLogicFiles(lmb);
        }

        private static void LoadAdditionalLogicFiles(LogicManagerBuilder lmb)
        {
            var modDir  = System.IO.Path.GetDirectoryName(typeof(ItemHandler).Assembly.Location);
            var jsonFmt = new JsonLogicFormat();

            void TryLoad(string file, LogicFileType type)
            {
                var path = System.IO.Path.Combine(modDir, file);
                if (System.IO.File.Exists(path))
                {
                    using var s = System.IO.File.OpenRead(path);
                    lmb.DeserializeFile(type, jsonFmt, s);
                }
            }

            TryLoad("LogicMacros.json", LogicFileType.MacroEdit);
            TryLoad("LogicWaypoints.json", LogicFileType.Waypoints);
            TryLoad("ConnectionLogicPatches.json", LogicFileType.LogicEdit);
            TryLoad("Locations.json", LogicFileType.Locations);
            TryLoad("Items.json", LogicFileType.ItemStrings);
        }
    }
}
