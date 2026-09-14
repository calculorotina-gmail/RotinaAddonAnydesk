using AnyDeskMonitor.Infrastructure;
using AnyDeskMonitor.Infrastructure.Data;
using AnyDeskMonitor.Web.Components;
using AnyDeskMonitor.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Enable running as Windows Service
builder.Host.UseWindowsService();

// Add Infrastructure and Application Services
builder.Services.AddInfrastructureAndApplication(builder.Configuration);

// Add Blazor Server interactive services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add HttpClient and WebApiClient
builder.Services.AddHttpClient<WebApiClient>();

var app = builder.Build();

// Auto Initialize Database and Seed Data on Startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        DbInitializer.Initialize(db);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Erro ao inicializar base de dados no painel Web.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
