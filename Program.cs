using U360Prospect.Data;
using U360Prospect.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<OracleConnectionFactory>();

builder.Services.AddScoped<ProspectRepository>();

// Add MVC
builder.Services.AddControllersWithViews();

// Windows Authentication
builder.Services.AddAuthentication(
    Microsoft.AspNetCore.Server.IISIntegration.IISDefaults.AuthenticationScheme);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

// Authentication must come before Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
