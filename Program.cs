using BankAppAPI.Data;
using BankAppAPI.Seeding;
using Blog_Assignment;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using BankAppAPI.Services;



var builder = WebApplication.CreateBuilder(args);

// Configure EF Core

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

//Scaffold-DbContext "Server=BOOK-FDVLVOU3MT;Database=BankAppData;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;"
//Dont want to alter generated code, so i use a separate partial class for any customizations.
builder.Services.AddDbContext<BankAppDataContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));

// Register Auth Service
builder.Services.AddScoped<IAuthService, AuthService>();

// Add Identity to recive user and role managment features
builder.Services.AddIdentity<IdentityUser,
IdentityRole>(options =>
{
    //Password policy 
    options.Password.RequiredLength = 4;              // minimum length
    options.Password.RequireNonAlphanumeric = false; // symbols
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireDigit = false;
    options.Password.RequiredUniqueChars = 1;       // distinct chars required

    //Lockout (mitigates brute force) 
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 10;   // tries before lockout
    options.Lockout.AllowedForNewUsers = true;

    // User rules 
    options.User.RequireUniqueEmail = true;

    // Sign-in rules 
    options.SignIn.RequireConfirmedEmail = false;    // or RequireConfirmedAccount
    options.SignIn.RequireConfirmedPhoneNumber = false;
})

    .AddEntityFrameworkStores<ApplicationDbContext>() //Explains wich db context to use for the identity for the user and roles
    .AddDefaultTokenProviders();

// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtIssuer,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});


builder.Services.AddSwaggerGen(options =>
{
    //Documentation from XML comments 
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml"; 
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);

    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT Authorization header using the Bearer scheme."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", document)] = []
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

// Runs the identity seeder to create default users and roles for when the application is started for the first time
await IdentitySeeder.SeedAsync(app.Services);

app.MapControllers();

app.Run();
