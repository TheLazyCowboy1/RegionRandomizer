using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using RainMeadow;
using System;
using System.Linq;
using System.Reflection;

namespace RegionRandomizer;

/**
 * Original file by Choc
 * Significantly modified to support soft-compatibility
 */
internal partial class RainMeadowCompat
{
    //public static bool meadowEnabled = false;
    public static bool IsOnline => OnlineManager.lobby != null;
    public static bool IsHost => OnlineManager.lobby.isOwner;

	//public static RandomizerData onlineData = new();
	//public static object onlineData = null;
	public static bool onlineDataAdded = false;

	public static void InitCompat()
	{
        //thanks Forthfora! https://github.com/forthfora/pearlcat/blob/1eea5c439cef8465e0639b8cecf63c9586bbf485/src/Scripts/ModCompat/RainMeadow/MeadowCompat.cs
        try
        {
            _ = new Hook(
                typeof(OnlineResource).GetMethod("Available", BindingFlags.Instance | BindingFlags.NonPublic),
                typeof(RainMeadowCompat).GetMethod(nameof(OnLobbyAvailable), BindingFlags.Static | BindingFlags.NonPublic)
            );
            RegionRandomizer.LogSomething("Added Lobby hook!");

            // use this event instead when it's been pushed
            // Lobby.ResourceAvailable
        }
        catch (Exception ex)
        {
            RegionRandomizer.LogSomething(ex);
        }
    }

    //thanks again Forthfora!! https://github.com/forthfora/pearlcat/blob/1eea5c439cef8465e0639b8cecf63c9586bbf485/src/Scripts/ModCompat/RainMeadow/MeadowCompat.cs
    private delegate void orig_OnLobbyAvailable(OnlineResource self);
    private static void OnLobbyAvailable(orig_OnLobbyAvailable orig, OnlineResource self)
    {
        orig(self);

		if (onlineDataAdded) return;

        //onlineData ??= new RandomizerData();

        self.AddData(new RandomizerData());

		RegionRandomizer.LogSomething("Added online data!");

		onlineDataAdded = true;
    }

    public static void AddOnlineData()
    {
        if (!IsOnline) return;
		//onlineData ??= new RandomizerData();
		//OnlineManager.lobby.AddData(onlineData as RandomizerData);
		//OnlineManager.lobby.AddData(new RandomizerData());
        RegionRandomizer.LogSomething("Added online data");
    }

    public class RandomizerData : OnlineResource.ResourceData
	{
		public RandomizerData() { }

		public override ResourceDataState MakeState(OnlineResource resource)
		{
			return new State(this);
		}

		private class State : ResourceDataState
		{
			public override Type GetDataType() => typeof(RandomizerData);

			//[OnlineField(nullable = true)]
			//Dictionary<string, string> CustomGateLocks;
			[OnlineField]
			string[] CustomGateLocksKeys = new string[0];
			[OnlineField]
			string[] CustomGateLocksValues = new string[0];
			[OnlineField]
			string[] GateNames = new string[0];
			[OnlineField]
			string[] NewGates1 = new string[0];
			[OnlineField]
			string[] NewGates2 = new string[0];

			public State() { }
			public State(RandomizerData data)
			{
				CustomGateLocksKeys = RegionRandomizer.CustomGateLocks.Keys.ToArray();
				CustomGateLocksValues = RegionRandomizer.CustomGateLocks.Values.ToArray();
				GateNames = RegionRandomizer.GateNames;
				NewGates1 = RegionRandomizer.NewGates1;
				NewGates2 = RegionRandomizer.NewGates2;
				RegionRandomizer.LogSomething("Added state. Lengths: " + CustomGateLocksKeys.Length + ", " + NewGates1.Length);
			}

			public override void ReadTo(OnlineResource.ResourceData data, OnlineResource resource)
			{
				try
				{
					RegionRandomizer.CustomGateLocks = CustomGateLocksKeys.Zip(CustomGateLocksValues, (k, v) => (k, v)).ToDictionary(x => x.k, x => x.v);
					RegionRandomizer.GateNames = GateNames;
					RegionRandomizer.NewGates1 = NewGates1;
					RegionRandomizer.NewGates2 = NewGates2;
					RegionRandomizer.LogSomething("Read state. Lengths: " + CustomGateLocksKeys.Length + ", " + NewGates1.Length);
				} catch (Exception ex) { RegionRandomizer.LogSomething(ex); }
            }
		}
	}
}
