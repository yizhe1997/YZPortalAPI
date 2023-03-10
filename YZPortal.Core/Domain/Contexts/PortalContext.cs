using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Reflection;
using System.Security.Claims;
using YZPortal.Core.Domain.Database.Dealers;
using YZPortal.Core.Domain.Database.EntityTypes.Auditable;
using YZPortal.Core.Domain.Database.Memberships;
using YZPortal.Core.Domain.Database.Sync;
using YZPortal.Core.Domain.Database.Users;

namespace YZPortal.Core.Domain.Contexts
{
    public class PortalContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
    {
        private IDbContextTransaction? _currentTransaction;
        private readonly HttpContext? _httpContext;

        public PortalContext(DbContextOptions<PortalContext> options, IHttpContextAccessor httpContextAccessor) : base(options)
        {
            _httpContext = httpContextAccessor.HttpContext ?? null;
        }

        #region Data Sets

        #region Users

        public DbSet<UserPasswordReset> UserPasswordResets { get; set; }

        #endregion

        #region Memberships

        public DbSet<ContentAccessLevel> ContentAccessLevels { get; set; }
        public DbSet<Membership> Memberships { get; set; }
        public DbSet<MembershipDealerRole> MembershipDealerRoles { get; set; }
        public DbSet<MembershipContentAccessLevel> MembershipContentAccessLevels { get; set; }
        public DbSet<MembershipNotification> MembershipNotifications { get; set; }

        #endregion

        #region Dealers

        public DbSet<Dealer> Dealers { get; set; }
        public DbSet<DealerInvite> DealerInvites { get; set; }
        public DbSet<DealerRole> DealerRoles { get; set; }

        #endregion

        #region Sync

        public DbSet<SyncStatus> SyncStatuses { get; set; }

        #endregion

        #endregion

        #region DBContext Overrides
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Define keys for models
            DefineDomainModels(builder);

            // Configuration on interface for IAuditableEntity
            // Allows for default behaviours for AuditEntities during creation
            var configureAuditableMethod = GetType().GetTypeInfo().DeclaredMethods
                .Single(m => m.Name == nameof(OnCreateAuditableEntity));

            var args = new object[] { builder };

