
namespace BE_TRELLO.Entities.Auth
{
    public class Users : BaseEntity
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } 
        public string PasswordHash { get; set; }
        public string Email { get; set; }
        public string GoogleId {get;set;}

    }
}