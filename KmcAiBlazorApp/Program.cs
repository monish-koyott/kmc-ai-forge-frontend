using KmcAiBlazorApp.Components;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri("http://localhost:5001/") });

// Add HttpClient for API calls with proper configuration
builder.Services.AddHttpClient("BackendAPI", client =>
{
    var configuration = builder.Configuration;
    var backendApiBaseUrl = configuration["BackendAPI:BaseUrl"] ?? "http://localhost:5000/";
    var backendApiTimeout = configuration.GetValue("BackendAPI:TimeoutSeconds", 300); // default 5 minutes

    client.BaseAddress = new Uri(backendApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(backendApiTimeout);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Add SignalR service


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

// Configure the application to use port 8000
app.Urls.Clear();
app.Urls.Add("http://localhost:8000");

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
