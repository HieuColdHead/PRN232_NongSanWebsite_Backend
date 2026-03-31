using System.Text;
using BLL.Services;
using BLL.Services.Interfaces;
using DAL.Data;
using DAL.Repositories;
using DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NongXanhController.BackgroundServices;
using RssFetchingService = BLL.Services.RssFetchingService;

namespace NongXanhController
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            var staffOrigins = builder.Configuration.GetSection("Cors:StaffOrigins").Get<string[]>() ?? [];
            var corsOrigins = configuredOrigins
                .Concat(staffOrigins)
                .Where(origin => !string.IsNullOrWhiteSpace(origin))
                .Select(origin => origin.Trim().TrimEnd('/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (corsOrigins.Length == 0)
            {
                corsOrigins = ["http://localhost:3000", "http://localhost:5173", "http://localhost:5174"];
            }

            // Add services to the container.

            // Cấu hình DbContext
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));

            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IEmailOtpRepository, EmailOtpRepository>();

            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IProviderService, ProviderService>();
            builder.Services.AddScoped<ICategoryService, CategoryService>();
            builder.Services.AddScoped<IProductService, ProductService>();
            builder.Services.AddScoped<IProductVariantService, ProductVariantService>();

            builder.Services.AddScoped<IOrderService, OrderService>();
            builder.Services.AddScoped<IReviewService, ReviewService>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddScoped<ICartService, CartService>();
            builder.Services.AddScoped<IVoucherService, VoucherService>();
            builder.Services.AddScoped<IPaymentService, PaymentService>();
            builder.Services.AddScoped<IGhnService, GhnService>();
            builder.Services.AddScoped<IShipmentService, ShipmentService>();
            builder.Services.AddScoped<IBlogService, BlogService>();
            builder.Services.AddScoped<IArticleRssService, ArticleRssService>();
            builder.Services.AddScoped<IWishlistService, WishlistService>();
            builder.Services.AddScoped<IChatService, ChatService>();
            builder.Services.AddScoped<IAiChatService, AiChatService>();
            builder.Services.AddScoped<IMealComboSuggestionService, MealComboSuggestionService>();
            builder.Services.AddScoped<IMealComboService, MealComboService>();
            builder.Services.AddScoped<IRecipeService, RecipeService>();
            builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();

            builder.Services.AddScoped<ITokenService, TokenService>();
            builder.Services.AddScoped<IGoogleOAuthService, GoogleOAuthService>();
            builder.Services.AddScoped<IEmailSender, EmailSender>();
            builder.Services.AddScoped<IEmailOtpService, EmailOtpService>();

            builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
            builder.Services.AddScoped<ILocalAuthService, LocalAuthService>();

            builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            builder.Services.AddHttpClient();
            builder.Services.AddHttpClient("GHN", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            });

            // Register the background service
            builder.Services.AddHostedService<RssFetchingService>();
            builder.Services.AddHostedService<SubscriptionWorker>();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("FrontendCors", policy =>
                {
                    policy.WithOrigins(corsOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            // JWT
            var jwtKey = builder.Configuration["Jwt:Key"];
            if (!string.IsNullOrWhiteSpace(jwtKey))
            {
                var issuer = builder.Configuration["Jwt:Issuer"];
                var audience = builder.Configuration["Jwt:Audience"];

                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                            ValidateIssuer = !string.IsNullOrWhiteSpace(issuer),
                            ValidIssuer = issuer,
                            ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                            ValidAudience = audience,
                            ValidateLifetime = true,
                            ClockSkew = TimeSpan.FromMinutes(1)
                        };

                        options.Events = new JwtBearerEvents
                        {
                            OnMessageReceived = context =>
                            {
                                var accessToken = context.Request.Query["access_token"];

                                // If the request is for our hub...
                                var path = context.HttpContext.Request.Path;
                                if (!string.IsNullOrEmpty(accessToken) &&
                                    (path.StartsWithSegments("/app-hub")))
                                {
                                    // Read the token out of the query string
                                    context.Token = accessToken;
                                }
                                return Task.CompletedTask;
                            }
                        };
                    });
            }

            builder.Services.AddAuthorization();
            builder.Services.AddSignalR();

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "NongXanh API", Version = "v1" });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "Chỉ cần dán token vào ô bên dưới",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            var app = builder.Build();

            // Auto apply pending EF migrations on startup (Only in Development)
            if (app.Environment.IsDevelopment())
            {
                using (var scope = app.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var pendingMigrations = db.Database.GetPendingMigrations().ToList();
                    if (pendingMigrations.Any())
                    {
                        Console.WriteLine($"Áp dụng {pendingMigrations.Count()} migration(s)...");
                        db.Database.Migrate();
                        Console.WriteLine("Đã tự update migrations");
                    }
                }
            }

            // Seed admin account if not exists
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var adminEmails = app.Configuration.GetSection("AdminEmails").Get<string[]>() ?? [];
                var staffEmails = app.Configuration.GetSection("StaffEmails").Get<string[]>() ?? ["staff@gmail.com"];
                var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                foreach (var adminEmail in adminEmails)
                {
                    var email = adminEmail.Trim().ToLowerInvariant();
                    var exists = db.Users.Any(u => u.Email == email);
                    if (!exists)
                    {
                        db.Users.Add(new DAL.Entity.User
                        {
                            Email = email,
                            DisplayName = "Admin",
                            Provider = "Local",
                            PasswordHash = passwordHasher.Hash("Admin@123"),
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        });
                        db.SaveChanges();
                        Console.WriteLine($"Seeded admin account: {email} / Admin@123");
                    }
                }

                foreach (var staffEmailRaw in staffEmails)
                {
                    var email = staffEmailRaw.Trim().ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(email)) continue;

                    var exists = db.Users.Any(u => u.Email == email);
                    if (!exists)
                    {
                        db.Users.Add(new DAL.Entity.User
                        {
                            Email = email,
                            DisplayName = "Staff",
                            Provider = "Local",
                            PasswordHash = passwordHasher.Hash("Staff@123"),
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        });
                        db.SaveChanges();
                        Console.WriteLine($"Seeded staff account: {email} / Staff@123");
                    }
                }
            }

            // Configure the HTTP request pipeline.
            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseHttpsRedirection();

            // Redirect root to Swagger UI
            app.MapGet("/", () => Results.Redirect("/swagger"));

            app.UseCors("FrontendCors");

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.MapHub<BLL.Hubs.AppHub>("/app-hub");

            app.Run();
        }
    }
}
