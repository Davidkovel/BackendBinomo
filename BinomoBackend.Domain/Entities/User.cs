namespace BinomoBackend.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Email { get; private set; }
    public string? PasswordHash { get; private set; }
    public decimal Balance { get; set; }  = 100m;
    public string? WalletAddress { get; private set; }
    public WalletType? WalletType { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public bool IsActive { get; private set; }
    public List<RefreshToken> RefreshTokens { get; private set; }
    public virtual ICollection<Position> Positions { get; private set; } = new List<Position>();
    public ICollection<PositionsHistory> PositionHistory { get; private set; } = new List<PositionsHistory>();

    private User() 
    {
        RefreshTokens = new List<RefreshToken>();
        Positions = new List<Position>();
        PositionHistory = new List<PositionsHistory>();
    }

    public static User CreateWithPassword(string name, string email, string passwordHash)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            Balance = 100m,
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            RefreshTokens = new List<RefreshToken>(),
            Positions = new List<Position>(),
            PositionHistory = new List<PositionsHistory>()
        };
    }

    public static User CreateWithWallet(string walletAddress, WalletType walletType, string name, string email)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            WalletAddress = walletAddress,
            WalletType = walletType,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            RefreshTokens = new List<RefreshToken>()
        };
    }

    public void UpdateLastLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }

    public void AddRefreshToken(RefreshToken token)
    {
        RefreshTokens.Add(token);
    }

    public void RevokeAllTokens()
    {
        foreach (var token in RefreshTokens.Where(t => t.IsActive))
        {
            token.Revoke();
        }
    }
}