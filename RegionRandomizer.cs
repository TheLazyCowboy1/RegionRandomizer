using BepInEx;
using RWCustom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System;
using UnityEngine;
using static RegionRandomizer.LogicalRando;

#pragma warning disable CS0618

[module: UnverifiableCode]
[assembly: SecurityPermission(System.Security.Permissions.SecurityAction.RequestMinimum, SkipVerification = true)]

namespace RegionRandomizer;

[BepInDependency("rwmodding.coreorg.rk", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("LazyCowboy.KarmaExpansion", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("henpemaz.rainmeadow", BepInDependency.DependencyFlags.SoftDependency)]

[BepInPlugin("LazyCowboy.RegionRandomizer", "Region Randomizer", "1.2.1")]
public partial class RegionRandomizer : BaseUnityPlugin
{

    /*
     * TODO Notes:
     * 
     * Add a "hard" option that changes the region pathing. Instead of setting echoes in the same method as finding regions,
     * go through one gate at a time (like a player would). This will require player progression to be more linear.
     * 
     * "Replacename" gates might not actually change the name. So we might need to shorten gate names to a length of 10
     * at the end of the randomizer logic process, so the GateSwitch hook can find the right gate.
    */

    public static RegionRandomizerOptions Options;

    public static RegionRandomizer Instance;

    public RegionRandomizer()
    {
        try
        {
            Instance = this;
            Options = new RegionRandomizerOptions(this, Logger);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            throw;
        }
    }

    private void OnEnable()
    {
        On.RainWorld.OnModsInit += RainWorldOnOnModsInit;
        //RegionLoader.Enable();
    }

    private void OnDisable()
    {
        //RegionLoader.Disable();
        if (IsInit)
        {
            On.OverWorld.GateRequestsSwitchInitiation -= OverWorld_GateRequestsSwitchInitiation;
            On.OverWorld.WorldLoaded -= OverWorld_WorldLoaded;

            On.SaveState.SessionEnded -= SaveState_SessionEnded;
            On.RainWorldGame.ExitGame -= RainWorldGame_ExitGame;

            On.Expedition.ExpeditionGame.ExpeditionRandomStarts -= ExpeditionGame_ExpeditionRandomStarts;
            On.PlayerProgression.GetOrInitiateSaveState -= PlayerProgression_GetOrInitiateSaveState;

            On.ShortcutGraphics.GenerateSprites -= ShortcutGraphics_GenerateSprites;

            On.HUD.Map.GateMarker.ctor -= GateMarker_ctor;
            On.GateKarmaGlyph.DrawSprites -= GateKarmaGlyph_DrawSprites;
            On.RegionGate.Update -= RegionGate_Update;
            On.RegionGate.KarmaBlinkRed -= RegionGate_KarmaBlinkRed;

            On.RegionGate.customKarmaGateRequirements -= RegionGate_customKarmaGateRequirements;
            On.HUD.Map.MapData.KarmaOfGate -= MapData_KarmaOfGate;
            On.RegionGate.customOEGateRequirements -= RegionGate_customOEGateRequirements;

            On.RainWorldGame.ShutDownProcess -= RainWorldGameOnShutDownProcess;
            On.GameSession.ctor -= GameSessionOnctor;
        }
    }

    //private static RainWorldGame game;

    private static Dictionary<string, string> CustomGateLocks = new(); //two locks separated by a colon. e.g 5:8 or 12:c

    public static int KarmaCap = 10;


    private bool IsInit;
    private void RainWorldOnOnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);
        try
        {
            if (IsInit) return;

            //Your hooks go here
            //RegionLoader.Enable();
            On.OverWorld.GateRequestsSwitchInitiation += OverWorld_GateRequestsSwitchInitiation;
            //On.AbstractRoom.Abstractize += AbstractRoom_Abstractize;
            On.OverWorld.WorldLoaded += OverWorld_WorldLoaded;

            On.SaveState.SessionEnded += SaveState_SessionEnded;
            On.RainWorldGame.ExitGame += RainWorldGame_ExitGame;

            //On.PlayerProgression.WipeSaveState += PlayerProgression_WipeSaveState;
            //On.Menu.SlugcatSelectMenu.StartGame += SlugcatSelectMenu_StartGame;
            On.Expedition.ExpeditionGame.ExpeditionRandomStarts += ExpeditionGame_ExpeditionRandomStarts;
            On.PlayerProgression.GetOrInitiateSaveState += PlayerProgression_GetOrInitiateSaveState;

            On.ShortcutGraphics.GenerateSprites += ShortcutGraphics_GenerateSprites;

            On.HUD.Map.GateMarker.ctor += GateMarker_ctor;
            On.GateKarmaGlyph.DrawSprites += GateKarmaGlyph_DrawSprites;
            On.RegionGate.Update += RegionGate_Update;
            On.RegionGate.KarmaBlinkRed += RegionGate_KarmaBlinkRed;

            On.RegionGate.customKarmaGateRequirements += RegionGate_customKarmaGateRequirements;
            On.HUD.Map.MapData.KarmaOfGate += MapData_KarmaOfGate;
            On.RegionGate.customOEGateRequirements += RegionGate_customOEGateRequirements;

            new RegionGate.GateRequirement("o", true);
            new RegionGate.GateRequirement("k", true);
            new RegionGate.GateRequirement("f", true);
            new RegionGate.GateRequirement("c", true);
            new RegionGate.GateRequirement("u", true);
            new RegionGate.GateRequirement("g", true);

            //On.Player.ctor += PlayerOnctor;

            On.RainWorldGame.ShutDownProcess += RainWorldGameOnShutDownProcess;
            On.GameSession.ctor += GameSessionOnctor;

            MachineConnector.SetRegisteredOI("LazyCowboy.RegionRandomizer", Options);
            IsInit = true;

            Options.BindRegionsTab();

            //previous version fixes
            PreviousVersionFixes();

            //check if custom karma mod installed
            foreach(ModManager.Mod mod in ModManager.ActiveMods)
            {
                if (mod.id == "LazyCowboy.KarmaExpansion")
                {
                    KarmaCap = 22;
                }
                if (mod.id == "henpemaz.rainmeadow")
                {
                    meadowEnabled = true;
                }
            }

            //double register extra karma levels...?
            //for (int i = 11; i <= KarmaCap; i++)
                //new RegionGate.GateRequirement(i.ToString(), true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            throw;
        }
    }
    

    //alters gate locks without manually merging changes into locks.txt
    public static void RegionGate_customKarmaGateRequirements(On.RegionGate.orig_customKarmaGateRequirements orig, RegionGate self)
    {
        orig(self);
        /*
        self.room.game.rainWorld.HandleLog("RegionRandomizer: CustomKarmaRequirements", "stuff", LogType.Log);
        string karma0 = self.karmaRequirements[0].value, karma1 = self.karmaRequirements[1].value;
        orig(self);
        if (self.karmaRequirements[0].value != karma0)
        { self.karmaRequirements[0].value = karma0; self.room.game.rainWorld.HandleLog("RegionRandomizer: Reset karma[0] to " + karma0, "stuff", LogType.Log); }
        if (self.karmaRequirements[1].value != karma1)
        { self.karmaRequirements[1].value = karma1; self.room.game.rainWorld.HandleLog("RegionRandomizer: Reset karma[1] to " + karma1, "stuff", LogType.Log); }
        */

        //alter gate locks
        if (Options.alterGateLocks.Value && CustomGateLocks.TryGetValue(self.room.abstractRoom.name, out string lockString) && lockString.Contains(':'))
        {
            string[] locks = lockString.Split(':');
            self.karmaRequirements[0].value = locks[0];
            self.karmaRequirements[1].value = locks[1];
            LogSomething("Set custom gate locks for " + self.room.abstractRoom.name + ": " + locks[0] + ", " + locks[1]);
        }

    }
    //alters the map symbols, again without manually merging locks.txt
    public static RegionGate.GateRequirement MapData_KarmaOfGate(On.HUD.Map.MapData.orig_KarmaOfGate orig, HUD.Map.MapData self, PlayerProgression progression, World initWorld, string roomName)
    {
        RegionGate.GateRequirement origRequirement = orig(self, progression, initWorld, roomName);

        if (Options.alterGateLocks.Value && CustomGateLocks.TryGetValue(roomName, out string locks) && locks.Contains(':')
            && origRequirement != null && origRequirement?.value != null)
        {
            //look through locks file to figure out if mapswapped or not
            bool mapSwapped = false;
            foreach (string line in progression.karmaLocks)
            {
                string[] data = Regex.Split(line, " : ");
                if (data[0] == roomName)
                {
                    if (data.Length >= 4 && data[3] == "SWAPMAPSYMBOL")
                        mapSwapped = true;
                    break;
                }
            }

            //correct karma value
            if (Region.EquivalentRegion(Regex.Split(roomName, "_")[1], initWorld.region.name) != mapSwapped)
            {
                origRequirement.value = locks.Split(':')[0];
            }
            else
            {
                origRequirement.value = locks.Split(':')[1];
            }
        }

        return origRequirement;
    }

    public static bool RegionGate_customOEGateRequirements(On.RegionGate.orig_customOEGateRequirements orig, RegionGate self)
    {
        self.room.game.rainWorld.HandleLog("RegionRandomizer: OEKarmaRequirements", "stuff", LogType.Log);
        if (self.karmaRequirements[0] != MoreSlugcats.MoreSlugcatsEnums.GateRequirement.OELock && self.karmaRequirements[1] != MoreSlugcats.MoreSlugcatsEnums.GateRequirement.OELock)
            return true;
        return orig(self);
    }

    public static void ShortcutGraphics_GenerateSprites(On.ShortcutGraphics.orig_GenerateSprites orig, ShortcutGraphics self)
    {
        try { orig(self); }
        catch (Exception ex)
        {
            self.room.game.rainWorld.HandleLog("RegionRandomizer: " + ex.Message, ex.StackTrace, LogType.Error);
        }
    }

    #region RegionKitFixes 

    private static void GateMarker_ctor(On.HUD.Map.GateMarker.orig_ctor orig, HUD.Map.GateMarker self, HUD.Map map, int room, RegionGate.GateRequirement karma, bool showAsOpen)
    {
        try
        {
            int k = 6;
            try { k = Int32.Parse(karma.value); } catch (Exception ex) { }
            if (k > 5 && k <= 10) // above 5 karma support, but doesn't operate for custom karma above 10
            {
                orig(self, map, room, new RegionGate.GateRequirement("0"), showAsOpen);
                // Debug.Log("ExtendedGates: Map.GateMarker_ctor got karma" + karma);
                switch (karma.value.Substring(0,1).ToLower())
                {
                    case "o": // open
                        self.symbolSprite.element = Futile.atlasManager.GetElementWithName("smallKarmaOpen"); // Custom
                        break;
                    case "k": // 10reinforced
                        self.symbolSprite.element = Futile.atlasManager.GetElementWithName("smallKarma10reinforced"); // Custom
                        break;
                    case "f": // forbidden
                        self.symbolSprite.element = Futile.atlasManager.GetElementWithName("smallKarmaForbidden"); // Custom
                        break;
                    case "c": // comsmark
                        self.symbolSprite.element = Futile.atlasManager.GetElementWithName("smallKarmaComsmark"); // Custom
                        break;
                    case "u": // uwu
                        self.symbolSprite.element = Futile.atlasManager.GetElementWithName("smallKarmaUwu"); // Custom
                        break;
                    case "g": // glow
                        self.symbolSprite.element = Futile.atlasManager.GetElementWithName("smallKarmaGlow"); // Custom
                        break;

                    default: // Alt art gates
                        k--; //because this code is designed for karma - 1 I think?
                        if (k >= 1000)
                        {
                            k -= 1000;
                            //altArt = true; // irrelevant in this case
                        }
                        
                        if (k > 4)
                        {
                            int? cap = map.hud.rainWorld.progression?.currentSaveState?.deathPersistentSaveData?.karmaCap;
                            if (!cap.HasValue || cap.Value < k) cap = Mathf.Max(6, k);
                            cap = Math.Min(cap.Value, 9);
                            self.symbolSprite.element = Futile.atlasManager.GetElementWithName("smallKarma" + k.ToString() + "-" + cap.Value.ToString()); // Vanilla, zero-indexed
                        }
                        else
                        {
                            self.symbolSprite.element = Futile.atlasManager.GetElementWithName("smallKarmaNoRing" + k);
                        }
                        break;
                }

                if (karma.value.ToLower() == "10reinforced")
                    self.symbolSprite.element = Futile.atlasManager.GetElementWithName("smallKarma10reinforced");
            }
            else
            {
                orig(self, map, room, karma, showAsOpen);
            }
        } catch (Exception ex)
        {
            orig(self, map, room, karma, showAsOpen);
        }
    }

    private static void RegionGate_Update(On.RegionGate.orig_Update orig, RegionGate self, bool eu)
    {
        int preupdateCounter = self.startCounter;
        orig(self, eu);
        if (!self.room.game.IsStorySession) return; // Paranoid, just like in the base game
        if (self.mode == RegionGate.Mode.MiddleClosed)
        {
            //manual comsmark check
            //if (self.karmaRequirements[(!self.letThroughDir) ? 1 : 0].value == "c" && !self.room.game.GetStorySession.saveState.deathPersistentSaveData.theMark)
            //{ self.dontOpen = true; self.room.world.game.rainWorld.HandleLog("RegionRandomizer: Manual comsmark check triggered!", "stuff", LogType.Log);  }

            int num = self.PlayersInZone();
            if (num > 0 && num < 3 && !self.dontOpen && self.PlayersStandingStill() && self.EnergyEnoughToOpen && PlayersMeetSpecialRequirements(self))
            {
                self.startCounter = preupdateCounter + 1;
            }

            if (self.startCounter == 69)
            {
                // OPEN THE GATES on the next frame
                if (self.room.game.GetStorySession.saveStateNumber == SlugcatStats.Name.Yellow)
                {
                    self.Unlock(); // sets savestate thing for monk
                }
                self.unlocked = true;
            }
        }
        else if (self.mode == RegionGate.Mode.ClosingAirLock)
        {
            if (preupdateCounter == 69) // We did it... last frame
            {
                // if it shouldn't be unlocked, lock it back
                self.unlocked = (self.room.game.GetStorySession.saveStateNumber == SlugcatStats.Name.Yellow && self.room.game.GetStorySession.saveState.deathPersistentSaveData.unlockedGates.Contains(self.room.abstractRoom.name)) || (self.room.game.StoryCharacter != SlugcatStats.Name.Red && File.Exists(RWCustom.Custom.RootFolderDirectory() + "nifflasmode.txt"));
            }

            if (self.room.game.overWorld.worldLoader == null) // In-region gate support
                self.waitingForWorldLoader = false;
        }
        else if (self.mode == RegionGate.Mode.Closed)
        {
            if (self.EnergyEnoughToOpen) self.mode = RegionGate.Mode.MiddleClosed;
        }
    }

    private static bool RegionGate_KarmaBlinkRed(On.RegionGate.orig_KarmaBlinkRed orig, RegionGate self)
    {
        if (self.mode != RegionGate.Mode.MiddleClosed)
        {
            int num = self.PlayersInZone();
            if (num > 0 && num < 3)
            {
                //self.letThroughDir = (num == 1);
                if (!self.dontOpen && self.karmaRequirements[(!(num == 1)) ? 1 : 0].value.Substring(0, 1).ToLower() == "f") // Forbidden
                {
                    return true;
                }
            }
        }
        // Orig doesn't blink if "unlocked", but we know better, forbiden shall stay forbidden
        return orig(self) && !PlayersMeetSpecialRequirements(self);
    }

    private static bool PlayersMeetSpecialRequirements(RegionGate self)
    {
        switch (self.karmaRequirements[(!self.letThroughDir) ? 1 : 0].value.Substring(0, 1).ToLower())
        {
            case "o": // open
                return true;
            case "k": // 10reinforced
                if (((self.room.game.Players[0].realizedCreature as Player).Karma == 9 && (self.room.game.Players[0].realizedCreature as Player).KarmaIsReinforced) || self.unlocked)
                    return true;
                break;
            case "f": // forbidden
                self.startCounter = 0;
                // caused problems with karmablinkred
                // self.dontOpen = true; // Hope this works against MONK players smh.
                break;
            case "c": // comsmark
                if (self.room.game.GetStorySession.saveState.deathPersistentSaveData.theMark || self.unlocked)
                    return true;
                break;
            //case "u": // uwu
            //    if (uwu != null || self.unlocked)
            //        return true;
            //    break;
            case "g": // glow
                if (self.room.game.GetStorySession.saveState.theGlow || self.unlocked)
                    return true;
                break;
            default: // default karma gate handled by the game
                break;
        }

        if (self.karmaRequirements[(!self.letThroughDir) ? 1 : 0].value.ToLower() == "10reinforced"
            && ((self.room.game.Players[0].realizedCreature as Player).Karma == 9 && (self.room.game.Players[0].realizedCreature as Player).KarmaIsReinforced) || self.unlocked)
            return true;

        return false;
    }

    private static void GateKarmaGlyph_DrawSprites(On.GateKarmaGlyph.orig_DrawSprites orig, GateKarmaGlyph self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, UnityEngine.Vector2 camPos)
    {
        if (self.symbolDirty) // redraw
        {
            self.room.game.rainWorld.HandleLog("RegionRandomizer: Drawing gate symbol: " + self.requirement.value, "stuff", LogType.Log);
            bool altArt = false;
            //bool altArt = DoesPlayerDeserveAltArt(self); // this was probably too costly to call every frame, moved
            int parseTest = 6;
            try { parseTest = Int32.Parse(self.requirement.value); } catch (Exception ex) { }
            if ((!self.gate.unlocked || self.requirement.value == "f") && (parseTest > 5 || altArt) && parseTest <= 10) // Custom
            {
                switch (self.requirement.value.Substring(0, 1).ToLower())
                {
                    case "o": // open
                        sLeaser.sprites[1].element = Futile.atlasManager.GetElementWithName("gateSymbol0"); // its vanilla
                        break;
                    case "k": // 10reinforced
                        sLeaser.sprites[1].element = Futile.atlasManager.GetElementWithName("gateSymbol10reinforced"); // Custom
                        break;
                    case "f": // forbidden
                        sLeaser.sprites[1].element = Futile.atlasManager.GetElementWithName("gateSymbolForbidden"); // Custom
                        break;
                    case "c": // comsmark
                        self.room.game.rainWorld.HandleLog("RegionRandomizer: Drawing comsmark symbol", "stuff", LogType.Log);
                        sLeaser.sprites[1].element = Futile.atlasManager.GetElementWithName("gateSymbolComsmark"); // Custom
                        break;
                    case "u": // uwu
                        sLeaser.sprites[1].element = Futile.atlasManager.GetElementWithName("gateSymbolUwu"); // Custom
                        break;
                    case "g": // glow
                        sLeaser.sprites[1].element = Futile.atlasManager.GetElementWithName("gateSymbolGlow"); // Custom
                        break;

                    default:
                        //int parseTest = Int32.Parse(self.requirement.value);
                        if (parseTest >= 1000) // alt art
                        {
                            parseTest -= 1000;
                            altArt = true;
                        }

                        //custom karma extension
                        //if (parseTest > 10)
                        //{
                            //sLeaser.sprites[1].element = Futile.atlasManager.GetElementWithName("gateSymbol" + parseTest.ToString());
                        //}

                        if (parseTest > 5)
                        {
                            int cap = (self.room.game.session as StoryGameSession).saveState.deathPersistentSaveData.karmaCap + 1;
                            if (cap < parseTest) cap = Mathf.Max(7, parseTest);
                            cap = Math.Min(cap, 10);
                            sLeaser.sprites[1].element = Futile.atlasManager.GetElementWithName("gateSymbol" + parseTest.ToString() + "-" + cap.ToString() + (altArt ? "alt" : "")); // Custom, 1-indexed
                        }
                        else
                        {
                            sLeaser.sprites[1].element = Futile.atlasManager.GetElementWithName("gateSymbol" + parseTest.ToString() + (altArt ? "alt" : "")); // Alt art for vanilla gates
                        }
                        break;
                }

                if (self.requirement.value.ToLower() == "10reinforced")
                    sLeaser.sprites[1].element = Futile.atlasManager.GetElementWithName("gateSymbol10reinforced");

                self.symbolDirty = false;
            }
        }
        orig(self, sLeaser, rCam, timeStacker, camPos);
        //orig(self, sLeaser, rCam, timeStacker, camPos);
    }

    #endregion

    private static string[] GateNames = {
    };
    private static string[] NewGates1 = {
    };
    private static string[] NewGates2 =
    {
    };

    #region hooks

    private static void OverWorld_GateRequestsSwitchInitiation(On.OverWorld.orig_GateRequestsSwitchInitiation orig, OverWorld self, RegionGate reportBackToGate)
    {
        //modded
        RealNewGateName = "";

        AbstractRoom room = reportBackToGate.room.abstractRoom;

        OriginalGateName = room.name;

        self.game.rainWorld.HandleLog("RegionRandomizer: Room entered is a gate: " + room.name, "stuff", LogType.Log);
        string oldName = room.name;
        string newName;
        string realNewName;
        //int idx = GateNames.IndexOf(room.name);

        int idx = -1;
        self.game.rainWorld.HandleLog("RegionRandomizer: GateNames length: " + GateNames.Length, "stuff", LogType.Log);
        for (int i = 0; i < GateNames.Length; i++)
        {
            try
            {
                if (room.name.Trim().ToUpper() == GateNames[i].Trim().ToUpper())
                {
                    self.game.rainWorld.HandleLog("RegionRandomizer: idx = " + i, "stuff", LogType.Log);
                    idx = i;
                    break;
                }
            }
            catch (Exception ex)
            {
                self.game.rainWorld.HandleLog("RegionRandomizer: Unknown error with reading GateNames[i]. i = " + i, "stuff", LogType.Log);
            }
        }

        if (idx < 0)
        { //don't change region if name isn't found in randomizer list
            string s = "";
            foreach (string g in GateNames)
                s += g + ", ";
            self.game.rainWorld.HandleLog("RegionRandomizer: Didn't find the gate in: " + s, "stuff", LogType.Log);
            newName = room.name;
            realNewName = room.name;
            orig(self, reportBackToGate);
            return;
        }
        else
        {
            string s2 = "";
            foreach (string g in NewGates1)
            {
                s2 += g + ", ";
            }
            self.game.rainWorld.HandleLog("RegionRandomizer: NewGates1: " + s2, "stuff", LogType.Log);
            s2 = "";
            foreach (string g in NewGates2)
            {
                s2 += g + ", ";
            }
            self.game.rainWorld.HandleLog("RegionRandomizer: NewGates2: " + s2, "stuff", LogType.Log);

            if (room.name.Split('_')[1] == Region.GetVanillaEquivalentRegionAcronym(self.game.world.region.name))
            {
                newName = NewGates1[idx];
                self.game.rainWorld.HandleLog("RegionRandomizer: newName: " + newName, "stuff", LogType.Log);
                int idx2 = -1;
                for (int i = 0; i < NewGates2.Length; i++)
                {
                    try
                    {
                        if (newName.Trim().ToUpper() == NewGates2[i].Trim().ToUpper())
                        {
                            self.game.rainWorld.HandleLog("RegionRandomizer: idx2 = " + i, "stuff", LogType.Log);
                            idx2 = i;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        self.game.rainWorld.HandleLog("RegionRandomizer: Unknown error with reading GateNames[i]. i = " + i, "stuff", LogType.Log);
                    }
                }
                if (idx2 >= 0)
                    realNewName = GateNames[idx2];
                else
                {
                    //search NewGates1
                    for (int i = 0; i < NewGates1.Length; i++)
                    {
                        if (i == idx)
                            continue;
                        try
                        {
                            if (newName.Trim().ToUpper() == NewGates1[i].Trim().ToUpper())
                            {
                                self.game.rainWorld.HandleLog("RegionRandomizer: idx2 = " + i, "stuff", LogType.Log);
                                idx2 = i;
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            self.game.rainWorld.HandleLog("RegionRandomizer: Unknown error with reading GateNames[i]. i = " + i, "stuff", LogType.Log);
                        }
                    }
                    if (idx2 >= 0)
                        realNewName = GateNames[idx2];
                    else
                    {
                        self.game.rainWorld.HandleLog("RegionRandomizer: idx2 not found", "stuff", LogType.Log);
                        realNewName = room.name;
                        orig(self, reportBackToGate);
                        return;
                    }
                }
            }
            else
            {
                newName = NewGates2[idx];
                self.game.rainWorld.HandleLog("RegionRandomizer: newName: " + newName, "stuff", LogType.Log);
                int idx2 = -1;
                for (int i = 0; i < NewGates1.Length; i++)
                {
                    try
                    {
                        if (newName.Trim().ToUpper() == NewGates1[i].Trim().ToUpper())
                        {
                            self.game.rainWorld.HandleLog("RegionRandomizer: idx2 = " + i, "stuff", LogType.Log);
                            idx2 = i;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        self.game.rainWorld.HandleLog("RegionRandomizer: Unknown error with reading GateNames[i]. i = " + i, "stuff", LogType.Log);
                    }
                }
                if (idx2 >= 0)
                    realNewName = GateNames[idx2];
                else
                {
                    //search NewGates2
                    for (int i = 0; i < NewGates2.Length; i++)
                    {
                        if (i == idx)
                            continue;
                        try
                        {
                            if (newName.Trim().ToUpper() == NewGates2[i].Trim().ToUpper())
                            {
                                self.game.rainWorld.HandleLog("RegionRandomizer: idx2 = " + i, "stuff", LogType.Log);
                                idx2 = i;
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            self.game.rainWorld.HandleLog("RegionRandomizer: Unknown error with reading GateNames[i]. i = " + i, "stuff", LogType.Log);
                        }
                    }
                    if (idx2 >= 0)
                        realNewName = GateNames[idx2];
                    else
                    {
                        self.game.rainWorld.HandleLog("RegionRandomizer: idx2 not found", "stuff", LogType.Log);
                        realNewName = room.name;
                        orig(self, reportBackToGate);
                        return;
                    }
                }
            }

            self.game.rainWorld.HandleLog("RegionRandomizer: newName: " + newName + ", realNewName: " + realNewName, "stuff", LogType.Log);

            reportBackToGate.room.abstractRoom.name = realNewName;
            //GateBlockUnload = realNewName;

            RealNewGateName = realNewName;

        }

        //subtract karma from player
        try
        {
            if (Options.gateKarmaPenalty.Value > 0)
            {
                StoryGameSession session = (self.game.session as StoryGameSession);
                int initialKarma = session.saveState.deathPersistentSaveData.karma;
                session.saveState.deathPersistentSaveData.karma = Math.Max(0, initialKarma - Options.gateKarmaPenalty.Value);
                //update karma display, copied from SSOracleBehavior original code
                for (int num2 = 0; num2 < reportBackToGate.room.game.cameras.Length; num2++)
                {
                    if (reportBackToGate.room.game.cameras[num2].hud.karmaMeter != null)
                    {
                        reportBackToGate.room.game.cameras[num2].hud.karmaMeter.UpdateGraphic();
                    }
                }
                addKarmaNextDeath = initialKarma - session.saveState.deathPersistentSaveData.karma;
            }
        }
        catch (Exception ex)
        {
            self.game.rainWorld.HandleLog("RegionRandomizer: Karma Subtraction Error: " + ex.Message, ex.StackTrace, LogType.Error);
        }


        //original code

        self.reportBackToGate = reportBackToGate;
        AbstractRoom abstractRoom = reportBackToGate.room.abstractRoom;
        Custom.Log(new string[] { "Switch Worlds" });
        Custom.Log(new string[]
        {
                "Gate:",
                abstractRoom.name,
                abstractRoom.index.ToString()
        });
        string text = self.activeWorld.name;
        text = Region.GetVanillaEquivalentRegionAcronym(text);

        
        //modded
        string[] array = Regex.Split(newName, "_");


        //mods finish here
        string text2 = "ERROR!";
        if (array.Length == 3)
        {
            for (int i = 1; i < 3; i++)
            {
                if (Region.GetVanillaEquivalentRegionAcronym(array[i]) != text) //ADDED Region.GetVanillaEquivalentRegionAcronym !!!
                {
                    text2 = array[i];
                    break;
                }
            }
        }
        text2 = Region.GetProperRegionAcronym(self.game.IsStorySession ? self.game.StoryCharacter : null, text2);
        Custom.Log(new string[] { "Old world:", text });
        Custom.Log(new string[] { "New world:", text2 });
        if (text2 == "ERROR!")
        {
            return;
        }
        if (text2 == "GW")
        {
            self.game.session.creatureCommunities.scavengerShyness = 0f;
        }
        if (ModManager.MSC)
        {
            Region region = self.GetRegion(text);
            WinState.ListTracker listTracker = self.game.GetStorySession.saveState.deathPersistentSaveData.winState.GetTracker(MoreSlugcats.MoreSlugcatsEnums.EndgameID.Nomad, true) as WinState.ListTracker;
            if (!listTracker.GoalAlreadyFullfilled)
            {
                Custom.Log(new string[]
                {
                        "Journey list before gate:",
                        listTracker.myList.Count.ToString()
                });
                if (listTracker.myLastList.Count > listTracker.myList.Count)
                {
                    Custom.Log(new string[] { "Stale journey max progress cleared" });
                    listTracker.myLastList.Clear();
                }
                if (listTracker.myList.Count == 0 || listTracker.myList[listTracker.myList.Count - 1] != self.GetRegion(text2).regionNumber)
                {
                    Custom.Log(new string[]
                    {
                            "Journey progress updated with",
                            region.regionNumber.ToString()
                    });
                    listTracker.myList.Add(region.regionNumber);
                }
                else
                {
                    Custom.Log(new string[]
                    {
                            "Journey is backtracking",
                            listTracker.myList[listTracker.myList.Count - 1].ToString()
                    });
                }
                Custom.Log(new string[]
                {
                        "Journey list:",
                        listTracker.myList.Count.ToString()
                });
                Custom.Log(new string[]
                {
                        "Old Journey list:",
                        listTracker.myLastList.Count.ToString()
                });
            }
        }
        
        self.worldLoader = new WorldLoader(self.game, self.PlayerCharacterNumber, false, text2, self.GetRegion(text2), self.game.setupValues);
        self.worldLoader.NextActivity();

    }

    private static string OriginalGateName = "";
    private static string RealNewGateName = "";

    public static void OverWorld_WorldLoaded(On.OverWorld.orig_WorldLoaded orig, OverWorld self)
    {
        if (RealNewGateName == "")
        {
            OriginalGateName = "";
            orig(self);
            return;
        }

        GateBlockUnload = "";

        self.game.rainWorld.HandleLog("RegionRandomizer: World loaded in gate: " + RealNewGateName, "stuff", LogType.Log);

        //copy of report-back-to-gate
        RegionGate reportBackToGate = self.reportBackToGate;

        try
        {
            orig(self);
        }
        catch (Exception ex)
        {
            self.game.rainWorld.HandleLog("RegionRandomizer: World Loader Error (Gates: " + OriginalGateName + ", " + RealNewGateName + "): " + ex.Message, ex.StackTrace, LogType.Error);
        }

        AbstractRoom abRoom = self.activeWorld.GetAbstractRoom(RealNewGateName);
        Room realRoom = abRoom.realizedRoom;
        if (realRoom == null)
        {
            Instance.Logger.LogDebug("Couldn't find realized room!");
            return;
        }
        /*
        //try to patch rain mask thingy
        try
        {
            if (!Futile.atlasManager.DoesContainElementWithName("RainMask_" + RealNewGateName))
            {
                FAtlasElement element = Futile.whiteElement.Clone();
                element.name = "RainMask_" + RealNewGateName;
                Futile.atlasManager.AddElement(element);
            }
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError(ex);
        }
        */
        //find a player
        if (realRoom.PlayersInRoom.Count < 1)
        {
            Instance.Logger.LogDebug("Couldn't find player in room!");
            return;
        }
        Vector2 playerPos = realRoom.PlayersInRoom[0].mainBodyChunk.pos;
        Instance.Logger.LogDebug("Player pos: " + playerPos.x);

        //determine current connections
        bool hasAnyConnections = false;
        int highestDestNode = -1;

        List<int> needToConnect = new();
        List<bool> isShelter = new();
        List<int> canConnect = new();
        int shelterCount = 0; //for debugging purposes

        try
        {
            for (int i = 0; i < realRoom.shortcuts.Length; i++)
            {
                try
                {
                    if (realRoom.shortcuts[i].shortCutType == null || realRoom.shortcuts[i].shortCutType != ShortcutData.Type.RoomExit)
                        continue;
                } catch (Exception ex)
                {
                    Instance.Logger.LogError(ex);
                    continue;
                }

                //int idx = realRoom.shortcuts[i].connection.destinationCoord.room;
                ShortcutData sc = realRoom.shortcuts[i];
                int idx = (sc.destNode >= 0 && sc.destNode < abRoom.connections.Length) ? abRoom.connections[sc.destNode] : -1;

                if (idx >= 0 && idx != abRoom.index)
                    hasAnyConnections = true;
                if (sc.destNode > highestDestNode)
                    highestDestNode = sc.destNode;

                Instance.Logger.LogDebug("Shortcut Pos " + i + ": " + (sc.StartTile.x * 20f) + "; index: " + idx);
                if (sc.shortCutType == ShortcutData.Type.RoomExit
                    && idx >= 0
                    && idx != abRoom.index
                    && (sc.StartTile.x * 20f < playerPos.x) == reportBackToGate.letThroughDir)
                {
                    needToConnect.Add(i);
                    //AbstractRoom abrm = self.activeWorld.GetAbstractRoom(idx);
                    bool shelter = self.activeWorld.shelters.Contains(idx);//(abrm == null) ? true : abrm.shelter;
                    isShelter.Add(shelter);
                    if (shelter)
                        shelterCount++;
                    Instance.Logger.LogDebug("Added needToConnect " + i + ", node: " + sc.destNode);
                }
                else if (sc.shortCutType == ShortcutData.Type.RoomExit
                    && !(idx != -1
                        && idx != abRoom.index)
                    && (sc.StartTile.x * 20f > playerPos.x) == reportBackToGate.letThroughDir)
                {
                    canConnect.Add(i);
                    Instance.Logger.LogDebug("Added canConnect " + i + ", node: " + sc.destNode);
                }
            }
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError(ex);
        }

        Instance.Logger.LogDebug("NeedToConnect: " + needToConnect.Count + "; Shelters: " + shelterCount);
        Instance.Logger.LogDebug("ConnectibleLocations: " + canConnect.Count);

        //change abRoom.connected to be the right length to prevent errors
        int[] oldGateConnections = abRoom.connections;
        int[] newGateConnections = new int[Math.Max(oldGateConnections.Length, highestDestNode + 1)];
        for (int i = 0; i < oldGateConnections.Length; i++)
            newGateConnections[i] = oldGateConnections[i];
        for (int i = oldGateConnections.Length; i < newGateConnections.Length; i++)
            newGateConnections[i] = -1;
        abRoom.connections = newGateConnections;

        //^^^ the primary error was that a gate with 3 connections (e.g: SI_LF) would be replaced by a gate with 2 connections (e.g: SU_HI)
        //  and so the shortcut that led to node #2 would throw an error when entered, since only nodes 0 and 1 exist in SU_HI

        try
        {
            //connect all non-shelter rooms
            for (int i = needToConnect.Count - 1; i >= 0; i--)
            {
                if (canConnect.Count <= 0)
                    break;
                if (isShelter[i])
                    continue;
                int idx = needToConnect[i];
                int newIdx = canConnect.Pop();
                if (realRoom.shortcuts[idx].destNode < 0 || realRoom.shortcuts[idx].destNode >= abRoom.connections.Length
                    || realRoom.shortcuts[newIdx].destNode < 0 || realRoom.shortcuts[newIdx].destNode >= abRoom.connections.Length)
                { //this should never be used anymore... but it exists just in case
                    int oldNode = realRoom.shortcuts[idx].destNode;
                    realRoom.shortcuts[idx].destNode = realRoom.shortcuts[newIdx].destNode;
                    realRoom.shortcuts[newIdx].destNode = oldNode;
                    Instance.Logger.LogDebug("Had to use old backup method: oldNode = " + realRoom.shortcuts[idx].destNode + ", newNode = " + realRoom.shortcuts[newIdx].destNode);
                }
                //^^^ doesn't work because destNode is kept track of in other places
                //instead, swap abRoom.connections
                else
                {
                    abRoom.connections[realRoom.shortcuts[newIdx].destNode] = abRoom.connections[realRoom.shortcuts[idx].destNode];
                    abRoom.connections[realRoom.shortcuts[idx].destNode] = -1;
                }

                needToConnect.RemoveAt(i);
                isShelter.RemoveAt(i);
            }
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError(ex);
        }

        Instance.Logger.LogDebug("abRoom.connections: " + abRoom.connections.Length + ", realRoom.shortcuts: " + realRoom.shortcuts.Length);
        Instance.Logger.LogDebug("Still NeedToConnect: " + needToConnect.Count);

        try
        {
            //connect shelters as available
            for (int i = needToConnect.Count - 1; i >= 0; i--)
            {
                if (canConnect.Count <= 0)
                    break;
                int idx = needToConnect[i];
                int newIdx = canConnect.Pop();
                if (realRoom.shortcuts[idx].destNode < 0 || realRoom.shortcuts[idx].destNode >= abRoom.connections.Length
                    || realRoom.shortcuts[newIdx].destNode < 0 || realRoom.shortcuts[newIdx].destNode >= abRoom.connections.Length)
                { //this should never be used anymore... but it exists just in case
                    int oldNode = realRoom.shortcuts[idx].destNode;
                    realRoom.shortcuts[idx].destNode = realRoom.shortcuts[newIdx].destNode;
                    realRoom.shortcuts[newIdx].destNode = oldNode;
                    Instance.Logger.LogDebug("Had to use old backup method: oldNode = " + realRoom.shortcuts[idx].destNode + ", newNode = " + realRoom.shortcuts[newIdx].destNode);
                }
                //^^^ doesn't work because destNode is kept track of in other places
                //instead, swap abRoom.connections
                else
                {
                    abRoom.connections[realRoom.shortcuts[newIdx].destNode] = abRoom.connections[realRoom.shortcuts[idx].destNode];
                    abRoom.connections[realRoom.shortcuts[idx].destNode] = -1;
                }

                needToConnect.RemoveAt(i);
                isShelter.RemoveAt(i);
            }
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError(ex);
        }

        Instance.Logger.LogDebug("Not Connected: " + needToConnect.Count);


        //fix gates that Rain World fails to connect whatsoever (e.g: GATE_DS_CC once)
        try
        {
            if (!hasAnyConnections)
            {
                Instance.Logger.LogDebug("Gate " + RealNewGateName + " has no connections whatsover; attempting to find other rooms that connect.");

                //search through every other room, looking for one that connects to this one
                int roomIdx = -1;

                foreach (AbstractRoom ar in self.activeWorld.abstractRooms)
                {
                    if (ar.connections.Contains(abRoom.index))
                    {
                        roomIdx = ar.index;
                        break;
                    }
                }

                if (roomIdx < 0)
                    Instance.Logger.LogDebug("Failed to find any room that connects to the gate.");
                else
                {
                    Instance.Logger.LogDebug("Found room " + roomIdx);

                    //connect the room to one of canConnect
                    int node = -1;
                    foreach (int scIdx in canConnect)
                    {
                        int destNode = realRoom.shortcuts[scIdx].destNode;
                        if (destNode >= 0 && destNode < abRoom.connections.Length)
                        {
                            node = destNode;
                            break;
                        }
                    }

                    if (node < 0)
                        Instance.Logger.LogDebug("Failed to find any node to connect to within the gate.");
                    else
                    {
                        abRoom.connections[node] = roomIdx;
                        Instance.Logger.LogDebug("Successfully connected node " + node + " to room " + roomIdx);
                    }
                }
            }
        } catch (Exception ex)
        {
            Instance.Logger.LogError(ex);
        }

        //get player list
        List<AbstractCreature> abstractPlayers = new();
        foreach (Player p in realRoom.PlayersInRoom)
        {
            abstractPlayers.Add(p.abstractCreature);
        }

        GateAbstractizeLoop(self, abstractPlayers, RealNewGateName, RealNewGateName, oldGateConnections);

        RealNewGateName = "";
        OriginalGateName = "";
    }

    /*
    public static void OverWorld_WorldLoaded(On.OverWorld.orig_WorldLoaded orig, OverWorld self)
    {
        if (RealNewGateName == "")
        {
            OriginalGateName = "";
            orig(self);
            return;
        }

        GateBlockUnload = "";

        self.game.rainWorld.HandleLog("RegionRandomizer: World loaded in gate: " + RealNewGateName, "stuff", LogType.Log);

        //AbstractRoom roomToReplace = self.worldLoader.ReturnWorld().GetAbstractRoom(RealNewGateName);

        //self.game.rainWorld.HandleLog("RegionRandomizer: World Loader code success!", "stuff", LogType.Log);

        try
        {
            //foreach (string thing in Futile.atlasManager._allElementsByName.Keys)
            //{
            //    self.game.rainWorld.HandleLog("RegionRandomizer: Atlas list: " + thing, "stuff", LogType.Log);
            //} gateSymbolComsmark
            
            orig(self);
        } catch (Exception ex)
        {
            self.game.rainWorld.HandleLog("RegionRandomizer: World Loader Error (Gates: " + OriginalGateName + ", " + RealNewGateName + "): " + ex.Message, ex.StackTrace, LogType.Error);
            //return;
        }


        //teleport player to new room
        AbstractRoom abRoom = self.activeWorld.GetAbstractRoom(RealNewGateName);

        //gate asset stuff patch: try renaming gate to original name, then renaming it again upon abstraction
        //abRoom.name = OriginalGateName;

        Room realRoom = abRoom.realizedRoom;
        ShortcutData shortcut = new ShortcutData();
        bool found = false;
        try
        {
            //search for node with the needed connection
            int count = 0;
            int highestDestNode = 0;
            foreach (ShortcutData s in realRoom.shortcuts)
            {
                if (s.destNode < abRoom.connections.Length)
                {
                    highestDestNode = Math.Max(highestDestNode, s.destNode);
                    if (s.destNode >= 0)
                    {
                        if (abRoom.connections[s.destNode] > -1 && abRoom.connections[s.destNode] != abRoom.index && Array.IndexOf(self.activeWorld.shelters, abRoom.connections[s.destNode]) < 0)
                        {
                            self.game.rainWorld.HandleLog("RegionRandomizer: Exit node idx: " + count, "stuff", LogType.Log);
                            shortcut = s;
                            found = true;
                            break;
                        }
                    }
                }
                count++;
            }
            if (!found)
            {
                self.game.rainWorld.HandleLog("RegionRandomizer: Attempting to correct no node found... highestDestNode: " + highestDestNode, "stuff", LogType.Log);
                int connectionIdx = 0;
                for (int i = 0; i < abRoom.connections.Length; i++)
                {
                    if (abRoom.connections[i] > -1 && abRoom.connections[i] != abRoom.index && Array.IndexOf(self.activeWorld.shelters, abRoom.connections[i]) < 0)
                    {
                        connectionIdx = i;
                        break;
                    }
                }
                self.game.rainWorld.HandleLog("RegionRandomizer: Connection idx: " + connectionIdx, "stuff", LogType.Log);

                //patch the first connection being an inaccessable node
                abRoom.connections[highestDestNode] = abRoom.connections[connectionIdx];
                count = 0;
                foreach (ShortcutData s in realRoom.shortcuts)
                {
                    if (s.destNode < abRoom.connections.Length && s.destNode >= 0)
                    {
                        if (abRoom.connections[s.destNode] > -1 && abRoom.connections[s.destNode] != abRoom.index && Array.IndexOf(self.activeWorld.shelters, abRoom.connections[s.destNode]) < 0)
                        {
                            self.game.rainWorld.HandleLog("RegionRandomizer: New exit node idx: " + count, "stuff", LogType.Log);
                            shortcut = s;
                            found = true;
                            break;
                        }
                    }
                    count++;
                }
                self.game.rainWorld.HandleLog("RegionRandomizer: Attempted connection patch!", "stuff", LogType.Log);
            }
        }
        catch (Exception ex)
        {
            self.game.rainWorld.HandleLog("RegionRandomizer: Shortcut Error: " + ex.Message, ex.StackTrace, LogType.Error);
        }

        //old creature list
        List<AbstractCreature> oldCreatures = new();//abRoom.creatures;
        foreach (AbstractCreature c in abRoom.creatures)
            oldCreatures.Add(c);

        string origName = OriginalGateName;
        string newName = RealNewGateName;

        OriginalGateName = "";
        RealNewGateName = "";

        if (found)
        {
            try
            {
                //kill old gate room
                RemovePlayersFromRoomLoop(self, oldCreatures, origName, newName, shortcut);
            }
            catch (Exception ex)
            {
                self.game.rainWorld.HandleLog("RegionRandomizer: Error: " + ex.Message, ex.StackTrace, LogType.Error);
            }
        }
        else
            self.game.rainWorld.HandleLog("RegionRandomizer: No exit node found", "stuff", LogType.Log);
    }
    */
    //unused and would throw errors unless the oldGateConnections param is added
    public static async void RemovePlayersFromRoomLoop(OverWorld self, List<AbstractCreature> oldCreatures, string origName, string newName, ShortcutData shortcut)
    {
        await Task.Delay(4000);
        AbstractRoom ar = null;

        //gate asset stuff patch: try renaming gate to original name, then renaming it again upon abstraction
        try
        {
            ar = self.activeWorld.GetAbstractRoom(newName);
            ar.name = origName;
            self.game.rainWorld.HandleLog("RegionRandomizer: Renamed " + newName + " to " + origName, "stuff", LogType.Log);
        } catch (Exception ex)
        {
            self.game.rainWorld.HandleLog("RegionRandomizer: Gate renaming error: Failed to rename " + newName + " to " + origName + ". " + ex.Message, ex.StackTrace, LogType.Error);
            origName = newName;
        }

        await Task.Delay(1000);

        while (true)
        {
            try
            {
                ar = self.activeWorld.GetAbstractRoom(origName);
                if (ar.realizedRoom == null || ar.realizedRoom.regionGate.mode == RegionGate.Mode.MiddleOpen)
                    break;
                await Task.Delay(200);
            }
            catch (Exception ex)
            {
                self.game.rainWorld.HandleLog("RegionRandomizer: Gate Opening Error: " + ex.Message, ex.StackTrace, LogType.Error);
                await Task.Delay(200);
            }
        }
        
            self.game.rainWorld.HandleLog("RegionRandomizer: Exit node found!", "stuff", LogType.Log);
        foreach (AbstractCreature c in oldCreatures)
        {
            try
            {
                if (c.Room.name != ar.name)
                    self.game.rainWorld.HandleLog("RegionRandomizer: Creature in room " + c.Room.name + ", not in " + ar.name, "no stack trace", LogType.Error);
                else if (c.realizedCreature != null && ar.realizedRoom != null)
                    self.game.shortcuts.SuckInCreature(c.realizedCreature, ar.realizedRoom, shortcut);
                await Task.Delay(500);
            }
            catch (Exception ex) { self.game.rainWorld.HandleLog("RegionRandomizer: Creature Shortcut Error: " + ex.Message, ex.StackTrace, LogType.Error); }
        }
        self.game.rainWorld.HandleLog("RegionRandomizer: Moved all creatures to exit pipe", "stuff", LogType.Log);
        GateAbstractizeLoop(self, oldCreatures, origName, newName, new int[0]);
    }

    public static async void GateAbstractizeLoop(OverWorld self, List<AbstractCreature> oldCreatures, string origName, string newName, int[] oldGateConnections)
    {
        try
        {
            self.game.rainWorld.HandleLog("RegionRandomizer: Room abstraction loop begun", "stuff", LogType.Log);
            //Thread.Sleep(5000);
            while (true)
            {
                bool creatureInRoom = false;
                foreach (AbstractCreature p in oldCreatures.Concat(self.game.AlivePlayers))
                {
                    if (p.Room.name == origName)
                    {
                        creatureInRoom = true;
                        break;
                    }
                    else
                        self.game.rainWorld.HandleLog("RegionRandomizer: Creature " + p.ID + " in room " + p.Room.name + " out of " + oldCreatures.Count + " creatures", "stuff", LogType.Log);
                }
                AbstractRoom ar = null;
                if (!creatureInRoom)
                {
                    ar = self.activeWorld.GetAbstractRoom(origName);
                    if (ar.creatures.Count > 0)
                    {
                        //creatureInRoom = true;
                        self.game.rainWorld.HandleLog("RegionRandomizer: Room still contains creatures", "stuff", LogType.Log);
                    }
                }
                if (creatureInRoom)
                {
                    try { await Task.Delay(100); }
                    catch (Exception ex) { self.game.rainWorld.HandleLog("RegionRandomizer: Thread Sleep Error: " + ex.Message, ex.StackTrace, LogType.Error); }
                    continue;
                }
                try
                {
                    self.game.rainWorld.HandleLog("RegionRandomizer: No players in room: " + origName, "stuff", LogType.Log);
                    await Task.Delay(50); //add an additional wait just to make sure everything has finished loading

                    ar.name = newName; //try renaming first...?
                    ar.Abstractize();
                    ar.connections = oldGateConnections;
                    //ar.name = newName; //rename gate back to its proper new name
                    self.game.rainWorld.HandleLog("RegionRandomizer: Abstractized room: " + origName, "stuff", LogType.Log);

                    //GateBlockUnload = newName;
                    break;
                }
                catch (Exception ex)
                {
                    self.game.rainWorld.HandleLog("RegionRandomizer: Thread Sleep Error: " + ex.Message, ex.StackTrace, LogType.Error);
                }
            }
            self.game.rainWorld.HandleLog("RegionRandomizer: Stopped gate room abstractizing loop", "stuff", LogType.Log);
            
            /*
            await Task.Delay(200);
            try { Futile.atlasManager._allElementsByName.Remove("RainMask_" + origName); }
            catch (Exception ex)
            {
                self.game.rainWorld.HandleLog("RegionRandomizer: Rain Mask Removal Error: " + ex.Message, ex.StackTrace, LogType.Error);
            }
            */
        }
        catch (Exception ex)
        {
            self.game.rainWorld.HandleLog("RegionRandomizer: Room killer error: " + ex.Message, ex.StackTrace, LogType.Error);
        }
    }

    private static int addKarmaNextDeath = 0;
    public static void SaveState_SessionEnded(On.SaveState.orig_SessionEnded orig, SaveState self, RainWorldGame game, bool survived, bool newMalnourished)
    {
        if (addKarmaNextDeath > 0 && !survived)
        {
            //add karma for player
            try
            {
                if (Options.gateKarmaPenalty.Value > 0)
                {
                    self.deathPersistentSaveData.karma = Math.Min(self.deathPersistentSaveData.karma + addKarmaNextDeath, self.deathPersistentSaveData.karmaCap);
                    game.rainWorld.HandleLog("RegionRandomizer: Set player karma to " + self.deathPersistentSaveData.karma, "no stack", LogType.Log);
                }
            }
            catch (Exception ex)
            {
                game.rainWorld.HandleLog("RegionRandomizer: Karma Addition Error: " + ex.Message, ex.StackTrace, LogType.Error);
            }
        }
        addKarmaNextDeath = 0;

        orig(self, game, survived, newMalnourished);
    }
    public static void RainWorldGame_ExitGame(On.RainWorldGame.orig_ExitGame orig, RainWorldGame self, bool asDeath, bool asQuit)
    {
        lastLoadTime = DateTime.MinValue.Ticks; //reset load time after quitting

        if (addKarmaNextDeath > 0)
        {
            //add karma for player
            try
            {
                if (Options.gateKarmaPenalty.Value > 0)
                {
                    StoryGameSession session = (self.session as StoryGameSession);
                    session.saveState.deathPersistentSaveData.karma = Math.Min(session.saveState.deathPersistentSaveData.karma + addKarmaNextDeath, session.saveState.deathPersistentSaveData.karmaCap);
                    self.rainWorld.HandleLog("RegionRandomizer: Set player karma to " + session.saveState.deathPersistentSaveData.karma, "no stack", LogType.Log);
                }
            }
            catch (Exception ex)
            {
                self.rainWorld.HandleLog("RegionRandomizer: Karma Addition Error: " + ex.Message, ex.StackTrace, LogType.Error);
            }
        }
        addKarmaNextDeath = 0;

        orig(self, asDeath, asQuit);
    }

    private static string GateBlockUnload = "";
    public static void AbstractRoom_Abstractize(On.AbstractRoom.orig_Abstractize orig, AbstractRoom self)
    {
        if (self.name == GateBlockUnload)
        {
            self.world.game.rainWorld.HandleLog("Blocked gate from abstractizing: " + GateBlockUnload, "stuff", LogType.Log);
            GateBlockUnload = "";
            return;
        }

        orig(self);
    }


    public static SaveState PlayerProgression_GetOrInitiateSaveState(On.PlayerProgression.orig_GetOrInitiateSaveState orig, PlayerProgression self, SlugcatStats.Name saveStateNumber, RainWorldGame game, ProcessManager.MenuSetup setup, bool saveAsDeathOrQuit)
    {
        SaveState originalState = orig(self, saveStateNumber, game, setup, saveAsDeathOrQuit);

        if (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - 5000 < lastLoadTime) //if last load occurred within last 5 seconds
        {
            //lastLoadTime = false;
            return originalState;
        }

        //restarting game
        if (self.saveFileDataInMemory == null || self.loadInProgress || !self.saveFileDataInMemory.Contains("save") || !setup.LoadInitCondition)
        {
            //generate randomizer files
            Options.Slugcat = saveStateNumber.value;
            Options.UpdateForSlugcat(saveStateNumber.value, false);
            Instance.RandomizeAllRegions();
        }
        
        InitiateGame(saveStateNumber.value, false);

        lastLoadTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

        //return orig(self, saveStateNumber, game, setup, saveAsDeathOrQuit);
        return originalState;
    }
    public static void SlugcatSelectMenu_StartGame(On.Menu.SlugcatSelectMenu.orig_StartGame orig, Menu.SlugcatSelectMenu self, SlugcatStats.Name storyGameCharacter)
    {
        Custom.LogImportant("RegionRandomizer: Save State Slugcat: " + storyGameCharacter.value);

        //restarting game
        if (self.restartChecked || !self.manager.rainWorld.progression.IsThereASavedGame(storyGameCharacter))
        {
            //generate randomizer files
            Options.Slugcat = storyGameCharacter.value;
            Options.UpdateForSlugcat(storyGameCharacter.value, false);
            Instance.RandomizeAllRegions();
        }

        InitiateGame(storyGameCharacter.value, false);

        orig(self, storyGameCharacter);
    }

    private static long lastLoadTime = DateTime.MinValue.Ticks;
    public static string ExpeditionGame_ExpeditionRandomStarts(On.Expedition.ExpeditionGame.orig_ExpeditionRandomStarts orig, RainWorld rainWorld, SlugcatStats.Name slug)
    {
        string origString = orig(rainWorld, slug);
        Options.startingRegion.Value = origString.Split('_')[0].ToUpper();

        //generate randomizer files
        Options.Slugcat = slug.value;
        Options.UpdateForSlugcat(slug.value, true);
        Instance.RandomizeAllRegions();

        InitiateGame(slug.value, true);

        lastLoadTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

        return origString;
    }

    private static void InitiateGame(string slugcat, bool expedition = false)
    {
        if (IsOnline && !IsHost)
        {
            // host will tell us
            Debug.Log($"CustomGateLocks: [ {string.Join(", ", CustomGateLocks.Select((key, value) => $"\"{key}\": \"{value}\""))} ]");
            Debug.Log($"GateNames: [ {string.Join(", ", GateNames)} ]");
            Debug.Log($"NewGates1: [ {string.Join(", ", NewGates1)} ]");
            Debug.Log($"NewGates2: [ {string.Join(", ", NewGates2)} ]");
            return;
        }

        addKarmaNextDeath = 0;

        //apply randomizer files
        string randomizerFile = AssetManager.ResolveFilePath(string.Concat(new String[] {
            "RegionRandomizer",
            Path.DirectorySeparatorChar.ToString(),
            "RegionRandomizer-",
            slugcat,
            ".txt"
        }));
        string locksFile = AssetManager.ResolveFilePath(string.Concat(new String[] {
            "RegionRandomizer",
            Path.DirectorySeparatorChar.ToString(),
            "locks-",
            slugcat,
            ".txt"
        }));

        //regenerate files if necessary
        if (!File.Exists(randomizerFile) || !File.Exists(locksFile))
        {
            //generate randomizer files
            Options.Slugcat = slugcat;
            Options.UpdateForSlugcat(slugcat, expedition);
            Instance.RandomizeAllRegions();
            //apply randomizer files
            /*
            randomizerFile = AssetManager.ResolveFilePath(string.Concat(new String[] {
                "RegionRandomizer",
                Path.DirectorySeparatorChar.ToString(),
                "RegionRandomizer-",
                slugcat,
                ".txt"
            }));
            locksFile = AssetManager.ResolveFilePath(string.Concat(new String[] {
                "RegionRandomizer",
                Path.DirectorySeparatorChar.ToString(),
                "locks-",
                slugcat,
                ".txt"
            }));
            */
        }

        ReadRandomizerFiles(slugcat);
        ReadLocksFiles(slugcat);

        //MergeLocksFile(locksFile);

        if (IsOnline && IsHost)
        {
            AddOnlineData();
        }
    }
    private static void MergeLocksFile(string locksFile) {
        //merge locks file
        try
        {
            string[] newLocks = File.ReadAllLines(locksFile);

            string oldLocksFile = AssetManager.ResolveFilePath(string.Concat(new String[] {
                "World",
                Path.DirectorySeparatorChar.ToString(),
                "Gates",
                Path.DirectorySeparatorChar.ToString(),
                "locks.txt"
            }));
            string[] oldLocks = File.ReadAllLines(oldLocksFile);

            string fileData = "";

            List<string> locksAdded = new();

            //add/replace all lines in oldlocks
            for (int i = 0; i < oldLocks.Length; i++)
            {
                if (oldLocks[i].Length < 10)
                    continue;
                string g = Regex.Split(oldLocks[i], ":")[0];//.Substring(0, 10);
                string n = "";
                foreach (string l in newLocks)
                {
                    if (l.StartsWith(g))
                    {
                        n = l;
                        locksAdded.Add(l);
                        break;
                    }
                }

                fileData += ((n == "") ? oldLocks[i] : n) + "\n";
            }

            //add all newlocks not present in oldlocks (e.g: some gates aren't in locks file if both sides are 1)
            foreach (string l in newLocks)
            {
                if (l.StartsWith("GATE_") && !locksAdded.Contains(l))
                    fileData += l + "\n";
            }

            File.WriteAllText(AssetManager.ResolveFilePath(string.Concat(new String[] {
                //AssetManager.ResolveDirectory("mergedmods"),
                "mergedmods",
                Path.DirectorySeparatorChar.ToString(),
                "World",
                Path.DirectorySeparatorChar.ToString(),
                "Gates",
                Path.DirectorySeparatorChar.ToString(),
                "locks.txt"
            })), fileData);
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError(ex);
        }
    }


    private void RainWorldGameOnShutDownProcess(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
    {
        orig(self);
        ClearMemory();
    }
    private void GameSessionOnctor(On.GameSession.orig_ctor orig, GameSession self, RainWorldGame game)
    {   
        orig(self, game);
        ClearMemory();
    }
    private void ClearMemory()
    {
        //If you have any collections (lists, dictionaries, etc.)
        //Clear them here to prevent a memory leak
        //YourList.Clear();
    }
    #endregion

    #region FileReaders
    public static List<string> GetRegions()
    {
        string[] arr = File.ReadAllLines(AssetManager.ResolveFilePath(string.Concat(new string[]
        {
            "World",
            Path.DirectorySeparatorChar.ToString(),
            "regions.txt"
        })));
        return new List<string>(arr);
    }

    private List<List<string>> GetGateData(List<string> regions)
    {
        List<List<string>> gates = new ();

        foreach (string region in regions)
        {
            List<string> regionGates = new ();
            //string region = regions[i];

            string filePath = AssetManager.ResolveFilePath(string.Concat(new string[]
            {
                "World",
                Path.DirectorySeparatorChar.ToString(),
                region,
                Path.DirectorySeparatorChar.ToString(),
                "world_",
                region,
                ".txt"
            }));

            try
            {
                string[] fileLines = File.ReadAllLines(filePath);
                bool roomsStart = false;
                bool roomsDone = false;
                bool conditionalsStart = false;
                bool conditionalsDone = false;
                List<string> blacklist = new();
                Dictionary<string, string> replaceList = new Dictionary<string, string>();
                foreach (string l in fileLines)
                {
                    string line = l;
                    if (line.Length < 5) //if it can't contain any gate, it's of no interest
                    {
                        continue;
                    }
                    if (roomsStart)
                    {
                        if (line[0] == '{')
                        {
                            if (line[1] == '!')
                            {
                                continue;
                            }
                            int nameStart = 1;
                            for (; line[nameStart] != '}' && nameStart < line.Length - 1; nameStart++) { }
                            if (nameStart >= line.Length - 1)
                            {
                                Logger.LogDebug("Broken syntax for " + line);
                                continue;
                            }
                            line = line.Substring(nameStart + 1);
                        }
                        if (line.Substring(0, 5) == "GATE_")
                        {
                            //cout << "Found GATE_" << endl;
                            //regionGates.Add(line.Substring(0, 10));
                            regionGates.Add(line);
                        }
                        else if (line.Substring(0, 3) == "END")
                        {
                            roomsDone = true;
                            roomsStart = false;
                            if (conditionalsDone)
                                break;
                        }
                    }
                    else if (conditionalsStart)
                    {
                        if (line.Substring(0, 3) == "END")
                        {
                            conditionalsDone = true;
                            conditionalsStart = false;
                            if (roomsDone)
                                break;
                            continue;
                        }
                        string[] d = line.Split(':');
                        if (d.Length < 3)
                            continue;
                        d[0] = d[0].Trim();
                        d[1] = d[1].Trim();
                        bool nameFound = false;
                        foreach (string slug in Regex.Split(d[0], ",")) {
                            if (slug == Options.World_State)
                            {
                                nameFound = true;
                                if (d[1] == "HIDEROOM")
                                    blacklist.Add(d[2].Trim());
                                else if (d[1] == "REPLACEROOM")
                                    replaceList.Add(d[2].Trim(), d[3].Trim());
                            }
                        }
                        if (!nameFound && d[1] == "EXCLUSIVEROOM")
                            blacklist.Add(d[2].Trim());
                    }
                    else if (line.Substring(0, 5) == "ROOMS")
                        roomsStart = true;
                    else if (line.StartsWith("CONDITIONAL"))
                        conditionalsStart = true;
                    //std::getline(inFS, line);
                }

                //rename replaced gates
                for (int k = regionGates.Count - 1; k >= 0; k--)
                {
                    string[] splitgate = Regex.Split(regionGates.ElementAt(k), " : ");
                    if (replaceList.ContainsKey(splitgate[0]))
                    {
                        //regionGates[k] = replaceList[shortgate];
                        //regionGates[k] = Regex.Replace(regionGates[k], shortgate, replaceList[shortgate]);
                        //regionGates[k] = replaceList[splitgate[0]] + " : " + splitgate[1];
                        //Logger.LogDebug("Replaced " + splitgate[0] + " with " + regionGates[k]);
                        //no longer replacing names... no need to do so
                    }
                }

                //remove blacklisted gates
                for (int k = regionGates.Count - 1; k >= 0; k--)
                {
                    if (blacklist.Contains(Regex.Split(regionGates.ElementAt(k), ":")[0].Trim()))
                    {
                        Logger.LogDebug("Blacklisted " + regionGates.ElementAt(k));
                        regionGates.RemoveAt(k);
                    }
                }

                //inFS.close();
            } catch (Exception ex)
            {
                Logger.LogError(ex);
                throw;
            }

            gates.Add(regionGates);
        }
        return gates;
    }

    public static List<string> GetGateLocks()
    {
        string[] arr = File.ReadAllLines(AssetManager.ResolveFilePath(string.Concat(new string[]
        {
            "World",
            Path.DirectorySeparatorChar.ToString(),
            "Gates",
            Path.DirectorySeparatorChar.ToString(),
            "locks.txt"
        })));
        return new List<string>(arr);
    }

    public static List<string> GetEchoRegions(List<string> regions, string slugcat)
    {
        List<string> echoRegions = new(RegionRandomizerOptions.ECHO_REGIONS);

        foreach (string r in regions)
        {
            if (echoRegions.Contains(r))
                continue;
            //string echoPath = path + "\\world\\";
            //echoPath += r + "\\echoSettings.txt";
            //if (filesystem::exists(echoPath))
            if (File.Exists(AssetManager.ResolveFilePath(string.Concat(new string[]
            {
                "World",
                Path.DirectorySeparatorChar.ToString(),
                r,
                Path.DirectorySeparatorChar.ToString(),
                "echoSettings.txt"
            }))))
                echoRegions.Add(r);
        }

        if (slugcat == "Saint")
        {
            echoRegions.Add("MS");
            echoRegions.Add("SL");
            echoRegions.Add("UG");
        }

        return echoRegions;
    }

    public static bool ReadRandomizerFiles(string slugcat)
    {
        try
        {
            string filePath = AssetManager.ResolveFilePath(string.Concat(new String[] {
                "RegionRandomizer",
                Path.DirectorySeparatorChar.ToString(),
                "RegionRandomizer-",
                slugcat,
                ".txt"
            }));
            if (File.Exists(filePath))
            {
                //Debug.log("hi");
                string[] fileLines = File.ReadAllLines(filePath);

                GateNames = new string[fileLines.Length];
                NewGates1 = new string[fileLines.Length];
                NewGates2 = new string[fileLines.Length];
                for (int i = 0; i < fileLines.Length; i++)
                {
                    string[] data = Regex.Split(fileLines[i], ";");
                    GateNames[i] = data[0];
                    NewGates1[i] = data[1];
                    NewGates2[i] = data[2];
                }
                //Logger.LogDebug("Successfully loaded randomizer file: " + GateNames.Length);
                return true;
            }
        } catch (Exception ex) {
            //Logger.LogError(ex);
        }
        //Logger.LogDebug("Failed to load randomizer file: " + GateNames.Length);
        return false;
    }

    public static bool ReadLocksFiles(string slugcat)
    {
        try
        {
            string filePath = AssetManager.ResolveFilePath(string.Concat(new String[] {
                "RegionRandomizer",
                Path.DirectorySeparatorChar.ToString(),
                "locks-",
                slugcat,
                ".txt"
            }));
            if (File.Exists(filePath))
            {
                //Debug.log("hi");
                string[] fileLines = File.ReadAllLines(filePath);

                CustomGateLocks.Clear();
                foreach (string line in fileLines)
                {
                    string[] l = Regex.Split(line, " : ");
                    if (l.Length >= 3)
                        CustomGateLocks.Add(l[0], l[1] + ":" + l[2]);
                }
                //Logger.LogDebug("Successfully loaded randomizer file: " + GateNames.Length);
                string debugMsg = "CustomGateLocks: ";
                foreach (var thing in CustomGateLocks)
                    debugMsg += thing.Key + "=" + thing.Value + ", ";
                LogSomething(debugMsg);

                return true;
            }
        }
        catch (Exception ex)
        {
            //Logger.LogError(ex);
        }
        //Logger.LogDebug("Failed to load randomizer file: " + GateNames.Length);
        return false;
    }
    #endregion

    #region RandomizerLogic
    private List<Connectible> Connectibles = new();

    private List<string> RandomizeRegions(List<string> gateNames, List<string> regions, List<List<string>> gates, System.Random randCopy = null)
    {
        System.Random rand = (randCopy == null) ? new System.Random() : randCopy;
        List<string> newGates = new();

        if (Options.sensibleRegionPlacements.Value) //new randomizer logic
        {
            Connectibles.Clear();
            for (int i = 0; i < regions.Count; i++)
            {
                if (ReplacedRegions.Contains(regions[i]))
                    continue;
                int replIdx = Array.IndexOf(ReplacementRegions, regions[i]);
                List<string> regionGates = new();
                foreach (string gate in gates[i])
                {
                    string g = Regex.Split(gate, ":")[0].Trim();
                    string[] s = g.Split('_');
                    g = String.Join("_", s, 0, 3);
                    if (gateNames.Contains(g) && !regionGates.Contains(g) && (s[1] == regions[i] || s[2] == regions[i] || (replIdx >= 0 && (s[1] == ReplacedRegions[replIdx] || s[2] == ReplacedRegions[replIdx]))))
                    {
                        regionGates.Add(g);
                    }
                }
                if (regionGates.Count < 1)
                {
                    Logger.LogDebug("Skipped connectible: " + regions[i]);
                    continue;
                }
                Dictionary<string, Vector2> roomMapPositions = GetRoomMapPositions(regions[i], regionGates, Options.Slugcat);
                Connectibles.Add(new Connectible(regions[i], roomMapPositions, (BlacklistedRegions.Contains(regions[i])) ? "blacklist" : "none"));

                //debug output
                if (i == 0)
                {
                    string debugText = "";
                    foreach (var room in roomMapPositions)
                        debugText += room.Key + ": " + room.Value.ToString() + "; ";
                    Logger.LogDebug(debugText);
                }
            }
            Connectibles.Shuffle(); //why not? Adds a tiny bit of extra randomness

            //randomize...
            Connectibles = RandomlyConnectConnectibles(Connectibles, Options.sensiblePlacementRandomness.Value).ToArray().ToList();

            //debug output
            string debugText2 = "";
            foreach (Connectible conn in Connectibles)
                debugText2 += conn.name + ": " + conn.position.ToString() + ", " + conn.radius + "; ";
            for (int i = 0; i < Connectibles.Count; i++)
            {
                debugText2 += " . . " + Connectibles[i].name + ": ";
                foreach (var conn in Connectibles[i].connections)
                    debugText2 += conn.Key + ":" + conn.Value + ", ";
            }
            Logger.LogDebug(debugText2);

            //TODO: figure out how blacklisted regions might work; currently they're just randomized too
            //now convert this to newGates
            string[] tempGates = new string[gateNames.Count];
            foreach (Connectible connectible in Connectibles)
            {
                foreach (var conn in connectible.connections)
                {
                    int idx = gateNames.IndexOf(conn.Key);
                    if (idx >= 0)
                    {
                        string reg1 = conn.Key.Split('_')[1];//connectible.name;//
                        int replacementIdx = Array.IndexOf(ReplacementRegions, connectible.name);
                        bool isValidNewGate = reg1 == connectible.name || (replacementIdx >= 0 && reg1 == ReplacedRegions[replacementIdx]);
                        if (!isValidNewGate)
                            continue;

                        reg1 = connectible.name;

                        string newName = "GATE_" + reg1 + "_" + conn.Value;
                        //check if need to swap positions
                        string swapName = "GATE_" + conn.Value + "_" + reg1;

                        if (tempGates.Contains(swapName))
                        {
                            newName = swapName;
                            /*
                            string oldNewName = newName;
                            newName += "_1";
                            for (int j = 2; tempGates.Contains(newName); j++)
                                newName = oldNewName + "_" + j; //GATE_XX_YY_ + j
                            */
                            /*
                            Logger.LogDebug("Duplicate gate " + newName);
                            return RandomizeRegions(gateNames, regions, gates, rand);
                            */
                        }
                        tempGates[idx] = newName;
                    }
                }
            }

            //emergency cleanup:
            for (int i = 0; i < gateNames.Count; i++)
            {
                if (tempGates[i] == null || tempGates[i] == "")
                {
                    Logger.LogDebug("Failed to find newGate for: " + gateNames[i]);
                    tempGates[i] = gateNames[i];
                }
            }

            newGates = tempGates.ToList();
        }

        else //old randomizer logic
        {
            
            //cout << "Randomizing..." << endl;
            List<string> weightedRegions = new();
            foreach (string g in gateNames)
                weightedRegions.Add(g.Split('_')[2]);

            //separate weighted blacklist regions
            List<string> blacklist = new();
            for (int i = weightedRegions.Count - 1; i >= 0; i--)
            {
                if (BlacklistedRegions.Contains(weightedRegions[i]))
                {
                    blacklist.Add(weightedRegions[i]);
                    weightedRegions.RemoveAt(i);
                }
            }

            
            for (int i = 0; i < gateNames.Count; i++)
            { //randomize second half of gate name
                string reg1 = gateNames.ElementAt(i).Split('_')[1];
                string newName = "GATE_" + gateNames.ElementAt(i).Split('_')[1] + "_";
                bool isBlk = BlacklistedRegions.Contains(reg1);
                //int count = 0;
                //while (true)
                //{
                //if (count > 10)
                //{
                //    Logger.LogDebug("Already added gate OR default gate; ");
                //    return RandomizeRegions(gateNames, regions, rand);
                //}

                //if there are no more regions to use, use the blacklist
                if (weightedRegions.Count == 0)
                    weightedRegions = blacklist;
                if (blacklist.Count == 0)
                    blacklist = weightedRegions;

                int idx = rand.Next() % (isBlk ? blacklist : weightedRegions).Count;
                newName += (isBlk ? blacklist : weightedRegions).ElementAt(idx);

                if ((isBlk ? blacklist : weightedRegions).ElementAt(idx) == reg1)
                {
                    bool unresolvable = true;
                    foreach (string r in (isBlk ? blacklist : weightedRegions))
                    {
                        if (r != reg1)
                        {
                            unresolvable = false;
                            break;
                        }
                    }
                    if (unresolvable)
                    {
                        //try to find a working connection through the other list
                        unresolvable = true;
                        foreach (string r in ((!isBlk) ? blacklist : weightedRegions))
                        {
                            if (r != reg1)
                            {
                                unresolvable = false;
                                newName = "GATE_" + gateNames.ElementAt(i).Split('_')[1] + "_" + r;
                                ((!isBlk) ? blacklist : weightedRegions).Remove(r);
                                Logger.LogDebug("Resolved self-connecting gate through blacklist: " + newName);
                                break;
                            }
                        }
                        if (unresolvable)
                        {
                            Logger.LogDebug("Unresolvable self-connecting gate");
                            return RandomizeRegions(gateNames, regions, gates, rand);
                        }
                    }
                    else
                    {
                        while ((isBlk ? blacklist : weightedRegions).ElementAt(idx) == reg1)
                            idx = rand.Next() % (isBlk ? blacklist : weightedRegions).Count;
                        newName = "GATE_" + gateNames.ElementAt(i).Split('_')[1] + "_" + (isBlk ? blacklist : weightedRegions).ElementAt(idx);
                    }
                }


                //if (newGates.IndexOf(newName) < 0 && (!Options.forbidDefaultGates.Value || gateNames.IndexOf(newName) < 0))
                if (newGates.Contains(newName))
                {
                    string oldNewName = newName;
                    newName += "_1";
                    for (int j = 2; newGates.Contains(newName); j++)
                        newName = oldNewName + "_" + j; //GATE_XX_YY_ + j
                    Logger.LogDebug("Duplicate gate " + newName);
                }
                //{
                newGates.Add(newName);
                //weightedRegions.erase(next(weightedRegions.begin(), idx));
                (isBlk ? blacklist : weightedRegions).RemoveAt(idx);
                //break;
                //}
                //newName = gateNames.ElementAt(i).Substring(0, 8);
                //count++;
                //}
            }
        }


        //check that every region is accessible
        bool markFound = MarkAtStart;
        List<string> reachedRegions = new ();
        List<string> reachedRegionsBuffer = new ();
        List<int> reachSteps = new ();
        reachedRegions.Add(Options.startingRegion.Value);
        reachSteps.Add(0);
        for (int i = 0; i < regions.Count && reachedRegions.Count < regions.Count; i++)
        {
            reachedRegionsBuffer.Clear();
            for (int j = 0; j < newGates.Count; j++)
            {
                string oldGate = gateNames.ElementAt(j);
                string s0 = newGates.ElementAt(j);
                string s1 = s0.Split('_')[1];
                string s2 = s0.Split('_')[2];
                if (reachedRegions.IndexOf(s1) >= 0)
                {
                    if (reachedRegions.IndexOf(s2) < 0 && !BlacklistedRegions.Contains(s2))
                    {
                        if (!(InaccessableGates.ContainsKey(s1) && InaccessableGates[s1].Contains(oldGate)) && !(InaccessableRegions.ContainsKey(oldGate) && InaccessableRegions[oldGate].Contains(s2))) {
                            if (markFound || !COMSMARKRegions.Contains(s2))
                            {
                                reachedRegionsBuffer.Add(s2);
                                if (IteratorRegions.Contains(s2))
                                    markFound = true;
                            }
                            reachSteps.Add(i + 1);
                        }
                    }
                }
                else if (reachedRegions.IndexOf(s2) >= 0)
                {
                    if (reachedRegions.IndexOf(s1) < 0 && !BlacklistedRegions.Contains(s1))
                    {
                        if (!(InaccessableGates.ContainsKey(s2) && InaccessableGates[s2].Contains(oldGate)) && !(InaccessableRegions.ContainsKey(oldGate) && InaccessableRegions[oldGate].Contains(s1)))
                        {
                            if (markFound || !COMSMARKRegions.Contains(s1))
                            {
                                reachedRegionsBuffer.Add(s1);
                                if (IteratorRegions.Contains(s1))
                                    markFound = true;
                            }
                            reachSteps.Add(i + 1);
                        }
                    }
                }
            }
            for (int j = 0; j < ReplacedRegions.Length; j++)
            {
                if (reachedRegionsBuffer.Contains(ReplacedRegions[j]) && !reachedRegionsBuffer.Contains(ReplacementRegions[j]))
                {
                    reachedRegionsBuffer.Add(ReplacementRegions[j]);
                    int idx = reachedRegionsBuffer.IndexOf(ReplacedRegions[j]);
                    reachSteps.Add(reachSteps.ElementAt(idx));
                }
                else if (!reachedRegionsBuffer.Contains(ReplacedRegions[j]) && reachedRegionsBuffer.Contains(ReplacementRegions[j]))
                {
                    reachedRegionsBuffer.Add(ReplacedRegions[j]);
                    int idx = reachedRegionsBuffer.IndexOf(ReplacementRegions[j]);
                    reachSteps.Add(reachSteps.ElementAt(idx));
                }
            }
            foreach (string r in reachedRegionsBuffer)
                reachedRegions.Add(r);
        }

        //check if min steps are adhered to
        /*
        string[] minSteps = Regex.Split(Options.minSteps.Value, ",");
        for (int i = 0; i < minSteps.Length; i++)
        {
            string[] d = Regex.Split(minSteps[i], ":");
            string r = d[0];
            int idx = reachedRegions.IndexOf(r);
            if (idx < 0)
                continue;
            if (reachSteps.ElementAt(idx) > 0 && reachSteps.ElementAt(idx) < Int32.Parse(d[1]))
            {
                Logger.LogDebug("Region " + r + " reached in only " + reachSteps.ElementAt(idx) + " steps");
                return RandomizeRegions(gateNames, regions, gates, rand);
            }
        }
        */

        /*foreach (string r in Options.keyRegions.Value.Split(','))
        {
            if (reachedRegions.IndexOf(r) < 0)
            {
                Logger.LogDebug("Inaccessable key region: " + r);
                return RandomizeRegions(gateNames, regions, rand);
            }
        }*/

        //markFound = Options.Slugcat == "Rivulet" || Options.Slugcat == "Saint" || Options.Slugcat == "Inv";
        List<string> vanillaReachedRegions = new ();
        vanillaReachedRegions.Add(Options.startingRegion.Value);
        for (int i = 0; i < regions.Count && vanillaReachedRegions.Count < regions.Count; i++)
        {
            for (int j = 0; j < gateNames.Count; j++)
            {
                string s0 = gateNames.ElementAt(j);
                string s1 = s0.Split('_')[1];
                string s2 = s0.Split('_')[2];
                if (vanillaReachedRegions.IndexOf(s1) >= 0)
                {
                    if (vanillaReachedRegions.IndexOf(s2) < 0 && !BlacklistedRegions.Contains(s2))
                    {
                        if (!(InaccessableGates.ContainsKey(s1) && InaccessableGates[s1].Contains(s0)) && !(InaccessableRegions.ContainsKey(s0) && InaccessableRegions[s0].Contains(s2)))
                        {
                            vanillaReachedRegions.Add(s2);
                            if (IteratorRegions.Contains(s2))
                                markFound = true;
                        }
                    }
                }
                else if (vanillaReachedRegions.IndexOf(s2) >= 0)
                {
                    if (vanillaReachedRegions.IndexOf(s1) < 0 && !BlacklistedRegions.Contains(s1))
                    {
                        if (!(InaccessableGates.ContainsKey(s2) && InaccessableGates[s2].Contains(s0)) && !(InaccessableRegions.ContainsKey(s0) && InaccessableRegions[s0].Contains(s1)))
                        {
                            vanillaReachedRegions.Add(s1);
                            if (IteratorRegions.Contains(s1))
                                markFound = true;
                        }
                    }
                }
            }
            for (int j = 0; j < ReplacedRegions.Length; j++)
            {
                if (vanillaReachedRegions.Contains(ReplacedRegions[j]) && !vanillaReachedRegions.Contains(ReplacementRegions[j]))
                    vanillaReachedRegions.Add(ReplacementRegions[j]);
                else if (!vanillaReachedRegions.Contains(ReplacedRegions[j]) && vanillaReachedRegions.Contains(ReplacementRegions[j]))
                    vanillaReachedRegions.Add(ReplacedRegions[j]);
            }
        }
        if (reachedRegions.Count < vanillaReachedRegions.Count)
        {
            Logger.LogDebug("Not enough regions reached");
            return RandomizeRegions(gateNames, regions, gates, rand);
        }

        return newGates;
    }

    private List<string> RandomizeConnections(List<string> oldNames, List<string> newNames, List<string> regions)
    {
        System.Random rand = new System.Random();
        List<string> connections = new ();
        for (int i = 0; i < oldNames.Count; i++)
            connections.Add(oldNames.ElementAt(i));

        //new logic
        //...just copy logic from RandomizeRegions ...?
        if (Options.sensibleRegionPlacements.Value) //new randomizer logic
        {
            //TODO: figure out how blacklisted regions might work; currently they're just randomized too
            //now convert this to newGates
            string[] tempGates = new string[oldNames.Count];
            foreach (Connectible connectible in Connectibles)
            {
                foreach (var conn in connectible.connections)
                {
                    int idx = oldNames.IndexOf(conn.Key);
                    if (idx >= 0)
                    {
                        string reg2 = conn.Key.Split('_')[2];//connectible.name;//
                        int replacementIdx = Array.IndexOf(ReplacementRegions, connectible.name);
                        bool isValidNewGate = reg2 == connectible.name || (replacementIdx >= 0 && reg2 == ReplacedRegions[replacementIdx]);
                        if (!isValidNewGate)
                            continue;

                        reg2 = connectible.name;

                        string newName = "GATE_" + conn.Value + "_" + reg2;

                        //check if need to swap positions
                        if (!newNames.Contains(newName) && !tempGates.Contains(newName))
                            newName = "GATE_" + reg2 + "_" + conn.Value;

                        /*
                        if (tempGates.Contains(newName))
                        {
                            string oldNewName = newName;
                            newName += "_1";
                            for (int j = 2; tempGates.Contains(newName); j++)
                                newName = oldNewName + "_" + j; //GATE_XX_YY_ + j
                            Logger.LogDebug("Duplicate gate " + newName);
                        }
                        */
                        tempGates[idx] = newName;
                    }
                }
            }

            //emergency cleanup:
            for (int i = 0; i < oldNames.Count; i++)
            {
                if (tempGates[i] == null || tempGates[i] == "")
                {
                    Logger.LogDebug("Failed to find gateConnection for: " + oldNames[i]);
                    tempGates[i] = oldNames[i];
                }
            }

            connections = tempGates.ToList();
        }

        else //old logic
        {
            int regionTries = 0;
            for (int i = 0; i < regions.Count; i++)
            {
                string region = regions.ElementAt(i);
                /*
                if (replacedRegions.Contains(region))
                    continue;
                string oldRegion = "";
                if (replacementRegions.Contains(region))
                    oldRegion = replacedRegions[Array.IndexOf(replacementRegions, region)];
                */

                if (regionTries > 10)
                {
                    for (int j = 0; j < oldNames.Count; j++)
                    {
                        if (oldNames.ElementAt(j).Split('_')[2] == region)// || oldNames.ElementAt(j).Split('_')[2] == oldRegion)
                            connections[j] = oldNames.ElementAt(j);
                    }
                    regionTries = 0;
                    continue;
                }

                List<string> conns = new();
                foreach (string n in newNames)
                {
                    if (oldNames.Contains(n)) //don't add vanilla gates to the list
                        continue;
                    if (n.Split('_')[2] == region)// || n.Split('_')[2] == oldRegion)
                                                  //conns.Add(n.Split('_')[1]);
                        conns.Add(n);
                }
                for (int j = 0; j < oldNames.Count; j++)
                {
                    //if (newNames.Contains(oldNames.ElementAt(j))) //don't make connections for vanilla gates
                    //continue;

                    if (oldNames.ElementAt(j).Split('_')[2] == region && conns.Count > 0)
                    {
                        int idx = rand.Next() % conns.Count;
                        //string s = "GATE_" + conns.ElementAt(idx);
                        //s += "_" + region;
                        string s = conns.ElementAt(idx);
                        regionTries = 0;
                        connections[j] = s;
                        //conns.erase(next(conns.begin(), idx));
                        conns.RemoveAt(idx);
                    }
                }
            }
        }

        Connectibles.Clear();

        return connections;
    }

    
    private void WriteGateLocks(List<string> regions, List<string> oldGates, List<string> newGates1, List<string> newGates2, List<bool> mapSwapped)
    {
        System.Random rand = new();
        List<string> gatesShuffled = new();
        foreach (string g in newGates1)
            gatesShuffled.Add(g);
        gatesShuffled.Shuffle();

        List<string> echoRegions = GetEchoRegions(regions, Options.Slugcat);

        //check that every region is accessible
        bool markFound = MarkAtStart;
        List<string> reachedRegions = new();
        List<int> obtainedEchoes = new();
        int echoCount = 0;
        reachedRegions.Add(Options.startingRegion.Value);
        if (echoRegions.Contains(Options.startingRegion.Value))
            echoCount++;
        obtainedEchoes.Add(echoCount);
        for (int i = 0; i < regions.Count && reachedRegions.Count < regions.Count; i++)
        {
            foreach (string s0 in gatesShuffled)
            {
                string s1 = s0.Split('_')[1];
                string s2 = s0.Split('_')[2];
                if (reachedRegions.Contains(s1))
                {
                    if (!reachedRegions.Contains(s2) && !BlacklistedRegions.Contains(s2))
                    {
                        if (!(InaccessableGates.ContainsKey(s1) && InaccessableGates[s1].Contains(s0)) && !(InaccessableRegions.ContainsKey(s0) && InaccessableRegions[s0].Contains(s2)))
                        {
                            if (markFound || !COMSMARKRegions.Contains(s2))
                            {
                                reachedRegions.Add(s2);
                                if (IteratorRegions.Contains(s2))
                                    markFound = true;
                                obtainedEchoes.Add(echoCount);
                                if (echoRegions.Contains(s2))
                                    echoCount++;
                            }
                            
                        }
                    }
                }
                else if (reachedRegions.Contains(s2))
                {
                    if (!reachedRegions.Contains(s1) && !BlacklistedRegions.Contains(s1))
                    {
                        if (!(InaccessableGates.ContainsKey(s2) && InaccessableGates[s2].Contains(s0)) && !(InaccessableRegions.ContainsKey(s0) && InaccessableRegions[s0].Contains(s1)))
                        {
                            if (markFound || !COMSMARKRegions.Contains(s1))
                            {
                                reachedRegions.Add(s1);
                                if (IteratorRegions.Contains(s1))
                                    markFound = true;
                                obtainedEchoes.Add(echoCount);
                                if (echoRegions.Contains(s1))
                                    echoCount++;
                            }
                        }
                    }
                }
            }
            for (int j = 0; j < ReplacedRegions.Length; j++) {
                if (reachedRegions.Contains(ReplacedRegions[j]) && !reachedRegions.Contains(ReplacementRegions[j]))
                {
                    reachedRegions.Add(ReplacementRegions[j]);
                    int idx = reachedRegions.IndexOf(ReplacedRegions[j]);
                    obtainedEchoes.Add(obtainedEchoes.ElementAt(idx));
                } else if (!reachedRegions.Contains(ReplacedRegions[j]) && reachedRegions.Contains(ReplacementRegions[j]))
                {
                    reachedRegions.Add(ReplacedRegions[j]);
                    int idx = reachedRegions.IndexOf(ReplacementRegions[j]);
                    obtainedEchoes.Add(obtainedEchoes.ElementAt(idx));
                }
            }
        }


        //determine max achievable karma
        /*
        double tempKarma = Options.startingKarma.Value;
        foreach (string r in reachedRegions)
        {
            if (echoRegions.Contains(r) && !COMSMARKRegions.Contains(r) && !IteratorRegions.Contains(r))
                try { tempKarma += Double.Parse(Options.karmaPerEcho.Value); } catch (Exception ex) { }
        }
        int maxKarma = Math.Min((int)Math.Floor(tempKarma), KarmaCap);
        */
        List<string> noMarkReachedRegions = new();
        noMarkReachedRegions.Add(Options.startingRegion.Value);
        int obtainableEchoes = 0;
        if (echoRegions.Contains(Options.startingRegion.Value))
            obtainableEchoes++;
        for (int i = 0; i < regions.Count && noMarkReachedRegions.Count < regions.Count; i++)
        {
            foreach (string s0 in gatesShuffled)
            {
                string s1 = s0.Split('_')[1];
                string s2 = s0.Split('_')[2];
                if (noMarkReachedRegions.Contains(s1))
                {
                    if (!noMarkReachedRegions.Contains(s2) && !BlacklistedRegions.Contains(s2))
                    {
                        if (!(InaccessableGates.ContainsKey(s1) && InaccessableGates[s1].Contains(s0)) && !(InaccessableRegions.ContainsKey(s0) && InaccessableRegions[s0].Contains(s2)))
                        {
                            if ((MarkAtStart || !COMSMARKRegions.Contains(s2)) && !IteratorRegions.Contains(s2))
                            {
                                noMarkReachedRegions.Add(s2);
                                if (echoRegions.Contains(s2))
                                    obtainableEchoes++;
                            }

                        }
                    }
                }
                else if (noMarkReachedRegions.Contains(s2))
                {
                    if (!noMarkReachedRegions.Contains(s1) && !BlacklistedRegions.Contains(s1))
                    {
                        if (!(InaccessableGates.ContainsKey(s2) && InaccessableGates[s2].Contains(s0)) && !(InaccessableRegions.ContainsKey(s0) && InaccessableRegions[s0].Contains(s1)))
                        {
                            if ((MarkAtStart || !COMSMARKRegions.Contains(s1)) && !IteratorRegions.Contains(s1))
                            {
                                noMarkReachedRegions.Add(s1);
                                if (echoRegions.Contains(s1))
                                    obtainableEchoes++;
                            }
                        }
                    }
                }
            }
            for (int j = 0; j < ReplacedRegions.Length; j++)
            {
                if (noMarkReachedRegions.Contains(ReplacedRegions[j]) && !noMarkReachedRegions.Contains(ReplacementRegions[j]))
                    noMarkReachedRegions.Add(ReplacementRegions[j]);
                else if (!noMarkReachedRegions.Contains(ReplacedRegions[j]) && noMarkReachedRegions.Contains(ReplacementRegions[j]))
                    noMarkReachedRegions.Add(ReplacedRegions[j]);
            }
        }
        int maxKarma = Math.Min((int)Math.Floor(Options.startingKarma.Value + obtainableEchoes * Double.Parse(Options.karmaPerEcho.Value)), KarmaCap);


        //random karma leniency
        List<double> leniencyPerNewGate1 = new List<double>();
        foreach (string g in newGates1)
            leniencyPerNewGate1.Add((newGates1.Contains(g) && newGates1.IndexOf(g) < leniencyPerNewGate1.Count) ? leniencyPerNewGate1[newGates1.IndexOf(g)] //if already added, copy
                : (Double.Parse(Options.randomKarmaLeniency.Value) * 0.01 * (rand.Next() % 100)));
        List<double> leniencyPerNewGate2 = new();
        foreach (string g in newGates2)
            leniencyPerNewGate2.Add((newGates1.Contains(g) && newGates1.IndexOf(g) < leniencyPerNewGate1.Count) ? leniencyPerNewGate1[newGates1.IndexOf(g)] //if in newGates1, copy
                : ((newGates2.Contains(g) && newGates2.IndexOf(g) < leniencyPerNewGate2.Count) ? leniencyPerNewGate2[newGates2.IndexOf(g)] //if already added, copy
                : (Double.Parse(Options.randomKarmaLeniency.Value) * 0.01 * (rand.Next() % 100))));

        //ofstream outFS;
        //outFS.open("C:\\Program Files (x86)\\Steam\\steamapps\\common\\Rain World\\RainWorld_Data\\StreamingAssets\\mods\\regionrandomizer\\modify\\world\\gates\\locks.txt");
        //outFS << "[MERGE]\n";
        string fileData = "";//"[MERGE]\n";
        for (int i = 0; i < oldGates.Count; i++)
        {
            string origGate = oldGates.ElementAt(i);
            string s1 = newGates1.ElementAt(i).Split('_')[1];
            string s2 = newGates1.ElementAt(i).Split('_')[2];
            string c1 = newGates2.ElementAt(i).Split('_')[1];
            string c2 = newGates2.ElementAt(i).Split('_')[2];
            int leftLock = 1;
            int rightLock = 1;
            //left region
            int echoes1 = 0;
            int idx = reachedRegions.IndexOf(s1);
            int idx2 = reachedRegions.IndexOf(s2);
            if (idx >= 0 && idx2 >= 0)
                echoes1 = Math.Min(obtainedEchoes.ElementAt(idx), obtainedEchoes.ElementAt(idx2));
            //right region
            int echoes2 = 0;
            idx = reachedRegions.IndexOf(c1);
            idx2 = reachedRegions.IndexOf(c2);
            if (idx >= 0 && idx2 >= 0)
                echoes2 = Math.Min(obtainedEchoes.ElementAt(idx), obtainedEchoes.ElementAt(idx2));

            //int echoesMin = Math.Min(echoes1, echoes2);
            leftLock = (int)Math.Ceiling(Options.startingKarma.Value + echoes1 * Double.Parse(Options.karmaPerEcho.Value) - leniencyPerNewGate1[i]);
            rightLock = (int)Math.Ceiling(Options.startingKarma.Value + echoes2 * Double.Parse(Options.karmaPerEcho.Value) - leniencyPerNewGate2[i]);

            //clamps
            if (leftLock < 1)
                leftLock = 1;
            else if (leftLock > KarmaCap)
                leftLock = KarmaCap;
            if (rightLock < 1)
                rightLock = 1;
            else if (rightLock > KarmaCap)
                rightLock = KarmaCap;
            string leftString = leftLock.ToString();
            string rightString = rightLock.ToString();

            //force locks
            string[] locks = Options.presetLocks.Value.Split(',');
            idx = -1;
            for (int j = 0; j < locks.Length; j++)
            {
                if (locks[j].Split(':')[0] == c1)
                {
                    idx = j;
                    break;
                }
            }
            if (idx >= 0)
            {
                bool useMaxKarma = locks[idx].Split(':')[1] == "m" || (locks[idx].Split(':')[1] == "c" && MarkAtStart); //use max karma instead of comsmark if starting with comsmark
                rightString = (useMaxKarma) ? maxKarma.ToString() : locks[idx].Split(':')[1];
            }

            idx = -1;
            for (int j = 0; j < locks.Length; j++)
            {
                if (locks[j].Split(':')[0] == s2)
                {
                    idx = j;
                    break;
                }
            }
            if (idx >= 0)
            {
                bool useMaxKarma = locks[idx].Split(':')[1] == "m" || (locks[idx].Split(':')[1] == "c" && MarkAtStart);
                leftString = (useMaxKarma) ? maxKarma.ToString() : locks[idx].Split(':')[1];
            }

            //forbidden regions force f locks
            if (BlacklistedRegions.Contains(c1))
                rightString = "f";
            if (BlacklistedRegions.Contains(s2))
                leftString = "f";

            //inaccessable regions force 1 locks (this prevents players from getting stuck in SL by GATE_UW_SL, etc.)
            if (InaccessableRegions.ContainsKey(origGate))
            {
                bool found = false;
                foreach (string s in InaccessableRegions[origGate])
                {
                    if (origGate.EndsWith(s))
                    {
                        found = true;
                        break;
                    }
                }
                if (found)
                    rightString = "1";
                else
                    leftString = "1";
            }

            //incomplete gates force locks
            if (s2 == "")
                leftString = "f";
            if (c1 == "")
                rightString = "f";

            //outFS << gates.ElementAt(i) << " : " << ((rand() % 5) + 1) << " : " << ((rand() % 5) + 1);
            //outFS << gates.ElementAt(i) << " : " << leftString << " : " << rightString;

            if (mapSwapped.ElementAt(i))
                fileData += origGate + " : " + rightString + " : " + leftString + " : SWAPMAPSYMBOL";
            else
                fileData += origGate + " : " + leftString + " : " + rightString;
            fileData += "\n";
        }
        //fileData += "[END MERGE]";
        //outFS.close();

        //actually write to the file
        //find region randomizer mod
        ModManager.Mod randomizerMod = null;
        ModManager.ActiveMods.ForEach(mod =>
        {
            if (mod.id == "LazyCowboy.RegionRandomizer")
                randomizerMod = mod;
        });
        if (randomizerMod == null)
            return;

        string filePath = AssetManager.ResolveFilePath(string.Concat(new string[] {
            "RegionRandomizer",
            Path.DirectorySeparatorChar.ToString(),
            "locks-",
            Options.Slugcat,
            ".txt"
        }));
        try
        {
            //Directory.CreateDirectory(filePath);
            //filePath += Path.DirectorySeparatorChar.ToString() + ;
            File.WriteAllText(filePath, fileData);
        } catch (Exception ex) { }
    }
    #endregion

    #region FileWriters
    private void WriteModifyFiles(List<List<string>> gates, List<string> gateData, List<string> regions, List<string> oldGates, List<string> newGates, List<string> connections, List<string> mapSwapped)
    {
        //find region randomizer mod
        ModManager.Mod randomizerMod = null;
        ModManager.ActiveMods.ForEach(mod =>
        {
            if (mod.id == "LazyCowboy.RegionRandomizer")
                randomizerMod = mod;
        });
        if (randomizerMod == null)
            return;

        //clear world directory
        string basePath = string.Concat(new string[] {
            randomizerMod.path,
            Path.DirectorySeparatorChar.ToString(),
            "Modify",
            Path.DirectorySeparatorChar.ToString(),
            "World"
        });
        try
        {
            Directory.Delete(basePath, true);
            Directory.CreateDirectory(basePath);
        } catch (Exception ex) { }


        //modify files
        for (int r = 0; r < regions.Count; r++)
        {
            string region = regions.ElementAt(r);
            //ofstream outFS, mapOutFS;
            //string path = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Rain World\\RainWorld_Data\\StreamingAssets\\mods\\regionrandomizer\\modify\\world\\" + region;
            string filePath = basePath + Path.DirectorySeparatorChar.ToString() + region;

            //create directory if non-existant
            try
            {
                Directory.CreateDirectory(filePath);
            } catch (Exception ex) { }

            string mapPath = filePath + Path.DirectorySeparatorChar.ToString() + "map_" + region + ".txt";
            filePath += Path.DirectorySeparatorChar.ToString() + "world_" + region + ".txt";

            string fileData = "";
            string mapData = "";
            
            List<string> gs = gates.ElementAt(r);
            foreach (string gate in gs)
            {
                string g = String.Join("_", (Regex.Split(gate, ":")[0]).Trim().Split('_'), 0, 3);
                //bool baseGate = false;
                if (g.Split('_')[1] == region)
                    continue;
                if (g.Split('_')[2] != region)
                {
                    //find another region with this gate
                    string newRegion = "";
                    for (int i = 0; i < gates.Count; i++)
                    {
                        if (i == r)
                            continue;
                        if (gates.ElementAt(i) == gs)
                        { //this currently is never true
                            string n = regions.ElementAt(i);
                            if (n != g.Split('_')[1] && n != g.Split('_')[2])
                                continue;
                            newRegion = n;
                            //cout << "Found equivalent region " << newRegion << " for " << region << endl;
                            break;
                        }
                    }
                    if (newRegion == "")
                        continue;
                    if (g.Split('_')[1] == newRegion)
                        continue;
                }

                int idx = oldGates.IndexOf(g);
                if (idx >= 0)
                {
                    //outFS << "[FIND]" << g << "\n[REPLACE]" << connections.ElementAt(idx) << "\n";
                    fileData += "[FIND]" + g + "\n[REPLACE]" + oldGates.ElementAt(newGates.IndexOf(connections.ElementAt(idx))) + "\n";
                    //mapOutFS << "[FIND]" << g << "\n[REPLACE]" << connections.ElementAt(idx) << "\n";
                    mapData += "[FIND]" + g + "\n[REPLACE]" + oldGates.ElementAt(newGates.IndexOf(connections.ElementAt(idx))) + "\n";
                    //egatesFS << "[FIND]" << g << "\n[REPLACE]" << connections.ElementAt(idx) << "\n";

                    string c = connections.ElementAt(idx);
                    int dataIdx = newGates.IndexOf(String.Join("_", c.Split('_'), 0, 2) + "_" + region);
                    if (dataIdx < 0)
                    {
                        //cout << "Couldn't find data! " << c << g << endl;
                        continue;
                    }
                    string data = gateData.ElementAt(dataIdx);
                    //cout << "Gate: " << gate << "; Data: " << data << endl;
                    //ss1.str(data);
                    //ss2.str(gate);
                    string newName = "";
                    List<string> conns1 = new();
                    List<string> conns2 = new();
                    int connCount = 0;
                    bool startFound = false;
                    //ss1 >> conn; //ignore gate name

                    string[] split = data.Split(' ');
                    for (int i = 1; i < split.Length; i++)
                    {
                        string conn = split[i];
                        if (conn == ":")
                        {
                            if (startFound)
                                break;
                            startFound = true;
                        }
                        else if (startFound)
                        {
                            if (conn.ElementAt(conn.Length - 1) == ',')
                                conns1.Add(conn.Substring(0, conn.Length - 1));
                            else
                                conns1.Add(conn);
                            if (conns1.ElementAt(conns1.Count - 1) != "DISCONNECTED")
                                connCount++;
                        }
                    }
                    //conns1.pop_back();
                    string connectionName = "";
                    List<string> connectionShelters = new();
                    startFound = false;
                    //ss2 >> conn; //ignore gate name
                    split = gate.Split(' ');
                    for (int i = 1; i < split.Length; i++)
                    {
                        string conn = split[i];
                        if (conn == ":")
                        {
                            if (startFound)
                                break;
                            startFound = true;
                        }
                        else if (startFound && conn.Length > 1)
                        {
                            string connstring = "";
                            if (conn.ElementAt(conn.Length - 1) == ',')
                                connstring = conn.Substring(0, conn.Length - 1);
                            else
                                connstring = conn;
                            conns2.Add(connstring);
                            if (connstring != "DISCONNECTED")
                            {
                                connCount++;
                                if (connstring.Length < 4)
                                {
                                    //cout << " Suspiciously short: " << connstring;
                                    connectionShelters.Add(connstring);
                                }
                                else if (connstring.Length < 7)
                                {
                                    if (connstring.ElementAt(2) == '_' && (connstring.ElementAt(3) == 'S' || connstring.ElementAt(3) == 's'))
                                        connectionShelters.Add(connstring);
                                    else if (connectionName == "")
                                        connectionName = connstring;
                                    else
                                    {
                                        connectionShelters.Add(connstring);
                                        //cout << "Found " << connectionName << " and " << connstring << " for " << g << endl;
                                    }
                                }
                                else
                                {
                                    if (connstring.ElementAt(2) == '_' && ((connstring.ElementAt(3) == 'S' || connstring.ElementAt(3) == 's')
                                    || (connstring.ElementAt(5) == '_' && (connstring.ElementAt(6) == 'S' || connstring.ElementAt(6) == 's'))))
                                    {
                                        connectionShelters.Add(connstring);
                                        //cout << connstring << " ";
                                    }
                                    else if (connectionName == "")
                                        connectionName = connstring;
                                    else
                                    {
                                        connectionShelters.Add(connstring);
                                        //cout << "Found " << connectionName << " and " << connstring << " for " << g << endl;
                                    }
                                }
                            }
                        }
                    }
                    if (connectionName == "")
                    {
                        if (connectionShelters.Count > 0)
                        {
                            connectionName = connectionShelters.ElementAt(connectionShelters.Count - 1);
                            connectionShelters.Pop();
                            //cout << "Used shelter " << connectionName << " for connection" << endl;
                        }
                        else
                        {
                            //cout << "No connection found for " << g << endl;
                            continue;
                        }
                    }
                    //if (connCount <= conns1.Count)
                    //    continue;
                    //  outFS << "[FIND]" << (connections.ElementAt(idx) + gate.Substring(10)) << "\n[REPLACE]" << (connections.ElementAt(idx) + gate.Substring(10)) << "\n";

                    //figure out if mapswapped
                    bool swap = mapSwapped.ElementAt(dataIdx) == "SWAPMAPSYMBOL";
                    //maybe more code later?? just see if this works...
                    string newData = " : ";
                    bool connPlaced = false;
                    for (int k = 0; k < conns1.Count; k++)
                    {
                        if (k > 0)
                            newData += ", ";
                        string c2 = conns1.ElementAt(k);
                        if ((c2 == "DISCONNECTED") != swap)
                        { //xor with swap
                            if (!connPlaced)
                            {
                                newData += connectionName;
                                connPlaced = true;
                            }
                            else if (connectionShelters.Count > 0)
                            {
                                newData += connectionShelters.ElementAt(connectionShelters.Count - 1);
                                connectionShelters.Pop();
                            }
                            else
                            {
                                newData += "DISCONNECTED";
                            }
                        }
                        else
                            newData += "DISCONNECTED";
                    }
                    if (!connPlaced)
                        newData += ", " + connectionName;
                    foreach (string s in connectionShelters)
                        newData += ", " + s;
                    newData += " : GATE";
                    if (newData == gate.Substring(10)) //if no change, don't apply any change!
                        continue;

                    //outFS << "[FIND]" << (connections.ElementAt(idx) + gate.Substring(10)) << "\n[REPLACE]" << (connections.ElementAt(idx) + newData) << "\n";
                    fileData += "[FIND]" + (oldGates.ElementAt(newGates.IndexOf(connections.ElementAt(idx))) + gate.Substring(10)) + "\n[REPLACE]" + (oldGates.ElementAt(newGates.IndexOf(connections.ElementAt(idx))) + newData) + "\n";

                    //outFS << "[FIND]" << gate << "\n[REPLACE]" << connections.ElementAt(idx) << "\n";
                }
                else
                    Logger.LogDebug("Gate not found! " + g);
            }
            /*
            //hide previous gate rooms
            fileData += "[MERGE]\nCONDITIONAL LINKS\n";
            foreach (string g in oldGates)
            {
                if (g.Split('_')[2] == region)
                    fileData += Options.Slugcat + " : HIDEROOM : " + g + "\n";
            }
            fileData += "END CONDITIONAL LINKS\n[END MERGE]";
            */
            //write to file
            try
            {
                File.WriteAllText(filePath, fileData);
                File.WriteAllText(mapPath, mapData);
            } catch (Exception ex) { }
        }



        //WriteGateLocks(regions, oldGates, newGates, connections, mapSwapped);
    }

    private void WriteRandomizerFiles()
    {
        //find region randomizer mod
        /*
        ModManager.Mod randomizerMod = null;
        ModManager.ActiveMods.ForEach(mod =>
        {
            if (mod.id == "LazyCowboy.RegionRandomizer")
                randomizerMod = mod;
        });
        if (randomizerMod == null)
            return;
        */

        string filePath = AssetManager.ResolveFilePath(string.Concat(new string[] {
            "RegionRandomizer",
            Path.DirectorySeparatorChar.ToString(),
            "RegionRandomizer-",
            Options.Slugcat,
            ".txt"
        }));
        string fileData = "";
        for (int i = 0; i < NewGates1.Length; i++)
        {
            if (i > 0)
                fileData += "\n";
            fileData += GateNames[i] + ";" + NewGates1[i] + ";" + NewGates2[i];
        }

        try
        {
            File.WriteAllText(filePath, fileData);
        }
        catch (Exception ex) { }
    }

    private static void WriteModList()
    {
        //find region randomizer mod
        /*
        ModManager.Mod randomizerMod = null;
        ModManager.ActiveMods.ForEach(mod =>
        {
            if (mod.id == "LazyCowboy.RegionRandomizer")
                randomizerMod = mod;
        });
        if (randomizerMod == null)
            return;
        */

        string filePath = AssetManager.ResolveFilePath(string.Concat(new string[] {
            "RegionRandomizer",
            Path.DirectorySeparatorChar.ToString(),
            "RegionRandomizerModList.txt"
        }));
        string fileData = "";
        for (int i = 0; i < ModManager.ActiveMods.Count; i++)
        {
            if (i > 0)
                fileData += "\n";
            fileData += ModManager.ActiveMods[i].name + ": " + ModManager.ActiveMods[i].id;
        }

        try
        {
            File.WriteAllText(filePath, fileData);
        }
        catch (Exception ex) { }
    }
    #endregion

    private string[] ReplacedRegions = new string[0];
    private string[] ReplacementRegions = new string[0];

    private Dictionary<string, List<string>> InaccessableGates;
    private Dictionary<string, List<string>> InaccessableRegions;
    private List<string> IteratorRegions;
    private List<string> COMSMARKRegions;
    private bool MarkAtStart = false;

    private List<string> BlacklistedRegions;

    public void RandomizeAllRegions()
    {
        Task.Run(() =>
        {
            try
            {
                WriteModList();

                //OverWorld world = game.overWorld;
                //string[] regions = GetRegions(world);
                //GetRegions();
                Logger.LogDebug("RegionRandomizer: 0");
                List<string> regions = GetRegions();

                //shuffle regions, because... why not?
                regions.Shuffle();

                //process ignored regions
                for (int i = regions.Count - 1; i >= 0; i--)
                {
                    if (Options.ignoredRegions.ContainsKey(regions[i]) && Options.ignoredRegions[regions[i]].Value == true)
                        regions.RemoveAt(i);
                }
                BlacklistedRegions = new List<string>();
                //get blacklisted regions
                foreach (string r in regions)
                {
                    if (Options.blacklistedRegions.ContainsKey(r) && Options.blacklistedRegions[r].Value == true)
                        BlacklistedRegions.Add(r);
                }

                Logger.LogDebug("RegionRandomizer: 1");
                List<List<string>> gates = GetGateData(regions);
                Logger.LogDebug("RegionRandomizer: 2");

                //remove 0-gate regions
                for (int i = gates.Count - 1; i >= 0; i--)
                {
                    if (gates.ElementAt(i).Count < 1)
                    {
                        Logger.LogDebug("Removed region: " + regions.ElementAt(i));
                        gates.RemoveAt(i);
                        regions.RemoveAt(i);
                    }
                }
                Logger.LogDebug("RegionRandomizer: 3");

                //hardcoded slugcat-specific region name changes
                if (Options.Slugcat == "Rivulet") { ReplacedRegions = new string[] { "SS" }; ReplacementRegions = new string[] { "RM" }; }
                else if (Options.Slugcat == "Artificer" || Options.Slugcat == "Spear") { ReplacedRegions = new string[] { "SL" }; ReplacementRegions = new string[] { "LM" }; }
                else if (Options.Slugcat == "Saint") { ReplacedRegions = new string[] { "DS", "SH", "SS" }; ReplacementRegions = new string[] { "UG", "CL", "RM" }; }

                //get gate names and data
                List<string> gateNames = new();
                List<string> gateData = new();
                for (int i = 0; i < gates.Count; i++)
                {
                    List<string> g = gates.ElementAt(i);
                    string region = regions.ElementAt(i);

                    if (ReplacedRegions.Contains(region))
                        continue;
                    if (ReplacementRegions.Contains(region))
                        region = ReplacedRegions[Array.IndexOf(ReplacementRegions, region)];

                    for (int j = 0; j < g.Count; j++)
                    {

                        string n = Regex.Split(g.ElementAt(j), ":")[0].Trim().ToUpper();//.Substring(0, 10);
                        if (n.Split('_')[1] != region)
                            continue;
                        //skip gates with unfound regions
                        if (!regions.Contains(n.Split('_')[1]) || !regions.Contains(n.Split('_')[2]))
                            continue;
                        //if (!found)
                        if (!gateNames.Contains(n))
                        {
                            gateNames.Add(n);
                            gateData.Add(g.ElementAt(j));
                        }
                    }
                }

                //shorten gateNames
                for (int i = 0; i < gateNames.Count; i++)
                {
                    string[] d = gateNames[i].Split('_');
                    if (d.Length > 3)
                        gateNames[i] = d[0] + "_" + d[1] + "_" + d[2];
                }

                Logger.LogDebug("RegionRandomizer: 3.5");
                //remove gate names with only one connection
                for (int i = gateNames.Count - 1; i >= 0; i--)
                {
                    string region = gateNames[i].Split('_')[2];
                    if (ReplacedRegions.Contains(region))
                        region = ReplacementRegions[Array.IndexOf(ReplacedRegions, region)];
                    bool found = false;
                    int idx = regions.IndexOf(region);
                    if (idx >= 0)
                    {
                        foreach (string g in gates.ElementAt(idx))
                        {
                            if (g.StartsWith(gateNames[i]))
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                    if (!found)
                    {
                        Logger.LogDebug("Gate with only one connection: " + gateNames[i]);
                        gateNames.RemoveAt(i);
                        gateData.RemoveAt(i);
                    }
                }

                Logger.LogDebug("RegionRandomizer: 4");

                List<string> gateLocks = GetGateLocks();
                Logger.LogDebug("RegionRandomizer: 5");
                //mapswapped gates
                List<bool> mapSwapped = new();
                string debugMapSwapped = "";
                //foreach (string n in gateNames)
                for (int i = 0; i < gateNames.Count; i++)
                {
                    string n = gateNames.ElementAt(i);
                    bool found = false;
                    foreach (string l in gateLocks)
                    {
                        string[] stuff = Regex.Split(l, " : ");
                        if (stuff.Length >= 4 && stuff[0].Trim().ToUpper() == n)
                        {
                            found = stuff[3] == "SWAPMAPSYMBOL";
                            break;
                        }
                    }
                    mapSwapped.Add(found);
                    debugMapSwapped += " " + found;
                }

                //always mapswapped
                string[] alwaysMapSwapped = new string[] { "GATE_SS_UW", "GATE_SL_VS", "GATE_DM_SL", "GATE_LF_SB", "GATE_DS_SB", "GATE_HI_CC", "GATE_RA_LF", "GATE_AB_LF", "GATE_SL_AB" };
                foreach (string g in alwaysMapSwapped)
                {
                    int idx = gateNames.IndexOf(g);
                    if (idx >= 0)
                    {
                        mapSwapped[idx] = true;
                    }
                }

                Logger.LogDebug("Map swaps:" + debugMapSwapped);
                Logger.LogDebug("RegionRandomizer: 6");


                //region reached checker hardcoded settings
                InaccessableGates = new();
                AddToDictionaryList(InaccessableGates, "SU", "GATE_OE_SU"); //vanilla regions
                AddToDictionaryList(InaccessableGates, "FR", "GATE_PA_FR");
                if (Options.World_State != "Saint") { AddToDictionaryList(InaccessableGates, "SL", "GATE_SL_MS"); AddToDictionaryList(InaccessableGates, "MS", "GATE_SL_MS"); AddToDictionaryList(InaccessableGates, "SB", "GATE_LF_SB"); }
                if (Options.Slugcat != "Artificer" && Options.Slugcat != "Rivulet" && Options.World_State != "Saint") AddToDictionaryList(InaccessableGates, "SH", "GATE_GW_SH");
                if (Options.World_State != "Artificer" && Options.World_State != "Spear") AddToDictionaryList(InaccessableGates, "SL", "GATE_UW_SL");

                InaccessableRegions = new();
                AddToDictionaryList(InaccessableRegions, "GATE_OE_SU", "OE"); //vanilla regions
                AddToDictionaryList(InaccessableRegions, "GATE_RA_LF", "RA"); //custom regions
                if (Options.World_State != "Saint") AddToDictionaryList(InaccessableRegions, "GATE_SL_MS", "MS");
                if (Options.Slugcat != "Spear" || Options.Slugcat == "Inv") AddToDictionaryList(InaccessableRegions, "GATE_SS_UW", "SS");
                if (Options.World_State != "Artificer" && Options.World_State != "Spear") AddToDictionaryList(InaccessableRegions, "GATE_UW_SL", "SL");

                IteratorRegions = new(new string[] { "CW", "DM", "SS" });
                if (Options.Slugcat == "Spear" || Options.Slugcat == "Rivulet" || Options.Slugcat == "Saint") IteratorRegions.Pop();
                COMSMARKRegions = new();
                string[] presetLocks = Options.presetLocks.Value.Split(',');
                foreach (string l in presetLocks)
                {
                    string[] s = l.Split(':');
                    if (s.Length < 2) continue;
                    if (s[1].ToLower() == "c")
                        COMSMARKRegions.Add(s[0]);
                }

                //MarkAtStart = Options.Slugcat == "Red" || Options.Slugcat == "Rivulet" || Options.Slugcat == "Saint" || Options.Slugcat == "Inv";
                MarkAtStart = Options.Starts_Mark;


                //randomizer logic
                List<string> newGates = RandomizeRegions(gateNames, regions, gates);
                Logger.LogDebug("RegionRandomizer: 7");
                List<string> connections = RandomizeConnections(gateNames, newGates, regions);
                Logger.LogDebug("RegionRandomizer: 8");
                //WriteModifyFiles(gates, gateData, regions, gateNames, newGates, connections, mapSwapped);

                //use full gate names for writing the gate locks (e.g: GATE_HH_DS_Monk)
                List<string> altOldGates = new();
                //foreach (string g in gateData)
                    //altOldGates.Add(g.Split(':')[0].Trim());

                //WriteGateLocks(regions, gateNames, newGates, connections, mapSwapped);
                //WriteGateLocks(regions, altOldGates, newGates, connections, mapSwapped);
                Logger.LogDebug("RegionRandomizer: 9");

                GateNames = gateNames.ToArray();
                NewGates1 = newGates.ToArray();
                NewGates2 = connections.ToArray();

                ReadLocksFiles(Options.Slugcat); //reread locks file, now that it has been rewritten

                WriteRandomizerFiles();
                Logger.LogDebug("RegionRandomizer: 10");

                //clear all lists/dictionaries
                gateNames.Clear();
                regions.Clear();
                foreach (var thing in InaccessableGates.Values)
                    thing.Clear();
                InaccessableGates.Clear();
                foreach (var thing in InaccessableRegions.Values)
                    thing.Clear();
                InaccessableRegions.Clear();
                IteratorRegions.Clear();
                COMSMARKRegions.Clear();
                foreach (var thing in gates)
                    thing.Clear();
                gates.Clear();
                gateData.Clear();
                gateLocks.Clear();
                mapSwapped.Clear();
                newGates.Clear();
                connections.Clear();
                altOldGates.Clear();

            } catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        });
    }
    private static void AddToDictionaryList(Dictionary<string, List<string>> d, string k, string v)
    {
        if (d.ContainsKey(k))
            d[k].Add(v);
        else
            d[k] = new List<string>(new string[] { v });
    }


    public void ClearAllRegions()
    {
        //find region randomizer mod
        ModManager.Mod randomizerMod = null;
        ModManager.ActiveMods.ForEach(mod =>
        {
            if (mod.id == "LazyCowboy.RegionRandomizer")
                randomizerMod = mod;
        });
        if (randomizerMod == null)
            return;

        //clear world directory
        string gatePath = string.Concat(new string[] {
            randomizerMod.path,
            Path.DirectorySeparatorChar.ToString(),
            "Modify",
            Path.DirectorySeparatorChar.ToString(),
            "World",
            Path.DirectorySeparatorChar.ToString(),
            "Gates"
        });
        try
        {
            //Directory.Delete(basePath, true);
            //Directory.CreateDirectory(basePath);
            Directory.Delete(gatePath, true); //delete gate locks file
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }

        //delete randomizer info
        string worldPath = string.Concat(new string[] {
            randomizerMod.path,
            Path.DirectorySeparatorChar.ToString(),
            "World"
        });
        try
        {
            Directory.Delete(worldPath, true);
            Directory.CreateDirectory(worldPath);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }

        try 
        {
            //delete randomizer file
            File.Delete(string.Concat(new string[] {
                randomizerMod.path,
                Path.DirectorySeparatorChar.ToString(),
                "World",
                Path.DirectorySeparatorChar.ToString(),
                "RegionRandomizer.txt"
            }));
        }
        catch (Exception ex) {
            Logger.LogError(ex);
        }
    }


    public void PreviousVersionFixes()
    {
        //find region randomizer mod
        ModManager.Mod randomizerMod = null;
        ModManager.ActiveMods.ForEach(mod =>
        {
            if (mod.id == "LazyCowboy.RegionRandomizer")
                randomizerMod = mod;
        });
        if (randomizerMod == null)
            return;


        //move all randomizer files from old location to new location
        string newRandomizerFolder = AssetManager.ResolveDirectory("RegionRandomizer");
        try
        {
            if (!Directory.Exists(newRandomizerFolder))
            {
                Directory.CreateDirectory(newRandomizerFolder);
                newRandomizerFolder = AssetManager.ResolveDirectory("RegionRandomizer");
            }
        }
        catch (Exception ex) { }
        string newRandomizerDirectory = newRandomizerFolder + Path.DirectorySeparatorChar.ToString();

        string oldRandomizerFolder = string.Concat(new string[] {
            randomizerMod.path,
            Path.DirectorySeparatorChar.ToString(),
            "World"
        });
        if (Directory.Exists(oldRandomizerFolder))
        {
            string[] oldFiles = Directory.GetFiles(oldRandomizerFolder);
            foreach (string f in oldFiles)
            {
                try
                {
                    string[] s = f.Split(Path.DirectorySeparatorChar);
                    File.Copy(f, newRandomizerDirectory + s[s.Length - 1]);
                }
                catch (Exception ex) { }
            }
            try
            {
                if (Directory.Exists(oldRandomizerFolder))
                    Directory.Delete(oldRandomizerFolder, true);
            }
            catch (Exception ex) { }
        }

        //convert old randomizer file into 9 slugcat-specific copies
        string oldRandomizerFile = newRandomizerDirectory + "RegionRandomizer.txt";

        string[] slugcats = new string[] { "Yellow", "White", "Red", "Gourmand", "Artificer", "Rivulet", "Spear", "Saint", "Inv" };

        if (File.Exists(oldRandomizerFile))
        {
            //copy
            foreach (string s in slugcats)
            {
                try
                {
                    File.Copy(oldRandomizerFile, newRandomizerDirectory + "RegionRandomizer-" + s + ".txt");
                }
                catch (Exception ex)
                {
                    RWCustom.Custom.Logger.Error("RegionRandomizer: " + ex.ToString());
                }
            }

            //delete
            try
            {
                File.Delete(oldRandomizerFile);
            }
            catch (Exception ex)
            {
                RWCustom.Custom.Logger.Error("RegionRandomizer: " + ex.ToString());
            }
        }

        //copy the locks file into 9 slugcat-specific files IF they don't exist
        string oldLocksFile = string.Concat(new string[] {
            randomizerMod.path,
            Path.DirectorySeparatorChar.ToString(),
            "Modify",
            Path.DirectorySeparatorChar.ToString(),
            "World",
            Path.DirectorySeparatorChar.ToString(),
            "Gates",
            Path.DirectorySeparatorChar.ToString(),
            "locks.txt"
        });

        if (File.Exists(oldLocksFile))
        {
            //check if any slugcat-specific locks exist
            bool specificLocks = false;
            foreach (string s in slugcats)
            {
                string newPath = newRandomizerDirectory + "locks-" + s + ".txt";
                if (File.Exists(newPath))
                {
                    specificLocks = true;
                    break;
                }
            }

            if (!specificLocks)
            {
                //copy
                foreach (string s in slugcats)
                {
                    string newPath = newRandomizerDirectory + "locks-" + s + ".txt";
                    try
                    {
                        if (!File.Exists(newPath))
                        {
                            File.Copy(oldLocksFile, newPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        RWCustom.Custom.Logger.Error("RegionRandomizer: " + ex.ToString());
                    }
                }
            }

            //delete
            try
            {
                File.Delete(oldLocksFile);
                File.WriteAllText(oldLocksFile, "[MERGE]\n[END MERGE]");
            }
            catch (Exception ex)
            {
                RWCustom.Custom.Logger.Error("RegionRandomizer: " + ex.ToString());
            }
        }


    }


    public static void LogSomething(object obj)
    {
        Instance.Logger.LogDebug(obj);
    }
}
