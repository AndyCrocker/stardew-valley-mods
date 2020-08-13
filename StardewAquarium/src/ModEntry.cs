﻿using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using StardewModdingAPI;
using StardewAquarium.Models;
using StardewValley;
using Harmony;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewAquarium.Patches;
using StardewAquarium.Editors;
using StardewAquarium.Menus;
using StardewValley.Menus;
using Object = StardewValley.Object;

namespace StardewAquarium
{
    public partial class ModEntry : Mod
    {
        public static ModConfig Config;
        public static bool RecatchLegends;
        public static ModData Data;
        public const string PufferChickName = "Pufferchick";
        private readonly bool _isAndroid = Constants.TargetPlatform == GamePlatform.Android;
        public static HarmonyInstance Harmony { get; } = HarmonyInstance.Create("Cherry.StardewAquarium");

        public static IJsonAssetsApi JsonAssets { get; set; }
        public static ISpaceCoreAPI SpaceCore { get; set; }
        public override void Entry(IModHelper helper)
        {
            Utils.Initialize(Helper, Monitor,ModManifest);

            Helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            Helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            Helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;

            if (_isAndroid)
            {
                AndroidShopMenuPatch.Initialize(Helper, Monitor);
                Helper.Events.Display.MenuChanged += AndroidPlsHaveMercyOnMe;
            }

            new ReturnTrain(Helper, Monitor);
            new InteractionHandler(Helper,Monitor);

            Config = Helper.ReadConfig<ModConfig>();

            string dataPath = Path.Combine("data", "data.json");
            Data = helper.Data.ReadJsonFile<ModData>(dataPath);

            //disable if recatch legendary fish is installed
            if (Config.EnableRecatchWorthlessUndonatedLegends &&
                !Helper.ModRegistry.IsLoaded("cantorsdust.RecatchLegendaryFish"))
            {
                Monitor.Log("Enabling the recatch of legendaries...");
                RecatchLegends = true;
            }
            else
            {
                Monitor.Log("Disabling the recatch of legendaries from this mod. (if cantorsdust.RecatchLegendaryFish is installed, behaviour will default to that mod's)");
                RecatchLegends = false;
            }

            LegendaryFishPatches.Initialize(Helper, Monitor);


            if (Config.EnableDebugCommands)
            {
                if (_isAndroid)
                    Helper.ConsoleCommands.Add("donatefish", "", AndroidDonateFish);
                else
                    Helper.ConsoleCommands.Add("donatefish", "", OpenDonationMenuCommand);

                Helper.ConsoleCommands.Add("aquariumprogress", "", OpenAquariumCollectionMenu);
                Helper.ConsoleCommands.Add("removedonatedfish", "", RemoveDonatedFish);
            }
        }

