using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.Repositories.IOUW
{
    public interface IUOW
    {
        IUserRepository Users { get; }

        Task<int> SaveChangesAsync();
    }
}
