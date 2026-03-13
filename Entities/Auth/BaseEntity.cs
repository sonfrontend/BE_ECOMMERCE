namespace BE_TRELLO.Entities.Auth
{
    public class BaseEntity
    {
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; } = null;

        public bool IsActived {get;set;} = true;
    }
}