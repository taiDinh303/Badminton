using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.Repositories.Entity
{
    public class User
    {
        public int UserId { get; set; }

        public int RoleId { get; set; }

        public string FullName { get; set; } = null!;

        public string Email { get; set; } = null!;

        public string? PhoneNumber { get; set; }

        public string PasswordHash { get; set; } = null!;

        public string? AvatarUrl { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public string? Gender { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public Role Role { get; set; } = null!;
    }
}
