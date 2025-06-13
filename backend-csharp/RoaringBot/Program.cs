var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5075");
var app = builder.Build();

Console.WriteLine("C# backend is running inside Docker!");

app.MapGet("/", () => "Hello World!");

app.Run();
