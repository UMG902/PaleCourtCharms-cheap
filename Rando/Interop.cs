using ItemChanger;

namespace PaleCourtCharms.Rando {
  internal static class Interop {
    public static void Setup(GlobalSettings gs, PaleCourtCharms main)
    {
      LogicHandler.Hook();
      ItemHandler.Hook();
      ConnectionMenu.Hook();
      Events.AfterStartNewGame += () => PaleCourtCharms.Instance.StartGame();
    }
  }
}
