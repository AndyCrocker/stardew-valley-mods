using ShopTileFramework.Data;
using ShopTileFramework.Utility;
using StardewValley;
using StardewValley.Menus;
using System.Collections.Generic;
using ShopTileFramework.API;
using System.Linq;

namespace ShopTileFramework.Shop
{
    /// <summary>
    /// This class represents each animal store
    /// </summary>
    class AnimalShop : AnimalShopModel
    {

        private List<Object> _shopAnimalStock;
        private List<Object> _allAnimalsStock;

        internal static List<string> ExcludeFromMarnie = new List<string>();

        /// <summary>
        /// Translate what needs to be translated on game saved, in case of the language being changed
        /// </summary>
        public void UpdateTranslations()
        {
            ClosedMessage = Translations.Localize(ClosedMessage, LocalizedClosedMessage);
        }

        /// <summary>
        /// Updates the stock by grabbing the current data from getPurchaseAnimalStock and taking the info 
        /// for the animals that will be sold in this store
        /// </summary>
        private void UpdateShopAnimalStock()
        {
            //BFAV patches this anyways so it'll automatically work if installed
            _allAnimalsStock = StardewValley.Utility.getPurchaseAnimalStock();

            var animalStockNames = AnimalStock.Select(animalStockName => APIs.FAVR != null ? APIs.FAVR.GetInternalName(animalStockName) : animalStockName);
            _shopAnimalStock = new List<Object>();
            foreach (var animal in _allAnimalsStock)
            {
                if (animalStockNames.Contains(animal.Name))
                {
                    _shopAnimalStock.Add(animal);
                }
            }
        }
        public void DisplayShop(bool debug = false)
        {
            //skip condition checking if called from console commands
            if (debug || APIs.Conditions.CheckConditions(When))
            {
                //get animal stock each time to refresh requirement checks
                UpdateShopAnimalStock();

                //sets variables I use to control hardcoded warps
                ModEntry.SourceLocation = Game1.currentLocation;
                Game1.activeClickableMenu = new PurchaseAnimalsMenu(_shopAnimalStock);
            }
            else if (ClosedMessage != null)
            {
                Game1.activeClickableMenu = new DialogueBox(ClosedMessage);
            }
        }
    }
}