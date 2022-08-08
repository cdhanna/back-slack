using System.Globalization;
using Newtonsoft.Json;
using SlackNet;
using SlackNet.Events;
using SlackNet.WebApi;

namespace BackSlack;

public class App
{
    private readonly ConfigService _config;
    private readonly StorageService _storage;

    public App(ConfigService config, StorageService storage)
    {
        _config = config;
        _storage = storage;
    }
    
    public async Task Run()
    {
        var api = new SlackServiceBuilder() // TODO: inject.
            .UseApiToken(_config.SlackAuthKey)
            .GetApiClient();

        var data = _storage.Data;
        var rs = await api.Conversations.List();
        foreach (var channel in rs.Channels)
        {
            if (!channel.IsMember) continue;
            var archive = new ChannelArchive();
            // if (channel.Name != "general") continue;
            Console.WriteLine(channel.Name);

            var channelData = data.GetChannel(channel.Id);
            channelData.ChannelName = channel.Name;

            archive.channelId = channel.Id;
            archive.channelName = channel.Name;
            var fromTs = channelData.LatestTs;
            if (string.IsNullOrEmpty(fromTs))
            {
                fromTs = channel.CreatedDate.ToTimestamp();
            }
            archive.fromTs = fromTs;
            string? toTs = null;
            var users = new Dictionary<string, User>();
            var found = 0;
            string latestTs = null;

            async Task ProcessDay()
            {
                // every run, add a day to the latest run time...
                //fromTs = fromTs.ToDateTime()?.AddDays(1).ToTimestamp();

                toTs = fromTs.ToDateTime()?.AddDays(1).ToTimestamp();
                
                Console.WriteLine("Processing day... " + fromTs.ToDateTime()?.ToLongDateString());
                var history = api.Conversations.GetAllMessages(channel.Id, latestTs:toTs, oldestTs:fromTs);

                var periodMessages = new List<MessageArchive>();
                await foreach (var msg in history)
                {
                    // toTs = msg.Ts;
                    latestTs =
                        string.IsNullOrEmpty(latestTs)
                            ? msg.Ts
                            : (msg.Ts.ToDateTime() > latestTs.ToDateTime() ? msg.Ts : latestTs);
                    if (!users.TryGetValue(msg.User, out var user))
                    {
                        user = await api.Users.Info(msg.User);
                        users.Add(msg.User, user);
                        archive.users.Add(new UserArchive
                        {
                            id = user.Id,
                            name = user.RealName
                        });
                    }
                    if (user.IsBot) continue;
                
                    periodMessages.Add(new MessageArchive
                    {
                        text = msg.Text,
                        ts = msg.Ts,
                        userId = msg.User
                    });
                    found++;
                    channelData.MessageCount++;
                }

                periodMessages.Reverse();
                archive.messages.AddRange(periodMessages);

                fromTs = toTs;
                Console.WriteLine($"  found {periodMessages.Count} messages for a total of {found}");
            }

            while (found < 500 && fromTs.ToDateTime() <= DateTime.UtcNow)
            {
                await ProcessDay();
            }

            archive.toTs = latestTs ?? fromTs;
            channelData.LatestTs = archive.toTs;
            Console.WriteLine($"Saving { archive.toTs.ToDateTime()?.ToLongDateString()} to file");
            var archiveJson = JsonConvert.SerializeObject(archive);

            if (found > 0)
            {
                var fileName = $"v1-{channel.Id}-{archive.fromTs.Replace(".", "_")}-{archive.toTs.Replace(".", "_")}";
                fileName = Path.Combine(_config.StorageBasePath, fileName);
                await System.IO.File.WriteAllTextAsync(fileName, archiveJson);

            }

            var output =
              $"Saved {archive.messages.Count} messages over the ~{(archive.toTs.ParseTs() - archive.fromTs.ParseTs()).TotalDays.ToString("N")} day period between {archive.fromTs.ParseTs().ToString(CultureInfo.InvariantCulture)} and {archive.toTs.ParseTs().ToString(CultureInfo.InvariantCulture)}...";

              Console.WriteLine(output);
            // await api.Chat.PostMessage(new Message
            // {
            //     Channel = channel.Id,
            //     Text = $"Saved {archive.messages.Count} messages over the ~{(fromTs.ParseTs() - toTs.ParseTs()).TotalDays.ToString("N")} day period between {fromTs.ParseTs().ToString(CultureInfo.InvariantCulture)} and {toTs.ParseTs().ToString(CultureInfo.InvariantCulture)}..."
            // });
            
            Console.WriteLine(archiveJson);
        }
    }
}



public class ChannelArchive
{
    [JsonProperty("i")]
    public string channelId;

    [JsonProperty("n")]
    public string channelName;
    [JsonProperty("u")]
    public List<UserArchive> users = new List<UserArchive>();
    [JsonProperty("m")]
    public List<MessageArchive> messages = new List<MessageArchive>();

    [JsonProperty("fr")]
    public string fromTs;
    [JsonProperty("to")]
    public string toTs;
}

public class MessageArchive
{
    [JsonProperty("u")]
    public string userId;
    [JsonProperty("m")]
    public string text;
    [JsonProperty("t")]
    public string ts;
}
public class UserArchive
{
    [JsonProperty("n")]
    public string name;
    [JsonProperty("i")]
    public string id;
}

public static class SlackExtensions
{

    public static DateTime ParseTs(this string? ts)
    {
        var dt = DateTime.UnixEpoch;
        var ms = double.Parse(ts);
        dt = dt.AddMilliseconds(ms * 1000);
        return dt;
    }
    
    public static async IAsyncEnumerable<MessageEvent> GetAllMessages(this IConversationsApi api, string channelId, string? oldestTs=null, string? latestTs=null, int batchSize = 100, int? max=null)
    {
        var more = true;
        string? cursor = null;
        long total = 0;
        while (more)
        {
            var history = await api.History(channelId, oldestTs:oldestTs, latestTs:latestTs, limit: batchSize, cursor: cursor, inclusive:false);
            
            foreach (var message in history.Messages)
            {
                yield return message;
                total++;
                if (total >= max)
                {
                    yield break;
                }
            }

            more = history.HasMore;
            if (more)
            {
                cursor = history.ResponseMetadata.NextCursor;
            }
        }



    }

}