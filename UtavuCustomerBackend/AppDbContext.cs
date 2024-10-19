using Microsoft.EntityFrameworkCore;

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
}
