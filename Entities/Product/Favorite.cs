using System;
using BE_ECOMMERCE.Entities.Auth;

namespace BE_ECOMMERCE.Entities.Product
{
    public class Favorite : BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public virtual User User { get; set; }

        public string ProductId { get; set; }
        public virtual Product Product { get; set; }
    }
}