        private void GameLoop_UpdateTicked(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
        {
            if (Game1.currentLocation?.Name != Data.ExteriorMapName) return;

            //This code was borrowed from East Scarpe

            // Very rarely show the Sea Monster.
            if (Game1.eventUp || !(Game1.random.NextDouble() < Data.DolphinChance))
                return;

            // Randomly find a starting position within the range.
            Vector2 position = 64f * new Vector2
            (Game1.random.Next(Data.DolphinRange.Left,
                    Data.DolphinRange.Right + 1),
                Game1.random.Next(Data.DolphinRange.Top,
                    Data.DolphinRange.Bottom + 1));

            var loc = Game1.currentLocation;
            // Confirm the monster can swim to the ocean from there.
            bool foundPosition = true;
            int height = loc.map.Layers[0]?.LayerHeight ?? 0;
            for (int y = (int)position.Y / 64; y < height; ++y)
            {
                if (loc.doesTileHaveProperty((int)position.X / 64, y, "Water", "Back") == null ||
                    loc.doesTileHaveProperty((int)position.X / 64 - 1, y, "Water", "Back") == null ||
                    loc.doesTileHaveProperty((int)position.X / 64 + 1, y, "Water", "Back") == null)
                {
                    foundPosition = false;
                    break;
                }
            }

            // Spawn if possible.
            if (foundPosition)
            {
                loc.temporarySprites.Add(new DolphinAnimatedSprite (position, Helper.Content.Load<Texture2D>("data\\dolphin.png")));
            }

        }

        private void AndroidPlsHaveMercyOnMe(object sender, StardewModdingAPI.Events.MenuChangedEventArgs e)
        {
            //don't ask me what the heck is going on here but its the only way to get it to work
            if (!(e.OldMenu is DonateFishMenuAndroid androidMenu)) return;
            //80% sure this is a DonateFishMenuAndroid but it won't work if i check for that but the harmony patch seems to work on it so idk
            if (!(e.NewMenu is ShopMenu menu)) return;

            menu.exitFunction = androidMenu.OnExit;
        }

        private void AndroidDonateFish(string arg1, string[] arg2)
        {
            Game1.activeClickableMenu = new DonateFishMenuAndroid(Helper, Monitor);
        }

        private void InitializeEditors()
        {
            Helper.Content.AssetEditors.Add(new AchievementEditor(Helper, Monitor));
            Helper.Content.AssetEditors.Add(new MiscEditor(Helper));
            Helper.Content.AssetEditors.Add(new FishEditor());
            Helper.Content.AssetEditors.Add(new MailEditor(Helper));
        }

        private void GameLoop_SaveLoaded(object sender, StardewModdingAPI.Events.SaveLoadedEventArgs e)
        {
            AquariumMessage.Initialize(Helper.Translation);
        }

        private void RemoveDonatedFish(string arg1, string[] arg2)
        {
            var mail = Game1.MasterPlayer.mailReceived;
            for (int i = mail.Count - 1; i >= 0; i--)
            {
                if (mail[i].StartsWith("AquariumDonated:") || mail[i].StartsWith("AquariumFishDonated:"))
                    mail.RemoveAt(i);
            }
        }

        private void OpenAquariumCollectionMenu(string arg1, string[] arg2)
        {
            Game1.activeClickableMenu = new AquariumCollectionMenu(Helper.Translation.Get("CollectionsMenu"));
        }

        private void OpenDonationMenuCommand(string arg1, string[] arg2)
        {
            Game1.activeClickableMenu = new DonateFishMenu(Helper,Monitor);
        }

        private void GameLoop_GameLaunched(object sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
        {
            JsonAssets = Helper.ModRegistry.GetApi<IJsonAssetsApi>("spacechase0.JsonAssets");
            JsonAssets.LoadAssets(Path.Combine(Helper.DirectoryPath, "data"));

            InitializeEditors();

            SpaceCore = Helper.ModRegistry.GetApi<ISpaceCoreAPI>("spacechase0.SpaceCore");
            SpaceCore.AddEventCommand("GiveAquariumTrophy1", typeof(ModEntry).GetMethod(nameof(GiveAquariumTrophy1)));
            SpaceCore.AddEventCommand("GiveAquariumTrophy2", typeof(ModEntry).GetMethod(nameof(GiveAquariumTrophy2)));
        }

        public static void GiveAquariumTrophy1(Event e, GameLocation loc, GameTime time, string[] args)
        {
            int id = JsonAssets.GetBigCraftableId("Stardew Aquarium Trophy");
            Object trophy = new Object(Vector2.Zero, id);
            e.farmer.holdUpItemThenMessage(trophy, true);
            ++e.CurrentCommand;
        }
        public static void GiveAquariumTrophy2(Event e, GameLocation loc, GameTime time, string[] args)
        {
            int id = JsonAssets.GetBigCraftableId("Stardew Aquarium Trophy");
            Object trophy = new Object(Vector2.Zero, id);
            e.farmer.addItemByMenuIfNecessary(trophy);
            if (Game1.activeClickableMenu == null)
                ++e.CurrentCommand;
            ++e.CurrentCommand;
        }
    }
}