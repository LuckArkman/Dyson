using Data;
using Dtos;
using Microsoft.AspNetCore.Authentication.Cookies;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Services;

var builder = WebApplication.CreateBuilder(args);

// Configuração MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));
try 
{
    BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
}
catch (BsonSerializationException)
{
    // Ignora se já estiver registrado (útil para evitar erro em hot-reload)
}
// Registrar Serviços
builder.Services.AddSingleton<AdminDataService>();

// Autenticação
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    });

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Seed Inicial de Admin (Opcional - remova em produção)
using (var scope = app.Services.CreateScope())
{
    var service = scope.ServiceProvider.GetRequiredService<AdminDataService>();
    try {
        // Cria um admin padrão se não houver conexão de banco falhando
         service.CreateAdminAsync("admin", "admin@dyson.ai", "admin123").Wait();
    } catch {}
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();