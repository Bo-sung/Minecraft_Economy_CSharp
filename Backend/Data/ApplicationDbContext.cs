using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HarvestCraft2.Economy.API.Models;

namespace HarvestCraft2.Economy.API.Data
{
    /// <summary>
    /// HarvestCraft 2 Economy 시스템 데이터베이스 컨텍스트
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // 테이블 DbSet 정의
        public DbSet<ShopItem> ShopItems { get; set; }
        public DbSet<ShopTransaction> ShopTransactions { get; set; }
        public DbSet<PriceHistory> PriceHistories { get; set; }
        public DbSet<ServerConfig> ServerConfigs { get; set; }
        public DbSet<PlayerSession> PlayerSessions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ShopItem 엔티티 구성
            ConfigureShopItem(modelBuilder);

            // ShopTransaction 엔티티 구성
            ConfigureShopTransaction(modelBuilder);

            // PriceHistory 엔티티 구성
            ConfigurePriceHistory(modelBuilder);

            // ServerConfig 엔티티 구성
            ConfigureServerConfig(modelBuilder);

            // PlayerSession 엔티티 구성
            ConfigurePlayerSession(modelBuilder);

            // 초기 데이터 시드
            SeedInitialData(modelBuilder);
        }

        private void ConfigureShopItem(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ShopItem>(entity =>
            {
                // 인덱스 구성
                entity.HasIndex(e => e.ItemId)
                    .IsUnique()
                    .HasDatabaseName("idx_shop_items_item_id");

                entity.HasIndex(e => e.Category)
                    .HasDatabaseName("idx_shop_items_category");

                entity.HasIndex(e => e.IsActive)
                    .HasDatabaseName("idx_shop_items_active");

                entity.HasIndex(e => new { e.ItemId, e.IsActive })
                    .HasDatabaseName("idx_shop_items_item_lookup");

                // 열거형을 문자열로 저장
                entity.Property(e => e.Category)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(e => e.ComplexityLevel)
                    .HasConversion<string>()
                    .HasMaxLength(10);

                // 기본값 설정
                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

                entity.Property(e => e.IsActive)
                    .HasDefaultValue(true);
            });
        }

        private void ConfigureShopTransaction(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ShopTransaction>(entity =>
            {
                // 인덱스 구성
                entity.HasIndex(e => new { e.PlayerId, e.CreatedAt })
                    .HasDatabaseName("idx_shop_transactions_player_time");

                entity.HasIndex(e => new { e.ItemId, e.CreatedAt })
                    .HasDatabaseName("idx_shop_transactions_item_time");

                entity.HasIndex(e => new { e.TransactionType, e.CreatedAt })
                    .HasDatabaseName("idx_shop_transactions_type_time");

                entity.HasIndex(e => new { e.CreatedAt, e.ItemId })
                    .HasDatabaseName("idx_shop_transactions_recent_trades")
                    .IsDescending(true, false);

                // 외래 키 관계
                entity.HasOne(e => e.ShopItem)
                    .WithMany(e => e.Transactions)
                    .HasForeignKey(e => e.ItemId)
                    .HasPrincipalKey(e => e.ItemId)
                    .OnDelete(DeleteBehavior.Cascade);

                // 열거형을 문자열로 저장
                entity.Property(e => e.TransactionType)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                // 기본값 설정
                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });
        }

        private void ConfigurePriceHistory(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PriceHistory>(entity =>
            {
                // 인덱스 구성
                entity.HasIndex(e => new { e.ItemId, e.PriceTimestamp })
                    .HasDatabaseName("idx_price_history_item_time");

                entity.HasIndex(e => e.PriceTimestamp)
                    .HasDatabaseName("idx_price_history_timestamp");

                entity.HasIndex(e => new { e.PriceTimestamp, e.ItemId })
                    .HasDatabaseName("idx_price_history_time_item")
                    .IsDescending(true, false);

                // 외래 키 관계
                entity.HasOne(e => e.ShopItem)
                    .WithMany(e => e.PriceHistories)
                    .HasForeignKey(e => e.ItemId)
                    .HasPrincipalKey(e => e.ItemId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private void ConfigureServerConfig(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ServerConfig>(entity =>
            {
                // 인덱스 구성
                entity.HasIndex(e => e.ConfigKey)
                    .IsUnique()
                    .HasDatabaseName("idx_server_config_key");

                // 기본값 설정
                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");
            });
        }

        private void ConfigurePlayerSession(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlayerSession>(entity =>
            {
                // 인덱스 구성
                entity.HasIndex(e => new { e.IsOnline, e.LastActivity })
                    .HasDatabaseName("idx_player_sessions_online_activity");

                entity.HasIndex(e => new { e.SessionWeight, e.IsOnline })
                    .HasDatabaseName("idx_player_sessions_weight_online");

                // 기본값 설정
                entity.Property(e => e.LastActivity)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

                entity.Property(e => e.SessionWeight)
                    .HasDefaultValue(0.3m);

                entity.Property(e => e.IsOnline)
                    .HasDefaultValue(true);
            });
        }

        private void SeedInitialData(ModelBuilder modelBuilder)
        {
            // 서버 기본 설정
            modelBuilder.Entity<ServerConfig>().HasData(
                new ServerConfig { Id = 1, ConfigKey = "base_online_players", ConfigValue = "25", Description = "기준 접속자 수 (서버 정원의 50%)" },
                new ServerConfig { Id = 2, ConfigKey = "price_update_interval", ConfigValue = "600", Description = "가격 업데이트 주기 (초)" },
                new ServerConfig { Id = 3, ConfigKey = "max_price_change", ConfigValue = "0.10", Description = "주기당 최대 가격 변동률" },
                new ServerConfig { Id = 4, ConfigKey = "min_price_ratio", ConfigValue = "0.50", Description = "기본가 대비 최저가 비율" },
                new ServerConfig { Id = 5, ConfigKey = "max_price_ratio", ConfigValue = "3.00", Description = "기본가 대비 최고가 비율" },
                new ServerConfig { Id = 6, ConfigKey = "session_weight_instant", ConfigValue = "0.3", Description = "즉석 접속자 가중치 (10분 미만)" },
                new ServerConfig { Id = 7, ConfigKey = "session_weight_short", ConfigValue = "0.6", Description = "단기 접속자 가중치 (10-30분)" },
                new ServerConfig { Id = 8, ConfigKey = "session_weight_medium", ConfigValue = "0.8", Description = "중기 접속자 가중치 (30분-2시간)" },
                new ServerConfig { Id = 9, ConfigKey = "session_weight_long", ConfigValue = "1.0", Description = "장기 접속자 가중치 (2시간+)" }
            );

            // 바닐라 테스트 아이템 (일부)
            modelBuilder.Entity<ShopItem>().HasData(
                new ShopItem
                {
                    Id = 1,
                    ItemId = "minecraft:wheat",
                    DisplayName = "밀",
                    Category = ItemCategory.Vanilla,
                    HungerRestore = 0,
                    SaturationRestore = 0,
                    ComplexityLevel = ComplexityLevel.Low,
                    BaseSellPrice = 2.00m,
                    BaseBuyPrice = 1.50m,
                    MinPrice = 1.00m,
                    MaxPrice = 6.00m,
                    CreatedAt = new DateTime(2025, 6, 9, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2025, 6, 9, 0, 0, 0, DateTimeKind.Utc)
                },
                new ShopItem
                {
                    Id = 2,
                    ItemId = "minecraft:carrot",
                    DisplayName = "당근",
                    Category = ItemCategory.Vanilla,
                    HungerRestore = 3,
                    SaturationRestore = 3.6m,
                    ComplexityLevel = ComplexityLevel.Low,
                    BaseSellPrice = 3.00m,
                    BaseBuyPrice = 2.25m,
                    MinPrice = 1.50m,
                    MaxPrice = 9.00m,
                    CreatedAt = new DateTime(2025, 6, 9, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2025, 6, 9, 0, 0, 0, DateTimeKind.Utc)
                },
                new ShopItem
                {
                    Id = 3,
                    ItemId = "minecraft:bread",
                    DisplayName = "빵",
                    Category = ItemCategory.Vanilla,
                    HungerRestore = 5,
                    SaturationRestore = 6.0m,
                    ComplexityLevel = ComplexityLevel.Low,
                    BaseSellPrice = 8.00m,
                    BaseBuyPrice = 6.00m,
                    MinPrice = 4.00m,
                    MaxPrice = 24.00m,
                    CreatedAt = new DateTime(2025, 6, 9, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2025, 6, 9, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        }

        /// <summary>
        /// 데이터베이스 연결 상태 확인
        /// </summary>
        public async Task<bool> CanConnectAsync()
        {
            try
            {
                return await Database.CanConnectAsync();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 마이그레이션 적용
        /// </summary>
        public async Task<bool> ApplyMigrationsAsync()
        {
            try
            {
                await Database.MigrateAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 서버 설정 엔티티
    /// </summary>
    [Table("server_config")]
    public class ServerConfig
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("config_key")]
        public string ConfigKey { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        [Column("config_value")]
        public string ConfigValue { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 플레이어 세션 엔티티
    /// </summary>
    [Table("player_sessions")]
    public class PlayerSession
    {
        [Key]
        [MaxLength(36)]
        [Column("player_id")]
        public string PlayerId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [Column("player_name")]
        public string PlayerName { get; set; } = string.Empty;

        [Required]
        [Column("login_time")]
        public DateTime LoginTime { get; set; }

        [Column("last_activity")]
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;

        [Column("session_weight")]
        [Precision(3, 1)]
        public decimal SessionWeight { get; set; } = 0.3m;

        [Column("is_online")]
        public bool IsOnline { get; set; } = true;

        [NotMapped]
        public TimeSpan SessionDuration => DateTime.UtcNow - LoginTime;

        [NotMapped]
        public TimeSpan TimeSinceActivity => DateTime.UtcNow - LastActivity;
    }
}