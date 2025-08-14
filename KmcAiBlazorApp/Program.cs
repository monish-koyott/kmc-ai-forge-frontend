using KmcAiBlazorApp.Components;
using KmcAiBlazorApp.Services;
using Microsoft.AspNetCore.Components.Server.Circuits;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        // Configure Blazor Server timeouts for file uploads
        options.DetailedErrors = true;
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(10);
        options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(5);
        options.MaxBufferedUnacknowledgedRenderBatches = 10;
    });

// Add HttpClient for API calls with proper configuration
builder.Services.AddHttpClient("BackendAPI", client =>
{
    var configuration = builder.Configuration;
    var backendApiBaseUrl = configuration["Urls:Backend"] + "/";
    var backendApiTimeout = configuration.GetValue("BackendAPI:TimeoutSeconds", 600); // default 10 minutes

    client.BaseAddress = new Uri(backendApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(backendApiTimeout);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Add SignalR service
builder.Services.AddScoped<SignalRNotificationService>();

builder.Services.AddServerSideBlazor().AddHubOptions(o =>
{
    o.ClientTimeoutInterval = TimeSpan.FromMinutes(3);  // default ~30s
    o.KeepAliveInterval     = TimeSpan.FromSeconds(15); // send pings
    o.MaximumReceiveMessageSize = 200 * 1024 * 1024;     // 200 MB (safe for 5×2MB)
});


// Add CORS to allow cross-origin requests
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
                
                .AllowAnyOrigin()
                .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the application to use port from configuration
app.Urls.Clear();
var frontendUrl = builder.Configuration["Urls:Frontend"] ?? "http://localhost:8000";
app.Urls.Add(frontendUrl);

// Update Kestrel configuration to use the same URL
builder.Configuration["Kestrel:Endpoints:Http:Url"] = frontendUrl;

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Use CORS
app.UseCors("AllowAll");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
