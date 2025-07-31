using System.Collections.Generic;
using UnityEngine;
using MenuChanger;
using MenuChanger.Extensions;
using MenuChanger.MenuElements;
using MenuChanger.MenuPanels;
using RandomizerMod.Menu;

namespace PaleCourtCharms.Rando
{
    public class ConnectionMenu
    {
        internal static ConnectionMenu Instance { get; private set; }
        private readonly SmallButton pageRootButton;
        private readonly MenuPage accessPage;
        private readonly MenuElementFactory<RandoSettings> topLevelFactory;

        public static void Hook()
        {
            RandomizerMenuAPI.AddMenuPage(ConstructMenu, HandleButton);
            MenuChangerMod.OnExitMainMenu += () => Instance = null;
        }

        private static bool HandleButton(MenuPage landingPage, out SmallButton button)
        {
            button = Instance.pageRootButton;
            bool enabled = PaleCourtCharms.GlobalSettings.AddCharms;
            button.Text.color = enabled ? Colors.TRUE_COLOR : Colors.DEFAULT_COLOR;
            return true;
        }

        private static void ConstructMenu(MenuPage landingPage)
        {
            Instance = new ConnectionMenu(landingPage);
        }

        private ConnectionMenu(MenuPage landingPage)
        {
            
            accessPage = new MenuPage("Pale Court Charms", landingPage);
            topLevelFactory = new MenuElementFactory<RandoSettings>(accessPage, RandoMenuProxy.RS);
            _ = new VerticalItemPanel(
                accessPage,
                new Vector2(0, 200),
                48f,
                true,
                topLevelFactory.Elements
            );

           
            pageRootButton = new SmallButton(landingPage, "Pale Court Charms");
            pageRootButton.AddHideAndShowEvent(landingPage, accessPage);

            landingPage.BeforeShow += () =>
            {
                bool both = RandoMenuProxy.RS.Enabled && RandoMenuProxy.RS.RandomizeCosts;
                pageRootButton.Text.color = both ? Colors.TRUE_COLOR : Colors.FALSE_COLOR;
            };

            accessPage.BeforeHide += () =>
            {
                var settings = new RandoSettings
                {
                    Enabled = (bool)topLevelFactory.ElementLookup[nameof(RandoSettings.Enabled)].Value,
                    RandomizeCosts = (bool)topLevelFactory.ElementLookup[nameof(RandoSettings.RandomizeCosts)].Value
                };
                RandoMenuProxy.RS = settings;
                topLevelFactory.SetMenuValues(settings);
                PaleCourtCharms.GlobalSettings.AddCharms = settings.Enabled;
                PaleCourtCharms.GlobalSettings.RandomizeCosts = settings.RandomizeCosts;
            };
        }

     
        public void Apply(RandoSettings settings)
        {
            topLevelFactory.SetMenuValues(settings);
        }

     
        public void Disable()
        {
            var elem = topLevelFactory.ElementLookup[nameof(RandoSettings.Enabled)];
            elem.SetValue(false);
        }
    }
}
