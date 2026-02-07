using System.Net.Http.Json;
using System.Diagnostics;

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

#if DEBUG
        if (!Debugger.IsAttached)
        {
            _error = "Click ricevuto, ma debugger NON agganciato al processo Web.";
            return;
        }

        Debugger.Break();
#endif

        try
        {
            var client = HttpClientFactory.CreateClient("Api");
            var resp = await client.PostAsJsonAsync("api/tickets", _model);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                _error = $"API error: {(int)resp.StatusCode} {resp.ReasonPhrase}. {body}";
                return;
            }

            Nav.NavigateTo("/tickets");
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
    }

    private void Cancel() => Nav.NavigateTo("/tickets");

    private class CreateTicketRequest
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public int Priority { get; set; }
    }
}
