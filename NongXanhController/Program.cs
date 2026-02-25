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

namespace NongXanhController
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

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
            builder.Services.AddScoped<IBlogService, BlogService>();

            builder.Services.AddScoped<ITokenService, TokenService>();
            builder.Services.AddScoped<IGoogleOAuthService, GoogleOAuthService>();
            builder.Services.AddScoped<IEmailSender, EmailSender>();
            builder.Services.AddScoped<IEmailOtpService, EmailOtpService>();

            builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
            builder.Services.AddScoped<ILocalAuthService, LocalAuthService>();

            builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            builder.Services.AddHttpClient();

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
                    });
            }

            builder.Services.AddAuthorization();

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

            // Auto apply pending EF migrations on startup
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var pendingMigrations = db.Database.GetPendingMigrations();
                if (pendingMigrations.Any())
                {
                    Console.WriteLine($"Áp dụng {pendingMigrations.Count()}  migration(s)...");
                    db.Database.Migrate();
                    Console.WriteLine("Đã tự update migrations");
                }

                // Seed admin account if not exists
                var adminEmails = app.Configuration.GetSection("AdminEmails").Get<string[]>() ?? [];
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
            }

            // Configure the HTTP request pipeline.
            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseHttpsRedirection();

            // Redirect root to Swagger UI
            app.MapGet("/", () => Results.Redirect("/swagger"));

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
