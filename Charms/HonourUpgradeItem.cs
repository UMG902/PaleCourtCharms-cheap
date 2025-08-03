using System.Collections.Generic;
using ItemChanger;
using ItemChanger.Tags;
using ItemChanger.UIDefs;
using Modding;
using UnityEngine;

namespace PaleCourtCharms
{
 public class HonourUpgradeItem : AbstractItem
{
   
    private const string HonourKey = "Kings_Honour";

  public HonourUpgradeItem()
{
    name = HonourKey;


    string honourName    = PaleCourtCharms.LangStrings.Get("CHARM_NAME_HONOUR","UI");
  
    string honourShopDesc = PaleCourtCharms.LangStrings.Get("SHOP_DESCRIPTION_HONOUR", "RANDO");
//this nonsense is needed so it just says "king's honour" in the shop and doesn't add a fake [-9999] cost
//apparently keeps the language you had on at the time of creating the save
            UIDef = new MsgUIDef
    {
        name     = new BoxedString(honourName),
        shopDesc = new BoxedString(honourShopDesc),
        sprite   = new ICShiny.EmbeddedSprite { key = HonourKey }
    };
}


        public override void GiveImmediate(GiveInfo info)
        {

            PaleCourtCharms.Settings.upgradedCharm_10 = true;
            PlayerData.instance.SetBool("upgradedCharm_10", true);
            
        GameManager.instance.SaveGame();
    }

    public override bool Redundant()
    {
        
        return PlayerData.instance.GetBool("gotCharm_10") &&
               PaleCourtCharms.Settings.upgradedCharm_10;
    }
}

}

