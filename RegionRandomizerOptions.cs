using BepInEx.Logging;
using Menu.Remix.MixedUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RegionRandomizer;

public class RegionRandomizerOptions : OptionInterface
{
    private readonly ManualLogSource Logger;
    private RegionRandomizer randomizerMod;

    public RegionRandomizerOptions(RegionRandomizer modInstance, ManualLogSource loggerSource)
    {
        randomizerMod = modInstance;
        Logger = loggerSource;
        //PlayerSpeed = this.config.Bind<float>("PlayerSpeed", 1f, new ConfigAcceptableRange<float>(0f, 100f));
        slugcatChoice = this.config.Bind<string>("slugcatChoice", "Survivor", new ConfigAcceptableList<string>(new string[] {"Monk", "Survivor", "Hunter", "Gourmand", "Artificer", "Rivulet", "Spearmaster", "Saint", "Inv", "Modded"}));
        customSlugcat = this.config.Bind<string>("customSlugcat", "");
        startingKarma = this.config.Bind<int>("startingKarma", 5, new ConfigAcceptableRange<int>(1, 10));
        startingRegion = this.config.Bind<string>("startingRegion", "SU");
        alterGateLocks = this.config.Bind<bool>("alterGateLocks", true);
        karmaPerEcho = this.config.Bind<string>("karmaPerEcho", "-1");
        randomKarmaLeniency = this.config.Bind<string>("randomKarmaLeniency", "3.0");
        gateKarmaPenalty = this.config.Bind<int>("gateKarmaPenalty", 0, new ConfigAcceptableRange<int>(0, 22));
        sensibleRegionPlacements = this.config.Bind<bool>("sensibleRegionPlacements", true);
        sensiblePlacementRandomness = this.config.Bind<float>("sensiblePlacementRandomness", 0.3f, new ConfigAcceptableRange<float>(0f, 4f));
        forbidDefaultGates = this.config.Bind<bool>("forbidDefaultGates", true);
        //keyRegions = this.config.Bind<string>("keyRegions", "SS,SB");
        minSteps = this.config.Bind<string>("minSteps", "SS:3,SB:2,DM:3,CW:3,FR:2");
        presetLocks = this.config.Bind<string>("presetLocks", "SS:m,SB:c,DM:m,CW:m,FR:c");

        //BindRegionsTab();

        //forbidDefaultGates.Value = true;
        keyRegions.Value = "";
    }

    public readonly Configurable<float> PlayerSpeed;
    private UIelement[] UIArr;
    private UIelement[] RegArr = new UIelement[0];

    //private UIelement[] UIArrRandomizerOptions;

    public string Slugcat = "White";
    public bool Starts_Mark = false;
    public string World_State = "White";

    public Configurable<string> slugcatChoice = new ("Survivor");
    public Configurable<string> customSlugcat = new ("");
    public Configurable<int> startingKarma = new (5);
    public Configurable<string> startingRegion = new ("SU");

    public Configurable<bool> alterGateLocks;
    public Configurable<string> karmaPerEcho = new ("-1");
    public Configurable<string> randomKarmaLeniency = new ("3.0");
    public Configurable<int> gateKarmaPenalty = new(0);

    public Configurable<bool> sensibleRegionPlacements;
    public Configurable<float> sensiblePlacementRandomness;

    public Configurable<bool> forbidDefaultGates = new (true);
    public Configurable<string> keyRegions = new ("");
    public Configurable<string> minSteps = new ("SS:3,SB:2,DM:3,CW:3,FR:2");
    public Configurable<string> presetLocks = new ("SS:m,SB:c,DM:m,CW:m,FR:c");

    public static readonly List<string> ECHO_REGIONS = new List<string> { "SH", "UW", "CC", "SI", "LF", "SB", "LC" };

    public Dictionary<string, Configurable<bool>> blacklistedRegions = new();
    public Dictionary<string, Configurable<bool>> ignoredRegions = new();


