using PaleCourtCharms; 

namespace PaleCourtCharms.Rando
{
    public class RandoSettings
    {
         public bool Enabled { get; set; }
    public bool RandomizeCosts { get; set; }
    public static RandoSettings FromGlobal(GlobalSettings g) {
    return new RandoSettings {
      Enabled        = g.AddCharms,
      RandomizeCosts = g.RandomizeCosts
    };
  }

       
        public void ApplyTo(SaveModSettings s)
        {
            if (!Enabled)
                s.EnabledCharms.Clear();

            PaleCourtCharms.GlobalSettings.RandomizeCosts = RandomizeCosts;
        }

        public bool IsEnabled() => Enabled;
    }
}
