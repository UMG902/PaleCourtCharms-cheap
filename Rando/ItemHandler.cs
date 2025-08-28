using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
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
using ItemChanger.Modules;

namespace PaleCourtCharms.Rando
{
    internal static class ItemHandler
    {
        private const string HonourKey = "Kings_Honour";
        public static bool ModulesRegistered = false;
        private static int _objectsDefined = 0;
        private static int _addToPoolRunning = 0;
        private static int _subscribed = 0;

        private static readonly object _defineLock = new object();
        private static int[] _savedVanillaNotchCosts = null;
        private static int _crestChainTagged = 0;
        private static int _crestOverrideSubscribed = 0;
        private static ItemChanger.AbstractItem _crestDefCache = null;

        public static void Hook()
        {
            if (Interlocked.CompareExchange(ref _objectsDefined, 1, 0) == 0)
            {
                DefineObjects();
                if (Interlocked.CompareExchange(ref _crestOverrideSubscribed, 1, 0) == 0)
                {
                    Finder.GetItemOverride += args =>
                    {
                        try
                        {
                            if (args.ItemName != ItemNames.Defenders_Crest) return;

                            var crest = _crestDefCache;
                            if (crest != null)
                            {
                                if (Interlocked.CompareExchange(ref _crestChainTagged, 1, 0) == 0)
                                {
                                    crest.tags ??= new List<Tag>();
                                    bool has = crest.tags.OfType<ItemChainTag>()
                                        .Any(t => t.predecessor == ItemNames.Defenders_Crest && t.successor == HonourKey);
                                    if (!has)
                                    {
                                        crest.tags.Add(new ItemChainTag
                                        {
                                            predecessor = ItemNames.Defenders_Crest,
                                            successor = HonourKey
                                        });
                                        Modding.Logger.Log("[PaleCourtCharms] (override) Added chain tag to Defenders_Crest -> Kings_Honour");
                                    }
                                }

                                args.Current = crest;
                            }
                        }
                        catch (Exception e)
                        {
                            Modding.Logger.LogError($"[PaleCourtCharms] GetItemOverride error for crest: {e}");
                        }
                    };
                }
            }

            if (Interlocked.CompareExchange(ref _subscribed, 1, 0) == 0)
            {
                RequestBuilder.OnUpdate.Subscribe(0f, rb =>
                {
                    int opId = Environment.TickCount;
                    try
                    {
                        if (PaleCourtCharms.GlobalSettings.AddCharms)
                            AddToPool(rb);
                    }
                    catch (Exception e)
                    {
                        Modding.Logger.LogError($"[PaleCourtCharms] AddToPool exception (op {opId}): {e}");
                        throw;
                    }
                });

                RequestBuilder.OnUpdate.Subscribe(1f, rb =>
                {
                    int opId = Environment.TickCount;
                    try
                    {
                        if (RandomizerMod.RandomizerMod.RS != null
                            && PaleCourtCharms.GlobalSettings.RandomizeCosts)
                            ScaleNotchBudget(rb);
                    }
                    catch (Exception e)
                    {
                        Modding.Logger.LogError($"[PaleCourtCharms] ScaleNotchBudget exception (op {opId}): {e}");
                        throw;
                    }
                });

                RequestBuilder.OnUpdate.Subscribe(50f, rb =>
                {
                    int opId = Environment.TickCount;
                    try
                    {
                        if (RandomizerMod.RandomizerMod.RS != null
                            && PaleCourtCharms.GlobalSettings.RandomizeCosts)
                            RandomizeNotchCosts(rb);
                    }
                    catch (Exception e)
                    {
                        Modding.Logger.LogError($"[PaleCourtCharms] RandomizeNotchCosts exception (op {opId}): {e}");
                        throw;
                    }
                });

                RCData.RuntimeLogicOverride.Subscribe(50f, (gs, lmb) =>
                {
                    int opId = Environment.TickCount;
                    try
                    {
                        if (PaleCourtCharms.GlobalSettings.AddCharms)
                            InjectLogic(gs, lmb);
                    }
                    catch (Exception e)
                    {
                        Modding.Logger.LogError($"[PaleCourtCharms] InjectLogic exception (op {opId}): {e}");
                        throw;
                    }
                });
            }

            RequestBuilder.OnUpdate.Subscribe(-100f, rb =>
            {
                try
                {
                    if (_savedVanillaNotchCosts == null && rb?.ctx?.notchCosts != null)
                    {
                        _savedVanillaNotchCosts = rb.ctx.notchCosts.ToArray();
                    }
                }
                catch (Exception e)
                {
                    Modding.Logger.LogError($"[PaleCourtCharms] Error saving vanilla notch costs: {e}");
                }
            });

            RequestBuilder.OnUpdate.Subscribe(100f, rb =>
            {
                try
                {
                    bool randoDoesCost = RandomizerMod.RandomizerMod.RS?
                                            .GenerationSettings?
                                            .MiscSettings?
                                            .RandomizeNotchCosts ?? false;

                    if (randoDoesCost && !PaleCourtCharms.GlobalSettings.RandomizeCosts)
                    {
                        var defaultCosts = new List<int>();
                        int nDefaults = PaleCourtCharms.CharmKeys.Length;
                        for (int i = 0; i < nDefaults; i++)
                            defaultCosts.Add(i < PaleCourtCharms.CharmCosts.Length ? PaleCourtCharms.CharmCosts[i] : 0);
                        try
                        {
                            var nc = rb.ctx.notchCosts;
                            if (nc != null)
                            {
                                nc.Clear();
                                nc.AddRange(defaultCosts);
                            }
                            else
                            {
                                rb.ctx.notchCosts = new List<int>(defaultCosts);
                            }
                        }
                        catch (Exception ex)
                        {
                            Modding.Logger.LogError($"[PaleCourtCharms] Could not write rb.ctx.notchCosts: {ex}");
                        }

                        PaleCourtCharms.Settings.notchCosts = new List<int>(defaultCosts);

                        int m = Math.Min(PaleCourtCharms.CharmIDs.Count, defaultCosts.Count);
                        for (int i = 0; i < m; i++)
                        {
                            int id = PaleCourtCharms.CharmIDs[i];
                            PaleCourtCharms.CharmCostsByID[id] = defaultCosts[i];
                        }
                    }
                }
                catch (Exception e)
                {
                    Modding.Logger.LogError($"[PaleCourtCharms] Error forcing default notch costs: {e}");
                }
            });

            RandoController.OnExportCompleted += _ =>
            {
                _savedVanillaNotchCosts = null;

                lock (_defineLock)
                {
                    try
                    {
                        DefineObjects();
                        Interlocked.Exchange(ref _objectsDefined, 1);
                    }
                    catch (Exception e)
                    {
                        Modding.Logger.LogError($"[PaleCourtCharms] Exception in DefineObjects during OnExportCompleted: {e}");
                    }
                }

                bool randoDoesCost = RandomizerMod.RandomizerMod.RS?
                                        .GenerationSettings?
                                        .MiscSettings?
                                        .RandomizeNotchCosts ?? false;

                if (PaleCourtCharms.GlobalSettings.RandomizeCosts
                    && !randoDoesCost
                    && !ModulesRegistered)
                {
                    try
                    {
                        ItemChangerMod.Modules.Add<NotchCostUI>();
                        ItemChangerMod.Modules.Add<ZeroCostCharmEquip>();
                        ModulesRegistered = true;
                    }
                    catch (Exception e)
                    {
                        Modding.Logger.LogError($"[PaleCourtCharms] Failed to register modules: {e}");
                    }
                }
            };
        }