    public override void Initialize()
    {

        var opTab = new OpTab(this, "Options");
        var modsTab = new OpTab(this, "Mod List");
        var regionsTab = new OpTab(this, "Regions");
        this.Tabs = new[]
        {
            opTab,
            regionsTab,
            modsTab
        };

        OpSimpleButton randomizeButton = new OpSimpleButton(new Vector2(10f, 0f), new Vector2(150f, 50f), "RANDOMIZE REGIONS") { description = "Obselete method of generating the randomizer files. This is now done automatically."};
        OpSimpleButton clearButton = new OpSimpleButton(new Vector2(200f, 0f), new Vector2(150f, 50f), "CLEAR RANDOMIZER") { description = "Clears all randomizer files, resulting in all campaigns being re-randomized."};
        OpLabel debugLabel = new OpLabel(10f, 55f, "");

        float g = -30f; //g = "gap" = vertical spacing
        //float h = 550f + 3.5f * gap;
        float h = 550f, s = 100f, w = 80f, l = 10f; //h = current height, s = horizontal spacing, w = width of configs, l = left margin

        UIArr = new UIelement[]
        {
            new OpLabel(l, h, "Randomizer Options", true), //0
            new OpLabel(s, h += g, "Alter Gate Locks"), new OpCheckBox(alterGateLocks, l, h) { description = "Unchecking this option will keep all karma gate locks unchanged. Unchecking this will disable the \"game mode\" aspect of finding enough echoes to reach an iterator.\nWARNING: Might make Artificer's or Saint's campaign impossible to complete."}, //8-9
            new OpLabel(s, h += g, "Karma per Echo"), new OpTextBox(karmaPerEcho, new Vector2(l, h), w) { description = "Percentage of echoes that one must obtain to progress. Ranges from 0.0 to 1.0. Will be set automatically if outside range."}, //1-2
            new OpLabel(s, h += g, "Karma Leniency"), new OpTextBox(randomKarmaLeniency, new Vector2(l, h), w) { description = "The maximum amount karma gate requirements can be randomly lowered. Ranges from 0.0 to 10.0."}, //3-4
            new OpLabel(s, h += g, "Gate Karma Penalty"), new OpUpdown(true, gateKarmaPenalty, new Vector2(l, h), w) { description = "How much karma should be taken away from the player when passing through a karma gate. Discourages quick region-hopping. Ranges from 0 to 22."}, //5-6

            new OpLabel(s, h += 2*g, "Sensible Region Placements"), new OpCheckBox(sensibleRegionPlacements, l, h) { description = "Attempts to place regions in a somewhat-sensible network, instead of making totally random connections.\n(Hopefully, starting in Outskirts and going left, up, right, down will bring you back near to Outskirts.)"}, //12-3
            new OpLabel(s, h += g, "Placement Randomness"), new OpUpdown(sensiblePlacementRandomness, new Vector2(l, h), w) { description = "How much the randomizer should prioritize \"randomness\" over sensible connections.\n(0 will result in nearly identical (but hopefully sensible) region placements each time; 0.5 compromises; 1 is very random.)" }, //14-5

            new OpLabel(s, h += 2*g, "Advanced Elements:", true), //7
            new OpLabel(s, h += g, "Forbid Default Gates"), new OpCheckBox(forbidDefaultGates, l, h) { description = "It is recommended to keep this option enabled for stability; however, this option does not effect the randomizer whatsoever if sensible room placements is enabled."} //8-9
            //new OpLabel(10f, h += gap, "Min Steps") { Hidden = true}, new OpTextBox(minSteps, new Vector2(150f, h+10*gap), 300f) { description = "A list of region-value pairs in which the number represents how \"far\" (in gates) each region must be from the starting region.", Hidden = true} //10-1

            
            /*
            new OpLabel(10f, 550f+gap, "Slugcat: "), //1 //new OpComboBox(slugcatChoice, new Vector2(150f, 550f+gap), 150f, new string[]
            //slugcatSelector, //3
            //slugcatNames[0], slugcatNames[1], slugcatNames[2], slugcatNames[3], slugcatNames[4], slugcatNames[5], slugcatNames[6], slugcatNames[7], slugcatNames[8], slugcatNames[9], //3-12
            new OpLabel(10f, 550f+2*gap, "Modcat Name: "), new OpTextBox(customSlugcat, new Vector2(150f, 550f+2*gap), 150f) { description = "Obselete method of allowing for modded slugcats. Now done automatically upon starting a campaign."}, //2-3
            new OpLabel(10f, h, "Starting Karma"), new OpSliderTick(startingKarma, new Vector2(150f, h), 200){min=1,max=10, description = "How much max karma the slugcat starts with. Ranges from 1 to 10."}, //4-5
            new OpLabel(10f, h+gap, "Starting Region"), new OpTextBox(startingRegion, new Vector2(150f, h+gap), 50f) { description = "Obselete. Filled automatically."}, //6-7

            new OpLabel(10f, h+3*gap, "Karma per Echo"), new OpTextBox(karmaPerEcho, new Vector2(150f, h+3*gap), 50f) { description = "Percentage of echoes that one must obtain to progress. Ranges from 0.0 to 1.0. Will be set automatically if outside range."}, //8-9
            new OpLabel(10f, h+4*gap, "Karma Leniency"), new OpTextBox(randomKarmaLeniency, new Vector2(150f, h+4*gap), 50f) { description = "The maximum amount karma gate requirements can be randomly lowered. Ranges from 0.0 to 10.0."}, //10-1

            new OpLabel(10f, h+6*gap, "Advanced Elements:"), //12
            new OpLabel(10f, h+7*gap, "Forbid Default Gates"), new OpCheckBox(forbidDefaultGates, 150f, h+7*gap) { description = "Please keep this box checked. Otherwise the randomizer logic sometimes breaks."}, //13-4
            new OpLabel(10f, h+8*gap, "Key Regions"), new OpTextBox(keyRegions, new Vector2(150f, h+8*gap), 200f) { description = "A list of uppercase region acronyms, separated by commas, that the randomizer ensures it always includes access to."}, //15-6
            new OpLabel(10f, h+9*gap, "Min Steps"), new OpTextBox(minSteps, new Vector2(150f, h+9*gap), 300f) { description = "A list of region-value pairs in which the number represents how \"far\" each region must be from the starting region."}, //17-8
            new OpLabel(10f, h+10*gap, "Preset Locks"), new OpTextBox(presetLocks, new Vector2(150f, h+10*gap), 400f) { description = "A list of region-value pairs for specific requirements to access each region. Overrides the standard randomizer logic."}, //19-20

            new OpComboBox(slugcatChoice, new Vector2(300f, 550f+gap), 150f, new string[]
            {
                "Monk", "Survivor", "Hunter", "Gourmand", "Artificer", "Rivulet", "Spearmaster", "Saint", "Inv", "Modded"
            }) { description = "Obselete method of choosing the slugcat to play. Each slugcat now has its own settings generated and stored individually and automatically."}, //21
            debugLabel, //22
            randomizeButton, //23
            clearButton, //24

            new OpLabel(10f, h+5*gap, "Gate Karma Penalty"), new OpUpdown(true, gateKarmaPenalty, new Vector2(150f, h+5*gap), 70f) { description = "How much karma should be taken away from the player when passing through a karma gate. Discourages quick region-hopping. Ranges from 0 to 22."}, //25-6
            */
        };
        opTab.AddItems(UIArr);

        //mods tab

        try
        {
            OpScrollBox box = new OpScrollBox(modsTab, 3000f);
            box.AddItems(new OpLabelLong(new Vector2(10f, 0f), new Vector2(500f, 3000f), File.ReadAllText(AssetManager.ResolveFilePath("RegionRandomizer" + Path.DirectorySeparatorChar + "RegionRandomizerModList.txt"))));
            //modsTab.AddItems(box);
            //modsTab.AddItems(new OpLabel(10f, 0f, File.ReadAllText(AssetManager.ResolveFilePath("World" + Path.DirectorySeparatorChar + "RegionRandomizerModList.txt"))));
        } catch (Exception ex) { }

        //regions tab
        try
        {
            string[] regions = File.ReadAllLines(AssetManager.ResolveFilePath("World" + Path.DirectorySeparatorChar + "Regions.txt"));
            RegArr = new UIelement[regions.Length * 3 + 5];
            RegArr[0] = new OpLabel(-5f, 580f, "Region Options", true);
            RegArr[1] = new OpLabel(150f, 580f, "Forbid", false);
            RegArr[2] = new OpLabel(200f, 580f, "Ignore", false);
            RegArr[3] = new OpLabel(400f, 580f, "Forbid", false);
            RegArr[4] = new OpLabel(450f, 580f, "Ignore", false);

            g = -25f; //gap/y-space between regions

            for (int i = 0; i < regions.Length; i++) {
                string r = regions[i];
                string displayString = r;
                try
                {
                    displayString += " - " + File.ReadAllText(AssetManager.ResolveFilePath("World" + Path.DirectorySeparatorChar + r + Path.DirectorySeparatorChar + "displayname.txt"));
                } catch (Exception ex) { }
                int idx = i * 3 + 5;
                float y = 550f + g * ((i >= 23) ? i-23 : i);
                float dx = (i >= 23) ? 250 : 0;
                RegArr[idx] = new OpLabel(0f+dx, y, displayString);

                try
                {
                    //blacklistedRegions.Add(r, this.config.Bind<bool>("blacklistRegion" + r, false));
                    RegArr[idx + 1] = new OpCheckBox(blacklistedRegions[r], 150f+dx, y) { description = "Check this if you want the region to never be entered. If you don't like a region, check this box." };
                } catch (Exception ex) { }
                try
                {
                    //ignoredRegions.Add(r, this.config.Bind<bool>("ignoreRegion" + r, false));
                    RegArr[idx + 2] = new OpCheckBox(ignoredRegions[r], 200f+dx, y) { description = "Check this if you want the randomizer to ignore this region. Its gates will instead lead to the same places they normally do." };
                }
                catch (Exception ex) { }
            }
        } catch (Exception ex) { Logger.LogError(ex); }
        regionsTab.AddItems(RegArr);
    }

