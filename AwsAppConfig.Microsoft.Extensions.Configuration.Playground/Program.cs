using AwsAppConfig.Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Configuration.AddAwsAppConfig();

var app = builder.Build();



// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.MapGet("/appconfig", (IConfiguration config) =>
{
    var a = config.GetSection("FromAws").Value;
    return a;
});

app.Run();