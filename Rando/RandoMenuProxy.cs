using PaleCourtCharms;

namespace PaleCourtCharms.Rando {
internal static class RandoMenuProxy {
  public static RandoSettings RS {
    get => new RandoSettings {
      Enabled        = PaleCourtCharms.GlobalSettings.AddCharms,
      RandomizeCosts = PaleCourtCharms.GlobalSettings.RandomizeCosts
    };
    set {
      PaleCourtCharms.GlobalSettings.AddCharms      = value.Enabled;
      PaleCourtCharms.GlobalSettings.RandomizeCosts = value.RandomizeCosts;
      
    }
  }
}
}
