using Newtonsoft.Json;
using System.Reflection;

namespace VRCFaceTracking.Babble;

public static class BabbleConfig
{
	private const string BabbleConfigFile = "BabbleConfig.json";

	public static Config GetBabbleConfig()
	{
        string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
		string path = Path.Combine(directoryName, BabbleConfigFile);
		string value = File.ReadAllText(path);
		return JsonConvert.DeserializeObject<Config>(value)!;
	}
}
