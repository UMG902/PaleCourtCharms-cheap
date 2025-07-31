using RandoSettingsManager;
using RandoSettingsManager.SettingsManagement;
using RandoSettingsManager.SettingsManagement.Versioning;
using PaleCourtCharms.Rando;

namespace PaleCourtCharms.Interop
{
    internal static class RSM_Interop
    {
        public static void Hook()
        {
            RandoSettingsManagerMod.Instance.RegisterConnection(new PaleCourtSettingsProxy());
        }
    }

    internal class PaleCourtSettingsProxy : RandoSettingsProxy<RandoSettings, string>
    {
        public override string ModKey => PaleCourtCharms.Instance.GetName();

        public override VersioningPolicy<string> VersioningPolicy { get; }
            = new EqualityVersioningPolicy<string>(PaleCourtCharms.Instance.GetVersion());

        public override bool TryProvideSettings(out RandoSettings settings)
        {
            settings = RandoSettings.FromGlobal(PaleCourtCharms.GlobalSettings);
            return settings.Enabled;
        }

        public override void ReceiveSettings(RandoSettings settings)
        {
            if (settings != null)
            {
                PaleCourtCharms.GlobalSettings.AddCharms = settings.Enabled;
                PaleCourtCharms.GlobalSettings.RandomizeCosts = settings.RandomizeCosts;
                ConnectionMenu.Instance.Apply(settings);
            }
            else
            {
                ConnectionMenu.Instance.Disable();
            }
        }
    }
}
