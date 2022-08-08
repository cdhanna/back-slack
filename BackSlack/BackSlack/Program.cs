using BackSlack;
using Microsoft.Extensions.DependencyInjection;



var builder = new ServiceCollection();
builder.AddSingleton(_ => new ConfigService(args));
builder.AddSingleton<App>();
builder.AddSingleton<StorageService>();

var container = builder.BuildServiceProvider();

try
{
    var app = container.GetService<App>();
    await app.Run();
}
finally
{
    var storage = container.GetService<StorageService>();
    await storage.Save();
}