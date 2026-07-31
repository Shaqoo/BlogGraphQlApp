using BlogGraphQlApp.DTOs;

namespace BlogGraphQlApp.Dtos
{
    public class ConversationDto
    {
        public Guid Id { get; set; }
        public MessageDto? LastMessage { get; set; }
        public ICollection<UserDto> Participants { get; set; } = [];
        public DateTime  UpdatedAt { get; set; }
        public int UnreadCount { get; set; }
    }
}