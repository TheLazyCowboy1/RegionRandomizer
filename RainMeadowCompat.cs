using RainMeadow;
using System;
using System.Linq;

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
    public static object onlineData = null;

    public static void AddOnlineData()
    {
        if (!IsOnline) return;
		onlineData ??= new RandomizerData();
		OnlineManager.lobby.AddData(onlineData as RandomizerData);
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
			public override Type GetDataType() => GetType();

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
				RegionRandomizer.CustomGateLocks = CustomGateLocksKeys.Zip(CustomGateLocksValues, (k, v) => (k, v)).ToDictionary(x => x.k, x => x.v);
				RegionRandomizer.GateNames = GateNames;
				RegionRandomizer.NewGates1 = NewGates1;
				RegionRandomizer.NewGates2 = NewGates2;
                RegionRandomizer.LogSomething("Read state. Lengths: " + CustomGateLocksKeys.Length + ", " + NewGates1.Length);
            }
		}
	}
}
