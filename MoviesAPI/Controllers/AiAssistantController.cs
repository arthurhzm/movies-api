using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoviesAPI.DTO;
using MoviesAPI.Services;

namespace MoviesAPI.Controllers;

public sealed class AiAssistantController : ControllerBase
{
    private const int MaxSearchQueryLength = 200;
    private const int MaxChatMessages = 20;
    private const int MaxChatMessageLength = 2_000;

    private readonly AiAssistantService _assistantService;

    public AiAssistantController(AiAssistantService assistantService)
    {
        _assistantService = assistantService;
    }

    [HttpPost("user/{userId}/assistant/search")]
    [Authorize]
    public async Task<IActionResult> Search(
        int userId,
        [FromBody] AiSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsCurrentUser(userId))
        {
            return Forbid();
        }

        var query = request.Query?.Trim();
        if (string.IsNullOrWhiteSpace(query) || query.Length > MaxSearchQueryLength)
        {
            return BadRequest(new { message = "Informe uma busca de até 200 caracteres." });
        }

        try
        {
            var results = await _assistantService.SearchMoviesAsync(query, cancellationToken);
            return Ok(new { data = results });
        }
        catch (HuggingFaceGenerationException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "A busca inteligente está indisponível no momento." });
        }
    }

    [HttpPost("user/{userId}/assistant/chat")]
    [Authorize]
    public async Task<IActionResult> Chat(
        int userId,
        [FromBody] AiChatRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsCurrentUser(userId))
        {
            return Forbid();
        }

        var messages = request.Messages;
        if (messages is null
            || messages.Count == 0
            || messages.Count > MaxChatMessages
            || messages.Any(message => string.IsNullOrWhiteSpace(message.Text) || message.Text.Length > MaxChatMessageLength))
        {
            return BadRequest(new { message = "A conversa enviada é inválida." });
        }

        try
        {
            var response = await _assistantService.GenerateChatResponseAsync(userId, messages, cancellationToken);
            return Ok(new { data = new { text = response } });
        }
        catch (HuggingFaceGenerationException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "O assistente está indisponível no momento." });
        }
    }

    private bool IsCurrentUser(int userId)
    {
        var claimValue = User.FindFirst(ClaimTypes.Name)?.Value;
        return int.TryParse(claimValue, out var authenticatedUserId) && authenticatedUserId == userId;
    }
}
