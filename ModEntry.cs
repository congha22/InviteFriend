
using InviteFriend;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;
using Netcode;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Minigames;
using StardewValley.Network;
using StardewValley.Objects;
using StardewValley.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using static InviteFriend.ModEntry;
using static System.Net.Mime.MediaTypeNames;
using Object = StardewValley.Object;

namespace InviteFriend
{
    public partial class ModEntry : Mod
    {
        static void Main(string[] args)
        {
            // Your program logic goes here
            Console.WriteLine("Hello, world!");
        }

        private static void NPC_dayUpdate_Postfix(NPC __instance)
        {
            if (!Config.EnableMod)
                return;
            __instance.modData["hapyke.FoodStore/inviteTried"] = "false";
            __instance.modData["hapyke.FoodStore/finishedDailyChat"] = "false";
        }

        private static void NPC_performTenMinuteUpdate_Postfix(NPC __instance)
        {
            try             //Warp invited NPC to and away
            {
                if (__instance.isVillager() && !Utility.isFestivalDay(Game1.dayOfMonth, Game1.currentSeason) && __instance.modData["hapyke.FoodStore/inviteDate"] == (Game1.stats.daysPlayed - 1).ToString())
                {
                    Random rand = new Random();
                    int index = rand.Next(7);
                    if (__instance.modData["hapyke.FoodStore/invited"] == "true" && Game1.timeOfDay == Config.InviteComeTime && __instance.currentLocation.Name != "Farm" && __instance.currentLocation.Name != "FarmHouse")
                    {
                        Game1.drawDialogue(__instance, SHelper.Translation.Get("foodstore.visitcome." + index));
                        Game1.globalFadeToBlack();


                        var door = Game1.getFarm().GetMainFarmHouseEntry();
                        door.X += 3 - index;
                        door.Y += 2;
                        var name = "Farm";

                        Game1.warpCharacter(__instance, name, door);

                        __instance.faceDirection(2);

                        door.X--;
                        __instance.controller = new PathFindController(__instance, Game1.getFarm(), door, 2);

                    }

                    if (__instance.modData["hapyke.FoodStore/invited"] == "true" && (__instance.currentLocation.Name == "Farm" || __instance.currentLocation.Name == "FarmHouse")
                        && (Game1.timeOfDay == Config.InviteLeaveTime || Game1.timeOfDay == Config.InviteLeaveTime + 30 || Game1.timeOfDay == Config.InviteLeaveTime + 100 || Game1.timeOfDay == Config.InviteLeaveTime + 130))
                    {
                        Game1.drawDialogue(__instance, SHelper.Translation.Get("foodstore.visitleave." + index));
                        Game1.globalFadeToBlack();

                        __instance.modData["hapyke.FoodStore/invited"] = "false";
                        __instance.controller = null;
                        __instance.clearSchedule();
                        __instance.ignoreScheduleToday = true;
                        Game1.warpCharacter(__instance, __instance.DefaultMap, new Point((int)__instance.DefaultPosition.X / 64, (int)__instance.DefaultPosition.Y / 64));
                    }
                }
            }
            catch { }

            if (!Config.EnableMod || Game1.eventUp || __instance.currentLocation is null || !__instance.isVillager())
                return;

            if (__instance.getTileLocation().X >= __instance.currentLocation.Map.DisplayWidth / 64 + 20 ||
                __instance.getTileLocation().Y >= __instance.currentLocation.Map.DisplayHeight / 64 + 20 ||
                __instance.getTileLocation().X <= -20 ||
                __instance.getTileLocation().Y <= -20 &&
                !__instance.IsReturningToEndPoint()
                )
            {
                __instance.returnToEndPoint();
                __instance.MovePosition(Game1.currentGameTime, Game1.viewport, __instance.currentLocation);
            }
        }

        public static IMonitor SMonitor;
        public static IModHelper SHelper;
        public static ModConfig Config;

        public static ModEntry context;

        internal static List<Response> ResponseList { get; private set; } = new();
        internal static List<Action> ActionList { get; private set; } = new();

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Config = Helper.ReadConfig<ModConfig>();

            context = this;

            SMonitor = Monitor;
            SHelper = helper;

            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            helper.Events.Multiplayer.ModMessageReceived += this.OnModMessageReceived;
            helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
            helper.Events.GameLoop.TimeChanged += this.OnTimeChange;
            helper.Events.Player.Warped += FarmOutside.PlayerWarp;
            helper.Events.GameLoop.DayEnding += GameLoop_DayEnding;

