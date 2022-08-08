namespace BackSlack;

public class ConfigService
{
    public string SlackAuthKey => string.IsNullOrEmpty(_argSlackKey)
        ? (Environment.GetEnvironmentVariable("BACK_SLACK_AUTH") ??
           throw new Exception("Must provide env var for BACK_SLACK_AUTH"))
        : _argSlackKey;

    public string StorageBasePath => "D:\\proj\\back-slack\\BackSlack\\data";
    public string StorageFilePath => Path.Combine(StorageBasePath, "storage.json");

    private readonly string? _argSlackKey;
    
    public ConfigService(string[] args)
    {
        // first arg is always the key, if it exists...
        if (args.Length > 0)
        {
            _argSlackKey = args[0];
        }
    }
}