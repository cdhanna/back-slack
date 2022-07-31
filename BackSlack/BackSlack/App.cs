using System.Globalization;
using Newtonsoft.Json;
using SlackNet;
using SlackNet.Events;
using SlackNet.WebApi;

namespace BackSlack;

public class App
{

    public async Task Run()
    {
//
        var key = Environment.GetEnvironmentVariable("BACK_SLACK_AUTH") ??
                  throw new Exception("Must provide env var for BACK_SLACK_AUTH");
        
        var api = new SlackServiceBuilder() // TODO: inject.
            .UseApiToken(key)
            .GetApiClient();

        var data = new AppData();
        
        var rs = await api.Conversations.List();
        foreach (var channel in rs.Channels)
        {
            if (!channel.IsMember) continue;
            var archive = new ChannelArchive();
            if (channel.Name != "dev") continue;
            Console.WriteLine(channel.Name);

            // continue;
           
            var history = api.Conversations.GetAllMessages(channel.Id, max:100);

            var users = new Dictionary<string, User>();
            string? fromTs = null;
            string? toTs = null;
            await foreach (var msg in history)
            {
                fromTs ??= msg.Ts;
                toTs = msg.Ts;
                
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
                
                archive.messages.Add(new MessageArchive
                {
                    text = msg.Text,
                    ts = msg.Ts,
                    userId = msg.User
                });
            }

            archive.fromTs = fromTs;
            archive.toTs = toTs;
            archive.channelId = channel.Id;
            archive.channelName = channel.Name;
            var archiveJson = JsonConvert.SerializeObject(archive);

            
            
            // await api.Chat.PostMessage(new Message
            // {
            //     Channel = channel.Id,
            //     Text = $"Saved {archive.messages.Count} messages over the ~{(fromTs.ParseTs() - toTs.ParseTs()).TotalDays.ToString("N")} day period between {fromTs.ParseTs().ToString(CultureInfo.InvariantCulture)} and {toTs.ParseTs().ToString(CultureInfo.InvariantCulture)}..."
            // });
            
            Console.WriteLine(archiveJson);
        }
    }
}

public class AppData
{
    public List<ChannelData> channels;
}

public class ChannelData
{
    public string channelName;
    public string channelId;
    public string latestTs;
    public long messageCount;
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
    
    public static async IAsyncEnumerable<MessageEvent> GetAllMessages(this IConversationsApi api, string channelId, string? fromTs=null, int batchSize = 100, int? max=null)
    {
        var more = true;
        string? cursor = null;
        long total = 0;
        while (more)
        {
            var history = await api.History(channelId, oldestTs:fromTs, limit: batchSize, cursor: cursor);
            
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