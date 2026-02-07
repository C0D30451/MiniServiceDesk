using System.Net.Http.Json;

namespace MiniServiceDesk.Web.Pages;

public partial class NewTicket
{
    private string? _error;

    private CreateTicketRequest _model = new()
    {
        Category = "IT",
        Priority = 1
    };

    private async Task Save()
    {
        _error = null;

        try
        {
            var client = HttpClientFactory.CreateClient("Api");
            try
            {
                var resp = await client.PostAsJsonAsync("api/tickets", _model);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    _error = $"API error: {(int)resp.StatusCode} {resp.ReasonPhrase}. {body}";
                    return;
                }
            }
            catch (Exception ex)
            {
                _error = $"Error calling API: {ex.Message}";
                return;
            }

            Nav.NavigateTo("/tickets");
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
    }

    private async Task HandleSaveClick() => await Save();

    private class CreateTicketRequest
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public int Priority { get; set; }
    }
}
