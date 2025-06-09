using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using HarvestCraft2.Economy.API.Data;
using HarvestCraft2.Economy.API.Services;
using HarvestCraft2.Economy.API.Services.Interfaces;
using HarvestCraft2.Economy.API.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;

namespace HarvestCraft2.Economy.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Services 구성
            ConfigureServices(builder.Services, builder.Configuration, builder.Environment);

            var app = builder.Build();

            // Middleware 파이프라인 구성
            await ConfigureMiddleware(app);

            // 애플리케이션 시작 로깅
            LogStartupInformation(app);

            // 개발 환경에서 DB 마이그레이션
            if (app.Environment.IsDevelopment())
            {
                await RunDatabaseMigration(app);
            }

            app.Run();
        }
        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
        {
            // Controllers 설정
            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = null; // PascalCase 유지
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                });

            // Entity Framework Core - MySQL
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                options.UseMySQL(connectionString); // UseMySql → UseMySQL 변경

                // Development 환경에서만 상세 에러 활성화
                if (environment.IsDevelopment())
                {
                    options.EnableDetailedErrors();
                    options.EnableSensitiveDataLogging();
                }
            });

            // Redis Configuration
            services.AddSingleton<IConnectionMultiplexer>(provider =>
            {
                var connectionString = configuration.GetConnectionString("RedisConnection");
                var options = ConfigurationOptions.Parse(connectionString);
                options.AbortOnConnectFail = false; // 연결 실패 시 재시도
                options.ConnectTimeout = 5000;
                options.SyncTimeout = 5000;
                return ConnectionMultiplexer.Connect(options);
            });

            // Custom Services Registration
            services.AddScoped<IRedisService, RedisService>();
            services.AddScoped<IPriceService, PriceService>();
            //services.AddScoped<IShopService, ShopService>();
            //
            //// Background Services
            //services.AddHostedService<PriceUpdateBackgroundService>();
            //services.AddHostedService<DataCleanupBackgroundService>();

            // API Documentation
            ConfigureSwagger(services);

            // CORS Configuration
            ConfigureCors(services, configuration);

            // Rate Limiting (Simple)
            services.AddMemoryCache();

            // Health Checks
            services.AddHealthChecks()
                .AddDbContextCheck<ApplicationDbContext>("database")
                .AddCheck<RedisHealthCheck>("redis");

            // Authentication & Authorization (API Key based)
            services.AddAuthentication("ApiKey")
                .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null);
            services.AddAuthorization(options =>
            {
                options.AddPolicy("ApiKey", policy => policy.RequireAuthenticatedUser());
            });

            // Response Caching
            services.AddResponseCaching();

            // Logging Configuration
            ConfigureLogging(services, environment);
        }

        private static void ConfigureSwagger(IServiceCollection services)
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "HarvestCraft 2 Economy API",
                    Version = "v1.0",
                    Description = "동적 가격 변동 기반 마인크래프트 경제 시스템",
                    Contact = new OpenApiContact
                    {
                        Name = "HarvestCraft 2 Economy Team",
                        Email = "admin@your-server.com"
                    }
                });

                // API Key Authentication for Swagger
                options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Name = "X-API-Key",
                    Description = "API Key for Minecraft Plugin Authentication"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "ApiKey"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });
        }

        private static void ConfigureCors(IServiceCollection services, IConfiguration configuration)
        {
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    var allowedOrigins = configuration.GetSection("ApiSettings:AllowedOrigins").Get<string[]>()
                                       ?? new[] { "*" };

                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });
        }

        private static void ConfigureLogging(IServiceCollection services, IWebHostEnvironment environment)
        {
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddConsole();
                loggingBuilder.AddDebug();

                if (environment.IsProduction())
                {
                    // 프로덕션에서는 로그 레벨 조정
                    loggingBuilder.SetMinimumLevel(LogLevel.Warning);
                }
            });
        }

        private static async Task ConfigureMiddleware(WebApplication app)
        {
            // Development 환경 설정
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "HarvestCraft 2 Economy API v1");
                    options.RoutePrefix = string.Empty; // Swagger UI를 루트에서 접근
                });
            }
            else
            {
                // 프로덕션 환경 설정
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            // CORS
            app.UseCors();

            // Response Caching
            app.UseResponseCaching();

            // Security Headers
            app.Use(async (context, next) =>
            {
                context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                context.Response.Headers.Add("X-Frame-Options", "DENY");
                context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
                await next();
            });

            // Authentication & Authorization
            app.UseAuthentication();
            app.UseAuthorization();

            // Health Checks
            app.MapHealthChecks("/health");

            // Controllers
            app.MapControllers();

            // Custom Middleware for Request Logging (개발 환경에서만)
            if (app.Environment.IsDevelopment())
            {
                app.Use(async (context, next) =>
                {
                    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("Request: {Method} {Path}",
                        context.Request.Method, context.Request.Path);

                    await next();

                    logger.LogInformation("Response: {StatusCode}", context.Response.StatusCode);
                });
            }
        }

        private static void LogStartupInformation(WebApplication app)
        {
            app.Logger.LogInformation("🚀 HarvestCraft 2 Economy API Starting...");
            app.Logger.LogInformation("📊 Environment: {Environment}", app.Environment.EnvironmentName);
            app.Logger.LogInformation("🔗 Database: MySQL");
            app.Logger.LogInformation("⚡ Cache: Redis");
            app.Logger.LogInformation("📈 Price Update Interval: 10 minutes");
        }

        private static async Task RunDatabaseMigration(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            try
            {
                await dbContext.Database.MigrateAsync();
                app.Logger.LogInformation("Database migration completed successfully.");
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Database migration failed.");
            }
        }
    }
}