        private static void DefineObjects()
        {
            lock (_defineLock)
            {
                try
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
                catch (Exception e)
                {
                    Modding.Logger.LogError($"[PaleCourtCharms] DefineObjects threw: {e}");
                    throw;
                }
                try
                {
                    _crestDefCache = Finder.GetItemInternal(ItemNames.Defenders_Crest);
                }
                catch (Exception e)
                {
                    Modding.Logger.LogError($"[PaleCourtCharms] Error caching crest ItemDef: {e}");
                }

            }
        }

        private static void AddToPool(RequestBuilder rb)
        {
            if (Interlocked.CompareExchange(ref _addToPoolRunning, 1, 0) == 1)
                return;

            try
            {
                int opId = Environment.TickCount;

                foreach (var key in PaleCourtCharms.CharmKeys.Concat(new[] { HonourKey }))
                {
                    try
                    {
                        rb.RemoveLocationByName(key);
                        rb.AddLocationByName(key);
                        rb.AddItemByName(key);
                        rb.EditItemRequest(key, info =>
                        {
                            info.getItemDef = () => new ItemDef
                            {
                                Name = key,
                                Pool = PoolNames.Charm,
                                MajorItem = false,
                                PriceCap = 2000
                            };

                            if (key == HonourKey && rb.gs.PoolSettings.Charms)
                            {
                                info.realItemCreator = (factory, placement) =>
                                {
                                    var item = factory.MakeItem(HonourKey);
                                    item.tags ??= new List<Tag>();
                                    item.tags.Add(new ItemChainTag
                                    {
                                        predecessor = ItemNames.Defenders_Crest,
                                        successor = HonourKey
                                    });
                                    return item;
                                };
                            }
                        });
                    }
                    catch (Exception exInner)
                    {
                        Modding.Logger.LogError($"[PaleCourtCharms] AddToPool: failed handling key {key}: {exInner}");
                        throw;
                    }
                }
            }
            catch (ThreadAbortException)
            {
                Modding.Logger.LogWarn($"[PaleCourtCharms] AddToPool aborted by thread (ThreadAbortException).");
                throw;
            }
            catch (Exception e)
            {
                Modding.Logger.LogError($"[PaleCourtCharms] AddToPool threw: {e}");
                throw;
            }
            finally
            {
                Interlocked.Exchange(ref _addToPoolRunning, 0);
                Modding.Logger.Log("[PaleCourtCharms] AddToPool cleaned up running flag.");
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
            var rng = rb.rng;
            int vanillaTotal = rb.ctx.notchCosts.Sum();
            int minTotal = rb.gs.MiscSettings.MinRandomNotchTotal;
            int maxTotal = rb.gs.MiscSettings.MaxRandomNotchTotal;
            int variance = maxTotal - minTotal;
            int n = PaleCourtCharms.CharmKeys.Length;

            int minCount = Math.Max(0, (vanillaTotal - variance) / 10);
            int maxCount = Math.Min(n * 6, (vanillaTotal + variance) / 10);
            int count = rng.Next(minCount, maxCount + 1);

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
            try
            {
                foreach (var key in PaleCourtCharms.CharmKeys)
                    lmb.AddItem(new SingleItem(key, new TermValue(lmb.GetOrAddTerm(key), 1)));
                lmb.AddItem(new SingleItem(HonourKey, new TermValue(lmb.GetOrAddTerm(HonourKey), 1)));

                LoadAdditionalLogicFiles(lmb);
            }
            catch (Exception e)
            {
                Modding.Logger.LogError($"[PaleCourtCharms] InjectLogic threw: {e}");
                throw;
            }
        }

        private static void LoadAdditionalLogicFiles(LogicManagerBuilder lmb)
        {
            var modDir = System.IO.Path.GetDirectoryName(typeof(ItemHandler).Assembly.Location);
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

