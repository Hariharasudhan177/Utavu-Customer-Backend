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

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthentication(); // Enable authentication
app.UseAuthorization(); // Enable authorization

// Endpoint for signing up a user
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

app.MapGet("/profile", async (ClaimsPrincipal user, AppDbContext dbContext) =>
{
    var email = user.FindFirst(ClaimTypes.Name)?.Value;

    if (email == null)
    {
        return Results.Unauthorized();
    }

    var userProfile = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (userProfile == null)
    {
        return Results.NotFound();
    }

    return Results.Ok(new
    {
        Email = userProfile.Email,
        Name = userProfile.Name,
        Address = userProfile.Address ?? "", // Handle null address
        JobType = userProfile.JobType ?? "", // Handle null job type
        GeneralAvailabilityStartTime = userProfile.GeneralAvailabilityStartTime?.ToString(@"hh\:mm") ?? "", // Handle null availability
        GeneralAvailabilityEndTime = userProfile.GeneralAvailabilityEndTime?.ToString(@"hh\:mm") ?? "" // Handle null availability
    });
}).RequireAuthorization();

app.MapPut("/profile", async (ClaimsPrincipal user, AppDbContext dbContext, UserProfileUpdateModel updatedProfile) =>
{
    var email = user.FindFirst(ClaimTypes.Name)?.Value;

    if (email == null)
    {
        return Results.Unauthorized();
    }

    var userProfile = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (userProfile == null)
    {
        return Results.NotFound();
    }

    // Update the user's profile details with the new data
    userProfile.Address = updatedProfile.Address ?? userProfile.Address;
    userProfile.JobType = updatedProfile.JobType ?? userProfile.JobType;
    userProfile.GeneralAvailabilityStartTime = updatedProfile.GeneralAvailabilityStartTime ?? userProfile.GeneralAvailabilityStartTime;
    userProfile.GeneralAvailabilityEndTime = updatedProfile.GeneralAvailabilityEndTime ?? userProfile.GeneralAvailabilityEndTime;

    // Save changes to the database
    await dbContext.SaveChangesAsync();

    return Results.Ok(new
    {
        Message = "Profile updated successfully"
    });
}).RequireAuthorization();

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
        expires: DateTime.Now.AddDays(7),
        signingCredentials: creds
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}

// Method to verify the Google ID token
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

public class UserProfileUpdateModel
{
    public string? Address { get; set; } // Nullable to allow partial updates
    public string? JobType { get; set; } // Nullable to allow partial updates
    public TimeSpan? GeneralAvailabilityStartTime { get; set; }  // Nullable to allow partial updates
    public TimeSpan? GeneralAvailabilityEndTime { get; set; } // Nullable to allow partial updates
}