    public void BindRegionsTab()
    {
        //regions tab
        try
        {
            string[] regions = File.ReadAllLines(AssetManager.ResolveFilePath("World" + Path.DirectorySeparatorChar + "Regions.txt"));
            for (int i = 0; i < regions.Length; i++)
            {
                string r = regions[i];
                try
                {
                    blacklistedRegions.Add(r, this.config.Bind<bool>("blacklistRegion" + r, false));
                }
                catch (Exception ex) { }
                try
                {
                    ignoredRegions.Add(r, this.config.Bind<bool>("ignoreRegion" + r, false));
                }
                catch (Exception ex) { }
            }
        }
        catch (Exception ex) { Logger.LogError(ex); }
    }

    public void UpdateSlugcat()
    {
        /*
        if (((OpUpdown)UIArrPlayerOptions[2]).GetValueFloat() > 10)
        {
            ((OpLabel)UIArrPlayerOptions[3]).Show();
        }
        else
        {
            ((OpLabel)UIArrPlayerOptions[3]).Hide();
        }
        */
        //change slugcat-related settings if choosing a new slugcat

        //OpRadioButtonGroup group = (OpRadioButtonGroup)UIArr[2];
        //switch (group.GetValueInt())
        Logger.LogDebug("Slugcat: " + ((OpComboBox)UIArr[21]).value);
        switch (((OpComboBox)UIArr[21]).value)
        {
            case "Monk":
                Slugcat = "Yellow";
                startingKarma.Value = 5;
                startingRegion.Value = "SU";
                break;
            case "Survivor":
                Slugcat = "White";
                startingKarma.Value = 5;
                startingRegion.Value = "SU";
                break;
            case "Hunter":
                Slugcat = "Red";
                startingKarma.Value = 5;
                startingRegion.Value = "LF";
                break;
            case "Gourmand":
                Slugcat = "Gourmand";
                startingKarma.Value = 5;
                startingRegion.Value = "SH";
                break;
            case "Artificer":
                Slugcat = "Artificer";
                startingKarma.Value = 2;
                startingRegion.Value = "GW";
                break;
            case "Rivulet":
                Slugcat = "Rivulet";
                startingKarma.Value = 5;
                startingRegion.Value = "DS";
                break;
            case "Spearmaster":
                Slugcat = "Spear";
                startingKarma.Value = 5;
                startingRegion.Value = "SU";
                break;
            case "Saint":
                Slugcat = "Saint";
                startingKarma.Value = 2;
                startingRegion.Value = "SI";
                break;
            case "Inv":
                Slugcat = "Inv";
                startingKarma.Value = 5;
                startingRegion.Value = "SH";
                break;
            default:
                Slugcat = customSlugcat.Value;
                break;
        }

        ((OpSliderTick)UIArr[5]).value = startingKarma.Value.ToString();
        ((OpTextBox)UIArr[7]).value = startingRegion.Value;
        UIArr[5].Update();
        UIArr[7].Update();

        //preset locks update
        switch (((OpComboBox)UIArr[21]).value)
        {
            case "Rivulet":
            case "Saint":
                ((OpTextBox)UIArr[20]).value = "DM:m,CW:m";
                break;
            case "Spear":
            case "Inv":
                ((OpTextBox)UIArr[20]).value = "SB:c,DM:m,CW:m,FR:c";
                break;
            default:
                ((OpTextBox)UIArr[20]).value = "SS:m,SB:c,DM:m,CW:m,FR:c";
                break;
        }
        ((OpTextBox)UIArr[20]).Update();

    }

