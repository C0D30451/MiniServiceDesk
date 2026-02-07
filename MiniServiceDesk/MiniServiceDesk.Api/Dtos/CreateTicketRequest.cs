namespace MiniServiceDesk.Api.Dtos
{
    public class CreateTicketRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = "IT";
        public int Priority { get; set; } = 1; 
    }
}