
using System;


using ItemChanger;
using ItemChanger.Locations;   
using ItemChanger.Tags;        
using ItemChanger.UIDefs;      


using RandomizerMod.RC;             
using RandomizerMod.Settings;       
using RandomizerMod.RandomizerData; 


namespace PaleCourtCharms.Rando
{
  public class PC_CharmItem : AbstractItem
  {public override void GiveImmediate(GiveInfo info)
{
    if (CharmIndex < 0 || CharmIndex >= PaleCourtCharms.CharmIDs.Count)
    {
        PaleCourtCharms.Instance.Log($"[PC_CharmItem] Invalid CharmIndex {CharmIndex}.");
        return;
    }

    int charmNum = PaleCourtCharms.CharmIDs[CharmIndex];
    PlayerData.instance.SetBool($"gotCharm_{charmNum}", true);
    PlayerData.instance.SetBool($"newCharm_{charmNum}", true);
    PlayerData.instance.SetBool("equippedCharm_" + charmNum, false);

   
}


    public int CharmIndex;

    public PC_CharmItem(int idx)
{
    CharmIndex = idx;

    if (idx < 0 || idx >= PaleCourtCharms.CharmKeys.Length)
        throw new ArgumentOutOfRangeException(nameof(idx), $"Charm index {idx} is out of range.");

    string key = PaleCourtCharms.CharmKeys[idx];
    name = key;

    if (idx >= PaleCourtCharms.CharmIDs.Count)
        throw new InvalidOperationException($"CharmIDs not yet initialized or missing index {idx}");

    int charmNum = PaleCourtCharms.CharmIDs[idx];

    UIDef = new MsgUIDef
    {
        name = new LanguageString("UI", $"CHARM_NAME_{charmNum}"),
        shopDesc = new BoxedString(PaleCourtCharms.Charms[CharmIndex].ShopDesc),
        sprite = new ICShiny.EmbeddedSprite { key = key }

    };

    var t = AddTag<InteropTag>();
    t.Message = "RandoSupplementalMetadata";
    t.Properties["ModSource"] = PaleCourtCharms.Instance.GetName();
    t.Properties["PoolGroup"] = PoolNames.Charm;
}

  }
}