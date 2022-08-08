using Newtonsoft.Json;

namespace BackSlack;

public class StorageService
{
    private readonly ConfigService _config;
    public AppData Data;

    public StorageService(ConfigService config)
    {
        _config = config;
        var json = File.Exists(config.StorageFilePath)
            ? File.ReadAllText(config.StorageFilePath)
            : "{ }"; 
        Data = JsonConvert.DeserializeObject<AppData>(json) ?? new AppData();
    }

    public async Task Save()
    {
        var json = JsonConvert.SerializeObject(Data);
        Directory.CreateDirectory(_config.StorageBasePath);
        await File.WriteAllTextAsync(_config.StorageFilePath, json);
    }
    
}

public class AppData
{
    public List<ChannelData> Channels = new List<ChannelData>();

    public ChannelData GetChannel(string channelId)
    {
        var channel = Channels.FirstOrDefault(c => c.ChannelId == channelId);
        if (channel == null)
        {
            channel = new ChannelData { ChannelId = channelId };
            Channels.Add(channel);
        }

        return channel;
    }
}

public class ChannelData
{
    public string ChannelName;
    public string ChannelId;
    public string LatestTs;
    public long MessageCount;
}
