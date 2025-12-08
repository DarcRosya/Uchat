

using Microsoft.EntityFrameworkCore;
using Uchat.Database.Entities;

namespace Uchat.Database.Context;
public class UchatDbContext : DbContext
{
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<ChatRoom> ChatRooms { get; set; } = null!;
    public DbSet<ChatRoomMember> ChatRoomMembers { get; set; } = null!;
    public DbSet<Contact> Contacts { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

    public UchatDbContext(DbContextOptions<UchatDbContext> options) : base(options)
    {
        // base(options) передает настройки в родительский класс DbContext
    }

    /// We use Fluent API!
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ====================================================================
        // USERS' TABLE
        // ====================================================================
        
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasKey(u => u.Id);

            entity.HasIndex(u => u.Username)
                .IsUnique() 
                .HasDatabaseName("IX_Users_Username");
            
            entity.HasIndex(u => u.Email)
                .IsUnique() 
                .HasDatabaseName("IX_Users_Email");

            entity.Property(u => u.Username)
                .IsRequired()  // NOT NULL
                .HasMaxLength(50);  // VARCHAR(50)

            entity.Property(u => u.DateOfBirth);

            entity.Property(u => u.PasswordHash)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(u => u.DisplayName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(u => u.AvatarUrl)
                .HasMaxLength(500);

            // PostgreSQL: CreatedAt TIMESTAMP NOT NULL DEFAULT NOW()
            // SQLite: CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            entity.Property(u => u.CreatedAt)
                .HasDefaultValueSql("NOW()"); 

            entity.Property(u => u.LanguageCode)
                .IsRequired()
                .HasMaxLength(5)  // "en", "uk", "en-US"
                .HasDefaultValue("en");

            // User -> ChatRoomMemberships (One-to-Many)
            entity.HasMany(u => u.ChatRoomMemberships)
                .WithOne(crm => crm.User)
                .HasForeignKey(crm => crm.UserId)
                .OnDelete(DeleteBehavior.Cascade); 

            // User -> Contacts (One-to-Many)
            entity.HasMany(u => u.Contacts)
                .WithOne(c => c.Owner)
                .HasForeignKey(c => c.OwnerId)
                .OnDelete(DeleteBehavior.Cascade); 
            
            // User -> RefreshTokens (One-to-Many)
            entity.HasMany(u => u.RefreshTokens)
                .WithOne(t => t.User)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ====================================================================
        // CHATROOMS' TABLE
        // ====================================================================
        
        modelBuilder.Entity<ChatRoom>(entity =>
        {
            entity.ToTable("ChatRooms");
            entity.HasKey(cr => cr.Id);

            entity.HasIndex(cr => cr.Type)
                .HasDatabaseName("IX_ChatRooms_Type");
            
            entity.HasIndex(cr => cr.CreatorId)
                .HasDatabaseName("IX_ChatRooms_CreatorId");
            
            
            entity.HasIndex(cr => cr.LastActivityAt)
                .HasDatabaseName("IX_ChatRooms_LastActivityAt");

            entity.Property(cr => cr.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(cr => cr.Description)
                .HasMaxLength(300);

            entity.Property(cr => cr.IconUrl)
                .HasMaxLength(500);

            entity.Property(cr => cr.CreatedAt)
                .HasDefaultValueSql("NOW()");

            // ChatRoom -> Creator (Many-to-One)
            entity.HasOne(cr => cr.Creator)
                .WithMany()  
                .HasForeignKey(cr => cr.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);  // Can't Delete the Creator

            // ChatRoom -> Members (One-to-Many)
            entity.HasMany(cr => cr.Members)
                .WithOne(crm => crm.ChatRoom)
                .HasForeignKey(crm => crm.ChatRoomId)
                .OnDelete(DeleteBehavior.Cascade);  
        });

        // ====================================================================
        // CHATROOMMEMBERS
        // ====================================================================
        
        modelBuilder.Entity<ChatRoomMember>(entity =>
        {
            entity.ToTable("ChatRoomMembers");
            entity.HasKey(crm => crm.Id);
            
            entity.HasIndex(crm => new { crm.ChatRoomId, crm.UserId })
                .IsUnique()  // UNIQUE (ChatRoomId, UserId)
                .HasDatabaseName("IX_ChatRoomMembers_ChatRoom_User");

            entity.HasIndex(crm => crm.UserId)
                .HasDatabaseName("IX_ChatRoomMembers_UserId");

            entity.Property(crm => crm.JoinedAt)
                .HasDefaultValueSql("NOW()");

            entity.Property(crm => crm.ClearedHistoryAt);

            // ChatRoomMember -> InvitedBy (who invited)
            entity.HasOne(crm => crm.InvitedBy)
                .WithMany()  
                .HasForeignKey(crm => crm.InvitedById)
                .OnDelete(DeleteBehavior.SetNull); 
        });

        // ====================================================================
        // CONTACTS
        // ====================================================================
        
        modelBuilder.Entity<Contact>(entity =>
        {
            entity.ToTable("Contacts");
            entity.HasKey(c => c.Id);

            entity.HasIndex(c => new { c.OwnerId, c.ContactUserId })
                .IsUnique()
                .HasDatabaseName("IX_Contacts_Owner_Contact");

            entity.HasIndex(c => c.ContactUserId)
                .HasDatabaseName("IX_Contacts_ContactUserId");

            entity.Property(c => c.Nickname)
                .HasMaxLength(100);

            entity.Property(c => c.AddedAt)
                .HasDefaultValueSql("NOW()");
            
            entity.Property(c => c.NotificationsEnabled)
                .HasDefaultValue(true);

            entity.Property(c => c.IsFavorite)
                .HasDefaultValue(false);

            entity.Property(c => c.IsBlocked)
                .HasDefaultValue(false);

            entity.Property(c => c.Status)
                .HasDefaultValue(ContactStatus.None);   

            // Contact -> Owner (Кто создал запись)
            entity.HasOne(c => c.Owner)
                .WithMany(u => u.Contacts) 
                .HasForeignKey(c => c.OwnerId)
                .OnDelete(DeleteBehavior.Cascade); 

            // Contact -> ContactUser (Кого добавили)
            entity.HasOne(c => c.ContactUser)
                .WithMany()  
                .HasForeignKey(c => c.ContactUserId)
                .OnDelete(DeleteBehavior.Cascade); 
                
            // Contact -> SavedChatRoom (Личный чат)
            entity.HasOne(c => c.SavedChatRoom)
                .WithMany()
                .HasForeignKey(c => c.SavedChatRoomId)
                .OnDelete(DeleteBehavior.SetNull); // Если чат удалили, контакт остается, но ссылка сбрасывается
        });

        // ====================================================================
        // REFRESHTOKENS
        // ====================================================================
        
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(t => t.Id);
            
            entity.HasIndex(t => t.TokenHash)
                .IsUnique()
                .HasDatabaseName("IX_RefreshTokens_TokenHash");
            
            entity.HasIndex(t => t.UserId)
                .HasDatabaseName("IX_RefreshTokens_UserId");
            
            entity.HasIndex(t => new { t.UserId, t.IsRevoked, t.ExpiresAt })
                .HasDatabaseName("IX_RefreshTokens_UserId_IsRevoked_ExpiresAt");
            
            entity.Property(t => t.TokenHash)
                .IsRequired()
                .HasMaxLength(64);  // SHA256 hash = 64 hex chars
            
            entity.Property(t => t.CreatedAt)
                .HasDefaultValueSql("NOW()");
            
            entity.Property(t => t.IsRevoked)
                .HasDefaultValue(false);
        });
    }
}