    public void UpdateForSlugcat(string slugcat, bool expedition = false)
    {
        Starts_Mark = false;
        switch (slugcat)
        {
            case "Yellow":
                startingKarma.Value = 5;
                startingRegion.Value = "SU";
                break;
            case "White":
                startingKarma.Value = 5;
                startingRegion.Value = "SU";
                break;
            case "Red":
                startingKarma.Value = 5;
                startingRegion.Value = "LF";
                Starts_Mark = true;
                break;
            case "Gourmand":
                startingKarma.Value = 5;
                startingRegion.Value = "SH";
                break;
            case "Artificer":
                startingKarma.Value = 2;
                startingRegion.Value = "GW";
                break;
            case "Rivulet":
                startingKarma.Value = 5;
                startingRegion.Value = "DS";
                Starts_Mark = true;
                break;
            case "Spear":
                startingKarma.Value = 5;
                startingRegion.Value = "SU";
                break;
            case "Saint":
                startingKarma.Value = 2;
                startingRegion.Value = "SI";
                Starts_Mark = true;
                break;
            case "Inv":
                startingKarma.Value = 5;
                startingRegion.Value = "SH";
                Starts_Mark = true;
                break;
            default:
                break;
        }

        //preset locks update
        switch (slugcat)
        {
            case "Rivulet":
            case "Saint":
            case "Inv":
            case "Spear":
                presetLocks.Value = "SB:c,DM:m,CW:m,FR:c";
                break;
            default:
                presetLocks.Value = "SS:m,SB:c,DM:m,CW:m,FR:c";
                break;
        }

        World_State = Slugcat;

        //try to find info if custom slugcat
        TryParseCustomSlugcat(slugcat);

        //expedition logic
        if (expedition)
        {
            Starts_Mark = true;
            startingKarma.Value = 5;
            karmaPerEcho.Value = "0.0";
            presetLocks.Value = "";
        }

        //automatic karma per echo assignment
        if (!Double.TryParse(karmaPerEcho.Value, out double kpe) || kpe < 0.0 || kpe > 1.0)
        {
            //karmaPerEcho.Value = Math.Min(1.0, Math.Pow(7.0 / RegionRandomizer.GetEchoRegions(RegionRandomizer.GetRegions(), slugcat).Count, 0.75)).ToString();
            karmaPerEcho.Value = Math.Min(1.0, Math.Pow(
                (0.8 * (RegionRandomizer.KarmaCap - 10) + 10 - startingKarma.Value) 
                / RegionRandomizer.GetEchoRegions(RegionRandomizer.GetRegions(), slugcat).Count
                , 0.6)).ToString();
        }

    }

