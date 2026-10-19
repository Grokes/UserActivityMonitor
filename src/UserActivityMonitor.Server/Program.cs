using UserActivityMonitor.Server;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<ClientRegistry>();
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new {status = "ok"}));
app.MapClientEndpoints();
app.UseDefaultFiles();
app.UseStaticFiles();
app.Run();
