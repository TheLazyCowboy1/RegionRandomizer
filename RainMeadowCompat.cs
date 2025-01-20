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
internal class RainMeadowCompat
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

		//if (onlineDataAdded) return;

		//onlineData ??= new RandomizerData();
		if (!IsHost) return;

        self.AddData(new RandomizerData());

		RegionRandomizer.LogSomething("Added online data!");

		onlineDataAdded = true;
    }

	public static void SignalUpdateToData()
	{
		try
		{
			OnlineManager.lobby.GetData<RandomizerData>().UpdateData();
		}
		catch (Exception ex) { RegionRandomizer.LogSomething(ex); }
	}

	public static bool IsChanged(string[] newGates)
	{
		if (newGates.Length < 1) //bad info
			return false;
		if (newGates.Length != RegionRandomizer.NewGates1.Length) //different length
			return true;
        for (int i = 0; i < newGates.Length; i++)
        {
            if (newGates[i] != RegionRandomizer.NewGates1[i]) //difference within array
				return true;
        }
		return false;
    }

    public class RandomizerData : OnlineResource.ResourceData
	{
		public RandomizerData() {
			currentState = new State(this);
		}

        string[] CustomGateLocksKeys = new string[0];
        string[] CustomGateLocksValues = new string[0];
        string[] GateNames = new string[0];
        string[] NewGates1 = new string[0];
        string[] NewGates2 = new string[0];
        public void UpdateData()
		{
            CustomGateLocksKeys = RegionRandomizer.CustomGateLocks.Keys.ToArray();
            CustomGateLocksValues = RegionRandomizer.CustomGateLocks.Values.ToArray();
            GateNames = RegionRandomizer.GateNames;
            NewGates1 = RegionRandomizer.NewGates1;
            NewGates2 = RegionRandomizer.NewGates2;
			currentState = new State(this);
            RegionRandomizer.LogSomething("Updated data. Lengths: " + CustomGateLocksKeys.Length + ", " + NewGates1.Length);
        }

		private State currentState;
		public override ResourceDataState MakeState(OnlineResource resource)
		{
			return currentState;
		}

		private class State : ResourceDataState
		{
			public override Type GetDataType() => typeof(RandomizerData);

			//[OnlineField(nullable = true)]
			//Dictionary<string, string> CustomGateLocks;
			[OnlineField]
			string[] CustomGateLocksKeys;
			[OnlineField]
			string[] CustomGateLocksValues;
			[OnlineField]
			string[] GateNames;
			[OnlineField]
			string[] NewGates1;
			[OnlineField]
			string[] NewGates2;
			
			public State() { }
			public State(RandomizerData data) {
				CustomGateLocksKeys = data.CustomGateLocksKeys;
                CustomGateLocksValues = data.CustomGateLocksValues;
                GateNames = data.GateNames;
                NewGates1 = data.NewGates1;
                NewGates2 = data.NewGates2;
                RegionRandomizer.LogSomething("Added state.");
			}

			public override void ReadTo(OnlineResource.ResourceData data, OnlineResource resource)
			{
				try
				{
					//determine if new data to be read
					if (!IsChanged(NewGates1)) return;

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