    private void TryParseCustomSlugcat(string slugcat)
    {
        try
        {
            string file = AssetManager.ResolveFilePath(String.Concat(new string[]
            {
            "slugbase",
            Path.DirectorySeparatorChar.ToString(),
            slugcat,
            ".json"
            }));
            if (File.Exists(file))
            {
                Logger.LogDebug("Found custom slugcat data: " + file);
                string text = File.ReadAllText(file);
                string[] lines = Regex.Split(text, ",");
                foreach (string line in lines)
                {
                    string l = line.Trim();

                    if (l.StartsWith("\"start_room\"")) //start room
                    {
                        string[] d = Regex.Split(l, ":");
                        if (d.Length < 2)
                            continue;
                        if (d[1].Contains("_"))
                        {
                            int idx = d[1].IndexOf('_');
                            if (idx >= 2)
                            {
                                startingRegion.Value = d[1].Substring(idx - 2, 2).ToUpper();
                                Logger.LogDebug("Found custom startingRegion: " + startingRegion.Value);
                            }
                        }
                    }

                    else if (l.StartsWith("\"karma_cap\"")) //karma cap
                    {
                        string[] d = Regex.Split(l, ":");
                        if (d.Length < 2)
                            continue;
                        if (Int32.TryParse(d[1].Trim(), out int karma))
                        {
                            startingKarma.Value = karma + 1;
                            Logger.LogDebug("Found custom karmaCap: " + startingKarma.Value);
                        }
                    }

                    else if (l.StartsWith("\"has_mark\"")) //starts with mark
                    {
                        string[] d = Regex.Split(l, ":");
                        if (d.Length < 2)
                            continue;
                        if (Boolean.TryParse(d[1].Trim(), out bool mark))
                        {
                            Starts_Mark = mark;
                            if (mark)
                                presetLocks.Value = "SB:m,SS:m,DM:m,CW:m,FR:m";
                            Logger.LogDebug("Found custom slugcat mark: " + mark.ToString());
                        }
                    }

                    else if (l.StartsWith("\"world_state\"")) //equivalent world state to copy
                    {
                        string[] d = Regex.Split(l, ":");
                        if (d.Length < 2)
                            continue;
                        if (d[1].Contains("\""))
                        {
                            int idx = d[1].IndexOf("\"");
                            int length = 0;
                            for (int i = idx + 1; i < d[1].Length && d[1][i] != '\"'; i++)
                                length++;
                            if (length > 0)
                            {
                                World_State = d[1].Substring(idx + 1, length);
                                Logger.LogDebug("Found custom world_state: " + World_State);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }

}