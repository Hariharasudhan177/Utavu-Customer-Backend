using Microsoft.EntityFrameworkCore;
using System;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
}

public class User
{
    public int Id { get; set; }
    public string Email { get; set; }
    public string Name { get; set; }
    public string GoogleId { get; set; } // Store Google ID if needed
    public string JwtToken { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;    
    // Make Address and JobType nullable
    public string? Address { get; set; } // User's address (nullable)
    public string? JobType { get; set; } // Types of jobs the user is available for (nullable)
    // Nullable TimeSpan properties to handle cases where availability is not set
    public TimeSpan? GeneralAvailabilityStartTime { get; set; }  // Nullable equivalent of SQL TIME
    public TimeSpan? GeneralAvailabilityEndTime { get; set; }

    // Add more fields as necessary, like phone number, profile picture URL, etc.
}