            var auditableEntityTypes = builder.Model.GetEntityTypes()
                .Where(t => typeof(AuditableEntity).IsAssignableFrom(t.ClrType));
            foreach (var entityType in auditableEntityTypes)
            {
                configureAuditableMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, args);
                // TO DO: extend auditable to include concurrenct, and etc...?
            }
        }

        private void DefineDomainModels(ModelBuilder builder)
        {
            #region Sync

            builder.Entity<SyncStatus>()
                .HasKey(x => new { x.Type, x.Name });

            #endregion

            #region Memberships

            #region Membership Dealer Role

            builder.Entity<MembershipDealerRole>()
                .HasKey(bc => new { bc.DealerRoleId, bc.MembershipId });
            builder.Entity<MembershipDealerRole>()
                .HasOne(bc => bc.DealerRole)
                .WithMany(b => b.MembershipDealerRoles)
                .HasForeignKey(bc => bc.DealerRoleId);
            builder.Entity<MembershipDealerRole>()
                .HasOne(bc => bc.Membership)
                .WithOne(c => c.MembershipDealerRole)
                .HasForeignKey<MembershipDealerRole>(bc => bc.MembershipId);

            // restrict deletion of dealer role when membership dealer role deleted or for statuses just dont cr8 a relationship..... omg
            #endregion

            #region Dealer Role

            builder.Entity<DealerRole>()
                .HasIndex(bc => new { bc.Name })
                .IsUnique();

            #endregion

            #region Membership Access Level

            builder.Entity<MembershipContentAccessLevel>()
                .HasKey(bc => new { bc.ContentAccessLevelId, bc.MembershipId });
            builder.Entity<MembershipContentAccessLevel>()
                .HasOne(bc => bc.ContentAccessLevel)
                .WithMany(b => b.MembershipContentAccessLevels)
                .HasForeignKey(bc => bc.ContentAccessLevelId);
            builder.Entity<MembershipContentAccessLevel>()
                .HasOne(bc => bc.Membership)
                .WithMany(c => c.MembershipContentAccessLevels)
                .HasForeignKey(bc => bc.MembershipId);

            #endregion

            #region Content Access Level

            builder.Entity<ContentAccessLevel>()
                .HasIndex(bc => new { bc.Name })
                .IsUnique();

            #endregion

            #endregion

            #region Dealers

            // Ref: https://dataschool.com/sql-optimization/how-indexing-works/
            builder.Entity<Dealer>()
                .HasIndex(bc => new { bc.Name })
                .IsUnique();

            #endregion
        }

        public override int SaveChanges()
        {
            OnCreateUpdateAuditEntries();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            OnCreateUpdateAuditEntries();
            return base.SaveChangesAsync(cancellationToken);
        }

        #endregion

        #region AuditableEntity Overrides CRUD

        // Behaviour for when creating and updating models
        private void OnCreateUpdateAuditEntries()
        {
            // Obtain entities from context in current intance
            var entries = ChangeTracker
                .Entries()
                .Where(e => e.Entity is AuditableEntity && (
                    e.State == EntityState.Added ||
                    e.State == EntityState.Modified ||
                    e.State == EntityState.Deleted));

            // Obtain user ID
            IEnumerable<Claim>? claims = _httpContext?.User?.Claims ?? null;
            var nameClaim = claims?.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;

            Guid currentUserId = string.IsNullOrEmpty(nameClaim) ? Guid.Empty : Guid.Parse(nameClaim);
            User? currentUser = Users?.FirstOrDefault(u => u.Id == currentUserId) ?? null;
            var identityName = currentUser?.Name ?? "Anonymous";

            // Track change and create timestamps
            foreach (var entityEntry in entries)
            {
                // General
                var auditableEntity = (AuditableEntity)entityEntry.Entity;

                auditableEntity.UpdatedDate = DateTime.UtcNow;
                auditableEntity.UpdatedBy = auditableEntity.UpdatedBy != null ? auditableEntity.UpdatedBy : identityName;

                // Add
                if (entityEntry.State == EntityState.Added)
                {
                    auditableEntity.CreatedDate = DateTime.UtcNow;
                    auditableEntity.CreatedBy = identityName;
                }

                // Default to english for translatble audit entities
                foreach (var translatableEnum in entries.Where(e => e.Entity is TranslatableEntity))
                {
                    var languageEntity = (TranslatableEntity)translatableEnum.Entity;

                    if (string.IsNullOrEmpty(languageEntity.LanguageCode))
                    {
                        languageEntity.LanguageCode = "en";
                    }
                }
            }
        }

        // Behaviour for when creating models
        private void OnCreateAuditableEntity<TEntity>(ModelBuilder modelBuilder) where TEntity : AuditableEntity
        {
            // Default values for IEntity
            modelBuilder.Entity<TEntity>().Property(e => e.Id).HasDefaultValueSql("newid()");

            // Default values for IAuditableEntity
            modelBuilder.Entity<TEntity>().Property(e => e.CreatedDate).HasDefaultValueSql("getutcdate()");
            modelBuilder.Entity<TEntity>().Property(e => e.CreatedBy).HasDefaultValue("unknown");
            modelBuilder.Entity<TEntity>().Property(e => e.UpdatedDate).HasDefaultValueSql("getutcdate()");
            modelBuilder.Entity<TEntity>().Property(e => e.UpdatedBy).HasDefaultValue("unknown");
        }

        #endregion

        #region Transaction Handling/ Pipeline Behaviour

        public void BeginTransaction()
        {
            if (_currentTransaction != null)
            {
                return;
            }
            else
            {
                _currentTransaction = Database.BeginTransaction(IsolationLevel.ReadCommitted);
            }
        }

        public void CommitTransaction()
        {
            try
            {
                _currentTransaction?.Commit();
            }
            catch
            {
                RollbackTransaction();
                throw;
            }
            finally
            {
                if (_currentTransaction != null)
                {
                    _currentTransaction.Dispose();
                    _currentTransaction = null;
                }
            }
        }

        public void RollbackTransaction()
        {
            try
            {
                _currentTransaction?.Rollback();
            }
            finally
            {
                if (_currentTransaction != null)
                {
                    _currentTransaction.Dispose();
                    _currentTransaction = null;
                }
            }
        }

        #endregion

    }
}
