using SqlDiagTool.Checks;
using SqlDiagTool.Configuration;
using SqlDiagTool.Runner;
using SqlDiagTool.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SqlDiagOptions>(builder.Configuration.GetSection(SqlDiagOptions.SectionName));
builder.Services.AddSingleton<DatabaseTargetService>();
builder.Services.AddSingleton<DiagnosticsRunner>(sp =>
    new DiagnosticsRunner(CheckRegistry.CreateAll(sp.GetRequiredService<ILoggerFactory>())));
builder.Services.AddMemoryCache();
builder.Services.AddHostedService<DemoProvisionHostedService>();
builder.Services.AddRazorPages();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorPages();
app.MapGet("/", () => Results.Redirect("/diagnostics", permanent: false));
app.MapRazorComponents<SqlDiagTool.Web.Components.App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
