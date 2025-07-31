
using ItemChanger;
using ItemChanger.Locations;   
using ItemChanger.Tags;        
using ItemChanger.UIDefs;      


using RandomizerMod.Settings;       
using RandomizerMod.RandomizerData; 

namespace PaleCourtCharms.Rando {
  public class PC_CharmLocation : CoordinateLocation {
    public PC_CharmLocation(string internalName, string scene, float x, float y) {
      name      = internalName;
      sceneName = scene;
      this.x    = x;
      this.y    = y;
      elevation = 0;

      var t = AddTag<InteropTag>();
      t.Message               = "RandoSupplementalMetadata";
      t.Properties["ModSource"]   = PaleCourtCharms.Instance.GetName();
      t.Properties["PoolGroup"]   = PoolNames.Charm;
      t.Properties["VanillaItem"] = internalName;
    }
  }
}
