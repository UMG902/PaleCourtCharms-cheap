using Modding;
namespace PaleCourtCharms.Rando
{
    internal static class RandoManager
    {
        public static void Hook()
        {
            ItemHandler.Hook();

           
            On.UIManager.StartNewGame += HandleNewGame;
        }

        private static void HandleNewGame(On.UIManager.orig_StartNewGame orig, UIManager self, bool permaDeath, bool bossRush)
        {
            orig(self, permaDeath, bossRush);

           if (ModHooks.GetMod("Randomizer 4") is Mod && RandomizerMod.RandomizerMod.IsRandoSave)
{
                PaleCourtCharms.Instance.StartGame();
}

        }
    }
}
