using Business.Interfaces;
using Business.Services;
using Data;
using Data.Adapters;
using Data.Options;
using Data.Repositories;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using WebUI.Options;
using WebUI.Seeders;
using WebUI.Workers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ReclamacionesDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// reguistro de los servicios usados en la aplicacion.
builder.Services.AddScoped<IClaimRepository, ClaimRepository>();
builder.Services.AddScoped<IRadicacionService, RadicacionService>();
builder.Services.Configure<IaOptions>(builder.Configuration.GetSection(IaOptions.Seccion));
builder.Services.AddHttpClient();
builder.Services.AddScoped<IClassificationService, HttpClassificationService>();
builder.Services.AddScoped<IMotorService, MotorService>();
builder.Services.AddScoped<IComplementacionService, ComplementacionService>();
builder.Services.AddScoped<IPanelService, PanelService>();
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.Seccion));
// trabajo con hilos de informes por telegram
builder.Services.AddHostedService<TelegramWorker>();
builder.Services.AddHostedService<VencimientoWorker>();
// trabajo de envio de correos
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.Seccion));
builder.Services.AddHostedService<CorreoWorker>();

// servicio de autenticacion para el analista
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Cuenta/Login";
        options.AccessDeniedPath = "/Cuenta/AccesoDenegado";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

var app = builder.Build();

// Aplicar la migracion al arrancar si existe la conexion a base dee datos pero no la base de datos del proyecto.
await DbInitializer.SeedAsync(app);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
