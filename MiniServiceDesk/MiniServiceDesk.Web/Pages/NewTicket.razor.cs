using System.Net.Http.Json;

namespace MiniServiceDesk.Web.Pages;

public partial class NewTicket
{
    private const int TitleMinLength = 4;
    private const int TitleMaxLength = 120;
    private const int DescriptionMinLength = 10;
    private const int DescriptionMaxLength = 4000;

    private string? _error;

    private CreateTicketRequest _model = new()
    {
        Category = "IT",
        Priority = 1
    };

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !Auth.IsAuthenticated)
        {
            Nav.NavigateTo("/login", replace: true);
        }

        return Task.CompletedTask;
    }

    private async Task Save()
    {
        _error = null;

        if (!Auth.IsAuthenticated)
        {
            Nav.NavigateTo("/login");
            return;
        }

        _model.Title = _model.Title.Trim();
        _model.Description = _model.Description.Trim();
        _model.Category = _model.Category.Trim();

        if (string.IsNullOrWhiteSpace(_model.Title) || string.IsNullOrWhiteSpace(_model.Description))
        {
            _error = "Title e Description sono obbligatori.";
            return;
        }

        if (_model.Title.Length < TitleMinLength || _model.Title.Length > TitleMaxLength)
        {
            _error = $"Title deve avere tra {TitleMinLength} e {TitleMaxLength} caratteri.";
            return;
        }

        if (_model.Description.Length < DescriptionMinLength || _model.Description.Length > DescriptionMaxLength)
        {
            _error = $"Description deve avere tra {DescriptionMinLength} e {DescriptionMaxLength} caratteri.";
            return;
        }

        try
        {
            var client = HttpClientFactory.CreateClient("Api");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Auth.Token);

            try
            {
                var resp = await client.PostAsJsonAsync("api/tickets", _model);
                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Auth.Clear();
                    Nav.NavigateTo("/login");
                    return;
                }

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
