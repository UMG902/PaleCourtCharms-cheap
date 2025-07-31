
using MenuChanger.Attributes;

namespace PaleCourtCharms.Rando {
  public class LogicSettings {
    public bool Purity;
    public bool Lament;
    public bool Boon;
    public bool Bloom;
    [MenuLabel("King's Honour Logic")]
    public bool KingsHonour;

    public bool AnyEnabled() =>
      Purity || Lament || Boon || Bloom || KingsHonour;
  }
}
