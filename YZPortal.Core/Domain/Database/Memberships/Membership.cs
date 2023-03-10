using System.ComponentModel.DataAnnotations;
using YZPortal.Core.Domain.Database.Dealers;
using YZPortal.Core.Domain.Database.EntityTypes.Auditable;
using YZPortal.Core.Domain.Database.Users;

namespace YZPortal.Core.Domain.Database.Memberships
{
    public class Membership : AuditableEntity
    {
        public User? User { get; set; }
		[Required]
		public Guid UserId { get; set; }
        public Dealer? Dealer { get; set; }
		[Required]
		public Guid DealerId { get; set; }
		// do i even need this/
		//public string ContextToken { get; set; }
		// User admin and membership admin is different!
		public bool Admin { get; set; } = false;
        public bool Disabled { get; set; } = false;
        public List<MembershipContentAccessLevel> MembershipContentAccessLevels { get; set; } = new List<MembershipContentAccessLevel>();
        public MembershipDealerRole? MembershipDealerRole { get; set; }
    }
}