            var harmony = new Harmony(ModManifest.UniqueID);
            harmony.Patch(
               original: AccessTools.Method(typeof(NPC), nameof(NPC.performTenMinuteUpdate)),
               postfix: new HarmonyMethod(typeof(ModEntry), nameof(NPC_performTenMinuteUpdate_Postfix))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), nameof(NPC.dayUpdate)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(NPC_dayUpdate_Postfix))
             );
        }

        private void GameLoop_UpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Config.EnableMod)
                return;

            if (e.IsMultipleOf(6))
            {
                PlayerChat playerChatInstance = new PlayerChat();
                playerChatInstance.Validate();
            }

            if (Game1.hasLoadedGame && e.IsMultipleOf(30))
            {

                Farmer farmerInstance = Game1.player;
                NetStringDictionary<Friendship, NetRef<Friendship>> friendshipData = farmerInstance.friendshipData;

                try
                {
                    foreach (NPC __instance in Utility.getAllCharacters())
                    {
                        if (__instance.isVillager() && friendshipData.TryGetValue(__instance.Name, out var friendship) && !Game1.isFestival())
                        {
                            if (friendshipData[__instance.Name].TalkedToToday)
                            {
                                try
                                {
                                    if (__instance.CurrentDialogue.Count == 0)
                                    {
                                        Random random = new Random();
                                        int randomIndex = random.Next(19);
                                        __instance.CurrentDialogue.Push(new Dialogue(SHelper.Translation.Get("foodstore.customerresponse." + randomIndex), __instance));

                                        if (__instance.modData["hapyke.FoodStore/finishedDailyChat"] == "true")
                                        {
                                            var formattedQuestion = string.Format(SHelper.Translation.Get("foodstore.responselist.main"), __instance);
                                            var entryQuestion = new EntryQuestion(formattedQuestion, ResponseList, ActionList);
                                            Game1.activeClickableMenu = entryQuestion;

                                            var pc = new PlayerChat();
                                            ActionList.Add(() => pc.OnPlayerSend(__instance, "hi"));
                                            ActionList.Add(() => pc.OnPlayerSend(__instance, "invite"));
                                        }
                                        __instance.modData["hapyke.FoodStore/finishedDailyChat"] = "true";
                                    }
                                }
                                catch (Exception ex) { }
                            }
                        }
                    }
                }
                catch (NullReferenceException) { }

            }
        }

        public class MyMessage
        {
            public string MessageContent { get; set; }
            public MyMessage() { }

            public MyMessage(string content)
            {
                MessageContent = content;
            }
        }       //Send and receive message

        public void OnModMessageReceived(object sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID == this.ModManifest.UniqueID && e.Type == "ExampleMessageType")
            {
                MyMessage message = e.ReadAs<MyMessage>();
                Game1.chatBox.addInfoMessage(message.MessageContent);
                // handle message fields here
            }
        }       //Send and receive message

        private void GameLoop_GameLaunched(object sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
        {
            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );


            configMenu.AddBoolOption(
            mod: ModManifest,
                name: () => SHelper.Translation.Get("foodstore.config.enable"),
                getValue: () => Config.EnableMod,
                setValue: value => Config.EnableMod = value
            );

            //Villager invite
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("foodstore.config.enablevisitinside"),
                getValue: () => Config.EnableVisitInside,
                setValue: value => Config.EnableVisitInside = value
            );
            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("foodstore.config.invitecometime"),
                getValue: () => "" + Config.InviteComeTime,
                setValue: delegate (string value) { try { Config.InviteComeTime = float.Parse(value, CultureInfo.InvariantCulture); } catch { } }
            );
            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("foodstore.config.inviteleavetime"),
                getValue: () => "" + Config.InviteLeaveTime,
                setValue: delegate (string value) { try { Config.InviteLeaveTime = float.Parse(value, CultureInfo.InvariantCulture); } catch { } }
            );
        }       // **** Config Handle ****

        private void GameLoop_SaveLoaded(object sender, StardewModdingAPI.Events.SaveLoadedEventArgs e)
        {
            if (!Config.EnableMod)
                return;

            //Send thanks
            Game1.chatBox.addInfoMessage(SHelper.Translation.Get("foodstore.thankyou"));
            MyMessage messageToSend = new MyMessage(SHelper.Translation.Get("foodstore.thankyou"));
            SHelper.Multiplayer.SendMessage(messageToSend, "ExampleMessageType");

            if (ResponseList?.Count is not 2)
            {
                ResponseList.Add(new Response("Talk", SHelper.Translation.Get("foodstore.responselist.talk")));
                ResponseList.Add(new Response("Invite", SHelper.Translation.Get("foodstore.responselist.invite")));
            }

            //Assign visit value
            foreach (NPC __instance in Utility.getAllCharacters())
            {
                if (__instance.isVillager())
                {
                    __instance.modData["hapyke.FoodStore/invited"] = "false";
                    __instance.modData["hapyke.FoodStore/inviteDate"] = "-99";
                }
            }
        }

        private void GameLoop_DayEnding(object sender, DayEndingEventArgs e)    //Wipe invitation on the day supposed to visit
        {
            try
            {
                foreach (NPC __instance in Utility.getAllCharacters())
                {
                    if (__instance.isVillager() && __instance.modData["hapyke.FoodStore/invited"] == "true"
                        && __instance.modData["hapyke.FoodStore/inviteDate"] == (Game1.stats.daysPlayed - 1).ToString())
                    {
                        __instance.modData["hapyke.FoodStore/invited"] = "false";
                        __instance.modData["hapyke.FoodStore/inviteDate"] = "-99";
                    }
                }
            }
            catch { }
        }

        private void OnTimeChange(object sender, TimeChangedEventArgs e)
        {
            if (Game1.timeOfDay > Config.InviteComeTime)
            {
                foreach (NPC c in Utility.getAllCharacters())
                {
                    if (c.isVillager() && c.currentLocation.Name == "Farm" && c.modData["hapyke.FoodStore/invited"] == "true" && c.modData["hapyke.FoodStore/inviteDate"] == (Game1.stats.daysPlayed - 1).ToString())
                    {
                        FarmOutside.WalkAroundFarm(c.Name);
                    }

                    if (c.isVillager() && c.currentLocation.Name == "FarmHouse" && c.modData["hapyke.FoodStore/invited"] == "true" && c.modData["hapyke.FoodStore/inviteDate"] == (Game1.stats.daysPlayed - 1).ToString())
                    {
                        FarmOutside.WalkAroundHouse(c.Name);
                    }
                }
            }
        }

        public static bool IsOutside { get; internal set; }
        internal static List<string> FurnitureList { get; private set; } = new();
        internal static List<string> Animals { get; private set; } = new();
        internal static Dictionary<int, string> Crops { get; private set; } = new();
    }
}
