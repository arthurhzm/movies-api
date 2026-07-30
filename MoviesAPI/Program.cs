using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using DotNetEnv;
using MoviesAPI.Data;
using Microsoft.EntityFrameworkCore;
using MoviesAPI.Services;
using MoviesAPI.Extensions;
using Microsoft.OpenApi.Models;

// Load .env file
if (File.Exists(".env"))
{
    Env.Load();
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    // Configuração para produção - aceitar tokens de diferentes fontes
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Aceitar token do header Authorization
            var authorizationHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authorizationHeader) && authorizationHeader.StartsWith("Bearer "))
            {
                context.Token = authorizationHeader.Substring("Bearer ".Length).Trim();
                Console.WriteLine($"JWT Token received: {context.Token.Substring(0, Math.Min(20, context.Token.Length))}...");
            }
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            // Log para debug em produção
            Console.WriteLine($"JWT Challenge: {context.Error}, {context.ErrorDescription}");
            Console.WriteLine($"Request Path: {context.Request.Path}");
            Console.WriteLine($"Request Method: {context.Request.Method}");
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            // Log para debug em produção
            Console.WriteLine($"JWT Auth Failed: {context.Exception.Message}");
            Console.WriteLine($"Request Path: {context.Request.Path}");
            Console.WriteLine($"Request Method: {context.Request.Method}");
            return Task.CompletedTask;
        }
    };
});

var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
string connectionString;

if (!string.IsNullOrEmpty(databaseUrl) && databaseUrl.StartsWith("postgresql://"))
{
    var uri = new Uri(databaseUrl);
    connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.Trim('/')};Username={uri.UserInfo.Split(':')[0]};Password={uri.UserInfo.Split(':')[1]};SSL Mode=Require;Trust Server Certificate=true";
}
else
{
    connectionString = databaseUrl ?? throw new InvalidOperationException("DATABASE_URL não está configurada");
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));


builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<GenresService>();
builder.Services.AddScoped<UserPreferencesService>();
builder.Services.AddScoped<UserMovieFeedbackService>();
builder.Services.AddScoped<UserRecommendationFeedbackService>();
builder.Services.AddScoped<UserFollowersService>();
builder.Services.AddScoped<AiAssistantService>();
builder.Services.AddScoped<HuggingFaceService>();
builder.Services.AddScoped<RecommendationService>();
builder.Services.AddScoped<MatchService>();
builder.Services.AddScoped<LetterboxdCsvParser>();
builder.Services.AddScoped<LetterboxdService>();
builder.Services.AddHostedService<LetterboxdPollingService>();
builder.Services.AddHttpClient("HuggingFaceClient", client =>
{
    client.BaseAddress = new Uri("https://router.huggingface.co/v1/");
    client.Timeout = TimeSpan.FromSeconds(90);
});
builder.Services.AddHttpClient("LetterboxdClient");

// TMDB is the canonical movie identity: resolve titles -> tmdbId so imported
// ratings line up with movies browsed through TMDB. Falls back to a no-op
// resolver when no token is configured (e.g. tests / local without TMDB).
var tmdbToken = Environment.GetEnvironmentVariable("TMDB_TOKEN")
    ?? builder.Configuration["Tmdb:Token"];
if (!string.IsNullOrWhiteSpace(tmdbToken))
{
    builder.Services.AddHttpClient("TmdbClient", client =>
    {
        client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {tmdbToken}");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });
    builder.Services.AddScoped<ITmdbResolver, TmdbResolver>();
}
else
{
    builder.Services.AddScoped<ITmdbResolver, NoopTmdbResolver>();
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        var allowedOrigins = new List<string>
        {
            "https://cinematch-inky.vercel.app",
            "https://cfcc352d8998.ngrok-free.app"
        };

        if (builder.Environment.IsDevelopment())
        {
            allowedOrigins.AddRange(new[]
            {
                "http://localhost:5173",
                "https://localhost:5173",
                "http://192.160.8.165:5173",
                "https://192.160.8.165:5173"
            });
        }

        policy.WithOrigins(allowedOrigins.ToArray())
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .WithExposedHeaders("ngrok-skip-browser-warning")
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});


builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Movies API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
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
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://*:{port}");

// Middleware específico para ngrok - deve ser o primeiro
app.Use(async (context, next) =>
{
    // Adicionar header para bypass do ngrok warning sempre
    context.Response.Headers["ngrok-skip-browser-warning"] = "any";

    // Se for produção, adicionar headers CORS específicos para ngrok
    if (!app.Environment.IsDevelopment())
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (!string.IsNullOrEmpty(origin) &&
            (origin.Contains("cinematch-inky.vercel.app") || origin.Contains("ngrok-free.app")))
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = origin;
            context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, PATCH, OPTIONS";
            context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, Accept, Origin, X-Requested-With, ngrok-skip-browser-warning";
            context.Response.Headers["Access-Control-Expose-Headers"] = "ngrok-skip-browser-warning";
        }

        // Handle preflight requests
        if (context.Request.Method == "OPTIONS")
        {
            context.Response.StatusCode = 200;
            return;
        }
    }

    await next();
});

// Middleware personalizado para debug e CORS em produção
if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        // Log da requisição
        Console.WriteLine($"Request: {context.Request.Method} {context.Request.Path}");
        Console.WriteLine($"Origin: {context.Request.Headers.Origin}");
        Console.WriteLine($"Authorization: {context.Request.Headers.Authorization}");

        await next();

        // Log da resposta
        Console.WriteLine($"Response: {context.Response.StatusCode}");
    });
}

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.Migrate();
}

await app.SeedDatabaseAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Use CORS first, before authentication
app.UseCors("AllowAll");

// Security middleware
app.UseAuthentication();
app.UseAuthorization();

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
