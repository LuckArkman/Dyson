using Dtos;
using Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
// Usings do Mongo
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Repositories;
using Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IUserService, UserService>(); 
builder.Services.AddSingleton<ConversationService>();
builder.Services.AddSingleton<CartService>();
builder.Services.AddSingleton<WalletService>(); 
builder.Services.AddHttpClient<ThirdwebApiService>();
builder.Services.AddSingleton<RewardContractService>();
builder.Services.AddSingleton<ContractDeploymentService>();
builder.Services.AddHttpClient<IPaymentGateway, MercadoPagoService>();
builder.Services.AddSingleton(typeof(IRepositorio<>), typeof(Repositorio<>));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Define o caminho para onde o usuário será redirecionado
        // se tentar acessar uma página protegida sem estar autenticado.
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(1);
    });

// Adiciona o serviço de usuários (SIMULADO - Substituir por MongoDB em produção)

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();