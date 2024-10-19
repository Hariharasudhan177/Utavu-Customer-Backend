using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Google.Apis.Auth;

var builder = WebApplication.CreateBuilder(args);

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Configure Entity Framework Core
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure JWT authentication
var key = builder.Configuration["Jwt:Key"]; // Store your secret key in appsettings.json
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"], // Change to your domain
            ValidAudience = builder.Configuration["Jwt:Audience"], // Change to your domain
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Enable CORS
app.UseCors("AllowAllOrigins");

//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
//}

app.UseHttpsRedirection();
app.UseAuthentication(); // Enable authentication
app.UseAuthorization(); // Enable authorization

app.MapPost("/signup", async (SignUpRequest request, AppDbContext dbContext) =>
{
    var payload = await VerifyIdToken(request.IdToken); // Verify the ID token and extract the payload

    // Check if user already exists
    var existingUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == payload.Email);
    if (existingUser == null)
    {
        // Save the new user to the database
        var user = new User
        {
            Email = payload.Email,
            Name = payload.Name,
            GoogleId = payload.Subject, // Store the unique Google ID
            JwtToken = GenerateJwtToken(payload.Email) // Store the generated JWT token
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
    }

    return Results.Ok(new
    {
        Message = "User signed up successfully!",
        User = new { Email = payload.Email, Name = payload.Name },
        Token = GenerateJwtToken(payload.Email) // Return the generated token
    });
});

app.Run();

// JWT generation method
string GenerateJwtToken(string email)
{
    var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]); // Use a strong secret key from configuration
    var creds = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: builder.Configuration["Jwt:Issuer"],
        audience: builder.Configuration["Jwt:Audience"],
        claims: new[] { new Claim(ClaimTypes.Name, email) }, // Add claims as necessary
        expires: DateTime.Now.AddMinutes(30),
        signingCredentials: creds
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}

async Task<GoogleJsonWebSignature.Payload> VerifyIdToken(string idToken)
{
    var settings = new GoogleJsonWebSignature.ValidationSettings()
    {
        Audience = new[] { builder.Configuration["Google:ClientId"] } // Use your Google Client ID from configuration
    };

    return await GoogleJsonWebSignature.ValidateAsync(idToken, settings); // This will return user info from the token
}

public class SignUpRequest
{
    public string IdToken { get; set; } = string.Empty; // User's ID token from Google
}