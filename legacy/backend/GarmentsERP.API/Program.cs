using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using GarmentsERP.API.Data;
using GarmentsERP.API.Models;
using GarmentsERP.API.Services;
using GarmentsERP.API.Services.Users;
using GarmentsERP.API.Services.Auth;
using GarmentsERP.API.Services.Interfaces;
using GarmentsERP.API.Interfaces;
using GarmentsERP.API.Middleware;
using GarmentsERP.API.Config;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Database Configuration
builder.Services.AddDatabaseConfiguration(builder.Configuration);

// Identity Configuration
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false; // For demo purposes
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// JWT Configuration
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Authorization
builder.Services.AddAuthorization();

// Register custom services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IPermissionSeederService, PermissionSeederService>();
builder.Services.AddScoped<IRoleManagementService, RoleManagementService>();

// Register security and audit services
// builder.Services.AddScoped<ITrialBalanceAuditService, TrialBalanceAuditService>();
// builder.Services.AddScoped<IRateLimitingService, RateLimitingService>();
// builder.Services.AddScoped<IDataEncryptionService, DataEncryptionService>();
// builder.Services.AddScoped<IInputSanitizationService, InputSanitizationService>();

// Register independent model services
// builder.Services.AddScoped<ICurrencyService, CurrencyService>();
// builder.Services.AddScoped<IProductCategoryService, ProductCategoryService>();
// builder.Services.AddScoped<ITaxRateService, TaxRateService>();
// builder.Services.AddScoped<ITaxSchemeService, TaxSchemeService>();
// builder.Services.AddScoped<IWarehouseService, WarehouseService>();
// builder.Services.AddScoped<ICompanyService, CompanyService>();
// builder.Services.AddScoped<IBusinessSettingService, BusinessSettingService>();
// builder.Services.AddScoped<IReportTemplateService, ReportTemplateService>();
// builder.Services.AddScoped<CashBookImportService>();

// Enhanced Dynamic Accounting Services
// Temporarily commented out to resolve compilation errors
// builder.Services.AddScoped<ICategoryService, CategoryService>();
// builder.Services.AddScoped<IContactService, ContactService>();
// builder.Services.AddScoped<ITransactionValidator, TransactionValidator>();
// builder.Services.AddScoped<IBusinessRuleValidator, BusinessRuleValidator>();
// builder.Services.AddScoped<ITrialBalanceCalculationService, TrialBalanceCalculationService>();
// builder.Services.AddScoped<ITrialBalanceCalculationMemoizationService, TrialBalanceCalculationMemoizationService>();
// builder.Services.AddScoped<ITrialBalanceCacheService, TrialBalanceCacheService>();
// builder.Services.AddScoped<ITrialBalanceService, TrialBalanceService>();
// builder.Services.AddScoped<IBalanceService, BalanceService>();
// Temporarily commented out to resolve compilation errors
// builder.Services.AddScoped<IEnhancedCashBookService, EnhancedCashBookService>();
builder.Services.AddScoped<IJournalEntryService, JournalEntryService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
// builder.Services.AddScoped<IDatabasePerformanceService, DatabasePerformanceService>();

// Redis Configuration
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var configuration = provider.GetService<IConfiguration>();
    var connectionString = configuration?.GetConnectionString("Redis") ?? "redis:6379";
    return ConnectionMultiplexer.Connect(connectionString);
});

// Add Redis distributed cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "redis:6379";
});

// Add in-memory cache
builder.Services.AddMemoryCache();

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000", "https://localhost:3000",  // Original Next.js port
                "http://localhost:3001", "https://localhost:3001",  // Alternative Next.js port
                "http://localhost:4000", "https://localhost:4000"   // Current Docker mapped port
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

// Swagger Configuration
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Garments ERP API", 
        Version = "v1",
        Description = "A comprehensive ERP system for garments manufacturing businesses with advanced trial balance reporting capabilities"
    });

    // Include XML comments for better API documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // JWT Authentication in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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
            new string[] {}
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Garments ERP API V1");
        c.RoutePrefix = string.Empty; // Set Swagger as the default page
    });
}

app.UseHttpsRedirection();

app.UseCors("AllowedOrigins");

// Add dependency check middleware
app.UseMiddleware<DependencyCheckMiddleware>();

// Add rate limiting middleware
// Temporarily commented out to resolve compilation errors
// app.UseMiddleware<RateLimitingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Database Migration and Seeding
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var permissionSeeder = scope.ServiceProvider.GetRequiredService<IPermissionSeederService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        await context.Database.MigrateAsync();
        logger.LogInformation("✅ Database migration completed successfully");
        
        // Seed permissions and role assignments
        // await permissionSeeder.SeedPermissionsAndRoleAssignmentsAsync();
        // logger.LogInformation("✅ Permissions and role assignments seeded successfully");
        
        // Seed initial the client categories
        // await CategorySeeder.SeedCategoriesAsync(context);
        // logger.LogInformation("✅ the client categories seeded successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ An error occurred while migrating the database or seeding data.");
    }
}

app.Run();

// Make Program class accessible for testing
public partial class Program { }
