using MongoDB.Bson;

namespace TeachersTimetable.Models
{
    public class User
    {
        public ObjectId Id { get; set; }
        public long UserId { get; set; }
        public string? Username { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Teacher { get; set; }
        public bool Notifications { get; set; }

        public User(long userId, string? username, string? firstName, string? lastName)
        {
            this.UserId = userId;
            this.Username = username;
            this.FirstName = firstName;
            this.LastName = lastName;
        }
    }
}